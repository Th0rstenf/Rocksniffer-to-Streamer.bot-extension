using YamlDotNet.Serialization;

public class CPHmock : IInlineInvokeProxy
{
    private static readonly Config? _config = readConfig();

    private string? currentScene;

    public void LogDebug(string str) { Console.WriteLine(str); }
    public void LogInfo(string str) { Console.WriteLine(str); }

    public void LogVerbose(string str) { Console.WriteLine(str); }

    public bool ObsIsConnected(int connection = 0) { return true; }

    public void ObsSetScene(string str) { Console.WriteLine(string.Format("Setting OBS scene to {0}", str)); currentScene = str; }

    public string ObsGetCurrentScene() { return currentScene ??= _config?.menuScene ?? ""; }

    public bool SlobsIsConnected(int connection = 0) { return false; }

    public void SlobsSetScene(string str) { Console.WriteLine(string.Format("Setting SLOBS scene to {0}", str)); }

    public string SlobsGetCurrentScene() { return currentScene; }

    public void SendMessage(string str) { Console.WriteLine(str); }

    public void RunAction(string str) { Console.WriteLine(string.Format("Running action: {0}", str)); }

    public string? GetGlobalVar<Type>(string key)
    {
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
        //   Console.WriteLine(string.Format("Writing value {1} to variable {0}",varName,value));
    }
    public void UnsetGlobalVar(string varName, bool persisted = true)
    {
        Console.WriteLine("Invalidating var: " + varName);
    }
    public static void Main(string[] args)
    {
        CPHInline obj = new CPHInline();

        obj.Init();
        obj.SetLogDebug(true);

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
        Console.WriteLine("-------------------------------------------------------");

        return config;
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
    }

}
