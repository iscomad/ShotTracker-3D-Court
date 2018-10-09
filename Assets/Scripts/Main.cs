using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;

public class Main : MonoBehaviour
{
    public GameObject courtWrapper;
    public GameObject team1Pool;
    public GameObject team2Pool;
    public GameObject scoreBoard;
    public GameObject ballsPool;
    public GameObject hoop1Wrapper;
    public GameObject hoop2Wrapper;
    public GameObject audioObject;
    public Button flipButton;

    CourtSocket courtSocket;
    StatsSocket statsSocket;
    SessionSocket sessionSocket;
    MakeMissSoundScript makeMissSoundScript;
    MakeMissAnimationScript basketAnimation1;
    MakeMissAnimationScript basketAnimation2;
    const float WIDTH = 26440f;
    const float HEIGHT = 14760f;
    bool isFlipped = false;

    Dictionary<string, GameObject> team1Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> team2Dict = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> ballDict = new Dictionary<string, GameObject>();
    Dictionary<string, MakeMissAnimationScript> hoopDict = new Dictionary<string, MakeMissAnimationScript>();

    Data liveGameData;

    Text sessionText;
    Text team1ScoreText;
    Text team2ScoreText;

    void Start()
    {
        makeMissSoundScript = audioObject.GetComponent<MakeMissSoundScript>();
        basketAnimation1 = hoop1Wrapper.transform.GetChild(0).GetComponent<MakeMissAnimationScript>();
        basketAnimation2 = hoop2Wrapper.transform.GetChild(0).GetComponent<MakeMissAnimationScript>();

        sessionText = scoreBoard.transform.Find("Session/Text").GetComponent<Text>();
        team1ScoreText = scoreBoard.transform.Find("Score1").GetComponent<Text>();
        team2ScoreText = scoreBoard.transform.Find("Score2").GetComponent<Text>();

        AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        string liveGameDataRaw = null;
        if (currentActivity != null)
        {
            AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent");
            bool hasExtra = intent.Call<bool>("hasExtra", "live_game_data");
            if (hasExtra)
            {
                AndroidJavaObject extras = intent.Call<AndroidJavaObject>("getExtras");
                liveGameDataRaw = extras.Call<string>("getString", "live_game_data");
            }
        }
        if (liveGameDataRaw == null)
        {
            liveGameDataRaw = ReadFromTestFile();
        }

        liveGameData = JsonUtility.FromJson<Data>(liveGameDataRaw);

        SetupTeam(liveGameData.team1, team1Dict, team1Pool);
        SetupTeam(liveGameData.team2, team2Dict, team2Pool);
        SetupCourt(liveGameData.court);
        SetupScoreBoard(liveGameData);
        SetupFlipButton();

        StartCourtSocket();
        StartStatsSocket();
        StartSessionSocket();
    }

    void OnDestroy()
    {
        courtSocket.Close();
        statsSocket.Close();
        sessionSocket.Close();
    }

    void SetupCourt(Court court) {
        GameObject[] hoopObjects = new GameObject[] { hoop1Wrapper, hoop2Wrapper };
        Hoop[] hoops = court.hoops;
        for (int i = 0; i < Math.Min(hoops.Length, hoopObjects.Length); i++) {
            float xNew = (float)hoops[i].y / court.width * 9 * 2;
            float zNew = (float)hoops[i].x / court.height * 5 * -2;
            float yNew = hoopObjects[i].transform.position.y;
            hoopObjects[i].transform.position = new Vector3(xNew, yNew, zNew);
            hoopObjects[i].transform.eulerAngles = new Vector3(0, 90 + hoops[i].angleReference);
            hoopDict.Add(hoops[i].id, hoopObjects[i].transform.GetChild(0).GetComponent<MakeMissAnimationScript>());
        }
    }

