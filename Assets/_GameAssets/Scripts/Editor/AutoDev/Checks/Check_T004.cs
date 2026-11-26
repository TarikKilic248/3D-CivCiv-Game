using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T004
{
    [AutoDevCheck("T004")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return AutoDevCheckResult.Fail("Canvas sahnede bulunamadi!");

        bool saveScene = false;
        GameplayHUD hud = canvas.GetComponentInChildren<GameplayHUD>(true);
        if (hud == null)
        {
            GameObject hudObj = new GameObject("GameplayHUD", typeof(RectTransform));
            hudObj.transform.SetParent(canvas.transform, false);
            RectTransform hRt = hudObj.GetComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = Vector2.one;
            hRt.offsetMin = Vector2.zero; hRt.offsetMax = Vector2.zero;
            hud = hudObj.AddComponent<GameplayHUD>();
            Undo.RegisterCreatedObjectUndo(hudObj, "Create GameplayHUD");
            saveScene = true;
        }

        TMP_FontAsset font = canvas.GetComponentInChildren<TextMeshProUGUI>(true)?.font;

        // StaminaBar
        Slider slider = hud.GetComponentInChildren<Slider>(true);
        if (slider == null)
        {
            GameObject sObj = new GameObject("StaminaBar", typeof(RectTransform), typeof(Slider));
            sObj.transform.SetParent(hud.transform, false);
            RectTransform sRt = sObj.GetComponent<RectTransform>();
            sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.zero; sRt.pivot = Vector2.zero;
            sRt.anchoredPosition = new Vector2(30f, 30f); sRt.sizeDelta = new Vector2(220f, 24f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sObj.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(3f, 3f); faRt.offsetMax = new Vector2(-3f, -3f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.2f, 0.85f, 0.35f, 1f);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.sizeDelta = Vector2.zero;

            slider = sObj.GetComponent<Slider>();
            slider.fillRect = fillRt; slider.targetGraphic = fillImg;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0f; slider.maxValue = 100f; slider.value = 100f;
            saveScene = true;
        }

        // EndGamePanel
        Transform panelTr = hud.transform.Find("EndGamePanel");
        GameObject panelObj = panelTr != null ? panelTr.gameObject : null;
        if (panelObj == null)
        {
            panelObj = new GameObject("EndGamePanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(hud.transform, false);
            RectTransform pRt = panelObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f); pRt.anchorMax = new Vector2(0.5f, 0.5f); pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = Vector2.zero; pRt.sizeDelta = new Vector2(400f, 300f);
            panelObj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            CreateTMP(panelObj.transform, "TitleText", new Vector2(0f, 90f), new Vector2(360f, 50f), 32, "VICTORY!", font);
            CreateTMP(panelObj.transform, "ScoreText", new Vector2(0f, 25f), new Vector2(360f, 35f), 20, "Score: 0", font);
            CreateTMP(panelObj.transform, "HighScoreText", new Vector2(0f, -25f), new Vector2(360f, 35f), 18, "High Score: 0", font);

            GameObject btn = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(panelObj.transform, false);
            RectTransform bRt = btn.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -90f); bRt.sizeDelta = new Vector2(180f, 44f);
            btn.GetComponent<Image>().color = new Color(0.2f, 0.65f, 0.35f, 1f);
            CreateTMP(btn.transform, "Text", Vector2.zero, Vector2.zero, 20, "Restart", font, true);

            panelObj.SetActive(false);
            saveScene = true;
        }

        hud.AutoResolveReferences();
        DashController dash = UnityEngine.Object.FindAnyObjectByType<DashController>();
        GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        CoinManager cm = UnityEngine.Object.FindAnyObjectByType<CoinManager>();
        if (dash == null || gm == null || cm == null) return AutoDevCheckResult.Fail("Gerekli yonetici bilesenler (Dash/GM/Coin) bulunamadi!");

        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("_staminaSlider").objectReferenceValue = slider;
        so.FindProperty("_endGamePanel").objectReferenceValue = panelObj;
        so.FindProperty("_titleText").objectReferenceValue = panelObj.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_scoreText").objectReferenceValue = panelObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_highScoreText").objectReferenceValue = panelObj.transform.Find("HighScoreText")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_restartButton").objectReferenceValue = panelObj.transform.Find("RestartButton")?.GetComponent<Button>();
        so.FindProperty("_dashController").objectReferenceValue = dash;
        so.FindProperty("_gameManager").objectReferenceValue = gm;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(hud);

        AutoDevVerifier.InvokeLifecycle(hud, "Awake");
        AutoDevVerifier.InvokeLifecycle(hud, "Start");
        hud.SubscribeEvents();

        if (hud.EndGamePanel.activeSelf) return AutoDevCheckResult.Fail("EndGamePanel baslangicta kapali olmali!");

        // 1. Stamina Slider Canlı Güncelleme Doğrulaması
        dash.SetStamina(45f);
        if (Mathf.Abs(hud.StaminaSlider.value - 45f) > 0.1f) return AutoDevCheckResult.Fail($"Stamina 45 iken Slider={hud.StaminaSlider.value}");
        dash.SetStamina(90f);
        if (Mathf.Abs(hud.StaminaSlider.value - 90f) > 0.1f) return AutoDevCheckResult.Fail($"Stamina 90 iken Slider={hud.StaminaSlider.value}");

        // 2. GameOver Durum ve Panel Doğrulaması
        gm.SetState(GameState.Playing);
        gm.TriggerGameOver();
        if (!hud.EndGamePanel.activeSelf) return AutoDevCheckResult.Fail("GameOver durumunda EndGamePanel acilmadi!");
        if (hud.TitleText == null || !hud.TitleText.text.ToUpper().Contains("GAME OVER")) return AutoDevCheckResult.Fail("GameOver basligi yanlis!");
        if (hud.ScoreText == null || string.IsNullOrEmpty(hud.ScoreText.text)) return AutoDevCheckResult.Fail("GameOver skor metni bos!");

        // 3. Victory Durum ve Skor Doğrulaması
        gm.SetState(GameState.Playing);
        hud.EndGamePanel.SetActive(false);
        cm.ResetCoins();
        cm.AddCoins(12);
        gm.TriggerVictory();
        if (!hud.EndGamePanel.activeSelf) return AutoDevCheckResult.Fail("Victory durumunda EndGamePanel acilmadi!");
        if (hud.TitleText == null || !hud.TitleText.text.ToUpper().Contains("VICTORY")) return AutoDevCheckResult.Fail("Victory basligi yanlis!");
        if (hud.ScoreText == null || !hud.ScoreText.text.Contains("12")) return AutoDevCheckResult.Fail($"ScoreText 12 altini gostermedi: {hud.ScoreText.text}");

        // Temizlik ve durum sıfırlama
        gm.SetState(GameState.Playing);
        hud.EndGamePanel.SetActive(false);
        cm.ResetCoins();
        dash.ResetStamina();

        return AutoDevCheckResult.Pass("T004 basariyla dogrulandi: StaminaBar ve EndGamePanel tam calisiyor.", $"Slider={hud.StaminaSlider.value}", saveScene);
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, string text, TMP_FontAsset font, bool stretch = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; }
        else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text; tmp.fontSize = fontSize; tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }
}
