using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;



//Mock CPH
public class CPHmock
{
    private string currentScene = "RocksmithBigCam";
    public void LogDebug(string str) { Console.WriteLine(str); }
    public void LogInfo(string str) { Console.WriteLine(str); }
    public void LogError(string str) { Console.WriteLine(str); }

    public void LogVerbose(string str) { Console.WriteLine(str); }

    public void ObsSetScene(string str) { Console.WriteLine(string.Format("Setting scene: {0}", str)); currentScene = str; }

    public string ObsGetCurrentScene() { return currentScene; }

    public void SendMessage(string str) { Console.WriteLine(str); }

    public static void Main(string[] args )
    { 
        CPHInline obj = new CPHInline();

        obj.Init();

        while (true)
        {   
            Console.Clear();
            obj.Execute();
            Thread.Sleep(1000);
        }
    }

}


//Implementation for Streamer.bot
 
public class CPHInline
{
    enum GameStage
    {
        Menu
        , InSong
        , InTuner
    }

    private string SnifferIp;
    private string SnifferPort;
    private string songID;
    private string arrangementID;
    private GameStage currentGameStage;
    private GameStage lastGameStage;
    private double currentSongTimer;
    private double lastSongTimer;
    private double accuracy;
    private int currentHitStreak;

    private string RocksmithScene;
    private string SongScene;
    private string SongPausedScene;

    private HttpClient client;
    private HttpResponseMessage response;
    private string responseString;

    private DateTime lastSceneChange;
    private int minDelay;

    private CPHmock CPH = new CPHmock();

    bool doLogToChat = false;
    // Disabling regular verbose request as they really bloat the log file rather quickly. Can be enabled if need be
    bool doLogVerbose = false;

    void debug(string str)
    {
        if (doLogToChat) CPH.SendMessage(str);
        CPH.LogDebug(str);
    }

    void verboseLog(string str)
    {
        if (doLogToChat) CPH.SendMessage(str);
        if (doLogVerbose) CPH.LogVerbose(str);
    }

    private GameStage evalGameStage(string stage)
    {
        GameStage currentStage = GameStage.Menu;
        verboseLog(string.Format("Evaluating game stage: {0}", stage));
        // Other potential values are: MainMenu las_SongList las_SongOptions las_tuner
        if (stage.Equals("las_game"))
        {
            verboseLog("Evaluated as InSong");
            currentStage = GameStage.InSong;
        }
        else
        {
            verboseLog("Evaluated as Menu");
        }

        return currentStage;
    }

    public void Init()
    {

        //Those will later be moved to external arguments
        SnifferIp = "192.168.1.37";
        SnifferPort = "9938";
        RocksmithScene = "RocksmithBigCam";
        SongScene = "RocksmithBigCamInGame";
        SongPausedScene = RocksmithScene;
        lastSceneChange = DateTime.Now;
        minDelay = 3;
        verboseLog("Initialising sniffer");
        client = new HttpClient();
        if (client == null) debug("Failed instantiating HttpClient");
    }

    private bool getLatestResponse()
    {
        bool success;
        try
        {
            string address = string.Format("http://{0}:{1}", SnifferIp, SnifferPort);
            verboseLog(string.Format("Trying to fetch latest response from {0}", address));
            response = client.GetAsync(address).GetAwaiter().GetResult();
            if (response != null)
            {
                verboseLog("Response received");
                response.EnsureSuccessStatusCode();
                verboseLog("Response Status Code validated");
                responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                verboseLog("Successfully fetched new response from sniffer");
                success = true;
            }
            else
            {
                success = false;
                debug("Response is null");
            }
        }
        catch (HttpRequestException e)
        {
            debug("Error in response");
            debug(string.Format("Caught exception trying to get response from sniffer: {0}", e.Message));
            success = false;
        }
        if (!success) debug("Failed fetching response");
        return success;
    }

