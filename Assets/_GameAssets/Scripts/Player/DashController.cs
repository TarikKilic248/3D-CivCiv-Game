using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerController))]
public class DashController : MonoBehaviour
{
    [Header("--- Data (ScriptableObject) ---")]
    [Tooltip("Merkezi karakter parametreleri")]
    [SerializeField] private PlayerStatsSO _playerStats;

    [Header("--- Dash Settings ---")]
    [Tooltip("Dash anında uygulanan yatay hız")]
    [SerializeField] private float _dashSpeed = 20f;

    [Tooltip("Dash etki süresi (saniye)")]
    [SerializeField] private float _dashDuration = 0.2f;

    [Tooltip("Dash yeteneği tekrar kullanım bekleme süresi (saniye)")]
    [SerializeField] private float _dashCooldown = 1f;

    [Tooltip("Dash tuşu (Eski Input için)")]
    [SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;

    [Header("--- Stamina Settings ---")]
    [Tooltip("Maksimum stamina")]
    [SerializeField] private float _maxStamina = 100f;

    [Tooltip("Dash kullanımında tüketilen stamina")]
    [SerializeField] private float _dashStaminaCost = 30f;

    [Tooltip("Saniye başına stamina yenilenme miktarı")]
    [SerializeField] private float _staminaRegenRate = 15f;

    [Tooltip("Dash bittikten sonra stamina yenilenmesinin başlaması için bekleme süresi (saniye)")]
    [SerializeField] private float _staminaRegenDelay = 0.5f;

    // Bileşen Referansları
    private PlayerController _playerController;
    private StateController _stateController;
    private Rigidbody _playerRigidbody;

    // Çalışma Zamanı Değişkenleri
    private bool _isDashing = false;
    private float _dashTimer = 0f;
    private float _cooldownTimer = 0f;
    private float _currentStamina = 100f;
    private float _regenTimer = 0f;
    private Vector3 _currentDashDirection = Vector3.forward;

    // Olaylar (Events)
    public event Action<float, float> OnStaminaChanged;

    // Public Özellikler (Properties)
    public bool IsDashing => _isDashing;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
    public float DashSpeed => _dashSpeed;
    public float DashDuration => _dashDuration;
    public float DashCooldown => _dashCooldown;
    public float CooldownTimer => _cooldownTimer;
    public float DashStaminaCost => _dashStaminaCost;
    public float StaminaRegenRate => _staminaRegenRate;
    public float StaminaRegenDelay => _staminaRegenDelay;
    public PlayerStatsSO PlayerStats => _playerStats;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_stateController == null) _stateController = GetComponent<StateController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
        if (_playerStats != null)
        {
            SyncFieldsFromSO(_playerStats);
        }
    }

    private void Reset()
    {
        _playerController = GetComponent<PlayerController>();
        _stateController = GetComponent<StateController>();
        _playerRigidbody = GetComponent<Rigidbody>();
        if (_playerController != null && _playerController.PlayerStats != null)
        {
            _playerStats = _playerController.PlayerStats;
            SyncFieldsFromSO(_playerStats);
        }
    }
#endif

    private void Awake()
    {
        ResolveDependencies();

        if (_playerStats == null && _playerController != null)
        {
            _playerStats = _playerController.PlayerStats;
        }

        if (_playerStats != null)
        {
            SyncFieldsFromSO(_playerStats);
        }

        _currentStamina = _maxStamina;
    }

    private void Start()
    {
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    public void ApplyStatsFromSO(PlayerStatsSO stats)
    {
        if (stats == null) return;
        _playerStats = stats;
        SyncFieldsFromSO(stats);
        if (_currentStamina > _maxStamina)
        {
            _currentStamina = _maxStamina;
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
        }
    }

    public void SyncFieldsFromSO(PlayerStatsSO stats)
    {
        if (stats == null) return;
        _dashSpeed = stats.dashSpeed;
        _dashDuration = stats.dashDuration;
        _dashCooldown = stats.dashCooldown;
        _maxStamina = stats.maxStamina;
        _dashStaminaCost = stats.dashStaminaCost;
        _staminaRegenRate = stats.staminaRegenRate;
        _staminaRegenDelay = stats.staminaRegenDelay;
    }

    private void OnDisable()
    {
        if (_isDashing)
        {
            EndDash();
        }
    }

    private void Update()
    {
        HandleInput();
    }

    private void FixedUpdate()
    {
        if (_playerController != null && _playerController.IsInputBlocked())
        {
            return;
        }

        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        Step(dt);
    }

    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // Bekleme süresi geri sayımı
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);
        }

        if (_isDashing)
        {
            _dashTimer -= deltaTime;

            if (_playerRigidbody != null)
            {
                Vector3 flatDir = new Vector3(_currentDashDirection.x, 0f, _currentDashDirection.z);
                if (flatDir.sqrMagnitude > 0.001f)
                {
                    flatDir.Normalize();
                    _playerRigidbody.linearVelocity = new Vector3(flatDir.x * _dashSpeed, _playerRigidbody.linearVelocity.y, flatDir.z * _dashSpeed);
                }
            }

            if (_dashTimer <= 0.0001f)
            {
                EndDash();
            }
        }
        else
        {
            // Stamina yenilenmesi
            if (_regenTimer > 0f)
            {
                _regenTimer = Mathf.Max(0f, _regenTimer - deltaTime);
            }
            else if (_currentStamina < _maxStamina)
            {
                float prevStamina = _currentStamina;
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _staminaRegenRate * deltaTime);
                if (!Mathf.Approximately(prevStamina, _currentStamina))
                {
                    OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
                }
            }
        }
    }

    private void HandleInput()
    {
        if (_playerController != null && _playerController.IsInputBlocked())
            return;

        bool dashPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.leftShiftKey.wasPressedThisFrame)
        {
            dashPressed = true;
        }
