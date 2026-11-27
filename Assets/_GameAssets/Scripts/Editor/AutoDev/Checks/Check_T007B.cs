using UnityEditor;
using UnityEngine;

public static class Check_T007B
{
    [AutoDevCheck("T007B")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        // 1. Demo.unity karakterine PlayerAbilityFeedback bagla ve VFX alt objelerini kur
        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
            return AutoDevCheckResult.Fail("Demo.unity icinde PlayerController bulunamadi.");

        bool saveScene = false;
        PlayerAbilityFeedback sceneFeedback = scenePlayer.GetComponent<PlayerAbilityFeedback>();
        if (sceneFeedback == null)
        {
            sceneFeedback = Undo.AddComponent<PlayerAbilityFeedback>(scenePlayer.gameObject);
            saveScene = true;
        }

        sceneFeedback.ResolveDependencies();
        if (sceneFeedback.EnsureVFXSetup())
            saveScene = true;

        if (sceneFeedback.DashTrail == null || sceneFeedback.DashBurstVFX == null ||
            sceneFeedback.AirJumpRingVFX == null || sceneFeedback.GlideFeatherVFX == null)
        {
            return AutoDevCheckResult.Fail("Sahne karakterinde gerekli TrailRenderer/ParticleSystem VFX bilesenleri eksik.");
        }

        if (saveScene)
        {
            EditorUtility.SetDirty(sceneFeedback);
            EditorUtility.SetDirty(scenePlayer.gameObject);
        }

        // 2. Izole test oyuncusu uzerinde Dash, AirJump ve Glide olay + gorsel + VFX dogrulamasi
        GameObject testPlayerObj = null;
        try
        {
            testPlayerObj = new GameObject("T007B_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 950f, 0f);

            GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualObj.name = "VisualModel";
            visualObj.transform.SetParent(testPlayerObj.transform, false);
            Object.DestroyImmediate(visualObj.GetComponent<Collider>());

            CapsuleCollider col = testPlayerObj.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;

            Rigidbody rb = testPlayerObj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;

            testPlayerObj.AddComponent<StateController>();
            PlayerController player = testPlayerObj.AddComponent<PlayerController>();
            DashController dash = testPlayerObj.AddComponent<DashController>();
            ChickGlideController glide = testPlayerObj.AddComponent<ChickGlideController>();
            PlayerAbilityFeedback feedback = testPlayerObj.AddComponent<PlayerAbilityFeedback>();

            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(dash, "Awake");
            AutoDevVerifier.InvokeLifecycle(glide, "Awake");
            AutoDevVerifier.InvokeLifecycle(feedback, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Start");
            AutoDevVerifier.InvokeLifecycle(dash, "Start");
            AutoDevVerifier.InvokeLifecycle(glide, "Start");

            Physics.SyncTransforms();
            player.RefreshGroundState();

            Quaternion baseRot = visualObj.transform.localRotation;
            Vector3 baseScale = visualObj.transform.localScale;

            // A: Dash Event + Trail + Burst + Scale/Tilt
            if (!dash.ExecuteDash())
                return AutoDevCheckResult.Fail("DashController.ExecuteDash() basarisiz oldu.");

            feedback.StepFeedback(0.08f);
            float dashAngle = Quaternion.Angle(visualObj.transform.localRotation, baseRot);
            float dashScaleDelta = Vector3.Distance(visualObj.transform.localScale, baseScale);

            if (feedback.DashEventCount != 1)
                return AutoDevCheckResult.Fail($"OnDashStarted tetiklenmedi. Count={feedback.DashEventCount}");
            if (!feedback.DashTrail.emitting)
                return AutoDevCheckResult.Fail("Dash sirasinda DashTrail.emitting true olmaliydi.");
            if (!feedback.DashBurstVFX.isPlaying)
                return AutoDevCheckResult.Fail("Dash sirasinda DashBurstVFX aktif olmaliydi.");
            if (dashAngle < 5f || dashScaleDelta < 0.05f)
                return AutoDevCheckResult.Fail($"Dash gorsel egim/olcek degisimi yetersiz! angle={dashAngle:F1}, scaleDelta={dashScaleDelta:F3}");

            dash.EndDash();
            for (int i = 0; i < 20; i++) feedback.StepFeedback(0.05f);
            if (feedback.DashTrail.emitting)
                return AutoDevCheckResult.Fail("Dash bitince DashTrail.emitting false olmaliydi.");

            // B: AirJump Event + Ring VFX + Procedural Spin/Squash-Stretch
            if (!glide.ExecuteAirJump())
                return AutoDevCheckResult.Fail("ChickGlideController.ExecuteAirJump() basarisiz oldu.");

            feedback.StepFeedback(0.08f);
            float airJumpAngle = Quaternion.Angle(visualObj.transform.localRotation, baseRot);
            float airJumpScaleDelta = Vector3.Distance(visualObj.transform.localScale, baseScale);

            if (feedback.AirJumpEventCount != 1)
                return AutoDevCheckResult.Fail($"OnAirJumpExecuted tetiklenmedi. Count={feedback.AirJumpEventCount}");
            if (!feedback.AirJumpRingVFX.isPlaying)
                return AutoDevCheckResult.Fail("AirJump sirasinda AirJumpRingVFX aktif olmaliydi.");
            if (airJumpAngle < 10f || airJumpScaleDelta < 0.03f)
                return AutoDevCheckResult.Fail($"AirJump gorsel donus/olcek degisimi yetersiz! angle={airJumpAngle:F1}, scaleDelta={airJumpScaleDelta:F3}");

            for (int i = 0; i < 15; i++) feedback.StepFeedback(0.05f);

            // C: Glide Event + Feather Emission + Tilt/Sway + Recovery
            rb.linearVelocity = new Vector3(0f, -10f, 0f);
            if (!glide.StartGlide())
                return AutoDevCheckResult.Fail("ChickGlideController.StartGlide() basarisiz oldu.");

            feedback.StepFeedback(0.1f);
            feedback.StepFeedback(0.1f);
            float glideAngle = Quaternion.Angle(visualObj.transform.localRotation, baseRot);

            if (feedback.GlideStartEventCount != 1)
                return AutoDevCheckResult.Fail($"OnGlideStateChanged(true) tetiklenmedi. Count={feedback.GlideStartEventCount}");
            if (!feedback.GlideFeatherVFX.emission.enabled || !feedback.GlideFeatherVFX.isPlaying)
                return AutoDevCheckResult.Fail("Glide sirasinda GlideFeatherVFX emisyonu ve oynatimi aktif olmaliydi.");
            if (glideAngle < 5f)
                return AutoDevCheckResult.Fail($"Glide sirasinda gorsel egim/salinim yetersiz! angle={glideAngle:F1}");

            glide.StopGlide();
            for (int i = 0; i < 25; i++) feedback.StepFeedback(0.05f);
            float recoveredAngle = Quaternion.Angle(visualObj.transform.localRotation, baseRot);

            if (feedback.GlideStopEventCount != 1)
                return AutoDevCheckResult.Fail($"OnGlideStateChanged(false) tetiklenmedi. Count={feedback.GlideStopEventCount}");
            if (feedback.GlideFeatherVFX.emission.enabled)
                return AutoDevCheckResult.Fail("Glide bitince GlideFeatherVFX emisyonu kapanmaliydi.");
            if (recoveredAngle > 2f)
                return AutoDevCheckResult.Fail($"Glide bitince gorsel orijinal durusa donmedi! recoveredAngle={recoveredAngle:F2}");

            return AutoDevCheckResult.Pass(
                "T007B: Dash, AirJump ve Glide olaylari, prosedurel animasyon ve Trail/ParticleSystem efektleri dogrulandi.",
                $"DashAngle={dashAngle:F1}, AirJumpAngle={airJumpAngle:F1}, GlideAngle={glideAngle:F1}, Recovered={recoveredAngle:F2}",
                saveScene: saveScene
            );
        }
        finally
        {
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
        }
    }
}
