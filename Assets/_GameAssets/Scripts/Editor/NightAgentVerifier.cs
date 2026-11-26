using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class NightAgentVerifier
{
    public const string OutputDir = "TestScreenshots";
    public const string HierarchySnapshotPath = "TestScreenshots/scene_hierarchy.txt";

    [MenuItem("Tools/Night Agent/Sahne Özeti ve Doğrulama Raporu Üret")]
    public static void ExportSceneHierarchySnapshotMenu()
    {
        NightAgentMenu.EnsureDemoSceneOpen();
        ExportSceneHierarchySnapshot();
        Debug.Log($"[Night Agent] Sahne hiyerarşisi özeti '{HierarchySnapshotPath}' dosyasına yazıldı.");
    }

    public static void ExportSceneHierarchySnapshot()
    {
        EnsureOutputDirectory();

        Scene scene = SceneManager.GetActiveScene();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# Aktif Sahne Özeti: {scene.name} ({scene.path})");
        sb.AppendLine($"Tarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            sb.AppendLine($"[Player] GameObject: '{player.gameObject.name}' Pos: {player.transform.position}");
            MonoBehaviour[] comps = player.GetComponents<MonoBehaviour>();
            sb.Append("  Bileşenler: ");
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    sb.Append(comps[i].GetType().Name + (i < comps.Length - 1 ? ", " : ""));
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("[Player] BULUNAMADI!");
        }

        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            sb.AppendLine($"[Canvas] '{c.gameObject.name}' (RenderMode: {c.renderMode})");
            foreach (Transform child in c.transform)
            {
                sb.AppendLine($"  - UI Child: '{child.name}' (Active: {child.gameObject.activeSelf})");
            }
        }

        CoinCollect[] coins = UnityEngine.Object.FindObjectsByType<CoinCollect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"[Collectables] CoinCollect Sayısı: {(coins != null ? coins.Length : 0)}");

        sb.AppendLine("[Managers]");
        sb.AppendLine($"  - CoinManager: {(UnityEngine.Object.FindAnyObjectByType<CoinManager>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - GameManager: {(UnityEngine.Object.FindAnyObjectByType<GameManager>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - ClockUI: {(UnityEngine.Object.FindAnyObjectByType<ClockUI>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - SettingsUI: {(UnityEngine.Object.FindAnyObjectByType<SettingsUI>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - GameplayHUD: {(UnityEngine.Object.FindAnyObjectByType<GameplayHUD>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - HealthUI: {(UnityEngine.Object.FindAnyObjectByType<HealthUI>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - ComboManager: {(UnityEngine.Object.FindAnyObjectByType<ComboManager>() != null ? "VAR" : "YOK")}");
        sb.AppendLine($"  - ComboUI: {(UnityEngine.Object.FindAnyObjectByType<ComboUI>() != null ? "VAR" : "YOK")}");

        File.WriteAllText(HierarchySnapshotPath, sb.ToString(), Encoding.UTF8);
    }

    public static bool VerifyPhase(int phaseNo, out string report)
    {
        EnsureOutputDirectory();
        NightAgentMenu.ApplyPhaseByNumber(phaseNo);
        ExportSceneHierarchySnapshot();

        StringBuilder sb = new StringBuilder();
        bool passed = true;

        switch (phaseNo)
        {
            case 1:
                passed = VerifyPhase1Internal(sb);
                break;
            case 2:
                passed = VerifyPhase2Internal(sb);
                break;
            default:
                sb.AppendLine($"Faz {phaseNo} için özel doğrulama kuralı henüz tanımlanmadı.");
                break;
        }

        string screenshotPath = $"{OutputDir}/faz{phaseNo}_sonucu.png";
        bool shotOk = CaptureScenePreviewToPng(screenshotPath);
        sb.AppendLine($"Screenshot: {(shotOk ? screenshotPath : "FAILED")}");

        report = $"{(passed ? "PASS" : "FAIL")} - Faz {phaseNo}\n{sb}";
        return passed;
    }

    private static bool VerifyPhase1Internal(StringBuilder sb)
    {
        bool ok = true;

        PlayerStatsSO stats = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(NightAgentMenu.PlayerStatsAssetPath);
        if (stats == null)
        {
            sb.AppendLine("FAIL: DefaultPlayerStats.asset bulunamadı.");
            ok = false;
        }
        else
        {
            sb.AppendLine($"OK: DefaultPlayerStats.asset mevcut (MoveSpeed={stats.MoveSpeed}, DashForce={stats.DashForce}).");
        }

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            sb.AppendLine("FAIL: Demo.unity içinde PlayerController bulunamadı.");
            return false;
        }

        DashController dash = player.GetComponent<DashController>();
        if (dash == null)
        {
            sb.AppendLine("FAIL: PlayerController üzerinde DashController bileşeni yok.");
            ok = false;
        }
        else
        {
            sb.AppendLine($"OK: '{player.gameObject.name}' üzerinde DashController bağlı.");
        }

        return ok;
    }

    private static bool VerifyPhase2Internal(StringBuilder sb)
    {
        bool ok = true;

        CoinManager coinMgr = UnityEngine.Object.FindAnyObjectByType<CoinManager>();
        if (coinMgr == null)
        {
            sb.AppendLine("FAIL: Demo.unity içinde CoinManager bulunamadı.");
            ok = false;
        }
        else
        {
            sb.AppendLine("OK: CoinManager sahnede mevcut.");
        }

        GameManager gameMgr = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gameMgr == null)
        {
            sb.AppendLine("FAIL: Demo.unity içinde GameManager bulunamadı.");
            ok = false;
        }
        else
        {
            sb.AppendLine($"OK: GameManager sahnede mevcut (State={gameMgr.CurrentState}).");
        }

        ClockUI clockUI = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
        if (clockUI == null)
        {
            sb.AppendLine("WARN: ClockUI sahnede bulunamadı.");
        }
        else
        {
            sb.AppendLine("OK: ClockUI sahnede mevcut.");
        }

        return ok;
    }

    public static bool CaptureScenePreviewToPng(string filePath)
    {
        try
        {
            EnsureOutputDirectory();

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
            if (cam == null)
            {
                return false;
            }

            const int width = 1280;
            const int height = 720;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevCamRt = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevCamRt;
            RenderTexture.active = prevActive;

            byte[] pngBytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, pngBytes);

            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Night Agent] Ekran görüntüsü alınamadı: {ex.Message}");
            return false;
        }
    }

    private static void EnsureOutputDirectory()
    {
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }
    }
}
