using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System.Threading;
using System;
using System.Collections.Generic;
using System.IO;

public class Main : MonoBehaviour
{
    public Text logText;
    public GameObject ball;
    public GameObject team1Pool;
    public GameObject team2Pool;

    WebSocket ws;
    const float WIDTH = 26440f;
    const float HEIGHT = 14760f;

    Dictionary<string, GameObject> team1Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> team2Dict = new Dictionary<string, GameObject>();

    Data liveGameData;

    void Start()
    {
        Debug.Log("start");
        logText.text = "start\n";

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
        if (liveGameDataRaw == null) {
            liveGameDataRaw = ReadFromTestFile();
        }

        logText.text = liveGameDataRaw;
        liveGameData = JsonUtility.FromJson<Data>(liveGameDataRaw);

        SetupTeam(liveGameData.team1, team1Dict, team1Pool);
        SetupTeam(liveGameData.team2, team2Dict, team2Pool);

        logText.text += liveGameData.game.socketUrl + '\n' + liveGameData.game.sessionId + '\n';
        StartSocket();

        ws.ConnectAsync();
    }

    private string ReadFromTestFile()
    {
        string path = "Assets/Resources/TestLiveGameData.txt";

        StreamReader reader = new StreamReader(path);
        string text = reader.ReadToEnd();
        reader.Close();

        return text;
    }

    private Color GetFontColor(Color jerseyColor)
    {
        // Based on Luma constants (see https://en.wikipedia.org/wiki/Luma_%28video%29)
        double threshold = 0.2126 * jerseyColor.r + 0.7152 * jerseyColor.g + 0.0722 * jerseyColor.b;
        return threshold < 0.67 ? Color.white : Color.black;
    }

    private void SetupTeam(Team team, Dictionary<string, GameObject> teamDict,
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

    private void StartSocket()
    {
        ws = new WebSocket(liveGameData.game.socketUrl);

        ws.OnOpen += OnOpenHandler;
        ws.OnMessage += OnMessageHandler;
        ws.OnClose += OnCloseHandler;
        ws.OnError += OnErrorHandler;
    }

    private void OnErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "WebSocket connection failure: " + e.Message;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
    }

    private void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "WebSocket connected!";
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
        Thread.Sleep(3000);
        ws.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + liveGameData.game.sessionId + "\",\"source\": \"court\"}",
            OnSendComplete
        );
    }

    private void OnMessageHandler(object sender, WebSocketSharp.MessageEventArgs e)
    {
        string message = "WebSocket server said: " + e.Data;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            int startIndex = Math.Max(0, logText.text.Length - 500);
            logText.text = logText.text.Substring(startIndex) + message + '\n';
        });

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

    private void SetPlayerPosition(string playerId, int x, int y)
    {
        if (team1Dict.ContainsKey(playerId))
        {
            Debug.LogWarning("Setting team 1 player position");
            SetGameObjectPosition(team1Dict[playerId], x, y);
        }
        else if (team2Dict.ContainsKey(playerId))
        {
            Debug.LogWarning("Setting team 2 player position");
            SetGameObjectPosition(team2Dict[playerId], x, y);
        }
    }

    private void SetBallPosition(int x, int y)
    {
        Debug.LogWarning("Setting BALL position");
        SetGameObjectPosition(ball, x, y);
    }

    private void SetGameObjectPosition(GameObject gObject, int x, int y)
    {
        Court court = liveGameData.court;
        float xNew = (float) x / court.width * 9 * 2;
        float zNew = (float) y / court.height * 5 * -2;
        float yNew = gObject.transform.position.y;
        gObject.transform.position = new Vector3(xNew, yNew, zNew);
        Debug.Log("new position for a player (" + xNew + ", " + zNew + ")");
        Debug.Log("court size (" + court.width + ", " + court.height + ")");
    }

    private void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
        if (e.Code == 1006)
        {
            StartSocket();
        }
    }

    private void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
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

    [Serializable]
    public class EventData
    {
        public string sessionId;
        public string status;
        public string gameId;
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