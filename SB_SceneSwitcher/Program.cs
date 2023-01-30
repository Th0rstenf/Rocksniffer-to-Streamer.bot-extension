using System;
//using System.Net.Http;
//using System.Text.Json;
using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;

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
	
    public void ObsSetGdiText(string scene, string source, string text, int connection =0){ Console.WriteLine(string.Format("Setting text field {1} in scene {0} to {2}", scene, source, text)); }
    
    public void SendMessage(string str) { Console.WriteLine(str); }

    public void RunAction(string str) { Console.WriteLine(string.Format("Running action: {0}",str)); }

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
        int i = 0;
        while (true)
        {   
            if (((++i) % 10) == 0) Console.Clear();
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
    public string SongId { get; set; } = null!;
    [JsonRequired]
    public string ArrangementId { get; set; } = null!;
    [JsonRequired]
    public string GameStage { get; set; } = null!;
    public double SongTimer { get; set; }
    public NoteData NoteData { get; set; } = null!;
}
record NoteData
{
    public double Accuracy { get; set; }
    public int CurrentHitStreak { get; set; }
}
record SongDetails
{
    public string SongName { get; set; } = null!;
    public string ArtistName { get; set; } = null!;
    public double SongLength { get; set; }
    public string AlbumName { get; set; } = null!;
    public int AlbumYear { get; set; }
    public Arrangement[] Arrangements { get; set; } = null!;

}


record Arrangement
{
    public string Name { get; set; } = null!;
    public string ArrangementID { get; set; } = null!;
    public string type { get; set; } = null!;
    public Tuning Tuning { get; set; } = null!;

    public Section[] Sections { get; set; } = null!;
}
record Tuning
{
    public string TuningName { get; set; } = null!;
}

record Section
{
    public string Name { get; set; } = null!;
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
record Response
{
    public MemoryReadout MemoryReadout { get; set; } = null!;
    public SongDetails SongDetails { get; set; } = null!;
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

    private string snifferIp = null!;
    private string snifferPort = null!;


    private GameStage currentGameStage;
    private GameStage lastGameStage;
    private SectionType currentSectionType;
    private SectionType lastSectionType;
    private double currentSongTimer;
    private double lastSongTimer;

    private Arrangement? currentArrangement = null!;
    private int currentSectionIndex;
   
    //Split into memory details and SongDetails, as it is only necessary to parse the latter once
    private Response lastResponse = null!;

    private string rocksmithScene = null!;
    private string songScene = null!;
    private string songPausedScene = null!;
	private string currentScene = null!;
	
    private HttpClient client = null!;
    private HttpResponseMessage response = null!;
    private string responseString = null!;

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
        
        currentSectionIndex = -1;
        lastSectionType = currentSectionType = SectionType.Default;
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
		if (currentScene != null)
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
            lastResponse = JsonConvert.DeserializeObject<Response>(responseString) ?? throw new Exception("Is never supposed to be zero");
            currentGameStage = evalGameStage(lastResponse.MemoryReadout.GameStage);
            currentSongTimer = lastResponse.MemoryReadout.SongTimer;
        }
        catch (JsonException ex)
        {
            debug("Error parsing response: " + ex.Message);
        }
        
    }

    private void identifyArrangement()
    {
        currentArrangement = null;
        currentSectionIndex = -1;
        if (lastResponse.SongDetails != null) 
        { 
            foreach (Arrangement arr in lastResponse.SongDetails.Arrangements)
            {
                if (arr.ArrangementID == lastResponse.MemoryReadout.ArrangementId)
                {
					currentArrangement = arr;
                    break;
                }
            }
        }
        if (currentArrangement != null)
        {
            CPH.RunAction("ArrangementAvailable");
        }
        else
        {
            CPH.RunAction("NoArrangementAvailable");
        }
    }
    private void identifySection()
    {
        if (currentArrangement != null)
        {
            string name = currentArrangement.Sections[currentSectionIndex].Name;
            if (name.ToLower().Contains("solo")) { currentSectionType = SectionType.Solo; }
            else if (name.ToLower().Contains("riff")) { currentSectionType = SectionType.Riff; }
            else if (name.ToLower().Contains("bridge")) { currentSectionType = SectionType.Brigde; }
            else if (name.ToLower().Contains("breakdown")) { currentSectionType = SectionType.Breakdown; }
            else if (name.ToLower().Contains("chorus")) { currentSectionType = SectionType.Chorus; }
            else if (name.ToLower().Contains("verse")) { currentSectionType = SectionType.Verse; }
            else { currentSectionType = SectionType.Default; }
        }
        else
        { 
            currentSectionType = SectionType.Default; 
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
            bool hasSectionChanged = false;
            if (currentSectionIndex == -1)
            {
                if (currentSongTimer >= currentArrangement.Sections[0].StartTime)
                {
                    currentSectionIndex = 0;
                    hasSectionChanged = true;
                }
            }
            else
            {
                // Check if entered a new section
                if (currentSongTimer >= currentArrangement.Sections[currentSectionIndex].EndTime)
                {
                    ++currentSectionIndex;
                    hasSectionChanged = true;
                }
            }
            if (hasSectionChanged)
            {
				identifySection();
				//TODO: Should only happen if I execute it
				CPH.ObsSetGdiText("Projection(RS)","textSectionName",currentArrangement.Sections[currentSectionIndex].Name);
				if (currentSectionType != lastSectionType)
                {
                    CPH.RunAction(string.Format("leave{0}", Enum.GetName(typeof(SectionType),lastSectionType)));
                    CPH.RunAction(string.Format("enter{0}", Enum.GetName(typeof(SectionType),currentSectionType)));
                    lastSectionType = currentSectionType;
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
