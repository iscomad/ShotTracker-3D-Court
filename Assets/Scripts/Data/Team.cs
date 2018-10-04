using System;

[Serializable]
public class Team
{
    public string id;
    public string name;
    public string jerseyColor;
    public string logoUrl;
    public Player[] players;
}