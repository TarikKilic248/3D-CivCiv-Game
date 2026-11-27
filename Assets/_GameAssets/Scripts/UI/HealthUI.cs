using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Kalp ve Can Göstergesi HUD Bileşeni (HealthUI)
/// PlayerHealth.OnHealthChanged olayını dinleyerek ekranda kalp ikonları ve can metnini günceller.
/// UIThemeSO / UIStyleHelper ile oyunun sıcak altın-sarı / turuncu / koyu kehribar stiline tam uyumludur.
/// </summary>
public class HealthUI : MonoBehaviour
{
    public static HealthUI Instance { get; private set; }

    [Header("--- Theme & Style ---")]
    [SerializeField] private UIThemeSO _theme;

    [Header("--- UI References ---")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private RectTransform _heartsContainer;
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private TextMeshProUGUI _healthText;

    [Header("--- Sprites & Colors ---")]
    [SerializeField] private Sprite _heartSprite;
    [SerializeField] private Sprite _emptyHeartSprite;
    [SerializeField] private Color _activeHeartColor = new Color(0.95f, 0.22f, 0.25f, 1f);
    [SerializeField] private Color _depletedHeartColor = new Color(0.35f, 0.20f, 0.12f, 0.38f);

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _hurtSound;
    [SerializeField] private AudioClip _healSound;

    [Header("--- Target References ---")]
    [SerializeField] private PlayerHealth _playerHealth;

    [Header("--- Procedural Motion ---")]
    [Tooltip("Hasar alındığında panelin anlık basıklaşma/büyüme ölçeği")]
    [SerializeField] private float _punchScale = 1.14f;
    [Tooltip("Görsel tepki sönümleme hızı")]
    [SerializeField] private float _punchDecaySpeed = 8f;
    [Tooltip("Hasar anındaki hafif sallanma açısı (derece)")]
    [SerializeField] private float _punchTiltAngle = 3.5f;

    private int _activeHeartCount;
    private int _currentHealth = 100;
    private int _maxHealth = 100;
    private float _punchAmount;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;

    public UIThemeSO Theme => _theme;
    public GameObject PanelRoot => _panelRoot;
    public RectTransform HeartsContainer => _heartsContainer;
    public Image[] HeartIcons => _heartIcons;
    public TextMeshProUGUI HealthText => _healthText;
    public PlayerHealth TargetPlayerHealth => _playerHealth;
    public int ActiveHeartCount => _activeHeartCount;
    public int ActiveIconCount => _activeHeartCount;
    public int TotalHearts => _heartIcons != null ? _heartIcons.Length : 0;
    public Color ActiveHeartColor => _activeHeartColor;
    public Color DepletedHeartColor => _depletedHeartColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_panelRoot != null)
        {
            _baseScale = _panelRoot.transform.localScale;
            _baseRotation = _panelRoot.transform.localRotation;
        }

