using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T007A
{
    [AutoDevCheck("T007A")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return AutoDevCheckResult.Fail("Canvas sahnede bulunamadi!");

        GameplayHUD hud = canvas.GetComponentInChildren<GameplayHUD>(true);
        if (hud == null) return AutoDevCheckResult.Fail("GameplayHUD sahnede bulunamadi (T004 on kosulu)!");

        // 1. DefaultUITheme.asset olustur veya yukle ve Sprite/Font bagla
        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
        if (theme == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_GameAssets/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
            }
            theme = ScriptableObject.CreateInstance<UIThemeSO>();
            AssetDatabase.CreateAsset(theme, UIThemeSO.DefaultAssetPath);
        }

        Sprite popupSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UIThemeSO.DefaultPopupSpritePath);
        TMP_FontAsset sceneFont = FindSceneFont(canvas);

        SerializedObject themeSO = new SerializedObject(theme);
        if (popupSprite != null) themeSO.FindProperty("panelSprite").objectReferenceValue = popupSprite;
        if (sceneFont != null) themeSO.FindProperty("defaultFont").objectReferenceValue = sceneFont;
        themeSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();

        // 2. StaminaBar ve EndGamePanel yerlesimini / referanslarini cilala
        hud.AutoResolveReferences();
        Slider slider = hud.StaminaSlider;
        if (slider == null) return AutoDevCheckResult.Fail("GameplayHUD StaminaSlider bulunamadi!");

        RectTransform sliderRt = slider.GetComponent<RectTransform>();
        sliderRt.anchorMin = Vector2.zero;
        sliderRt.anchorMax = Vector2.zero;
        sliderRt.pivot = Vector2.zero;
        sliderRt.anchoredPosition = new Vector2(36f, 36f);
        sliderRt.sizeDelta = new Vector2(260f, 30f);

        TextMeshProUGUI staminaText = hud.StaminaText;
        if (staminaText == null)
        {
            GameObject stObj = new GameObject("StaminaText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stObj.transform.SetParent(slider.transform, false);
            RectTransform stRt = stObj.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;
            staminaText = stObj.GetComponent<TextMeshProUGUI>();
            staminaText.alignment = TextAlignmentOptions.Center;
            staminaText.fontSize = 16f;
            staminaText.text = "100 / 100";
            Undo.RegisterCreatedObjectUndo(stObj, "Create StaminaText");
        }

        if (hud.EndGamePanel != null)
        {
            RectTransform panelRt = hud.EndGamePanel.GetComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(460f, 340f);
        }

        SerializedObject hudSO = new SerializedObject(hud);
        hudSO.FindProperty("_theme").objectReferenceValue = theme;
        hudSO.FindProperty("_staminaText").objectReferenceValue = staminaText;
        if (slider.fillRect != null)
        {
            hudSO.FindProperty("_staminaFillImage").objectReferenceValue = slider.fillRect.GetComponent<Image>();
        }
        hudSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);

        // 3. Temayi uygula ve sayisal dogrula
        hud.ApplyTheme(theme);

        if (theme.panelSprite == null)
            return AutoDevCheckResult.Fail("DefaultUITheme.panelSprite (MenuPopUp.png) baglanamadi!");
        if (theme.defaultFont == null)
            return AutoDevCheckResult.Fail("DefaultUITheme.defaultFont sahneden baglanamadi!");
        if (hud.Theme != theme)
            return AutoDevCheckResult.Fail("GameplayHUD._theme DefaultUITheme.asset ile eslesmiyor!");

        // Doluluk renk gecisi dogrulamasi (100% altin-sari vs 15% kirmizi-turuncu uyari)
        hud.UpdateStaminaBar(100f, 100f);
        Color fullColor = hud.StaminaFillImage.color;
        if (IsDefaultGrayOrWhite(fullColor) || fullColor.r < 0.8f || fullColor.g < 0.55f)
            return AutoDevCheckResult.Fail($"Tam stamina rengi altin-sari degil: {fullColor}");

        hud.UpdateStaminaBar(15f, 100f);
        Color lowColor = hud.StaminaFillImage.color;
        float colorDist = Vector3.Distance(new Vector3(fullColor.r, fullColor.g, fullColor.b), new Vector3(lowColor.r, lowColor.g, lowColor.b));
        if (colorDist < 0.25f || lowColor.r <= lowColor.g)
            return AutoDevCheckResult.Fail($"Dusuk stamina renk gecisi yetersiz! full={fullColor}, low={lowColor}, dist={colorDist:F2}");

        // Panel sprite, buton rengi ve font dogrulamasi
        Image panelImg = hud.EndGamePanel != null ? hud.EndGamePanel.GetComponent<Image>() : null;
        if (panelImg == null || panelImg.sprite != theme.panelSprite)
            return AutoDevCheckResult.Fail("EndGamePanel arka plani MenuPopUp.png sprite'ina baglanmadi!");

        Image btnImg = hud.RestartButton != null ? hud.RestartButton.GetComponent<Image>() : null;
        if (btnImg == null || IsDefaultGrayOrWhite(btnImg.color))
            return AutoDevCheckResult.Fail($"RestartButton rengi varsayilan gri/beyaz kaldi: {(btnImg != null ? btnImg.color.ToString() : "null")}");

        if (hud.TitleText == null || hud.TitleText.font != theme.defaultFont)
            return AutoDevCheckResult.Fail("TitleText fontu DefaultUITheme.defaultFont ile eslesmiyor!");

        // Sahneyi temiz baslangic durumuna dondur ve kaydet
        hud.UpdateStaminaBar(100f, 100f);
        if (hud.EndGamePanel != null) hud.EndGamePanel.SetActive(false);

        return AutoDevCheckResult.Pass(
            "T007A basariyla dogrulandi: UIThemeSO, UIStyleHelper ve GameplayHUD temasi aktif.",
            $"Full={ColorUtility.ToHtmlStringRGB(fullColor)}, Low={ColorUtility.ToHtmlStringRGB(lowColor)}, Dist={colorDist:F2}, Sprite={theme.panelSprite.name}",
            true
        );
    }

    private static TMP_FontAsset FindSceneFont(Canvas canvas)
    {
        TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
            {
                return texts[i].font;
            }
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static bool IsDefaultGrayOrWhite(Color c)
    {
        float spread = Mathf.Max(Mathf.Abs(c.r - c.g), Mathf.Max(Mathf.Abs(c.g - c.b), Mathf.Abs(c.r - c.b)));
        return spread < 0.08f;
    }
}
