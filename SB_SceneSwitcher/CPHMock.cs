using YamlDotNet.Serialization;
using static CPHmock.LogLevels;
using static MockConstants;

public static class MockConstants
{
    public const string MockAppName = "[MOCK] ";

    public const string DefaultSceneSwitchPeriod = "5";
    public const string DefaultSceneSwitchCooldownPeriod = "3";
}

public class CPHmock : IInlineInvokeProxy
{
    private static readonly Config? _config = readConfig();
    private const LogLevels DefaultLogLevel = Info;
    private const LogLevels DefaultLogLevelSB = Debug;

    private static LogLevels LogLevel;
    private static LogLevels LogLevelSB;
    private string? currentScene;

    public static Dictionary<string, object> args;

    private static Dictionary<string, Tuple<string, String[]>> triggers;

    public enum LogLevels
    {
        Verbose,
        Debug,
        Info,
        Warn,
        Error_
    }

    public CPHmock()
    {
        LogLevel = GetLogLevel(_config?.logLevel);
        LogLevelSB = GetLogLevel(_config?.logLevelSB);
        args = new Dictionary<string, object>();
        triggers = new Dictionary<string, Tuple<string, String[]>>();


        args.Add(Constants.ArgumentNameSnifferIP, _config.snifferIp);
        args.Add(Constants.ArgumentNameSnifferPort, _config.snifferPort);
        args.Add(Constants.ArgumentNameMenuScene, _config.menuScene);
        args.Add(Constants.ArgumentNameSongScenes, _config.songScenes);
        args.Add(Constants.ArgumentNamePauseScene, _config?.pauseScene);
        args.Add(Constants.ArgumentNameSwitchScenes, _config?.switchScenes);
        args.Add(Constants.ArgumentNameSceneSwitchPeriod, _config?.sceneSwitchPeriod);
        args.Add(Constants.ArgumentNameSceneSwitchCooldownPeriod, _config.sceneSwitchCooldownPeriod);
        args.Add(Constants.ArgumentNameSongSceneAutoSwitchMode, _config?.songSceneAutoSwitchMode);
        args.Add(Constants.ArgumentNameSectionActions, _config?.sectionActions);
        args.Add(Constants.ArgumentNameBehavior, _config?.behavior);
        args.Add(Constants.ArgumentNameBlackList, _config?.blackList);



}

    public void LogWarn(string str)
    {
        if (LogLevelSB <= Warn) Console.WriteLine(Now("WRN") + str);
    }

    public void LogInfo(string str)
    {
        if (LogLevelSB <= Info) Console.WriteLine(Now("INF") + str);
    }

    public void LogDebug(string str)
    {
        if (LogLevelSB <= Debug) Console.WriteLine(Now("DBG") + str);
    }

    public void LogVerbose(string str)
    {
        if (LogLevelSB <= Verbose) Console.WriteLine(Now("VER") + str);
    }

    private static string Now(string logLevel)
    {
        return "[ " + DateTime.Now + " " + logLevel + "] ";
    }

    private static LogLevels GetLogLevel(string? level)
    {
        if (level == null) return DefaultLogLevel;
        return level switch
        {
            "VERBOSE" => Verbose,
            "DEBUG" => Debug,
            "INFO" => Info,
            "WARN" => Warn,
            "ERROR" => Error_,
            _ => DefaultLogLevel
        };
    }

    public bool ObsIsConnected(int connection = 0)
    {
        return true;
    }

    public void ObsSetScene(string str)
    {
        Console.WriteLine(MockAppName + $"Setting OBS scene to: {str}");
        currentScene = str;
    }

    public string ObsGetCurrentScene()
    {
        var obsGetCurrentScene = currentScene ??= _config?.menuScene ?? "";
        //Console.WriteLine(MockAppName + $"OBS current scene is: {obsGetCurrentScene}");
        return obsGetCurrentScene;
    }

    public bool SlobsIsConnected(int connection = 0)
    {
        return false;
    }

    public void SlobsSetScene(string str)
    {
        Console.WriteLine(MockAppName + $"Setting SLOBS scene to {str}");
    }

    public string SlobsGetCurrentScene()
    {
        return currentScene;
    }

