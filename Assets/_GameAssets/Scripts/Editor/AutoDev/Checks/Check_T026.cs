using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - T026 Doğrulama Kontrolü
/// Kurtarılan CivCiv İlerleme HUD Bileşeni (RescueProgressUI)
/// ChickRescueManager üzerinden civciv kurtarma tetiklendiğinde RescueProgressUI metninin güncellendiğini,
/// UIThemeSO/UIStyleHelper stilinin uygulandığını ve StepAnimation ölçek sıçramasının çalıştığını doğrular.
/// </summary>
public static class Check_T026
{
    [AutoDevCheck("T026")]
    [AutoDevCheck("ChickRescueManager uzerinden civciv kurtarma tetiklendiginde RescueProgressUI metninin guncellendigini, UIThemeSO/UIStyleHelper stilinin uygulandigini ve StepAnimation olcek sicramasinin calistigini dogrula.")]
    [AutoDevCheck("Check_ChickRescueManager uzerinden civciv kurtarma tetiklendiginde RescueProgressUI metninin guncellendigini, UIThemeSO/UIStyleHelper stilinin uygulandigini ve StepAnimation olcek sicramasinin calistigini dogrula.")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;
        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
        if (theme == null)
        {
            return AutoDevCheckResult.Fail("DefaultUITheme.asset bulunamadi (T007A on kosulu).");
        }

        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return AutoDevCheckResult.Fail("Demo.unity sahnesinde Canvas bulunamadi.");
        }

        // 1. Demo.unity sahnesinde ChickRescueManager bilesenini idempotent kur
        ChickRescueManager sceneManager = Object.FindAnyObjectByType<ChickRescueManager>();
        if (sceneManager == null)
        {
            GameObject managerObj = new GameObject("ChickRescueManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create ChickRescueManager");
            sceneManager = managerObj.AddComponent<ChickRescueManager>();
            if (scenePlayer != null) sceneManager.PlayerTransform = scenePlayer.transform;
            EditorUtility.SetDirty(managerObj);
            saveScene = true;
        }

        // 2. Demo.unity Canvas altinda RescueProgressUI bilesenini idempotent kur (sag-ust SettingsUI altinda)
        RescueProgressUI sceneUI = canvas.GetComponentInChildren<RescueProgressUI>(true);
        if (sceneUI == null)
        {
            sceneUI = CreateRescueProgressUIHierarchy(canvas.transform, theme);
            saveScene = true;
        }
        else
        {
            if (sceneUI.EnsureVFXSetup())
            {
                EditorUtility.SetDirty(sceneUI.gameObject);
                saveScene = true;
            }
        }

        SerializedObject uiSO = new SerializedObject(sceneUI);
        uiSO.FindProperty("_theme").objectReferenceValue = theme;
        uiSO.FindProperty("_rescueManager").objectReferenceValue = sceneManager;
        uiSO.ApplyModifiedPropertiesWithoutUndo();
        sceneUI.ApplyTheme(theme);
        EditorUtility.SetDirty(sceneUI);

        // Stil & Tema Kontrolü (Düz gri/mavi varsayılan UI olmamalı)
        if (sceneUI.ProgressText == null || sceneUI.ProgressText.font != theme.defaultFont)
        {
            return AutoDevCheckResult.Fail("RescueProgressUI ProgressText fontu DefaultUITheme ile eslesmiyor.");
        }
        if (sceneUI.ProgressBar == null || sceneUI.ProgressFillImage == null)
        {
            return AutoDevCheckResult.Fail("RescueProgressUI ProgressBar veya ProgressFillImage eksik.");
        }
        if (sceneUI.CelebrationVFX == null)
        {
            return AutoDevCheckResult.Fail("RescueProgressUI CelebrationVFX kurulumu eksik.");
        }

        // 3. Davranışsal Doğrulama (İzole Test Ortamı)
        GameObject testRoot = null;
        ChickRescueManager prevManagerInstance = ChickRescueManager.Instance;
        RescueProgressUI prevUIInstance = RescueProgressUI.Instance;

