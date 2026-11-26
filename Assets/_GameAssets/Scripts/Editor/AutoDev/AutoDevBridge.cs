using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class AutoDevBridge
{
    public const string RuntimeDir = ".autodev";
    public const string CmdFilePath = ".autodev/unity_cmd.json";
    public const string ResultFilePath = ".autodev/unity_result.json";
    public const string DefaultSnapshotPath = ".autodev/scene_hierarchy.txt";

    private static readonly List<string> CompileErrors = new List<string>();
    private static double _nextPollTime;

    [Serializable]
    private class BridgeCommand
    {
        public string id;
        public string action;
        public string arg;
        public string scenePath;
        public string phase;
    }

    [Serializable]
    private class BridgeResult
    {
        public string id;
        public bool passed;
        public string summary;
        public string details;
        public string timestamp;
    }

    static AutoDevBridge()
    {
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.delayCall += PollAndProcess;
    }

    private static void OnCompilationStarted(object _)
    {
        CompileErrors.Clear();
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        if (messages == null) return;
        foreach (var msg in messages)
        {
            if (msg.type == CompilerMessageType.Error)
            {
                string line = $"{msg.file}({msg.line},{msg.column}): {msg.message}";
                if (CompileErrors.Count < 30)
                {
                    CompileErrors.Add(line);
                }
            }
        }
    }

    private static void OnCompilationFinished(object _)
    {
        // If compilation failed, domain reload will NOT occur; fail any pending REFRESHED command immediately.
        if (CompileErrors.Count == 0 || !File.Exists(CmdFilePath))
            return;

        BridgeCommand cmd = ReadCommandSafe();
        if (cmd == null)
            return;

        DeleteCommandSafe();
        WriteResult(
            cmd.id,
            false,
            $"C# derleme hatasi ({CompileErrors.Count} hata).",
            string.Join("\n", CompileErrors));
    }

    private static void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup < _nextPollTime)
            return;
        _nextPollTime = EditorApplication.timeSinceStartup + 0.5;
        PollAndProcess();
    }

    private static void PollAndProcess()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (!File.Exists(CmdFilePath))
            return;

        BridgeCommand cmd = ReadCommandSafe();
        if (cmd == null || string.IsNullOrEmpty(cmd.action))
        {
            DeleteCommandSafe();
            return;
        }

        // Phase 1: Force AssetDatabase refresh so newly written C# scripts compile before execution
        if (!string.Equals(cmd.phase, "REFRESHED", StringComparison.OrdinalIgnoreCase))
        {
            CompileErrors.Clear();
            cmd.phase = "REFRESHED";
            try
            {
                File.WriteAllText(CmdFilePath, JsonUtility.ToJson(cmd, true), Encoding.UTF8);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                DeleteCommandSafe();
                WriteResult(cmd.id, false, "AssetDatabase.Refresh hatasi.", ex.Message);
            }
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (CompileErrors.Count > 0)
        {
            DeleteCommandSafe();
            WriteResult(
                cmd.id,
                false,
                $"C# derleme hatasi ({CompileErrors.Count} hata).",
                string.Join("\n", CompileErrors));
            return;
        }

        DeleteCommandSafe();
        ExecuteCommand(cmd);
    }

    public static void RunFromCommandLine()
    {
        BridgeCommand cmd = ReadCommandSafe();
        if (cmd == null || string.IsNullOrEmpty(cmd.action))
        {
            cmd = new BridgeCommand
            {
                id = "cli",
                action = "SNAPSHOT",
                arg = DefaultSnapshotPath,
                scenePath = AutoDevVerifier.DefaultScenePath,
                phase = "REFRESHED"
            };
        }
        else
        {
            DeleteCommandSafe();
        }

        ExecuteCommand(cmd);
    }

    private static void ExecuteCommand(BridgeCommand cmd)
    {
        string scenePath = string.IsNullOrEmpty(cmd.scenePath)
            ? AutoDevVerifier.DefaultScenePath
            : cmd.scenePath;
        string action = (cmd.action ?? "").Trim().ToUpperInvariant();

        try
        {
            switch (action)
            {
                case "COMPILE_CHECK":
                    WriteResult(cmd.id, true, "0 derleme hatasi (Unity C# temiz derlendi).", "");
                    break;

                case "SNAPSHOT":
                {
                    string outPath = string.IsNullOrEmpty(cmd.arg) ? DefaultSnapshotPath : cmd.arg;
                    AutoDevVerifier.EnsureSceneOpen(scenePath);
                    string summary = AutoDevVerifier.ExportSceneHierarchySnapshot(outPath);
                    WriteResult(cmd.id, true, $"Sahne ozeti uretildi: {outPath}", summary);
                    break;
                }

                case "VERIFY":
                {
                    string checkId = string.IsNullOrEmpty(cmd.arg) ? "SMOKE" : cmd.arg.Trim();
                    AutoDevCheckResult res = AutoDevVerifier.RunCheck(checkId, scenePath);
                    WriteResult(cmd.id, res.Passed, res.Summary, res.Details);
                    break;
                }

                case "SCREENSHOT":
                {
                    string outPng = string.IsNullOrEmpty(cmd.arg) ? ".autodev/preview.png" : cmd.arg;
                    AutoDevVerifier.EnsureSceneOpen(scenePath);
                    bool ok = AutoDevVerifier.CaptureCameraPng(outPng, out string err);
                    WriteResult(
                        cmd.id,
                        ok,
                        ok ? $"Ekran goruntusu kaydedildi: {outPng}" : "Ekran goruntusu alinamadi.",
                        err);
                    break;
                }

                default:
                    WriteResult(cmd.id, false, $"Bilinmeyen kopru komutu: '{action}'", "");
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteResult(cmd.id, false, $"Kopru calisma hatasi ({action}): {ex.Message}", ex.StackTrace);
        }
    }

    private static BridgeCommand ReadCommandSafe()
    {
        try
        {
            if (!File.Exists(CmdFilePath)) return null;
            string raw = File.ReadAllText(CmdFilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return JsonUtility.FromJson<BridgeCommand>(raw);
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteCommandSafe()
    {
        try
        {
            if (File.Exists(CmdFilePath))
            {
                File.Delete(CmdFilePath);
            }
        }
        catch
        {
            // Ignore transient lock
        }
    }

    private static void WriteResult(string id, bool passed, string summary, string details)
    {
        Directory.CreateDirectory(RuntimeDir);
        var result = new BridgeResult
        {
            id = id ?? "",
            passed = passed,
            summary = summary ?? "",
            details = details ?? "",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
        string json = JsonUtility.ToJson(result, true);
        string tmp = ResultFilePath + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        if (File.Exists(ResultFilePath))
        {
            File.Delete(ResultFilePath);
        }
        File.Move(tmp, ResultFilePath);
        Debug.Log($"[AutoDevBridge] {(passed ? "PASS" : "FAIL")} ({id}): {summary}");
    }
}
