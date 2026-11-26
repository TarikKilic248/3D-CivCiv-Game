using System;
using UnityEngine;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [SerializeField] private float _comboResetTime = 3.5f;
    [SerializeField] private int _maxMultiplier = 5;

    public int CurrentStreak { get; private set; }
    public int CurrentMultiplier { get; private set; } = 1;
    public float RemainingComboTimeNormalized { get; private set; }

    public event Action<int, int, float> OnComboUpdated;

    private float _comboTimer;
    private bool _isSubscribedToCoins;
    private bool _isSubscribedToHealth;
    private PlayerHealth _playerHealth;
    private int _lastKnownHealth = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentStreak = 0;
        CurrentMultiplier = 1;
        RemainingComboTimeNormalized = 0f;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        OnComboUpdated?.Invoke(CurrentStreak, CurrentMultiplier, RemainingComboTimeNormalized);
    }

    private void OnDisable()
    {
        if (_isSubscribedToCoins && CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsCollected -= HandleCoinCollected;
            _isSubscribedToCoins = false;
        }

        if (_isSubscribedToHealth && _playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _isSubscribedToHealth = false;
        }
    }

    private void Update()
    {
        if (!_isSubscribedToCoins || !_isSubscribedToHealth)
        {
            TrySubscribe();
        }

        if (CurrentStreak > 0 && _comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f)
            {
                ResetCombo();
            }
            else
            {
                RemainingComboTimeNormalized = _comboResetTime > 0f ? Mathf.Clamp01(_comboTimer / _comboResetTime) : 0f;
                OnComboUpdated?.Invoke(CurrentStreak, CurrentMultiplier, RemainingComboTimeNormalized);
            }
        }
    }

    private void TrySubscribe()
    {
        if (!_isSubscribedToCoins && CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsCollected += HandleCoinCollected;
            _isSubscribedToCoins = true;
        }

        if (!_isSubscribedToHealth)
        {
            if (_playerHealth == null)
            {
                _playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (_playerHealth != null)
            {
                _lastKnownHealth = _playerHealth.CurrentHealth;
                _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
                _isSubscribedToHealth = true;
            }
        }
    }

    private void HandleCoinCollected(int baseAmount)
    {
        _comboTimer = _comboResetTime;
        CurrentStreak++;
        CurrentMultiplier = Mathf.Clamp(1 + (CurrentStreak / 3), 1, _maxMultiplier);
        RemainingComboTimeNormalized = 1f;

        OnComboUpdated?.Invoke(CurrentStreak, CurrentMultiplier, RemainingComboTimeNormalized);
    }

    private void HandlePlayerHealthChanged(int current, int max)
    {
        if (_lastKnownHealth != -1 && current < _lastKnownHealth)
        {
            ResetCombo();
        }
        _lastKnownHealth = current;
    }

    public void ResetCombo()
    {
        _comboTimer = 0f;
        CurrentStreak = 0;
        CurrentMultiplier = 1;
        RemainingComboTimeNormalized = 0f;
        OnComboUpdated?.Invoke(CurrentStreak, CurrentMultiplier, RemainingComboTimeNormalized);
    }
}
