using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T009
{
    [AutoDevCheck("T009")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (canvas == null || scenePlayer == null)
            return AutoDevCheckResult.Fail("Sahne on kosullari eksik (Canvas veya PlayerController).");

        PlayerHealth sceneHealth = scenePlayer.GetComponent<PlayerHealth>();
        if (sceneHealth == null)
            return AutoDevCheckResult.Fail("Sahne karakterinde PlayerHealth bileseni eksik.");

        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
        if (theme == null)
            return AutoDevCheckResult.Fail("DefaultUITheme.asset bulunamadi (T007A on kosulu).");

        bool saveScene = false;

        // 1. HealthUI ve HealthPanel kurulumu / referans baglantisi
        HealthUI healthUI = canvas.GetComponentInChildren<HealthUI>(true);
        if (healthUI == null)
        {
            healthUI = CreateHealthUIHierarchy(canvas.transform, theme, sceneHealth);
            saveScene = true;
        }

        SerializedObject uiSO = new SerializedObject(healthUI);
        uiSO.FindProperty("_theme").objectReferenceValue = theme;
        uiSO.FindProperty("_playerHealth").objectReferenceValue = sceneHealth;
        uiSO.ApplyModifiedPropertiesWithoutUndo();
        healthUI.ApplyTheme(theme);
        EditorUtility.SetDirty(healthUI);

        // UI bilesenlerinin tema ve nesne kontrolu
        if (healthUI.HealthText == null || healthUI.HealthText.font != theme.defaultFont)
            return AutoDevCheckResult.Fail("HealthUI HealthText fontu DefaultUITheme ile eslesmiyor.");

        if (healthUI.TotalHearts != 5)
            return AutoDevCheckResult.Fail($"HealthUI toplam kalp sayisi 5 olmaliydi. Mevcut: {healthUI.TotalHearts}");

        // 2. Davranissal Dogrulama (Hasar alma, iyilesme, aktif ikon sayisi ve metin guncellemesi)
        GameObject tempPlayerObj = null;
        try
        {
            tempPlayerObj = new GameObject("T009_TempPlayer", typeof(Rigidbody), typeof(PlayerHealth));
            PlayerHealth testHealth = tempPlayerObj.GetComponent<PlayerHealth>();
            AutoDevVerifier.InvokeLifecycle(testHealth, "Awake");

            healthUI.BindPlayerHealth(testHealth);

            // A: Baslangic 100/100 tam can
            if (healthUI.ActiveHeartCount != 5)
                return AutoDevCheckResult.Fail($"Baslangicta ActiveHeartCount 5 olmaliydi. Mevcut: {healthUI.ActiveHeartCount}");
            if (!healthUI.HealthText.text.Contains("100"))
                return AutoDevCheckResult.Fail($"Baslangicta can metni 100 icermeliydi. Metin: {healthUI.HealthText.text}");
            if (healthUI.HeartIcons[4].color != healthUI.ActiveHeartColor)
                return AutoDevCheckResult.Fail("Tam canda 5. kalp aktif renkte olmaliydi.");

            // B: Hasar alma (20 hasar -> 80 can, 4 aktif kalp)
            testHealth.TakeDamage(20);
            if (healthUI.ActiveHeartCount != 4)
                return AutoDevCheckResult.Fail($"TakeDamage(20) sonrasi ActiveHeartCount 4 olmaliydi. Mevcut: {healthUI.ActiveHeartCount}");
            if (!healthUI.HealthText.text.Contains("80"))
                return AutoDevCheckResult.Fail($"TakeDamage(20) sonrasi metin 80 icermeliydi. Metin: {healthUI.HealthText.text}");
            if (healthUI.HeartIcons[4].color != healthUI.DepletedHeartColor)
                return AutoDevCheckResult.Fail("Hasar sonrasi 5. kalp sönük/tükenmiş renkte olmaliydi.");

            // C: Daha fazla hasar (40 hasar daha -> 40 can, 2 aktif kalp)
            for (int i = 0; i < 20; i++) testHealth.StepFeedback(0.06f);
            testHealth.TakeDamage(40);
            if (healthUI.ActiveHeartCount != 2)
                return AutoDevCheckResult.Fail($"TakeDamage(40) sonrasi ActiveHeartCount 2 olmaliydi. Mevcut: {healthUI.ActiveHeartCount}");
            if (!healthUI.HealthText.text.Contains("40"))
                return AutoDevCheckResult.Fail($"Metin 40 icermeliydi. Metin: {healthUI.HealthText.text}");

            // D: Iyilesme (30 iyilesme -> 70 can, 4 aktif kalp)
            testHealth.Heal(30);
            if (healthUI.ActiveHeartCount != 4)
                return AutoDevCheckResult.Fail($"Heal(30) sonrasi ActiveHeartCount 4 olmaliydi. Mevcut: {healthUI.ActiveHeartCount}");
            if (!healthUI.HealthText.text.Contains("70"))
                return AutoDevCheckResult.Fail($"Heal(30) sonrasi metin 70 icermeliydi. Metin: {healthUI.HealthText.text}");

            // E: Olumcul hasar (100 hasar -> 0 can, 0 aktif kalp)
            for (int i = 0; i < 20; i++) testHealth.StepFeedback(0.06f);
            testHealth.TakeDamage(100);
            if (healthUI.ActiveHeartCount != 0)
                return AutoDevCheckResult.Fail($"Olum sonrasi ActiveHeartCount 0 olmaliydi. Mevcut: {healthUI.ActiveHeartCount}");
            if (!healthUI.HealthText.text.Contains("0"))
                return AutoDevCheckResult.Fail($"Olum sonrasi metin 0 icermeliydi. Metin: {healthUI.HealthText.text}");

            return AutoDevCheckResult.Pass(
                "T009 basariyla dogrulandi: HealthUI kalp ikonlari, can metni ve PlayerHealth entegrasyonu tam.",
                $"Hearts={healthUI.ActiveHeartCount}/5, Text={healthUI.HealthText.text}",
                saveScene: saveScene
            );
        }
        finally
        {
            healthUI.BindPlayerHealth(sceneHealth);
            healthUI.UpdateHealthUI(sceneHealth.CurrentHealth, sceneHealth.MaxHealth);
            if (tempPlayerObj != null) Object.DestroyImmediate(tempPlayerObj);
        }
    }

    private static HealthUI CreateHealthUIHierarchy(Transform canvasTransform, UIThemeSO theme, PlayerHealth sceneHealth)
    {
        GameObject panelObj = new GameObject("HealthPanel", typeof(RectTransform), typeof(Image), typeof(HealthUI));
        panelObj.transform.SetParent(canvasTransform, false);

        RectTransform pRt = panelObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 1f);
        pRt.anchorMax = new Vector2(0f, 1f);
        pRt.pivot = new Vector2(0f, 1f);
        pRt.anchoredPosition = new Vector2(36f, -95f);
        pRt.sizeDelta = new Vector2(210f, 62f);

        GameObject heartsContainer = new GameObject("HeartsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        heartsContainer.transform.SetParent(panelObj.transform, false);
        RectTransform hcRt = heartsContainer.GetComponent<RectTransform>();
        hcRt.anchorMin = new Vector2(0f, 0.5f);
        hcRt.anchorMax = new Vector2(0f, 0.5f);
        hcRt.pivot = new Vector2(0f, 0.5f);
        hcRt.anchoredPosition = new Vector2(16f, 10f);
        hcRt.sizeDelta = new Vector2(178f, 26f);

        HorizontalLayoutGroup hlg = heartsContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Sprite heartFilled = HealthUI.CreateHeartSprite(false);
        Sprite heartEmpty = HealthUI.CreateHeartSprite(true);

        Image[] heartIcons = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject heartObj = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(Image));
            heartObj.transform.SetParent(heartsContainer.transform, false);
            RectTransform hRt = heartObj.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(24f, 24f);

            Image img = heartObj.GetComponent<Image>();
            img.sprite = heartFilled;
            img.color = new Color(0.95f, 0.22f, 0.25f, 1f);
            heartIcons[i] = img;
        }

        GameObject textObj = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panelObj.transform, false);
        RectTransform tRt = textObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 0f);
        tRt.anchoredPosition = new Vector2(0f, 7f);
        tRt.sizeDelta = new Vector2(-20f, 18f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = "100 / 100";
        text.fontSize = 14f;
        text.alignment = TextAlignmentOptions.Center;

        HealthUI ui = panelObj.GetComponent<HealthUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("_theme").objectReferenceValue = theme;
        so.FindProperty("_panelRoot").objectReferenceValue = panelObj;
        so.FindProperty("_heartsContainer").objectReferenceValue = hcRt;
        so.FindProperty("_healthText").objectReferenceValue = text;
        so.FindProperty("_heartSprite").objectReferenceValue = heartFilled;
        so.FindProperty("_emptyHeartSprite").objectReferenceValue = heartEmpty;
        so.FindProperty("_playerHealth").objectReferenceValue = sceneHealth;

        SerializedProperty iconsProp = so.FindProperty("_heartIcons");
        iconsProp.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = heartIcons[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        ui.ApplyTheme(theme);
        Undo.RegisterCreatedObjectUndo(panelObj, "Create HealthPanel");
        return ui;
    }
}