        AutoResolveReferences();
        EnsureHeartSprites();
        ApplyTheme(_theme);
    }

    private void Start()
    {
        SubscribeEvents();
        if (_playerHealth != null)
        {
            UpdateHealthUI(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
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
        StepFeedback(dt);
    }

    public void StepFeedback(float dt)
    {
        if (_punchAmount > 0.001f)
        {
            _punchAmount = Mathf.Max(0f, _punchAmount - dt * _punchDecaySpeed);
            float scale = 1f + (_punchScale - 1f) * _punchAmount;
            float angle = Mathf.Sin(_punchAmount * Mathf.PI * 3f) * _punchTiltAngle * _punchAmount;

            if (_panelRoot != null)
            {
                _panelRoot.transform.localScale = _baseScale * scale;
                _panelRoot.transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
            }
        }
        else if (_panelRoot != null && (_panelRoot.transform.localScale != _baseScale || _panelRoot.transform.localRotation != _baseRotation))
        {
            _panelRoot.transform.localScale = _baseScale;
            _panelRoot.transform.localRotation = _baseRotation;
        }
    }

    public void ResetVisualPoseImmediate()
    {
        _punchAmount = 0f;
        if (_panelRoot != null)
        {
            _panelRoot.transform.localScale = _baseScale;
            _panelRoot.transform.localRotation = _baseRotation;
        }
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

        if (_healthText != null)
        {
            UIStyleHelper.ApplyTextStyle(_healthText, activeTheme, false, 15f);
        }

        _activeHeartColor = new Color(0.95f, 0.22f, 0.25f, 1f);
        _depletedHeartColor = new Color(activeTheme.darkAmberBorder.r, activeTheme.darkAmberBorder.g, activeTheme.darkAmberBorder.b, 0.38f);

        RefreshIcons();
    }

    public void AutoResolveReferences()
    {
        if (_panelRoot == null)
        {
            _panelRoot = gameObject;
        }

        if (_heartsContainer == null)
        {
            Transform hc = transform.Find("HeartsContainer");
            if (hc != null) _heartsContainer = hc as RectTransform;
        }

        if (_heartIcons == null || _heartIcons.Length == 0)
        {
            if (_heartsContainer != null)
            {
                _heartIcons = _heartsContainer.GetComponentsInChildren<Image>(true);
            }
            else
            {
                var allImgs = GetComponentsInChildren<Image>(true);
                var list = new List<Image>();
                for (int i = 0; i < allImgs.Length; i++)
                {
                    if (allImgs[i].gameObject != _panelRoot) list.Add(allImgs[i]);
                }
                _heartIcons = list.ToArray();
            }
        }

        if (_healthText == null)
        {
            Transform tr = transform.Find("HealthText");
            if (tr != null) _healthText = tr.GetComponent<TextMeshProUGUI>();
            if (_healthText == null) _healthText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_playerHealth == null)
        {
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }
    }

    public void BindPlayerHealth(PlayerHealth health)
    {
        UnsubscribeEvents();
        _playerHealth = health;
        SubscribeEvents();

        if (_playerHealth != null)
        {
            UpdateHealthUI(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }
    }

    public void SubscribeEvents()
    {
        if (_playerHealth == null)
        {
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= UpdateHealthUI;
            _playerHealth.OnHealthChanged += UpdateHealthUI;
            _playerHealth.OnDied -= HandlePlayerDied;
            _playerHealth.OnDied += HandlePlayerDied;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= UpdateHealthUI;
            _playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied()
    {
        UpdateHealthUI(0, _maxHealth);
    }

    public void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        int prevHealth = _currentHealth;
        _currentHealth = Mathf.Max(0, currentHealth);
        _maxHealth = Mathf.Max(1, maxHealth);

        int total = TotalHearts;
        if (total == 0)
        {
            _activeHeartCount = 0;
        }
        else
        {
            if (_currentHealth <= 0)
            {
                _activeHeartCount = 0;
            }
            else
            {
                float healthPerHeart = (float)_maxHealth / total;
                _activeHeartCount = Mathf.Clamp(Mathf.CeilToInt((float)_currentHealth / healthPerHeart), 1, total);
            }
        }

        RefreshIcons();

        if (_healthText != null)
        {
            _healthText.text = $"{_currentHealth} / {_maxHealth}";
        }

        if (prevHealth != _currentHealth)
        {
            _punchAmount = 1f;
            if (_currentHealth < prevHealth)
            {
                if (_hurtSound != null) SettingsUI.PlaySFX(_hurtSound, 0.8f);
            }
            else
            {
                if (_healSound != null) SettingsUI.PlaySFX(_healSound, 0.8f);
            }
        }
    }

    private void RefreshIcons()
    {
        EnsureHeartSprites();

        if (_heartIcons == null) return;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            Image icon = _heartIcons[i];
            if (icon == null) continue;

            bool isActive = i < _activeHeartCount;
            icon.color = isActive ? _activeHeartColor : _depletedHeartColor;

            if (isActive && _heartSprite != null)
            {
                icon.sprite = _heartSprite;
            }
            else if (!isActive && _emptyHeartSprite != null)
            {
                icon.sprite = _emptyHeartSprite;
            }
            else if (_heartSprite != null)
            {
                icon.sprite = _heartSprite;
            }

            icon.transform.localScale = isActive ? Vector3.one : new Vector3(0.85f, 0.85f, 1f);
        }
    }

    public void EnsureHeartSprites()
    {
        if (_heartSprite == null)
        {
            _heartSprite = CreateHeartSprite(false);
        }
        if (_emptyHeartSprite == null)
        {
            _emptyHeartSprite = CreateHeartSprite(true);
        }
    }

    public static Sprite CreateHeartSprite(bool outlineOnly)
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = outlineOnly ? "Heart_Empty_Procedural" : "Heart_Filled_Procedural";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / (size * 0.38f);
                float ny = (y - center * 0.9f) / (size * 0.38f);

                float x2 = nx * nx;
                float y2 = ny * ny;
                float val = Mathf.Pow(x2 + y2 - 1f, 3f) - x2 * ny * y2;

                if (outlineOnly)
                {
                    if (val <= 0.08f && val >= -0.32f)
                    {
                        float alpha = Mathf.Clamp01(1f - Mathf.Abs(val - (-0.12f)) / 0.2f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
                else
                {
                    if (val <= 0.05f)
                    {
                        float alpha = Mathf.Clamp01(-val / 0.08f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
