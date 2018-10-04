using System;
using UnityEngine;
using WebSocketSharp;

public delegate void SessionStartDelegate(string sessionId);
public delegate void SessionFinalDelegate();

public class SessionSocket : WebSocket {

    public SessionStartDelegate OnSessionStarted;
    public SessionFinalDelegate OnGameEnded;

    private Data liveGameData;

    public SessionSocket(Data data) : base(data.game.socketUrl) {
        this.liveGameData = data;

        OnOpen += OnOpenHandler;
        OnMessage += OnMessageHandler;
        OnClose += OnCloseHandler;
        OnError += OnErrorHandler;
    }

    void OnOpenHandler(object sender, EventArgs e)
    {
        string message = "Session WebSocket connected!";
        Debug.Log(message);
        SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + liveGameData.game.facilityId + "\",\"source\": \"facility\"}",
            OnSendComplete
        );
    }

    void OnMessageHandler(object sender, MessageEventArgs e)
    {
        string message = "Session WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);

        if ("SESSION".Equals(entity.data.type))
        {
            if ("STARTED".Equals(entity.data.data.status) && OnSessionStarted != null) 
            {
                string newSessionId = entity.data.data.sessionId;
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnSessionStarted(newSessionId));
            }
        } 
        else if ("GAME".Equals(entity.data.type))
        {
            if ("GAME_END".Equals(entity.data.data.status) && OnGameEnded != null) 
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnGameEnded());
            }
        }
    }

    void OnCloseHandler(object sender, CloseEventArgs e)
    {
        string message = "Session WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006)
        {
            ConnectAsync();
        }
    }

    void OnErrorHandler(object sender, ErrorEventArgs e)
    {
        string message = "Session WebSocket connection failure: " + e.Message;
        Debug.Log(message);
    }

    void OnSendComplete(bool success)
    {
        string message = "Message sent successfully? " + success;
        //Debug.Log(message);
    }
}