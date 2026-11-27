using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T027_Polish
{
    private const string ParticleMatPath = "Assets/_GameAssets/Metarials/VFXParticleMat.mat";

    [AutoDevCheck("T027_Polish")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        GameplayHUD hud = Object.FindAnyObjectByType<GameplayHUD>();
        if (canvas == null || hud == null)
            return AutoDevCheckResult.Fail("Canvas veya GameplayHUD sahnede bulunamadi!");

        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
        if (theme == null)
            return AutoDevCheckResult.Fail("DefaultUITheme.asset bulunamadi!");

        // 1. ChickEggThrower tus cakismasini gider (E -> Q)
        ChickEggThrower thrower = Object.FindAnyObjectByType<ChickEggThrower>();
        if (thrower != null)
        {
            SerializedObject throwSo = new SerializedObject(thrower);
            SerializedProperty keyProp = throwSo.FindProperty("_throwKey");
            if (keyProp != null && keyProp.intValue != (int)KeyCode.Q)
            {
                keyProp.intValue = (int)KeyCode.Q;
                throwSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(thrower);
            }
        }

        // 2. Tum ParticleSystemRenderer bilesenlerine URP uyumlu materyal bagla (pembe efekt onleme)
        Material vfxMat = EnsureParticleMaterial();
        int fixedParticles = 0;
        if (vfxMat != null)
        {
            foreach (ParticleSystemRenderer psr in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include))
            {
                if (psr != null && psr.sharedMaterial == null)
                {
                    psr.sharedMaterial = vfxMat;
                    EditorUtility.SetDirty(psr);
                    fixedParticles++;
                }
            }
        }

        // 3. EndGamePanel uzerine MainMenuButton ekle & duzenle
        EnsureEndGameMainMenuButton(hud, theme);

        // 4. StartMenuUI ve Kontrol Rehberi kurulumu
        StartMenuUI startMenu = EnsureStartMenuHierarchy(canvas.transform, theme);

        // 5. Build & Player ayarlarini yapilandir
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(AutoDevVerifier.DefaultScenePath, true)
        };
        PlayerSettings.productName = "3D CivCiv Game";
        PlayerSettings.companyName = "Tarik Kilic";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        // 6. Davranissal dogrulama
        AutoDevVerifier.InvokeLifecycle(startMenu, "Awake");
        AutoDevVerifier.InvokeLifecycle(startMenu, "Start");
        if (!StartMenuUI.IsOpen || !Mathf.Approximately(Time.timeScale, 0f))
            return AutoDevCheckResult.Fail("Baslangicta StartMenuUI acilmali ve Time.timeScale == 0 olmaliydi.");

        startMenu.ShowControls();
        if (startMenu.ControlsCard == null || !startMenu.ControlsCard.activeSelf)
            return AutoDevCheckResult.Fail("Kontroller karti acilmadi!");

        startMenu.ShowMainCard();
        startMenu.StartGame();
        if (StartMenuUI.IsOpen || !Mathf.Approximately(Time.timeScale, 1f))
            return AutoDevCheckResult.Fail("StartGame() sonrasi menu kapanmali ve Time.timeScale == 1 olmaliydi.");

        if (startMenu.ControlsHintBar == null || !startMenu.ControlsHintBar.activeSelf)
            return AutoDevCheckResult.Fail("Oyun ici kontrol ipucu cubugu aktif olmaliydi.");

        // Edit Mode sahne kaydi icin acilis durumuna getir
        startMenu.ShowMenu();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Sahne butunluk kontrolu (missing script / null mesh / null material)
        string integrityErr = ProjectCleanupAuditor.CheckSceneIntegrity(AutoDevVerifier.DefaultScenePath);
        if (!string.IsNullOrEmpty(integrityErr))
            return AutoDevCheckResult.Fail("Sahne butunluk hatasi tespit edildi!", integrityErr);

        return AutoDevCheckResult.Pass(
            "T027_Polish PASS: Baslangic menusu, kontroller, BuildSettings ve temiz sahne dogrulandi.",
            $"FixedParticleMats={fixedParticles}, BuildScene={AutoDevVerifier.DefaultScenePath}",
            saveScene: true);
    }

    private static Material EnsureParticleMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
        if (mat != null) return mat;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        if (sh == null) return null;

        mat = new Material(sh) { color = Color.white };
        AssetDatabase.CreateAsset(mat, ParticleMatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureEndGameMainMenuButton(GameplayHUD hud, UIThemeSO theme)
    {
        if (hud.EndGamePanel == null) return;
        Transform endTr = hud.EndGamePanel.transform;
        Transform restartTr = endTr.Find("RestartButton");
        if (restartTr != null)
        {
            RectTransform rRt = restartTr.GetComponent<RectTransform>();
            rRt.anchoredPosition = new Vector2(-95f, -95f);
            rRt.sizeDelta = new Vector2(165f, 44f);
        }

        Transform mmTr = endTr.Find("MainMenuButton");
        Button mmBtn;
        if (mmTr == null)
        {
            mmBtn = CreateButton(endTr, "MainMenuButton", new Vector2(95f, -95f), new Vector2(165f, 44f), "Ana Menu", theme.defaultFont);
        }
        else
        {
            mmBtn = mmTr.GetComponent<Button>();
            RectTransform mRt = mmTr.GetComponent<RectTransform>();
            mRt.anchoredPosition = new Vector2(95f, -95f);
            mRt.sizeDelta = new Vector2(165f, 44f);
        }

        SerializedObject hudSo = new SerializedObject(hud);
        hudSo.FindProperty("_mainMenuButton").objectReferenceValue = mmBtn;
        hudSo.ApplyModifiedPropertiesWithoutUndo();
        hud.ApplyTheme(theme);
        EditorUtility.SetDirty(hud);
    }

    private static StartMenuUI EnsureStartMenuHierarchy(Transform canvasTransform, UIThemeSO theme)
    {
        Transform existingRoot = canvasTransform.Find("StartMenuPanel");
        GameObject rootObj = existingRoot != null
            ? existingRoot.gameObject
            : new GameObject("StartMenuPanel", typeof(RectTransform), typeof(StartMenuUI));

        if (existingRoot == null)
        {
            rootObj.transform.SetParent(canvasTransform, false);
            Undo.RegisterCreatedObjectUndo(rootObj, "Create StartMenuPanel");
        }

        RectTransform fullRt = rootObj.GetComponent<RectTransform>();
        fullRt.anchorMin = Vector2.zero;
        fullRt.anchorMax = Vector2.one;
        fullRt.offsetMin = Vector2.zero;
        fullRt.offsetMax = Vector2.zero;
        rootObj.transform.SetAsLastSibling();

        GameObject overlay = EnsureChildPanel(rootObj.transform, "StartMenuRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bgDim = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
        bgDim.color = new Color(0.08f, 0.05f, 0.02f, 0.58f);

        // Main Card
        GameObject mainCard = EnsureCenteredCard(overlay.transform, "MainCard", new Vector2(500f, 380f));
        TextMeshProUGUI title = EnsureTMP(mainCard.transform, "TitleText", new Vector2(0f, 130f), new Vector2(440f, 54f), 36f, "3D CivCiv Game", theme.defaultFont);
        TextMeshProUGUI sub = EnsureTMP(mainCard.transform, "SubtitleText", new Vector2(0f, 82f), new Vector2(440f, 32f), 17f, "Altinlari Topla, Kayip CivCivleri Kurtar!", theme.defaultFont);
        TextMeshProUGUI stats = EnsureTMP(mainCard.transform, "StatsText", new Vector2(0f, 40f), new Vector2(440f, 28f), 15f, "Rekor Altin: 0 | En Iyi Yildiz: 0/3 | Kurtarilan: 0", theme.defaultFont);

        Button playBtn = EnsureChildButton(mainCard.transform, "PlayButton", new Vector2(0f, -20f), new Vector2(240f, 48f), "OYNA", theme.defaultFont);
        Button ctrlBtn = EnsureChildButton(mainCard.transform, "ControlsButton", new Vector2(0f, -80f), new Vector2(240f, 44f), "KONTROLLER", theme.defaultFont);
        Button quitBtn = EnsureChildButton(mainCard.transform, "QuitButton", new Vector2(0f, -136f), new Vector2(240f, 40f), "CIKIS", theme.defaultFont);

        // Controls Card
        GameObject ctrlCard = EnsureCenteredCard(overlay.transform, "ControlsCard", new Vector2(540f, 400f));
        EnsureTMP(ctrlCard.transform, "ControlsTitle", new Vector2(0f, 152f), new Vector2(460f, 40f), 28f, "KONTROLLER", theme.defaultFont);
        string bodyStr =
            "WASD / Ok Tuslari : Hareket\n" +
            "Space             : Ziplama / Havada Cift Ziplama\n" +
            "Space (Basili)    : Kanat Suzulmesi (Glide)\n" +
            "Left Shift        : Hizli Dash (Stamina)\n" +
            "Ctrl / C          : Yerde Kayma / Havada Yere Cakilma\n" +
            "F                 : Gaga Saldirisi\n" +
            "Q / Sag Tik       : Ziplama Yumurtasi Firlatma\n" +
            "E                 : Etkilesim (Civciv Kurtar / Dugme)\n" +
            "V / ESC           : Kamera (TPS/FPS)  /  Ayarlar";
        TextMeshProUGUI bodyTxt = EnsureTMP(ctrlCard.transform, "ControlsBodyText", new Vector2(0f, 4f), new Vector2(480f, 235f), 16f, bodyStr, theme.defaultFont);
        bodyTxt.alignment = TextAlignmentOptions.Left;
        Button backBtn = EnsureChildButton(ctrlCard.transform, "BackButton", new Vector2(0f, -148f), new Vector2(200f, 44f), "GERI", theme.defaultFont);
        ctrlCard.SetActive(false);

        // In-game subtle bottom-right hint bar
        GameObject hintBar = EnsureChildPanel(rootObj.transform, "ControlsHintBar", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(430f, 32f));
        RectTransform hbRt = hintBar.GetComponent<RectTransform>();
        hbRt.pivot = new Vector2(1f, 0f);
        TextMeshProUGUI hintTxt = EnsureTMP(hintBar.transform, "HintText", Vector2.zero, new Vector2(414f, 26f), 13f, "[ESC] Ayarlar  |  [V] Kamera  |  [F] Gaga  |  [Q] Yumurta  |  [E] Etkilesim", theme.defaultFont);
        hintBar.SetActive(false);

        StartMenuUI ui = rootObj.GetComponent<StartMenuUI>() ?? rootObj.AddComponent<StartMenuUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("_theme").objectReferenceValue = theme;
        so.FindProperty("_menuRoot").objectReferenceValue = overlay;
        so.FindProperty("_mainCard").objectReferenceValue = mainCard;
        so.FindProperty("_controlsCard").objectReferenceValue = ctrlCard;
        so.FindProperty("_controlsHintBar").objectReferenceValue = hintBar;
        so.FindProperty("_titleText").objectReferenceValue = title;
        so.FindProperty("_subtitleText").objectReferenceValue = sub;
        so.FindProperty("_statsText").objectReferenceValue = stats;
        so.FindProperty("_controlsBodyText").objectReferenceValue = bodyTxt;
        so.FindProperty("_hintBarText").objectReferenceValue = hintTxt;
        so.FindProperty("_playButton").objectReferenceValue = playBtn;
        so.FindProperty("_controlsButton").objectReferenceValue = ctrlBtn;
        so.FindProperty("_controlsBackButton").objectReferenceValue = backBtn;
        so.FindProperty("_quitButton").objectReferenceValue = quitBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        ui.ApplyTheme(theme);
        EditorUtility.SetDirty(ui);
        return ui;
    }

    private static GameObject EnsureChildPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        Transform tr = parent.Find(name);
        GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (tr == null) go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return go;
    }

    private static GameObject EnsureCenteredCard(Transform parent, string name, Vector2 size)
    {
        GameObject go = EnsureChildPanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        go.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        return go;
    }

    private static TextMeshProUGUI EnsureTMP(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, string text, TMP_FontAsset font)
    {
        Transform tr = parent.Find(name);
        GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (tr == null) go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button EnsureChildButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, TMP_FontAsset font)
    {
        Transform tr = parent.Find(name);
        return tr != null ? tr.GetComponent<Button>() : CreateButton(parent, name, pos, size, label, font);
    }

    private static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = EnsureTMP(go.transform, "Text", Vector2.zero, size, 19f, label, font);
        RectTransform tRt = tmp.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
