using System;
using System.Net.Http;
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

    private string snifferIp = null!;
    private string snifferPort = null!;

    private GameStage currentGameStage;
    private GameStage lastGameStage;
    private SectionType currentSectionType;
    private SectionType lastSectionType;
    private ActivityBehavior itsBehavior;
    private string[] blackListedScenes = null!;
    private double currentSongTimer;
    private double lastSongTimer;

    private Arrangement? currentArrangement = null!;
    private int currentSectionIndex;
   
    private Response currentResponse = null!;
    private NoteData lastNoteData = null!;

    private UInt32 totalNotesThisStream;
    private UInt32 totalNotesHitThisStream;
    private UInt32 totalNotesMissedThisStream;
    private double accuracyThisStream;



    private string menuScene = null!;
    private string songScene = null!;
    private string songPausedScene = null!;
	private string currentScene = null!;
	
    private HttpClient client = null!;
    private HttpResponseMessage response = null!;
    private string responseString = null!;

    private DateTime lastSceneChange;
    private int minDelay;
    private int sameTimeCounter;
	private bool doLogToChat = false;
    private bool isSwitchingScenes = true;
    private bool isReactingToSections =true;
	private bool isArrangementIdentified = false;
    //Needs to be commented out in streamer bot.
    //private CPHmock CPH = new CPHmock();
    
    void debug(string str)
    {
        if (doLogToChat) CPH.SendMessage(str);
        CPH.LogDebug(str);
    }
    private GameStage evalGameStage(string stage)
    {
        GameStage currentStage = GameStage.Menu;
        // Other potential values are: MainMenu las_SongList las_SongOptions las_tuner
        if (stage.Equals("las_game"))
        {
            currentStage = GameStage.InSong;
        }
        else if (stage.Equals("las_tuner"))
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

        //Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = CPH.GetGlobalVar<string>("snifferIP").Replace('"',' ').Trim();
        snifferPort = "9938";
		menuScene = CPH.GetGlobalVar<string>("menuScene");
		songScene = CPH.GetGlobalVar<string>("songScene");
		songPausedScene = CPH.GetGlobalVar<string>("pauseScene");

        isSwitchingScenes = CPH.GetGlobalVar<string>("switchScenes").ToLower().Contains("true");
        isReactingToSections = CPH.GetGlobalVar<string>("sectionActions").ToLower().Contains("true");
		
        lastSceneChange = DateTime.Now;
        minDelay = 3;
        client = new HttpClient();
        if (client == null) debug("Failed instantiating HttpClient");
		currentScene = "";

        string behaviorString = CPH.GetGlobalVar<string>("behavior");
        if (behaviorString!= null)
        {
            if (behaviorString.ToLower().Contains("whitelist")) itsBehavior = ActivityBehavior.WhiteList;
            else if (behaviorString.ToLower().Contains("blacklist")) itsBehavior = ActivityBehavior.BlackList;
            else if (behaviorString.ToLower().Contains("always")) itsBehavior = ActivityBehavior.AlwaysOn;
            else
            {
                itsBehavior = ActivityBehavior.WhiteList;
                debug("Behavior not configured, setting to whitelist as default");
            }
        }

        if (itsBehavior == ActivityBehavior.BlackList)
        {
            string temp = CPH.GetGlobalVar<string>("blackList");
            blackListedScenes = temp.Split(',');
        }
        else
        {
            blackListedScenes = new string[1];
        }
        totalNotesThisStream= 0;
        totalNotesHitThisStream = 0;
        totalNotesMissedThisStream = 0;
        accuracyThisStream = 0;
        currentSectionIndex = -1;
        lastSectionType = currentSectionType = SectionType.Default;
        lastGameStage = currentGameStage = GameStage.Menu;
        sameTimeCounter= 0;
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
        if (!success) debug("Failed fetching response");
        return success;
    }
    private bool isRelevantScene()
    {
        bool isRelevant = false;
        currentScene = CPH.ObsGetCurrentScene();
        switch (itsBehavior)
        {
            case ActivityBehavior.WhiteList:
            {
                if (currentScene != null)
                {
                    if (currentScene.Equals(menuScene)
                    || currentScene.Equals(songScene)
                    || currentScene.Equals(songPausedScene))
                    {
                        isRelevant = true;
                    }
                }
                break;
            }
            case ActivityBehavior.BlackList:
            {
               isRelevant = true;
                foreach (string str in blackListedScenes)
                {
                    if(str.Trim().ToLower().Equals(currentScene.ToLower()))
                    {
                        isRelevant = false; 
                        break;
                    }
                }    
                break;
            }
            case ActivityBehavior.AlwaysOn:
            {
                 isRelevant= true;
                 break;
            }
            
        }

		
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
        return success;
    }
    private void saveSongMetaData()
    {
        CPH.SetGlobalVar("songName", currentResponse.SongDetails.SongName, false);
        CPH.SetGlobalVar("artistName", currentResponse.SongDetails.ArtistName, false);
        CPH.SetGlobalVar("albumName", currentResponse.SongDetails.AlbumName, false);
        if (currentArrangement != null)
        {
            CPH.SetGlobalVar("arrangement", currentArrangement.Name, false);
            CPH.SetGlobalVar("arrangementType", currentArrangement.type, false);
            CPH.SetGlobalVar("tuning", currentArrangement.Tuning.TuningName, false);
        }
    }
    private void saveNoteDataIfNecessary()
    {
        if (currentGameStage == GameStage.InSong)
        {
            if (lastNoteData != currentResponse.MemoryReadout.NoteData)
            {
                CPH.SetGlobalVar("accuracy", currentResponse.MemoryReadout.NoteData.Accuracy, false);
                CPH.SetGlobalVar("currentHitStreak", currentResponse.MemoryReadout.NoteData.CurrentHitStreak, false);
                CPH.SetGlobalVar("currentMissStreak", currentResponse.MemoryReadout.NoteData.CurrentMissStreak, false);
                CPH.SetGlobalVar("totalNotes", currentResponse.MemoryReadout.NoteData.TotalNotes, false);
                CPH.SetGlobalVar("totalNotesHit", currentResponse.MemoryReadout.NoteData.TotalNotesHit, false);
                CPH.SetGlobalVar("totalNotesMissed", currentResponse.MemoryReadout.NoteData.TotalNotesMissed, false);
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
                totalNotesMissedThisStream+= additionalNotesMissed;
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
    private bool identifyArrangement()
    {
        currentArrangement = null;
        currentSectionIndex = -1;
        if (currentResponse.SongDetails != null) 
        { 
            foreach (Arrangement arr in currentResponse.SongDetails.Arrangements)
            {
                if (arr.ArrangementID == currentResponse.MemoryReadout.ArrangementId)
                {
					currentArrangement = arr;
                    break;
                }
            }
        }
		return (currentArrangement != null);
    }
    private void identifySection()
    {
        if (currentArrangement != null)
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
        if ((currentGameStage == GameStage.InTuner) && (lastGameStage != GameStage.InTuner))
        { 
            CPH.RunAction("enterTuner");
        }
        if ((currentGameStage != GameStage.InTuner) && (lastGameStage == GameStage.InTuner))
        {
            CPH.RunAction("leaveTuner");
        }

        if (currentGameStage == GameStage.InSong)
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
            if (!currentScene.Equals(songScene))
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
                            CPH.ObsSetScene(songScene);
                            lastSceneChange = DateTime.Now;
                        }
                    }
                }
                else
                {
                    //Already in correct scene
                }
            }
            else if (currentScene.Equals(songScene))
            {
                if (isInPause())
                {
                    CPH.RunAction("enterPause");
                    if (isSwitchingScenes)
                    {
                        if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                        {
                            CPH.ObsSetScene(songPausedScene);
                            lastSceneChange = DateTime.Now;
                        }
                    }
                }
               
            }
        }
        else if (currentGameStage == GameStage.Menu)
        {
            if (!currentScene.Equals(menuScene) && isSwitchingScenes)
            {
                if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                {
                    CPH.ObsSetScene(menuScene);
                    lastSceneChange = DateTime.Now;
                }
            }
            if (lastGameStage == GameStage.InSong)
            {
                isArrangementIdentified = false;
				invalidateGlobalVariables();
                CPH.RunAction("SongEnd");
            }
        }
        if (currentGameStage != lastGameStage)
        {
            CPH.SetGlobalVar("gameState",currentGameStage.ToString());
        }
        lastGameStage = currentGameStage;
        lastSongTimer = currentResponse.MemoryReadout.SongTimer;
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
        if (isRelevantScene())
        {
            if (getLatestResponse())
            {
                if (parseLatestResponse())
                {
                    saveNoteDataIfNecessary();
                    performSceneSwitchIfNecessary();
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
}