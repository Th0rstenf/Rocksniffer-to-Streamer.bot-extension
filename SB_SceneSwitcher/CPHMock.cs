using YamlDotNet.Serialization;

public class CPHmock : IInlineInvokeProxy
{
    private static readonly Config? _config = readConfig();
    private static readonly CPHInline.LogLevel DefaultLogLevel = CPHInline.LogLevel.INFO;
    private static readonly CPHInline.LogLevel DefaultLogLevelSB = CPHInline.LogLevel.DEBUG;

    private static CPHInline.LogLevel LogLevel;
    private static CPHInline.LogLevel LogLevelSB;
    private string? currentScene;

    public void LogError(string str)
    {
        if (LogLevel <= CPHInline.LogLevel.ERROR)
            throw new Exception("[ " + DateTime.Now + " ERR] " + str);
    }

    public void LogWarn(string str)
    {
        if (LogLevel <= CPHInline.LogLevel.WARN) Console.WriteLine("[ " + DateTime.Now + " WRN] " + str);
    }

    public void LogInfo(string str)
    {
        if (LogLevel <= CPHInline.LogLevel.INFO) Console.WriteLine("[ " + DateTime.Now + " INF] " + str);
    }

    public void LogDebug(string str)
    {
        if (LogLevel <= CPHInline.LogLevel.DEBUG) Console.WriteLine("[ " + DateTime.Now + " DBG] " + str);
    }

    public void LogVerbose(string str)
    {
        if (LogLevel <= CPHInline.LogLevel.VERBOSE) Console.WriteLine("[ " + DateTime.Now + " VER] " + str);
    }

    public bool ObsIsConnected(int connection = 0)
    {
        return true;
    }

    public void ObsSetScene(string str)
    {
        Console.WriteLine($"Setting OBS scene to {str}");
        currentScene = str;
    }

    public string ObsGetCurrentScene()
    {
        return currentScene ??= _config?.menuScene ?? "";
    }

    public bool SlobsIsConnected(int connection = 0)
    {
        return false;
    }

    public void SlobsSetScene(string str)
    {
        Console.WriteLine($"Setting SLOBS scene to {str}");
    }

    public string SlobsGetCurrentScene()
    {
        return currentScene;
    }

    public void SendMessage(string str)
    {
        Console.WriteLine(str);
    }

    public void RunAction(string str)
    {
        Console.WriteLine($"Running action: {str}");
    }

    public string? GetGlobalVar<Type>(string key)
    {
        if (key == "logLevel")
        {
            return _config == null ? DefaultLogLevel.ToString() : _config.logLevel;
        }

        return key switch
        {
            "snifferIP" => _config?.snifferIp,
            "snifferPort" => _config?.snifferPort,
            "songScenes" => _config?.songScenes,
            "menuScene" => _config?.menuScene,
            "pauseScene" => _config?.pauseScene,
            "sectionDetection" => _config?.sectionDetection,
            "behavior" => _config?.behavior,
            "switchScenes" => _config?.switchScenes,
            "sectionActions" => _config?.sectionActions,
            "blackList" => _config?.blackList,
            _ => throw new InvalidOperationException("Key " + key + " is not found!")
        };
    }

    public void SetGlobalVar(string varName, object value, bool persisted = true)
    {
        LogVerbose(string.Format("Writing value {1} to variable {0}", varName, value));
    }

    public void UnsetGlobalVar(string varName, bool persisted = true)
    {
        Console.WriteLine("Invalidating var: " + varName);
    }

    public static void Main(string[] args)
    {
        CPHInline obj = new CPHInline();

        obj.Init();
        while (true)
        {
            obj.Execute();
            Thread.Sleep(1000);
        }
    }

    private static Config readConfig()
    {
        var json = File.ReadAllText("config.yml");
        var config = new DeserializerBuilder().Build().Deserialize<Config>(json);

        setLogLevel(config);
        setLogLevelSB(config);

        Console.WriteLine("----------- CONFIG ------------------------------------");
        Console.WriteLine("sniffIP=" + config.snifferIp);
        Console.WriteLine("sniffPort=" + config.snifferPort);
        Console.WriteLine("songScenes=" + config.songScenes);
        Console.WriteLine("menuScene=" + config.menuScene);
        Console.WriteLine("pauseScene=" + config.pauseScene);
        Console.WriteLine("sectionDetection=" + config.sectionDetection);
        Console.WriteLine("behavior=" + config.behavior);
        Console.WriteLine("switchScenes=" + config.switchScenes);
        Console.WriteLine("sectionActions=" + config.sectionActions);
        Console.WriteLine("blackList=" + config.blackList);
        Console.WriteLine("logLevel=" + config.logLevel);
        Console.WriteLine("logLevelSB=" + config.logLevelSB);
        Console.WriteLine("-------------------------------------------------------");

        return config;
    }

    private static void setLogLevel(Config config)
    {
        if (config.logLevel == null)
        {
            Console.WriteLine("logLevel not found! Will use default Level: " + DefaultLogLevel);
            config.logLevel = DefaultLogLevel.ToString();
        }

        LogLevel = CPHInline.GetLogLevel(_config?.logLevel);
    }

    private static void setLogLevelSB(Config config)
    {
        if (config.logLevelSB == null)
        {
            Console.WriteLine("logLevelSB not found! Will use default Level: " + DefaultLogLevelSB);
            config.logLevelSB = DefaultLogLevelSB.ToString();
        }

        LogLevelSB = CPHInline.GetLogLevel(_config?.logLevelSB);
    }

    private record Config
    {
        public string? snifferIp { get; set; }
        public string? snifferPort { get; set; }
        public string? songScenes { get; set; }
        public string? menuScene { get; set; }
        public string? pauseScene { get; set; }
        public string? sectionDetection { get; set; }
        public string? behavior { get; set; }
        public string? switchScenes { get; set; }
        public string? sectionActions { get; set; }
        public string? blackList { get; set; }
        public string? logLevel { get; set; }
        public string? logLevelSB { get; set; }
    }
}