    void SetupScoreBoard(Data data)
    {
        if (data == null) return;

        scoreBoard.transform.Find("Name1").GetComponent<Text>().text = data.team1.name.ToUpper();
        scoreBoard.transform.Find("Name2").GetComponent<Text>().text = data.team2.name.ToUpper();

        Image image = scoreBoard.transform.Find("Logo1/Mask/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team1.logoUrl));

        image = scoreBoard.transform.Find("Logo2/Mask/Logo").GetComponent<Image>();
        StartCoroutine(LoadImage(image, data.team2.logoUrl));

        SetScore(data.team1.id, data.game.score1);
        SetScore(data.team2.id, data.game.score2);

        SetSession();
    }

    void SetupFlipButton() {
        flipButton.onClick.AddListener(HandleFlipButton);
    }

    void HandleFlipButton() {
        Quaternion rotation = courtWrapper.transform.rotation;
        float rotationValue;
        if (rotation.y == 0) {
            rotationValue = 180f;
        }
        else {
            rotationValue = -180f;
        }
        StartCoroutine(RotateObject(courtWrapper, Quaternion.Euler(0, !isFlipped ? 180 : 0, 0), 1f));
        isFlipped = !isFlipped;
    }

    string ReadFromTestFile()
    {
        string path = "Assets/Resources/TestLiveGameData.txt";

        StreamReader reader = new StreamReader(path);
        string text = reader.ReadToEnd();
        reader.Close();

        return text;
    }

    Color GetFontColor(Color jerseyColor)
    {
        // Based on Luma constants (see https://en.wikipedia.org/wiki/Luma_%28video%29)
        double threshold = 0.2126 * jerseyColor.r + 0.7152 * jerseyColor.g + 0.0722 * jerseyColor.b;
        return threshold < 0.67 ? Color.white : Color.black;
    }

    void SetupTeam(Team team, Dictionary<string, GameObject> teamDict,
                               GameObject playersPool)
    {
        int playersCount = playersPool.transform.childCount;
        Color jerseyColor = new Color();
        ColorUtility.TryParseHtmlString(team.jerseyColor, out jerseyColor);
        Color fontColor = GetFontColor(jerseyColor);
        for (int i = 0; i < Math.Min(playersCount, team.players.Length); i++)
        {
            GameObject playerObject = playersPool.transform.GetChild(i).gameObject;
            SetupPlayer(playerObject, jerseyColor, team.players[i].number, fontColor);
            string id = team.players[i].id;
            if (!teamDict.ContainsKey(id)) {
                teamDict.Add(id, playerObject);
            }
        }
    }

    private void SetupPlayer(GameObject playerObject, Color jerseyColor, string number, Color fontColor)
    {
        playerObject.transform.GetChild(0).GetComponent<Renderer>().material.color = jerseyColor;
        playerObject.transform.GetChild(1).GetComponent<TextMesh>().text = number;
        playerObject.transform.GetChild(1).GetComponent<TextMesh>().color = fontColor;
        playerObject.transform.GetChild(2).GetComponent<TextMesh>().text = number;
        playerObject.transform.GetChild(2).GetComponent<TextMesh>().color = fontColor;   
    }

    void StartCourtSocket() 
    {
        courtSocket = new CourtSocket(liveGameData) 
        {
            OnPlayerPositionChanged = SetPlayerPosition,
            OnBallPositionChanged = SetBallPosition,
            OnShotMade = OnShot
        };
        courtSocket.ConnectAsync();
    }

    void StartStatsSocket()
    {
        statsSocket = new StatsSocket(liveGameData) {
            OnScoreChanged = SetScore
        };
        statsSocket.ConnectAsync();
    }

    void StartSessionSocket() 
    {
        sessionSocket = new SessionSocket(liveGameData) {
            OnSessionStarted = OnNewSession,
            OnGameEnded = SetGameEnded
        };
        sessionSocket.ConnectAsync();
    }

    void OnShot(string hid, string st) {
        if (hoopDict.ContainsKey(hid)) {
            if ("MAKE".Equals(st)) {
                hoopDict[hid].AnimateMake();
                makeMissSoundScript.PlayMakeAudio();
            } 
            else if ("MISS".Equals(st)) {
                hoopDict[hid].AnimateMiss();
                makeMissSoundScript.PlayMissAudio();
            }
        }
    }

    void OnShotMake(string tid) {
        if (liveGameData.team1.id.Equals(tid)) {
            basketAnimation2.AnimateMake();
        } else if (liveGameData.team2.id.Equals(tid)) {
            basketAnimation1.AnimateMake();
        }
        makeMissSoundScript.PlayMakeAudio();
    }

    void OnShotMiss(string tid) {
        if (liveGameData.team1.id.Equals(tid)) {
            basketAnimation2.AnimateMiss();
        } else if (liveGameData.team2.id.Equals(tid)) {
            basketAnimation1.AnimateMiss();
        }
        makeMissSoundScript.PlayMissAudio();
    }

    void OnNewSession(string newSessionId) {
        int newSize = liveGameData.game.sessions.Length + 1;
        Array.Resize(ref liveGameData.game.sessions, newSize);
        liveGameData.game.sessions[newSize - 1] = newSessionId;
        
        OnSessionChanged();
    }

    void OnSessionChanged()
    {
        SetSession();
        courtSocket.SubscribeToCourt();
        statsSocket.SubscribeToStats();
    }

    void SetGameEnded()
    {
        SetSessionText("final");
    }

    void SetSession()
    {
        SetSessionText(GetSessionText(liveGameData.game.roundType, liveGameData.game.sessions));
    }

    void SetSessionText(string text) 
    {
        sessionText.text = text;
    }

    string GetSessionText(string type, string[] sessions)
    {
        string text = (sessions.Length) + "";
        if ("HALF".Equals(type))
        {
            if (sessions.Length > 2) 
            {
                return "OT";
            }
            text += "H";
        }
        else
        {
            if (sessions.Length > 4)
            {
                return "OT";
            }
            text += "Q";
        }

        return text;
    }

    void SetPlayerPosition(string playerId, int x, int y, int z)
    {
        if (team1Dict.ContainsKey(playerId))
        {
            SetGameObjectPosition(team1Dict[playerId], x, y, int.MinValue);
        }
        else if (team2Dict.ContainsKey(playerId))
        {
            SetGameObjectPosition(team2Dict[playerId], x, y, int.MinValue);
        }
    }

    void SetBallPosition(string id, int x, int y, int z)
    {
        GameObject ball = null;
        if (!ballDict.ContainsKey(id))
        {
            for (int i = 0; i < ballsPool.transform.childCount; i++) 
            {
                GameObject newBall = ballsPool.transform.GetChild(i).gameObject;
                if (!ballDict.ContainsValue(newBall)) 
                {
                    ball = newBall;
                    break;
                }
            }
            if (ball == null) 
            {
                Debug.LogError("No balls left in the pool");
                return;
            }

            ballDict.Add(id, ball);
        } else {
            ball = ballDict[id];
        }
        SetGameObjectPosition(ball, x, y, z);
    }

    void SetGameObjectPosition(GameObject gObject, int x, int y, int z)
    {
        Court court = liveGameData.court;
        float xNew = (float)x / court.width * 9 * 2;
        float zNew = (float)y / court.height * 5 * -2;
        float yNew = z == int.MinValue ? gObject.transform.localPosition.y : (float) z / court.width * 9 * 2 + 0.35f; // 0.1 is the height of the floor
        Vector3 target = new Vector3(xNew, yNew, zNew);
        //gObject.transform.position = target;
        StartCoroutine(MoveObject(gObject, target, 0.3f));
    }

    void SetScore(string teamId, string score)
    {
        if (teamId.Equals(liveGameData.team1.id))
        {
            team1ScoreText.text = score;
        }
        else if (teamId.Equals(liveGameData.team2.id))
        {
            team2ScoreText.text = score;
        }
    }

    IEnumerator LoadImage(Image uiImage, string loadedURL)
    {
        Texture2D temp = new Texture2D(0, 0);
        WWW www = new WWW(loadedURL);
        yield return www;

        temp = www.texture;
        if (temp != null) 
        { 
            Sprite sprite = Sprite.Create(temp, new Rect(0, 0, temp.width, temp.height), new Vector2(0.5f, 0.5f));
            uiImage.sprite = sprite;
        }
    }

    IEnumerator MoveObject(GameObject gObject, Vector3 target, float duration)
    {
        Vector3 source = gObject.transform.localPosition;
        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            gObject.transform.localPosition = Vector3.Lerp(source, target, (Time.time - startTime) / duration);
            yield return null;
        }
        gObject.transform.localPosition = target;
    }

    IEnumerator RotateObject(GameObject gObject, Quaternion target, float duration) {
        Quaternion source = gObject.transform.rotation;
        float startTime = Time.time;
        while (Time.time < startTime + duration) {
            gObject.transform.rotation = Quaternion.Lerp(source, target, (Time.time - startTime) / duration);
            yield return null;
        }
        gObject.transform.rotation = target;
    }
}