using System;

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