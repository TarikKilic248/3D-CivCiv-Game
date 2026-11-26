using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class NightAgentMenu
{
    public const string DemoScenePath = "Assets/Synty/PolygonStarter/Scenes/Demo.unity";
    public const string DataFolder = "Assets/_GameAssets/Data";
    public const string PrefabsFolder = "Assets/_GameAssets/Prefabs";
    public const string PlayerStatsAssetPath = "Assets/_GameAssets/Data/DefaultPlayerStats.asset";

    public static Scene EnsureDemoSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path != DemoScenePath && System.IO.File.Exists(DemoScenePath))
        {
            if (!EditorApplication.isPlaying)
            {
                active = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            }
        }
        return active;
    }

    public static void SaveDemoScene()
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

    public static bool ApplyPhaseByNumber(int phaseNo)
    {
        EnsureDemoSceneOpen();
        switch (phaseNo)
        {
            case 1:
                ApplyPhase1();
                return true;
            case 2:
                ApplyPhase2();
                return true;
            default:
                Debug.LogWarning($"[Night Agent] Faz {phaseNo} için henüz Apply metodu tanımlanmadı.");
                return false;
        }
    }

    public static bool RevertPhaseByNumber(int phaseNo)
    {
        EnsureDemoSceneOpen();
        switch (phaseNo)
        {
            case 1:
                RevertPhase1();
                return true;
            case 2:
                RevertPhase2();
                return true;
            default:
                Debug.LogWarning($"[Night Agent] Faz {phaseNo} için henüz Revert metodu tanımlanmadı.");
                return false;
        }
    }

    // =========================================================================
    // FAZ 1: Veri Mimarisi ve Dash Sistemi
    // =========================================================================

    [MenuItem("Tools/Night Agent/Faz 1: Veri Mimarisi ve Dash Sistemi/Uygula")]
    public static void ApplyPhase1()
    {
        EnsureDemoSceneOpen();
        EnsureFoldersExist();

        PlayerStatsSO statsSO = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(PlayerStatsAssetPath);
        if (statsSO == null)
        {
            statsSO = ScriptableObject.CreateInstance<PlayerStatsSO>();
            statsSO.ValidateStats();
            AssetDatabase.CreateAsset(statsSO, PlayerStatsAssetPath);
            AssetDatabase.SaveAssets();
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[Night Agent] Sahnede PlayerController bulunamadı!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Apply Phase 1");

        SerializedObject soPlayer = new SerializedObject(player);
        SerializedProperty playerStatsProp = soPlayer.FindProperty("_playerStats");
        if (playerStatsProp != null)
        {
            playerStatsProp.objectReferenceValue = statsSO;
            soPlayer.ApplyModifiedProperties();
        }
        player.ApplyStatsFromSO(statsSO);

        DashController dash = player.GetComponent<DashController>();
        if (dash == null)
        {
            dash = Undo.AddComponent<DashController>(player.gameObject);
        }

        SerializedObject soDash = new SerializedObject(dash);
        SerializedProperty dashStatsProp = soDash.FindProperty("_playerStats");
        if (dashStatsProp != null)
        {
            dashStatsProp.objectReferenceValue = statsSO;
            soDash.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(dash);
        SaveDemoScene();
        NightAgentVerifier.ExportSceneHierarchySnapshot();
        Debug.Log("[Night Agent] Faz 1 (Veri Mimarisi ve Dash Sistemi) Demo.unity sahnesine uygulandı ve kaydedildi.");
    }

    [MenuItem("Tools/Night Agent/Faz 1: Veri Mimarisi ve Dash Sistemi/Geri çek")]
    public static void RevertPhase1()
    {
        EnsureDemoSceneOpen();
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            DashController dash = player.GetComponent<DashController>();
            if (dash != null)
            {
                Undo.DestroyObjectImmediate(dash);
            }

            SerializedObject soPlayer = new SerializedObject(player);
            SerializedProperty playerStatsProp = soPlayer.FindProperty("_playerStats");
            if (playerStatsProp != null)
            {
                playerStatsProp.objectReferenceValue = null;
                soPlayer.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(player);
        }

        SaveDemoScene();
        NightAgentVerifier.ExportSceneHierarchySnapshot();
        Debug.Log("[Night Agent] Faz 1 (Veri Mimarisi ve Dash Sistemi) sahneden geri çekildi.");
    }

    [MenuItem("Tools/Night Agent/Faz 1: Veri Mimarisi ve Dash Sistemi/Detay")]
    public static void DetailPhase1()
    {
        EditorUtility.DisplayDialog(
            "Faz 1: Veri Mimarisi ve Dash Sistemi",
            "Eklenen Scriptler:\n" +
            "- PlayerStatsSO.cs (ScriptableObject veri mimarisi)\n" +
            "- PlayerState.cs (Dash ve Glide durumları eklendi)\n" +
            "- PlayerController.cs (ApplyStatsFromSO ve hız çarpanı entegrasyonu)\n" +
            "- DashController.cs (İleri atılma ve Stamina tüketim/yenilenme sistemi)\n\n" +
            "Demo.unity Sahne Değişiklikleri:\n" +
            "- Assets/_GameAssets/Data/DefaultPlayerStats.asset oluşturulur.\n" +
            "- Sahnedeki gerçek PlayerController nesnesine DashController eklenir ve SO referansı bağlanıp sahne kaydedilir.\n\n" +
            "Nasıl Test Edilir:\n" +
            "- Play moduna geçip Left Shift tuşuna basarak karakterin baktığı yöne ileri atıldığını (Dash) ve Stamina değerinin azalıp zamanla yenilendiğini gözlemleyin.",
            "Tamam");
    }

    // =========================================================================
    // FAZ 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi
    // =========================================================================

    [MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi/Uygula")]
    public static void ApplyPhase2()
    {
        EnsureDemoSceneOpen();

        CoinManager coinManager = Object.FindAnyObjectByType<CoinManager>();
        if (coinManager == null)
        {
            GameObject coinMgrObj = new GameObject("CoinManager");
            Undo.RegisterCreatedObjectUndo(coinMgrObj, "Create CoinManager");
            coinManager = coinMgrObj.AddComponent<CoinManager>();
        }
        EditorUtility.SetDirty(coinManager);

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            GameObject gameMgrObj = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(gameMgrObj, "Create GameManager");
            gameManager = gameMgrObj.AddComponent<GameManager>();
        }
        EditorUtility.SetDirty(gameManager);

        ClockUI clockUI = Object.FindAnyObjectByType<ClockUI>();
        if (clockUI != null)
        {
            EditorUtility.SetDirty(clockUI);
        }

        SaveDemoScene();
        NightAgentVerifier.ExportSceneHierarchySnapshot();
        Debug.Log("[Night Agent] Faz 2 (Çekirdek Oyun Döngüsü ve Skor Yönetimi) Demo.unity sahnesine uygulandı ve kaydedildi.");
    }

    [MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi/Geri çek")]
    public static void RevertPhase2()
    {
        EnsureDemoSceneOpen();

        GameManager[] gameManagers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var gm in gameManagers)
        {
            if (gm == null) continue;
            if (gm.gameObject.name == "NightAgent_GameManager" || gm.gameObject.name == "GameManager")
            {
                Undo.DestroyObjectImmediate(gm.gameObject);
            }
            else
            {
                Undo.DestroyObjectImmediate(gm);
            }
        }

        SaveDemoScene();
        NightAgentVerifier.ExportSceneHierarchySnapshot();
        Debug.Log("[Night Agent] Faz 2 (Çekirdek Oyun Döngüsü ve Skor Yönetimi) sahneden geri çekildi.");
    }

    [MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi/Detay")]
    public static void DetailPhase2()
    {
        EditorUtility.DisplayDialog(
            "Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi",
            "Eklenen / Güncellenen Scriptler:\n" +
            "- GameState.cs (Playing, Paused, Victory, GameOver durumları)\n" +
            "- GameManager.cs (Merkezi oyun durumu, hedef altın kontrolü, Victory/GameOver ve RestartLevel yönetimi)\n" +
            "- ClockUI.cs (Süre bittiğinde OnTimerExpired ve GameOver tetikleme, AddBonusTime desteği)\n" +
            "- CoinManager.cs (PlayerPrefs 'CivCiv_HighScore' ile kalıcı yüksek skor kaydı)\n\n" +
            "Demo.unity Sahne Değişiklikleri:\n" +
            "- Sahneye GameManager (ve eksikse CoinManager) yöneticisi eklenir ve sahne kaydedilir.\n\n" +
            "Nasıl Test Edilir:\n" +
            "- Play modunda tüm altınları topladığınızda oyunun Victory durumuna geçtiğini, HighScore kaydedildiğini ve süre 00:00 olduğunda GameOver tetiklendiğini gözlemleyin.",
            "Tamam");
    }

    public static void EnsureFoldersExist()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_GameAssets"))
        {
            AssetDatabase.CreateFolder("Assets", "_GameAssets");
        }
        if (!AssetDatabase.IsValidFolder(DataFolder))
        {
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        }
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Prefabs");
        }
    }
}
