﻿public interface IInlineInvokeProxy
{
    string GetGlobalVar<Type>(string key, bool persisted = true);
    void LogWarn(string str);
    void LogInfo(string str);
    void LogDebug(string str);
    void LogVerbose(string str);
    string ObsGetCurrentScene();
    bool ObsIsConnected(int connection = 0);
    void ObsSetScene(string str);
    void RunAction(string str);
    void SendMessage(string str, bool bot = true);
    void SendYouTubeMessage(string str, bool bot = true);
    void SetGlobalVar(string varName, object value, bool persisted = true);
    string SlobsGetCurrentScene();
    bool SlobsIsConnected(int connection = 0);
    void SlobsSetScene(string str);
    void UnsetGlobalVar(string varName, bool persisted = true);
}