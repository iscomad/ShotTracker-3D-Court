using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;

public class Main : MonoBehaviour
{
    public GameObject ball;
    public GameObject team1Pool;
    public GameObject team2Pool;
    public GameObject scoreBoard;

    WebSocket courtSocket;
    WebSocket scoreSocket;
    const float WIDTH = 26440f;
    const float HEIGHT = 14760f;

    Dictionary<string, GameObject> team1Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> team2Dict = new Dictionary<string, GameObject>();

    Data liveGameData;

    void Start()
    {
        AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        string liveGameDataRaw = null;
        if (currentActivity != null)
        {
            AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent");
            bool hasExtra = intent.Call<bool>("hasExtra", "live_game_data");
            if (hasExtra)
            {
                AndroidJavaObject extras = intent.Call<AndroidJavaObject>("getExtras");
                liveGameDataRaw = extras.Call<string>("getString", "live_game_data");
            }
        }
        if (liveGameDataRaw == null)
        {
            liveGameDataRaw = ReadFromTestFile();
        }

        liveGameData = JsonUtility.FromJson<Data>(liveGameDataRaw);

        SetupTeam(liveGameData.team1, team1Dict, team1Pool);
        SetupTeam(liveGameData.team2, team2Dict, team2Pool);
        SetupScoreBoard(liveGameData);

        StartCourtSocket();
        StartScoreSocket();
    }

