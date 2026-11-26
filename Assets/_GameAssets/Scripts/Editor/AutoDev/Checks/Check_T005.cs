using System;
using UnityEditor;
using UnityEngine;

public static class Check_T005
{
    [AutoDevCheck("T005")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        ClockUI clock = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
        CoinManager coinManager = UnityEngine.Object.FindAnyObjectByType<CoinManager>();

        if (player == null || clock == null || coinManager == null)
        {
            return AutoDevCheckResult.Fail("Temel sahne bilesenleri (Player/Clock/CoinManager) eksik!");
        }

        DashController dash = player.GetComponent<DashController>();
        if (dash == null)
        {
            return AutoDevCheckResult.Fail("Player uzerinde DashController bulunamadi!");
        }

        bool saveScene = false;

        // 1. Sahnede her 3 tur PowerUpCollectable orneginin varligini idempotent olarak sagla
        PowerUpCollectable[] scenePowerUps = UnityEngine.Object.FindObjectsByType<PowerUpCollectable>(FindObjectsInactive.Include);
        bool hasExtraTime = false, hasSpeedBoost = false, hasStaminaRefill = false;

        foreach (var p in scenePowerUps)
        {
            if (p.Type == PowerUpType.ExtraTime) hasExtraTime = true;
            if (p.Type == PowerUpType.SpeedBoost) hasSpeedBoost = true;
            if (p.Type == PowerUpType.StaminaRefill) hasStaminaRefill = true;
        }

        if (!hasExtraTime)
        {
            CreateScenePowerUp("PowerUp_ExtraTime", PowerUpType.ExtraTime, new Vector3(3.5f, 0.4f, 3.5f));
            saveScene = true;
        }
        if (!hasSpeedBoost)
        {
            CreateScenePowerUp("PowerUp_SpeedBoost", PowerUpType.SpeedBoost, new Vector3(5.5f, 0.4f, 5.5f));
            saveScene = true;
        }
        if (!hasStaminaRefill)
        {
            CreateScenePowerUp("PowerUp_StaminaRefill", PowerUpType.StaminaRefill, new Vector3(7.5f, 0.4f, 7.5f));
            saveScene = true;
        }

        // 2. CoinCollect Davranissal Dogrulama: Donme/suzme ve cift toplamayi engelleme
        GameObject testCoinObj = new GameObject("T005_TestCoin");
        testCoinObj.transform.position = new Vector3(0f, 900f, 0f);
        CoinCollect testCoin = testCoinObj.AddComponent<CoinCollect>();
        testCoin.UpdateMotion(0.25f);
        if (Mathf.Approximately(testCoin.transform.eulerAngles.y, 0f) && Mathf.Approximately(testCoin.transform.position.y, 900f))
        {
            UnityEngine.Object.DestroyImmediate(testCoinObj);
            return AutoDevCheckResult.Fail("CoinCollect UpdateMotion ile rotasyon veya bobbing hareketi uretmedi!");
        }

        int coinsBefore = coinManager.Coins;
        testCoin.Collect();
        if (!testCoin.IsCollected)
        {
            UnityEngine.Object.DestroyImmediate(testCoinObj);
            return AutoDevCheckResult.Fail("CoinCollect.Collect sonrasi IsCollected true olmaliydi!");
        }
        int coinsAfterFirst = coinManager.Coins;
        if (coinsAfterFirst != coinsBefore + testCoin.Value)
        {
            UnityEngine.Object.DestroyImmediate(testCoinObj);
            return AutoDevCheckResult.Fail($"CoinCollect.Collect sonrasi bakiye artmadi! Once: {coinsBefore}, Simdi: {coinsAfterFirst}");
        }

        // Ikinci Collect cagrisi cift toplamayi onlemeli
        testCoin.Collect();
        if (coinManager.Coins != coinsAfterFirst)
        {
            UnityEngine.Object.DestroyImmediate(testCoinObj);
            return AutoDevCheckResult.Fail("CoinCollect cift Collect() cagrisini yoksaymadi, puan tekrar eklendi!");
        }
        UnityEngine.Object.DestroyImmediate(testCoinObj);

        // 3. PowerUp ExtraTime Davranissal Dogrulama
        float initialClock = clock.RemainingSeconds;
        GameObject testTimeObj = new GameObject("T005_TestExtraTime");
        PowerUpCollectable testTimePu = testTimeObj.AddComponent<PowerUpCollectable>();
        testTimePu.Type = PowerUpType.ExtraTime;
        testTimePu.ExtraTimeAmount = 15f;
        testTimePu.Collect(player);

        if (Mathf.Abs(clock.RemainingSeconds - (initialClock + 15f)) > 0.05f)
        {
            UnityEngine.Object.DestroyImmediate(testTimeObj);
            return AutoDevCheckResult.Fail($"ExtraTime ClockUI'a 15s eklemedi! Once: {initialClock}, Simdi: {clock.RemainingSeconds}");
        }
        testTimePu.Collect(player); // Cift toplama testi
        if (Mathf.Abs(clock.RemainingSeconds - (initialClock + 15f)) > 0.05f)
        {
            UnityEngine.Object.DestroyImmediate(testTimeObj);
            return AutoDevCheckResult.Fail("ExtraTime cift Collect cagrisinda ikinci kez sure ekledi!");
        }
        clock.SetRemainingSeconds(initialClock);
        UnityEngine.Object.DestroyImmediate(testTimeObj);

        // 4. PowerUp SpeedBoost Davranissal Dogrulama
        player.SetSpeedMultiplier(1f);
        GameObject testSpeedObj = new GameObject("T005_TestSpeedBoost");
        PowerUpCollectable testSpeedPu = testSpeedObj.AddComponent<PowerUpCollectable>();
        testSpeedPu.Type = PowerUpType.SpeedBoost;
        testSpeedPu.SpeedMultiplier = 1.75f;
        testSpeedPu.Collect(player);

        if (Mathf.Abs(player.SpeedMultiplier - 1.75f) > 0.01f)
        {
            UnityEngine.Object.DestroyImmediate(testSpeedObj);
            return AutoDevCheckResult.Fail($"SpeedBoost PlayerController hiz carpanini 1.75 yapmadi! Simdiki: {player.SpeedMultiplier}");
        }
        testSpeedPu.Collect(player); // Cift toplama testi
        if (Mathf.Abs(player.SpeedMultiplier - 1.75f) > 0.01f)
        {
            UnityEngine.Object.DestroyImmediate(testSpeedObj);
            return AutoDevCheckResult.Fail("SpeedBoost cift toplamada bozuldu!");
        }
        player.SetSpeedMultiplier(1f);
        UnityEngine.Object.DestroyImmediate(testSpeedObj);

        // 5. PowerUp StaminaRefill Davranissal Dogrulama
        dash.SetStamina(25f);
        GameObject testStaminaObj = new GameObject("T005_TestStaminaRefill");
        PowerUpCollectable testStaminaPu = testStaminaObj.AddComponent<PowerUpCollectable>();
        testStaminaPu.Type = PowerUpType.StaminaRefill;
        testStaminaPu.StaminaRefillAmount = 50f;
        testStaminaPu.Collect(player);

        if (Mathf.Abs(dash.CurrentStamina - 75f) > 0.1f)
        {
            UnityEngine.Object.DestroyImmediate(testStaminaObj);
            return AutoDevCheckResult.Fail($"StaminaRefill DashController staminasini 75 yapmadi! Simdiki: {dash.CurrentStamina}");
        }
        testStaminaPu.Collect(player); // Cift toplama testi
        if (Mathf.Abs(dash.CurrentStamina - 75f) > 0.1f)
        {
            UnityEngine.Object.DestroyImmediate(testStaminaObj);
            return AutoDevCheckResult.Fail("StaminaRefill cift toplamada tekrar stamina ekledi!");
        }
        dash.ResetStamina();
        UnityEngine.Object.DestroyImmediate(testStaminaObj);

        return AutoDevCheckResult.Pass("T005 basariyla dogrulandi: CoinCollect ve 3 PowerUp turu (ExtraTime, SpeedBoost, StaminaRefill) tam calisiyor.", $"SaveScene={saveScene}", saveScene);
    }

    private static void CreateScenePowerUp(string name, PowerUpType type, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.5f, 0.2f, 0.5f);

        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        PowerUpCollectable pup = go.AddComponent<PowerUpCollectable>();
        pup.Type = type;
        if (type == PowerUpType.ExtraTime) pup.ExtraTimeAmount = 15f;
        else if (type == PowerUpType.SpeedBoost) { pup.SpeedMultiplier = 1.5f; pup.SpeedBoostDuration = 5f; }
        else if (type == PowerUpType.StaminaRefill) pup.StaminaRefillAmount = 100f;

        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
    }
}
