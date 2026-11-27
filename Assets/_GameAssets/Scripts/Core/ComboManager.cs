using System;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Ardışık Altın Kombo ve Çarpan Yöneticisi (ComboManager)
/// Ardışık altın toplandığında seri (CurrentStreak) ve skor çarpanını (CurrentMultiplier) artırır.
/// Kombo süresi dolduğunda veya oyuncu hasar aldığında komboyu sıfırlar.
/// </summary>
public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("--- Combo Settings ---")]
    [Tooltip("Kombo serisinin sıfırlanmadan önce aktif kalacağı süre (saniye)")]
    [SerializeField] private float _comboDuration = 4.0f;

    [Tooltip("Her ardışık altın alımında çarpan artış adımı")]
    [SerializeField] private float _multiplierStep = 0.5f;

    [Tooltip("Ulaşılabilecek maksimum kombo çarpanı")]
    [SerializeField] private float _maxMultiplier = 5.0f;

    [Tooltip("Çarpan > 1 iken CoinManager'a ek bonus altın eklensin mi")]
    [SerializeField] private bool _applyBonusCoins = true;

    [Header("--- Target References ---")]
    [SerializeField] private CoinManager _coinManager;
    [SerializeField] private PlayerHealth _playerHealth;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _comboUpSound;
    [SerializeField] private AudioClip _comboResetSound;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.8f;

    private int _currentStreak;
    private int _bestStreak;
    private float _currentMultiplier = 1f;
    private float _comboTimer;
    private int _lastKnownHealth = -1;
    private bool _isApplyingBonus;

    /// <summary>
    /// (currentStreak, currentMultiplier, normalizedTimeRemaining)
    /// </summary>
    public event Action<int, float, float> OnComboUpdated;
    public event Action OnComboReset;

    public int CurrentStreak => _currentStreak;
    public int BestStreak => _bestStreak;
    public float CurrentMultiplier => _currentMultiplier;
    public float ComboTimer => _comboTimer;
    public float ComboDuration => _comboDuration;
    public float MaxMultiplier => _maxMultiplier;
    public float NormalizedTimeRemaining => (_currentStreak > 0 && _comboDuration > 0f)
        ? Mathf.Clamp01(_comboTimer / _comboDuration)
        : 0f;
    public CoinManager TargetCoinManager => _coinManager;
    public PlayerHealth TargetPlayerHealth => _playerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveDependencies();
        _currentStreak = 0;
        _currentMultiplier = 1f;
        _comboTimer = 0f;
    }

    private void Start()
    {
        SubscribeEvents();
        NotifyComboUpdated();
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
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (SettingsUI.IsOpen || Time.timeScale <= 0f)
            return;

        Tick(Time.deltaTime);
    }

    public void ResolveDependencies()
    {
        if (_coinManager == null)
        {
            _coinManager = CoinManager.Instance != null
                ? CoinManager.Instance
                : FindAnyObjectByType<CoinManager>();
        }

        if (_playerHealth == null)
        {
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null && _lastKnownHealth < 0)
        {
            _lastKnownHealth = _playerHealth.CurrentHealth;
        }
    }

    public void BindTargets(CoinManager coinManager, PlayerHealth playerHealth)
    {
        UnsubscribeEvents();
        _coinManager = coinManager;
        _playerHealth = playerHealth;
        _lastKnownHealth = _playerHealth != null ? _playerHealth.CurrentHealth : -1;
        SubscribeEvents();
    }

    public void SubscribeEvents()
    {
        ResolveDependencies();

        if (_coinManager != null)
        {
            _coinManager.CoinsCollected -= HandleCoinsCollected;
            _coinManager.CoinsCollected += HandleCoinsCollected;
        }

        if (_playerHealth != null)
        {
            _lastKnownHealth = _playerHealth.CurrentHealth;
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
            _playerHealth.OnDied -= HandlePlayerDied;
            _playerHealth.OnDied += HandlePlayerDied;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_coinManager != null)
        {
            _coinManager.CoinsCollected -= HandleCoinsCollected;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    /// <summary>
    /// Ardışık altın toplama olayını işler, seriyi ve çarpanı artırır.
    /// </summary>
    public void RegisterCoinPickup(int coinAmount = 1)
    {
        if (coinAmount <= 0)
            return;

        float previousMultiplier = _currentMultiplier;
        _currentStreak++;
        if (_currentStreak > _bestStreak)
        {
            _bestStreak = _currentStreak;
        }

        _currentMultiplier = CalculateMultiplier(_currentStreak);
        _comboTimer = _comboDuration;

        if (_applyBonusCoins && _coinManager != null && _currentMultiplier > 1f)
        {
            int bonusCoins = Mathf.FloorToInt(coinAmount * (_currentMultiplier - 1f));
            if (bonusCoins > 0)
            {
                _isApplyingBonus = true;
                try
                {
                    _coinManager.AddCoins(bonusCoins);
                }
                finally
                {
                    _isApplyingBonus = false;
                }
            }
        }

        if (_comboUpSound != null && _currentMultiplier > previousMultiplier)
        {
            SettingsUI.PlaySFX(_comboUpSound, _sfxVolume);
        }

        NotifyComboUpdated();
    }

    /// <summary>
    /// Kombo süresini ilerletir; süre dolunca komboyu sıfırlar.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_currentStreak <= 0 || deltaTime <= 0f)
            return;

        _comboTimer -= deltaTime;
        if (_comboTimer <= 0f)
        {
            ResetCombo();
        }
        else
        {
            NotifyComboUpdated();
        }
    }

    /// <summary>
    /// Kombo serisini ve çarpanı başlangıç durumuna (0 seri, 1x çarpan) döndürür.
    /// </summary>
    public void ResetCombo()
    {
        bool hadCombo = _currentStreak > 0 || !Mathf.Approximately(_currentMultiplier, 1f);

        _currentStreak = 0;
        _currentMultiplier = 1f;
        _comboTimer = 0f;

        if (hadCombo && _comboResetSound != null)
        {
            SettingsUI.PlaySFX(_comboResetSound, _sfxVolume);
        }

        NotifyComboUpdated();
        if (hadCombo)
        {
            OnComboReset?.Invoke();
        }
    }

    private float CalculateMultiplier(int streak)
    {
        if (streak <= 1)
            return 1f;

        float computed = 1f + (streak - 1) * _multiplierStep;
        return Mathf.Clamp(computed, 1f, Mathf.Max(1f, _maxMultiplier));
    }

    private void HandleCoinsCollected(int amount)
    {
        if (_isApplyingBonus)
            return;

        RegisterCoinPickup(amount);
    }

    private void HandlePlayerHealthChanged(int currentHealth, int maxHealth)
    {
        if (_lastKnownHealth >= 0 && currentHealth < _lastKnownHealth)
        {
            ResetCombo();
        }
        _lastKnownHealth = currentHealth;
    }

    private void HandlePlayerDied()
    {
        _lastKnownHealth = 0;
        ResetCombo();
    }

    private void NotifyComboUpdated()
    {
        OnComboUpdated?.Invoke(_currentStreak, _currentMultiplier, NormalizedTimeRemaining);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _comboDuration = Mathf.Max(0.5f, _comboDuration);
        _multiplierStep = Mathf.Max(0.1f, _multiplierStep);
        _maxMultiplier = Mathf.Max(1f, _maxMultiplier);
    }
#endif
}
