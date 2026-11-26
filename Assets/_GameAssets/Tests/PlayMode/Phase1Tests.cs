using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Assertions;
#if CIVCIV_PLAYMODE_TESTS
using NUnit.Framework;
using UnityEngine.TestTools;
#endif

public class Phase1Tests
{
#if CIVCIV_PLAYMODE_TESTS
    [UnityTest]
#endif
    public IEnumerator Phase1_PlayerStats_And_Dash_Mechanics_Work()
    {
        // 1. Geçici test sahnesi kur
        Scene testScene = SceneManager.CreateScene("Phase1_TestScene");
        SceneManager.SetActiveScene(testScene);

        // Kamera ve Işık
        GameObject camObj = new GameObject("MainCamera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.24f, 0.32f, 1f);
        camObj.transform.position = new Vector3(0f, 3.5f, -6.5f);
        camObj.transform.LookAt(new Vector3(0f, 1f, 2f));

        GameObject lightObj = new GameObject("DirectionalLight");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 1.2f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Zemin
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TestGround";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(4f, 1f, 4f);

        // Karakter + PlayerStatsSO + DashController
        PlayerStatsSO stats = ScriptableObject.CreateInstance<PlayerStatsSO>();
        stats.DashForce = 20f;
        stats.DashStaminaCost = 35f;
        stats.MaxStamina = 100f;
        stats.ValidateStats();

        GameObject playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObj.name = "TestPlayer";
        playerObj.transform.position = new Vector3(0f, 1.05f, 0f);

        Rigidbody rb = playerObj.AddComponent<Rigidbody>();
        rb.freezeRotation = true;

        StateController stateCtrl = playerObj.AddComponent<StateController>();
        PlayerController playerCtrl = playerObj.AddComponent<PlayerController>();
        playerCtrl.ApplyStatsFromSO(stats);

        DashController dashCtrl = playerObj.AddComponent<DashController>();

        // Basit bilgilendirme HUD'u (Görsel doğrulama için)
        GameObject canvasObj = new GameObject("TestCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject barBgObj = new GameObject("StaminaBarBG", typeof(RectTransform), typeof(Image));
        barBgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = barBgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.05f, 0.90f);
        bgRect.anchorMax = new Vector2(0.30f, 0.94f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        barBgObj.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        GameObject barFillObj = new GameObject("StaminaBarFill", typeof(RectTransform), typeof(Image));
        barFillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRect = barFillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        Image fillImg = barFillObj.GetComponent<Image>();
        fillImg.color = new Color(0.2f, 0.85f, 0.35f, 1f);

        yield return new WaitForFixedUpdate();
        yield return null;

        float initialStamina = dashCtrl.CurrentStamina;
        Vector3 initialPos = playerObj.transform.position;

        UnityEngine.Assertions.Assert.AreApproximatelyEqual(100f, initialStamina, 0.1f, "Başlangıç staminası 100 olmalı.");
        UnityEngine.Assertions.Assert.IsTrue(dashCtrl.CanDash(), "Karakter başlangıçta dash atabilmeli.");

        // 2. Dash mekaniğini tetikle
        dashCtrl.ExecuteDash();

        fillRect.anchorMax = new Vector2(dashCtrl.CurrentStamina / dashCtrl.MaxStamina, 1f);

        UnityEngine.Assertions.Assert.IsTrue(dashCtrl.CurrentStamina < initialStamina, "Dash sonrası stamina azalmalı.");
        UnityEngine.Assertions.Assert.AreEqual(PlayerState.Dash, stateCtrl.GetCurrentState(), "Durum PlayerState.Dash olmalı.");

        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.1f);

        UnityEngine.Assertions.Assert.IsTrue(playerObj.transform.position.z > initialPos.z + 0.05f, "Karakter ileri yönde (Z+) hareket etmiş olmalı.");

        // 3. Ekran görüntüsü al
        if (!Directory.Exists("TestScreenshots"))
        {
            Directory.CreateDirectory("TestScreenshots");
        }
        ScreenCapture.CaptureScreenshot("TestScreenshots/faz1_sonucu.png");
        yield return null;
    }
}
