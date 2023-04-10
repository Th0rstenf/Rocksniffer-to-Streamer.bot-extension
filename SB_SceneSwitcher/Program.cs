using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public struct Constants
{
    public const string AppName = "RS2SB :: ";

    public const string GlobalVarNameSnifferIP = "snifferIP";
    public const string GlobalVarNameSnifferPort = "snifferPort";
    public const string GlobalVarNameMenuScene = "menuScene";
    public const string GlobalVarNameMenuSongScenes = "songScenes";
    public const string GlobalVarNamePauseScene = "pauseScene";
    public const string GlobalVarNameSongSceneAutoSwitchMode = "songSceneAutoSwitchMode";
    public const string GlobalVarNameSwitchScenes = "switchScenes";
    public const string GlobalVarNameSectionActions = "sectionActions";
    public const string GlobalVarNameSceneSwitchPeriod = "sceneSwitchPeriod";
    public const string GlobalVarNameBehavior = "behavior";
    public const string GlobalVarNameBlackList = "blackList";

    public const string ActionNameSongStart = "SongStart";
    public const string ActionNameLeavePause = "leavePause";
    public const string ActionNameEnterPause = "enterPause";
    public const string ActionNameSongEnd = "SongEnd";
    public const string ActionNameEnterTuner = "enterTuner";
    public const string ActionNameLeaveTuner = "leaveTuner";

    public const string SnifferPortDefault = "9938";
}

internal enum SongSceneAutoSwitchMode
{
    Off,
    Sequential,
    Random
}

// Objects for parsing the song data
// 