        try
        {
            Vector3 testPos = new Vector3(0f, 960f, 0f);
            testRoot = new GameObject("T026_BehavioralTestRoot");
            testRoot.transform.position = testPos;

            // Test Oyuncusu
            GameObject testPlayer = new GameObject("TestPlayer");
            testPlayer.transform.SetParent(testRoot.transform, false);

            // Test Rescue Manager
            GameObject testManagerObj = new GameObject("TestRescueManager");
            testManagerObj.transform.SetParent(testRoot.transform, false);
            ChickRescueManager testManager = testManagerObj.AddComponent<ChickRescueManager>();
            testManager.PlayerTransform = testPlayer.transform;
            ChickRescueManager.Instance = testManager;

            // Test Canvas ve RescueProgressUI
            GameObject testCanvasObj = new GameObject("TestCanvas", typeof(Canvas));
            testCanvasObj.transform.SetParent(testRoot.transform, false);
            RescueProgressUI testUI = CreateRescueProgressUIHierarchy(testCanvasObj.transform, theme);
            testUI.BindRescueManager(testManager);

            // Test SFX Kancası Hazırlığı
            AudioClip testSFX = AudioClip.Create("T026_TestRescueClip", 441, 1, 44100, false);
            testUI.RescueSFX = testSFX;
            testUI.AllRescuedSFX = testSFX;

            // 3 Adet Test Civcivi Oluştur
            LostChickNPC[] testChicks = new LostChickNPC[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject chickObj = new GameObject($"TestChick_{i + 1}");
                chickObj.transform.SetParent(testRoot.transform, false);
                chickObj.transform.position = testPos + new Vector3(i * 3f, 0f, 10f);
                testChicks[i] = chickObj.AddComponent<LostChickNPC>();
                testManager.RegisterChick(testChicks[i]);
            }

            // Test A: Başlangıç durumu (0 / 3)
            if (testUI.TotalChicks != 3 || testUI.CurrentRescued != 0)
            {
                return AutoDevCheckResult.Fail($"Baslangic civciv sayilari hatali: Rescued={testUI.CurrentRescued}, Total={testUI.TotalChicks}");
            }
            if (!testUI.ProgressText.text.Contains("0") || !testUI.ProgressText.text.Contains("3"))
            {
                return AutoDevCheckResult.Fail($"ProgressText baslangic metni eslesmedi: '{testUI.ProgressText.text}'");
            }
            if (!Mathf.Approximately(testUI.ProgressBar.value, 0f))
            {
                return AutoDevCheckResult.Fail($"ProgressBar baslangic degeri 0 degil: {testUI.ProgressBar.value}");
            }

            // Test B: İlk civciv kurtarılması (1 / 3) + Sıçrama, VFX ve SFX kancası
            testManager.RescueChick(testChicks[0]);

            if (testUI.CurrentRescued != 1)
            {
                return AutoDevCheckResult.Fail($"Ilk kurtarma sonrasi CurrentRescued 1 olmaliydi: {testUI.CurrentRescued}");
            }
            if (!testUI.ProgressText.text.Contains("1") || !testUI.ProgressText.text.Contains("3"))
            {
                return AutoDevCheckResult.Fail($"Ilk kurtarma sonrasi metin '1 / 3' icermiyor: '{testUI.ProgressText.text}'");
            }
            if (Mathf.Abs(testUI.ProgressBar.value - (1f / 3f)) > 0.05f)
            {
                return AutoDevCheckResult.Fail($"ProgressBar degeri 1/3 e yaklasamadi: {testUI.ProgressBar.value}");
            }
            if (testUI.PunchAmount <= 0.01f)
            {
                return AutoDevCheckResult.Fail("Civciv kurtarildiginda PunchAmount olcek sicramasi tetiklenmedi.");
            }
            if (testUI.CelebrationVfxPlayCount < 1)
            {
                return AutoDevCheckResult.Fail("Civciv kurtarildiginda CelebrationVFX tetiklenmedi.");
            }
            if (testUI.SfxPlayCount < 1)
            {
                return AutoDevCheckResult.Fail("Civciv kurtarildiginda SettingsUI.PlaySFX ses kancasi tetiklenmedi.");
            }

            // Test C: StepAnimation prosedürel sönümleme ve ölçek değişimi
            testUI.StepAnimation(0.04f);
            if (testUI.PanelRoot.transform.localScale.x <= 1.01f)
            {
                return AutoDevCheckResult.Fail($"StepAnimation aninda olcek buyumesi aktif degil: {testUI.PanelRoot.transform.localScale.x}");
            }

            // Test D: Tüm civcivlerin kurtarılması (3 / 3) + Tamamlanma durumu
            testManager.RescueChick(testChicks[1]);
            testManager.RescueChick(testChicks[2]);

            if (testUI.CurrentRescued != 3 || !testUI.IsCompleted)
            {
                return AutoDevCheckResult.Fail($"Tum civcivler kurtarildiginda IsCompleted true olmaliydi: Rescued={testUI.CurrentRescued}, Completed={testUI.IsCompleted}");
            }
            if (!testUI.ProgressText.text.Contains("3"))
            {
                return AutoDevCheckResult.Fail($"Tum civcivler kurtarildiginda metin '3' icermiyor: '{testUI.ProgressText.text}'");
            }
            if (testUI.ProgressBar.value < 0.99f)
            {
                return AutoDevCheckResult.Fail($"Tum civcivler kurtarildiginda bar degeri 1.0 olmaliydi: {testUI.ProgressBar.value}");
            }

            // Animasyonun tamamen sönümlenmesi (1 saniye)
            testUI.StepAnimation(1.0f);
            if (Mathf.Abs(testUI.PanelRoot.transform.localScale.x - 1.0f) > 0.01f)
            {
                return AutoDevCheckResult.Fail($"StepAnimation sonrasinda olcek orijinal haline donmedi: {testUI.PanelRoot.transform.localScale.x}");
            }

            return AutoDevCheckResult.Pass(
                "RescueProgressUI metin guncellemesi, UIThemeSO/UIStyleHelper stilleri, StepAnimation sicramasi ve VFX/SFX kancalari basariyla dogrulandi.",
                saveScene: saveScene);
        }
        finally
        {
            ChickRescueManager.Instance = prevManagerInstance;
            // RescueProgressUI.Instance has a private setter; it is cleared in OnDestroy.
            _ = prevUIInstance;

            if (testRoot != null)
            {
                Object.DestroyImmediate(testRoot);
            }
        }
    }

    private static RescueProgressUI CreateRescueProgressUIHierarchy(Transform canvasTransform, UIThemeSO theme)
    {
        // Panel Kökü (Sağ-Üst SettingsUI altına yerleşim)
        GameObject panelObj = new GameObject("RescueProgressPanel", typeof(RectTransform), typeof(Image), typeof(RescueProgressUI));
        panelObj.transform.SetParent(canvasTransform, false);

        RectTransform pRt = panelObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(1f, 1f);
        pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(1f, 1f);
        pRt.anchoredPosition = new Vector2(-28f, -92f);
        pRt.sizeDelta = new Vector2(185f, 54f);

        // Başlık Metni (Civcivler)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(0f, 1f);
        tRt.pivot = new Vector2(0f, 1f);
        tRt.anchoredPosition = new Vector2(14f, -8f);
        tRt.sizeDelta = new Vector2(100f, 20f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "Civcivler";
        titleTmp.fontSize = 15f;
        titleTmp.alignment = TextAlignmentOptions.Left;

        // İlerleme Metni (0 / 3)
        GameObject progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
        progObj.transform.SetParent(panelObj.transform, false);
        RectTransform prRt = progObj.GetComponent<RectTransform>();
        prRt.anchorMin = new Vector2(1f, 1f);
        prRt.anchorMax = new Vector2(1f, 1f);
        prRt.pivot = new Vector2(1f, 1f);
        prRt.anchoredPosition = new Vector2(-14f, -8f);
        prRt.sizeDelta = new Vector2(65f, 20f);

        TextMeshProUGUI progTmp = progObj.GetComponent<TextMeshProUGUI>();
        progTmp.text = "0 / 0";
        progTmp.fontSize = 15f;
        progTmp.alignment = TextAlignmentOptions.Right;

        // İlerleme Barı (Slider)
        GameObject sliderObj = new GameObject("ProgressBar", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(panelObj.transform, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(0f, 8f);
        sRt.sizeDelta = new Vector2(-24f, 14f);

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(2f, 2f);
        faRt.offsetMax = new Vector2(-2f, -2f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fillObj.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.sizeDelta = Vector2.zero;

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.interactable = false;
        slider.fillRect = fRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        RescueProgressUI ui = panelObj.GetComponent<RescueProgressUI>();
        ui.AutoResolveReferences();
        ui.EnsureVFXSetup();
        ui.ApplyTheme(theme);

        Undo.RegisterCreatedObjectUndo(panelObj, "Create RescueProgressPanel");
        return ui;
    }
}
