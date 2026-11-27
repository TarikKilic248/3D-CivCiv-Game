using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProjectCleanupAuditor
{
    public const string AuditReportPath = ".autodev/cleanup_audit.txt";

    // Top-level directories / assets that are always preserved structurally (C# scripts, Unity settings, InputSystem)
    private static readonly string[] AlwaysKeepPrefixes =
    {
        "Assets/_GameAssets/Scripts/",
        "Assets/Settings/",
        "Assets/TextMesh Pro/",
        "Assets/_GameAssets/Data/",
        "Assets/_GameAssets/Sprites/",
        "Assets/InputSystem_Actions.inputactions"
    };

    // Asset folders eligible for pruning unreferenced files
    private static readonly string[] PrunableRoots =
    {
        "Assets/Synty/",
        "Assets/_GameAssets/RestaurantBits/",
        "Assets/_GameAssets/Kitchen Treats/",
        "Assets/_GameAssets/Sounds/",
        "Assets/_GameAssets/Animations/",
        "Assets/_GameAssets/Character/",
        "Assets/_GameAssets/Metarials/",
        "Assets/_GameAssets/Prefabs/",
        "Assets/_GameAssets/Scenes/",
        "Assets/Plugins/Demigiant/",
        "Assets/Resources/DOTweenSettings.asset",
        "Assets/Default.unity",
        "Assets/DroneTest.unity",
        "Assets/New Terrain.asset"
    };

    [AutoDevCheck("AUDIT_CLEANUP")]
    public static AutoDevCheckResult RunAuditCheck()
    {
        BuildPlan(out List<string> keepList, out List<string> deleteList, out long deleteBytes, out Dictionary<string, (int delCount, long delBytes, int keepCount)> byBucket);
        string report = FormatReport(keepList, deleteList, deleteBytes, byBucket, applied: false);
        Directory.CreateDirectory(".autodev");
        File.WriteAllText(AuditReportPath, report, new UTF8Encoding(false));
        return AutoDevCheckResult.Pass(
            $"AUDIT PASS: {keepList.Count} dosya korunuyor, {deleteList.Count} kullanilmayan dosya ({deleteBytes / (1024f * 1024f):F2} MB) silinebilir.",
            $"Rapor: {AuditReportPath}");
    }

    [AutoDevCheck("APPLY_CLEANUP")]
    public static AutoDevCheckResult RunApplyCleanup()
    {
        BuildPlan(out List<string> keepList, out List<string> deleteList, out long deleteBytes, out Dictionary<string, (int delCount, long delBytes, int keepCount)> byBucket);
        int deleted = 0;
        var failed = new List<string>();

        // Delete deepest paths first so folders/sub-assets delete cleanly
        foreach (string assetPath in deleteList.OrderByDescending(p => p.Length))
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null && !File.Exists(assetPath))
                continue;
            if (AssetDatabase.DeleteAsset(assetPath))
                deleted++;
            else
                failed.Add(assetPath);
        }

        PruneEmptyAssetDirectories("Assets");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // Verify Demo.unity has zero missing scripts or broken material/mesh/sprite references
        string integrityErr = CheckSceneIntegrity(AutoDevVerifier.DefaultScenePath);
        string report = FormatReport(keepList, deleteList, deleteBytes, byBucket, applied: true)
            + $"\nDeleted: {deleted}, Failed: {failed.Count}\nIntegrity: {(string.IsNullOrEmpty(integrityErr) ? "OK" : integrityErr)}\n";
        Directory.CreateDirectory(".autodev");
        File.WriteAllText(AuditReportPath, report, new UTF8Encoding(false));

        if (!string.IsNullOrEmpty(integrityErr))
            return AutoDevCheckResult.Fail("Temizlik sonrasi sahne butunluk hatasi!", integrityErr);

        return AutoDevCheckResult.Pass(
            $"CLEANUP PASS: {deleted} kullanilmayan dosya ({deleteBytes / (1024f * 1024f):F2} MB) silindi, sahne butunlugu temiz.",
            $"Korunan: {keepList.Count} | Basarisiz: {failed.Count}");
    }

    public static void BuildPlan(
        out List<string> keepList,
        out List<string> deleteList,
        out long deleteBytes,
        out Dictionary<string, (int delCount, long delBytes, int keepCount)> byBucket)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AutoDevVerifier.DefaultScenePath,
            "Assets/InputSystem_Actions.inputactions"
        };

        AddRenderPipelineRoots(roots);
        AddFolderAssetRoots("Assets/Settings", roots);
        AddFolderAssetRoots("Assets/_GameAssets/Data", roots);
        AddFolderAssetRoots("Assets/_GameAssets/Prefabs", roots);
        AddFolderAssetRoots("Assets/_GameAssets/Sprites", roots);
        AddFolderAssetRoots("Assets/TextMesh Pro/Resources", roots);

        // Keep SettingsUI audio fallback search matches in Sounds if any
        foreach (string term in new[] { "Gymnopédie", "Minuet", "Tales", "cozy_fx" })
        {
            foreach (string guid in AssetDatabase.FindAssets($"{term} t:AudioClip"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p)) roots.Add(p);
            }
        }

        string[] allDeps = AssetDatabase.GetDependencies(roots.ToArray(), true);
        var usedSet = new HashSet<string>(allDeps, StringComparer.OrdinalIgnoreCase);
        foreach (string r in roots) usedSet.Add(r);

        keepList = new List<string>();
        deleteList = new List<string>();
        deleteBytes = 0;
        byBucket = new Dictionary<string, (int, long, int)>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (AssetDatabase.IsValidFolder(path))
                continue;

            bool isUsed = usedSet.Contains(path) || AlwaysKeepPrefixes.Any(pref => path.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
            bool isPrunable = PrunableRoots.Any(root =>
                string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(root, StringComparison.OrdinalIgnoreCase));

            string bucket = GetBucket(path);
            byBucket.TryGetValue(bucket, out var stat);

            if (!isUsed && isPrunable)
            {
                long sz = File.Exists(path) ? new FileInfo(path).Length : 0;
                deleteList.Add(path);
                deleteBytes += sz;
                stat.delCount++;
                stat.delBytes += sz;
            }
            else
            {
                keepList.Add(path);
                stat.keepCount++;
            }
            byBucket[bucket] = stat;
        }
    }

    private static void AddRenderPipelineRoots(HashSet<string> roots)
    {
        if (GraphicsSettings.defaultRenderPipeline != null)
        {
            string p = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            if (!string.IsNullOrEmpty(p)) roots.Add(p);
        }
        int levels = QualitySettings.count;
        for (int i = 0; i < levels; i++)
        {
            var rp = QualitySettings.GetRenderPipelineAssetAt(i);
            if (rp != null)
            {
                string p = AssetDatabase.GetAssetPath(rp);
                if (!string.IsNullOrEmpty(p)) roots.Add(p);
            }
        }
    }

    private static void AddFolderAssetRoots(string folder, HashSet<string> roots)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
                roots.Add(p);
        }
    }

    private static string GetBucket(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length >= 3) return $"{parts[0]}/{parts[1]}/{parts[2]}";
        if (parts.Length >= 2) return $"{parts[0]}/{parts[1]}";
        return path;
    }

    private static void PruneEmptyAssetDirectories(string rootDir)
    {
        foreach (string sub in Directory.GetDirectories(rootDir))
        {
            PruneEmptyAssetDirectories(sub);
            bool hasFiles = Directory.GetFiles(sub).Any(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
            bool hasDirs = Directory.GetDirectories(sub).Length > 0;
            if (!hasFiles && !hasDirs)
            {
                string rel = sub.Replace('\\', '/');
                AssetDatabase.DeleteAsset(rel);
            }
        }
    }

    public static string CheckSceneIntegrity(string scenePath)
    {
        var scene = AutoDevVerifier.EnsureSceneOpen(scenePath);
        if (!scene.IsValid()) return $"Sahne acilamadi: {scenePath}";

        var issues = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missingScripts > 0)
                    issues.Add($"Missing script ({missingScripts}) on {t.name}");

                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null)
                    issues.Add($"Null Mesh on {t.name}");

                var r = t.GetComponent<Renderer>();
                if (r != null && !(r is ParticleSystemRenderer))
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null)
                            issues.Add($"Null Material on {t.name}");
                    }
                }
            }
        }
        return issues.Count == 0 ? "" : string.Join("\n", issues.Take(20));
    }

    private static string FormatReport(
        List<string> keepList,
        List<string> deleteList,
        long deleteBytes,
        Dictionary<string, (int delCount, long delBytes, int keepCount)> byBucket,
        bool applied)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== ProjectCleanupAuditor ({(applied ? "APPLIED" : "DRY-RUN")}) ===");
        sb.AppendLine($"Keep total: {keepList.Count} files | Delete total: {deleteList.Count} files ({deleteBytes / (1024f * 1024f):F2} MB)");
        sb.AppendLine();
        sb.AppendLine("--- Breakdown by Bucket ---");
        foreach (var kv in byBucket.OrderByDescending(k => k.Value.delBytes).ThenBy(k => k.Key))
        {
            if (kv.Value.delCount == 0 && kv.Value.keepCount == 0) continue;
            sb.AppendLine($"{kv.Key}: delete={kv.Value.delCount} ({kv.Value.delBytes / (1024f * 1024f):F2} MB), keep={kv.Value.keepCount}");
        }
        sb.AppendLine();
        sb.AppendLine("--- Sample Kept Files Outside Scripts ---");
        foreach (string k in keepList.Where(p => !p.StartsWith("Assets/_GameAssets/Scripts/", StringComparison.OrdinalIgnoreCase)).Take(40))
            sb.AppendLine("  KEEP: " + k);
        return sb.ToString();
    }

    public static void BuildWin64FromCommandLine()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(AutoDevVerifier.DefaultScenePath, true)
        };

        string outDir = Path.GetFullPath("Builds/Win64");
        Directory.CreateDirectory(outDir);
        string exePath = Path.Combine(outDir, "3D CivCiv Game.exe");

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { AutoDevVerifier.DefaultScenePath },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;
        string msg = $"Build result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}, sizeMB={summary.totalSize / (1024f * 1024f):F2}, path={exePath}";
        Directory.CreateDirectory(".autodev");
        File.WriteAllText(".autodev/build_report.txt", msg + "\n", new UTF8Encoding(false));
        Debug.Log("[ProjectCleanupAuditor] " + msg);
        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    public static void CapturePortfolioScreenshots()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Directory.CreateDirectory("docs");

        // Edit Mode screenshot sirasinda Settings popup ve Gameplay EndGamePanel gecici gizlenir
        SettingsUI settings = UnityEngine.Object.FindAnyObjectByType<SettingsUI>();
        GameObject settingsPop = settings != null ? settings.transform.Find("SettingPopUpUI")?.gameObject : null;
        if (settingsPop == null && settings != null)
        {
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (tr.name == "SettingPopUpUI") { settingsPop = tr.gameObject; break; }
            }
        }
        bool origSettingsPop = settingsPop != null && settingsPop.activeSelf;
        if (settingsPop != null) settingsPop.SetActive(false);

        StartMenuUI startMenu = UnityEngine.Object.FindAnyObjectByType<StartMenuUI>();
        if (startMenu != null)
        {
            startMenu.ShowMenu();
            Time.timeScale = 1f;
            AutoDevVerifier.CaptureCameraPng("docs/start_menu.png", out _);

            if (startMenu.MenuRoot != null) startMenu.MenuRoot.SetActive(false);
            if (startMenu.ControlsHintBar != null) startMenu.ControlsHintBar.SetActive(true);
            AutoDevVerifier.CaptureCameraPng("docs/gameplay_hud.png", out _);

            startMenu.ShowMenu();
            Time.timeScale = 1f;
        }
        else
        {
            AutoDevVerifier.CaptureCameraPng("docs/gameplay_hud.png", out _);
        }

        if (settingsPop != null) settingsPop.SetActive(origSettingsPop);
    }
}
