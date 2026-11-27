using System;
using UnityEditor;
using UnityEngine;

public static class Check_T003
{
    private const string DefaultDataFolder = "Assets/_GameAssets/Data";
    private const string DefaultStatsAssetPath = "Assets/_GameAssets/Data/DefaultPlayerStats.asset";

    [AutoDevCheck("T003")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        // 1. DefaultPlayerStats.asset varlığını sağla
        PlayerStatsSO defaultStats = EnsureDefaultStatsAsset(out string resolvedAssetPath);
        if (defaultStats == null)
        {
            return AutoDevCheckResult.Fail("DefaultPlayerStats.asset olusturulamadi veya yuklenemedi.");
        }

        // 2. Demo.unity sahnesindeki PlayerController'ı bul ve DashController ile DefaultPlayerStats bağla
        PlayerController scenePlayer = FindSceneComponent<PlayerController>();
        if (scenePlayer == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity icinde PlayerController bulunamadi.");
        }

        bool saveScene = false;

        // PlayerController üzerinde _playerStats bağlantısını sağla
        SerializedObject serializedPlayer = new SerializedObject(scenePlayer);
        SerializedProperty playerStatsProp = serializedPlayer.FindProperty("_playerStats");
        if (playerStatsProp != null && playerStatsProp.objectReferenceValue != defaultStats)
        {
            Undo.RecordObject(scenePlayer, "Wire DefaultPlayerStats to PlayerController");
            playerStatsProp.objectReferenceValue = defaultStats;
            serializedPlayer.ApplyModifiedProperties();
            EditorUtility.SetDirty(scenePlayer);
            saveScene = true;
        }

        // DashController bileşeninin sahnede oyuncu üzerinde varlığını sağla
        DashController sceneDash = scenePlayer.GetComponent<DashController>();
        if (sceneDash == null)
        {
            sceneDash = Undo.AddComponent<DashController>(scenePlayer.gameObject);
            saveScene = true;
        }

        SerializedObject serializedDash = new SerializedObject(sceneDash);
        SerializedProperty dashStatsProp = serializedDash.FindProperty("_playerStats");
        if (dashStatsProp != null && dashStatsProp.objectReferenceValue != defaultStats)
        {
            Undo.RecordObject(sceneDash, "Wire DefaultPlayerStats to DashController");
            dashStatsProp.objectReferenceValue = defaultStats;
            serializedDash.ApplyModifiedProperties();
            EditorUtility.SetDirty(sceneDash);
            saveScene = true;
        }

        // 3. Davranışsal Doğrulama: İzole test nesnesi ile ExecuteDash + Physics.Simulate 4 kare testi
        float originalTimeScale = Time.timeScale;
        GameManager sceneGameManager = FindSceneComponent<GameManager>();
        GameState originalGameState = sceneGameManager != null ? sceneGameManager.CurrentState : GameState.Playing;

        GameObject testGroundObj = null;
        GameObject testPlayerObj = null;
        PhysicsMaterial frictionlessMat = null;

        try
        {
            Time.timeScale = 1f;
            if (sceneGameManager != null && GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            frictionlessMat = new PhysicsMaterial("T003_Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };

            testGroundObj = new GameObject("T003_TestGround");
            testGroundObj.transform.position = new Vector3(0f, 600f, 0f);
            BoxCollider groundCol = testGroundObj.AddComponent<BoxCollider>();
            groundCol.size = new Vector3(50f, 1f, 50f);
            groundCol.sharedMaterial = frictionlessMat;

            testPlayerObj = new GameObject("T003_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 601.52f, 0f);
            CapsuleCollider playerCol = testPlayerObj.AddComponent<CapsuleCollider>();
            playerCol.height = 2f;
            playerCol.radius = 0.4f;
            playerCol.center = Vector3.zero;
            playerCol.sharedMaterial = frictionlessMat;

            Rigidbody testRb = testPlayerObj.AddComponent<Rigidbody>();
            testRb.mass = 1f;

            StateController testState = testPlayerObj.AddComponent<StateController>();
            PlayerController testPlayer = testPlayerObj.AddComponent<PlayerController>();
            DashController testDash = testPlayerObj.AddComponent<DashController>();

            AutoDevVerifier.InvokeLifecycle(testPlayer, "Awake");
            AutoDevVerifier.InvokeLifecycle(testDash, "Awake");

            testPlayer.ApplyStatsFromSO(defaultStats);
            testDash.ApplyStatsFromSO(defaultStats);

            AutoDevVerifier.InvokeLifecycle(testPlayer, "Start");
            AutoDevVerifier.InvokeLifecycle(testDash, "Start");

            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();

            // 3A: Başlangıç Stamina ve Durum Doğrulaması
            if (testDash.IsDashing)
            {
                return AutoDevCheckResult.Fail("Baslangicta IsDashing == false olmaliydi.");
            }

            if (!Mathf.Approximately(testDash.CurrentStamina, defaultStats.maxStamina) ||
                !Mathf.Approximately(testDash.MaxStamina, defaultStats.maxStamina))
            {
                return AutoDevCheckResult.Fail($"Baslangic stamina hatali. Beklenen: {defaultStats.maxStamina}, Alinan: {testDash.CurrentStamina}/{testDash.MaxStamina}");
            }

            float reportedCurrent = -1f;
            float reportedMax = -1f;
            int staminaEventCalls = 0;
            Action<float, float> staminaHandler = (curr, max) =>
            {
                reportedCurrent = curr;
                reportedMax = max;
                staminaEventCalls++;
            };
            testDash.OnStaminaChanged += staminaHandler;

            // 3B: ExecuteDash() Çağrısı ve Tetiklenme Doğrulaması
            Vector3 startPos = testPlayerObj.transform.position;
            bool dashStarted = testDash.ExecuteDash();
            if (!dashStarted)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail("ExecuteDash() cagrisi basarisiz oldu (false dondurdu).");
            }

            if (!testDash.IsDashing)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail("ExecuteDash sonrasi IsDashing == true olmaliydi.");
            }

            float expectedStaminaAfterDash = defaultStats.maxStamina - defaultStats.dashStaminaCost;
            if (Mathf.Abs(testDash.CurrentStamina - expectedStaminaAfterDash) > 0.01f)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail($"Dash sonrasi Stamina tuketimi hatali. Beklenen: {expectedStaminaAfterDash}, Alinan: {testDash.CurrentStamina}");
            }

            if (staminaEventCalls != 1 || Mathf.Abs(reportedCurrent - expectedStaminaAfterDash) > 0.01f || Mathf.Abs(reportedMax - defaultStats.maxStamina) > 0.01f)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail($"OnStaminaChanged event tetiklenme veya deger hatasi! Cagri={staminaEventCalls}, Current={reportedCurrent}, Max={reportedMax}");
            }

            if (!testPlayer.IsExternalMovementOverridden)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail("Dash basladiginda PlayerController.IsExternalMovementOverridden == true olmaliydi.");
            }

