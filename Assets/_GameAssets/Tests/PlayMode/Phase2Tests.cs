using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_INCLUDE_TESTS
using UnityEngine.Assertions;
using UnityEngine.TestTools;
#endif

#if UNITY_INCLUDE_TESTS
public class Phase2Tests
{
    [UnityTest]
    public IEnumerator Phase2_GameLoop_HighScore_And_TimerExpiration_Work()
    {
        // 1. Geçici test sahnesi kur
        Scene testScene = SceneManager.CreateScene("Phase2_TestScene");
        SceneManager.SetActiveScene(testScene);

        // Kamera ve Işık
        GameObject camObj = new GameObject("MainCamera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.20f, 0.28f, 1f);
        camObj.transform.position = new Vector3(0f, 3f, -6f);
        camObj.transform.LookAt(Vector3.zero);

        GameObject lightObj = new GameObject("DirectionalLight");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 1.1f;
        lightObj.transform.rotation = Quaternion.Euler(45f, -25f, 0f);

        // Zemin ve görsel dekor
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TestGround";
        ground.transform.position = Vector3.zero;

        // Yöneticiler: CoinManager & GameManager
        PlayerPrefs.DeleteKey("CivCiv_HighScore");
        GameObject managersObj = new GameObject("TestManagers");
        CoinManager coinMgr = managersObj.AddComponent<CoinManager>();
        GameManager gameMgr = managersObj.AddComponent<GameManager>();
        gameMgr.SetTargetCoinsForVictory(3);

        // HUD Canvas ve Saat/Skor panelleri
        GameObject canvasObj = new GameObject("TestCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Üst HUD Barı
        GameObject topBarObj = new GameObject("TopHudPanel", typeof(RectTransform), typeof(Image));
        topBarObj.transform.SetParent(canvasObj.transform, false);
        RectTransform topRect = topBarObj.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0.05f, 0.88f);
        topRect.anchorMax = new Vector2(0.95f, 0.96f);
        topRect.offsetMin = Vector2.zero;
        topRect.offsetMax = Vector2.zero;
        topBarObj.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

        // Saat (ClockUI) ve İbre (Stick)
        GameObject clockObj = new GameObject("ClockWidget", typeof(RectTransform), typeof(Image));
        clockObj.transform.SetParent(topBarObj.transform, false);
        RectTransform clockRect = clockObj.GetComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(0.45f, 0.1f);
        clockRect.anchorMax = new Vector2(0.55f, 0.9f);
        clockRect.offsetMin = Vector2.zero;
        clockRect.offsetMax = Vector2.zero;
        clockObj.GetComponent<Image>().color = new Color(0.95f, 0.78f, 0.22f, 1f);

        GameObject stickObj = new GameObject("Stick", typeof(RectTransform), typeof(Image));
        stickObj.transform.SetParent(clockObj.transform, false);
        RectTransform stickRect = stickObj.GetComponent<RectTransform>();
        stickRect.anchorMin = new Vector2(0.48f, 0.5f);
        stickRect.anchorMax = new Vector2(0.52f, 0.9f);
        stickRect.pivot = new Vector2(0.5f, 0f);
        stickRect.offsetMin = Vector2.zero;
        stickRect.offsetMax = Vector2.zero;
        stickObj.GetComponent<Image>().color = Color.red;

        ClockUI clockUI = clockObj.AddComponent<ClockUI>();

        // Durum Göstergesi Kartı (Ortada)
        GameObject stateCardObj = new GameObject("StateIndicatorCard", typeof(RectTransform), typeof(Image));
        stateCardObj.transform.SetParent(canvasObj.transform, false);
        RectTransform cardRect = stateCardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.35f, 0.40f);
        cardRect.anchorMax = new Vector2(0.65f, 0.60f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        Image cardImg = stateCardObj.GetComponent<Image>();
        cardImg.color = new Color(0.2f, 0.75f, 0.35f, 0.9f);

        yield return null;

        Assert.AreEqual(GameState.Playing, gameMgr.CurrentState, "Başlangıç durumu Playing olmalı.");

        // 2. Bonus süre ekleme ve altın toplama -> HighScore & Victory doğrulaması
        float beforeBonus = clockUI.RemainingSeconds;
        clockUI.AddBonusTime(10f);
        Assert.IsTrue(clockUI.RemainingSeconds > beforeBonus + 9f, "AddBonusTime süreyi artırmalı.");

        coinMgr.AddCoins(3);
        yield return null;

        Assert.AreEqual(3, coinMgr.Coins, "Toplanan altın 3 olmalı.");
        Assert.AreEqual(3, coinMgr.HighScore, "HighScore 3 olarak güncellenmeli.");
        Assert.AreEqual(3, PlayerPrefs.GetInt("CivCiv_HighScore", 0), "HighScore PlayerPrefs'e kaydedilmeli.");
        Assert.AreEqual(GameState.Victory, gameMgr.CurrentState, "Hedef altına ulaşıldığında Victory tetiklenmeli.");

        // 3. Süre bitimi -> OnTimerExpired & GameOver doğrulaması
        gameMgr.SetGameState(GameState.Playing);
        bool timerExpiredEventFired = false;
        clockUI.OnTimerExpired += () => timerExpiredEventFired = true;

        clockUI.SetRemainingTime(0.02f);
        yield return new WaitForSeconds(0.08f);

        Assert.IsTrue(timerExpiredEventFired, "Süre bittiğinde OnTimerExpired tetiklenmeli.");
        Assert.AreEqual(GameState.GameOver, gameMgr.CurrentState, "Süre bittiğinde GameState.GameOver olmalı.");

        // 4. Ekran görüntüsü al
        if (!Directory.Exists("TestScreenshots"))
        {
            Directory.CreateDirectory("TestScreenshots");
        }
        ScreenCapture.CaptureScreenshot("TestScreenshots/faz2_sonucu.png");
        yield return null;
    }
}
#endif
