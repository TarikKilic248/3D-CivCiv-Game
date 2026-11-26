using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Check_T001
{
    private const string DefaultDataFolder = "Assets/_GameAssets/Data";
    private const string DefaultStatsAssetPath = "Assets/_GameAssets/Data/DefaultPlayerStats.asset";

    [AutoDevCheck("T001")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        // 1. DefaultPlayerStats.asset varlığını idempotent olarak sağla
        PlayerStatsSO defaultStats = EnsureDefaultStatsAsset(out string resolvedAssetPath);
        if (defaultStats == null)
        {
            return AutoDevCheckResult.Fail("DefaultPlayerStats.asset olusturulamadi veya yuklenemedi.");
        }

        // 2. Demo.unity içindeki PlayerController'ın varlığını doğrula
        PlayerController scenePlayer = FindSceneComponent<PlayerController>();
        if (scenePlayer == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity icinde PlayerController bulunamadi.");
        }

        // 3. Davranışsal Doğrulamalar (izole test rig ile gerçek fizik/girdi/override/zıplama sınaması)
        float originalTimeScale = Time.timeScale;
        SettingsUI settingsUI = FindSceneComponent<SettingsUI>();
        SettingsUI originalSettingsInstance = SettingsUI.Instance;
        MethodInfo settingsInstanceSetter = typeof(SettingsUI)
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
            ?.GetSetMethod(true);
        bool originalSettingsOpen = false;
        FieldInfo settingsMenuOpenField = typeof(SettingsUI).GetField("_isMenuOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        if (settingsUI != null && settingsMenuOpenField != null)
        {
            originalSettingsOpen = (bool)settingsMenuOpenField.GetValue(settingsUI);
        }

        GameManager gameManager = FindSceneComponent<GameManager>();
        GameManager originalGameManagerInstance = GameManager.Instance;
        MethodInfo gameManagerInstanceSetter = typeof(GameManager)
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
            ?.GetSetMethod(true);
        GameState originalGameState = gameManager != null ? gameManager.CurrentState : GameState.Playing;
        bool globalsRestored = false;

        GameObject groundObj = null;
        GameObject testPlayerObj = null;
        GameObject targetJumpObj = null;
        PhysicsMaterial frictionlessMat = null;
        PlayerStatsSO customStats = null;

        try
        {
            // İzole test zemini (sürtünmesiz, sahne geometrisinden uzak y=500'de)
            frictionlessMat = new PhysicsMaterial("T001_Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };

            groundObj = new GameObject("T001_TestGround");
            groundObj.transform.position = new Vector3(0f, 500f, 0f);
            BoxCollider groundCol = groundObj.AddComponent<BoxCollider>();
            groundCol.size = new Vector3(40f, 1f, 40f);
            groundCol.sharedMaterial = frictionlessMat;

            testPlayerObj = new GameObject("T001_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            CapsuleCollider playerCol = testPlayerObj.AddComponent<CapsuleCollider>();
            playerCol.height = 2f;
            playerCol.radius = 0.4f;
            playerCol.center = Vector3.zero;
            playerCol.sharedMaterial = frictionlessMat;

            Rigidbody testRb = testPlayerObj.AddComponent<Rigidbody>();
            testRb.mass = 1f;
            PlayerController testPlayer = testPlayerObj.AddComponent<PlayerController>();

            AutoDevVerifier.InvokeLifecycle(testPlayer, "Awake");
            if (testPlayer.GetRigidbody() == null || testPlayer.GetRigidbody() != testRb)
            {
                return AutoDevCheckResult.Fail("PlayerController.GetRigidbody() beklenen Rigidbody bilesenini dondurmedi.");
            }

            // 3A. ApplyStatsFromSO doğrulaması
            customStats = ScriptableObject.CreateInstance<PlayerStatsSO>();
            customStats.moveSpeed = 62f;
            customStats.maxSpeed = 9.5f;
            customStats.groundDrag = 5.5f;
            customStats.jumpForce = 11f;
            customStats.coyoteTime = 0.22f;
            customStats.jumpBufferTime = 0.18f;

            testPlayer.ApplyStatsFromSO(customStats);
            if (!Mathf.Approximately(testPlayer.MoveSpeed, 62f)
                || !Mathf.Approximately(testPlayer.MaxSpeed, 9.5f)
                || !Mathf.Approximately(testPlayer.JumpForce, 11f)
                || !Mathf.Approximately(testPlayer.CoyoteTime, 0.22f)
                || !Mathf.Approximately(testPlayer.JumpBufferTime, 0.18f)
                || !Mathf.Approximately(testRb.linearDamping, 5.5f))
            {
                return AutoDevCheckResult.Fail(
                    "ApplyStatsFromSO(PlayerStatsSO) degerleri PlayerController/Rigidbody uzerine dogru aktarmadi.");
            }

            // DefaultPlayerStats uygula
            testPlayer.ApplyStatsFromSO(defaultStats);
            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();
            if (!testPlayer.IsGrounded)
            {
                return AutoDevCheckResult.Fail("Test oyuncusu zemin uzerindeyken IsGrounded == true olmaliydi.");
            }

            // 3B. Girdi kilidi doğrulaması (Time.timeScale == 0, SettingsUI.IsOpen, GameManager duraklatma/bitiş)
            if (gameManager != null)
            {
                if (GameManager.Instance == null)
                {
                    gameManagerInstanceSetter?.Invoke(null, new object[] { gameManager });
                }
                gameManager.SetState(GameState.Playing);
            }

            // B1: Time.timeScale == 0
            Time.timeScale = 0f;
            testRb.linearVelocity = Vector3.zero;
            testPlayer.ProcessInputs(1f, 1f, true, true, true);
            if (!testPlayer.IsInputBlocked()
                || !Mathf.Approximately(testPlayer.HorizontalInput, 0f)
                || !Mathf.Approximately(testPlayer.VerticalInput, 0f)
                || testPlayer.IsSliding
                || testRb.linearVelocity.y > 0.01f)
            {
                return AutoDevCheckResult.Fail("Time.timeScale == 0 iken PlayerController girdileri kilitlenmedi.");
            }
            Time.timeScale = 1f;

            // B2: SettingsUI.IsOpen == true (Time.timeScale == 1f iken bile kilitli olmalı)
            if (settingsUI != null && settingsMenuOpenField != null)
            {
                if (SettingsUI.Instance == null)
                {
                    settingsInstanceSetter?.Invoke(null, new object[] { settingsUI });
                }
                settingsMenuOpenField.SetValue(SettingsUI.Instance, true);
                if (!SettingsUI.IsOpen)
                {
                    return AutoDevCheckResult.Fail("SettingsUI.IsOpen test icin true yapilamadi.");
                }

                testRb.linearVelocity = Vector3.zero;
                testPlayer.ProcessInputs(1f, 0.5f, true, true, true);
                if (!testPlayer.IsInputBlocked()
                    || !Mathf.Approximately(testPlayer.HorizontalInput, 0f)
                    || !Mathf.Approximately(testPlayer.VerticalInput, 0f)
                    || testPlayer.IsSliding
                    || testRb.linearVelocity.y > 0.01f)
                {
                    return AutoDevCheckResult.Fail("SettingsUI.IsOpen == true iken PlayerController girdileri kilitlenmedi.");
                }

                settingsMenuOpenField.SetValue(SettingsUI.Instance, false);
            }

            // B3: GameManager Paused / GameOver / Victory
            if (gameManager != null)
            {
                gameManager.SetState(GameState.Paused);
                testPlayer.ProcessInputs(1f, 1f, true, true, true);
                if (!testPlayer.IsInputBlocked() || !Mathf.Approximately(testPlayer.HorizontalInput, 0f) || testPlayer.IsSliding)
                {
                    return AutoDevCheckResult.Fail("GameState.Paused iken PlayerController girdileri kilitlenmedi.");
                }

                gameManager.SetState(GameState.GameOver);
                testPlayer.ProcessInputs(1f, 1f, true, true, true);
                if (!testPlayer.IsInputBlocked() || !Mathf.Approximately(testPlayer.HorizontalInput, 0f) || testPlayer.IsSliding)
                {
                    return AutoDevCheckResult.Fail("GameState.GameOver iken PlayerController girdileri kilitlenmedi.");
                }

                gameManager.SetState(GameState.Playing);
            }

            // 3C. SetSpeedMultiplier doğrulaması (Physics.Simulate + LimitSpeed ile)
            // 1x çarpanla hızlanma + LimitSpeed ölçümü
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();
            testPlayer.SetSpeedMultiplier(1f);
            testPlayer.ProcessInputs(0f, 1f, false, false, false);

            AutoDevVerifier.SimulatePhysicsSteps(10, 0.02f, () =>
            {
                AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            });
            testPlayer.ProcessInputs(0f, 0f, false, false, false);
            AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            float normalClampedSpeed = new Vector3(testRb.linearVelocity.x, 0f, testRb.linearVelocity.z).magnitude;

            // 2x çarpanla hızlanma + LimitSpeed ölçümü
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();
            testPlayer.SetSpeedMultiplier(2f);
            testPlayer.ProcessInputs(0f, 1f, false, false, false);

            AutoDevVerifier.SimulatePhysicsSteps(10, 0.02f, () =>
            {
                AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            });
            testPlayer.ProcessInputs(0f, 0f, false, false, false);
            AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            float boostedFlatSpeed = new Vector3(testRb.linearVelocity.x, 0f, testRb.linearVelocity.z).magnitude;

            if (normalClampedSpeed > defaultStats.maxSpeed + 0.15f || normalClampedSpeed < defaultStats.maxSpeed * 0.7f)
            {
                return AutoDevCheckResult.Fail(
                    $"Normal (1x) hiz siniri dogrulanamadi. Beklenen~{defaultStats.maxSpeed:F2}, Alinan={normalClampedSpeed:F2}");
            }

            if (boostedFlatSpeed <= defaultStats.maxSpeed * 1.7f || boostedFlatSpeed > defaultStats.maxSpeed * 2f + 0.15f)
            {
                return AutoDevCheckResult.Fail(
                    $"SetSpeedMultiplier(2f) beklenen hiz araligini uretmedi. Beklenen: ({defaultStats.maxSpeed * 1.7f:F2}..{defaultStats.maxSpeed * 2f:F2}], Alinan: {boostedFlatSpeed:F2}");
            }

            testPlayer.SetSpeedMultiplier(1f);
            testPlayer.ProcessInputs(0f, 0f, false, false, false);

            // 3D. Harici hareket override koruması (Physics.Simulate + FixedUpdate ile sayısal doğrulama)
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();

            float highSpeed = Mathf.Max(22f, defaultStats.maxSpeed * 3f);
            testPlayer.SetExternalMovementOverride(true);
            testRb.linearVelocity = new Vector3(highSpeed, 0f, 0f);

            AutoDevVerifier.SimulatePhysicsSteps(10, 0.02f, () =>
            {
                AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            });

            float speedDuringOverride = new Vector3(testRb.linearVelocity.x, 0f, testRb.linearVelocity.z).magnitude;
            if (speedDuringOverride < highSpeed * 0.9f || speedDuringOverride <= defaultStats.maxSpeed * 2f)
            {
                return AutoDevCheckResult.Fail(
                    $"Harici hareket override aktifken hiz ezildi! Baslangic={highSpeed:F2}, 10 adim sonra={speedDuringOverride:F2}, MaxSpeed={defaultStats.maxSpeed:F2}");
            }

            // Override kapatılınca LimitSpeed ve ApplyDrag tekrar hızı MaxSpeed sınırına çekmeli
            testPlayer.SetExternalMovementOverride(false);
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            Physics.SyncTransforms();
            testPlayer.RefreshGroundState();
            testRb.linearVelocity = new Vector3(highSpeed, 0f, 0f);

            AutoDevVerifier.SimulatePhysicsSteps(5, 0.02f, () =>
            {
                AutoDevVerifier.InvokeLifecycle(testPlayer, "FixedUpdate");
            });

            float speedAfterOverrideOff = new Vector3(testRb.linearVelocity.x, 0f, testRb.linearVelocity.z).magnitude;
            if (speedAfterOverrideOff > defaultStats.maxSpeed + 0.15f)
            {
                return AutoDevCheckResult.Fail(
                    $"Override kapatildiktan sonra LimitSpeed hizi sinirlamadi! Hiz={speedAfterOverrideOff:F2}, MaxSpeed={defaultStats.maxSpeed:F2}");
            }

            // 3E. Coyote Time ve Jump Buffering davranışsal doğrulaması
            // E1: Coyote Time penceresi içinde zıplama
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.StepTimers(defaultStats.jumpCooldown + 0.1f); // Grounded + coyote dolu + cooldown temiz

            testPlayerObj.transform.position = new Vector3(0f, 510f, 0f); // Platformdan ayrıl
            Physics.SyncTransforms();
            testPlayer.StepTimers(defaultStats.coyoteTime * 0.4f); // Coyote süresi dolmadı
            if (testPlayer.IsGrounded)
            {
                return AutoDevCheckResult.Fail("Coyote Time testi sirasinda oyuncu havada olmalıydı.");
            }

            testPlayer.ProcessInputs(0f, 0f, true, false, false);
            AutoDevVerifier.SimulatePhysicsSteps(1, 0.02f);
            float coyoteJumpVelY = testRb.linearVelocity.y;
            if (coyoteJumpVelY <= 1f)
            {
                return AutoDevCheckResult.Fail($"Coyote Time penceresinde ziplama tetiklenmedi! velY={coyoteJumpVelY:F2}");
            }

            // E2: Coyote Time süresi dolunca havada zıplama engellenmeli
            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.StepTimers(defaultStats.jumpCooldown + 0.1f);

            testPlayerObj.transform.position = new Vector3(0f, 510f, 0f);
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.StepTimers(defaultStats.coyoteTime + 0.1f); // Coyote süresi doldu
            testPlayer.ProcessInputs(0f, 0f, true, false, false);
            AutoDevVerifier.SimulatePhysicsSteps(1, 0.02f);
            if (testRb.linearVelocity.y > 0.1f)
            {
                return AutoDevCheckResult.Fail($"Coyote Time dolduktan sonra havada ziplama yapildi! velY={testRb.linearVelocity.y:F2}");
            }

            // E3: Jump Buffering — yere inmeden önce basılan zıplama yere değince tetiklenmeli
            testPlayer.StepTimers(defaultStats.jumpCooldown + defaultStats.jumpBufferTime + 0.1f);
            testRb.linearVelocity = Vector3.zero;
            testPlayer.ProcessInputs(0f, 0f, true, false, false); // Havada buffer'a al
            testPlayer.StepTimers(defaultStats.jumpBufferTime * 0.4f); // Hâlâ havada, buffer aktif

            testPlayerObj.transform.position = new Vector3(0f, 501.52f, 0f); // Yere indi
            testRb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            testPlayer.StepTimers(0.02f); // Yere inişi algılayıp buffer'lı zıplamayı tüketmeli
            AutoDevVerifier.SimulatePhysicsSteps(1, 0.02f);
            float bufferedJumpVelY = testRb.linearVelocity.y;
            if (bufferedJumpVelY <= 1f)
            {
                return AutoDevCheckResult.Fail($"Jump Buffering yere inildiginde ziplamayi tetiklemedi! velY={bufferedJumpVelY:F2}");
            }

            // 3F. TargetJump düşen oyuncuya tutarlı fırlatma yüksekliği doğrulaması
            targetJumpObj = new GameObject("T001_TestTargetJump");
            TargetJump targetJump = targetJumpObj.AddComponent<TargetJump>();
            MethodInfo onTriggerEnter = typeof(TargetJump).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (onTriggerEnter == null)
            {
                return AutoDevCheckResult.Fail("TargetJump.OnTriggerEnter metodu bulunamadi.");
            }

            // Durgun oyuncu fırlatması
            testPlayer.SetExternalMovementOverride(true);
            testPlayerObj.transform.position = new Vector3(0f, 520f, 0f);
            Physics.SyncTransforms();
            testRb.linearDamping = 0f;
            testRb.linearVelocity = new Vector3(3f, 0f, 0f);
            onTriggerEnter.Invoke(targetJump, new object[] { playerCol });
            AutoDevVerifier.SimulatePhysicsSteps(1, 0.02f);
            float stationaryLaunchY = testRb.linearVelocity.y;

            // Hızlı düşen oyuncu (-18 m/s) fırlatması
            testPlayerObj.transform.position = new Vector3(0f, 520f, 0f);
            Physics.SyncTransforms();
            testRb.linearDamping = 0f;
            testRb.linearVelocity = new Vector3(3f, -18f, 0f);
            onTriggerEnter.Invoke(targetJump, new object[] { playerCol });
            AutoDevVerifier.SimulatePhysicsSteps(1, 0.02f);
            float fallingLaunchY = testRb.linearVelocity.y;
            float preservedHorizontalX = testRb.linearVelocity.x;

            if (stationaryLaunchY <= 5f || fallingLaunchY <= 5f || Mathf.Abs(fallingLaunchY - stationaryLaunchY) > 0.05f)
            {
                return AutoDevCheckResult.Fail(
                    $"TargetJump dusen oyuncuda tutarli dikey hiz uygulamadi! Durgun={stationaryLaunchY:F3}, Dusen={fallingLaunchY:F3}");
            }

            if (Mathf.Abs(preservedHorizontalX - 3f) > 0.1f)
            {
                return AutoDevCheckResult.Fail(
                    $"TargetJump yatay hizi korumadi! Beklenen X~3.0, Alinan={preservedHorizontalX:F3}");
            }

            string details =
                $"Asset={resolvedAssetPath} | SpeedMult2x={boostedFlatSpeed:F2} | OverrideSpeed={speedDuringOverride:F2} -> NormalClamped={speedAfterOverrideOff:F2} | " +
                $"CoyoteJumpY={coyoteJumpVelY:F2} | BufferedJumpY={bufferedJumpVelY:F2} | TargetJump(0 vs -18)={stationaryLaunchY:F2} vs {fallingLaunchY:F2}";

            // 4. Fizik simülasyonunun Demo.unity'de iz bırakmaması için temiz sahneyi yeniden yükleyip yalnızca SO bağlantısını uygula
            CleanupTempObjects(ref testPlayerObj, ref groundObj, ref targetJumpObj, ref frictionlessMat, ref customStats);
            RestoreRuntimeGlobals(
                originalTimeScale,
                settingsUI,
                originalSettingsInstance,
                settingsInstanceSetter,
                originalSettingsOpen,
                settingsMenuOpenField,
                gameManager,
                originalGameManagerInstance,
                gameManagerInstanceSetter,
                originalGameState);
            globalsRestored = true;

            AutoDevVerifier.ReloadCleanScene(AutoDevVerifier.DefaultScenePath);

            defaultStats = AssetDatabase.LoadAssetAtPath<PlayerStatsSO>(resolvedAssetPath);
            PlayerController cleanScenePlayer = FindSceneComponent<PlayerController>();
            if (cleanScenePlayer == null || defaultStats == null)
            {
                return AutoDevCheckResult.Fail("Temiz sahne yuklendikten sonra PlayerController veya DefaultPlayerStats bulunamadi.");
            }

            bool saveScene = false;
            SerializedObject serializedPlayer = new SerializedObject(cleanScenePlayer);
            SerializedProperty statsProp = serializedPlayer.FindProperty("_playerStats");
            if (statsProp == null)
            {
                return AutoDevCheckResult.Fail("PlayerController uzerinde '_playerStats' alani bulunamadi.");
            }

            if (statsProp.objectReferenceValue != defaultStats)
            {
                Undo.RecordObject(cleanScenePlayer, "Wire DefaultPlayerStats to PlayerController");
                statsProp.objectReferenceValue = defaultStats;
                serializedPlayer.ApplyModifiedProperties();
                EditorUtility.SetDirty(cleanScenePlayer);
                saveScene = true;
            }

            if (cleanScenePlayer.PlayerStats != defaultStats)
            {
                return AutoDevCheckResult.Fail("DefaultPlayerStats.asset Demo.unity'deki PlayerController'a baglanamadi.");
            }

            return AutoDevCheckResult.Pass(
                "T001 basariyla dogrulandi: PlayerStatsSO baglandi, girdi kilidi, hiz carpani, harici override korumasi, Coyote Time, Jump Buffering ve TargetJump tutarliligi onaylandi.",
                details,
                saveScene
            );
        }
        finally
        {
            CleanupTempObjects(ref testPlayerObj, ref groundObj, ref targetJumpObj, ref frictionlessMat, ref customStats);
            if (!globalsRestored)
            {
                RestoreRuntimeGlobals(
                    originalTimeScale,
                    settingsUI,
                    originalSettingsInstance,
                    settingsInstanceSetter,
                    originalSettingsOpen,
                    settingsMenuOpenField,
                    gameManager,
                    originalGameManagerInstance,
                    gameManagerInstanceSetter,
                    originalGameState);
            }
        }
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

    private static void CleanupTempObjects(
        ref GameObject testPlayerObj,
        ref GameObject groundObj,
        ref GameObject targetJumpObj,
        ref PhysicsMaterial frictionlessMat,
        ref PlayerStatsSO customStats)
    {
        if (testPlayerObj != null) { UnityEngine.Object.DestroyImmediate(testPlayerObj); testPlayerObj = null; }
        if (groundObj != null) { UnityEngine.Object.DestroyImmediate(groundObj); groundObj = null; }
        if (targetJumpObj != null) { UnityEngine.Object.DestroyImmediate(targetJumpObj); targetJumpObj = null; }
        if (frictionlessMat != null) { UnityEngine.Object.DestroyImmediate(frictionlessMat); frictionlessMat = null; }
        if (customStats != null) { UnityEngine.Object.DestroyImmediate(customStats); customStats = null; }
    }

    private static void RestoreRuntimeGlobals(
        float originalTimeScale,
        SettingsUI settingsUI,
        SettingsUI originalSettingsInstance,
        MethodInfo settingsInstanceSetter,
        bool originalSettingsOpen,
        FieldInfo settingsMenuOpenField,
        GameManager gameManager,
        GameManager originalGameManagerInstance,
        MethodInfo gameManagerInstanceSetter,
        GameState originalGameState)
    {
        Time.timeScale = originalTimeScale;

        if (settingsUI != null && settingsMenuOpenField != null)
        {
            settingsMenuOpenField.SetValue(settingsUI, originalSettingsOpen);
        }
        settingsInstanceSetter?.Invoke(null, new object[] { originalSettingsInstance });

        if (gameManager != null)
        {
            gameManager.SetState(originalGameState);
        }
        gameManagerInstanceSetter?.Invoke(null, new object[] { originalGameManagerInstance });
    }
}
