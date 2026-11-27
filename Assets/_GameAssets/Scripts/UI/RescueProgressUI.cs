using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Kurtarılan Civciv İlerleme HUD Bileşeni (RescueProgressUI)
/// ChickRescueManager olaylarını dinleyerek kurtarılan ve toplam civciv sayısını,
/// ilerleme barını ve çizgi film kartı temasını anlık günceller.
/// Civciv kurtarıldığında prosedürel ölçek sıçraması (StepAnimation),
/// ParticleSystem kutlama efekti ve SettingsUI.PlaySFX ses kancası sunar.
/// </summary>
[DisallowMultipleComponent]
public class RescueProgressUI : MonoBehaviour
{
    public static RescueProgressUI Instance { get; private set; }

    [Header("--- Theme & Style ---")]
    [Tooltip("UIThemeSO merkezi renk ve font teması")]
    [SerializeField] private UIThemeSO _theme;

    [Header("--- UI References ---")]
    [Tooltip("Panel kök nesnesi (çizgi film kartı)")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("Kayıp civciv başlık metni (örn: Civcivler)")]
    [SerializeField] private TextMeshProUGUI _titleText;

    [Tooltip("Kurtarılan / toplam civciv metni (örn: 1 / 3)")]
    [SerializeField] private TextMeshProUGUI _progressText;

    [Tooltip("Kurtarma ilerleme göstergesi barı")]
    [SerializeField] private Slider _progressBar;

    [Tooltip("İlerleme barı dolgu görseli")]
    [SerializeField] private Image _progressFillImage;

    [Tooltip("Civciv veya hedef simgesi")]
    [SerializeField] private Image _iconImage;

    [Header("--- Target References ---")]
    [Tooltip("Dinlenecek civciv kurtarma yöneticisi")]
    [SerializeField] private ChickRescueManager _rescueManager;

    [Header("--- Feedback & VFX ---")]
    [Tooltip("Kurtarma anında patlayan kutlama partikül efekti")]
    [SerializeField] private ParticleSystem _celebrationVFX;

    [Tooltip("Her civciv kurtarıldığında çalınacak ses efekti")]
    [SerializeField] private AudioClip _rescueSFX;

    [Tooltip("Tüm civcivler kurtarıldığında çalınacak kutlama ses efekti")]
    [SerializeField] private AudioClip _allRescuedSFX;

    [Header("--- Procedural Motion (StepAnimation) ---")]
    [Tooltip("Kurtarma anında kartın anlık büyüme ölçeği")]
    [SerializeField] private float _punchScale = 1.22f;

    [Tooltip("Ölçek sıçraması sönümleme hızı")]
    [SerializeField] private float _punchDecaySpeed = 7.5f;

    [Tooltip("Kurtarma anında hafif çizgi film sallantı açısı")]
    [SerializeField] private float _punchTiltAngle = 4.0f;

    [Header("--- Text Formats ---")]
    [SerializeField] private string _progressFormat = "{0} / {1}";
    [SerializeField] private string _allRescuedTitle = "Civcivler Güvende!";

    // Runtime Durumları
    private int _lastRescuedCount = -1;
    private int _currentRescued = 0;
    private int _totalChicks = 0;
    private float _punchAmount = 0f;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;

    // Public Properties & Doğrulama Sayaçları
    public UIThemeSO Theme => _theme;
    public GameObject PanelRoot => _panelRoot;
    public TextMeshProUGUI TitleText => _titleText;
    public TextMeshProUGUI ProgressText => _progressText;
    public Slider ProgressBar => _progressBar;
    public Image ProgressFillImage => _progressFillImage;
    public Image IconImage => _iconImage;
    public ParticleSystem CelebrationVFX => _celebrationVFX;
    public AudioClip RescueSFX { get => _rescueSFX; set => _rescueSFX = value; }
    public AudioClip AllRescuedSFX { get => _allRescuedSFX; set => _allRescuedSFX = value; }
    public ChickRescueManager TargetRescueManager => _rescueManager;
    public float PunchAmount => _punchAmount;
    public float PunchScale => _punchScale;
    public int CurrentRescued => _currentRescued;
    public int TotalChicks => _totalChicks;
    public bool IsCompleted => _totalChicks > 0 && _currentRescued >= _totalChicks;
    public int SfxPlayCount { get; private set; }
    public int CelebrationVfxPlayCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoResolveReferences();

