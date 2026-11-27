using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Kombo Çarpanı HUD Bileşeni (ComboUI)
/// ComboManager.OnComboUpdated olayını dinleyerek çarpan metnini, seri bilgisini ve süre barını günceller.
/// UIThemeSO / UIStyleHelper ile oyunun sıcak altın-sarı / turuncu / koyu kehribar stiline uyar.
/// </summary>
public class ComboUI : MonoBehaviour
{
    [Header("--- Theme & Style ---")]
    [SerializeField] private UIThemeSO _theme;

    [Header("--- UI Elements ---")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TextMeshProUGUI _multiplierText;
    [SerializeField] private TextMeshProUGUI _streakText;
    [SerializeField] private Slider _timerSlider;
    [SerializeField] private Image _timerFillImage;

    [Header("--- Target References ---")]
    [SerializeField] private ComboManager _comboManager;

    [Header("--- Procedural Feedback ---")]
    [Tooltip("Kombo arttığında panelin anlık büyüme ölçeği")]
    [SerializeField] private float _punchScale = 1.18f;
    [Tooltip("Büyüme / eğim tepkisinin sönümleme hızı")]
    [SerializeField] private float _punchDecaySpeed = 8f;
    [Tooltip("Kombo artışında hafif sallanma açısı (derece)")]
    [SerializeField] private float _punchTiltAngle = 4.5f;

    private int _lastStreak;
    private float _currentNormalizedTime;
    private float _punchAmount;
    private float _pulseTimer;

    public UIThemeSO Theme => _theme;
    public GameObject PanelRoot => _panelRoot;
    public TextMeshProUGUI MultiplierText => _multiplierText;
    public TextMeshProUGUI StreakText => _streakText;
    public Slider TimerSlider => _timerSlider;
    public Image TimerFillImage => _timerFillImage;
    public ComboManager TargetComboManager => _comboManager;

    private void Awake()
    {
        AutoResolveReferences();
        ApplyTheme(_theme);
    }

    private void Start()
    {
        SubscribeEvents();
        SyncFromManager();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        StepAnimation(Time.unscaledDeltaTime);
    }

    public void ApplyTheme(UIThemeSO themeOverride = null)
    {
        if (themeOverride != null)
        {
            _theme = themeOverride;
        }

        UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);
        AutoResolveReferences();

        if (_panelRoot != null)
        {
            UIStyleHelper.ApplyPanelStyle(_panelRoot, activeTheme, false);
            Image panelImg = _panelRoot.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = activeTheme.barBackgroundColor;
            }
        }

        if (_multiplierText != null)
        {
            UIStyleHelper.ApplyTextStyle(_multiplierText, activeTheme, true);
        }

        if (_streakText != null)
        {
            UIStyleHelper.ApplyTextStyle(_streakText, activeTheme, false);
        }

        if (_timerSlider != null)
        {
            UIStyleHelper.ApplyBarStyle(_timerSlider, activeTheme, _timerFillImage);
        }
    }

    public void AutoResolveReferences()
    {
        if (_panelRoot == null)
        {
            _panelRoot = gameObject;
        }

        if (_multiplierText == null)
        {
            Transform tr = transform.Find("MultiplierText");
            if (tr != null) _multiplierText = tr.GetComponent<TextMeshProUGUI>();
        }

        if (_streakText == null)
        {
            Transform tr = transform.Find("StreakText");
            if (tr != null) _streakText = tr.GetComponent<TextMeshProUGUI>();
        }

        if (_timerSlider == null)
        {
            _timerSlider = GetComponentInChildren<Slider>(true);
        }

        if (_timerSlider != null && _timerFillImage == null && _timerSlider.fillRect != null)
        {
            _timerFillImage = _timerSlider.fillRect.GetComponent<Image>();
        }

        if (_comboManager == null)
        {
            _comboManager = ComboManager.Instance != null
                ? ComboManager.Instance
                : FindAnyObjectByType<ComboManager>();
        }
    }

    public void BindComboManager(ComboManager manager)
    {
        UnsubscribeEvents();
        _comboManager = manager;
        SubscribeEvents();
        SyncFromManager();
    }

    public void SubscribeEvents()
    {
        if (_comboManager == null)
        {
            _comboManager = ComboManager.Instance != null
                ? ComboManager.Instance
                : FindAnyObjectByType<ComboManager>();
        }

        if (_comboManager != null)
        {
            _comboManager.OnComboUpdated -= UpdateComboDisplay;
            _comboManager.OnComboUpdated += UpdateComboDisplay;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_comboManager != null)
        {
            _comboManager.OnComboUpdated -= UpdateComboDisplay;
        }
    }

    public void SyncFromManager()
    {
        if (_comboManager != null)
        {
            UpdateComboDisplay(
                _comboManager.CurrentStreak,
                _comboManager.CurrentMultiplier,
                _comboManager.NormalizedTimeRemaining
            );
        }
        else
        {
            UpdateComboDisplay(0, 1f, 0f);
        }
    }

    public void UpdateComboDisplay(int streak, float multiplier, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        _currentNormalizedTime = clampedTime;

        if (streak > _lastStreak)
        {
            _punchAmount = 1f;
        }
        _lastStreak = streak;

        if (_multiplierText != null)
        {
            _multiplierText.text = $"x{multiplier:0.#}";
        }

        if (_streakText != null)
        {
            _streakText.text = streak > 0 ? $"COMBO {streak}!" : "COMBO";
        }

        if (_timerSlider != null)
        {
            _timerSlider.minValue = 0f;
            _timerSlider.maxValue = 1f;
            _timerSlider.value = clampedTime;
        }

        if (_timerFillImage != null)
        {
            UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);
            _timerFillImage.color = UIStyleHelper.EvaluateBarFillColor(
                streak > 0 ? clampedTime : 1f,
                activeTheme,
                _pulseTimer
            );
        }
    }

    public void StepAnimation(float deltaTime)
    {
        if (deltaTime <= 0f || _panelRoot == null)
            return;

        UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);

        if (_punchAmount > 0.001f)
        {
            _punchAmount = Mathf.MoveTowards(_punchAmount, 0f, deltaTime * _punchDecaySpeed);
            float scale = Mathf.Lerp(1f, _punchScale, _punchAmount);
            float tilt = Mathf.Sin(_punchAmount * Mathf.PI) * _punchTiltAngle;
            _panelRoot.transform.localScale = new Vector3(scale, scale, 1f);
            _panelRoot.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
        else if (_panelRoot.transform.localScale != Vector3.one || _panelRoot.transform.localRotation != Quaternion.identity)
        {
            _panelRoot.transform.localScale = Vector3.one;
            _panelRoot.transform.localRotation = Quaternion.identity;
        }

        if (_lastStreak > 0 && _timerFillImage != null && _currentNormalizedTime <= activeTheme.lowStaminaThreshold)
        {
            _pulseTimer += deltaTime * activeTheme.lowPulseSpeed;
            _timerFillImage.color = UIStyleHelper.EvaluateBarFillColor(_currentNormalizedTime, activeTheme, _pulseTimer);
        }
        else
        {
            _pulseTimer = 0f;
        }
    }
}
