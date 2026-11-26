using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class NightAgentMenu
{
    private const string DataFolder = "Assets/_GameAssets/Data";
    private const string PlayerStatsAssetPath = "Assets/_GameAssets/Data/DefaultPlayerStats.asset";

    // =========================================================================
    // FAZ 1: Veri Mimarisi ve Dash Sistemi
    // =========================================================================

    [MenuItem("Tools/Night Agent/Faz 1: Veri Mimarisi ve Dash Sistemi/Uygula")]
    public static void ApplyPhase1()
    {
        EnsureDataFolderExists();

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
            GameObject playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObj.name = "NightAgent_Player";
            playerObj.transform.position = new Vector3(0f, 1.5f, 0f);
            Undo.RegisterCreatedObjectUndo(playerObj, "Create NightAgent_Player");
            player = playerObj.AddComponent<PlayerController>();
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Apply Phase 1");
        }

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
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Night Agent] Faz 1 (Veri Mimarisi ve Dash Sistemi) sahneye uygulandı.");
    }

    [MenuItem("Tools/Night Agent/Faz 1: Veri Mimarisi ve Dash Sistemi/Geri çek")]
    public static void RevertPhase1()
    {
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

            if (player.gameObject.name == "NightAgent_Player")
            {
                Undo.DestroyObjectImmediate(player.gameObject);
            }
            else
            {
                EditorUtility.SetDirty(player);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
            "Sahne Değişiklikleri:\n" +
            "- Assets/_GameAssets/Data/DefaultPlayerStats.asset oluşturulur.\n" +
            "- Sahnedeki PlayerController nesnesine DashController eklenir ve SO referansı bağlanır.\n\n" +
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
        CoinManager coinManager = Object.FindAnyObjectByType<CoinManager>();
        if (coinManager == null)
        {
            GameObject coinMgrObj = new GameObject("NightAgent_CoinManager");
            Undo.RegisterCreatedObjectUndo(coinMgrObj, "Create NightAgent_CoinManager");
            coinManager = coinMgrObj.AddComponent<CoinManager>();
        }
        EditorUtility.SetDirty(coinManager);

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            GameObject gameMgrObj = new GameObject("NightAgent_GameManager");
            Undo.RegisterCreatedObjectUndo(gameMgrObj, "Create NightAgent_GameManager");
            gameManager = gameMgrObj.AddComponent<GameManager>();
        }
        EditorUtility.SetDirty(gameManager);

        ClockUI clockUI = Object.FindAnyObjectByType<ClockUI>();
        if (clockUI != null)
        {
            EditorUtility.SetDirty(clockUI);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Night Agent] Faz 2 (Çekirdek Oyun Döngüsü ve Skor Yönetimi) sahneye uygulandı.");
    }

    [MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi/Geri çek")]
    public static void RevertPhase2()
    {
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

        CoinManager[] coinManagers = Object.FindObjectsByType<CoinManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cm in coinManagers)
        {
            if (cm != null && cm.gameObject.name == "NightAgent_CoinManager")
            {
                Undo.DestroyObjectImmediate(cm.gameObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
            "Sahne Değişiklikleri:\n" +
            "- Sahneye GameManager (ve eksikse CoinManager) yöneticisi eklenir.\n\n" +
            "Nasıl Test Edilir:\n" +
            "- Play modunda tüm altınları topladığınızda oyunun Victory durumuna geçtiğini, HighScore kaydedildiğini ve süre 00:00 olduğunda GameOver tetiklendiğini gözlemleyin.",
            "Tamam");
    }

    private static void EnsureDataFolderExists()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_GameAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "_GameAssets");
            }
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        }
    }
}
