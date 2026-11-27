using UnityEditor;
using UnityEngine;

/// <summary>
/// CivCiv 3D - T024 Doğrulama Kontrolü
/// Hızlandırma Halkası (SpeedBoostRing): İleri fiziksel hız artışı, cooldown kontrolü,
/// OnRingPassed olayı, prosedürel nabız/dönüş animasyonu ve VFX/Audio tetiklenmesi.
/// </summary>
public static class Check_T024
{
    [AutoDevCheck("T024")]
    [AutoDevCheck("TriggerBoost ile Rigidbody ileri hizinin belirgin sekilde arttigini, cooldown suresince tekrar tetiklenmedigini, OnRingPassed olayinin ve StepFeedback/VFX kurulumunun calistigini dogrula.")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Demo.unity sahnesinde idempotent SpeedBoostRing nesnesi kur/doğrula
        SpeedBoostRing sceneRing = Object.FindAnyObjectByType<SpeedBoostRing>();
        if (sceneRing == null)
        {
            GameObject ringObj = new GameObject("SpeedBoostRing");
            Undo.RegisterCreatedObjectUndo(ringObj, "Create SpeedBoostRing");
            ringObj.transform.position = new Vector3(1.85f, 2.5f, 8.0f);
            ringObj.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            sceneRing = ringObj.AddComponent<SpeedBoostRing>();
            sceneRing.EnsureVFXSetup();
            EditorUtility.SetDirty(ringObj);
            saveScene = true;
        }
        else
        {
            if (sceneRing.EnsureVFXSetup())
            {
                EditorUtility.SetDirty(sceneRing.gameObject);
                saveScene = true;
            }
        }

        // 2. İzole test ortamında davranışsal doğrulama
        GameObject testRingObj = null;
        GameObject testPlayerObj = null;

        try
        {
            testRingObj = new GameObject("T024_TestRing");
            testRingObj.transform.position = new Vector3(0f, 920f, 0f);
            testRingObj.transform.rotation = Quaternion.identity;
            SpeedBoostRing ring = testRingObj.AddComponent<SpeedBoostRing>();
            AutoDevVerifier.InvokeLifecycle(ring, "Awake");

            if (ring.BoostVFX == null || ring.Trail == null || ring.BoostSFX == null)
            {
                return AutoDevCheckResult.Fail("SpeedBoostRing VFX, TrailRenderer veya SFX kurulumu eksik.");
            }

            Rigidbody passedRb = null;
            ring.OnRingPassed += r => passedRb = r;

            testPlayerObj = new GameObject("T024_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 920f, -1f);
            Rigidbody rb = testPlayerObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;

            // Test 1: İlk tetikleme - Hız artışı, olay ve sayaçlar
            bool firstBoost = ring.TriggerBoost(rb);
            if (!firstBoost)
            {
                return AutoDevCheckResult.Fail("TriggerBoost ilk cagrisinda basarisiz oldu.");
            }

            if (passedRb != rb)
            {
                return AutoDevCheckResult.Fail("OnRingPassed olayi tetiklenmedi veya Rigidbody referansi eslesmedi.");
            }

            if (rb.linearVelocity.z < ring.BoostForce - 0.01f)
            {
                return AutoDevCheckResult.Fail($"Ileri hiz beklenen seviyeye ulasmadi: {rb.linearVelocity.z:F2} < {ring.BoostForce:F2}");
            }

            if (ring.BoostCount != 1)
            {
                return AutoDevCheckResult.Fail($"BoostCount beklenen 1 degil: {ring.BoostCount}");
            }

            if (ring.SfxPlayCount != 1)
            {
                return AutoDevCheckResult.Fail($"SfxPlayCount beklenen 1 degil: {ring.SfxPlayCount}");
            }

            // Test 2: Cooldown testi - Bekleme süresi dolmadan çift itki engellenmeli
            float speedAfterFirst = rb.linearVelocity.z;
            bool secondBoost = ring.TriggerBoost(rb);
            if (secondBoost)
            {
                return AutoDevCheckResult.Fail("Cooldown suresi dolmadan cift itki uygulandi.");
            }

            if (ring.BoostCount != 1)
            {
                return AutoDevCheckResult.Fail("Cooldown sirasinda BoostCount artti.");
            }

            if (Mathf.Abs(rb.linearVelocity.z - speedAfterFirst) > 0.01f)
            {
                return AutoDevCheckResult.Fail("Cooldown sirasinda Rigidbody hizi degisti.");
            }

            // Test 3: Cooldown süresinin tamamlanması ve yeniden tetiklenebilirlik
            ring.Step(ring.Cooldown + 0.05f);
            if (ring.IsOnCooldown)
            {
                return AutoDevCheckResult.Fail("Cooldown suresi dolmasina ragmen IsOnCooldown true kaldi.");
            }

            bool thirdBoost = ring.TriggerBoost(rb);
            if (!thirdBoost)
            {
                return AutoDevCheckResult.Fail("Cooldown suresi dolduktan sonra TriggerBoost tekrar tetiklenemedi.");
            }

            if (ring.BoostCount != 2)
            {
                return AutoDevCheckResult.Fail($"Ikinci gecerli boost sonrasi BoostCount beklenen 2 degil: {ring.BoostCount}");
            }

            if (rb.linearVelocity.z <= speedAfterFirst)
            {
                return AutoDevCheckResult.Fail("Ikinci gecerli boost sonrasi Rigidbody ileri hizi artmadi.");
            }

            // Test 4: VFX ve Prosedürel Nabız / Trail Davranışları
            if (ring.ActiveTrail == null || !ring.ActiveTrail.emitting)
            {
                return AutoDevCheckResult.Fail("TriggerBoost sonrasi TrailRenderer aktif edilmedi.");
            }

            ring.Step(0.05f);
            if (ring.VisualRoot.localScale.x <= 1.0f)
            {
                return AutoDevCheckResult.Fail("StepFeedback sirasinda nabiz genlesmesi (squash-stretch) gerceklesmedi.");
            }

            ring.Step(1.0f);
            if (Mathf.Abs(ring.VisualRoot.localScale.x - 1.0f) > 0.02f)
            {
                return AutoDevCheckResult.Fail("Nabiz animasyonu sonunda VisualRoot baslangic olcegine donmedi.");
            }

            if (ring.ActiveTrail.emitting)
            {
                return AutoDevCheckResult.Fail("Trail suresi dolmasina ragmen emitting kapatilmadi.");
            }

            return AutoDevCheckResult.Pass(
                $"SpeedBoostRing davranislari basariyla dogrulandi. BoostForce={ring.BoostForce:F1}, BoostCount={ring.BoostCount}",
                "",
                saveScene);
        }
        finally
        {
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
            if (testRingObj != null) Object.DestroyImmediate(testRingObj);
        }
    }
}
