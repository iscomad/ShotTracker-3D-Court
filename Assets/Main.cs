using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System.Threading;
using System;
using System.Collections.Generic;

public class Main : MonoBehaviour
{
    public Text logText;
    public GameObject ball;
    public GameObject team1Pool;
    public GameObject team2Pool;

    private WebSocket ws;
    private string sessionId;
    private string socketUrl;
    private const float WIDTH = 26440f;
    private const float HEIGHT = 14760f;
    private Color team1Color;
    private Color team2Color;

    private Dictionary<string, GameObject> team1Dict = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> team2Dict = new Dictionary<string, GameObject>();
    private string[] team1Numbers;
    private string[] team2Numbers;

    void Start()
    {
        // setting custom jersey colors. It works!
        //team1Pool.transform.GetChild(4).transform.GetChild(0).GetComponent<Renderer>().material.SetColor("_Color", Color.green);
        //team1Pool.transform.GetChild(4).transform.GetChild(1).GetComponent<TextMesh>().color = Color.black;

        Debug.Log("start");
        logText.text = "start\n";

        //AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        //AndroidJavaObject currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        //AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent");
        //bool hasExtra = intent.Call<bool>("hasExtra", "socket_url");

        socketUrl = "ws://devapp.shottracker.com/live?start_token=739ae998-07d6-4e2a-9974-66578b3ea742";
        sessionId = "8072b047-b6ac-11e8-b7bf-02424a95ad15";
        ColorUtility.TryParseHtmlString("#00ff00", out team1Color);
        ColorUtility.TryParseHtmlString("#bb2211", out team2Color);
        team1Numbers = new string[] { "11", "30", "0", "10", "5" };
        team2Numbers = new string[] { "0", "1", "2", "22", "19" };
        //if (hasExtra)
        //{
            //AndroidJavaObject extras = intent.Call<AndroidJavaObject>("getExtras");
            //socketUrl = extras.Call<string>("getString", "socket_url");
            //sessionId = extras.Call<string>("getString", "session_id");

            //string playerIdsRaw = extras.Call<string>("getString", "player_ids_1");
        string playerIdsRaw = "3664,3728,3999,4000";
            ReadPlayerIds(team1Dict, playerIdsRaw.Split(','), team1Pool);

            //playerIdsRaw = extras.Call<string>("getString", "player_ids_2");
        playerIdsRaw = "2902,2903,2904,2905,2906";
            ReadPlayerIds(team2Dict, playerIdsRaw.Split(','), team2Pool);

            SetupPlayers();
        //}

        logText.text += socketUrl + '\n' + sessionId + '\n';
        StartSocket();

        ws.ConnectAsync();
    }

    private void SetupPlayers()
    {
        SetupTeam(team1Pool, team1Color, team1Numbers);
        SetupTeam(team2Pool, team2Color, team2Numbers);
    }

    private void SetupTeam(GameObject teamPool, Color jerseyColor, string[] jerseyNumbers)
    {
        for (int i = 0; i < teamPool.transform.childCount; i++)
        {
            Transform playerObject = teamPool.transform.GetChild(i);
            playerObject.transform.GetChild(0).GetComponent<Renderer>().material.color = jerseyColor;
            playerObject.transform.GetChild(1).GetComponent<TextMesh>().text = jerseyNumbers[i];
            playerObject.transform.GetChild(2).GetComponent<TextMesh>().text = jerseyNumbers[i];
        }
    }

    private void ReadPlayerIds(Dictionary<string, GameObject> players, 
                               string[] playerIds, GameObject playersPool)
    {
        int playersCount = playersPool.transform.childCount;
        for (int i = 0; i < Math.Min(playersCount, playerIds.Length); i++)
        {
            players.Add(playerIds[i], playersPool.transform.GetChild(i).gameObject);
        }
    }

    private void StartSocket()
    {
        ws = new WebSocket(socketUrl);

        ws.OnOpen += OnOpenHandler;
        ws.OnMessage += OnMessageHandler;
        ws.OnClose += OnCloseHandler;
        ws.OnError += OnErrorHandler;
    }

    private void OnErrorHandler(object sender, ErrorEventArgs e)
    {
        string message = "WebSocket connection failure: " + e.Message;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n' );
    }

    private void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "WebSocket connected!";
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
        Thread.Sleep(3000);
        ws.SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"court\"}",
            OnSendComplete
        );
    }

    private void OnMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "WebSocket server said: " + e.Data;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            int startIndex = Math.Max(0, logText.text.Length - 500);
            logText.text = logText.text.Substring(startIndex) + message + '\n';
        });
    
        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        Entity entity = JsonUtility.FromJson<Entity>(jsonString);
        if (entity.data.bid > 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetBallPosition(entity.data.y, entity.data.x));
        } else if (entity.data.pid > 0) {
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetPlayerPosition(entity.data.pid + "", entity.data.y, entity.data.x));
        }

    }

    private void SetPlayerPosition(string playerId, int x, int y)
    {
        if (team1Dict.ContainsKey(playerId)) {
            Debug.LogWarning("Setting team 1 player position");
            SetGameObjectPosition(team1Dict[playerId], x, y);
        } else if (team2Dict.ContainsKey(playerId)) {
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
        float xNew = x / WIDTH * 9 * 2;
        float zNew = y / HEIGHT * 5 * -2;
        float yNew = gObject.transform.position.y;
        gObject.transform.position = new Vector3(xNew, yNew, zNew);
    }

    private void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => logText.text += message + '\n');
        if (e.Code == 1006) {
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

[Serializable]
public class Entity
{
    public string source;
    public Data data;
}

[Serializable]
public class Data
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