    void SetupScoreBoard(Data data)
    {
        if (data == null) return;

        scoreBoard.transform.Find("Name1").GetComponent<Text>().text = data.team1.name;
        scoreBoard.transform.Find("Name2").GetComponent<Text>().text = data.team2.name;

        Image image = scoreBoard.transform.Find("Logo1/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team1.logoUrl));

        image = scoreBoard.transform.Find("Logo2/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team2.logoUrl));

        SetScore(data.team1.id, data.game.score1);
        SetScore(data.team2.id, data.game.score2);
    }

    string ReadFromTestFile()
    {
        string path = "Assets/Resources/TestLiveGameData.txt";

        StreamReader reader = new StreamReader(path);
        string text = reader.ReadToEnd();
        reader.Close();

        return text;
    }

    Color GetFontColor(Color jerseyColor)
    {
        // Based on Luma constants (see https://en.wikipedia.org/wiki/Luma_%28video%29)
        double threshold = 0.2126 * jerseyColor.r + 0.7152 * jerseyColor.g + 0.0722 * jerseyColor.b;
        return threshold < 0.67 ? Color.white : Color.black;
    }

    void SetupTeam(Team team, Dictionary<string, GameObject> teamDict,
                               GameObject playersPool)
    {
        int playersCount = playersPool.transform.childCount;
        Color jerseyColor = new Color();
        ColorUtility.TryParseHtmlString(team.jerseyColor, out jerseyColor);
        Color fontColor = GetFontColor(jerseyColor);
        for (int i = 0; i < Math.Min(playersCount, team.players.Length); i++)
        {
            GameObject playerObject = playersPool.transform.GetChild(i).gameObject;
            playerObject.transform.GetChild(0).GetComponent<Renderer>().material.color = jerseyColor;
            playerObject.transform.GetChild(1).GetComponent<TextMesh>().text = team.players[i].number;
            playerObject.transform.GetChild(1).GetComponent<TextMesh>().color = fontColor;
            playerObject.transform.GetChild(2).GetComponent<TextMesh>().text = team.players[i].number;
            playerObject.transform.GetChild(2).GetComponent<TextMesh>().color = fontColor;
            teamDict.Add(team.players[i].id, playerObject);
        }
    }

    #region Court Socket
    void StartCourtSocket()
    {
        courtSocket = new WebSocket(liveGameData.game.socketUrl);

        courtSocket.OnOpen += OnOpenHandler;
        courtSocket.OnMessage += OnMessageHandler;
        courtSocket.OnClose += OnCloseHandler;
        courtSocket.OnError += OnErrorHandler;

        courtSocket.ConnectAsync();
    }

    void OnErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "WebSocket connection failure: " + e.Message;
        //Debug.Log(message);
    }

    void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "WebSocket connected!";
        //Debug.Log(message);
        courtSocket.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + liveGameData.game.sessionId + "\",\"source\": \"court\"}",
            OnSendComplete
        );
    }

    void OnMessageHandler(object sender, WebSocketSharp.MessageEventArgs e)
    {
        string message = "WebSocket server said: " + e.Data;
        //Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);
        if (entity.data.bid > 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetBallPosition(entity.data.y, entity.data.x));
        }
        else if (entity.data.pid > 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetPlayerPosition(entity.data.pid + "", entity.data.y, entity.data.x));
        }
    }

    void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        //Debug.Log(message);
        if (e.Code == 1006)
        {
            StartCourtSocket();
        }
    }

    void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        //Debug.Log(message);
    }
    #endregion

    #region Score Socket
    void StartScoreSocket()
    {
        scoreSocket = new WebSocket(liveGameData.game.socketUrl);

        scoreSocket.OnOpen += OnScoreWsOpenHandler;
        scoreSocket.OnMessage += OnScoreWsMessageHandler;
        scoreSocket.OnClose += OnScoreWsCloseHandler;
        scoreSocket.OnError += OnScoreWsErrorHandler;

        scoreSocket.ConnectAsync();
    }

    void OnScoreWsOpenHandler(object sender, EventArgs e)
    {
        string message = "Score WebSocket connected!";
        Debug.Log(message);
        scoreSocket.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + liveGameData.game.sessionId + "\",\"source\": \"stats\"}",
            OnSendComplete
        );
    }

    void OnScoreWsMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Score WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        float score = entity.data.stats.TEAM_SCORE;
        if (entity.data.tid > 0 && score >= 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SetScore(entity.data.tid.ToString(), ((int)score).ToString());
            });
        }
    }

    void OnScoreWsCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Score WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            StartScoreSocket();
        }
    }

    void OnScoreWsErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "Score WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }
    #endregion

    void SetPlayerPosition(string playerId, int x, int y)
    {
        if (team1Dict.ContainsKey(playerId))
        {
            SetGameObjectPosition(team1Dict[playerId], x, y);
        }
        else if (team2Dict.ContainsKey(playerId))
        {
            SetGameObjectPosition(team2Dict[playerId], x, y);
        }
    }

    void SetBallPosition(int x, int y)
    {
        SetGameObjectPosition(ball, x, y);
    }

    void SetGameObjectPosition(GameObject gObject, int x, int y)
    {
        Court court = liveGameData.court;
        float xNew = (float)x / court.width * 9 * 2;
        float zNew = (float)y / court.height * 5 * -2;
        float yNew = gObject.transform.position.y;
        gObject.transform.position = new Vector3(xNew, yNew, zNew);
    }

    void SetScore(string teamId, string score)
    {
        Text text = null;
        if (teamId.Equals(liveGameData.team1.id))
        {
            text = scoreBoard.transform.Find("Score1").GetComponent<Text>();
        }
        else if (teamId.Equals(liveGameData.team2.id))
        {
            text = scoreBoard.transform.Find("Score2").GetComponent<Text>();
        }
        if (text != null)
        {
            text.text = score;
        }
    }

    IEnumerator LoadImage(Image uiImage, string loadedURL)
    {
        Texture2D temp = new Texture2D(0, 0);
        WWW www = new WWW(loadedURL);
        yield return www;

        temp = www.texture;
        if (temp != null) 
        { 
            Sprite sprite = Sprite.Create(temp, new Rect(0, 0, temp.width, temp.height), new Vector2(0.5f, 0.5f));
            uiImage.sprite = sprite;
        }
    }
}

#region Data Objects
[Serializable]
public class SocketEntity
{
    public string source;
    public SocketData data;
}

[Serializable]
public class SocketData
{
    public int tid;
    public long bid;
    public int pid;
    public int x;
    public int y;
    public string type;
    public EventData data;
    public Stat stats;

    [Serializable]
    public class EventData
    {
        public string sessionId;
        public string status;
        public string gameId;
    }

    [Serializable]
    public class Stat
    {
        public float TEAM_SCORE = -1;
    }
}

[Serializable]
public class Data
{
    public Game game;
    public Court court;
    public Team team1;
    public Team team2;
}

[Serializable]
public class Game
{
    public string id;
    public string socketUrl;
    public string sessionId;

    public string score1;
    public string score2;
}

[Serializable]
public class Court
{
    public string id;
    public string name;
    public int width;
    public int height;
}

[Serializable]
public class Team
{
    public string id;
    public string name;
    public string jerseyColor;
    public string logoUrl;
    public Player[] players;
}

[Serializable]
public class Player
{
    public string id;
    public string name;
    public string number;
}
#endregion