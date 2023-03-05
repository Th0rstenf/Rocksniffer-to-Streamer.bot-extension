public class CPHmock
{
    private string currentScene = "RocksmithBigCam";
    public void LogDebug(string str) { Console.WriteLine(str); }
    public void LogInfo(string str) { Console.WriteLine(str); }
    public void LogError(string str) { Console.WriteLine(str); }

    public void LogVerbose(string str) { Console.WriteLine(str); }

    public bool ObsIsConnected(int connection = 0) { return true; }
    public void ObsSetScene(string str) { Console.WriteLine(string.Format("Setting OBS scene to {0}", str)); currentScene = str; }

    public string ObsGetCurrentScene() { return currentScene; }


    public bool SlobsIsConnected(int connectiot = 0) { return false; }
    public void SlobsSetScene(string str) { Console.WriteLine(string.Format("Setting SLOBS scene to {0}", str)); }

    public string SlobsGetCurrentScene() { return currentScene; }
    public void SendMessage(string str) { Console.WriteLine(str); }

    public void RunAction(string str) { Console.WriteLine(string.Format("Running action: {0}", str)); }



    public string GetGlobalVar<Type>(string key)
    {
        string value = "";
        if (key.Equals("snifferIP")) value = "192.168.1.37";
        if (key.Equals("snifferPort")) value = "9938";
        if (key.Equals("songScenes")) value = "RocksmithBigCamInGame";
        if (key.Equals("menuScene")) value = "RocksmithBigCam";
        if (key.Equals("pauseScene")) value = "RocksmithPause";
        if (key.Equals("sectionDetection")) value = "True";
        if (key.Equals("behavior")) value = "WhiteList";
        if (key.Equals("switchScenes")) value = "True";
        if (key.Equals("sectionActions")) value = "True";
        if (key.Equals("blackList")) value = "Scenex,sceney,RocksmithBigCam";

        return value;

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
        int i = 0;
        while (true)
        {
            if (((++i) % 10) == 0) Console.Clear();
            obj.Execute();
            Thread.Sleep(1000);
        }
    }

}

