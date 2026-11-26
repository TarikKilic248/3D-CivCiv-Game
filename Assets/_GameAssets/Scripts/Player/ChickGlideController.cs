using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerController))]
public class ChickGlideController : MonoBehaviour
{
    [SerializeField] private int _maxAirJumps = 1;
    [SerializeField] private float _airJumpForce = 6.5f;
    [SerializeField] private float _glideFallSpeedLimit = -2.2f;
    [SerializeField] private float _glideStaminaDrainPerSec = 12f;

    public bool IsGliding { get; private set; }

    private PlayerController _playerController;
    private StateController _stateController;
    private DashController _dashController;
    private int _remainingAirJumps;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _stateController = GetComponent<StateController>();
        _dashController = GetComponent<DashController>();
        _remainingAirJumps = _maxAirJumps;
    }

    private void Update()
    {
        if (_playerController == null)
            return;

        if (_playerController.IsGrounded())
        {
            if (IsGliding || _remainingAirJumps != _maxAirJumps)
            {
                StopGlide();
            }
            return;
        }

        HandleAirInput();
    }

    private void FixedUpdate()
    {
        if (IsGliding)
        {
            ApplyGlidePhysics();
        }
    }

    private void HandleAirInput()
    {
        if (SettingsUI.IsOpen)
        {
            if (IsGliding) StopGlide();
            return;
        }

        bool spacePressed = false;
        bool spaceHeld = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) spacePressed = true;
            if (kb.spaceKey.isPressed) spaceHeld = true;
        }
#endif

        try
        {
            if (!spacePressed && Input.GetKeyDown(KeyCode.Space)) spacePressed = true;
            if (!spaceHeld && Input.GetKey(KeyCode.Space)) spaceHeld = true;
        }
        catch
        {
            // Input System paketi tek basina aktifse eski API sessizce atlanir
        }

        if (spacePressed && _remainingAirJumps > 0)
        {
            ExecuteAirJump();
            return;
        }

        Rigidbody rb = _playerController.GetRigidbody();
        bool isFalling = rb != null && rb.linearVelocity.y < -0.1f;

        if (spaceHeld && isFalling)
        {
            if (_dashController != null && _glideStaminaDrainPerSec > 0f)
            {
                float drain = _glideStaminaDrainPerSec * Time.deltaTime;
                if (_dashController.TryConsumeStamina(drain))
                {
                    StartGlide();
                }
                else
                {
                    StopGlide();
                }
            }
            else
            {
                StartGlide();
            }
        }
        else if (IsGliding)
        {
            IsGliding = false;
        }
    }

    public void ExecuteAirJump()
    {
        if (_playerController == null)
            return;

        Rigidbody rb = _playerController.GetRigidbody();
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
            rb.AddForce(Vector3.up * _airJumpForce, ForceMode.Impulse);
        }

        _remainingAirJumps = Mathf.Max(0, _remainingAirJumps - 1);
        IsGliding = false;
    }

    private void StartGlide()
    {
        IsGliding = true;
    }

    private void ApplyGlidePhysics()
    {
        if (_playerController == null)
            return;

        Rigidbody rb = _playerController.GetRigidbody();
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.y < _glideFallSpeedLimit)
            {
                rb.linearVelocity = new Vector3(vel.x, Mathf.Max(vel.y, _glideFallSpeedLimit), vel.z);
            }
        }

        if (_stateController != null)
        {
            _stateController.ChangeState(PlayerState.Glide);
        }
    }

    public void StopGlide()
    {
        IsGliding = false;
        _remainingAirJumps = _maxAirJumps;
    }
}
