using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;



// Objects for parsing the song data
// 

public record MemoryReadout
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
public record NoteData
{
    public double Accuracy { get; set; }
    public int TotalNotes { get; set; }
    public int TotalNotesHit { get; set; }
    public int CurrentHitStreak { get; set; }

    public int HighestHitStreak { get; set; }
    public int TotalNotesMissed { get; set; }
    public int CurrentMissStreak { get; set; }
}
public record SongDetails
{
    public string SongName { get; set; } = null!;
    public string ArtistName { get; set; } = null!;
    public double SongLength { get; set; }
    public string AlbumName { get; set; } = null!;
    public int AlbumYear { get; set; }
    public Arrangement[] Arrangements { get; set; } = null!;

}


public record Arrangement
{
    public string Name { get; set; } = null!;
    public string ArrangementID { get; set; } = null!;
    public string type { get; set; } = null!;
    public Tuning Tuning { get; set; } = null!;

    public Section[] Sections { get; set; } = null!;
}
public record Tuning
{
    public string TuningName { get; set; } = null!;
}

public record Section
{
    public string Name { get; set; } = null!;
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
public record Response
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
        , NoGuitar
        , Riff
        , Solo
        , Verse
        , Chorus
        , Bridge
        , Breakdown
    }
    enum ActivityBehavior
    {
        WhiteList
        , BlackList
        , AlwaysOn
    }


    public class SceneInteractor
    {
        enum BroadcastingSoftware
        {
          OBS
        , SLOBS
        , NONE
        }
        private IInlineInvokeProxy CPH;
        private BroadcastingSoftware itsBroadcastingSoftware;

        public SceneInteractor(IInlineInvokeProxy cph)
        {
            CPH= cph;
        }

        private void DetermineConnectedBroadcastingSoftware()
        {
            if (CPH.ObsIsConnected())
                itsBroadcastingSoftware = BroadcastingSoftware.OBS;
            else if (CPH.SlobsIsConnected())
                itsBroadcastingSoftware = BroadcastingSoftware.SLOBS;
            else
                itsBroadcastingSoftware = BroadcastingSoftware.NONE;
        }

        public string GetCurrent()
        {
            DetermineConnectedBroadcastingSoftware();
            string scene = "";
            switch (itsBroadcastingSoftware)
            {
                case BroadcastingSoftware.OBS:
                    {
                        scene = CPH.ObsGetCurrentScene();
                        break;
                    }
                case BroadcastingSoftware.SLOBS:
                    {
                        scene = CPH.SlobsGetCurrentScene();
                        break;
                    }
                case BroadcastingSoftware.NONE:
                default:
                {
                    scene = "";
                    break;
                }
            }
            return scene;
        }

        public void SwitchToScene(string scene)
        {
            switch (itsBroadcastingSoftware)
            {
                case BroadcastingSoftware.OBS:
                    {
                        CPH.ObsSetScene(scene);
                        break;
                    }
                case BroadcastingSoftware.SLOBS:
                    {
                        CPH.SlobsSetScene(scene);
                        break;
                    }
                case BroadcastingSoftware.NONE:
                default:
                    {
                        CPH.LogDebug("No stream program defined");
                        break;
                    }
            }
        }
    }

    public class ResponseFetcher
    {
        private IInlineInvokeProxy CPH;
        private readonly string ip;
        private readonly string port;
        private HttpResponseMessage response = null!;
        private HttpClient client = null!;

        public ResponseFetcher(IInlineInvokeProxy  cph, string ip, string port)
        {
            CPH = cph;
            this.ip = ip;
            this.port = port;

            client = new HttpClient();
        }

        public string Fetch()
        {
            string responseString = string.Empty;
            try
            {
                string address = string.Format("http://{0}:{1}", ip, port);
                response = client.GetAsync(address).GetAwaiter().GetResult();
                if (response != null)
                {
                    response.EnsureSuccessStatusCode();
                    responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                else
                {

                    CPH.LogDebug("Response is null");
                }
            }
            catch (HttpRequestException e)
            {
                CPH.LogDebug("Error in response");
                CPH.LogDebug(string.Format("Caught exception trying to get response from sniffer: {0}", e.Message));
            }
            catch (ObjectDisposedException e)
            {
                CPH.LogDebug("HttpClient was disposed. Exception: " + e.Message + " Reinitialising.");
                throw e;
            }
            catch (Exception e)
            {
                CPH.LogDebug("Caught unknown exception trying to read from HttpClient: " + e.Message);
            }
            
            return responseString;
        }

        public Response ExtractResponse(string responseString)
        {
            Response? currentResponse = null;
            try
            {
                currentResponse = JsonConvert.DeserializeObject<Response>(responseString) ?? throw new Exception("Is never supposed to be zero");                
            }
            catch (JsonException ex)
            {
                CPH.LogDebug("Error parsing response: " + ex.Message);
                currentResponse= null;
            }
            catch (Exception e)
            {
                CPH.LogDebug("Caught exception when trying to deserialize response string");
                CPH.LogDebug("Exception: " + e.Message);
                currentResponse = null;
            }
            return currentResponse;
        }
    }
    public class ResponseParser
    {
        private GameStage currentGameStage;
        private GameStage lastGameStage;
        private SectionType currentSectionType;
        private SectionType lastSectionType;
        private ActivityBehavior itsBehavior;
        private SceneInteractor itsSceneInterActor;

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

        private DateTime lastSceneChange;
        private int minDelay;
        private int sameTimeCounter;
        private string currentScene = null!;

        private bool switchScenes = true;
        private bool reactingToSections = true;
        private bool arrangementIdentified = false;
        private IInlineInvokeProxy CPH;
        
        public ResponseParser(IInlineInvokeProxy cph, SceneInteractor interactor)
        {
            CPH = cph;
            itsSceneInterActor = interactor;
        }

        public void SetResponse(Response response)
        {
            currentResponse = response;
        }
        public void setCurrentScene(string scene)
        {
            currentScene = scene;
        }

        public void Init()
        {
            menuScene = CPH.GetGlobalVar<string>("menuScene");
            CPH.LogInfo("Menu scene: " + menuScene);
            songScenes = Regex.Split(CPH.GetGlobalVar<string>("songScenes").Trim(), @"\s*[,;]\s*");
            CPH.LogInfo("Song scene: " + string.Join(", ", songScenes));
            songPausedScene = CPH.GetGlobalVar<string>("pauseScene");
            CPH.LogInfo("Song paused scene: " + songPausedScene);

            switchScenes = CPH.GetGlobalVar<string>("switchScenes").ToLower().Contains("true");
            CPH.LogInfo("Switching scenes configured to " + switchScenes);
            reactingToSections = CPH.GetGlobalVar<string>("sectionActions").ToLower().Contains("true");
            CPH.LogInfo("Section actions are configured to " + reactingToSections);
            lastSceneChange = DateTime.Now;
            minDelay = 3;

            string behaviorString = CPH.GetGlobalVar<string>("behavior");
            if (behaviorString != null)
            {
                if (behaviorString.ToLower().Contains("whitelist")) itsBehavior = ActivityBehavior.WhiteList;
                else if (behaviorString.ToLower().Contains("blacklist")) itsBehavior = ActivityBehavior.BlackList;
                else if (behaviorString.ToLower().Contains("always")) itsBehavior = ActivityBehavior.AlwaysOn;
                else
                {
                    itsBehavior = ActivityBehavior.WhiteList;
                    CPH.LogDebug("Behavior not configured, setting to whitelist as default");
                }
                CPH.LogInfo("Behavior configured as " + itsBehavior);
            }

            if (itsBehavior == ActivityBehavior.BlackList)
            {
                blackListedScenes = Regex.Split(CPH.GetGlobalVar<string>("blackList").Trim(), @"\s*[,;]\s*");
                CPH.LogInfo("The following scenes are blacklisted:");
                foreach (string str in blackListedScenes)
                {
                    CPH.LogInfo(str);
                }
            }
            else
            {
                blackListedScenes = new string[1];
            }

            totalNotesThisStream = 0;
            totalNotesHitThisStream = 0;
            totalNotesMissedThisStream = 0;
            accuracyThisStream = 0;
            highestStreakSinceLaunch = 0;
            currentSectionIndex = -1;
            currentSongSceneIndex = 0;
            lastSectionType = currentSectionType = SectionType.Default;
            lastGameStage = currentGameStage = GameStage.Menu;
            sameTimeCounter = 0;
        }

        private GameStage EvalGameStage(string stage)
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

        public void UpdateStageAndTimer()
        {
            currentGameStage = EvalGameStage(currentResponse.MemoryReadout.GameStage);
            currentSongTimer = currentResponse.MemoryReadout.SongTimer;
        }

        public bool IsRelevantScene()
        {
            bool isRelevant = false;

            switch (itsBehavior)
            {
                case ActivityBehavior.WhiteList:
                    {
                        if (currentScene.Equals(menuScene)
                        || IsSongScene(currentScene)
                        || currentScene.Equals(songPausedScene))
                        {
                            isRelevant = true;
                        }
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

            return isRelevant;
        }

        public bool IsSongScene(string scene)
        {
            foreach (var s in songScenes)
            {
                if (scene.Equals(s))
                {
                    return true;
                }
            }

            return false;
        }

        public void SaveSongMetaData()
        {
            try
            {
                CPH.SetGlobalVar("songName", currentResponse.SongDetails.SongName, false);
                CPH.SetGlobalVar("artistName", currentResponse.SongDetails.ArtistName, false);
                CPH.SetGlobalVar("albumName", currentResponse.SongDetails.AlbumName, false);
                CPH.SetGlobalVar("songLength", (int)currentResponse.SongDetails.SongLength, false);
                CPH.SetGlobalVar("songLengthFormatted", FormatTime((int)currentResponse.SongDetails.SongLength), false);
                if (currentArrangement == null) return;
                CPH.SetGlobalVar("arrangement", currentArrangement.Name, false);
                CPH.SetGlobalVar("arrangementType", currentArrangement.type, false);
                CPH.SetGlobalVar("tuning", currentArrangement.Tuning.TuningName, false);
            }
            catch (ObjectDisposedException e)
            {
                CPH.LogDebug("Caught object disposed exception when trying to save meta data: " + e.Message);
                throw e;
            }
            catch (Exception e)
            {
                CPH.LogDebug("Caught exception trying to save song meta data");
                CPH.LogDebug("Exception: " + e.Message);
                throw;
            }
        }
        public void SaveNoteDataIfNecessary()
        {
            try
            {
                if (currentGameStage == GameStage.InSong)
                {
                    CPH.SetGlobalVar("songTimer", (int)currentResponse.MemoryReadout.SongTimer, false);
                    string formatted = FormatTime((int)currentResponse.MemoryReadout.SongTimer);
                    CPH.SetGlobalVar("songTimerFormatted", formatted, false);
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
            catch (ObjectDisposedException e)
            {
                CPH.LogDebug("Caught object disposed exception when trying to save note data: " + e.Message);
                throw;
            }
            catch (Exception e)
            {
                CPH.LogDebug("Caught exception: " + e.Message);
                throw;
            }
        }
        public bool IdentifyArrangement()
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
            catch (Exception e)
            {
                CPH.LogDebug("Caught exception trying to identify the arrangement: " + e.Message);
            }
            return (currentArrangement != null);
        }
        public void IdentifySection()
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
                catch (Exception e)
                {
                    CPH.LogDebug("Caught unknown exception trying to identify the section: " + e.Message);
                }

            }
            else
            {
                currentSectionType = SectionType.Default;
            }
        }
        public bool IsInPause()
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
        public void PerformSceneSwitchIfNecessary()
        {
            CheckTunerActions();

            if (currentGameStage == GameStage.InSong)
            {
                CheckGameStageSong();
            }
            else if (currentGameStage == GameStage.Menu)
            {
                CheckGameStageMenu();
            }
            if (currentGameStage != lastGameStage)
            {
                CPH.SetGlobalVar("gameState", currentGameStage.ToString());
            }
            lastGameStage = currentGameStage;
            lastSongTimer = currentResponse.MemoryReadout.SongTimer;
        }
        public void CheckGameStageSong()
        {
            if (lastGameStage != GameStage.InSong)
            {
                CPH.RunAction("SongStart");
            }

            if (!arrangementIdentified)
            {
                arrangementIdentified = IdentifyArrangement();
                SaveSongMetaData();
            }
            if (!IsSongScene(currentScene))
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
                        if (switchScenes)
                        {
                            itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex]);
                            lastSceneChange = DateTime.Now;
                        }
                    }
                }
                else
                {
                    // Already in correct scene
                }
            }
            else if (IsSongScene(currentScene))
            {
                if (IsInPause())
                {
                    CPH.RunAction("enterPause");
                    if (switchScenes)
                    {
                        if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                        {
                            itsSceneInterActor.SwitchToScene(songPausedScene);
                            lastSceneChange = DateTime.Now;
                        }
                    }
                }

            }
        }

        public void CheckGameStageMenu()
        {
            if (!currentScene.Equals(menuScene) && switchScenes)
            {
                if ((DateTime.Now - lastSceneChange).TotalSeconds > minDelay)
                {
                    itsSceneInterActor.SwitchToScene(menuScene);
                    lastSceneChange = DateTime.Now;
                }
            }
            if (lastGameStage == GameStage.InSong)
            {
                arrangementIdentified = false;
                lastNoteData = null!;
                CPH.RunAction("SongEnd");
            }
        }

        public void CheckTunerActions()
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
        public void CheckSectionActions()
        {
            if (currentArrangement != null && reactingToSections)
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
                    IdentifySection();
                    if (currentSectionType != lastSectionType)
                    {
                        CPH.RunAction(string.Format("leave{0}", Enum.GetName(typeof(SectionType), lastSectionType)));
                        CPH.RunAction(string.Format("enter{0}", Enum.GetName(typeof(SectionType), currentSectionType)));
                        lastSectionType = currentSectionType;
                    }
                }
            }
        }

        private static string FormatTime(int totalSeconds)
        {
            return TimeSpan.FromSeconds(totalSeconds).ToString();
        }

    }

    //Needs to be commented out in streamer bot.
    private CPHmock CPH = new CPHmock();
    private SceneInteractor itsSceneInteractor = null!;
    private ResponseFetcher itsFetcher = null!;
    private ResponseParser itsParser = null!;

    private string snifferIp = null!;
    private string snifferPort = null!;

    private string currentScene = null!;

    private void UpdateCurrentScene()
    {
        currentScene = itsSceneInteractor.GetCurrent();
        itsParser.setCurrentScene(currentScene);
    }

    public void Init()
    {
        CPH.LogInfo("Initialising RockSniffer to SB plugin");
        //Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = GetSnifferIp();
        // TODO snifferPort should be also configurable
        snifferPort = "9938"; 
        CPH.LogInfo(string.Format("Sniffer ip configured as {0}:{1}",snifferIp,snifferPort));
        itsSceneInteractor = new SceneInteractor(CPH);
        itsFetcher = new ResponseFetcher(CPH,snifferIp,snifferPort);
        itsParser = new ResponseParser(CPH, itsSceneInteractor);
        itsParser.Init();

        currentScene = "";
        
    }

    private string GetSnifferIp()
    {
        return CPH.GetGlobalVar<string>("snifferIP").Replace('"',' ').Trim();
    }

    
    public bool Execute()
    {       
        UpdateCurrentScene();

        if (itsParser.IsRelevantScene())
        {
            string response = itsFetcher.Fetch();
            
            if (response != string.Empty)
            { 
                Response currentResponse = itsFetcher.ExtractResponse(response);

                if (currentResponse != null)
                {
                    itsParser.SetResponse(currentResponse);
                    itsParser.UpdateStageAndTimer();

                    try
                    {
                        itsParser.SaveNoteDataIfNecessary();
                    }
                    catch (ObjectDisposedException e)
                    {
                        CPH.LogDebug("Caught object disposed exception when trying to save note data: " + e.Message);
                        CPH.LogDebug("Trying to reinitialize");
                        Init();
                    }
                    catch (Exception e)
                    {
                        CPH.LogDebug("Caught unknown exception when trying to write song meta data: " + e.Message);
                    }

                    try
                    {
                        itsParser.PerformSceneSwitchIfNecessary();
                    }
                    catch (NullReferenceException e)
                    {
                        CPH.LogDebug("Caught null reference in scene switch: " + e.Message);
                        CPH.LogDebug("Reinitialising to fix the issue");
                        Init();
                    }

                    itsParser.CheckSectionActions();
                    

                }
            }
            else
            {
                CPH.LogDebug("Fetching response failed, exiting action.");
                return false;
            }
        }

        return true;
    }





}