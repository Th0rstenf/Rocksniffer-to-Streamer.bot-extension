using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;



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
    public int TotalNotes { get; set; }
    public int TotalNotesHit { get; set; }
    public int CurrentHitStreak { get; set; }

    public int HighestHitStreak { get; set; }
    public int TotalNotesMissed { get; set; }
    public int CurrentMissStreak { get; set; }
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
    //It does not give any performance boost to parse only partially, due to the way the parser works.
    //However parsing the full song takes roughly 0.2 micro seconds, so it's pretty neglectable
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
		,NoGuitar
        ,Riff
        ,Solo
        ,Verse
        ,Chorus
        ,Bridge
        ,Breakdown
    }
    enum ActivityBehavior
    {
        WhiteList
        ,BlackList
        ,AlwaysOn
    }
    enum BroadcastingSoftware
    {
        OBS
        ,SLOBS
        ,NONE
    }

    //Needs to be commented out in streamer bot.
    private CPHmock CPH = new CPHmock();


    private string snifferIp = null!;
    private string snifferPort = null!;

    private GameStage currentGameStage;
    private GameStage lastGameStage;
    private SectionType currentSectionType;
    private SectionType lastSectionType;
    private ActivityBehavior itsBehavior;
    private BroadcastingSoftware itsBroadcastingSoftware;
    private string[] blackListedScenes = null!;
    private double currentSongTimer;
    private double lastSongTimer;

    private Arrangement? currentArrangement = null!;
    private int currentSectionIndex;
    private int currentSongSceneIndex;
   
    private Response currentResponse = null!;
    private NoteData lastNoteData = null!;

    private UInt32 totalNotesThisStream;
    private UInt32 totalNotesHitThisStream;
    private UInt32 totalNotesMissedThisStream;
    private double accuracyThisStream;
    private UInt32 highestStreakSinceLaunch;

    private string menuScene = null!;
    private string[] songScenes = null!;
    private string songPausedScene = null!;
	private string currentScene = null!;
	
    private HttpClient client = null!;
    private HttpResponseMessage response = null!;
    private string responseString = null!;

    private DateTime lastSceneChange;
    private int minDelay;
    private int sameTimeCounter;
	private bool doLogToChat = false;
	private bool logDebug = true;
    private bool isSwitchingScenes = true;
    private bool isReactingToSections =true;
	private bool isArrangementIdentified = false;

    public void SetLogDebug(bool value)
    {
        logDebug = value;
    }
    
    void debug(string str)
    {
        if (doLogToChat) CPH.SendMessage(str);
        if (logDebug) CPH.LogDebug(str);
    }

    private string formatTime(int totalSeconds)
    {
        TimeSpan timeSpan= TimeSpan.FromSeconds(totalSeconds);
        
        return timeSpan.ToString();
    }

    private void switchToScene(string scene) 
    {
        switch (itsBroadcastingSoftware)
        {
            case BroadcastingSoftware.OBS:
            {
                CPH.ObsSetScene(scene); break;
            }
            case BroadcastingSoftware.SLOBS:
            {
                CPH.SlobsSetScene(scene);
                break;
            }
            default:
            {
                debug("No stream program defined");
                break;
            }
        }
    }

    private void UpdateCurrentScene()
    {
        currentScene = GetCurrentScene();
        LogCurrentScene();
    }
    
    private string GetCurrentScene()
    {
        switch (itsBroadcastingSoftware)
        {
            case BroadcastingSoftware.OBS:
            {
                return CPH.ObsGetCurrentScene(); 
            }
            case BroadcastingSoftware.SLOBS:
            {
                return CPH.SlobsGetCurrentScene();
            }
            case BroadcastingSoftware.NONE:
            default:
            {
                debug("No stream program defined! Please connect either to OBS or SLOBS!");
                return "";
            }
        }
    }
    private GameStage evalGameStage(string stage)
    {
        GameStage currentStage = GameStage.Menu;
        // Other potential values are: MainMenu las_SongList las_SongOptions las_tuner
        if (stage.Equals("las_game") || stage.Equals("sa_game"))
        {
            currentStage = GameStage.InSong;
        }
        else if (stage.Contains("tuner"))
        {
            currentStage = GameStage.InTuner;
        }
        else
        {
            //Evaluated as Menu
        }

        return currentStage;
    }
    public void Init()
    {
        debug("Initialising RockSniffer to SB plugin");
        //Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = GetSnifferIp();
        // TODO snifferPort should be also configurable
        snifferPort = "9938"; 
        debug(string.Format("Sniffer ip configured as {0}:{1}",snifferIp,snifferPort));
		menuScene = GetGlobalVar("menuScene");
        debug("Menu scene: " + menuScene);
        songScenes = Regex.Split(GetGlobalVar("songScenes").Trim(), @"\s*[,;]\s*");
        debug("Song scenes: " + string.Join(", ", songScenes));
		songPausedScene = GetGlobalVar("pauseScene");
        debug("Song paused scene: " + songPausedScene);

        isSwitchingScenes = GetGlobalVar("switchScenes").ToLower().Contains("true");
        debug("Switching scenes configured to " + isSwitchingScenes.ToString());
        isReactingToSections = GetGlobalVar("sectionActions").ToLower().Contains("true");
		debug("Section actions are configured to " + isReactingToSections.ToString());
        lastSceneChange = DateTime.Now;
        minDelay = 3;
        client = new HttpClient();
        // TODO I think this is always false, so it will never write out a debug message.
        if (client == null) debug("Failed instantiating HttpClient");
        UpdateCurrentScene();

        string behaviorString = GetGlobalVar("behavior");
        if (behaviorString != null)
        {
            if (behaviorString.ToLower().Contains("whitelist")) itsBehavior = ActivityBehavior.WhiteList;
            else if (behaviorString.ToLower().Contains("blacklist")) itsBehavior = ActivityBehavior.BlackList;
            else if (behaviorString.ToLower().Contains("always")) itsBehavior = ActivityBehavior.AlwaysOn;
            else
            {
                itsBehavior = ActivityBehavior.WhiteList;
                debug("Behavior not configured, setting to whitelist as default");
            }
            debug("Behavior configured as " + itsBehavior.ToString());
        }

        if (itsBehavior == ActivityBehavior.BlackList)
        {
            blackListedScenes = Regex.Split(GetGlobalVar("blackList").Trim(), @"\s*[,;]\s*");
            debug("Blacklisted scenes:" + string.Join(", ", blackListedScenes));
        }
        else
        {
            blackListedScenes = new string[1];
        }

        itsBroadcastingSoftware = BroadcastingSoftware.NONE;

        totalNotesThisStream= 0;
        totalNotesHitThisStream = 0;
        totalNotesMissedThisStream = 0;
        accuracyThisStream = 0;
        highestStreakSinceLaunch= 0;
        currentSectionIndex = -1;
        currentSongSceneIndex= 0;
        lastSectionType = currentSectionType = SectionType.Default;
        lastGameStage = currentGameStage = GameStage.Menu;
        sameTimeCounter= 0;
    }

    private string GetSnifferIp()
    {
        return GetGlobalVar("snifferIP").Replace('"',' ').Trim();
    }

    private string GetGlobalVar(string behavior)
    {
        return CPH.GetGlobalVar<string>(behavior);
    }

    private bool isSongScene(string scene)
    {
        foreach (var s in songScenes)
        {
            if(scene.Equals(s))
            {
                return true;
            }
        }

        return false;
    }

    private void DetermineConnectedBroadcastingSoftware()
    {
        if (CPH.ObsIsConnected())
            itsBroadcastingSoftware = BroadcastingSoftware.OBS;
        else if (CPH.SlobsIsConnected())
            itsBroadcastingSoftware = BroadcastingSoftware.SLOBS;
        else
            itsBroadcastingSoftware= BroadcastingSoftware.NONE;
    }
    private bool getLatestResponse()
    {
        bool success;
        try
        {
            string address = string.Format("http://{0}:{1}", snifferIp, snifferPort);
            response = client.GetAsync(address).GetAwaiter().GetResult();
            if (response != null)
            {
                response.EnsureSuccessStatusCode();
                responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
        catch (ObjectDisposedException e)
        {
            debug("HttpClient was disposed. Exception: " + e.Message + " Reinitialising.");
            Init();
            success = false;
        }
        catch (Exception e)
        {
            debug("Caught unknown exception trying to read from HttpClient: " + e.Message);
            success = false;
        }
        if (!success) debug("Failed fetching response");
        return success;
    }

    private bool IsRelevantScene()
    {
        bool isRelevant;

        switch (itsBehavior)
        {
            case ActivityBehavior.WhiteList:
            {
                isRelevant = currentScene.Equals(menuScene)
                             || isSongScene(currentScene)
                             || currentScene.Equals(songPausedScene);
                break;
            }
            case ActivityBehavior.BlackList:
            {
		isRelevant = true;    
                foreach (string str in blackListedScenes)
                {
                    if (str.Trim().ToLower().Equals(currentScene.ToLower()))
                    {
                        isRelevant = false;
                        break;
                    }
                }                
                break;
            }
            case ActivityBehavior.AlwaysOn:
            {
                isRelevant = true;
                break;
            }

            default:
                isRelevant = false;
                break;
        }
        
        debug($"isRelevant={isRelevant}");
        return isRelevant;
    }

    private bool parseLatestResponse()
    {
        bool success = false;
        try
        {             
            currentResponse = JsonConvert.DeserializeObject<Response>(responseString) ?? throw new Exception("Is never supposed to be zero");
            if (currentResponse != null)
            {
                currentGameStage = evalGameStage(currentResponse.MemoryReadout.GameStage);
                currentSongTimer = currentResponse.MemoryReadout.SongTimer;
                success = true;
            }
        }
        catch (JsonException ex)
        {
            debug("Error parsing response: " + ex.Message);
        }
        catch (Exception e)
        {
            debug("Caught exception when trying to deserialize response string");
            debug("Exception: " + e.Message);
            debug("Trying to reinitialize to solve the issue");
            Init();
        }
        return success;
    }
    private void saveSongMetaData()
    {
        try
        {
            CPH.SetGlobalVar("songName", currentResponse.SongDetails.SongName, false);
            CPH.SetGlobalVar("artistName", currentResponse.SongDetails.ArtistName, false);
            CPH.SetGlobalVar("albumName", currentResponse.SongDetails.AlbumName, false);
            CPH.SetGlobalVar("songLength", (int)currentResponse.SongDetails.SongLength, false);
            string formatted = formatTime((int)currentResponse.SongDetails.SongLength);
            CPH.SetGlobalVar("songLengthFormatted",formatted, false);
            if (currentArrangement != null)
            {
                CPH.SetGlobalVar("arrangement", currentArrangement.Name, false);
                CPH.SetGlobalVar("arrangementType", currentArrangement.type, false);
                CPH.SetGlobalVar("tuning", currentArrangement.Tuning.TuningName, false);
            }
        }
        catch ( ObjectDisposedException e)
        {
            debug("Caught object disposed exception when trying to save meta data: " + e.Message);
            debug("Trying to reinitialize");
            Init();
        }
        catch(Exception e)
        {
            debug("Caught exception trying to save song meta data");
            debug("Exception: " + e.Message);
            debug("Trying to reinitialize to recover");
            Init();
        }
    }
    private void saveNoteDataIfNecessary()
    {
        try
        {
            if (currentGameStage == GameStage.InSong)
            {
                CPH.SetGlobalVar("songTimer", (int)currentResponse.MemoryReadout.SongTimer, false);
                string formatted = formatTime((int)currentResponse.MemoryReadout.SongTimer);
                CPH.SetGlobalVar("songTimerFormatted", formatted,false);
                if (lastNoteData != currentResponse.MemoryReadout.NoteData)
                {
                    CPH.SetGlobalVar("accuracy", currentResponse.MemoryReadout.NoteData.Accuracy, false);
                    CPH.SetGlobalVar("currentHitStreak", currentResponse.MemoryReadout.NoteData.CurrentHitStreak, false);
                    CPH.SetGlobalVar("currentMissStreak", currentResponse.MemoryReadout.NoteData.CurrentMissStreak, false);
                    CPH.SetGlobalVar("totalNotes", currentResponse.MemoryReadout.NoteData.TotalNotes, false);
                    CPH.SetGlobalVar("totalNotesHit", currentResponse.MemoryReadout.NoteData.TotalNotesHit, false);
                    CPH.SetGlobalVar("totalNotesMissed", currentResponse.MemoryReadout.NoteData.TotalNotesMissed, false);

                    UInt32 highestHitStreak = (UInt32)currentResponse.MemoryReadout.NoteData.HighestHitStreak;
                    CPH.SetGlobalVar("highestHitStreak", highestHitStreak, false);
                    if (highestHitStreak > highestStreakSinceLaunch)
                    {
                        highestStreakSinceLaunch = highestHitStreak;
                        CPH.SetGlobalVar("highestHitStreakSinceLaunch", highestStreakSinceLaunch, false);
                    }
                    
                    UInt32 additionalNotesHit;
                    UInt32 additionalNotesMissed;
                    UInt32 additionalNotes;
                    if (lastNoteData != null)
                    {
                        additionalNotesHit = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotesHit - lastNoteData.TotalNotesHit);
                        additionalNotesMissed = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotesMissed - lastNoteData.TotalNotesMissed);
                        additionalNotes = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotes - lastNoteData.TotalNotes);
                    }
                    else
                    {
                        additionalNotesHit = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotesHit);
                        additionalNotesMissed = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotesMissed);
                        additionalNotes = (uint)(currentResponse.MemoryReadout.NoteData.TotalNotes);
                    }
                    totalNotesHitThisStream += additionalNotesHit;
                    totalNotesMissedThisStream += additionalNotesMissed;
                    totalNotesThisStream += additionalNotes;
                    CPH.SetGlobalVar("totalNotesSinceLaunch", totalNotesThisStream, false);
                    CPH.SetGlobalVar("totalNotesHitSinceLaunch", totalNotesHitThisStream, false);
                    CPH.SetGlobalVar("totalNotesMissedSinceLaunch", totalNotesMissedThisStream, false);
                    if (totalNotesThisStream > 0)
                    {
                        accuracyThisStream = 100.0 * ((double)(totalNotesHitThisStream) / totalNotesThisStream);
                    }
                    CPH.SetGlobalVar("accuracySinceLaunch", accuracyThisStream, false);            

                    lastNoteData = currentResponse.MemoryReadout.NoteData;
                }
            }
        }
        catch ( ObjectDisposedException e)
        {
            debug("Caught object disposed exception when trying to save note data: " + e.Message);
            debug("Trying to reinitialize");
            Init();
        }
        catch (Exception e)
        {
           debug("Caught exception: " + e.Message);
           debug("Trying to reinitialize");
           Init(); 
        }
    }
    private bool identifyArrangement()
    {
        try
        {
            currentArrangement = null;
            currentSectionIndex = -1;
            if (currentResponse.SongDetails != null)
            {
                foreach (Arrangement arr in currentResponse.SongDetails.Arrangements)
                {
                   if (arr.ArrangementID == currentResponse.MemoryReadout.ArrangementId)
                   {
                        if (arr.ArrangementID == currentResponse.MemoryReadout.ArrangementId)
                        {
                            currentArrangement = arr;
                            break;
                        }
                   }
                }
            }
        }
        catch(Exception e)
        {
            debug("Caught exception trying to identify the arrangement: " + e.Message);
        }	
        return (currentArrangement != null);
    }
    private void identifySection()
    {
        if (currentArrangement != null)
        {
            try
            {
                string name = currentArrangement.Sections[currentSectionIndex].Name;
                if (name.ToLower().Contains("solo")) { currentSectionType = SectionType.Solo; }
                else if (name.ToLower().Contains("noguitar")) { currentSectionType = SectionType.NoGuitar; }
                else if (name.ToLower().Contains("riff")) { currentSectionType = SectionType.Riff; }
                else if (name.ToLower().Contains("bridge")) { currentSectionType = SectionType.Bridge; }
                else if (name.ToLower().Contains("breakdown")) { currentSectionType = SectionType.Breakdown; }
                else if (name.ToLower().Contains("chorus")) { currentSectionType = SectionType.Chorus; }
                else if (name.ToLower().Contains("verse")) { currentSectionType = SectionType.Verse; }
                else { currentSectionType = SectionType.Default; }
            }
            catch ( Exception e)
            {
                debug("Caught unknown exception trying to identify the section: " + e.Message);
            }

        }
        else
        { 
            currentSectionType = SectionType.Default; 
        }
    }
    private bool isInPause()
    {
        bool isPause = false;
        if (currentResponse.MemoryReadout.SongTimer.Equals(lastSongTimer)) 
        {   //Checking for zero, as otherwise the start of the song can be mistakenly identified as pause
            //When ending the song, there are a few responses with the same time before game state switches. Not triggering a pause if it's less than 250ms to end of song.
            if (currentResponse.MemoryReadout.SongTimer.Equals(0)
            || ((currentResponse.SongDetails.SongLength - currentResponse.MemoryReadout.SongTimer) < 0.25))
            {
                if ((sameTimeCounter++) >= minDelay)
                {
                    isPause = true;
                }
            }
            else
            { 
                isPause = true; 
            }        
        }
        else
        {
            sameTimeCounter = 0;
        }
        return isPause;
    }
    private void performSceneSwitchIfNecessary()
    {
        checkTunerActions();

        if (currentGameStage == GameStage.InSong)
        {
            checkGameStageSong();
        }
        else if (currentGameStage == GameStage.Menu)
        {
            checkGameStageMenu();
        }
        if (currentGameStage != lastGameStage)
        {
            CPH.SetGlobalVar("gameState", currentGameStage.ToString());
        }
        lastGameStage = currentGameStage;
        lastSongTimer = currentResponse.MemoryReadout.SongTimer;
    }
    private void checkGameStageSong()
    {
        if (lastGameStage != GameStage.InSong)
        {
            CPH.RunAction("SongStart");
        }

        if (!isArrangementIdentified)
        {
            isArrangementIdentified = identifyArrangement();
            saveSongMetaData();
        }
        if (!isSongScene(currentScene))
        {
            if (!currentResponse.MemoryReadout.SongTimer.Equals(lastSongTimer))
            {
                sameTimeCounter = 0;
                if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                {
                    if (currentScene.Equals(songPausedScene))
                    {
                        CPH.RunAction("leavePause");
                    }
                    if (isSwitchingScenes)
                    {
                        switchToScene(songScenes[currentSongSceneIndex]);
                        lastSceneChange = DateTime.Now;
                    }
                }
            }
            else
            {
                // Already in correct scene
            }
        }
        else if (isSongScene(currentScene))
        {
            if (isInPause())
            {
                CPH.RunAction("enterPause");
                if (isSwitchingScenes)
                {
                    if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                    {
                        switchToScene(songPausedScene);
                        lastSceneChange = DateTime.Now;
                    }
                }
            }

        }
    }

    private void checkGameStageMenu()
    {
        if (!currentScene.Equals(menuScene) && isSwitchingScenes)
        {
            if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
            {
                switchToScene(menuScene);
                lastSceneChange = DateTime.Now;
            }
        }
        if (lastGameStage == GameStage.InSong)
        {
            isArrangementIdentified = false;
            lastNoteData = null!;
            CPH.RunAction("SongEnd");
        }
    }

    private void checkTunerActions()
    {
        if ((currentGameStage == GameStage.InTuner) && (lastGameStage != GameStage.InTuner))
        {
            CPH.RunAction("enterTuner");
        }
        if ((currentGameStage != GameStage.InTuner) && (lastGameStage == GameStage.InTuner))
        {
            CPH.RunAction("leaveTuner");
        }
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
				if (currentSectionType != lastSectionType)
                {
                    CPH.RunAction(string.Format("leave{0}", Enum.GetName(typeof(SectionType),lastSectionType)));
                    CPH.RunAction(string.Format("enter{0}", Enum.GetName(typeof(SectionType),currentSectionType)));
                    lastSectionType = currentSectionType;
                }
            }
        }
    }
    
    // TODO needed?
    private void invalidateGlobalVariables()
    {
        CPH.UnsetGlobalVar("SongName");
        CPH.UnsetGlobalVar("ArtistName");
        CPH.UnsetGlobalVar("AlbumName");
        CPH.UnsetGlobalVar("Tuning");
        CPH.UnsetGlobalVar("Accuracy");
        CPH.UnsetGlobalVar("CurrentHitStreak");
        CPH.UnsetGlobalVar("CurrentMissStreak");
        CPH.UnsetGlobalVar("TotalNotes");
        CPH.UnsetGlobalVar("TotalNotesHit");
        CPH.UnsetGlobalVar("TotalNotesMissed");
    }
    public bool Execute()
    {
        DetermineConnectedBroadcastingSoftware();
        UpdateCurrentScene();

        if (IsRelevantScene())
        {
            if (getLatestResponse())
            {
                if (parseLatestResponse())
                {
                    try
                    {
                        saveNoteDataIfNecessary();
                    }
                    catch (ObjectDisposedException e)
                    {
                        debug("Caught object disposed exception when trying to save note data: " + e.Message);
                        debug("Trying to reinitialize");
                        Init();
                    }
                    catch (Exception e)
                    {
                        debug("Caught unknown exception when trying to write song meta data: " + e.Message);
                    }

                    try
                    {
                        performSceneSwitchIfNecessary();
                    }
                    catch (NullReferenceException e)
                    {
                        debug("Caught null reference in scene switch: " + e.Message);
                        debug("Reinitialising to fix the issue");
                        Init();
                    }

                    if (isReactingToSections)
                    {
                        checkSectionActions();
                    }
                }
            }
            else
            {
                debug("Fetching response failed, exiting action.");
                return false;
            }
        }

        return true;
    }

    public string GetStatus()
    {
        return $"currentScene={currentScene} currentSectionType={currentSectionType}";
    }

    // TODO needed?
    private void LogStatus()
    {
        debug(GetStatus());
    }

    private void LogCurrentScene()
    {
        debug($"currentScene={currentScene}");
    }

}
