using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class NightAgentBridge
{
    public const string CommandFilePath = "TestScreenshots/night_agent_cmd.txt";
    public const string ResultFilePath = "TestScreenshots/night_agent_result.txt";

    private static double _nextCheckTime;

    static NightAgentBridge()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.delayCall += TryProcessPendingCommand;
    }

    private static void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup < _nextCheckTime)
            return;

        _nextCheckTime = EditorApplication.timeSinceStartup + 1.0;
        TryProcessPendingCommand();
    }

    public static void TryProcessPendingCommand()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (!File.Exists(CommandFilePath))
            return;

        string rawCmd;
        try
        {
            rawCmd = File.ReadAllText(CommandFilePath, Encoding.UTF8).Trim();
            File.Delete(CommandFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NightAgentBridge] Komut dosyası okunamadı: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(rawCmd))
            return;

        ExecuteCommandString(rawCmd);
    }

    public static void RunFromCommandLine()
    {
        if (File.Exists(CommandFilePath))
        {
            string rawCmd = File.ReadAllText(CommandFilePath, Encoding.UTF8).Trim();
            File.Delete(CommandFilePath);
            ExecuteCommandString(rawCmd);
        }
        else
        {
            NightAgentMenu.EnsureDemoSceneOpen();
            NightAgentVerifier.ExportSceneHierarchySnapshot();
            WriteResult("PASS: SNAPSHOT COMPLETED");
        }
    }

    public static void ExecuteCommandString(string commandLine)
    {
        try
        {
            string[] parts = commandLine.Split(':');
            string action = parts[0].Trim().ToUpperInvariant();
            int phaseNo = parts.Length > 1 && int.TryParse(parts[1].Trim(), out int p) ? p : 0;

            switch (action)
            {
                case "SNAPSHOT":
                    NightAgentMenu.EnsureDemoSceneOpen();
                    NightAgentVerifier.ExportSceneHierarchySnapshot();
                    WriteResult("PASS: SNAPSHOT COMPLETED");
                    break;

                case "APPLY":
                    bool applied = NightAgentMenu.ApplyPhaseByNumber(phaseNo);
                    WriteResult(applied ? $"PASS: APPLY PHASE {phaseNo}" : $"FAIL: APPLY PHASE {phaseNo}");
                    break;

                case "REVERT":
                    bool reverted = NightAgentMenu.RevertPhaseByNumber(phaseNo);
                    WriteResult(reverted ? $"PASS: REVERT PHASE {phaseNo}" : $"FAIL: REVERT PHASE {phaseNo}");
                    break;

                case "VERIFY":
                    bool verified = NightAgentVerifier.VerifyPhase(phaseNo, out string report);
                    WriteResult(report);
                    break;

                default:
                    WriteResult($"FAIL: Bilinmeyen komut '{commandLine}'");
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteResult($"FAIL: Exception during '{commandLine}': {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void WriteResult(string content)
    {
        if (!Directory.Exists("TestScreenshots"))
        {
            Directory.CreateDirectory("TestScreenshots");
        }

        string fullOutput = $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{content}";
        File.WriteAllText(ResultFilePath, fullOutput, Encoding.UTF8);
        Debug.Log($"[NightAgentBridge] Sonuç yazıldı:\n{fullOutput}");
    }
}
