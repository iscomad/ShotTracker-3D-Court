﻿using System;
using UnityEngine;
using WebSocketSharp;

public delegate void CourtPositionDelegate(string id, int x, int y);

public class CourtSocket : WebSocket {

    public CourtPositionDelegate OnPlayerPositionChanged;
    public CourtPositionDelegate OnBallPositionChanged;

    private Data liveGameData;

    public CourtSocket(Data data) : base(data.game.socketUrl) {
        this.liveGameData = data;

        OnOpen += OnOpenHandler;
        OnMessage += OnMessageHandler;
        OnClose += OnCloseHandler;
        OnError += OnErrorHandler;
    }

    public void SubscribeToCourt()
    {
        string sessionId = liveGameData.game.sessions[liveGameData.game.sessions.Length - 1];
        SendAsync(
            "{ \"action\": \"subscribe\",\"sessionId\": \"" + sessionId + "\",\"source\": \"court\"}",
            OnSendComplete
        );
    }

    void OnOpenHandler(object sender, EventArgs e) {
        //string message = "WebSocket connected!";
        //Debug.Log(message);
        SubscribeToCourt();
    }

    void OnMessageHandler(object sender, MessageEventArgs e) {
        //string message = "WebSocket server said: " + e.Data;
        //Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);
        if (entity.data.bid > 0) {
            if (OnBallPositionChanged != null) {
                string bid = entity.data.bid + "";
                int x = entity.data.y;
                int y = entity.data.x;
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnBallPositionChanged(bid, x, y)
                );
            }
        }
        else if (entity.data.pid > 0) {
            if (OnPlayerPositionChanged != null) {
                string pid = entity.data.pid + "";
                int x = entity.data.y;
                int y = entity.data.x;
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnPlayerPositionChanged(pid, x, y)
                );
            }
        }
    }

    void OnCloseHandler(object sender, CloseEventArgs e) {
        string message = "WebSocket closed with code: " + e.Code + " and reason: " + e.Reason;
        Debug.Log(message);
        if (e.Code == 1006) {
            ConnectAsync();
        }
    }

    void OnErrorHandler(object sender, ErrorEventArgs e) {
        string message = "WebSocket connection failure: " + e.Message;
        Debug.LogError(message);
    }

    void OnSendComplete(bool success) {
        string message = "Message sent successfully? " + success;
        Debug.Log(message);
    }
}