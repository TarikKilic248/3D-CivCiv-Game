using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;

public static class CivCivAutoSetup
{
    [MenuItem("Tools/CivCiv/Kamera ve Karakteri Otomatik Kur ve Bağla %#k")]
    public static void SetupAll()
    {
        // 1. Sahnede Player nesnesini bul
        PlayerController playerController = Object.FindAnyObjectByType<PlayerController>();
        GameObject playerObj = null;

        if (playerController != null)
        {
            playerObj = playerController.gameObject;
        }
        else
        {
            var tagPlayer = GameObject.FindWithTag("Player");
            if (tagPlayer != null)
            {
                playerObj = tagPlayer;
            }
            else
            {
                // Sahnede 'Player' içeren nesne ara
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                {
                    if (go.name.ToLower().Contains("player") || go.name.ToLower().Contains("character") || go.name.ToLower().Contains("civciv"))
                    {
                        playerObj = go;
                        break;
                    }
                }
            }
        }

        if (playerObj == null)
        {
            Debug.LogWarning("[CivCiv AutoSetup] Sahnede Player nesnesi bulunamadı. Lütfen hiyerarşideki karakterinizi seçin.");
            if (Selection.activeGameObject != null)
            {
                playerObj = Selection.activeGameObject;
                Debug.Log($"[CivCiv AutoSetup] Seçili nesne '{playerObj.name}' Player olarak kullanılıyor.");
            }
            else
            {
                EditorUtility.DisplayDialog("CivCiv Kurulum", "Sahnede bir karakter bulunamadı. Lütfen Hierarchy'de karakterinizi seçip menüyü tekrar çalıştırın.", "Tamam");
                return;
            }
        }

        Undo.RegisterFullObjectHierarchyUndo(playerObj, "CivCiv Auto Setup");

        // 2. PlayerController ve Temel Fizik Bileşenleri
        if (playerController == null)
        {
            playerController = playerObj.GetComponent<PlayerController>() ?? playerObj.AddComponent<PlayerController>();
        }

        Rigidbody rb = playerObj.GetComponent<Rigidbody>() ?? playerObj.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Collider col = playerObj.GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider cap = playerObj.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            col = cap;
        }

        // 3. Orientation Alt Nesnesi
        Transform orientation = playerObj.transform.Find("Orientation");
        if (orientation == null)
        {
            GameObject orientObj = new GameObject("Orientation");
            orientObj.transform.SetParent(playerObj.transform);
            orientObj.transform.localPosition = Vector3.zero;
            orientObj.transform.localRotation = Quaternion.identity;
            orientation = orientObj.transform;
            Undo.RegisterCreatedObjectUndo(orientObj, "Create Orientation");
        }

        // 4. CameraPos_FPS Alt Nesnesi (Göz hizası)
        Transform fpsPos = playerObj.transform.Find("CameraPos_FPS");
        if (fpsPos == null)
        {
            GameObject fpsObj = new GameObject("CameraPos_FPS");
            fpsObj.transform.SetParent(playerObj.transform);
            fpsObj.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
            fpsObj.transform.localRotation = Quaternion.identity;
            fpsPos = fpsObj.transform;
            Undo.RegisterCreatedObjectUndo(fpsObj, "Create CameraPos_FPS");
        }

        // 5. PlayerVisual (3D Model / Mesh) Tespiti
        Transform playerVisual = null;
        foreach (Transform child in playerObj.transform)
        {
            string cName = child.name.ToLower();
            if (cName.Contains("visual") || cName.Contains("model") || cName.Contains("chr") || cName.Contains("mesh"))
            {
                playerVisual = child;
                break;
            }
        }