            if (testState.GetCurrentState() != PlayerState.Dash)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail($"Dash basladiginda StateController durumu PlayerState.Dash olmaliydi! Simdiki: {testState.GetCurrentState()}");
            }

            // 3C: PlayerController.FixedUpdate + DashController.FixedUpdate + Physics.Simulate ile 4 Kare Simülasyon
            AutoDevVerifier.SimulatePhysicsSteps(4, 0.02f, () =>
            {
                AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
                AutoDevVerifier.InvokeLifecycle(testDash, "FixedUpdate");
            });

            // Yatay hızın MaxSpeed'in belirgin üstünde korunduğunu doğrula
            Vector3 flatVel = new Vector3(testRb.linearVelocity.x, 0f, testRb.linearVelocity.z);
            float horizontalSpeed = flatVel.magnitude;

            if (horizontalSpeed < defaultStats.maxSpeed * 1.5f || horizontalSpeed > defaultStats.dashSpeed + 0.5f)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail($"Dash sirasinda yatay hiz MaxSpeed'in ({defaultStats.maxSpeed}) belirgin ustunde korunamadi! Alinan: {horizontalSpeed:F2}, Beklenen DashSpeed~{defaultStats.dashSpeed:F2}");
            }

            // Karakterin ilerlediğini doğrula
            float distanceMoved = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(testPlayerObj.transform.position.x, 0f, testPlayerObj.transform.position.z));
            if (distanceMoved < 0.3f)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail($"4 kare simülasyon sonrasi karakter ilerlemedi! Gidilen mesafe: {distanceMoved:F2}");
            }

            // Cooldown süresi dolmadan ikinci bir dash başlatılamamalı
            bool dashWhileActive = testDash.ExecuteDash();
            if (dashWhileActive)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail("IsDashing/Cooldown devam ederken ikinci bir ExecuteDash() calismamaliydi!");
            }

            // 3D: Dash bitimi, override kapanışı, stamina yenilenmesi ve yetersiz stamina koruması
            testDash.Step(defaultStats.dashDuration + 0.05f);
            if (testDash.IsDashing || testPlayer.IsExternalMovementOverridden || testState.GetCurrentState() == PlayerState.Dash)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail(
                    $"Dash suresi doldugunda IsDashing/Override/Dash durumu kapanmadi! IsDashing={testDash.IsDashing}, Override={testPlayer.IsExternalMovementOverridden}, State={testState.GetCurrentState()}");
            }

            int eventCountBeforeRegen = staminaEventCalls;
            testDash.Step(defaultStats.staminaRegenDelay + 0.05f);
            testDash.Step(0.5f);
            if (testDash.CurrentStamina <= expectedStaminaAfterDash || staminaEventCalls <= eventCountBeforeRegen)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail(
                    $"Stamina yenilenmesi veya OnStaminaChanged event'i calismadi! Stamina={testDash.CurrentStamina:F2}, Onceki={expectedStaminaAfterDash:F2}, Events={staminaEventCalls}");
            }

            // Cooldown temizlendikten sonra yetersiz stamina ile dash engellenmeli
            testDash.Step(defaultStats.dashCooldown + 0.1f);
            testDash.SetStamina(Mathf.Max(0f, defaultStats.dashStaminaCost - 1f));
            bool dashWithLowStamina = testDash.ExecuteDash();
            if (dashWithLowStamina)
            {
                testDash.OnStaminaChanged -= staminaHandler;
                return AutoDevCheckResult.Fail("Yetersiz stamina varken ExecuteDash() false dondurmeliydi!");
            }

            testDash.OnStaminaChanged -= staminaHandler;
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            if (sceneGameManager != null && GameManager.Instance != null && GameManager.Instance.CurrentState != originalGameState)
            {
                GameManager.Instance.SetState(originalGameState);
            }
            if (testPlayerObj != null) UnityEngine.Object.DestroyImmediate(testPlayerObj);
            if (testGroundObj != null) UnityEngine.Object.DestroyImmediate(testGroundObj);
            if (frictionlessMat != null) UnityEngine.Object.DestroyImmediate(frictionlessMat);
        }

        // 4. Demo.unity sahnesindeki kalıcı bileşenlerin doğrulanması
        if (scenePlayer.GetComponent<DashController>() == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity sahnesindeki oyuncu nesnesine DashController bileseni baglanamadi.");
        }

        if (scenePlayer.PlayerStats == null || scenePlayer.GetComponent<DashController>().PlayerStats == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity sahnesindeki PlayerController veya DashController uzerine DefaultPlayerStats atanamadi.");
        }

        return AutoDevCheckResult.Pass(
            "T003 basariyla dogrulandi: PlayerState.Dash, DashController stamina tuketimi, OnStaminaChanged event'i, 4 kare Physics.Simulate hiz override korumasi ve Demo.unity baglantilari tamamlandi.",
            $"ScenePlayer={scenePlayer.name}, DashSpeed={defaultStats.dashSpeed}, MaxSpeed={defaultStats.maxSpeed}, DashCost={defaultStats.dashStaminaCost}",
            saveScene
        );
    }

    private static PlayerStatsSO EnsureDefaultStatsAsset(out string assetPath)
    {
        assetPath = DefaultStatsAssetPath;
        PlayerStatsSO existing = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(DefaultStatsAssetPath);
        if (existing != null)
        {
            return existing;
        }

        string[] guids = AssetDatabase.FindAssets("DefaultPlayerStats t:PlayerStatsSO");
        foreach (string guid in guids)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(candidatePath) && candidatePath.StartsWith("Assets/_GameAssets/", StringComparison.OrdinalIgnoreCase))
            {
                PlayerStatsSO found = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(candidatePath);
                if (found != null)
                {
                    assetPath = candidatePath;
                    return found;
                }
            }
        }

        if (!AssetDatabase.IsValidFolder(DefaultDataFolder))
        {
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        }

        PlayerStatsSO created = ScriptableObject.CreateInstance<PlayerStatsSO>();
        AssetDatabase.CreateAsset(created, DefaultStatsAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(DefaultStatsAssetPath);
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T comp = UnityEngine.Object.FindAnyObjectByType<T>();
        if (comp != null)
            return comp;

        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return (all != null && all.Length > 0) ? all[0] : null;
    }
}
