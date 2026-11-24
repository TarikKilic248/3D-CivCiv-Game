using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerController))]
public class DashController : MonoBehaviour
{
    [Header("--- Data & Input ---")]
    [SerializeField] private PlayerStatsSO _playerStats;
    [SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;

    [Header("--- Fallback Dash & Stamina Settings ---")]
    [SerializeField] private float _dashForce = 18f;
    [SerializeField] private float _dashCooldown = 1.2f;
    [SerializeField] private float _dashDuration = 0.2f;
    [SerializeField] private float _dashStaminaCost = 35f;
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaRegenRate = 20f;

    public event Action<float, float> OnStaminaChanged;
    public event Action OnDashExecuted;

    public bool IsDashing { get; private set; }
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;

    private PlayerController _playerController;
    private StateController _stateController;
    private float _currentStamina;
    private float _cooldownTimer;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _stateController = GetComponent<StateController>();

        if (_playerStats != null)
        {
            _dashForce = _playerStats.DashForce;
            _dashCooldown = _playerStats.DashCooldown;
            _dashDuration = _playerStats.DashDuration;
            _dashStaminaCost = _playerStats.DashStaminaCost;
            _maxStamina = _playerStats.MaxStamina;
            _staminaRegenRate = _playerStats.StaminaRegenRate;
        }

        _currentStamina = _maxStamina;
    }

    private void Start()
    {
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        HandleDashInput();
        RegenerateStamina();
    }

    private void HandleDashInput()
    {
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
            // Input System paketi tek basina aktifse eski API sessizce atlanir
        }

        if (dashPressed && CanDash())
        {
            ExecuteDash();
        }
    }

    public bool CanDash()
    {
        if (IsDashing) return false;
        if (_cooldownTimer > 0f) return false;
        if (_currentStamina < _dashStaminaCost) return false;
        if (SettingsUI.IsOpen) return false;
        return true;
    }

    public void ExecuteDash()
    {
        if (!CanDash()) return;

        _currentStamina = Mathf.Max(0f, _currentStamina - _dashStaminaCost);
        _cooldownTimer = _dashCooldown;

        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);

        if (_playerController != null)
        {
            Rigidbody rb = _playerController.GetRigidbody();
            Transform orient = _playerController.GetOrientation();

            if (rb != null)
            {
                Vector3 dashDir = orient != null ? orient.forward : transform.forward;
                dashDir.y = 0f;
                if (dashDir.sqrMagnitude < 0.001f)
                {
                    dashDir = transform.forward;
                }
                dashDir.Normalize();

                rb.AddForce(dashDir * _dashForce, ForceMode.Impulse);
            }
        }

        if (_stateController != null)
        {
            _stateController.ChangeState(PlayerState.Dash);
        }

        OnDashExecuted?.Invoke();
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        yield return new WaitForSeconds(_dashDuration);
        IsDashing = false;
    }

    public void RefillStamina(float amount)
    {
        if (amount <= 0f) return;
        _currentStamina = Mathf.Clamp(_currentStamina + amount, 0f, _maxStamina);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    public bool TryConsumeStamina(float amount)
    {
        if (amount < 0f || _currentStamina < amount) return false;
        _currentStamina = Mathf.Max(0f, _currentStamina - amount);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
        return true;
    }

    private void RegenerateStamina()
    {
        if (IsDashing) return;
        if (_currentStamina >= _maxStamina) return;

        _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _staminaRegenRate * Time.deltaTime);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }
}
