using System;
using System.Net.Http;
//using System.Text.Json;
using Newtonsoft.Json;
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

    public string GetGlobalVar<Type>(string key)
    {
        string value = "";
        if (key.Equals("snifferIP")) value = "192.168.1.37";
        if (key.Equals("snifferPort")) value = "9938";
        if (key.Equals("songScene")) value = "RocksmithBigCamInGame";
        if (key.Equals("rocksmithScene")) value = "RocksmithBigCam";
        if (key.Equals("pauseScene")) value = "RocksmithBigCam";



        return value;

    }

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


// Objects for parsing the song data
// 

record MemoryReadout
{
    [JsonRequired]
    public string SongId { get; set; }
    [JsonRequired]
    public string ArrangementId { get; set; }
    [JsonRequired]
    public string GameStage { get; set; }
    public double SongTimer { get; set; }
    public NoteData NoteData { get; set; }
}
record NoteData
{
    public double Accuracy { get; set; }
    public int CurrentHitStreak { get; set; }
}
record SongDetails
{
    public string SongName { get; set; } 
    public string ArtistName { get; set; } 
    public double SongLength { get; set; }
    public string AlbumName { get; set; }
    public int AlbumYear { get; set; }
    public Arrangement[] Arrangements { get; set; }
    
}


record Arrangement
{
    public string Name { get; set; }
    public string ArrangementID { get; set; }
    public string type { get; set; }
    public Tuning Tuning { get; set; }

    public Section[] Sections { get; set; }
}
record Tuning
{
    public string TuningName { get; set; }
}

record Section
{
    public string Name { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
record Response
{
    public MemoryReadout MemoryReadout { get; set; }
    public SongDetails SongDetails { get; set; }
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

    enum SectionType
    {
        Default
        ,Riff
        ,Solo
        ,Verse
        ,Chorus
        ,Brigde
        ,Breakdown
    }

    private string snifferIp;
    private string snifferPort;


    private GameStage currentGameStage;
    private GameStage lastGameStage;
    private double currentSongTimer;
    private double lastSongTimer;

    private Arrangement currentArrangement;
    private int currentSectionIndex;
   
    //Split into memory details and SongDetails, as it is only necessary to parse the latter once
    private Response lastResponse;

    private string rocksmithScene;
    private string songScene;
    private string songPausedScene;
	private string currentScene;
	
    private HttpClient client;
    private HttpResponseMessage response;
    private string responseString;

    private DateTime lastSceneChange;
    private int minDelay;

    //Needs to be commented out in streamer bot.
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
        else if (stage.Equals("las_tuner"))
        {
            currentStage = GameStage.InTuner;
        }
        else
        {
            verboseLog("Evaluated as Menu");
        }

        return currentStage;
    }

    public void Init()
    {

        //Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = CPH.GetGlobalVar<string>("snifferIP").Replace('"',' ').Trim();//"192.168.1.37";
        verboseLog(string.Format("Initialized sniffer ip as {0}",snifferIp));
		snifferPort = "9938";
		verboseLog(string.Format("Initialized sniffer port as {0}",snifferPort));
        rocksmithScene = CPH.GetGlobalVar<string>("rocksmithScene");
		verboseLog(string.Format("Initialized Rocksmith scene as {0}",rocksmithScene));
        songScene = CPH.GetGlobalVar<string>("songScene");
		verboseLog(string.Format("Initialized song scene as {0}",songScene));
        songPausedScene = CPH.GetGlobalVar<string>("pauseScene");
		verboseLog(string.Format("Initialized pause scene as {0}",songPausedScene));

        lastSceneChange = DateTime.Now;
        minDelay = 3;
        verboseLog("Initialising sniffer");
        client = new HttpClient();
        if (client == null) debug("Failed instantiating HttpClient");
		currentScene = "";
        currentArrangement = null;
        currentSectionIndex = -1;
    }

    private bool getLatestResponse()
    {
        bool success;
        try
        {
            string address = string.Format("http://{0}:{1}", snifferIp, snifferPort);
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
        bool isRelevant = false;
		currentScene = CPH.ObsGetCurrentScene();
		if (currentScene.Equals(rocksmithScene)
		|| currentScene.Equals(songScene)
		|| currentScene.Equals(songPausedScene))
		{
			isRelevant = true;
		}
        return isRelevant;
    }

    private void parseLatestResponse()
    {
        try
        {
            lastResponse = JsonConvert.DeserializeObject<Response>(responseString);
            currentGameStage = evalGameStage(lastResponse.MemoryReadout.GameStage);
            currentSongTimer = lastResponse.MemoryReadout.SongTimer;

        }
        catch (System.Text.Json.JsonException ex)
        {
            debug("Error parsing response: " + ex.Message);
        }
        
    }

    private void identifyArrangement()
    {
        currentArrangement = null;
        currentSectionIndex = -1;
        foreach (Arrangement arr in lastResponse.SongDetails.Arrangements)
        {
            if (arr.ArrangementID == lastResponse.MemoryReadout.ArrangementId)
            {
                currentArrangement = arr;
                break;
            }
        }
    }

    private void performSceneSwitchIfNecessary()
    {
        verboseLog(string.Format("Currently in scene {0}", currentScene));

        if (currentGameStage == GameStage.InSong)
        {
            if (lastGameStage == GameStage.InTuner)
            {
                identifyArrangement();
            }

            verboseLog("Current game stage in song");
            if (currentScene.Equals(rocksmithScene))
            {
                verboseLog("Current scene is the Rocksmith scene");
                if (!lastResponse.MemoryReadout.SongTimer.Equals(lastSongTimer))
                {
                    verboseLog("Song timer has changed");
                    if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                    {
                        verboseLog(string.Format("Switching to {0}", songScene));
                        CPH.ObsSetScene(songScene);
                        lastSceneChange = DateTime.Now;
                    }
                }
                else
                {
                    //Already in correct scene
                }
            }
            else if (currentScene.Equals(songScene))
            {
                verboseLog("Current scene is song scene");
                if (lastResponse.MemoryReadout.SongTimer.Equals(lastSongTimer))
                    if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                    {
                        CPH.ObsSetScene(songPausedScene);
                        lastSceneChange = DateTime.Now;
                    }

            }
        }
        else if (currentGameStage == GameStage.Menu)
        {
            verboseLog("Currently in game stage menu");
            if (!currentScene.Equals(rocksmithScene))
            {
                verboseLog(string.Format("Switching scene from {0} to {1}",currentScene, rocksmithScene));
                if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                {
                    CPH.ObsSetScene(rocksmithScene);
                    lastSceneChange = DateTime.Now;
                }
            }
        }
        lastGameStage = currentGameStage;
        lastSongTimer = lastResponse.MemoryReadout.SongTimer;
    }

    private void checkSectionActions()
    {
        if (currentArrangement != null)
        {
            if (currentSectionIndex == -1)
            {
                if (currentSongTimer >= currentArrangement.Sections[0].StartTime)
                {
                    currentSectionIndex = 0;
                }
            }
            else
            {
                // Check if entered a new section
                if (currentSongTimer >= currentArrangement.Sections[currentSectionIndex].EndTime)
                {
                    verboseLog(string.Format("Now entering Section: {0}", currentArrangement.Sections[++currentSectionIndex].Name));
                }
            }
        }
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
                checkSectionActions();
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
