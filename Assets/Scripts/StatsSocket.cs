using System;
using UnityEngine;
using WebSocketSharp;

public delegate void StatsScoreDelegate(string tid, string score);

public class StatsSocket : WebSocket {

    public StatsScoreDelegate OnScoreChanged;

    private Data liveGameData;
    public StatsSocket(Data data) : base(data.game.socketUrl) {
        this.liveGameData = data;

        OnOpen += OnOpenHandler;
        OnMessage += OnMessageHandler;
        OnClose += OnCloseHandler;
        OnError += OnErrorHandler;
    }

    public void SubscribeToStats()
    {
        string sessionId = liveGameData.game.sessions[liveGameData.game.sessions.Length - 1];
        SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"stats\"}",
            OnSendComplete
        );
    }

    void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "Stats WebSocket connected!";
        Debug.Log(message);
        SubscribeToStats();
    }

    void OnMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Stats WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        float score = entity.data.stats.TEAM_SCORE;
        int teamId = entity.data.tid;
        if (teamId > 0 && score >= 0)
        {
            if (OnScoreChanged != null) {
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnScoreChanged(teamId.ToString(), ((int)score).ToString())
                );
            }
        }
    }

    void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Stats WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            ConnectAsync();
        }
    }

    void OnErrorHandler(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        string message = "Stats WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }

    void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        //Debug.Log(message);
    }
}