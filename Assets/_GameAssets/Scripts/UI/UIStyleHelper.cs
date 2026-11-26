using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Merkezi UI Stil Yardımcı Sınıfı (UIStyleHelper)
/// Tüm HUD barları, paneller, butonlar ve metinler için tutarlı altın-sarı / turuncu / koyu kehribar teması uygular.
/// </summary>
public static class UIStyleHelper
{
    private static UIThemeSO _fallbackTheme;

    /// <summary>
    /// Geçerli tema null ise çalışma zamanı varsayılan CivCiv temasını döndürür.
    /// </summary>
    public static UIThemeSO ResolveTheme(UIThemeSO theme)
    {
        if (theme != null)
        {
            return theme;
        }

        if (_fallbackTheme == null)
        {
            _fallbackTheme = ScriptableObject.CreateInstance<UIThemeSO>();
            _fallbackTheme.hideFlags = HideFlags.HideAndDontSave;
        }

        return _fallbackTheme;
    }

    /// <summary>
    /// Bir Slider barını koyu kehribar çerçeveli, altın-turuncu dolgulu temaya dönüştürür.
    /// </summary>
    public static void ApplyBarStyle(Slider slider, UIThemeSO theme, Image fillImage = null, TextMeshProUGUI valueText = null)
    {
        if (slider == null) return;
        UIThemeSO t = ResolveTheme(theme);

        // Arka plan görseli (Background)
        Transform bgTr = slider.transform.Find("Background");
        Image bgImg = bgTr != null ? bgTr.GetComponent<Image>() : slider.GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.color = t.barBackgroundColor;
            ApplyBorderAndShadow(bgImg.gameObject, t.darkAmberBorder, t.borderThickness, t.shadowColor, t.shadowOffset);
        }

        // Fill Area iç boşluğu (çerçeve içinde temiz görünüm)
        Transform fillAreaTr = slider.transform.Find("Fill Area");
        if (fillAreaTr != null)
        {
            RectTransform faRt = fillAreaTr.GetComponent<RectTransform>();
            if (faRt != null)
            {
                faRt.anchorMin = Vector2.zero;
                faRt.anchorMax = Vector2.one;
                faRt.offsetMin = new Vector2(4f, 4f);
                faRt.offsetMax = new Vector2(-4f, -4f);
            }
        }

        // Dolgu görseli (Fill)
        Image targetFill = fillImage;
        if (targetFill == null && slider.fillRect != null)
        {
            targetFill = slider.fillRect.GetComponent<Image>();
        }