    public void SendMessage(string str, bool bot = true)
    {
        Console.WriteLine(MockAppName + $"SendMessage: {str}");
    }

    public void SendYouTubeMessage(string str, bool bot = true)
    {

    }

    public void RunAction(string str, bool runImmediately = true)
    {
        string when = runImmediately ? "immediately" : "consecutively";
        Console.WriteLine(MockAppName + $"Running action: {str} {when}");
    }


    public string? GetGlobalVar<Type>(string key, bool persisted = true)
    {
        var value = key switch
        {
            "snifferIP" => _config?.snifferIp,
            "snifferPort" => _config?.snifferPort,
            "songScenes" => _config?.songScenes,
            "menuScene" => _config?.menuScene,
            "pauseScene" => _config?.pauseScene,
            "behavior" => _config?.behavior,
            "switchScenes" => _config?.switchScenes,
            "songSceneAutoSwitchMode" => _config?.songSceneAutoSwitchMode,
            "sceneSwitchPeriod" => GetConfigSceneSwitchPeriod(),
            "sceneSwitchCooldownPeriod" => GetConfigSceneSwitchCooldownPeriod(),
            "sectionActions" => _config?.sectionActions,
            "blackList" => _config?.blackList,
            "logLevel" => _config == null ? DefaultLogLevel.ToString() : _config.logLevel,
            "guessingIsActive" => "True",
            "guessMinGuesserCount" => _config == null ? "1" : _config.guessMinGuesserCount,
            "guessTime" => _config == null ? "30" : _config.guessTime,
            "guessingDictionary" => _config == null ? "{{Th0lamin : 90}}" :_config.guessingDictionary,
            "guessStartingText" => "Starting to accept guesses",
            "guessTimeoutText" => "No more guesses allowed",
            Constants.GlobalVarNameTotalNotesLifeTime => "0",
            Constants.GlobalVarNameTotalNotesHitLifeTime => "0",
            Constants.GlobalVarNameTotalNotesMissedLifeTime => "0",
            Constants.GlobalVarNameAccuracyLifeTime => "0,0",
            Constants.GlobalVarNameGuessingWinnersCount => "{\"Th0lamin (twitch)\":4,\"RedVarg91 (twitch)\":2,\"Skippern666 (twitch)\":1,\"RubberDave (twitch)\":3,\"Puffelmuggu (twitch)\":1,\"Boernii (twitch)\":1,\"cryptghoul (twitch)\":1,\"theshadow63 (twitch)\":1,\"vincent77600 (twitch)\":1,\"Basilius_Fleischlein (twitch)\":1,\"maxwattel20 (twitch)\":1,\"FF_ByTheSword (twitch)\":1,\"DerPlaymo (twitch)\":4,\"naturaledge (twitch)\":1}",
            _ => null
        }; ;

        if (value == null) Console.WriteLine($"{MockAppName}Key {key} is not found in config.yml!");

        return value;
    }

    private static string GetConfigSceneSwitchPeriod()
    {
        return string.IsNullOrEmpty(_config?.sceneSwitchPeriod)
            ? DefaultSceneSwitchPeriod
            : _config.sceneSwitchPeriod;
    }

    private static string GetConfigSceneSwitchCooldownPeriod()
    {
        return string.IsNullOrEmpty(_config?.sceneSwitchCooldownPeriod)
            ? DefaultSceneSwitchCooldownPeriod
            : _config.sceneSwitchCooldownPeriod;
    }

    public void SetGlobalVar(string varName, object value, bool persisted = true)
    {
        if (LogLevel <= Verbose)
        {
            Console.WriteLine(
                MockAppName + string.Format("Writing value {1} to variable {0}", varName, value));
        }
    }

    public void UnsetGlobalVar(string varName, bool persisted = true)
    {
        Console.WriteLine(MockAppName + $"UnsetGlobalVar var: {varName}");
    }
    
    public bool RegisterCustomTrigger(string triggerName, string eventName, String[] categories)
    {
        if (!triggers.ContainsKey(eventName))
        {
            triggers.Add(eventName, new Tuple<string, string[]>(triggerName, categories));
            return true;
        }
        return false;
    }

    public void TriggerCodeEvent(string eventName, bool useArgs = true)
    {

    }
    public void TriggerCodeEvent(string eventName, Dictionary<string, object> args)
    {

    }

