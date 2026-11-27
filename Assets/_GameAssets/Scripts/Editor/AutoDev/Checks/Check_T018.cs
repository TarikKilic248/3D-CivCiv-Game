using UnityEditor;
using UnityEngine;

/// <summary>
/// CivCiv 3D - T018 Doğrulama Kontrolü
/// Nesne Havuzu ve Karakter Hareket Efektleri (ObjectPoolManager & PlayerVFXController)
/// </summary>
public static class Check_T018
{
    [AutoDevCheck("T018")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Idempotent: Demo.unity sahnesinde ObjectPoolManager kur
        ObjectPoolManager poolManager = Object.FindAnyObjectByType<ObjectPoolManager>();
        if (poolManager == null)
        {
            GameObject poolObj = new GameObject("ObjectPoolManager");
            Undo.RegisterCreatedObjectUndo(poolObj, "Create ObjectPoolManager");
            poolManager = poolObj.AddComponent<ObjectPoolManager>();
            EditorUtility.SetDirty(poolObj);
            saveScene = true;
        }

        // 2. Idempotent: Demo.unity sahnesinde oyuncuda PlayerVFXController kur
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            return AutoDevCheckResult.Fail("Demo sahnesinde PlayerController bulunamadi.");
        }

        PlayerVFXController vfxController = player.GetComponent<PlayerVFXController>();
        if (vfxController == null)
        {
            vfxController = Undo.AddComponent<PlayerVFXController>(player.gameObject);
            saveScene = true;
        }

        vfxController.ResolveDependencies();
        if (vfxController.EnsureVFXSetup())
        {
            saveScene = true;
        }

        vfxController.SubscribeEvents();

        if (saveScene)
        {
            EditorUtility.SetDirty(player.gameObject);
            EditorUtility.SetDirty(vfxController);
        }

        // 3. Davranışsal Kontrol 1: ObjectPoolManager Nesne Alma / İade Etme ve Instance Yeniden Kullanımı
        GameObject testPrefab = new GameObject("T018_BehavioralPoolTestPrefab");
        GameObject instanceA = null;
        GameObject instanceB = null;

        try
        {
            testPrefab.AddComponent<ParticleSystem>();

            // İlk alma (Spawn): Yeni bir örnek üretilmeli
            instanceA = poolManager.SpawnObject(testPrefab, Vector3.zero, Quaternion.identity);
            if (instanceA == null || !instanceA.activeSelf)
            {
                return AutoDevCheckResult.Fail("ObjectPoolManager.SpawnObject geçerli bir aktif örnek döndürmedi.");
            }

            // İade etme (Return): Örnek pasifleşmeli
            poolManager.ReturnObject(instanceA);
            if (instanceA.activeSelf)
            {
                return AutoDevCheckResult.Fail("ObjectPoolManager.ReturnObject sonrasında nesne inaktif edilmedi.");
            }

            // İkinci alma (Spawn): Havuzdaki instanceA tekrar döndürülmeli (aynı nesne referansı!)
            instanceB = poolManager.SpawnObject(testPrefab, new Vector3(10f, 0f, 5f), Quaternion.identity);
            if (!ReferenceEquals(instanceA, instanceB))
            {
                return AutoDevCheckResult.Fail("ObjectPoolManager iade edilen nesneyi yeniden kullanmadı, gereksiz Instantiate yaptı.");
            }

            if (!instanceB.activeSelf)
            {
                return AutoDevCheckResult.Fail("Havuzdan tekrar alınan nesne aktif edilmedi.");
            }

            poolManager.ReturnObject(instanceB);
        }
        finally
        {
            if (instanceA != null) Object.DestroyImmediate(instanceA);
            if (instanceB != null && instanceB != instanceA) Object.DestroyImmediate(instanceB);
            Object.DestroyImmediate(testPrefab);
        }

        // 4. Davranışsal Kontrol 2: PlayerVFXController Eksik Prefab Güvenliği (Hata Fırlatmama)
        vfxController.ResetCounters();
        vfxController.JumpVFXPrefab = null;
        vfxController.LandVFXPrefab = null;
        vfxController.SlideVFXPrefab = null;
        vfxController.DashVFXPrefab = null;

        try
        {
            vfxController.TriggerJumpVFX();
            vfxController.TriggerLandVFX();
            vfxController.TriggerSlideVFX();
            vfxController.TriggerDashVFX();
        }
        catch (System.Exception ex)
        {
            return AutoDevCheckResult.Fail($"PlayerVFXController eksik prefab durumunda hata fırlattı: {ex.Message}");
        }

        if (vfxController.JumpVFXCount != 1 || vfxController.LandVFXCount != 1 ||
            vfxController.SlideVFXCount != 1 || vfxController.DashVFXCount != 1)
        {
            return AutoDevCheckResult.Fail($"Eksik prefab çağrılarında sayaçlar beklenen değeri almadı. Toplam: {vfxController.TotalVFXCount}");
        }

        // 5. Davranışsal Kontrol 3: Olay (Event) Tetikleme ve Sayaç Doğrulaması
        player.TriggerJumpEvent();
        if (vfxController.JumpVFXCount != 2)
        {
            return AutoDevCheckResult.Fail($"PlayerController.OnJump olayı PlayerVFXController zıplama sayacını tetiklemedi. Beklenen: 2, Gerçek: {vfxController.JumpVFXCount}");
        }

        player.TriggerLandedEvent();
        if (vfxController.LandVFXCount != 2)
        {
            return AutoDevCheckResult.Fail($"PlayerController.OnLanded olayı PlayerVFXController iniş sayacını tetiklemedi. Beklenen: 2, Gerçek: {vfxController.LandVFXCount}");
        }

        player.TriggerSlideStartedEvent();
        if (vfxController.SlideVFXCount != 2)
        {
            return AutoDevCheckResult.Fail($"PlayerController.OnSlideStarted olayı PlayerVFXController kayma sayacını tetiklemedi. Beklenen: 2, Gerçek: {vfxController.SlideVFXCount}");
        }

        DashController dash = player.GetComponent<DashController>();
        if (dash != null)
        {
            int dashBefore = vfxController.DashVFXCount;
            vfxController.HandleDash();
            if (vfxController.DashVFXCount != dashBefore + 1)
            {
                return AutoDevCheckResult.Fail("Dash tetiklemesi PlayerVFXController DashVFXCount sayacını artırmadı.");
            }
        }

        return AutoDevCheckResult.Pass(
            "ObjectPoolManager nesne yeniden kullanımı ve PlayerVFXController tetikleme sayaçları başarıyla doğrulandı.",
            $"Havuz Testi: OK, Eksik Prefab Toleransı: OK, JumpCount: {vfxController.JumpVFXCount}, LandCount: {vfxController.LandVFXCount}, SlideCount: {vfxController.SlideVFXCount}, DashCount: {vfxController.DashVFXCount}",
            saveScene
        );
    }
}