        if (targetFill != null)
        {
            float normalized = slider.maxValue > slider.minValue
                ? Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value)
                : 1f;
            targetFill.color = EvaluateBarFillColor(normalized, t, 0f);
            slider.targetGraphic = targetFill;
        }

        // Bar üzerindeki değer metni
        if (valueText != null)
        {
            ApplyTextStyle(valueText, t, false);
        }
    }

    /// <summary>
    /// Normalize edilmiş doluluk oranına (0-1) göre bar dolgu rengini hesaplar.
    /// Tam: Altın-Sarı -> Orta: Turuncu -> Düşük: Kırmızı-Turuncu nabız.
    /// </summary>
    public static Color EvaluateBarFillColor(float normalizedValue, UIThemeSO theme, float pulseTime = 0f)
    {
        UIThemeSO t = ResolveTheme(theme);
        float n = Mathf.Clamp01(normalizedValue);

        if (n <= t.lowStaminaThreshold)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime);
            Color lowPeak = Color.Lerp(t.lowWarningColor, t.accentOrange, 0.35f);
            return Color.Lerp(t.lowWarningColor, lowPeak, pulse);
        }

        float midPoint = Mathf.Clamp01((n - t.lowStaminaThreshold) / Mathf.Max(0.01f, 1f - t.lowStaminaThreshold));
        return Color.Lerp(t.accentOrange, t.primaryGold, midPoint);
    }

    /// <summary>
    /// Bir paneli MenuPopUp.png (veya sıcak kehribar çerçeveli fallback) stiliyle giydirir.
    /// </summary>
    public static void ApplyPanelStyle(GameObject panel, UIThemeSO theme, bool usePopupSprite = true)
    {
        if (panel == null) return;
        UIThemeSO t = ResolveTheme(theme);

        Image img = panel.GetComponent<Image>();
        if (img == null)
        {
            img = panel.AddComponent<Image>();
        }

        if (usePopupSprite && t.panelSprite != null)
        {
            img.sprite = t.panelSprite;
            img.type = t.panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = t.panelTintColor;
            img.preserveAspect = false;
        }
        else
        {
            img.color = t.panelFallbackColor;
            ApplyBorderAndShadow(panel, t.darkAmberBorder, t.borderThickness, t.shadowColor, t.shadowOffset);
        }
    }

    /// <summary>
    /// Bir butonu yuvarlatılmış sıcak turuncu tonları, koyu kehribar kontur ve gölge ile stillendirir.
    /// </summary>
    public static void ApplyButtonStyle(Button button, UIThemeSO theme, TextMeshProUGUI label = null)
    {
        if (button == null) return;
        UIThemeSO t = ResolveTheme(theme);

        Image btnImg = button.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.color = t.buttonNormalColor;
            ApplyBorderAndShadow(button.gameObject, t.darkAmberBorder, t.borderThickness, t.shadowColor, t.shadowOffset);
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = t.buttonHighlightColor;
        colors.pressedColor = t.buttonPressedColor;
        colors.selectedColor = t.buttonHighlightColor;
        colors.disabledColor = t.buttonDisabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI btnText = label != null ? label : button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (btnText != null)
        {
            ApplyTextStyle(btnText, t, false);
            btnText.fontStyle |= FontStyles.Bold;
        }
    }

    /// <summary>
    /// TMP metnine oyunun sıcak krem-beyaz / altın başlık stili, yazı tipi ve okunaklı gradient uygular.
    /// </summary>
    public static void ApplyTextStyle(TextMeshProUGUI text, UIThemeSO theme, bool isTitle = false, float fontSizeOverride = -1f)
    {
        if (text == null) return;
        UIThemeSO t = ResolveTheme(theme);

        if (t.defaultFont != null)
        {
            text.font = t.defaultFont;
        }

        if (fontSizeOverride > 0f)
        {
            text.fontSize = fontSizeOverride;
        }

        text.color = isTitle ? t.titleTextColor : t.primaryTextColor;
        text.enableVertexGradient = true;

        if (isTitle)
        {
            text.fontStyle |= FontStyles.Bold;
            text.colorGradient = new VertexGradient(
                t.primaryTextColor,
                t.primaryTextColor,
                t.accentOrange,
                t.accentOrange
            );
        }
        else
        {
            text.colorGradient = new VertexGradient(
                t.primaryTextColor,
                t.primaryTextColor,
                t.primaryGold,
                t.primaryGold
            );
        }

        // Çalışma zamanında materyal kopyası üzerinden koyu kehribar kontur uygula (EditMode materyal sızıntısını önle)
        if (Application.isPlaying)
        {
            text.outlineColor = t.darkAmberBorder;
            text.outlineWidth = isTitle ? 0.24f : 0.16f;
        }
    }

    private static void ApplyBorderAndShadow(GameObject target, Color borderColor, Vector2 borderDistance, Color shadowColor, Vector2 shadowDistance)
    {
        if (target == null) return;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }
        outline.effectColor = borderColor;
        outline.effectDistance = borderDistance;
        outline.useGraphicAlpha = true;

        // Outline zaten Shadow'dan türer; ayrı bir Shadow bileşeni varsa onu kullan, yoksa ekle
        Shadow[] shadows = target.GetComponents<Shadow>();
        Shadow dropShadow = null;
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i].GetType() == typeof(Shadow))
            {
                dropShadow = shadows[i];
                break;
            }
        }

        if (dropShadow == null)
        {
            dropShadow = target.AddComponent<Shadow>();
        }
        dropShadow.effectColor = shadowColor;
        dropShadow.effectDistance = shadowDistance;
        dropShadow.useGraphicAlpha = true;
    }
}