    public bool TryGetArg<T>(string argName, out T value)
    {
        if (args.ContainsKey(argName))
        {
            value = (T)args[argName];
            return true;
        }
        value = default(T);
        return false;
    }

    public bool TryGetArg(string argName, out object value)
    {
        if (args.ContainsKey(argName))
        {
            value = args[argName];
            return true;
        }
        value = null;
        return false;
    }
   
    public void Wait(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }

    public static void Main(string[] args)
    {
        CPHInline cphInline = new CPHInline();

        bool testTopGuessers = true;

        cphInline.Init();

        if (testTopGuessers)
        {
            for (; ; )
            {
                cphInline.GetTopGuessers();
                Thread.Sleep(1000);
            }
            return;
        }
        while (true)
        {
            cphInline.Execute();
            Thread.Sleep(1000);


        }
    }

    private static Config readConfig()
    {
        var json = File.ReadAllText("config.yml");
        var config = new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<Config>(json);

        SetLogLevel(config);
        SetLogLevelSB(config);

        Console.WriteLine(MockAppName + config);

        return config;
    }

    private static void SetLogLevel(Config config)
    {
        if (string.IsNullOrEmpty(config.logLevel))
        {
            Console.WriteLine($"{MockAppName}logLevel not found! Will use default Level: {DefaultLogLevel}");
            config.logLevel = DefaultLogLevel.ToString();
        }

        LogLevel = GetLogLevel(config.logLevel);
    }

    private static void SetLogLevelSB(Config config)
    {
        if (string.IsNullOrEmpty(config.logLevelSB))
        {
            Console.WriteLine($"{MockAppName}logLevelSB not found! Will use default Level: {DefaultLogLevelSB}");
            config.logLevelSB = DefaultLogLevelSB.ToString();
        }

        LogLevelSB = GetLogLevel(config.logLevelSB);
    }

    private record Config
    {
        public string? snifferIp { get; set; }
        public string? snifferPort { get; set; }
        public string? menuScene { get; set; }
        public string? songScenes { get; set; }
        public string? pauseScene { get; set; }
        public string? blackList { get; set; }
        public string? behavior { get; set; }
        public string? switchScenes { get; set; }
        public string? songSceneAutoSwitchMode { get; set; }
        public string? sceneSwitchPeriod { get; set; }
        public string? sceneSwitchCooldownPeriod { get; set; }
        public string? sectionActions { get; set; }
        public string? logLevel { get; set; }
        public string? logLevelSB { get; set; }
        public string? guessMinGuesserCount { get; set; }
        public string? guessTime { get; set; }
        public string? guessingDictionary { get; set; }

        public override string ToString()
        {
            return $"----------- CONFIG ------------------------------------{Environment.NewLine}" +
                   $"{nameof(snifferIp)}: {snifferIp}{Environment.NewLine}" +
                   $"{nameof(snifferPort)}: {snifferPort}{Environment.NewLine}" +
                   $"{nameof(menuScene)}: {menuScene}{Environment.NewLine}" +
                   $"{nameof(songScenes)}: {songScenes}{Environment.NewLine}" +
                   $"{nameof(pauseScene)}: {pauseScene}{Environment.NewLine}" +
                   $"{nameof(blackList)}: {blackList}{Environment.NewLine}" +
                   $"{nameof(behavior)}: {behavior}{Environment.NewLine}" +
                   $"{nameof(switchScenes)}: {switchScenes}{Environment.NewLine}" +
                   $"{nameof(songSceneAutoSwitchMode)}: {songSceneAutoSwitchMode}{Environment.NewLine}" +
                   $"{nameof(sceneSwitchPeriod)}: {sceneSwitchPeriod}{Environment.NewLine}" +
                   $"{nameof(sceneSwitchCooldownPeriod)}: {sceneSwitchCooldownPeriod}{Environment.NewLine}" +
                   $"{nameof(sectionActions)}: {sectionActions}{Environment.NewLine}" +
                   $"{nameof(logLevel)}: {logLevel}{Environment.NewLine}" +
                   $"{nameof(logLevelSB)}: {logLevelSB}{Environment.NewLine}" +
                   "-------------------------------------------------------";
        }
    }
}