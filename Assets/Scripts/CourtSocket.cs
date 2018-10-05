using System;
using UnityEngine;
using WebSocketSharp;

public delegate void CourtPositionDelegate(string id, int x, int y, int z);
public delegate void CourtShotDelegate(string hid, string st);

public class CourtSocket : WebSocket {

    public CourtPositionDelegate OnPlayerPositionChanged;
    public CourtPositionDelegate OnBallPositionChanged;
    public CourtShotDelegate OnShotMade;

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
        string message = "Court WebSocket server said: " + e.Data;
        Debug.Log(message);

        string jsonString = System.Text.Encoding.UTF8.GetString(e.RawData);
        SocketEntity entity = JsonUtility.FromJson<SocketEntity>(jsonString);
        if (entity.data.bid > 0) {
            if (OnBallPositionChanged != null) {
                string bid = entity.data.bid + "";
                int x = entity.data.y;
                int y = entity.data.x;
                int z = entity.data.z;
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnBallPositionChanged(bid, x, y, z)
                );
            }
        }
        else if (entity.data.pid > 0) {
            if (OnPlayerPositionChanged != null) {
                string pid = entity.data.pid + "";
                int x = entity.data.y;
                int y = entity.data.x;
                int z = entity.data.z;
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnPlayerPositionChanged(pid, x, y, z)
                );
            }
            if (entity.data.shot != null && entity.data.shot.hid != null && OnShotMade != null) {
                Debug.LogWarning("============== " + entity.data.shot.hid + "........" + entity.data.shot.st);
                UnityMainThreadDispatcher.Instance().Enqueue(
                    () => OnShotMade(entity.data.shot.hid, entity.data.shot.st)
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
        string message = "Court WebSocket connection failure: " + e.Message;
        Debug.LogError(message);
    }

    void OnSendComplete(bool success) {
        string message = "Court Message sent successfully? " + success;
        Debug.Log(message);
    }
}