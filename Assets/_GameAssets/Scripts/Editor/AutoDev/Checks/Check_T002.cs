using System;
using UnityEditor;
using UnityEngine;

public static class Check_T002
{
    [AutoDevCheck("T002")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Sahnede GameManager ve CoinManager nesnelerinin varlığını doğrula ve eksikse kur
        GameManager gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gameManager = gmObj.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(gmObj, "Create GameManager for T002");
            saveScene = true;
        }

        CoinManager coinManager = UnityEngine.Object.FindAnyObjectByType<CoinManager>();
        if (coinManager == null)
        {
            GameObject cmObj = new GameObject("CoinManager");
            coinManager = cmObj.AddComponent<CoinManager>();
            Undo.RegisterCreatedObjectUndo(cmObj, "Create CoinManager for T002");
            saveScene = true;
        }

        ClockUI clockUI = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
        if (clockUI == null)
        {
            return AutoDevCheckResult.Fail("ClockUI sahnede bulunamadi!");
        }

        // Awake/Start bağlantılarını güvene al
        AutoDevVerifier.InvokeLifecycle(coinManager, "Awake");
        AutoDevVerifier.InvokeLifecycle(gameManager, "Awake");
        AutoDevVerifier.InvokeLifecycle(clockUI, "Awake");

        gameManager.SubscribeEvents();

        // 2. CoinManager ve HighScore Davranışsal Doğrulama
        int initialHighScore = PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0);
        coinManager.ResetHighScore();
        if (coinManager.HighScore != 0 || PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0) != 0)
        {
            return AutoDevCheckResult.Fail("ResetHighScore sonrasi HighScore sifirlanamadi!");
        }

        coinManager.ResetCoins();
        if (coinManager.Coins != 0)
        {
            return AutoDevCheckResult.Fail("CoinManager.ResetCoins sonrasi Coins 0 olmaliydi!");
        }

        bool coinAdded = coinManager.AddCoins(7);
        if (!coinAdded || coinManager.Coins != 7)
        {
            return AutoDevCheckResult.Fail($"AddCoins(7) basarisiz oldu. Coins={coinManager.Coins}");
        }

        if (coinManager.HighScore != 7 || PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0) != 7)
        {
            return AutoDevCheckResult.Fail($"PlayerPrefs HighScore kaydedilemedi! Beklenen: 7, Alinan: {coinManager.HighScore}, Prefs: {PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0)}");
        }

        // Tekrar az miktar ekle - HighScore artmamalı
        coinManager.ResetCoins();
        coinManager.AddCoins(3);
        if (coinManager.HighScore != 7 || PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0) != 7)
        {
            return AutoDevCheckResult.Fail($"HighScore daha kucuk skorla bozuldu! HighScore={coinManager.HighScore}");
        }

        // 3. ClockUI AddBonusTime ve OnTimerExpired Davranışsal Doğrulama
        float currentRemaining = clockUI.RemainingSeconds;
        clockUI.AddBonusTime(15f);
        if (Mathf.Abs(clockUI.RemainingSeconds - (currentRemaining + 15f)) > 0.01f)
        {
            return AutoDevCheckResult.Fail($"ClockUI.AddBonusTime kalan sureyi dogru artirmadi! Onceki: {currentRemaining}, Simdiki: {clockUI.RemainingSeconds}");
        }

        int expiredEventCount = 0;
        Action timerHandler = () => expiredEventCount++;
        clockUI.OnTimerExpired += timerHandler;

        // Süreyi sıfıra indirip tetiklenmeyi sına
        clockUI.SetRemainingSeconds(0f);
        if (expiredEventCount != 1)
        {
            clockUI.OnTimerExpired -= timerHandler;
            return AutoDevCheckResult.Fail($"OnTimerExpired tam olarak 1 kez tetiklenmeliydi! Tetiklenme sayisi: {expiredEventCount}");
        }

        // Tekrar Tick çalıştırıldığında event ikinci kez tetiklenmemeli
        clockUI.Tick(0.5f);
        if (expiredEventCount != 1)
        {
            clockUI.OnTimerExpired -= timerHandler;
            return AutoDevCheckResult.Fail($"OnTimerExpired sure sifirken tekrar tekrar tetiklendi! Tetiklenme sayisi: {expiredEventCount}");
        }
        clockUI.OnTimerExpired -= timerHandler;

        // 4. GameManager State Geçişleri ve Event Doğrulaması
        gameManager.SetState(GameState.Playing);
        if (gameManager.CurrentState != GameState.Playing)
        {
            return AutoDevCheckResult.Fail($"GameManager CurrentState Playing olmaliydi! Simdiki: {gameManager.CurrentState}");
        }

        GameState lastReportedState = GameState.Playing;
        int stateChangedEventCount = 0;
        Action<GameState> stateHandler = (state) =>
        {
            lastReportedState = state;
            stateChangedEventCount++;
        };
        gameManager.OnGameStateChanged += stateHandler;

        // Süre dolduğunda GameOver olma testi
        clockUI.AddBonusTime(10f);
        clockUI.SetRemainingSeconds(0f); // Tetiklenen OnTimerExpired, GameManager'i GameOver yapmalı
        if (gameManager.CurrentState != GameState.GameOver || lastReportedState != GameState.GameOver)
        {
            gameManager.OnGameStateChanged -= stateHandler;
            return AutoDevCheckResult.Fail($"Sure doldugunda GameManager GameOver durumuna gecmedi! CurrentState={gameManager.CurrentState}");
        }

        // Hedef altın toplandığında Victory olma testi
        gameManager.SetState(GameState.Playing);
        gameManager.TargetCoins = 20;
        coinManager.ResetCoins();
        coinManager.AddCoins(20);

        if (gameManager.CurrentState != GameState.Victory || lastReportedState != GameState.Victory)
        {
            gameManager.OnGameStateChanged -= stateHandler;
            return AutoDevCheckResult.Fail($"Hedef altin toplandiginda GameManager Victory durumuna gecmedi! CurrentState={gameManager.CurrentState}");
        }
        gameManager.OnGameStateChanged -= stateHandler;

        // 5. Test sonrası temizlik ve başlangıç durumunu geri yükleme
        gameManager.SetState(GameState.Playing);
        clockUI.SetRemainingSeconds(clockUI.StartingSeconds);
        coinManager.ResetCoins();
        if (initialHighScore > 0)
        {
            PlayerPrefs.SetInt(CoinManager.HighScoreKey, initialHighScore);
            PlayerPrefs.Save();
        }

        return AutoDevCheckResult.Pass(
            "T002 basariyla dogrulandi: GameState, GameManager, ClockUI bonus/expire ve CoinManager HighScore tam islevsel.",
            $"GameManager={gameManager.name}, CoinManager={coinManager.name}, ClockUI={clockUI.name}, HighScore={PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0)}",
            saveScene
        );
    }
}