        if (_panelRoot != null)
        {
            _baseScale = _panelRoot.transform.localScale;
            _baseRotation = _panelRoot.transform.localRotation;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
        }

        EnsureVFXSetup();
        ApplyTheme(_theme);
    }

    private void Start()
    {
        AutoResolveReferences();
        SubscribeEvents();
        SyncFromManager();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        SyncFromManager();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        ResetVisualPoseImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
        StepAnimation(dt);
    }

    /// <summary>
    /// Prosedürel ölçek sıçraması ve açısal salınım animasyonunu zamanla söndürür.
    /// Editör testleri ve çalışma zamanı için deterministiktir.
    /// </summary>
    public void StepAnimation(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (_punchAmount > 0.0001f)
        {
            _punchAmount = Mathf.Max(0f, _punchAmount - deltaTime * _punchDecaySpeed);
            float currentScale = 1f + (_punchScale - 1f) * _punchAmount;
            float tilt = Mathf.Sin(_punchAmount * Mathf.PI * 3f) * _punchTiltAngle * _punchAmount;

            if (_panelRoot != null)
            {
                _panelRoot.transform.localScale = _baseScale * currentScale;
                _panelRoot.transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, tilt);
            }
        }
        else if (_panelRoot != null && (_panelRoot.transform.localScale != _baseScale || _panelRoot.transform.localRotation != _baseRotation))
        {
            _panelRoot.transform.localScale = _baseScale;
            _panelRoot.transform.localRotation = _baseRotation;
        }
    }

    /// <summary>
    /// Kurtarma anında ölçek sıçraması nabzını tetikler.
    /// </summary>
    public void TriggerPunch(float multiplier = 1f)
    {
        _punchAmount = Mathf.Clamp01(1f * multiplier);
    }

    /// <summary>
    /// Panel poz ve ölçeğini hemen başlangıç durumuna döndürür.
    /// </summary>
    public void ResetVisualPoseImmediate()
    {
        _punchAmount = 0f;
        if (_panelRoot != null)
        {
            _panelRoot.transform.localScale = _baseScale;
            _panelRoot.transform.localRotation = _baseRotation;
        }
    }

    /// <summary>
    /// Testler ve dinamik kurulumlar için ChickRescueManager hedefini bağlar.
    /// </summary>
    public void BindRescueManager(ChickRescueManager manager)
    {
        UnsubscribeEvents();
        _rescueManager = manager;
        SubscribeEvents();
        SyncFromManager();
    }

    public void SubscribeEvents()
    {
        if (_rescueManager == null)
        {
            _rescueManager = ChickRescueManager.Instance != null
                ? ChickRescueManager.Instance
                : FindAnyObjectByType<ChickRescueManager>();
        }

        if (_rescueManager != null)
        {
            _rescueManager.OnRescueProgressChanged -= HandleRescueProgressChanged;
            _rescueManager.OnRescueProgressChanged += HandleRescueProgressChanged;

            _rescueManager.OnAllChicksRescued -= HandleAllChicksRescued;
            _rescueManager.OnAllChicksRescued += HandleAllChicksRescued;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_rescueManager != null)
        {
            _rescueManager.OnRescueProgressChanged -= HandleRescueProgressChanged;
            _rescueManager.OnAllChicksRescued -= HandleAllChicksRescued;
        }
    }

    /// <summary>
    /// Yöneticideki mevcut değerleri arayüze senkronize eder.
    /// </summary>
    public void SyncFromManager()
    {
        if (_rescueManager != null)
        {
            UpdateProgressUI(_rescueManager.RescuedChickCount, _rescueManager.TotalChickCount, false);
        }
    }

    private void HandleRescueProgressChanged(int rescued, int total)
    {
        bool isNewRescue = rescued > _lastRescuedCount && _lastRescuedCount >= 0;
        UpdateProgressUI(rescued, total, isNewRescue);
    }

    private void HandleAllChicksRescued()
    {
        TriggerPunch(1.3f);
        PlayCelebrationVFX();

        if (_allRescuedSFX != null)
        {
            SettingsUI.PlaySFX(_allRescuedSFX);
            SfxPlayCount++;
        }
        else if (_rescueSFX != null)
        {
            SettingsUI.PlaySFX(_rescueSFX);
            SfxPlayCount++;
        }

        if (_titleText != null)
        {
            _titleText.text = _allRescuedTitle;
        }
    }

    /// <summary>
    /// Metin, bar ve prosedürel geri bildirimleri günceller.
    /// </summary>
    public void UpdateProgressUI(int rescued, int total, bool animate = true)
    {
        _currentRescued = rescued;
        _totalChicks = total;

        if (_progressText != null)
        {
            _progressText.text = string.Format(_progressFormat, rescued, total);
        }

        if (_progressBar != null)
        {
            float normalized = total > 0 ? Mathf.Clamp01((float)rescued / total) : 0f;
            _progressBar.minValue = 0f;
            _progressBar.maxValue = 1f;
            _progressBar.value = normalized;

            if (_progressFillImage != null)
            {
                _progressFillImage.color = UIStyleHelper.EvaluateBarFillColor(normalized, _theme);
            }
        }

        if (animate && rescued > 0)
        {
            TriggerPunch();
            PlayCelebrationVFX();

            if (_rescueSFX != null)
            {
                SettingsUI.PlaySFX(_rescueSFX);
                SfxPlayCount++;
            }
        }

        _lastRescuedCount = rescued;
    }

    public void PlayCelebrationVFX()
    {
        CelebrationVfxPlayCount++;
        if (_celebrationVFX != null)
        {
            _celebrationVFX.Play();
        }
    }

    /// <summary>
    /// Kutlama ParticleSystem nesnesini Unity 6 standartlarında (new MainModule çağrılmadan) idempotent kurar.
    /// </summary>
    public bool EnsureVFXSetup()
    {
        if (_celebrationVFX != null) return false;

        Transform parentTarget = _panelRoot != null ? _panelRoot.transform : transform;
        Transform existing = parentTarget.Find("CelebrationVFX");
        if (existing != null)
        {
            _celebrationVFX = existing.GetComponent<ParticleSystem>();
            if (_celebrationVFX != null) return false;
        }

        GameObject vfxObj = new GameObject("CelebrationVFX");
        vfxObj.transform.SetParent(parentTarget, false);
        vfxObj.transform.localPosition = Vector3.zero;

        _celebrationVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = _celebrationVFX.main;
        main.duration = 0.8f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 20f;
        main.startSize = 6f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.84f, 0.31f, 1f), new Color(1f, 0.6f, 0f, 1f));

        var emission = _celebrationVFX.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        var shape = _celebrationVFX.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 18f;

        ParticleSystemRenderer renderer = _celebrationVFX.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        return true;
    }

    /// <summary>
    /// Paneli ve alt öğeleri UIThemeSO / UIStyleHelper ile altın-turuncu cizgi film stiline büründürür.
    /// </summary>
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
            UIStyleHelper.ApplyPanelStyle(_panelRoot, activeTheme, true);
        }

        if (_titleText != null)
        {
            UIStyleHelper.ApplyTextStyle(_titleText, activeTheme, true);
        }

        if (_progressText != null)
        {
            UIStyleHelper.ApplyTextStyle(_progressText, activeTheme, false);
        }

        if (_progressBar != null)
        {
            UIStyleHelper.ApplyBarStyle(_progressBar, activeTheme, _progressFillImage);
        }

        if (_iconImage != null && activeTheme != null)
        {
            _iconImage.color = activeTheme.primaryGold;
        }
    }

    /// <summary>
    /// Boş kalan hiyerarşi referanslarını isim bazlı otomatik bulur.
    /// </summary>
    public void AutoResolveReferences()
    {
        if (_panelRoot == null)
        {
            _panelRoot = gameObject;
        }

        if (_titleText == null)
        {
            Transform tTr = transform.Find("TitleText");
            if (tTr != null) _titleText = tTr.GetComponent<TextMeshProUGUI>();
        }

        if (_progressText == null)
        {
            Transform pTr = transform.Find("ProgressText");
            if (pTr != null) _progressText = pTr.GetComponent<TextMeshProUGUI>();
        }

        if (_progressBar == null)
        {
            _progressBar = GetComponentInChildren<Slider>(true);
        }

        if (_progressBar != null && _progressFillImage == null && _progressBar.fillRect != null)
        {
            _progressFillImage = _progressBar.fillRect.GetComponent<Image>();
        }

        if (_iconImage == null)
        {
            Transform iTr = transform.Find("ChickIcon");
            if (iTr != null) _iconImage = iTr.GetComponent<Image>();
        }

        if (_rescueManager == null)
        {
            _rescueManager = ChickRescueManager.Instance != null
                ? ChickRescueManager.Instance
                : FindAnyObjectByType<ChickRescueManager>();
        }
    }
}