        if (playerVisual == null)
        {
            var smr = playerObj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) playerVisual = smr.transform;
            else
            {
                var mr = playerObj.GetComponentInChildren<MeshRenderer>();
                if (mr != null) playerVisual = mr.transform;
            }
        }

        // 6. Main Camera üzerinde CinemachineBrain Kontrolü
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = Object.FindAnyObjectByType<Camera>();
        if (mainCam != null)
        {
            CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                mainCam.gameObject.AddComponent<CinemachineBrain>();
                Debug.Log("[CivCiv AutoSetup] Main Camera üzerine CinemachineBrain bileşeni eklendi.");
            }
        }

        // 7. CinemachineKameralarını Bul veya Sıfırdan Oluştur
        CinemachineCamera tpsCam = null;
        CinemachineCamera fpsCam = null;

        var allCams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        foreach (var c in allCams)
        {
            string n = c.name.ToLower();
            if (tpsCam == null && (n.Contains("tps") || n.Contains("third"))) tpsCam = c;
            else if (fpsCam == null && (n.Contains("fps") || n.Contains("first"))) fpsCam = c;
        }

        // TPS Kamerası yoksa oluştur
        if (tpsCam == null)
        {
            if (allCams.Length > 0 && allCams[0] != fpsCam)
            {
                tpsCam = allCams[0];
            }
            else
            {
                GameObject tpsObj = new GameObject("CM_TPS_Camera");
                tpsCam = tpsObj.AddComponent<CinemachineCamera>();
                tpsObj.transform.position = playerObj.transform.position + new Vector3(0f, 2.5f, -3.5f);
                Undo.RegisterCreatedObjectUndo(tpsObj, "Create CM_TPS_Camera");
            }
        }
        tpsCam.name = "CM_TPS_Camera";
        tpsCam.Target.TrackingTarget = playerObj.transform;
        tpsCam.Priority = 20;

        // FPS Kamerası yoksa oluştur
        if (fpsCam == null)
        {
            if (allCams.Length > 1 && allCams[1] != tpsCam)
            {
                fpsCam = allCams[1];
            }
            else
            {
                GameObject fpsObj = new GameObject("CM_FPS_Camera");
                fpsCam = fpsObj.AddComponent<CinemachineCamera>();
                fpsObj.transform.position = fpsPos.position;
                Undo.RegisterCreatedObjectUndo(fpsObj, "Create CM_FPS_Camera");
            }
        }
        fpsCam.name = "CM_FPS_Camera";
        fpsCam.Target.TrackingTarget = fpsPos;
        fpsCam.Priority = 10;

        // 8. ThirdPersonCameraController Referanslarını Bağla
        ThirdPersonCameraController camController = Object.FindAnyObjectByType<ThirdPersonCameraController>();
        if (camController == null)
        {
            camController = playerObj.AddComponent<ThirdPersonCameraController>();
        }

        SerializedObject soCam = new SerializedObject(camController);
        soCam.FindProperty("_playerTransform").objectReferenceValue = playerObj.transform;
        soCam.FindProperty("_orientationTransform").objectReferenceValue = orientation;
        if (playerVisual != null)
        {
            soCam.FindProperty("_playerVisualTransform").objectReferenceValue = playerVisual;
        }
        soCam.FindProperty("_tpsCamera").objectReferenceValue = tpsCam;
        soCam.FindProperty("_fpsCamera").objectReferenceValue = fpsCam;
        soCam.ApplyModifiedProperties();

        // 9. PlayerController Referanslarını Bağla
        SerializedObject soPlayer = new SerializedObject(playerController);
        soPlayer.FindProperty("_playerOrientationTransform").objectReferenceValue = orientation;
        soPlayer.FindProperty("_playerCollider").objectReferenceValue = col;
        soPlayer.ApplyModifiedProperties();

        EditorUtility.SetDirty(camController);
        EditorUtility.SetDirty(playerController);
        EditorUtility.SetDirty(playerObj);

        Debug.Log("<color=green><b>[CivCiv AutoSetup] Başarılı!</b></color> TPS Kamera, FPS Kamera, Orientation, Collider ve Karakter bağlantıları tamamlandı!");
        EditorUtility.DisplayDialog("CivCiv Kurulum", "Harika! Tüm kameralar, orientation ve karakter referansları başarıyla bağlandı ve kuruldu.", "Tamam");
    }

    [MenuItem("Tools/CivCiv/Settings UI ve Butonları Otomatik Kur ve Bağla %#u")]
    public static void SetupSettingsUI()
    {
        // 1. Sahnede Canvas ve SettingsUI nesnesini bul
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        GameObject settingsObj = null;

        // SettingsUI adında nesne ara
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go.name.Equals("SettingsUI", System.StringComparison.OrdinalIgnoreCase))
            {
                settingsObj = go;
                break;
            }
        }

        if (settingsObj == null)
        {
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Settings UI Kurulum", "Sahnede Canvas bulunamadı!", "Tamam");
                return;
            }

            // Canvas altında SettingsUI oluştur
            settingsObj = new GameObject("SettingsUI", typeof(RectTransform));
            settingsObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(settingsObj, "Create SettingsUI");
        }

        Undo.RegisterFullObjectHierarchyUndo(settingsObj, "Setup SettingsUI");

        // 2. SettingsUI Script Bileşenini Ekle veya Al
        SettingsUI settingsScript = settingsObj.GetComponent<SettingsUI>() ?? settingsObj.AddComponent<SettingsUI>();

        // 3. Eksik Popup Butonlarını Oluştur ve Referansları Bağla
        settingsScript.AutoResolveReferences();
        settingsScript.BuildMissingPopupButtons();
        settingsScript.AutoResolveReferences();

        EditorUtility.SetDirty(settingsScript);
        EditorUtility.SetDirty(settingsObj);

        Debug.Log("<color=green><b>[CivCiv AutoSetup] Başarılı!</b></color> Settings UI, Menu butonları, müzik ve ses bağlantıları tamamlandı!");
        EditorUtility.DisplayDialog("Settings UI Kurulum", "Harika! Settings UI ve tüm menü butonları (Açma butonu, Müzik, Ses, Kapatma) tam işlevsel şekilde kuruldu ve bağlandı!", "Tamam");
    }

    [MenuItem("Tools/CivCiv/Oyun Yöneticilerini ve PlayerStats SO Kur %#g")]
    public static void SetupCoreManagers()
    {
        const string folderPath = "Assets/_GameAssets/Data";
        const string assetPath = "Assets/_GameAssets/Data/DefaultPlayerStats.asset";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_GameAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "_GameAssets");
            }
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        }

        PlayerStatsSO statsSO = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(assetPath);
        if (statsSO == null)
        {
            statsSO = ScriptableObject.CreateInstance<PlayerStatsSO>();
            statsSO.ValidateStats();
            AssetDatabase.CreateAsset(statsSO, assetPath);
            AssetDatabase.SaveAssets();
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Setup PlayerStats & DashController");

            SerializedObject soPlayer = new SerializedObject(player);
            SerializedProperty playerStatsProp = soPlayer.FindProperty("_playerStats");
            if (playerStatsProp != null)
            {
                playerStatsProp.objectReferenceValue = statsSO;
                soPlayer.ApplyModifiedProperties();
            }

            DashController dash = player.GetComponent<DashController>() ?? Undo.AddComponent<DashController>(player.gameObject);
            SerializedObject soDash = new SerializedObject(dash);
            SerializedProperty dashStatsProp = soDash.FindProperty("_playerStats");
            if (dashStatsProp != null)
            {
                dashStatsProp.objectReferenceValue = statsSO;
                soDash.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(dash);
        }

        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(gmObj, "Create GameManager");
            gm = gmObj.AddComponent<GameManager>();
        }
        EditorUtility.SetDirty(gm);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            GameplayHUD hud = canvas.GetComponentInChildren<GameplayHUD>(true);
            if (hud == null)
            {
                GameObject hudObj = new GameObject("GameplayHUD", typeof(RectTransform));
                hudObj.transform.SetParent(canvas.transform, false);
                Undo.RegisterCreatedObjectUndo(hudObj, "Create GameplayHUD");
                hud = hudObj.AddComponent<GameplayHUD>();
            }
            EditorUtility.SetDirty(hud);
        }

        Debug.Log("<color=green><b>[CivCiv AutoSetup] Başarılı!</b></color> DefaultPlayerStats, DashController, GameManager ve GameplayHUD kuruldu!");
    }
}
