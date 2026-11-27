using System;
using UnityEditor;
using UnityEngine;

public static class Check_T007
{
    [AutoDevCheck("T007")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        // 1. Sahnedeki PlayerController'ı bul ve ChickGlideController bileşenini bağla
        PlayerController scenePlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity icinde PlayerController bulunamadi.");
        }

        bool saveScene = false;
        ChickGlideController sceneGlide = scenePlayer.GetComponent<ChickGlideController>();
        if (sceneGlide == null)
        {
            sceneGlide = Undo.AddComponent<ChickGlideController>(scenePlayer.gameObject);
            saveScene = true;
        }

        // 2. Davranışsal Doğrulama: İzole test nesnesi ile Air Jump ve Glide testleri
        GameObject testGroundObj = null;
        GameObject testPlayerObj = null;

        try
        {
            // İzole Zemin (Y = 900)
            testGroundObj = new GameObject("T007_TestGround");
            testGroundObj.transform.position = new Vector3(0f, 900f, 0f);
            BoxCollider groundCol = testGroundObj.AddComponent<BoxCollider>();
            groundCol.size = new Vector3(20f, 1f, 20f);

            // İzole Havada Oyuncu (Y = 950)
            testPlayerObj = new GameObject("T007_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 950f, 0f);
            CapsuleCollider playerCol = testPlayerObj.AddComponent<CapsuleCollider>();
            playerCol.height = 2f;
            playerCol.radius = 0.4f;

            Rigidbody rb = testPlayerObj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;

            StateController state = testPlayerObj.AddComponent<StateController>();
            PlayerController player = testPlayerObj.AddComponent<PlayerController>();
            ChickGlideController glide = testPlayerObj.AddComponent<ChickGlideController>();

            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(glide, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Start");
            AutoDevVerifier.InvokeLifecycle(glide, "Start");

            Physics.SyncTransforms();
            player.RefreshGroundState();

            // A: Başlangıç Havada Durumu
            if (player.IsGrounded)
            {
                return AutoDevCheckResult.Fail("Test oyuncusu baslangicta havadayken IsGrounded == false olmaliydi.");
            }

            if (glide.RemainingAirJumps != 1)
            {
                return AutoDevCheckResult.Fail($"Baslangicta RemainingAirJumps 1 olmaliydi. Alinan: {glide.RemainingAirJumps}");
            }

            if (glide.IsGliding)
            {
                return AutoDevCheckResult.Fail("Baslangicta IsGliding false olmaliydi.");
            }

            // B: Air Jump Tetikleme ve Pozitif Dikey Hız Doğrulaması
            bool airJumpSuccess = glide.ExecuteAirJump();
            if (!airJumpSuccess)
            {
                return AutoDevCheckResult.Fail("Havadayken ExecuteAirJump() basarisiz oldu (false dondurdu).");
            }

            if (glide.RemainingAirJumps != 0)
            {
                return AutoDevCheckResult.Fail($"Air Jump sonrasi RemainingAirJumps 0 olmaliydi. Alinan: {glide.RemainingAirJumps}");
            }

            if (rb.linearVelocity.y <= 0f)
            {
                return AutoDevCheckResult.Fail($"Air Jump sonrasi dikey hiz pozitif olmaliydi! linearVelocity.y: {rb.linearVelocity.y}");
            }

            if (state.GetCurrentState() != PlayerState.Jump)
            {
                return AutoDevCheckResult.Fail($"Air Jump sonrasi durum PlayerState.Jump olmaliydi. Alinan: {state.GetCurrentState()}");
            }

            // İkinci Air Jump denenirse başarısız olmalı (hakkı bitti)
            if (glide.ExecuteAirJump())
            {
                return AutoDevCheckResult.Fail("RemainingAirJumps bittiginde ExecuteAirJump() false dondurmelidir.");
            }

            // C: Süzülme (Glide) ve Düşüş Hızı Sınırı (maxGlideFallSpeed) Doğrulaması
            // Karakteri aşağı doğru yüksek hızla düşüşe geçir
            rb.linearVelocity = new Vector3(0f, -12f, 0f);

            bool glideStarted = glide.StartGlide();
            if (!glideStarted)
            {
                return AutoDevCheckResult.Fail("StartGlide() cagrisi false dondurdu.");
            }

            if (!glide.IsGliding)
            {
                return AutoDevCheckResult.Fail("StartGlide() sonrasi IsGliding true olmaliydi.");
            }

            if (state.GetCurrentState() != PlayerState.Glide)
            {
                return AutoDevCheckResult.Fail($"StartGlide sonrasi durum PlayerState.Glide olmaliydi. Alinan: {state.GetCurrentState()}");
            }

            // Physics.Simulate ile 10 adım fizik koştur
            float maxAllowedFallSpeed = glide.MaxGlideFallSpeed;
            AutoDevVerifier.SimulatePhysicsSteps(10, 0.02f, () =>
            {
                glide.Step(0.02f);
            });

            float actualFallSpeed = -rb.linearVelocity.y;
            if (actualFallSpeed > maxAllowedFallSpeed + 0.1f)
            {
                return AutoDevCheckResult.Fail($"Suzulme sirasinda dusus hizi maxGlideFallSpeed ({maxAllowedFallSpeed}) sinirini asti! Alinan: {actualFallSpeed}");
            }

            // D: StopGlide Doğrulaması
            glide.StopGlide();
            if (glide.IsGliding)
            {
                return AutoDevCheckResult.Fail("StopGlide() sonrasi IsGliding false olmaliydi.");
            }

            if (state.GetCurrentState() == PlayerState.Glide)
            {
                return AutoDevCheckResult.Fail("StopGlide() sonrasi durum PlayerState.Glide kalmamaliydi.");
            }

            // E: Yere İniş ve Air Jump Sayacının Sıfırlanması
            testPlayerObj.transform.position = new Vector3(0f, 901.55f, 0f);
            rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            player.RefreshGroundState();
            glide.Step(0.02f);

            if (player.IsGrounded && glide.RemainingAirJumps != glide.MaxAirJumps)
            {
                return AutoDevCheckResult.Fail($"Yere inildiginde RemainingAirJumps ({glide.RemainingAirJumps}) MaxAirJumps'a ({glide.MaxAirJumps}) sifirlanmadi!");
            }
        }
        finally
        {
            if (testPlayerObj != null) UnityEngine.Object.DestroyImmediate(testPlayerObj);
            if (testGroundObj != null) UnityEngine.Object.DestroyImmediate(testGroundObj);
        }

        return AutoDevCheckResult.Pass("T007: Cift ziplama ve kanatla suzulme mekanigi basariyla dogrulandi.", saveScene: saveScene);
    }
}
