using System.Collections.Generic;

/// <summary>
/// A test double for IInlineInvokeProxy that records interactions for assertions.
/// </summary>
public class TestCPH : IInlineInvokeProxy
{
    public List<string> LogMessages { get; } = new();
    public Dictionary<string, object?> GlobalVars { get; } = new();
    public List<(string EventName, object Args)> TriggeredEvents { get; } = new();
    public List<string> SentMessages { get; } = new();
    public bool ObsConnected { get; set; } = true;
    public string CurrentScene { get; set; } = "MenuScene";

    public string GetGlobalVar<Type>(string key, bool persisted = true)
    {
        if (GlobalVars.TryGetValue(key, out var val))
            return val?.ToString() ?? string.Empty;
        return null!;
    }

    public void LogWarn(string str) => LogMessages.Add($"WARN: {str}");
    public void LogInfo(string str) => LogMessages.Add($"INFO: {str}");
    public void LogDebug(string str) => LogMessages.Add($"DEBUG: {str}");
    public void LogVerbose(string str) => LogMessages.Add($"VERBOSE: {str}");

    public string ObsGetCurrentScene() => CurrentScene;
    public bool ObsIsConnected(int connection = 0) => ObsConnected;
    public void ObsSetScene(string str) => CurrentScene = str;

    public void RunAction(string str, bool runImmediately = true) { }
    public void SendMessage(string str, bool bot = true) => SentMessages.Add(str);
    public void SendYouTubeMessage(string str, bool bot = true) => SentMessages.Add(str);
    public void SetGlobalVar(string varName, object value, bool persisted = true) => GlobalVars[varName] = value;
    public string SlobsGetCurrentScene() => string.Empty;
    public bool SlobsIsConnected(int connection = 0) => false;
    public void SlobsSetScene(string str) { }
    public void UnsetGlobalVar(string varName, bool persisted = true) => GlobalVars.Remove(varName);

    public bool RegisterCustomTrigger(string triggerName, string eventName, string[] categories) => true;
    public void TriggerCodeEvent(string eventName, bool useArgs = true)
        => TriggeredEvents.Add((eventName, useArgs));
    public void TriggerCodeEvent(string eventName, Dictionary<string, object> args)
        => TriggeredEvents.Add((eventName, args));

    public void Wait(int milliseconds) { }

    public bool TryGetArg<T>(string argName, out T value)
    {
        if (GlobalVars.TryGetValue(argName, out var val) && val is T typedVal)
        {
            value = typedVal;
            return true;
        }
        value = default!;
        return false;
    }

    public bool TryGetArg(string argName, out object value)
    {
        if (GlobalVars.TryGetValue(argName, out var val) && val != null)
        {
            value = val;
            return true;
        }
        value = null!;
        return false;
    }
}
