using System;
using UnityEngine;
using WebSocketSharp;

public delegate void ChartMakeDelegate(string tid);
public delegate void ChartMissDelegate(string tid);

public class ChartSocket : WebSocket {

    public ChartMakeDelegate OnMake;
    public ChartMissDelegate OnMiss;

    private Data liveGameData;

    public ChartSocket(Data data) : base(data.game.socketUrl) {
        this.liveGameData = data;

        OnOpen += OnOpenHandler;
        OnMessage += OnMessageHandler;
        OnClose += OnCloseHandler;
        OnError += OnErrorHandler;
    }

    public void SubscribeToChart()
    {
        string sessionId = liveGameData.game.sessions[liveGameData.game.sessions.Length - 1];
        SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"chart\"}",
            OnSendComplete
        );
    }

    void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "Charts WebSocket connected!";
        Debug.Log(message);
        SubscribeToChart();
    }

    void OnMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Charts WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        string teamId = entity.data.tid + "";
        string shotType = entity.data.st;
        if (shotType != null && !shotType.IsNullOrEmpty()) {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (shotType.Equals("MAKE") && OnMake != null) {
                    OnMake(teamId);
                }
                else if (shotType.Equals("MISS") && OnMiss != null) {
                    OnMiss(teamId);
                }
            });
        }
    }

    void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Charts WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            ConnectAsync();
        }
    }

    void OnErrorHandler(object sender, ErrorEventArgs e)
    {
        string message = "Charts WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }

    void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        //Debug.Log(message);
    }
}