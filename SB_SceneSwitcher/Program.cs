using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
    public const string GlobalVarNameSwitchScenes = "switchScenes";
    public const string GlobalVarNameSceneSwitchPeriod = "sceneSwitchPeriod";
    public const string GlobalVarNameSceneSwitchCooldownPeriod = "sceneSwitchCooldownPeriod";
    public const string GlobalVarNameSongSceneAutoSwitchMode = "songSceneAutoSwitchMode";
    public const string GlobalVarNameSectionActions = "sectionActions";
    public const string GlobalVarNameBehavior = "behavior";
    public const string GlobalVarNameBlackList = "blackList";

    public const string ActionNameSongStart = "SongStart";
    public const string ActionNameLeavePause = "leavePause";
    public const string ActionNameEnterPause = "enterPause";
    public const string ActionNameSongEnd = "SongEnd";
    public const string ActionNameEnterTuner = "enterTuner";
    public const string ActionNameLeaveTuner = "leaveTuner";

    public const string SnifferPortDefault = "9938";

    public const int DefaultSceneSwitchPeriod = 5;
    public const int DefaultSceneSwitchCooldownPeriod = 3;

    public const string GlobalVarNameTotalNotesLifeTime = "totalNotesLifeTime";
    public const string GlobalVarNameTotalNotesHitLifeTime = "totalNotesHitLifeTime";
    public const string GlobalVarNameTotalNotesMissedLifeTime = "totalNotesMissedLifeTime";
    public const string GlobalVarNameAccuracyLifeTime = "accuracyLifeTime";

    public const string GlobalVarNameGuessingDictionary = "guessingDictionary";
    public const string GlobalVarNameGuessingIsActive = "guessingIsActive";
    public const string GlobalVarNameGuessingState = "guessingState";
    public const string GlobalVarNameGuessingMinGuesser = "guessMinGuesserCount";
    public const string GlobalVarNameGuessingGuessTime = "guessTime";

    public const string GlobalVarNameGuessingStartedText = "guessStartingText";
    public const string GlobalVarNameGuessingTimeoutText = "guessTimeoutText";
    public const string GlobarVarNameGuessingWinner = "guessWinner";
    public const string GlobarVarNameGuessingWinningGuess = "guessWinningGuess";
    public const string GlobalVarNameGuessingWinnersCount = "guessWinnersCount";
    public const string GlobalVarNameGuessingWinningDeviation = "guessWinningDeviation";
    public const string GlobalVarNameGuessingFinalAccuracy = "guessFinalAccuracy";

    public const string ActionNameGuessingFinished = "guessWinner";


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
            "No stream app connection available! Please set and connect either to Obs or Slobs under 'Stream Apps' in SB!";

        enum StreamApp
        {
            Obs,
            Slobs
        }

        private int cooldownPeriod;
        private DateTime lastSceneChange;
        private IInlineInvokeProxy CPH;
        private StreamApp? itsStreamApp;

        public SceneInteractor(IInlineInvokeProxy cph)
        {
            CPH = cph;
            cooldownPeriod = Constants.DefaultSceneSwitchCooldownPeriod;
            lastSceneChange = DateTime.Now;
        }

        public void SetCooldownPeriod(int cooldown)
        {
            cooldownPeriod = cooldown;
        }

        private void DetermineAndSetConnectedStreamApp()
        {
            if (CPH.ObsIsConnected())
            {
                itsStreamApp = StreamApp.Obs;
                CPH.LogDebug(Constants.AppName + $"Connected to {StreamApp.Obs}");
            }
            else if (CPH.SlobsIsConnected())
            {
                itsStreamApp = StreamApp.Slobs;
                CPH.LogDebug(Constants.AppName + $"Connected to {StreamApp.Slobs}");
            }
            else
            {
                itsStreamApp = null;
                CPH.LogDebug(MessageNoStreamAppConnectionAvailable);
            }
        }

        public string GetCurrentScene()
        {
            DetermineAndSetConnectedStreamApp();

            return itsStreamApp switch
            {
                StreamApp.Obs => CPH.ObsGetCurrentScene(),
                StreamApp.Slobs => CPH.SlobsGetCurrentScene(),
                _ => ""
            };
        }

        public void SwitchToScene(string scene, bool switchScenes)
        {
            if (switchScenes && IsNotInCooldown())
            {
                switch (itsStreamApp)
                {
                    case StreamApp.Obs:
                        CPH.LogInfo(Constants.AppName + $"Switching to Obs scene: {scene}");
                        CPH.ObsSetScene(scene);
                        break;
                    case StreamApp.Slobs:
                        CPH.LogInfo(Constants.AppName + $"Switching to Slobs scene: {scene}");
                        CPH.SlobsSetScene(scene);
                        break;
                    default:
                        CPH.LogWarn(MessageNoStreamAppConnectionAvailable);
                        break;
                }

                lastSceneChange = DateTime.Now;
            }
        }

        public bool IsNotInCooldown()
        {
            var timeSinceLastSceneChange = GetTimeSinceLastSceneChange();
            var notInCooldown = !(timeSinceLastSceneChange < cooldownPeriod);
            CPH.LogDebug($"{Constants.AppName}Is in cooldown={!notInCooldown}");
            CPH.LogVerbose($"{Constants.AppName}isNotInCooldown={notInCooldown} - " +
                           $"timeSinceLastSceneChange={timeSinceLastSceneChange} " +
                           $"cooldownPeriod={cooldownPeriod} ");
            return notInCooldown;
        }

        public double GetTimeSinceLastSceneChange()
        {
            var timeSinceLastSceneChange = DateTime.Now.Subtract(lastSceneChange).TotalSeconds;
            CPH.LogVerbose($"{Constants.AppName}timeSinceLastSceneChange={timeSinceLastSceneChange}");
            return timeSinceLastSceneChange;
        }
    }

    private class ResponseFetcher
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
            }
            catch (ObjectDisposedException e)
            {
                CPH.LogWarn(Constants.AppName + $"HttpClient was disposed. Exception: {e.Message} Reinitialising.");
            }
            catch (Exception e)
            {
                CPH.LogWarn(
                    Constants.AppName + $"Caught an Exception, when trying to read from HttpClient: {e.Message}");
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
        struct SongScene
        {
            public string Name;

            public enum Period
            {
                Fixed,
                Range
            }

            public Period period;
            public int minimumPeriod;
            public int maximumPeriod;
            public int currentSwitchPeriod;

            public void RandomizePeriodIfNecessary()
            {
                if (period == Period.Range)
                {
                    currentSwitchPeriod = new Random().Next(minimumPeriod, maximumPeriod + 1);
                }
            }

            public override string ToString()
            {
                return
                    $"{nameof(Name)}: {Name}, " +
                    $"{nameof(period)}: {period}, " +
                    $"{nameof(minimumPeriod)}: {minimumPeriod}, " +
                    $"{nameof(maximumPeriod)}: {maximumPeriod}, " +
                    $"{nameof(currentSwitchPeriod)}: {currentSwitchPeriod}";
            }
        }

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

        private int defaultSceneSwitchPeriodInSeconds = Constants.DefaultSceneSwitchPeriod;
        private int sceneSwitchCooldownPeriodInSeconds = Constants.DefaultSceneSwitchCooldownPeriod;

        private Response currentResponse = null!;
        private NoteData lastNoteData = null!;

        private UInt32 totalNotesThisStream;
        private UInt32 totalNotesHitThisStream;
        private UInt32 totalNotesMissedThisStream;
        private double accuracyThisStream;
        private UInt32 highestStreakSinceLaunch;
        private UInt64 totalNotesLifeTime;
        private UInt64 totalNotesHitLifeTime;
        private UInt64 totalNotesMissedLifeTime;
        private double accuracyLifeTime;


        private string menuScene = null!;
        private SongScene[]? songScenes = null!;
        private string songPausedScene = null!;

        private int sameTimeCounter;
        private string currentScene = "";

        private bool switchScenes = true;
        private SongSceneAutoSwitchMode songSceneAutoSwitchMode = SongSceneAutoSwitchMode.Off;
        private bool reactingToSections = true;
        private bool arrangementIdentified = false;
        private IInlineInvokeProxy CPH;
        private GuessingGame itsGuessingGame;

        public ResponseParser(IInlineInvokeProxy cph, SceneInteractor interactor, GuessingGame guessing)
        {
            CPH = cph;
            itsSceneInterActor = interactor;
            itsGuessingGame = guessing;
        }

        public double GetCurrentTimer()
        {
            return currentSongTimer;
        }

        public void SetResponse(Response response)
        {
            currentResponse = response;
        }

        public void SetCurrentScene(string scene)
        {
            currentScene = scene;
        }

        public int GetSceneSwitchCooldownPeriodInSeconds()
        {
            return sceneSwitchCooldownPeriodInSeconds;
        }

        public void Init()
        {
            UpdateConfig();

            totalNotesThisStream = 0;
            totalNotesHitThisStream = 0;
            totalNotesMissedThisStream = 0;
            accuracyThisStream = 0;
            totalNotesLifeTime = GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesLifeTime);
            totalNotesHitLifeTime = GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesHitLifeTime);
            totalNotesMissedLifeTime = GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesMissedLifeTime);
            accuracyLifeTime = GetGlobalVarAsDouble(Constants.GlobalVarNameAccuracyLifeTime);
            highestStreakSinceLaunch = 0;
            currentSectionIndex = -1;
            currentSongSceneIndex = 0;
            lastSectionType = currentSectionType = SectionType.Default;
            lastGameStage = currentGameStage = GameStage.Menu;
            sameTimeCounter = 0;
        }

        public void UpdateConfig()
        {
            menuScene = GetGlobalVarAsString(Constants.GlobalVarNameMenuScene);
            string[] songScenesRaw = GetGlobalVarAsStringArray(Constants.GlobalVarNameMenuSongScenes);
            songScenes = new SongScene[songScenesRaw.Length];
            for (var i = 0; i < songScenesRaw.Length; ++i)
            {
                if (songScenesRaw[i].Contains("#"))
                {
                    string[] songSceneRaw = songScenesRaw[i].Split('#');
                    songScenes[i].Name = songSceneRaw[0];
                    if (songSceneRaw[1].Contains("-"))
                    {
                        string[] minMax = songSceneRaw[1].Split('-');
                        songScenes[i].period = SongScene.Period.Range;
                        songScenes[i].minimumPeriod = int.Parse(minMax[0]);
                        songScenes[i].maximumPeriod = int.Parse(minMax[1]);
                        songScenes[i].RandomizePeriodIfNecessary();
                    }
                    else
                    {
                        songScenes[i].period = SongScene.Period.Fixed;
                        songScenes[i].currentSwitchPeriod = int.Parse(songSceneRaw[1]);
                    }
                }
                else
                {
                    songScenes[i].Name = songScenesRaw[i];
                    songScenes[i].period = SongScene.Period.Fixed;
                    songScenes[i].currentSwitchPeriod = defaultSceneSwitchPeriodInSeconds;
                }
            }

            songPausedScene = GetGlobalVarAsString(Constants.GlobalVarNamePauseScene);

            switchScenes = GetGlobalVarAsBool(Constants.GlobalVarNameSwitchScenes);
            songSceneAutoSwitchMode = GetGlobalVarSongSceneAutoSwitchMode();
            reactingToSections = GetGlobalVarAsBool(Constants.GlobalVarNameSectionActions);

            defaultSceneSwitchPeriodInSeconds = GetGlobalVarSceneSwitchPeriod();
            sceneSwitchCooldownPeriodInSeconds = GetGlobalVarSceneSwitchCooldownPeriod();
            itsBehavior = GetGlobalVarBehavior();
            blackListedScenes = GetGlobalVarBlackListedScenes();
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

        private int GetGlobalVarAsInt(string name, int def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : int.Parse(globalVar);
        }

        private UInt64 GetGlobalVarAsUInt64(string name, UInt64 def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : UInt64.Parse(globalVar);
        }

        private double GetGlobalVarAsDouble(string name, double def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : double.Parse(globalVar);
        }

        private int GetGlobalVarSceneSwitchPeriod()
        {
            var globalVar = GetGlobalVarAsInt(Constants.GlobalVarNameSceneSwitchPeriod,
                Constants.DefaultSceneSwitchPeriod);
            CPH.LogInfo($"{Constants.AppName}{Constants.GlobalVarNameSceneSwitchPeriod}={globalVar}");
            return globalVar;
        }

        private int GetGlobalVarSceneSwitchCooldownPeriod()
        {
            var globalVar = GetGlobalVarAsInt(Constants.GlobalVarNameSceneSwitchCooldownPeriod,
                Constants.DefaultSceneSwitchCooldownPeriod);
            CPH.LogInfo($"{Constants.AppName}{Constants.GlobalVarNameSceneSwitchCooldownPeriod}={globalVar}");
            return globalVar;
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

        private bool IsRelevantScene()
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
            foreach (SongScene s in songScenes)
                if (s.Name.Equals(scene))
                    return true;
            return false;
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

                        // Usually additional Notes should never be negative, but could be in case sniffer delivers bad data
                        // In this case we will log a warning, and ignore this data for the accumulation. It should fix itself next cycle
                        if ((additionalNotes < 0) || (additionalNotesHit < 0) || (additionalNotesMissed < 0))
                        {
                            CPH.LogWarn(Constants.AppName +
                                        "Leaving pause lead to inconsistency in note data. they will be ignored for accumulation");
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

                            totalNotesHitLifeTime += (UInt64)additionalNotesHit;
                            totalNotesMissedLifeTime += (uint)additionalNotesMissed;
                            totalNotesLifeTime += (uint)additionalNotes;
                            CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesLifeTime, totalNotesLifeTime, true);
                            CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesHitLifeTime, totalNotesHitLifeTime, true);
                            CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesMissedLifeTime, totalNotesMissedLifeTime, true);

                            if (totalNotesThisStream > 0)
                            {
                                accuracyThisStream = 100.0 * ((double)totalNotesHitThisStream / totalNotesThisStream);
                                accuracyLifeTime = 100.0 * ((double)totalNotesHitLifeTime / totalNotesLifeTime);
                            }

                            CPH.SetGlobalVar("accuracySinceLaunch", accuracyThisStream, false);
                            CPH.SetGlobalVar(Constants.GlobalVarNameAccuracyLifeTime, accuracyLifeTime, true);
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

        private bool IsInPause()
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
            if (!IsRelevantScene())
                return;
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

        private void CheckGameStageSong()
        {
            if (lastGameStage != GameStage.InSong)
            {
                RunAction(Constants.ActionNameSongStart);
                itsGuessingGame.StartAcceptingGuesses();
            }

            if (!arrangementIdentified)
            {
                arrangementIdentified = IdentifyArrangement();
                SaveSongMetaData();
            }

            var songTimer = currentResponse.MemoryReadout.SongTimer;
            CPH.LogVerbose(Constants.AppName + $"songTimer={songTimer} | lastSongTimer={lastSongTimer}");

            if (!IsSongScene(currentScene))
            {
                if (!songTimer.Equals(lastSongTimer))
                {
                    sameTimeCounter = 0;
                    if (itsSceneInterActor.IsNotInCooldown())
                    {
                        if (currentScene.Equals(songPausedScene))
                        {
                            RunAction(Constants.ActionNameLeavePause);
                        }

                        itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex].Name, switchScenes);
                    }
                }
            }
            else if (IsSongScene(currentScene))
            {
                CPH.LogDebug($"{Constants.AppName}currentScene IsSongScene");
                CPH.LogVerbose($"{Constants.AppName}songSceneAutoSwitchMode={songSceneAutoSwitchMode}");

                if (IsInPause())
                {
                    RunAction(Constants.ActionNameEnterPause);
                    itsSceneInterActor.SwitchToScene(songPausedScene, switchScenes);
                }
                else if (HasToSwitchScene())
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
            }
        }

        private bool HasToSwitchScene()
        {
            return switchScenes && HasMoreThan1Scenes() && ItsTimeToSwitchScene();
        }

        private bool ItsTimeToSwitchScene()
        {
            var timeSinceLastSceneChange = itsSceneInterActor.GetTimeSinceLastSceneChange();
            CPH.LogVerbose($"{Constants.AppName}songScene={songScenes[currentSongSceneIndex]}");
            CPH.LogVerbose($"{Constants.AppName}timeSinceLastSceneChange={timeSinceLastSceneChange}");
            return timeSinceLastSceneChange >= songScenes[currentSongSceneIndex].currentSwitchPeriod;
        }

        private void DoSequentialSceneSwitch()
        {
            if (++currentSongSceneIndex >= songScenes.Length)
            {
                currentSongSceneIndex = 0;
            }

            itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex].Name, switchScenes);
            songScenes[currentSongSceneIndex].RandomizePeriodIfNecessary();
        }

        private void DoRandomSceneSwitch()
        {
            int newSongSceneIndex;
            do
            {
                if (HasOnly1Scene())
                {
                    newSongSceneIndex = 0;
                    break;
                }

                newSongSceneIndex = new Random().Next(0, songScenes.Length);
            } while (newSongSceneIndex == currentSongSceneIndex);

            currentSongSceneIndex = newSongSceneIndex;
            itsSceneInterActor.SwitchToScene(songScenes[currentSongSceneIndex].Name, switchScenes);
            songScenes[currentSongSceneIndex].RandomizePeriodIfNecessary();
        }

        private bool HasOnly1Scene()
        {
            return songScenes.Length == 1;
        }

        private bool HasMoreThan1Scenes()
        {
            return songScenes.Length > 1;
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
                itsGuessingGame.FinishAndEvaluate((float)currentResponse.MemoryReadout.NoteData.Accuracy);
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
            if (!IsRelevantScene())
                return;
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

    public class GuessingGame
    {
        enum State
        {
            InActive,
            AcceptingGuesses,
            WaitingForTheSongToFinish
        }

        private Boolean isActive;
        private State itsState;

        private int minimumGuesses = 2;
        private int timeOut;
        private IInlineInvokeProxy CPH;

        //Unfortunately guesses will be entered via separate actions in streamer.bot. Therefore we cannot access any contents here directly and need to work with variables
        // JsonConvert shall be used to store/extract in in a variable.
        Dictionary<string, float> guesses;
        Dictionary<string, int> guessWinningCountDict;


        public GuessingGame(IInlineInvokeProxy cph)
        {
            CPH = cph;    
            ResetGuesses();
            SetState(State.InActive);

            string temp = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingWinnersCount, true);
            if (temp == null)
            {
                guessWinningCountDict = new Dictionary<string, int>();
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingWinnersCount, JsonConvert.SerializeObject(guessWinningCountDict), true);
            }
            else
            {
                guessWinningCountDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(temp);
            }
            
        }

        


        public void UpdateConfig()
        {
            ReadConfig();
        }

        private void SendToChats(string str)
        {
            CPH.SendMessage(str, true);
            CPH.SendYouTubeMessage(str, true);
        }
        
        private void ResetGuesses()
        {
            guesses = new Dictionary<string, float>();
            string DictAsString = JsonConvert.SerializeObject(guesses);
            CPH.SetGlobalVar(Constants.GlobalVarNameGuessingDictionary, DictAsString, false);
        }

        private void ReadConfig()
        {
            //TODO: refactor accessing globals to its own subclass and make it available here
            string temp = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingIsActive, false);
            isActive = temp.ToLower().Contains("true");

            temp = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingMinGuesser, false);
            minimumGuesses = string.IsNullOrEmpty(temp) ? 2 : int.Parse(temp);

            temp = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingGuessTime, false);
            timeOut = string.IsNullOrEmpty(temp) ? 30 : int.Parse(temp);
        }

        private void SetState(GuessingGame.State state)
        {
            itsState = state;
            CPH.SetGlobalVar(Constants.GlobalVarNameGuessingState, state.ToString(), false);
        }

        public void Init()
        {
            ResetGuesses();
            ReadConfig();

        }

        public void StartAcceptingGuesses()
        {
            //Plausability check
            if (itsState != State.AcceptingGuesses)
            {
                ResetGuesses();
                SetState(State.AcceptingGuesses);
                string message = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingStartedText, false);
                SendToChats(message);
            }
        }

        public void CheckTimeout(int currentTimer)
        {
            if (isActive && itsState == State.AcceptingGuesses)
            {
                if (currentTimer >= timeOut)
                {
                    StopAcceptingGuesses();
                }
            }
        }

        private void StopAcceptingGuesses()
        {
            if (itsState == State.AcceptingGuesses)
            {
                SetState(State.WaitingForTheSongToFinish);
                string message = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingTimeoutText, false);
                SendToChats(message);
                string temp = CPH.GetGlobalVar<string>(Constants.GlobalVarNameGuessingDictionary, false);
                if (temp != null)
                {
                    guesses = JsonConvert.DeserializeObject<Dictionary<string, float>>(temp);
                }
                else
                {
                    guesses = new Dictionary<string, float>();
                }
            }
        }

        public void FinishAndEvaluate(float accuracy)
        {
            
            if (itsState != State.WaitingForTheSongToFinish)
            {
                SetState(State.InActive);
                return;
            }
            
            SetState(State.InActive);
            if (guesses.Count < minimumGuesses)
            {
               SendToChats(string.Format("Unfortunately only {0} out of required {1} people have guessed",guesses.Count, minimumGuesses));
            }
            else
            {
                string winnerName = "";
                float minimumDeviation = 1000000.0f;
                foreach (KeyValuePair<string, float> guess in guesses)
                {
                    float deviation = Math.Abs(accuracy - guess.Value);
                    if (deviation < minimumDeviation)
                    {
                        winnerName = guess.Key;
                        minimumDeviation = deviation;
                    }
                }
                CPH.SetGlobalVar(Constants.GlobarVarNameGuessingWinner, winnerName, false);
                CPH.SetGlobalVar(Constants.GlobarVarNameGuessingWinningGuess, guesses[winnerName], false);
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingWinningDeviation, minimumDeviation, false);
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingFinalAccuracy, accuracy, false);

                CPH.RunAction(Constants.ActionNameGuessingFinished);

                if (guessWinningCountDict.TryGetValue(winnerName, out int currentCount))
                {
                    guessWinningCountDict[winnerName] = currentCount + 1;
                }
                else
                {
                    guessWinningCountDict[winnerName] = 1;
                }
                string temp = JsonConvert.SerializeObject(guessWinningCountDict);
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingWinnersCount, temp, true);
            }
        }
    }

    // -------------------------------------------------
    // Needs to be commented out in streamer bot!
    private CPHmock CPH = new CPHmock();
    // -------------------------------------------------

    private SceneInteractor itsSceneInteractor = null!;
    private ResponseFetcher itsFetcher = null!;
    private ResponseParser itsParser = null!;
    private GuessingGame itsGuessingGame = null!;

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
        UpdateConfig();
        CPH.LogInfo($"{Constants.AppName}Sniffer ip configured as {snifferIp}:{snifferPort}");
        itsSceneInteractor = new SceneInteractor(CPH);
        itsFetcher = new ResponseFetcher(CPH, snifferIp, snifferPort);
        itsGuessingGame = new GuessingGame(CPH);
        itsParser = new ResponseParser(CPH, itsSceneInteractor, itsGuessingGame);
        
        itsParser.Init();
        itsSceneInteractor.SetCooldownPeriod(itsParser.GetSceneSwitchCooldownPeriodInSeconds());

        currentScene = "";
    }

    private void UpdateConfig()
    {
        // Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = GetSnifferIp();
        // TODO in case snifferIp is null, no need to do anything after this as, Sniffer could be not connected/used.
        snifferPort = GetSnifferPort();
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
        UpdateConfig();
        itsParser.UpdateConfig();
        itsGuessingGame.UpdateConfig();

        string response = itsFetcher.Fetch();

        if (response != string.Empty)
        {
            CPH.LogVerbose(Constants.AppName + "Valid response received.");
            Response currentResponse = itsFetcher.ExtractResponse(response);

            if (currentResponse != null)
            {
                itsParser.SetResponse(currentResponse);
                itsParser.UpdateStageAndTimer();
                itsGuessingGame.CheckTimeout((int)itsParser.GetCurrentTimer());


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

            return true;
        }

        CPH.LogDebug(Constants.AppName + "------- END! -------");

        return true;
    }
}
