using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;

public class Main : MonoBehaviour
{
    public GameObject team1Pool;
    public GameObject team2Pool;
    public GameObject scoreBoard;
    public GameObject ballsPool;
    public GameObject basket1;
    public GameObject basket2;
    public GameObject audioObject;

    CourtSocket courtSocket;
    WebSocket statsSocket;
    WebSocket chartsSocket;
    WebSocket sessionSocket;
    MakeMissSoundScript makeMissSoundScript;
    const float WIDTH = 26440f;
    const float HEIGHT = 14760f;

    Dictionary<string, GameObject> team1Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> team2Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> ballDict = new Dictionary<string, GameObject>();

    Data liveGameData;

    Text sessionText;
    Text team1ScoreText;
    Text team2ScoreText;

    void Start()
    {
        makeMissSoundScript = audioObject.GetComponent<MakeMissSoundScript>();

        sessionText = scoreBoard.transform.Find("Session/Text").GetComponent<Text>();
        team1ScoreText = scoreBoard.transform.Find("Score1").GetComponent<Text>();
        team2ScoreText = scoreBoard.transform.Find("Score2").GetComponent<Text>();

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
        StartStatsSocket();
        StartChartsSocket();
        StartSessionSocket();
    }

    void SetupScoreBoard(Data data)
    {
        if (data == null) return;

        scoreBoard.transform.Find("Name1").GetComponent<Text>().text = data.team1.name;
        scoreBoard.transform.Find("Name2").GetComponent<Text>().text = data.team2.name;

        Image image = scoreBoard.transform.Find("Logo1/Mask/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team1.logoUrl));

        image = scoreBoard.transform.Find("Logo2/Mask/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team2.logoUrl));

        SetScore(data.team1.id, data.game.score1);
        SetScore(data.team2.id, data.game.score2);

        SetSession();
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

    void StartCourtSocket() 
    {
        courtSocket = new CourtSocket(liveGameData) 
        {
            OnPlayerPositionChanged = SetPlayerPosition,
            OnBallPositionChanged = SetBallPosition
        };
        courtSocket.ConnectAsync();
    }

    void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        //Debug.Log(message);
    }

    #region Stats Socket
    void StartStatsSocket()
    {
        if (statsSocket == null)
        {
            statsSocket = new WebSocket(liveGameData.game.socketUrl);

            statsSocket.OnOpen += OnStatsWsOpenHandler;
            statsSocket.OnMessage += OnStatsWsMessageHandler;
            statsSocket.OnClose += OnStatsWsCloseHandler;
            statsSocket.OnError += OnStatsWsErrorHandler;
        }
        if (statsSocket.IsAlive) 
        {
            statsSocket.Close();
        }

        statsSocket.ConnectAsync();
    }

    void OnStatsWsOpenHandler(object sender, EventArgs e)
    {
        string message = "Stats WebSocket connected!";
        Debug.Log(message);
        SendStatsSessionMessage();
    }

    void SendStatsSessionMessage()
    {
        string sessionId = liveGameData.game.sessions[liveGameData.game.sessions.Length - 1];
        statsSocket.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"stats\"}",
            OnSendComplete
        );
    }

    void OnStatsWsMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Stats WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        float score = entity.data.stats.TEAM_SCORE;
        int teamId = entity.data.tid;
        if (teamId > 0 && score >= 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SetScore(teamId.ToString(), ((int)score).ToString());
            });
        }
    }

    void OnStatsWsCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Stats WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            StartStatsSocket();
        }
    }

    void OnStatsWsErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "Stats WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }
    #endregion

    #region Charts Socket
    void StartChartsSocket()
    {
        if (chartsSocket == null)
        {
            chartsSocket = new WebSocket(liveGameData.game.socketUrl);

            chartsSocket.OnOpen += OnChartsWsOpenHandler;
            chartsSocket.OnMessage += OnChartsWsMessageHandler;
            chartsSocket.OnClose += OnChartsWsCloseHandler;
            chartsSocket.OnError += OnChartsWsErrorHandler;
        }
        if (chartsSocket.IsAlive)
        {
            chartsSocket.Close();
        }

        chartsSocket.ConnectAsync();
    }

    void OnChartsWsOpenHandler(object sender, EventArgs e)
    {
        string message = "Charts WebSocket connected!";
        Debug.Log(message);
        SendChartsSessionMessage();
    }

    void SendChartsSessionMessage()
    {
        string sessionId = liveGameData.game.sessions[liveGameData.game.sessions.Length - 1];
        chartsSocket.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"chart\"}",
            OnSendComplete
        );
    }

    void OnChartsWsMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Charts WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        int teamId = entity.data.tid;
        string shotType = entity.data.st;
        if (shotType != null && !shotType.IsNullOrEmpty()) {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                MakeMissAnimationScript script = null;
                if (liveGameData.team1.id.Equals(teamId + "")) {
                    script = basket2.GetComponent<MakeMissAnimationScript>();
                } else if (liveGameData.team2.id.Equals(teamId + "")) {
                    script = basket1.GetComponent<MakeMissAnimationScript>();
                }
                if (script != null) {
                    if (shotType.Equals("MAKE")) {
                        script.AnimateMake();
                        makeMissSoundScript.PlayMakeAudio();
                    }
                    else {
                        script.AnimateMiss();
                        makeMissSoundScript.PlayMissAudio();
                    }

                }
            });
        }
    }

    void OnChartsWsCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Charts WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            StartChartsSocket();
        }
    }

    void OnChartsWsErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "Charts WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }
    #endregion

    #region Session Socket
    void StartSessionSocket() 
    {
        if (sessionSocket == null) 
        {
            sessionSocket = new WebSocket(liveGameData.game.socketUrl);

            sessionSocket.OnOpen += OnSessionWsOpenHandler;
            sessionSocket.OnMessage += OnSessionWsMessageHandler;
            sessionSocket.OnClose += OnSessionWsCloseHandler;
            sessionSocket.OnError += OnSessionWsErrorHandler;
        }
        if (sessionSocket.IsAlive)
        {
            sessionSocket.Close();
        }

        sessionSocket.ConnectAsync();
    }

    void OnSessionWsOpenHandler(object sender, EventArgs e)
    {
        string message = "Session WebSocket connected!";
        Debug.Log(message);
        sessionSocket.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + liveGameData.game.facilityId + "\",\"source\": \"facility\"}",
            OnSendComplete
        );
    }

    void OnSessionWsMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Session WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        if ("SESSION".Equals(entity.data.type))
        {
            if ("STARTED".Equals(entity.data.data.status)) 
            {
                string newSessionId = entity.data.data.sessionId;
                int newSize = liveGameData.game.sessions.Length + 1;
                Array.Resize(ref liveGameData.game.sessions, newSize);
                liveGameData.game.sessions[newSize - 1] = newSessionId;

                Debug.LogWarning("new sessions: " + string.Join(", ", liveGameData.game.sessions));
                UnityMainThreadDispatcher.Instance().Enqueue(OnSessionChanged);
            }
        } 
        else if ("GAME".Equals(entity.data.type))
        {
            if ("GAME_END".Equals(entity.data.data.status)) 
            {
                UnityMainThreadDispatcher.Instance().Enqueue(SetGameEnded);
            }
        }
    }

    void OnSessionChanged()
    {
        SetSession();
        courtSocket.SubscribeToCourt();
        SendStatsSessionMessage();
    }

    void OnSessionWsCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Session WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            StartStatsSocket();
        }
    }

    void OnSessionWsErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "Session WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }
    #endregion

    void SetGameEnded()
    {
        SetSessionText("final");
    }

    void SetSession()
    {
        SetSessionText(GetSessionText(liveGameData.game.roundType, liveGameData.game.sessions));
    }

    void SetSessionText(string text) 
    {
        sessionText.text = text;
    }

    string GetSessionText(string type, string[] sessions)
    {
        string text = (sessions.Length) + "";
        if ("HALF".Equals(type))
        {
            if (sessions.Length > 2) 
            {
                return "OT";
            }
            text += "H";
        }
        else
        {
            if (sessions.Length > 4)
            {
                return "OT";
            }
            text += "Q";
        }

        return text;
    }

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

    void SetBallPosition(string id, int x, int y)
    {
        GameObject ball = null;
        if (!ballDict.ContainsKey(id))
        {
            for (int i = 0; i < ballsPool.transform.childCount; i++) 
            {
                GameObject newBall = ballsPool.transform.GetChild(i).gameObject;
                if (!ballDict.ContainsValue(newBall)) 
                {
                    ball = newBall;
                    break;
                }
            }
            if (ball == null) 
            {
                Debug.LogError("No balls left in the pool");
                return;
            }

            ballDict.Add(id, ball);
        } else {
            ball = ballDict[id];
        }
        SetGameObjectPosition(ball, x, y);
    }

    void SetGameObjectPosition(GameObject gObject, int x, int y)
    {
        Court court = liveGameData.court;
        float xNew = (float)x / court.width * 9 * 2;
        float zNew = (float)y / court.height * 5 * -2;
        float yNew = gObject.transform.position.y;
        Vector3 target = new Vector3(xNew, yNew, zNew);
        //gObject.transform.position = target;
        StartCoroutine(MoveObject(gObject, target, 0.3f));
    }

    void SetScore(string teamId, string score)
    {
        if (teamId.Equals(liveGameData.team1.id))
        {
            team1ScoreText.text = score;
        }
        else if (teamId.Equals(liveGameData.team2.id))
        {
            team2ScoreText.text = score;
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

    IEnumerator MoveObject(GameObject gObject, Vector3 target, float duration)
    {
        Vector3 source = gObject.transform.position;
        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            gObject.transform.position = Vector3.Lerp(source, target, (Time.time - startTime) / duration);
            yield return null;
        }
        gObject.transform.position = target;
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
    public string st;
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
    public string[] sessions;
    public string facilityId;
    public string roundType;

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