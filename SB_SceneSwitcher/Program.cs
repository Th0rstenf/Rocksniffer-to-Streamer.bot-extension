using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public struct Constants
{
    public const string AppName = "RS2SB :: ";

    public const string ArgumentNameSnifferIP = "snifferIP";
    public const string ArgumentNameSnifferPort = "snifferPort";
    public const string ArgumentNameMenuScene = "menuScene";
    public const string ArgumentNameSongScenes = "songScenes";
    public const string ArgumentNamePauseScene = "pauseScene";
    public const string ArgumentNameSwitchScenes = "switchScenes";
    public const string ArgumentNameSceneSwitchPeriod = "sceneSwitchPeriod";
    public const string ArgumentNameSceneSwitchCooldownPeriod = "sceneSwitchCooldownPeriod";
    public const string ArgumentNameSongSceneAutoSwitchMode = "songSceneAutoSwitchMode";
    public const string ArgumentNameSectionActions = "sectionActions";
    public const string ArgumentNameBehavior = "behavior";
    public const string ArgumentNameBlackList = "blackList";

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
    public const string GlobalVarNameGuessingWinnersCount = "guessWinnersCount";

    public const string ArgumentNameGuessingStartedText = "guessStartingText";
    public const string ArgumentNameGuessingTimeoutText = "guessTimeoutText";
    public const string ArgumentNameGuessingWinner = "guessWinner";
    public const string ArgumentNameGuessingWinningGuess = "guessWinningGuess";
    
    public const string ArgumentNameGuessingWinningDeviation = "guessWinningDeviation";
    public const string ArgumentNameGuessingFinalAccuracy = "guessFinalAccuracy";

    public const string TriggerNameGuessWinnerDetermined = "guessWinner";
    


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
        private StreamApp? itsLastStreamApp;

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
            }
            else if (CPH.SlobsIsConnected())
            {
                itsStreamApp = StreamApp.Slobs;
            }
            else
            {
                itsStreamApp = null;
                CPH.LogDebug(MessageNoStreamAppConnectionAvailable);
            }
            if (itsStreamApp != itsLastStreamApp)
            {
                CPH.LogInfo(Constants.AppName + $"Connected to {itsStreamApp}");
                itsLastStreamApp = itsStreamApp;
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
        private string ip;
        private string port;
        private HttpResponseMessage response = null!;
        private HttpClient client = null!;

        public ResponseFetcher(IInlineInvokeProxy cph, string ip, string port)
        {
            CPH = cph;
            this.ip = ip;
            this.port = port;

            client = new HttpClient();
        }

        public void setIp(string ip)
        {
            if (ip != this.ip)
            {
                CPH.LogDebug(Constants.AppName + $"Setting ip to {ip}");
            }
            this.ip = ip;
        }

        public void setPort(string port)
        {
            if (port != this.port)
            {
                CPH.LogDebug(Constants.AppName + $"Setting port to {port}");
            }
            this.port = port;
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


        private SceneInteractor itsSceneInterActor;
        private IInlineInvokeProxy CPH;
        private GuessingGame itsGuessingGame;


        private double currentSongTimer;
        private double lastSongTimer;

        private Arrangement? currentArrangement = null!;
        private int currentSectionIndex;
        private int currentSongSceneIndex;

        private int defaultSceneSwitchPeriodInSeconds = Constants.DefaultSceneSwitchPeriod;
        private int sceneSwitchCooldownPeriodInSeconds = Constants.DefaultSceneSwitchCooldownPeriod;

        private Response currentResponse = null!;
        private NoteData lastNoteData = null!;
        private DataHandler itsDataHandler = null!;

        private UInt32 totalNotesThisStream;
        private UInt32 totalNotesHitThisStream;
        private UInt32 totalNotesMissedThisStream;
        private double accuracyThisStream;
        private UInt32 highestStreakSinceLaunch;
        private UInt64 totalNotesLifeTime;
        private UInt64 totalNotesHitLifeTime;
        private UInt64 totalNotesMissedLifeTime;
        private double accuracyLifeTime;

        private System.DateTime lastPersistingVariables = System.DateTime.Now;

        // Configuration attributes filled by user config!
        struct UserConfig
        {
            public ActivityBehavior itsBehavior;
            public string menuScene;
            public SongScene[] songScenes;
            public string songPausedScene;
            public string[] blackListedScenes;
            public bool switchScenes;
            public bool reactingToSections;
            public int defaultSceneSwitchPeriodInSeconds;
            public int sceneSwitchCooldownPeriodInSeconds;
            public SongSceneAutoSwitchMode songSceneAutoSwitchMode;
        }
        private UserConfig currentConfig;
        private UserConfig lastConfig;

        private int sameTimeCounter;
        private string currentScene = "";


        private bool arrangementIdentified = false;


        public ResponseParser(IInlineInvokeProxy cph, SceneInteractor interactor, GuessingGame guessing, DataHandler dataHandler)
        {
            CPH = cph;
            itsSceneInterActor = interactor;
            itsGuessingGame = guessing;
            itsDataHandler = dataHandler;
        }

        public double GetCurrentTimer()
        {
            return currentSongTimer;
        }

        private void LogResponseChange(Response oldResponse, Response newResponse)
        {
            CPH.LogDebug(Constants.AppName + $"Response received from Rocksniffer");

            if (oldResponse == null || newResponse == null)
            {
                CPH.LogDebug("One of the responses is null.");
                return;
            }

            var songDetailproperties = typeof(SongDetails).GetProperties();
            foreach (var property in songDetailproperties)
            {
                var oldValue = (oldResponse != null && oldResponse.SongDetails != null) ? property.GetValue(oldResponse.SongDetails) : "";
                var newValue = (newResponse.SongDetails != null) ? property.GetValue(newResponse.SongDetails) : "";

                if ((oldValue == null && newValue != null) || (oldValue != null && !oldValue.Equals(newValue)))
                {
                    if (property.Name != "Arrangements")
                    {
                        CPH.LogDebug(Constants.AppName + $"Response: {property.Name} changed from {oldValue ?? "null"} to {newValue ?? "null"}");
                    }
                }
            }

            var readoutProperties = typeof(MemoryReadout).GetProperties();
            foreach (var property in readoutProperties)
            {
                var oldValue = (oldResponse.MemoryReadout != null) ? property.GetValue(oldResponse.MemoryReadout) : "";
                var newValue = (newResponse.MemoryReadout != null) ? property.GetValue(newResponse.MemoryReadout) : "";
                if ((oldValue == null && newValue != null) || (oldValue != null && !oldValue.Equals(newValue)))
                {
                    if (property.Name != "NoteData")
                    {
                        CPH.LogDebug(Constants.AppName + $"Response: {property.Name} changed from {oldValue ?? "null"} to {newValue ?? "null"}");
                    }
                    else
                    {
                        var noteDataProperties = typeof(NoteData).GetProperties();
                        foreach (var noteDataProperty in noteDataProperties)
                        {
                            var oldNoteDataValue = (oldResponse.MemoryReadout.NoteData != null) ? noteDataProperty.GetValue(oldResponse.MemoryReadout.NoteData) : "";
                            var newNoteDataValue = (newResponse.MemoryReadout.NoteData != null) ? noteDataProperty.GetValue(newResponse.MemoryReadout.NoteData) : "";
                            if ((oldNoteDataValue == null && newNoteDataValue != null) || (oldNoteDataValue != null && !oldNoteDataValue.Equals(newNoteDataValue)))
                            {
                                CPH.LogDebug(Constants.AppName + $"Response: {noteDataProperty.Name} changed from {oldNoteDataValue ?? "null"} to {newNoteDataValue ?? "null"}");
                            }
                        }
                    }
                }
            }

        }


        public void SetResponse(Response response)
        {
            try
            {
                LogResponseChange(currentResponse, response);
            }
            catch (Exception e)
            {
                CPH.LogWarn(Constants.AppName + "Error in SetResponse: " + e.Message);
            }
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

        private void RegisterCustomTrigger(string description, string name)
        {
            String[] categories = new[] { "Rocksmith to Streamer.bot" };
            bool success = CPH.RegisterCustomTrigger(description, name, categories);
            CPH.LogDebug(Constants.AppName + $"RegisterCustomTrigger operation of {name} was {(success ? "successful" : "unsuccessful")}");
        }

        public void Init()
        {
            //UpdateConfig();

            totalNotesThisStream = 0;
            totalNotesHitThisStream = 0;
            totalNotesMissedThisStream = 0;
            accuracyThisStream = 0;
            totalNotesLifeTime = itsDataHandler.GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesLifeTime);
            totalNotesHitLifeTime = itsDataHandler.GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesHitLifeTime);
            totalNotesMissedLifeTime = itsDataHandler.GetGlobalVarAsUInt64(Constants.GlobalVarNameTotalNotesMissedLifeTime);
            accuracyLifeTime = itsDataHandler.GetGlobalVarAsDouble(Constants.GlobalVarNameAccuracyLifeTime);
            highestStreakSinceLaunch = 0;
            currentSectionIndex = -1;
            currentSongSceneIndex = 0;
            lastSectionType = currentSectionType = SectionType.Default;
            lastGameStage = currentGameStage = GameStage.Menu;
            sameTimeCounter = 0;

            arrangementIdentified = false;


            // Register "enter" and "leave" actions for each SectionType
            foreach (SectionType sectionType in Enum.GetValues(typeof(SectionType)))
            {
                string enterAction = $"enter{sectionType}";
                string leaveAction = $"leave{sectionType}";

                // Register actions
                // Assuming there are methods to register actions
                RegisterCustomTrigger($"Entering section {sectionType}", enterAction);
                RegisterCustomTrigger($"Leaving section {sectionType}", leaveAction);
            }
            RegisterCustomTrigger("Entering Song", Constants.ActionNameSongStart);
            RegisterCustomTrigger("Leaving Song", Constants.ActionNameSongEnd);
            RegisterCustomTrigger("Entering Tuner", Constants.ActionNameEnterTuner);
            RegisterCustomTrigger("Leaving Tuner", Constants.ActionNameLeaveTuner);
            RegisterCustomTrigger("Entering Pause", Constants.ActionNameEnterPause);
            RegisterCustomTrigger("Leaving Pause", Constants.ActionNameLeavePause);

        }

        public void UpdateConfig()
        {
            currentConfig.menuScene = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameMenuScene);

            string[] songScenesRaw = itsDataHandler.ReadArgumentAsStringArray(Constants.ArgumentNameSongScenes);
            currentConfig.songScenes = new SongScene[songScenesRaw.Length];
            for (var i = 0; i < songScenesRaw.Length; ++i)
            {
                if (songScenesRaw[i].Contains("#"))
                {
                    string[] songSceneRaw = songScenesRaw[i].Split('#');
                    currentConfig.songScenes[i].Name = songSceneRaw[0];
                    if (songSceneRaw[1].Contains("-"))
                    {
                        string[] minMax = songSceneRaw[1].Split('-');
                        currentConfig.songScenes[i].period = SongScene.Period.Range;
                        currentConfig.songScenes[i].minimumPeriod = int.Parse(minMax[0]);
                        currentConfig.songScenes[i].maximumPeriod = int.Parse(minMax[1]);
                        currentConfig.songScenes[i].RandomizePeriodIfNecessary();
                    }
                    else
                    {
                        currentConfig.songScenes[i].period = SongScene.Period.Fixed;
                        currentConfig.songScenes[i].currentSwitchPeriod = int.Parse(songSceneRaw[1]);
                    }
                }
                else
                {
                    currentConfig.songScenes[i].Name = songScenesRaw[i];
                    currentConfig.songScenes[i].period = SongScene.Period.Fixed;
                    currentConfig.songScenes[i].currentSwitchPeriod = defaultSceneSwitchPeriodInSeconds;
                }
            }

            currentConfig.songPausedScene = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNamePauseScene);
            currentConfig.switchScenes = itsDataHandler.ReadArgumentAsBool(Constants.ArgumentNameSwitchScenes);

            currentConfig.songSceneAutoSwitchMode = GetSongSceneAutoSwitchMode();

            currentConfig.reactingToSections = itsDataHandler.ReadArgumentAsBool(Constants.ArgumentNameSectionActions);

            currentConfig.defaultSceneSwitchPeriodInSeconds = GetSceneSwitchPeriod();
            currentConfig.sceneSwitchCooldownPeriodInSeconds = GetSceneSwitchCooldownPeriod();
            currentConfig.itsBehavior = GetBehavior();

            currentConfig.blackListedScenes = GetBlackListedScenes();

            LogConfigChanges();

        }

        private void LogConfigChanges()
        {
            var properties = typeof(UserConfig).GetProperties();
            foreach (var property in properties)
            {
                var currentValue = property.GetValue(currentConfig);
                var lastValue = property.GetValue(lastConfig);

                if (!Equals(currentValue, lastValue))
                {
                    Console.WriteLine($"Property {property.Name} changed from {lastValue} to {currentValue}");
                    property.SetValue(lastConfig, currentValue);
                }
            }
        }



        private ActivityBehavior GetBehavior()
        {
            string argumentValue = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameBehavior);
            if (string.IsNullOrEmpty(argumentValue))
                return ActivityBehavior.WhiteList;

            return argumentValue.ToLower().Trim() switch
            {
                "whitelist" => ActivityBehavior.WhiteList,
                "blacklist" => ActivityBehavior.BlackList,
                "alwayson" => ActivityBehavior.AlwaysOn,
                _ => ActivityBehavior.WhiteList
            };
        }

        private SongSceneAutoSwitchMode GetSongSceneAutoSwitchMode()
        {
            var autoSwitchMode =  itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameSongSceneAutoSwitchMode);
            if (string.IsNullOrEmpty(autoSwitchMode))
                return SongSceneAutoSwitchMode.Off;

            return autoSwitchMode.ToLower().Trim() switch
            {
                "off" => SongSceneAutoSwitchMode.Off,
                "sequential" => SongSceneAutoSwitchMode.Sequential,
                "random" => SongSceneAutoSwitchMode.Random,
                _ => SongSceneAutoSwitchMode.Off
            };

        }

       

        private int GetSceneSwitchPeriod()
        {
            var raw = itsDataHandler.ReadArgument(Constants.ArgumentNameSceneSwitchPeriod);
            if (raw == null)
            {
                CPH.LogDebug(Constants.AppName + $"Scene switch period is not set. Using default value.");
                return Constants.DefaultSceneSwitchPeriod;
            }
            try
            {
                int.TryParse(raw.ToString(), out var period);
                return period;
            }
            catch (Exception e)
            {
                CPH.LogDebug(Constants.AppName + $"Tried parsing {Constants.ArgumentNameSceneSwitchPeriod}, lead to error : {e.Message}");
                return Constants.DefaultSceneSwitchPeriod;
            }

        }

        private int GetSceneSwitchCooldownPeriod()
        {
            var raw = itsDataHandler.ReadArgument(Constants.ArgumentNameSceneSwitchCooldownPeriod);
            if (raw == null)
            {
                CPH.LogDebug(Constants.AppName + $"Scene switch cooldown period is not set. Using default value.");
                return Constants.DefaultSceneSwitchCooldownPeriod;
            }
            try
            {
                int.TryParse(raw.ToString(), out var period);
                return period;
            }
            catch (Exception e)
            {
                CPH.LogDebug(Constants.AppName + $"Tried parsing {Constants.ArgumentNameSceneSwitchCooldownPeriod}, lead to error : {e.Message}");
                return Constants.DefaultSceneSwitchCooldownPeriod;
            }

        }

        private string[] GetBlackListedScenes()
        {
            return (currentConfig.itsBehavior == ActivityBehavior.BlackList
                ? itsDataHandler.ReadArgumentAsStringArray(Constants.ArgumentNameBlackList)
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

            CPH.LogVerbose(Constants.AppName + $"IsRelevantScene - itsBehavior={currentConfig.itsBehavior}");
            switch (currentConfig.itsBehavior)
            {
                case ActivityBehavior.WhiteList:
                    {
                        if (currentScene.Equals(currentConfig.menuScene)
                            || IsSongScene(currentScene)
                            || currentScene.Equals(currentConfig.songPausedScene))
                        {
                            isRelevant = true;
                        }
                        break;
                    }
                case ActivityBehavior.BlackList:
                    {
                        isRelevant = true;
                        foreach (string str in currentConfig.blackListedScenes)
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

            CPH.LogDebug(Constants.AppName + $"IsRelevantScene - itsBehavior={currentConfig.itsBehavior} isRelevant={isRelevant}");
            return isRelevant;
        }

        private bool IsSongScene(string scene)
        {
            foreach (SongScene s in currentConfig.songScenes)
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
                           

                            if (totalNotesThisStream > 0)
                            {
                                accuracyThisStream = 100.0 * ((double)totalNotesHitThisStream / totalNotesThisStream);
                                accuracyLifeTime = 100.0 * ((double)totalNotesHitLifeTime / totalNotesLifeTime);
                            }
                            CPH.SetGlobalVar("accuracySinceLaunch", accuracyThisStream, false);

                            if ((System.DateTime.Now - lastPersistingVariables) > TimeSpan.FromSeconds(30))
                            {
                                CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesLifeTime, totalNotesLifeTime, true);
                                CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesHitLifeTime, totalNotesHitLifeTime, true);
                                CPH.SetGlobalVar(Constants.GlobalVarNameTotalNotesMissedLifeTime, totalNotesMissedLifeTime, true);
                                CPH.SetGlobalVar(Constants.GlobalVarNameAccuracyLifeTime, accuracyLifeTime, true);
                                lastPersistingVariables = System.DateTime.Now;
                            }
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
                TriggerAction(Constants.ActionNameSongStart);
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
                        if (currentScene.Equals(currentConfig.songPausedScene))
                        {
                            TriggerAction(Constants.ActionNameLeavePause);
                        }

                        itsSceneInterActor.SwitchToScene(currentConfig.songScenes[currentSongSceneIndex].Name, currentConfig.switchScenes);
                    }
                }
            }
            else if (IsSongScene(currentScene))
            {
                CPH.LogDebug($"{Constants.AppName}currentScene IsSongScene");
                CPH.LogVerbose($"{Constants.AppName}songSceneAutoSwitchMode={currentConfig.songSceneAutoSwitchMode}");

                if (IsInPause())
                {
                    TriggerAction(Constants.ActionNameEnterPause);
                    itsSceneInterActor.SwitchToScene(currentConfig.songPausedScene, currentConfig.switchScenes);
                }
                else if (HasToSwitchScene())
                {
                    switch (currentConfig.songSceneAutoSwitchMode)
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
            return currentConfig.switchScenes && HasMoreThan1Scenes() && ItsTimeToSwitchScene();
        }

        private bool ItsTimeToSwitchScene()
        {
            var timeSinceLastSceneChange = itsSceneInterActor.GetTimeSinceLastSceneChange();
            CPH.LogVerbose($"{Constants.AppName}songScene={currentConfig.songScenes[currentSongSceneIndex]}");
            CPH.LogVerbose($"{Constants.AppName}timeSinceLastSceneChange={timeSinceLastSceneChange}");
            return timeSinceLastSceneChange >= currentConfig.songScenes[currentSongSceneIndex].currentSwitchPeriod;
        }

        private void DoSequentialSceneSwitch()
        {
            if (++currentSongSceneIndex >= currentConfig.songScenes.Length)
            {
                currentSongSceneIndex = 0;
            }

            itsSceneInterActor.SwitchToScene(currentConfig.songScenes[currentSongSceneIndex].Name, currentConfig.switchScenes);
            currentConfig.songScenes[currentSongSceneIndex].RandomizePeriodIfNecessary();
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

                newSongSceneIndex = new Random().Next(0, currentConfig.songScenes.Length);
            } while (newSongSceneIndex == currentSongSceneIndex);

            currentSongSceneIndex = newSongSceneIndex;
            itsSceneInterActor.SwitchToScene(currentConfig.songScenes[currentSongSceneIndex].Name, currentConfig.switchScenes);
            currentConfig.songScenes[currentSongSceneIndex].RandomizePeriodIfNecessary();
        }

        private bool HasOnly1Scene()
        {
            return currentConfig.songScenes.Length == 1;
        }

        private bool HasMoreThan1Scenes()
        {
            return currentConfig.songScenes.Length > 1;
        }

        private Dictionary<string, object> CollectParameters()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            return parameters;
        }

        private void TriggerAction(string actionName, bool provideData = false)
        {
            if (provideData)
            {
                CPH.TriggerCodeEvent(actionName, CollectParameters());
            }
            CPH.TriggerCodeEvent(actionName, provideData);
        }

        private void CheckGameStageMenu()
        {
            if (currentConfig.switchScenes && !currentScene.Equals(currentConfig.menuScene))
            {
                itsSceneInterActor.SwitchToScene(currentConfig.menuScene, currentConfig.switchScenes);
            }

            if (lastGameStage == GameStage.InSong)
            {
                arrangementIdentified = false;
                lastNoteData = null!;
                TriggerAction(Constants.ActionNameSongEnd);
                // It is necessary to pass lastSongTimer for evaluation, as currentSongTimer is already reset to 0 at this stage
                itsGuessingGame.FinishAndEvaluate(currentResponse.SongDetails.SongLength, currentResponse.MemoryReadout.NoteData);

            }
        }

        private void CheckTunerActions()
        {
            if ((currentGameStage == GameStage.InTuner) && (lastGameStage != GameStage.InTuner))
            {
                TriggerAction(Constants.ActionNameEnterTuner);
            }

            if ((currentGameStage != GameStage.InTuner) && (lastGameStage == GameStage.InTuner))
            {
                TriggerAction(Constants.ActionNameLeaveTuner);
            }
        }

        public void CheckSectionActions()
        {
            if (!IsRelevantScene())
                return;
            if (currentArrangement != null && currentConfig.reactingToSections)
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
                        TriggerAction($"leave{Enum.GetName(typeof(SectionType), lastSectionType)}");
                        TriggerAction($"enter{Enum.GetName(typeof(SectionType), currentSectionType)}");
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
        
        private State itsState;

        struct UserConfig 
        {
            public Boolean isActive;
            public int minimumGuesses;
            public int timeOut;
            public UserConfig()
            {
                isActive = false;
                minimumGuesses = 2;
                timeOut = 60;
            }
        }

        private UserConfig currentConfig;
        private UserConfig lastConfig;

        private IInlineInvokeProxy CPH;
        private DataHandler itsDataHandler;

        //Unfortunately guesses will be entered via separate actions in streamer.bot. Therefore we cannot access any contents here directly and need to work with variables
        // JsonConvert shall be used to store/extract in in a variable.
        Dictionary<string, float> guesses;
        Dictionary<string, int> guessWinningCountDict;
        


        public GuessingGame(IInlineInvokeProxy cph, DataHandler dataHandler)
        {
            CPH = cph;
            itsDataHandler = dataHandler;
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
            currentConfig = new UserConfig();
            lastConfig = new UserConfig();
            String[] categories = new[] { "Rocksmith to Streamer.bot" };
            bool success = CPH.RegisterCustomTrigger("Guessing game finished",Constants.TriggerNameGuessWinnerDetermined , categories);

        }
        
        public bool GetTopGuessers()
        {
            var sorted = guessWinningCountDict.OrderByDescending(x => x.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
            var topTen = sorted.Take(10);         
            int rank = 1;
            // Assuming the container is a dictionary where the key is the rank and the value is a list of elements on that rank
            Dictionary<int, Tuple<int, List<string>>> container = new Dictionary<int, Tuple<int, List<string>>>();
            //Assemble top list
            for (int i = 0; i < topTen.Count(); ++i)
            {
                var entry = topTen.ElementAt(i);
                if (i > 0)
                {
                    var previousEntry = topTen.ElementAt(i - 1);
                    if (entry.Value != previousEntry.Value)
                    {
                        ++rank;
                    }
                }
                if (!container.ContainsKey(rank))
                {
                    container[rank] = new Tuple<int, List<string>>(entry.Value, new List<string>());
                }
                container[rank].Item2.Add(entry.Key);                
            }
            for (int i = 1; i <= container.Count; ++i)
            {
                string message = $"Rank {i} ({container[i].Item1} win): ";
                for (var iter = container[i].Item2.GetEnumerator();;)
                {
                    message += $"{iter.Current}";
                    if (iter.MoveNext())
                    {
                        message += ", ";
                    }
                    else
                    {
                        break;
                    }
                }
                SendToChats(message);
                CPH.Wait(350);
            }           

            return true;
        }
        
        public void UpdateConfig()
        {

            string temp = itsDataHandler.ReadArgumentAsString(Constants.GlobalVarNameGuessingIsActive);
            currentConfig.isActive = temp.ToLower().Contains("true");

            temp = itsDataHandler.ReadArgumentAsString(Constants.GlobalVarNameGuessingMinGuesser);
            currentConfig.minimumGuesses = string.IsNullOrEmpty(temp) ? 2 : int.Parse(temp);

            temp = itsDataHandler.ReadArgumentAsString(Constants.GlobalVarNameGuessingGuessTime);
            currentConfig.timeOut = string.IsNullOrEmpty(temp) ? 30 : int.Parse(temp);

            LogConfigChanges();
        }

        private void LogConfigChanges()
        {
            var properties = typeof(UserConfig).GetProperties();
            foreach (var property in properties)
            {
                var currentValue = property.GetValue(currentConfig);
                var lastValue = property.GetValue(lastConfig);

                if (!Equals(currentValue, lastValue))
                {
                    Console.WriteLine($"Property {property.Name} changed from {lastValue} to {currentValue}");
                    property.SetValue(lastConfig, currentValue);
                }
            }
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


        private void SetState(GuessingGame.State state)
        {
            itsState = state;
            CPH.SetGlobalVar(Constants.GlobalVarNameGuessingState, state.ToString(), false);
        }

        public void Init()
        {
            ResetGuesses();
            UpdateConfig();

        }

        public void StartAcceptingGuesses()
        {
            //Plausability check
            if (currentConfig.isActive && itsState != State.AcceptingGuesses)
            {
                ResetGuesses();
                SetState(State.AcceptingGuesses);
                string message = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameGuessingStartedText);
                SendToChats(message);
            }
        }

        public void CheckTimeout(int currentTimer)
        {
            if (currentConfig.isActive && itsState == State.AcceptingGuesses)
            {
                if (currentTimer >= currentConfig.timeOut)
                {
                    StopAcceptingGuesses();
                }
            }
        }

        private void StopAcceptingGuesses()
        {
            if (currentConfig.isActive && itsState == State.AcceptingGuesses)
            {
                SetState(State.WaitingForTheSongToFinish);
                string message = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameGuessingTimeoutText);
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

        public void FinishAndEvaluate(double totalLength, NoteData currentNoteData)
        {
            SetState(State.InActive);
            if (!currentConfig.isActive)
                return;
            if (totalLength < currentConfig.timeOut)
            {
                SendToChats("This song was too short to count for the guessing game");
            }
            else if (currentNoteData.TotalNotes > (currentNoteData.TotalNotesHit + currentNoteData.TotalNotesMissed))
            {
                SendToChats("It seems the song was not played to the end, guessing game is not counting this one.");
            }
            else if (guesses.Count < currentConfig.minimumGuesses)
            {
                SendToChats(string.Format("Unfortunately only {0} out of required {1} people have guessed", guesses.Count, currentConfig.minimumGuesses));
            }
            else
            {
                string winnerName = "";
                float minimumDeviation = 1000000.0f;
                foreach (KeyValuePair<string, float> guess in guesses)
                {
                    float deviation = Math.Abs((float)currentNoteData.Accuracy - guess.Value);
                    if (deviation < minimumDeviation)
                    {
                        winnerName = guess.Key;
                        minimumDeviation = deviation;
                    }
                }
                /*
                CPH.SetGlobalVar(Constants.GlobarVarNameGuessingWinner, winnerName, false);
                CPH.SetGlobalVar(Constants.GlobarVarNameGuessingWinningGuess, guesses[winnerName], false);
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingWinningDeviation, minimumDeviation, false);
                CPH.SetGlobalVar(Constants.GlobalVarNameGuessingFinalAccuracy, currentNoteData.Accuracy, false);

                CPH.RunAction(Constants.ActionNameGuessingFinished);
                */
                Dictionary<string, object> evenArgs =  new Dictionary<string, object>();
                evenArgs.Add(Constants.ArgumentNameGuessingWinner, winnerName);
                evenArgs.Add(Constants.ArgumentNameGuessingWinningGuess, guesses[winnerName]);
                evenArgs.Add(Constants.ArgumentNameGuessingWinningDeviation, minimumDeviation);
                evenArgs.Add(Constants.ArgumentNameGuessingFinalAccuracy, currentNoteData.Accuracy);
                CPH.TriggerCodeEvent(Constants.TriggerNameGuessWinnerDetermined, evenArgs);


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


    public class DataHandler
    {

        private IInlineInvokeProxy CPH;
        Dictionary<string, object> arguments;

        public DataHandler(IInlineInvokeProxy cph, Dictionary<string, object> args) { CPH = cph; arguments = args; }

        public void setArguments(Dictionary<string, object> args)
        {
            arguments = args;
        }

        public object ReadArgument(string name)
        {
            CPH.LogVerbose(Constants.AppName + $"Reading argument {name}");        
            try
            {
                if (arguments.TryGetValue(name, out var arg))
                {
                    return arg; 
                }
                else
                {
                    CPH.LogDebug(Constants.AppName + $"Parsing argument {name} failed");
                }
            }
            catch (Exception e)
            {
                CPH.LogDebug(Constants.AppName + $"Error reading argument {name}: {e.Message}");
            }

            return "";
        }

        public string ReadArgumentAsString(string name)
        {
            return ReadArgument(name)?.ToString() ?? "";
        }

        public int ReadArgumentAsInt(string name)
        {
            try
            {
                return int.TryParse(ReadArgumentAsString(name), out var result) ? result : 0;
            }
            catch(Exception e)
            {
                CPH.LogDebug(Constants.AppName + $"Error parsing argument {name}: {e.Message}");
                return 0;
            }
            
        }

        public float ReadArgumentAsFloat(string name)
        {
            return float.TryParse(ReadArgumentAsString(name), out var result) ? result : 0;
        }

        public bool ReadArgumentAsBool(string name)
        {
            return bool.TryParse(ReadArgumentAsString(name), out var result) && result;
        }

        public string[]? ReadArgumentAsStringArray(string name)
        {
            CPH.TryGetArg<string>(name, out string value);
            if (string.IsNullOrEmpty(value)) return null;
            var trimmedValues = Regex.Split(value.Trim(), @"\s*[,;]\s*");
            return trimmedValues;
        }

        public int GetGlobalVarAsInt(string name, int def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : int.Parse(globalVar);
        }

        public UInt64 GetGlobalVarAsUInt64(string name, UInt64 def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : UInt64.Parse(globalVar);
        }

        public double GetGlobalVarAsDouble(string name, double def = 0)
        {
            var globalVar = CPH.GetGlobalVar<string>(name);
            return string.IsNullOrEmpty(globalVar) ? def : double.Parse(globalVar);
        }
    }


    // -------------------------------------------------
    // Needs to be commented out in streamer bot!
    private CPHmock CPH = new CPHmock();
    private Dictionary<string, object> args = CPHmock.args;
    // -------------------------------------------------

    private SceneInteractor itsSceneInteractor = null!;
    private ResponseFetcher itsFetcher = null!;
    private ResponseParser itsParser = null!;
    private GuessingGame itsGuessingGame = null!;
    private DataHandler itsDataHandler = null!;

    private string snifferIp = null!;
    private string snifferPort = null!;

    private string currentScene = "";

    private void UpdateCurrentScene()
    {
        if (itsSceneInteractor == null) { CPH.LogDebug(Constants.AppName + $"SceneInteractor is null!"); }
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

        itsDataHandler = new DataHandler(CPH, args);
        itsSceneInteractor = new SceneInteractor(CPH);
        itsFetcher = new ResponseFetcher(CPH, snifferIp, snifferPort);
        itsGuessingGame = new GuessingGame(CPH, itsDataHandler);
        itsParser = new ResponseParser(CPH, itsSceneInteractor, itsGuessingGame, itsDataHandler);
        itsDataHandler.setArguments(args);

        itsParser.Init();
        UpdateConfig();
        itsSceneInteractor.SetCooldownPeriod(itsParser.GetSceneSwitchCooldownPeriodInSeconds());

        currentScene = "";
    }

    private void UpdateConfig()
    {
        // Init happens before arguments are passed, therefore temporary globals are used.
        snifferIp = GetSnifferIp();
        itsFetcher.setIp(snifferIp);
        // TODO in case snifferIp is null, no need to do anything after this as, Sniffer could be not connected/used.
        snifferPort = GetSnifferPort();
        itsFetcher.setPort(snifferPort);
    }

    private string GetSnifferIp()
    {
        var globalVar = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameSnifferIP);
        // TODO in case not found, return null, or return default localhost?
        return string.IsNullOrEmpty(globalVar) ? null : globalVar.Replace('"', ' ').Trim();
    }

    private string GetSnifferPort()
    {
        var globalVar = itsDataHandler.ReadArgumentAsString(Constants.ArgumentNameSnifferPort);
        return string.IsNullOrEmpty(globalVar) ? Constants.SnifferPortDefault : globalVar.Trim();
    }

    public bool Execute()
    {
        var executionStart = DateTime.Now;
        CPH.LogDebug(Constants.AppName + "Action main ------- START! -------");

        itsDataHandler.setArguments(args);
        UpdateCurrentScene();
        UpdateConfig();
        itsParser.UpdateConfig();
        itsGuessingGame.UpdateConfig();

        string response = itsFetcher.Fetch();

        if (response != string.Empty)
        {
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
            CPH.LogWarn(Constants.AppName + "Action main: Fetching response failed, exiting action.");

            return true;
        }

        CPH.LogDebug(Constants.AppName + "------- END! -------");
        var executionEnd = DateTime.Now;
        CPH.LogDebug(Constants.AppName + $"Action main started at {executionStart} and took a total of {(executionEnd - executionStart).TotalMilliseconds} ms");
        return true;
    }

    public void Dispose()
    {
        CPH.LogDebug(Constants.AppName + "Disposing RockSniffer to SB plugin");
    }

    public bool GetTopGuessers()
    {
        return itsGuessingGame.GetTopGuessers();
    }
}
