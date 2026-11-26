using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class AutoDevCheckAttribute : Attribute
{
    public string Id { get; }

    public AutoDevCheckAttribute(string id)
    {
        Id = id;
    }
}

public struct AutoDevCheckResult
{
    public bool Passed;
    public string Summary;
    public string Details;
    public bool SaveSceneOnPass;

    public static AutoDevCheckResult Pass(string summary, string details = "", bool saveScene = false)
    {
        return new AutoDevCheckResult
        {
            Passed = true,
            Summary = summary,
            Details = details,
            SaveSceneOnPass = saveScene
        };
    }

    public static AutoDevCheckResult Fail(string summary, string details = "")
    {
        return new AutoDevCheckResult
        {
            Passed = false,
            Summary = summary,
            Details = details,
            SaveSceneOnPass = false
        };
    }
}

public static class AutoDevVerifier
{
    public const string DefaultScenePath = "Assets/Synty/PolygonStarter/Scenes/Demo.unity";

    public static Scene EnsureSceneOpen(string scenePath)
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(scenePath)
            && active.path != scenePath && File.Exists(scenePath))
        {
            active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        return active;
    }

    public static void SaveActiveScene()
    {
        if (EditorApplication.isPlaying) return;
        Scene active = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(active);
        if (!string.IsNullOrEmpty(active.path))
        {
            EditorSceneManager.SaveScene(active);
        }
        AssetDatabase.SaveAssets();
    }

    public static void ReloadCleanScene(string scenePath)
    {
        if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }

    public static void InvokeLifecycle(MonoBehaviour target, string methodName)
    {
        if (target == null) return;
        MethodInfo mi = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        mi?.Invoke(target, null);
    }

    public static void SimulatePhysicsSteps(int steps, float fixedDeltaTime, Action onStep = null)
    {
        SimulationMode originalMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            for (int i = 0; i < steps; i++)
            {
                onStep?.Invoke();
                Physics.Simulate(fixedDeltaTime);
            }
        }
        finally
        {
            Physics.simulationMode = originalMode;
        }
    }

    public static AutoDevCheckResult RunCheck(string checkId, string scenePath)
    {
        EnsureSceneOpen(scenePath);

        MethodInfo targetMethod = FindCheckMethod(checkId);
        if (targetMethod == null)
        {
            return AutoDevCheckResult.Fail(
                $"'{checkId}' icin [AutoDevCheck(\"{checkId}\")] dogrulama metodu bulunamadi.",
                "Assets/_GameAssets/Scripts/Editor/AutoDev/Checks/ altinda ilgili davranissal dogrulama metodunu ekleyin.");
        }

        AutoDevCheckResult result;
        try
        {
            object ret = targetMethod.Invoke(null, null);
            result = ret is AutoDevCheckResult r
                ? r
                : AutoDevCheckResult.Fail($"'{checkId}' metodu AutoDevCheckResult dondurmedi.");
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            result = AutoDevCheckResult.Fail(
                $"Dogrulama istisnasi ({checkId}): {tie.InnerException.Message}",
                tie.InnerException.StackTrace);
        }
        catch (Exception ex)
        {
            result = AutoDevCheckResult.Fail($"Dogrulama hatasi ({checkId}): {ex.Message}", ex.StackTrace);
        }

        if (result.Passed && result.SaveSceneOnPass)
        {
            SaveActiveScene();
        }
        else
        {
            // Restore clean scene so physics simulations or failed setup never dirty Demo.unity
            ReloadCleanScene(scenePath);
        }

        ExportSceneHierarchySnapshot(AutoDevBridge.DefaultSnapshotPath);
        return result;
    }

    private static MethodInfo FindCheckMethod(string checkId)
    {
        foreach (MethodInfo mi in TypeCache.GetMethodsWithAttribute<AutoDevCheckAttribute>())
        {
            if (!mi.IsStatic || mi.GetParameters().Length != 0)
                continue;
            foreach (AutoDevCheckAttribute attr in mi.GetCustomAttributes<AutoDevCheckAttribute>())
            {
                if (string.Equals(attr.Id, checkId, StringComparison.OrdinalIgnoreCase))
                {
                    return mi;
                }
            }
        }
        return null;
    }

    [AutoDevCheck("SMOKE")]
    public static AutoDevCheckResult RunSmokeCheck()
    {
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            return AutoDevCheckResult.Fail("Aktif sahnede PlayerController bulunamadi.");
        }

        Camera cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            return AutoDevCheckResult.Fail("Aktif sahnede Camera bulunamadi.");
        }

        return AutoDevCheckResult.Pass(
            $"SMOKE PASS: '{player.gameObject.name}' ve kamera '{cam.gameObject.name}' aktif sahnede dogrulandi.");
    }

    public static string ExportSceneHierarchySnapshot(string outputPath)
    {
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Scene scene = SceneManager.GetActiveScene();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# Unity Scene Snapshot: {scene.name} ({scene.path})");

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            sb.Append($"[Player] '{player.gameObject.name}' @ {player.transform.position} | Components: ");
            MonoBehaviour[] comps = player.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                {
                    sb.Append(comps[i].GetType().Name + (i < comps.Length - 1 ? ", " : ""));
                }
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("[Player] NOT FOUND");
        }

        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas c in canvases)
        {
            sb.Append($"[Canvas] '{c.gameObject.name}' children: ");
            for (int i = 0; i < c.transform.childCount && i < 12; i++)
            {
                Transform child = c.transform.GetChild(i);
                sb.Append(child.name + (child.gameObject.activeSelf ? "" : "(off)"));
                if (i < c.transform.childCount - 1 && i < 11) sb.Append(", ");
            }
            sb.AppendLine();
        }

        CoinCollect[] coins = UnityEngine.Object.FindObjectsByType<CoinCollect>(FindObjectsInactive.Include);
        sb.AppendLine($"[Collectables] CoinCollect: {(coins != null ? coins.Length : 0)}");

        Camera mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        sb.AppendLine($"[Camera] {(mainCam != null ? mainCam.gameObject.name : "NONE")}");

        string content = sb.ToString();
        File.WriteAllText(outputPath, content, new UTF8Encoding(false));
        return content;
    }

    public static bool CaptureCameraPng(string outputPath, out string error)
    {
        error = "";
        try
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Camera cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                error = "Sahnede Camera bulunamadi.";
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

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
