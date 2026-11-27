using UnityEditor;
using UnityEngine;

/// <summary>
/// CivCiv 3D - T019 Doğrulama Kontrolü
/// Kamera Hissiyatı ve 3D Ses Mimarisi (CameraJuiceController & SpatialAudioManager & FootstepAudioTrigger)
/// </summary>
public static class Check_T019
{
    [AutoDevCheck("T019")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Idempotent: SpatialAudioManager kur
        SpatialAudioManager audioManager = Object.FindAnyObjectByType<SpatialAudioManager>();
        if (audioManager == null)
        {
            GameObject audioObj = new GameObject("SpatialAudioManager");
            Undo.RegisterCreatedObjectUndo(audioObj, "Create SpatialAudioManager");
            audioManager = audioObj.AddComponent<SpatialAudioManager>();
            audioManager.InitializePool();
            EditorUtility.SetDirty(audioObj);
            saveScene = true;
        }

        // 2. Idempotent: CameraJuiceController kur
        CameraJuiceController juice = Object.FindAnyObjectByType<CameraJuiceController>();
        if (juice == null)
        {
            GameObject juiceObj = new GameObject("CameraJuiceController");
            Undo.RegisterCreatedObjectUndo(juiceObj, "Create CameraJuiceController");
            juice = juiceObj.AddComponent<CameraJuiceController>();
            EditorUtility.SetDirty(juiceObj);
            saveScene = true;
        }
        juice.ResolveDependencies();

        // 3. Idempotent: Karakterde FootstepAudioTrigger kur
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            return AutoDevCheckResult.Fail("Demo sahnesinde PlayerController bulunamadı.");
        }

        FootstepAudioTrigger footstep = player.GetComponent<FootstepAudioTrigger>();
        if (footstep == null)
        {
            footstep = Undo.AddComponent<FootstepAudioTrigger>(player.gameObject);
            EditorUtility.SetDirty(player.gameObject);
            saveScene = true;
        }
        footstep.ResolveDependencies();

        // --- Davranışsal Test 1: CameraJuiceController FOV Genişlemesi ---
        juice.SetBoostActive(false);
        float baseFov = juice.DefaultFOV;
        juice.StepFOV(1.0f);

        juice.SetBoostActive(true);
        if (juice.TargetFOV <= baseFov)
        {
            return AutoDevCheckResult.Fail($"Boost aktifken TargetFOV artırılmadı. Baz: {baseFov}, Target: {juice.TargetFOV}");
        }

        juice.StepFOV(0.2f);
        if (juice.CurrentFOV <= baseFov)
        {
            return AutoDevCheckResult.Fail($"StepFOV sonrasında CurrentFOV baz değerin üzerine çıkmadı. CurrentFOV: {juice.CurrentFOV}");
        }

        juice.SetBoostActive(false);
        if (!Mathf.Approximately(juice.TargetFOV, baseFov))
        {
            return AutoDevCheckResult.Fail($"Boost kapandığında TargetFOV baz değere dönmedi. Target: {juice.TargetFOV}, Baz: {baseFov}");
        }

        juice.StepFOV(2.0f);
        if (Mathf.Abs(juice.CurrentFOV - baseFov) > 0.1f)
        {
            return AutoDevCheckResult.Fail($"Boost sonrasında CurrentFOV baz değere sönümlenmedi. CurrentFOV: {juice.CurrentFOV}");
        }

        // --- Davranışsal Test 2: CameraJuiceController Sönümlü Sarsıntı (Shake) ---
        juice.TriggerShake(intensity: 1.8f, duration: 0.35f);
        if (juice.ShakeIntensity <= 0f || juice.ShakeTimer <= 0f)
        {
            return AutoDevCheckResult.Fail("TriggerShake sarsıntı şiddeti veya sayacını başlatamadı.");
        }

        juice.StepShake(0.05f);
        if (juice.CurrentShakeOffset.sqrMagnitude <= 0.00001f)
        {
            return AutoDevCheckResult.Fail("StepShake sonrasında sarsıntı ofseti üretilemedi.");
        }

        // Sarsıntı süresini tamamla ve tam sönümlenmeyi doğrula
        juice.StepShake(0.5f);
        if (juice.ShakeTimer > 0f || juice.CurrentShakeOffset != Vector3.zero)
        {
            return AutoDevCheckResult.Fail($"Süre bittiğinde sarsıntı sıfırlanmadı. Timer: {juice.ShakeTimer}, Offset: {juice.CurrentShakeOffset}");
        }

        // --- Davranışsal Test 3: SpatialAudioManager Mute/Unmute & Pitch Varyasyonu ---
        SettingsUI settingsUI = Object.FindAnyObjectByType<SettingsUI>();
        AudioClip testClip = AudioClip.Create("T019_TestClip", 441, 1, 44100, false);

        try
        {
            if (settingsUI != null)
            {
                // Sessize alma (Mute) kontrolü
                if (SettingsUI.IsSoundEnabled) settingsUI.ToggleSound();

                audioManager.ResetCounters();
                audioManager.PlaySoundAtPoint(testClip, Vector3.zero, 1f, 0.9f, 1.1f);

                if (audioManager.TotalSoundsPlayed > 0)
                {
                    return AutoDevCheckResult.Fail("SettingsUI.IsSoundEnabled kapalıyken SpatialAudioManager ses çaldı.");
                }

                // Sesi tekrar açma (Unmute) ve Pitch kontrolü
                if (!SettingsUI.IsSoundEnabled) settingsUI.ToggleSound();

                audioManager.ResetCounters();
                audioManager.PlaySoundAtPoint(testClip, Vector3.zero, 1f, 0.85f, 1.15f);

                if (audioManager.TotalSoundsPlayed != 1)
                {
                    return AutoDevCheckResult.Fail($"SettingsUI.IsSoundEnabled açıkken SpatialAudioManager ses çalamadı. Toplam: {audioManager.TotalSoundsPlayed}");
                }

                if (audioManager.LastPlayedPitch < 0.84f || audioManager.LastPlayedPitch > 1.16f)
                {
                    return AutoDevCheckResult.Fail($"Çalınan sesin pitch değeri beklenen aralıkta [0.85, 1.15] değil: {audioManager.LastPlayedPitch}");
                }
            }
        }
        finally
        {
            if (testClip != null) Object.DestroyImmediate(testClip);
        }

        // --- Davranışsal Test 4: FootstepAudioTrigger Adım Birikimi ---
        footstep.ResetCounters();
        footstep.StepFootstep(deltaTime: 1f, currentSpeed: 5f, isGrounded: true);
        if (footstep.FootstepCount <= 0)
        {
            return AutoDevCheckResult.Fail($"FootstepAudioTrigger hareket anında adım sesi tetiklemedi. FootstepCount: {footstep.FootstepCount}");
        }

        return AutoDevCheckResult.Pass(
            "Kamera FOV genişlemesi, sönümlü sarsıntı, SpatialAudioManager mute/unmute/pitch ve FootstepAudioTrigger başarıyla doğrulandı.",
            $"BaseFOV: {baseFov}, TargetFOV: {juice.TargetFOV}, Pitch: {audioManager.LastPlayedPitch:F2}, Footsteps: {footstep.FootstepCount}",
            saveScene
        );
    }
}