public record MemoryReadout
{
    [JsonRequired] public string SongId { get; set; } = null!;
    [JsonRequired] public string ArrangementId { get; set; } = null!;
    [JsonRequired] public string GameStage { get; set; } = null!;
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
        Menu,
        InSong,
        InTuner
    }

    enum SectionType
    {
        Default,
        NoGuitar,
        Riff,
        Solo,
        Verse,
        Chorus,
        Bridge,
        Breakdown
    }

    enum ActivityBehavior
    {
        WhiteList,
        BlackList,
        AlwaysOn
    }

    public class SceneInteractor
    {
        private const string MessageNoStreamAppConnectionAvailable =
            Constants.AppName +
            "No stream app connection available! Please set and connect either to OBS or SLOBS under 'Stream Apps' in SB!";

        enum BroadcastingSoftware
        {
            OBS,
            SLOBS
        }

        private int cooldownPeriod;
        private DateTime lastSceneChange;
        private IInlineInvokeProxy CPH;
        private BroadcastingSoftware? itsBroadcastingSoftware;

        public SceneInteractor(IInlineInvokeProxy cph)
        {
            CPH = cph;
            cooldownPeriod = 3;
            lastSceneChange = DateTime.Now;
        }

        public void DetermineAndSetConnectedBroadcastingSoftware()
        {
            if (CPH.ObsIsConnected())
            {
                itsBroadcastingSoftware = BroadcastingSoftware.OBS;
                CPH.LogDebug(Constants.AppName + $"Connected to BroadcastingSoftware {BroadcastingSoftware.OBS}");
            }
            else if (CPH.SlobsIsConnected())
            {
                itsBroadcastingSoftware = BroadcastingSoftware.SLOBS;
                CPH.LogDebug(Constants.AppName + $"Connected to BroadcastingSoftware {BroadcastingSoftware.SLOBS}");
            }
            else
            {
                itsBroadcastingSoftware = null;
                CPH.LogDebug(MessageNoStreamAppConnectionAvailable);
            }
        }

        public string GetCurrentScene()
        {
            DetermineAndSetConnectedBroadcastingSoftware();

            return itsBroadcastingSoftware switch
            {
                BroadcastingSoftware.OBS => CPH.ObsGetCurrentScene(),
                BroadcastingSoftware.SLOBS => CPH.SlobsGetCurrentScene(),
                _ => ""
            };
        }

        public void SwitchToScene(string scene, bool switchScenes)
        {
            if (switchScenes && IsNotInCooldown())
            {
                switch (itsBroadcastingSoftware)
                {
                    case BroadcastingSoftware.OBS:
                        CPH.LogInfo(Constants.AppName + $"Switching to OBS scene: {scene}");
                        CPH.ObsSetScene(scene);
                        break;
                    case BroadcastingSoftware.SLOBS:
                        CPH.LogInfo(Constants.AppName + $"Switching to SLOBS scene: {scene}");
                        CPH.SlobsSetScene(scene);
                        break;
                    default:
                        CPH.LogDebug(MessageNoStreamAppConnectionAvailable);
                        break;
                }

                lastSceneChange = DateTime.Now;
            }
        }

        public bool IsNotInCooldown()
        {
            var timeSinceLastSceneChange = GetTimeSinceLastSceneChange();
            var isNotInCooldown = !(timeSinceLastSceneChange < cooldownPeriod);
            CPH.LogVerbose(
                $"{Constants.AppName}isNotInCooldown={isNotInCooldown} - " +
                $"timeSinceLastSceneChange={timeSinceLastSceneChange} " +
                $"cooldownPeriod={cooldownPeriod} ");
            return isNotInCooldown;
        }

        public double GetTimeSinceLastSceneChange()
        {
            var timeSinceLastSceneChange = DateTime.Now.Subtract(lastSceneChange).TotalSeconds;
            CPH.LogVerbose($"timeSinceLastSceneChange={timeSinceLastSceneChange}");
            return timeSinceLastSceneChange;
        }
    }

    public class ResponseFetcher
    {
        private IInlineInvokeProxy CPH;
        private readonly string ip;
        private readonly string port;
        private HttpResponseMessage response = null!;
        private HttpClient client = null!;

        public ResponseFetcher(IInlineInvokeProxy cph, string ip, string port)
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
                // TODO always false?
                if (response == null)
                {
                    // TODO in case Response is null, no need to continue! Drop an error!?
                    CPH.LogWarn(Constants.AppName + "Response is null");
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
            catch (HttpRequestException e)
            {
                CPH.LogWarn(Constants.AppName + $"Exception, when trying to get response from sniffer: {e.Message}");
                throw;
            }
            catch (ObjectDisposedException e)
            {
                CPH.LogWarn(Constants.AppName + $"HttpClient was disposed. Exception: {e.Message} Reinitialising.");
                throw;
            }
            catch (Exception e)
            {
                CPH.LogWarn(
                    Constants.AppName + $"Caught an Exception, when trying to read from HttpClient: {e.Message}");
                throw;
            }

            return responseString;
        }

        public Response ExtractResponse(string responseString)
        {
            Response? currentResponse;
            try
            {
                currentResponse = JsonConvert.DeserializeObject<Response>(responseString) ??
                                  throw new Exception(Constants.AppName + "Is never supposed to be zero");
            }
            catch (JsonException ex)
            {
                CPH.LogWarn(Constants.AppName + $"Error parsing response: {ex.Message}");
                throw;
            }
            catch (Exception e)
            {
                CPH.LogWarn(Constants.AppName +
                            $"Caught exception when trying to deserialize response string! Exception: {e.Message}");
                throw;
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
        private ActivityBehavior itsBehavior = ActivityBehavior.WhiteList;
        private SceneInteractor itsSceneInterActor;

        private string[]? blackListedScenes = null!;
        private double currentSongTimer;
        private double lastSongTimer;

        private Arrangement? currentArrangement = null!;
        private int currentSectionIndex;
        private int currentSongSceneIndex;
        private int sceneSwitchPeriodInSeconds = 5;

        private Response currentResponse = null!;
        private NoteData lastNoteData = null!;

        private UInt32 totalNotesThisStream;
        private UInt32 totalNotesHitThisStream;
        private UInt32 totalNotesMissedThisStream;
        private double accuracyThisStream;
        private UInt32 highestStreakSinceLaunch;

        private string menuScene = null!;
        private string[]? songScenes = null!;
        private string songPausedScene = null!;

        private int sameTimeCounter;
        private string currentScene = "";

        private bool switchScenes = true;
        private SongSceneAutoSwitchMode songSceneAutoSwitchMode = SongSceneAutoSwitchMode.Off;
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

        public void SetCurrentScene(string scene)
        {
            currentScene = scene;
        }

        public void Init()
        {
            menuScene = GetGlobalVarAsString(Constants.GlobalVarNameMenuScene);
            songScenes = GetGlobalVarAsStringArray(Constants.GlobalVarNameMenuSongScenes);
            songPausedScene = GetGlobalVarAsString(Constants.GlobalVarNamePauseScene);

            switchScenes = GetGlobalVarAsBool(Constants.GlobalVarNameSwitchScenes);
            songSceneAutoSwitchMode = GetGlobalVarSongSceneAutoSwitchMode();
            reactingToSections = GetGlobalVarAsBool(Constants.GlobalVarNameSectionActions);

            sceneSwitchPeriodInSeconds = GetGlobalVarSceneSwitchPeriod();

            itsBehavior = GetGlobalVarBehavior();
            blackListedScenes = GetGlobalVarBlackListedScenes();

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

        private string GetGlobalVarAsString(string name)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            CPH.LogInfo($"{Constants.AppName}{name}={globalVar}");
            return globalVar;
        }

        private bool GetGlobalVarAsBool(string name)
        {
            var globalVar = CPH.GetGlobalVar<string>(name).ToLower().Contains("true");
            CPH.LogInfo($"{Constants.AppName}{name}={globalVar}");
            return globalVar;
        }

        private string[]? GetGlobalVarAsStringArray(string name)
        {
            var rawValue = CPH.GetGlobalVar<string>(name);
            CPH.LogVerbose($"{Constants.AppName}{name} raw={rawValue}");

            if (string.IsNullOrEmpty(rawValue)) return null;

            var trimmedValues = Regex.Split(rawValue.Trim(), @"\s*[,;]\s*");
            CPH.LogInfo($"{Constants.AppName}{name}=[{string.Join(",", trimmedValues)}]");

            return trimmedValues;
        }

        private ActivityBehavior GetGlobalVarBehavior()
        {
            var behavior = GetBehavior(CPH.GetGlobalVar<string>(Constants.GlobalVarNameBehavior));
            CPH.LogInfo($"{Constants.AppName}{nameof(behavior)}={behavior}");
            return behavior;
        }

        private static ActivityBehavior GetBehavior(string globalVar)
        {
            if (string.IsNullOrEmpty(globalVar))
                return ActivityBehavior.WhiteList;

            return globalVar.ToLower().Trim() switch
            {
                "whitelist" => ActivityBehavior.WhiteList,
                "blacklist" => ActivityBehavior.BlackList,
                "alwayson" => ActivityBehavior.AlwaysOn,
                _ => ActivityBehavior.WhiteList
            };
        }

        private SongSceneAutoSwitchMode GetGlobalVarSongSceneAutoSwitchMode()
        {
            var autoSwitchMode =
                GetSongSceneAutoSwitchMode(CPH.GetGlobalVar<string>(Constants.GlobalVarNameSongSceneAutoSwitchMode));
            CPH.LogInfo($"{Constants.AppName}{nameof(autoSwitchMode)}={autoSwitchMode}");
            return autoSwitchMode;
        }

        private static SongSceneAutoSwitchMode GetSongSceneAutoSwitchMode(string globalVar)
        {
            if (string.IsNullOrEmpty(globalVar))
                return SongSceneAutoSwitchMode.Off;

            return globalVar.ToLower().Trim() switch
            {
                "off" => SongSceneAutoSwitchMode.Off,
                "sequential" => SongSceneAutoSwitchMode.Sequential,
                "random" => SongSceneAutoSwitchMode.Random,
                _ => SongSceneAutoSwitchMode.Off
            };
        }

        private int GetGlobalVarSceneSwitchPeriod()
        {
            var sceneSwitchPeriodVar = CPH.GetGlobalVar<string>(Constants.GlobalVarNameSceneSwitchPeriod);
            // how to parse string to int
            var sceneSwitchPeriod = string.IsNullOrEmpty(sceneSwitchPeriodVar) ? 5 : int.Parse(sceneSwitchPeriodVar);
            CPH.LogInfo($"{Constants.AppName}{nameof(sceneSwitchPeriod)}={sceneSwitchPeriod}");
            return sceneSwitchPeriod;
        }

        private string[] GetGlobalVarBlackListedScenes()
        {
            return (itsBehavior == ActivityBehavior.BlackList
                ? GetGlobalVarAsStringArray(Constants.GlobalVarNameBlackList)
                : new string[1])!;
        }

        private GameStage EvalGameStage(string stage)
        {
            // Other potential values are: MainMenu las_SongList las_SongOptions las_tuner
            if (stage.Equals("las_game") || stage.Equals("sa_game"))
                return GameStage.InSong;

            if (stage.Contains("tuner"))
                return GameStage.InTuner;

            return GameStage.Menu;
        }

        public void UpdateStageAndTimer()
        {
            if (currentResponse.MemoryReadout == null)
            {
                throw new Exception(
                    Constants.AppName +
                    "Could not read Sniffer game values! Please check configuration and run Rocksmith and RockSniffer!");
            }

            currentGameStage = EvalGameStage(currentResponse.MemoryReadout.GameStage);
            currentSongTimer = currentResponse.MemoryReadout.SongTimer;
        }

        public bool IsRelevantScene()
        {
            var isRelevant = false;

            CPH.LogVerbose(Constants.AppName + $"IsRelevantScene - itsBehavior={itsBehavior}");
            switch (itsBehavior)
            {
                case ActivityBehavior.WhiteList:
                {
                    CPH.LogDebug(Constants.AppName + "IsRelevantScene - case ActivityBehavior.WhiteList");
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
                    CPH.LogDebug(Constants.AppName + "IsRelevantScene - case ActivityBehavior.BlackList");
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
                    CPH.LogDebug(Constants.AppName + "IsRelevantScene - case ActivityBehavior.AlwaysOn");
                    isRelevant = true;
                    break;
                }

                default:
                    CPH.LogDebug(Constants.AppName + "IsRelevantScene - case default --> not relevant");
                    isRelevant = false;
                    break;
            }

            CPH.LogVerbose(Constants.AppName + $"IsRelevantScene - itsBehavior={itsBehavior} isRelevant={isRelevant}");
            return isRelevant;
        }

        private bool IsSongScene(string scene)
        {
            return Array.Find(songScenes, s => s.Equals(scene)) != null;
        }

        private bool IsNotSongScene(string scene)
        {
            return !IsSongScene(scene);
        }


        private void SaveSongMetaData()
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
                CPH.LogWarn(Constants.AppName +
                            $"Caught object disposed exception when trying to save meta data: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                CPH.LogWarn(Constants.AppName +
                            $"Caught exception trying to save song meta data! Exception: {e.Message}");
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
                    CPH.SetGlobalVar("songTimerFormatted", FormatTime((int)currentResponse.MemoryReadout.SongTimer),
                        false);

                    if (lastNoteData != currentResponse.MemoryReadout.NoteData)
                    {
                        CPH.LogVerbose(Constants.AppName + "Note data has changed, saving new values");

                        CPH.SetGlobalVar("accuracy", currentResponse.MemoryReadout.NoteData.Accuracy, false);
                        CPH.SetGlobalVar("currentHitStreak", currentResponse.MemoryReadout.NoteData.CurrentHitStreak,
                            false);
                        CPH.SetGlobalVar("currentMissStreak", currentResponse.MemoryReadout.NoteData.CurrentMissStreak,
                            false);
                        CPH.SetGlobalVar("totalNotes", currentResponse.MemoryReadout.NoteData.TotalNotes, false);
                        CPH.SetGlobalVar("totalNotesHit", currentResponse.MemoryReadout.NoteData.TotalNotesHit, false);
                        CPH.SetGlobalVar("totalNotesMissed", currentResponse.MemoryReadout.NoteData.TotalNotesMissed,
                            false);

                        int highestHitStreak = currentResponse.MemoryReadout.NoteData.HighestHitStreak;
                        CPH.SetGlobalVar("highestHitStreak", highestHitStreak, false);
                        if (highestHitStreak > highestStreakSinceLaunch)
                        {
                            highestStreakSinceLaunch = (uint)highestHitStreak;
                            CPH.SetGlobalVar("highestHitStreakSinceLaunch", highestStreakSinceLaunch, false);
                        }

                        int additionalNotesHit;
                        int additionalNotesMissed;
                        int additionalNotes;
                        if (lastNoteData != null)
                        {
                            additionalNotesHit = currentResponse.MemoryReadout.NoteData.TotalNotesHit -
                                                 lastNoteData.TotalNotesHit;
                            additionalNotesMissed = currentResponse.MemoryReadout.NoteData.TotalNotesMissed -
                                                    lastNoteData.TotalNotesMissed;
                            additionalNotes = currentResponse.MemoryReadout.NoteData.TotalNotes -
                                              lastNoteData.TotalNotes;
                        }
                        else
                        {
                            additionalNotesHit = currentResponse.MemoryReadout.NoteData.TotalNotesHit;
                            additionalNotesMissed = currentResponse.MemoryReadout.NoteData.TotalNotesMissed;
                            additionalNotes = currentResponse.MemoryReadout.NoteData.TotalNotes;
                        }

                        //Usually additional Notes should never be negative, but could be in case sniffer delivers bad data
                        // In this case we will log a warning, and ignore this data for the accumulation. It should fix itself next cycle
                        if ((additionalNotes < 0) || (additionalNotesHit < 0) || (additionalNotesMissed < 0))
                        {
                            CPH.LogWarn(Constants.AppName +
                                        $"additionalNotes is negative! additionalNotes={additionalNotes} additionalNotesHit={additionalNotesHit} additionalNotesMissed={additionalNotesMissed} totalNotesThisStream={totalNotesThisStream} totalNotesHitThisStream={totalNotesHitThisStream} totalNotesMissedThisStream={totalNotesMissedThisStream}");
                        }
                        else
                        {
                            totalNotesHitThisStream += (uint)additionalNotesHit;
                            totalNotesMissedThisStream += (uint)additionalNotesMissed;
                            totalNotesThisStream += (uint)additionalNotes;
                            CPH.SetGlobalVar("totalNotesSinceLaunch", totalNotesThisStream, false);
                            CPH.SetGlobalVar("totalNotesHitSinceLaunch", totalNotesHitThisStream, false);
                            CPH.SetGlobalVar("totalNotesMissedSinceLaunch", totalNotesMissedThisStream, false);
                            if (totalNotesThisStream > 0)
                            {
                                accuracyThisStream = 100.0 * ((double)totalNotesHitThisStream / totalNotesThisStream);
                            }

                            CPH.SetGlobalVar("accuracySinceLaunch", accuracyThisStream, false);
                        }

                        lastNoteData = currentResponse.MemoryReadout.NoteData;
                    }
                }
            }
            catch (ObjectDisposedException e)
            {
                CPH.LogWarn(Constants.AppName +
                            $"Caught object disposed exception when trying to save note data: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                CPH.LogWarn(Constants.AppName + $"Caught exception: {e.Message}");
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
                                CPH.LogVerbose(Constants.AppName + $"currentArrangement: {currentArrangement}");
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CPH.LogWarn(Constants.AppName + $"Caught exception trying to identify the arrangement: {e.Message}");
            }

            return currentArrangement != null;
        }

        public void IdentifySection()
        {
            if (currentArrangement != null)
            {
                try
                {
                    var name = currentArrangement.Sections[currentSectionIndex].Name;
                    if (name.ToLower().Contains("solo"))
                    {
                        currentSectionType = SectionType.Solo;
                    }
                    else if (name.ToLower().Contains("noguitar"))
                    {
                        currentSectionType = SectionType.NoGuitar;
                    }
                    else if (name.ToLower().Contains("riff"))
                    {
                        currentSectionType = SectionType.Riff;
                    }
                    else if (name.ToLower().Contains("bridge"))
                    {
                        currentSectionType = SectionType.Bridge;
                    }
                    else if (name.ToLower().Contains("breakdown"))
                    {
                        currentSectionType = SectionType.Breakdown;
                    }
                    else if (name.ToLower().Contains("chorus"))
                    {
                        currentSectionType = SectionType.Chorus;
                    }
                    else if (name.ToLower().Contains("verse"))
                    {
                        currentSectionType = SectionType.Verse;
                    }
                    else
                    {
                        currentSectionType = SectionType.Default;
                    }
                }
                catch (Exception e)
                {
                    CPH.LogWarn(Constants.AppName + "Caught unknown exception trying to identify the section: " +
                                e.Message);
                    throw;
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
            {
                //Checking for zero, as otherwise the start of the song can be mistakenly identified as pause
                //When ending the song, there are a few responses with the same time before game state switches. Not triggering a pause if it's less than 250ms to end of song.
                if (currentResponse.MemoryReadout.SongTimer.Equals(0)
                    || ((currentResponse.SongDetails.SongLength - currentResponse.MemoryReadout.SongTimer) < 0.25))
                {
                    if ((sameTimeCounter++) >= 3)
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

            switch (currentGameStage)
            {
                case GameStage.InSong:
                    CheckGameStageSong();
                    break;
                case GameStage.Menu:
                    CheckGameStageMenu();
                    break;
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
                RunAction(Constants.ActionNameSongStart);
            }

            if (!arrangementIdentified)
            {
                arrangementIdentified = IdentifyArrangement();
                SaveSongMetaData();
            }

            if (IsNotSongScene(currentScene))
            {
                var songTimer = currentResponse.MemoryReadout.SongTimer;
                CPH.LogVerbose(Constants.AppName + $"songTimer={songTimer} | lastSongTimer={lastSongTimer}");
                if (!songTimer.Equals(lastSongTimer))
                {
                    if (songTimer < lastSongTimer)
                    {
                        // When leaving pause, it is either a restart, in that case lastNoteData is from previous playthrough
                        // or the timer roll back when resuming could lead to unexpected deltas.
                        // In both cases we want to reset the lastNoteData to the current one to prevent underflows
                        lastNoteData = currentResponse.MemoryReadout.NoteData;
                    }

                    sameTimeCounter = 0;
                    if (itsSceneInterActor.IsNotInCooldown())
                    {
                        if (currentScene.Equals(songPausedScene))
                        {
                            RunAction(Constants.ActionNameLeavePause);
                        }

                        itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex], switchScenes);
                    }
                }
            }
            else if (IsSongScene(currentScene))
            {
                CPH.LogDebug("currentScene IsSongScene");
                CPH.LogVerbose($"songSceneAutoSwitchMode={songSceneAutoSwitchMode}");
                if (switchScenes && ItsTimeToSwitchScene() && (songScenes.Length > 1))
                {
                    switch (songSceneAutoSwitchMode)
                    {
                        case SongSceneAutoSwitchMode.Sequential:
                            DoSequentialSceneSwitch();
                            break;
                        case SongSceneAutoSwitchMode.Random:
                            DoRandomSceneSwitch();
                            break;
                        case SongSceneAutoSwitchMode.Off:
                        default:
                            // Nothing to do
                            break;
                    }
                }

                if (IsInPause())
                {
                    RunAction(Constants.ActionNameEnterPause);
                    itsSceneInterActor.SwitchToScene(songPausedScene, switchScenes);
                }
            }
        }

        private bool ItsTimeToSwitchScene()
        {
            return itsSceneInterActor.GetTimeSinceLastSceneChange() >= sceneSwitchPeriodInSeconds;
        }

        private void DoSequentialSceneSwitch()
        {
            if (++currentSongSceneIndex >= songScenes.Length)
            {
                currentSongSceneIndex = 0;
            }

            itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex], switchScenes);
        }

        private void DoRandomSceneSwitch()
        {
            int newSongSceneIndex;
            do
            {
                if (songScenes.Length == 1)
                {
                    newSongSceneIndex = 0;
                    break;
                }

                newSongSceneIndex = new Random().Next(0, songScenes.Length);
            } while (newSongSceneIndex == currentSongSceneIndex);

            currentSongSceneIndex = newSongSceneIndex;
            itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex], switchScenes);
        }

        private void RunAction(string actionName)
        {
            CPH.LogInfo(Constants.AppName + $"RunAction: {actionName}");
            CPH.RunAction(actionName);
        }

        private void CheckGameStageMenu()
        {
            if (switchScenes && !currentScene.Equals(menuScene))
            {
                itsSceneInterActor.SwitchToScene(menuScene, switchScenes);
            }

            if (lastGameStage == GameStage.InSong)
            {
                arrangementIdentified = false;
                lastNoteData = null!;
                RunAction(Constants.ActionNameSongEnd);
            }
        }

        private void CheckTunerActions()
        {
            if ((currentGameStage == GameStage.InTuner) && (lastGameStage != GameStage.InTuner))
            {
                RunAction(Constants.ActionNameEnterTuner);
            }

            if ((currentGameStage != GameStage.InTuner) && (lastGameStage == GameStage.InTuner))
            {
                RunAction(Constants.ActionNameLeaveTuner);
            }
        }

        public void CheckSectionActions()
        {
            if (currentArrangement != null && reactingToSections)
            {
                bool isSectionChanged = false;
                if (currentSectionIndex == -1)
                {
                    if (currentSongTimer >= currentArrangement.Sections[0].StartTime)
                    {
                        currentSectionIndex = 0;
                        isSectionChanged = true;
                    }
                }
                else
                {
                    // Check if entered a new section
                    if (currentSongTimer >= currentArrangement.Sections[currentSectionIndex].EndTime)
                    {
                        ++currentSectionIndex;
                        isSectionChanged = true;
                    }
                }

                if (isSectionChanged)
                {
                    IdentifySection();
                    if (currentSectionType != lastSectionType)
                    {
                        RunAction($"leave{Enum.GetName(typeof(SectionType), lastSectionType)}");
                        RunAction($"enter{Enum.GetName(typeof(SectionType), currentSectionType)}");
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

    // -------------------------------------------------
    // Needs to be commented out in streamer bot!
    private CPHmock CPH = new CPHmock();
    // -------------------------------------------------

    private SceneInteractor itsSceneInteractor = null!;
    private ResponseFetcher itsFetcher = null!;
    private ResponseParser itsParser = null!;

    private string snifferIp = null!;
    private string snifferPort = null!;

    private string currentScene = "";

    private void UpdateCurrentScene()
    {
        var newCurrentScene = itsSceneInteractor.GetCurrentScene();

        if (newCurrentScene.Equals(currentScene))
        {
            CPH.LogDebug(Constants.AppName + "Scene does not changed, nothing to update!");
            return;
        }

        CPH.LogInfo(Constants.AppName + $"Scene has been changed! Set current scene to '{newCurrentScene}'");
        currentScene = newCurrentScene;
        itsParser.SetCurrentScene(currentScene);
    }

    public void Init()
    {
        CPH.LogInfo($"{Constants.AppName}!!! Initialising RockSniffer to SB plugin !!!");
        // Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = GetSnifferIp();
        // TODO in case snifferIp is null, no need to do anything after this as, Sniffer could be not connected/used.
        snifferPort = GetSnifferPort();
        CPH.LogInfo($"{Constants.AppName}Sniffer ip configured as {snifferIp}:{snifferPort}");
        itsSceneInteractor = new SceneInteractor(CPH);
        itsFetcher = new ResponseFetcher(CPH, snifferIp, snifferPort);
        itsParser = new ResponseParser(CPH, itsSceneInteractor);
        itsParser.Init();

        currentScene = "";
    }

    private string GetSnifferIp()
    {
        var globalVar = CPH.GetGlobalVar<string>(Constants.GlobalVarNameSnifferIP);
        CPH.LogInfo($"{Constants.AppName}{Constants.GlobalVarNameSnifferIP}={globalVar}");
        // TODO in case not found, return null, or return default localhost?
        return string.IsNullOrEmpty(globalVar) ? null : globalVar.Replace('"', ' ').Trim();
    }

    private string GetSnifferPort()
    {
        var globalVar = CPH.GetGlobalVar<string>(Constants.GlobalVarNameSnifferPort);
        CPH.LogInfo($"{Constants.AppName}{Constants.GlobalVarNameSnifferPort}={globalVar}");
        return string.IsNullOrEmpty(globalVar) ? Constants.SnifferPortDefault : globalVar.Trim();
    }

    public bool Execute()
    {
        CPH.LogDebug(Constants.AppName + "------- START! -------");

        UpdateCurrentScene();

        if (itsParser.IsRelevantScene())
        {
            CPH.LogVerbose(Constants.AppName + "Scene is relevant, fetching data from sniffer...");
            string response = itsFetcher.Fetch();

            if (response != string.Empty)
            {
                CPH.LogVerbose(Constants.AppName + "Valid response received.");
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
                        CPH.LogWarn(Constants.AppName +
                                    $"Caught object disposed exception when trying to save note data: {e.Message}");
                        CPH.LogWarn(Constants.AppName + "Trying to reinitialize");
                        Init();
                    }
                    catch (Exception e)
                    {
                        throw new Exception(
                            Constants.AppName +
                            $"Caught unknown exception when trying to write song meta data: {e.Message}", e);
                    }

                    try
                    {
                        itsParser.PerformSceneSwitchIfNecessary();
                    }
                    catch (NullReferenceException e)
                    {
                        CPH.LogWarn(Constants.AppName + $"Caught null reference in scene switch: {e.Message}");
                        CPH.LogWarn(Constants.AppName + "Reinitialising to fix the issue");
                        Init();
                    }

                    itsParser.CheckSectionActions();
                }
            }
            else
            {
                CPH.LogWarn(Constants.AppName + "Fetching response failed, exiting action.");
                return false;
            }
        }
        else
        {
            CPH.LogVerbose(Constants.AppName + "Scene is not relevant, skipping.");
        }

        CPH.LogDebug(Constants.AppName + "------- END! -------");

        return true;
    }
}
