using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T010
{
    [AutoDevCheck("T010")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        CoinManager coinManager = Object.FindAnyObjectByType<CoinManager>();
        if (canvas == null || scenePlayer == null || coinManager == null)
            return AutoDevCheckResult.Fail("Sahne on kosullari eksik (Canvas, PlayerController veya CoinManager).");

        bool saveScene = false;
        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
        if (theme == null)
            return AutoDevCheckResult.Fail("DefaultUITheme.asset bulunamadi (T007A on kosulu).");

        // 1. ComboManager kurulumu
        ComboManager comboManager = Object.FindAnyObjectByType<ComboManager>();
        if (comboManager == null)
        {
            GameObject cmObj = new GameObject("ComboManager");
            comboManager = cmObj.AddComponent<ComboManager>();
            Undo.RegisterCreatedObjectUndo(cmObj, "Create ComboManager");
            saveScene = true;
        }
        PlayerHealth sceneHealth = scenePlayer.GetComponent<PlayerHealth>();
        SerializedObject cmSO = new SerializedObject(comboManager);
        cmSO.FindProperty("_coinManager").objectReferenceValue = coinManager;
        cmSO.FindProperty("_playerHealth").objectReferenceValue = sceneHealth;
        cmSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(comboManager);

        // 2. CoinMagnet kurulumu (sahne karakteri)
        CoinMagnet sceneMagnet = scenePlayer.GetComponent<CoinMagnet>();
        if (sceneMagnet == null)
        {
            sceneMagnet = Undo.AddComponent<CoinMagnet>(scenePlayer.gameObject);
            saveScene = true;
        }
        if (sceneMagnet.EnsureVFXSetup()) saveScene = true;
        if (sceneMagnet.MagnetAuraParticles == null)
            return AutoDevCheckResult.Fail("Sahne karakterinde CoinMagnetVFX ParticleSystem eksik.");
        EditorUtility.SetDirty(sceneMagnet);

        // 3. ComboUI kurulumu (sag-alt kose, UIThemeSO/UIStyleHelper temali)
        ComboUI comboUI = canvas.GetComponentInChildren<ComboUI>(true);
        if (comboUI == null)
        {
            comboUI = CreateComboUIHierarchy(canvas.transform, theme);
            saveScene = true;
        }
        SerializedObject uiSO = new SerializedObject(comboUI);
        uiSO.FindProperty("_theme").objectReferenceValue = theme;
        uiSO.FindProperty("_comboManager").objectReferenceValue = comboManager;
        uiSO.ApplyModifiedPropertiesWithoutUndo();
        comboUI.ApplyTheme(theme);
        EditorUtility.SetDirty(comboUI);

        // Tema dogrulamasi (duz gri/beyaz olmamali, font eslesmeli)
        Image fillImg = comboUI.TimerFillImage;
        if (fillImg == null || IsGrayOrWhite(fillImg.color))
            return AutoDevCheckResult.Fail("ComboUI TimerFillImage temali degil veya varsayilan gri/beyaz.");
        if (comboUI.MultiplierText == null || comboUI.MultiplierText.font != theme.defaultFont)
            return AutoDevCheckResult.Fail("ComboUI MultiplierText fontu DefaultUITheme ile eslesmiyor.");

        // 4. Davranissal dogrulama
        GameObject tempHealthObj = null;
        GameObject tempMagnetObj = null;
        GameObject nearCoinObj = null;
        GameObject farCoinObj = null;
        int originalCoins = coinManager.Coins;
        try
        {
            tempHealthObj = new GameObject("T010_TempHealth", typeof(Rigidbody), typeof(PlayerHealth));
            PlayerHealth testHealth = tempHealthObj.GetComponent<PlayerHealth>();
            AutoDevVerifier.InvokeLifecycle(testHealth, "Awake");
            AutoDevVerifier.InvokeLifecycle(comboManager, "Awake");
            comboManager.BindTargets(coinManager, testHealth);
            comboUI.BindComboManager(comboManager);
            comboManager.ResetCombo();

            // A: Ardisik 3 altin -> seri ve carpan artisi + ComboUI guncellemesi
            coinManager.AddCoins(1);
            coinManager.AddCoins(1);
            coinManager.AddCoins(1);
            if (comboManager.CurrentStreak != 3 || comboManager.CurrentMultiplier < 1.99f)
                return AutoDevCheckResult.Fail($"3 ardisik altin sonrasi streak=3, mult>=2 olmaliydi. Streak={comboManager.CurrentStreak}, Mult={comboManager.CurrentMultiplier:F2}");
            if (!comboUI.MultiplierText.text.Contains("2") || comboUI.TimerSlider.value < 0.9f)
                return AutoDevCheckResult.Fail($"ComboUI guncellenmedi. Text={comboUI.MultiplierText.text}, Slider={comboUI.TimerSlider.value:F2}");

            // B: Sure dolunca sifirlanma
            comboManager.Tick(comboManager.ComboDuration + 0.5f);
            if (comboManager.CurrentStreak != 0 || !Mathf.Approximately(comboManager.CurrentMultiplier, 1f))
                return AutoDevCheckResult.Fail($"Sure dolunca kombo sifirlanmadi. Streak={comboManager.CurrentStreak}, Mult={comboManager.CurrentMultiplier:F2}");

            // C: Hasar alinca sifirlanma
            coinManager.AddCoins(1);
            coinManager.AddCoins(1);
            if (comboManager.CurrentStreak != 2 || comboManager.CurrentMultiplier <= 1f)
                return AutoDevCheckResult.Fail("Hasar oncesi kombo aktif degil.");
            testHealth.TakeDamage(20);
            if (comboManager.CurrentStreak != 0 || !Mathf.Approximately(comboManager.CurrentMultiplier, 1f))
                return AutoDevCheckResult.Fail($"Hasar sonrasi kombo sifirlanmadi. Streak={comboManager.CurrentStreak}, Mult={comboManager.CurrentMultiplier:F2}");

            // D: CoinMagnet yakin altini ceker, SuperMagnet uzak altini da ceker
            Vector3 origin = new Vector3(0f, 960f, 0f);
            tempMagnetObj = new GameObject("T010_TempMagnet");
            tempMagnetObj.transform.position = origin;
            CoinMagnet testMagnet = tempMagnetObj.AddComponent<CoinMagnet>();
            AutoDevVerifier.InvokeLifecycle(testMagnet, "Awake");

            nearCoinObj = new GameObject("T010_NearCoin", typeof(CoinCollect));
            nearCoinObj.transform.position = origin + new Vector3(0f, 0.8f, 4.5f);
            farCoinObj = new GameObject("T010_FarCoin", typeof(CoinCollect));
            farCoinObj.transform.position = origin + new Vector3(0f, 0.8f, 9.0f);

            testMagnet.RefreshCoinCache();
            float nearBefore = Vector3.Distance(nearCoinObj.transform.position, origin + Vector3.up * 0.8f);
            float farBefore = Vector3.Distance(farCoinObj.transform.position, origin + Vector3.up * 0.8f);
            for (int i = 0; i < 3; i++) testMagnet.StepMagnet(0.05f);

            float nearAfter = Vector3.Distance(nearCoinObj.transform.position, origin + Vector3.up * 0.8f);
            float farMid = Vector3.Distance(farCoinObj.transform.position, origin + Vector3.up * 0.8f);
            if (nearAfter >= nearBefore - 0.5f)
                return AutoDevCheckResult.Fail($"CoinMagnet yakin altini cekmedi. Before={nearBefore:F2}, After={nearAfter:F2}");
            if (Mathf.Abs(farMid - farBefore) > 0.05f)
                return AutoDevCheckResult.Fail($"Normal miknatis menzil disi altini cekmemeliydi. Before={farBefore:F2}, Mid={farMid:F2}");

            testMagnet.ActivateSuperMagnet(4f);
            if (!testMagnet.IsSuperMagnetActive || testMagnet.EffectiveRadius <= testMagnet.BaseRadius)
                return AutoDevCheckResult.Fail("SuperMagnet aktiflesmedi veya yaricapi artmadi.");
            for (int i = 0; i < 3; i++) testMagnet.StepMagnet(0.05f);

            float farAfter = Vector3.Distance(farCoinObj.transform.position, origin + Vector3.up * 0.8f);
            if (farAfter >= farBefore - 0.5f)
                return AutoDevCheckResult.Fail($"SuperMagnet uzak altini cekmedi. Before={farBefore:F2}, After={farAfter:F2}");

            return AutoDevCheckResult.Pass(
                "T010 basariyla dogrulandi: ComboManager, ComboUI (temali) ve CoinMagnet/SuperMagnet calisiyor.",
                $"Near={nearBefore:F2}->{nearAfter:F2}, Far={farBefore:F2}->{farAfter:F2}, SuperR={testMagnet.EffectiveRadius:F1}",
                saveScene: saveScene
            );
        }
        finally
        {
            comboManager.BindTargets(coinManager, sceneHealth);
            comboManager.ResetCombo();
            comboUI.BindComboManager(comboManager);
            comboUI.SyncFromManager();
            coinManager.ResetCoins();
            if (originalCoins > 0) coinManager.AddCoins(originalCoins);
            if (tempHealthObj != null) Object.DestroyImmediate(tempHealthObj);
            if (tempMagnetObj != null) Object.DestroyImmediate(tempMagnetObj);
            if (nearCoinObj != null) Object.DestroyImmediate(nearCoinObj);
            if (farCoinObj != null) Object.DestroyImmediate(farCoinObj);
        }
    }

    private static ComboUI CreateComboUIHierarchy(Transform canvasTransform, UIThemeSO theme)
    {
        GameObject panel = new GameObject("ComboPanel", typeof(RectTransform), typeof(Image), typeof(ComboUI));
        panel.transform.SetParent(canvasTransform, false);
        RectTransform pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(1f, 0f);
        pRt.anchorMax = new Vector2(1f, 0f);
        pRt.pivot = new Vector2(1f, 0f);
        pRt.anchoredPosition = new Vector2(-36f, 36f);
        pRt.sizeDelta = new Vector2(210f, 74f);

        TextMeshProUGUI multText = CreateTMP(panel.transform, "MultiplierText", new Vector2(-12f, 14f), new Vector2(80f, 36f), 28f, "x1", TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        TextMeshProUGUI streakText = CreateTMP(panel.transform, "StreakText", new Vector2(12f, 14f), new Vector2(115f, 28f), 17f, "COMBO", TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

        GameObject sliderObj = new GameObject("ComboTimerBar", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(panel.transform, false);
        RectTransform slRt = sliderObj.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0f, 0f); slRt.anchorMax = new Vector2(1f, 0f); slRt.pivot = new Vector2(0.5f, 0f);
        slRt.anchoredPosition = new Vector2(0f, 10f); slRt.sizeDelta = new Vector2(-24f, 16f);

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one; faRt.offsetMin = new Vector2(3f, 3f); faRt.offsetMax = new Vector2(-3f, -3f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.interactable = false;
        slider.fillRect = fRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0f;

        ComboUI ui = panel.GetComponent<ComboUI>();
        ui.AutoResolveReferences();
        ui.ApplyTheme(theme);
        Undo.RegisterCreatedObjectUndo(panel, "Create ComboPanel");
        return ui;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, string text, TextAlignmentOptions align, Vector2 anchor, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.alignment = align;
        return tmp;
    }

    private static bool IsGrayOrWhite(Color c)
    {
        return Mathf.Max(Mathf.Abs(c.r - c.g), Mathf.Max(Mathf.Abs(c.g - c.b), Mathf.Abs(c.r - c.b))) < 0.08f;
    }
}
