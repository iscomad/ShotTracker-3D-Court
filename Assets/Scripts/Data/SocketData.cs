using System;

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