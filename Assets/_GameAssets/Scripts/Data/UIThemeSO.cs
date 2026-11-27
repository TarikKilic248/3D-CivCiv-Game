using UnityEngine;
using TMPro;

/// <summary>
/// CivCiv 3D - Merkezi UI Tasarım Teması (ScriptableObject)
/// Oyunun sıcak altın-sarı, turuncu ve koyu kehribar çizgi film stilini tek merkezden yönetir.
/// </summary>
[CreateAssetMenu(fileName = "DefaultUITheme", menuName = "CivCiv/UI Theme")]
public class UIThemeSO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_GameAssets/Data/DefaultUITheme.asset";
    public const string DefaultPopupSpritePath = "Assets/_GameAssets/Sprites/MenuPopUp.png";

    [Header("--- Palette (Ana Renkler) ---")]
    [Tooltip("Sıcak altın-sarı (#FFD54F) - Ana vurgu ve tam bar dolgusu")]
    public Color primaryGold = new Color(1f, 0.835f, 0.310f, 1f);

    [Tooltip("Turuncu (#FF9800) - İkincil vurgu ve buton ana rengi")]
    public Color accentOrange = new Color(1f, 0.596f, 0f, 1f);

    [Tooltip("Koyu kehribar çerçeve (#6D3B00) - Bar/buton konturu ve kenarlık")]
    public Color darkAmberBorder = new Color(0.427f, 0.231f, 0f, 1f);

    [Tooltip("Bar arka plan rengi (koyu sıcak kahve/kehribar)")]
    public Color barBackgroundColor = new Color(0.243f, 0.133f, 0.024f, 0.92f);

    [Tooltip("Düşük stamina / uyarı rengi (canlı sıcak kırmızı-turuncu)")]
    public Color lowWarningColor = new Color(0.898f, 0.224f, 0.208f, 1f);

    [Header("--- Panel & Button Colors ---")]
    [Tooltip("MenuPopUp sprite kullanıldığında panel renk tonu")]
    public Color panelTintColor = Color.white;

    [Tooltip("Sprite yoksa kullanılacak sıcak krem panel arka planı")]
    public Color panelFallbackColor = new Color(0.996f, 0.945f, 0.820f, 0.96f);

    [Tooltip("Buton normal rengi (#FF9800)")]
    public Color buttonNormalColor = new Color(1f, 0.596f, 0f, 1f);

    [Tooltip("Buton vurgu (hover) rengi (#FFB74D)")]
    public Color buttonHighlightColor = new Color(1f, 0.718f, 0.302f, 1f);

    [Tooltip("Buton basılı (pressed) rengi (#E65100)")]
    public Color buttonPressedColor = new Color(0.902f, 0.318f, 0f, 1f);

    [Tooltip("Buton devre dışı rengi")]
    public Color buttonDisabledColor = new Color(0.55f, 0.42f, 0.30f, 0.65f);

    [Header("--- Typography Colors ---")]
    [Tooltip("Ana metin rengi (sıcak krem-beyaz #FFF8E1)")]
    public Color primaryTextColor = new Color(1f, 0.973f, 0.882f, 1f);

    [Tooltip("Başlık metin rengi (parlak altın-sarı #FFD54F)")]
    public Color titleTextColor = new Color(1f, 0.835f, 0.310f, 1f);

    [Tooltip("Koyu gölge / kontur rengi (#3E1F00)")]
    public Color shadowColor = new Color(0.243f, 0.122f, 0f, 0.78f);

    [Header("--- Sprites & Fonts ---")]
    [Tooltip("Ana pencere / popup arka plan görseli (MenuPopUp.png)")]
    public Sprite panelSprite;

    [Tooltip("Varsayılan TMP yazı tipi")]
    public TMP_FontAsset defaultFont;

    [Header("--- Bar Feedback ---")]
    [Tooltip("Düşük stamina renk geçiş ve nabız eşiği (0-1)")]
    [Range(0.05f, 0.6f)]
    public float lowStaminaThreshold = 0.3f;

    [Tooltip("Düşük stamina nabız hızı")]
    public float lowPulseSpeed = 7f;

    [Tooltip("Kenarlık kalınlığı")]
    public Vector2 borderThickness = new Vector2(3f, -3f);

    [Tooltip("Gölge ofseti")]
    public Vector2 shadowOffset = new Vector2(0f, -4f);

    private void OnValidate()
    {
        lowStaminaThreshold = Mathf.Clamp(lowStaminaThreshold, 0.05f, 0.6f);
        lowPulseSpeed = Mathf.Max(0.5f, lowPulseSpeed);
    }
}