#endif

        try
        {
            if (!dashPressed && Input.GetKeyDown(_dashKey))
            {
                dashPressed = true;
            }
        }
        catch
        {
            // Input System tek başına aktifse eski Input API istisnası yutulur
        }

        if (dashPressed)
        {
            ExecuteDash();
        }
    }

    public bool ExecuteDash()
    {
        if (_isDashing) return false;
        if (_cooldownTimer > 0f) return false;
        if (_currentStamina < _dashStaminaCost) return false;
        if (_playerController != null && _playerController.IsInputBlocked()) return false;

        ResolveDependencies();

        _isDashing = true;
        _dashTimer = _dashDuration;
        _cooldownTimer = _dashCooldown;
        _regenTimer = _staminaRegenDelay;

        _currentStamina = Mathf.Max(0f, _currentStamina - _dashStaminaCost);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);

        if (_playerController != null)
        {
            _playerController.SetExternalMovementOverride(true);
        }

        if (_stateController != null)
        {
            _stateController.ChangeState(PlayerState.Dash);
        }

        _currentDashDirection = DetermineDashDirection();

        if (_playerRigidbody != null)
        {
            Vector3 flatDir = new Vector3(_currentDashDirection.x, 0f, _currentDashDirection.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                flatDir.Normalize();
                _playerRigidbody.linearVelocity = new Vector3(flatDir.x * _dashSpeed, _playerRigidbody.linearVelocity.y, flatDir.z * _dashSpeed);
            }
        }

        return true;
    }

    public void EndDash()
    {
        if (!_isDashing) return;

        _isDashing = false;
        _dashTimer = 0f;

        if (_playerController != null)
        {
            _playerController.SetExternalMovementOverride(false);
        }

        if (_stateController != null && _stateController.GetCurrentState() == PlayerState.Dash)
        {
            PlayerState returnState = (_playerController != null && _playerController.IsGrounded)
                ? PlayerState.Idle
                : PlayerState.Jump;
            _stateController.ChangeState(returnState);
        }
    }

    public void ResetStamina()
    {
        _currentStamina = _maxStamina;
        _cooldownTimer = 0f;
        _regenTimer = 0f;
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    public void SetStamina(float amount)
    {
        _currentStamina = Mathf.Clamp(amount, 0f, _maxStamina);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    private Vector3 DetermineDashDirection()
    {
        Transform moveRef = (_playerController != null && _playerController.OrientationTransform != null)
            ? _playerController.OrientationTransform
            : transform;

        Vector3 moveInput = Vector3.zero;
        if (_playerController != null)
        {
            float h = _playerController.HorizontalInput;
            float v = _playerController.VerticalInput;
            moveInput = moveRef.forward * v + moveRef.right * h;
        }

        if (moveInput.sqrMagnitude > 0.01f)
        {
            moveInput.y = 0f;
            if (moveInput.sqrMagnitude > 0.001f)
            {
                return moveInput.normalized;
            }
        }

        Vector3 forward = moveRef.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.001f)
        {
            return forward.normalized;
        }

        return Vector3.forward;
    }

    private void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_stateController == null) _stateController = GetComponent<StateController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
    }
}
