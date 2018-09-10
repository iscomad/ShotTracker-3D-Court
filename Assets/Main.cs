﻿using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System.Threading;
using System;

public class Main : MonoBehaviour
{
    public Text logText;
    public GameObject ball;

    private WebSocket ws;
    private string sessionId;
    private string socketUrl;
    private const float WIDTH = 26440f;
    private const float HEIGHT = 14760f;

    void Start()
    {
        Debug.Log("start");
        logText.text = "start\n";

        //AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        //AndroidJavaObject currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        //AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent");
        //bool hasExtra = intent.Call<bool>("hasExtra", "socket_url");

        socketUrl = "ws://devapp.shottracker.com/live?start_token=4330d84b-3ff6-4290-8f0c-2bb65d49faf0";
        sessionId = "ac9b64e5-b50c-11e8-8c97-0242fd01d15f";
        //if (hasExtra)
        //{
        //    AndroidJavaObject extras = intent.Call<AndroidJavaObject>("getExtras");
        //    socketUrl = extras.Call<string>("getString", "socket_url");
        //    sessionId = extras.Call<string>("getString", "session_id");
        //}

        logText.text += socketUrl + '\n' + sessionId + '\n';
        StartSocket();

        ws.ConnectAsync();
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
        }

    }

    private void SetBallPosition(int x, int y)
    {
        float xNew = x / WIDTH * 9 * 2;
        float zNew = y / HEIGHT * 5 * -2;
        float yNew = ball.transform.position.y;
        ball.transform.position = new Vector3(xNew, yNew, zNew);
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