    private bool isRelevantScene()
    {
        //Not sure I need/Want this
        bool isRelevant = true;

        return isRelevant;
    }

    private void parseLatestResponse()
    {
        verboseLog("Calling Parse()");
        var obj = JObject.Parse(responseString)["memoryReadout"];
        var songDetails = JObject.Parse(responseString)["songDetails"];
        if (obj == null) debug("Could not parse MemoryReadout");
        if (obj != null)
        {
            verboseLog("Successfully parsed memoryReadout");
            songID = obj["songID"].ToString();

            arrangementID = obj["arrangementID"].ToString();
            currentGameStage = evalGameStage(obj["gameStage"].ToString());
            currentSongTimer = double.Parse(obj["songTimer"].ToString());

            //var noteData = JObject.Parse(obj.ToString())["noteData"];
            var noteData = obj["noteData"];
            if (noteData == null)
            {
                CPH.LogDebug("noteData node not found. New sniffer version?");
            }
            else
            {
                if (noteData.HasValues)
                {
                    verboseLog("Fetching Accurary and streak");
                    accuracy = double.Parse(noteData["Accuracy"].ToString());
                    currentHitStreak = int.Parse(noteData["CurrentHitStreak"].ToString());
                }
                else
                {
                    verboseLog("No note data available");
                    accuracy = 0.0;
                    currentHitStreak = 0;
                }
            }
            if (songID == null) debug("Failed parsing song ID");
            else verboseLog("Song id: " + songID);
            if (arrangementID == null) debug("Failed parsing arrangement ID");


            if (songDetails == null) { CPH.LogDebug("Songdetails not found. New sniffer version?"); }
            else
            {
                if (songDetails.HasValues)
                {
                    //Here we can readout current song information to post in chat, or deliver uppon command
                }
            }
        }
        else
        {
            debug("Could not parse response.");
        }
    }

    private void performSceneSwitchIfNecessary()
    {
        string currentScene = CPH.ObsGetCurrentScene();
        verboseLog(string.Format("Currently in scene {0}", currentScene));

        if (currentGameStage == GameStage.InSong)
        {
            verboseLog("Current game stage in song");
            if (currentScene.Equals(RocksmithScene))
            {
                verboseLog("Current scene is the Rocksmith scene");
                if (!currentSongTimer.Equals(lastSongTimer))
                {
                    verboseLog("Song timer has changed");
                    if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                    {
                        verboseLog(string.Format("Switching to {0}", SongScene));
                        CPH.ObsSetScene(SongScene);
                        lastSceneChange = DateTime.Now;
                    }
                }
                else
                {
                    //Already in correct scene
                }
            }
            else if (currentScene.Equals(SongScene))
            {
                verboseLog("Current scene is song scene");
                if (currentSongTimer.Equals(lastSongTimer))
                    if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                    {
                        CPH.ObsSetScene(SongPausedScene);
                        lastSceneChange = DateTime.Now;
                    }

            }
        }
        else if (currentGameStage == GameStage.Menu)
        {
            verboseLog("Currently in game stage menu");
            if (!currentScene.Equals(RocksmithScene))
            {
                verboseLog(string.Format("Switching scene from {0} to {1}",currentScene,RocksmithScene));
                if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                {
                    CPH.ObsSetScene(RocksmithScene);
                    lastSceneChange = DateTime.Now;
                }
            }
        }
        lastGameStage = currentGameStage;
        lastSongTimer = currentSongTimer;
    }

    public bool Execute()
    {
        verboseLog("Execute sniffing");
        if (isRelevantScene())
        {
            verboseLog("Scene is relevant, now fetching response");
            if (getLatestResponse())
            {
                verboseLog("Now Parsing response");
                parseLatestResponse();
                verboseLog("Performing necessary switches");
                performSceneSwitchIfNecessary();

            }
            else
            {
                debug("Fetching response failed, exiting action.");
                return false;
            }
        }

        return true;
    }
}






