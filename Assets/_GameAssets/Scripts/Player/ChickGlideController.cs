using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerController))]
public class ChickGlideController : MonoBehaviour
{
    [Header("--- Data (ScriptableObject) ---")]
    [Tooltip("Merkezi karakter parametreleri (opsiyonel)")]
    [SerializeField] private PlayerStatsSO _playerStats;

    [Header("--- Air Jump Settings ---")]
    [Tooltip("Havadayken yapilabilecek maksimum ekstra ziplama sayisi")]
    [SerializeField] private int _maxAirJumps = 1;

    [Tooltip("Havadaki ziplama kuvveti")]
    [SerializeField] private float _airJumpForce = 7f;

    [Header("--- Glide Settings ---")]
    [Tooltip("Suzulme sirasinda izin verilen maksimum dusus hizi")]
    [SerializeField] private float _maxGlideFallSpeed = 2.0f;

    [Tooltip("Suzulme ve ziplama tusu (Eski Input icin)")]
    [SerializeField] private KeyCode _glideKey = KeyCode.Space;

    // Bilesen Referanslari
    private PlayerController _playerController;
    private StateController _stateController;
    private Rigidbody _playerRigidbody;
    private DashController _dashController;

    // Calisma Zamani Degiskenleri
    private int _remainingAirJumps = 1;
    private bool _isGliding = false;

    // Olaylar (Events)
    public event Action OnAirJumpExecuted;
    public event Action<bool> OnGlideStateChanged;

    // Public Ozellikler (Properties)
    public bool IsGliding => _isGliding;
    public int RemainingAirJumps => _remainingAirJumps;
    public int MaxAirJumps
    {
        get => _maxAirJumps;
        set => _maxAirJumps = Mathf.Max(0, value);
    }
    public float MaxGlideFallSpeed
    {
        get => _maxGlideFallSpeed;
        set => _maxGlideFallSpeed = Mathf.Max(0.1f, value);
    }
    public float maxGlideFallSpeed => _maxGlideFallSpeed;
    public float AirJumpForce
    {
        get => _airJumpForce;
        set => _airJumpForce = Mathf.Max(0f, value);
    }
    public PlayerStatsSO PlayerStats => _playerStats;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_stateController == null) _stateController = GetComponent<StateController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
        if (_dashController == null) _dashController = GetComponent<DashController>();
    }

    private void Reset()
    {
        ResolveDependencies();
        if (_playerController != null && _playerController.PlayerStats != null)
        {
            _playerStats = _playerController.PlayerStats;
        }
        if (_playerController != null)
        {
            _airJumpForce = _playerController.JumpForce;
        }
        _remainingAirJumps = _maxAirJumps;
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        if (_playerStats == null && _playerController != null)
        {
            _playerStats = _playerController.PlayerStats;
        }
        _remainingAirJumps = _maxAirJumps;
    }

    private void Start()
    {
        _remainingAirJumps = _maxAirJumps;
    }

    private void OnDisable()
    {
        if (_isGliding)
        {
            StopGlide();
        }
    }

    public void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_stateController == null) _stateController = GetComponent<StateController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
        if (_dashController == null) _dashController = GetComponent<DashController>();
    }

    private void Update()
    {
        HandleInput();
    }

    private void FixedUpdate()
    {
        if (_playerController != null && _playerController.IsInputBlocked())
        {
            if (_isGliding) StopGlide();
            return;
        }

        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        Step(dt);
    }

    public void Step(float deltaTime)
    {
        ResolveDependencies();

        // Dash yapiliyorsa suzulmeyi durdur
        if (_dashController != null && _dashController.IsDashing)
        {
            if (_isGliding) StopGlide();
            return;
        }

        // Yere inildiyse ziplamalari sifirla ve suzulmeyi durdur
        if (_playerController != null && _playerController.IsGrounded)
        {
            _remainingAirJumps = _maxAirJumps;
            if (_isGliding)
            {
                StopGlide();
            }
            return;
        }

        // Suzulme fizigi: Dusus hizini _maxGlideFallSpeed ile sinirla
        if (_isGliding && _playerRigidbody != null)
        {
            Vector3 vel = _playerRigidbody.linearVelocity;
            float predictedY = vel.y + Physics.gravity.y * Mathf.Max(0f, deltaTime);
            if (vel.y <= -_maxGlideFallSpeed || predictedY < -_maxGlideFallSpeed)
            {
                vel.y = -_maxGlideFallSpeed;
                _playerRigidbody.linearVelocity = vel;
                _playerRigidbody.AddForce(-Physics.gravity * _playerRigidbody.mass, ForceMode.Force);
            }
        }
    }

    private void HandleInput()
    {
        if (_playerController != null && _playerController.IsInputBlocked())
            return;

        bool jumpPressed = false;
        bool jumpHeld = false;
        bool jumpReleased = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) jumpPressed = true;
            if (kb.spaceKey.isPressed) jumpHeld = true;
            if (kb.spaceKey.wasReleasedThisFrame) jumpReleased = true;
        }
#endif

        try
        {
            if (!jumpPressed && Input.GetKeyDown(_glideKey)) jumpPressed = true;
            if (!jumpHeld && Input.GetKey(_glideKey)) jumpHeld = true;
            if (!jumpReleased && Input.GetKeyUp(_glideKey)) jumpReleased = true;
        }
        catch
        {
            // Input System tek basina aktifse eski Input API istisnasi yutulur
        }

        bool isGrounded = _playerController != null && _playerController.IsGrounded;

        // Yerdeyken air jump veya glide baslatilamaz
        if (isGrounded)
        {
            _remainingAirJumps = _maxAirJumps;
            if (_isGliding) StopGlide();
            return;
        }

        // Havadayken Space basildiginda Air Jump dene
        if (jumpPressed && _remainingAirJumps > 0)
        {
            ExecuteAirJump();
            return;
        }

        // Havadayken ve duserken Space basili tutuluyorsa suzulmeyi baslat
        if (jumpHeld && !_isGliding && _playerRigidbody != null && _playerRigidbody.linearVelocity.y <= 0.1f)
        {
            StartGlide();
        }
        else if (jumpReleased && _isGliding)
        {
            StopGlide();
        }
    }

    public bool ExecuteAirJump()
    {
        if (_remainingAirJumps <= 0) return false;
        if (_playerController != null && _playerController.IsInputBlocked()) return false;
        if (_playerController != null && _playerController.IsGrounded) return false;

        ResolveDependencies();
        if (_playerRigidbody == null) return false;

        if (_isGliding)
        {
            StopGlide();
        }

        _remainingAirJumps--;

        float jumpVelocity = _airJumpForce / Mathf.Max(0.0001f, _playerRigidbody.mass);
        Vector3 curVel = _playerRigidbody.linearVelocity;
        _playerRigidbody.linearVelocity = new Vector3(curVel.x, jumpVelocity, curVel.z);

        if (_stateController != null)
        {
            _stateController.ChangeState(PlayerState.Jump);
        }

        OnAirJumpExecuted?.Invoke();

        return true;
    }

    public bool StartGlide()
    {
        if (_isGliding) return true;
        if (_playerController != null && _playerController.IsInputBlocked()) return false;
        if (_playerController != null && _playerController.IsGrounded) return false;

        ResolveDependencies();

        _isGliding = true;

        if (_playerRigidbody != null && _playerRigidbody.linearVelocity.y < -_maxGlideFallSpeed)
        {
            Vector3 vel = _playerRigidbody.linearVelocity;
            vel.y = -_maxGlideFallSpeed;
            _playerRigidbody.linearVelocity = vel;
        }

        if (_stateController != null)
        {
            _stateController.ChangeState(PlayerState.Glide);
        }

        OnGlideStateChanged?.Invoke(true);

        return true;
    }

    public void StopGlide()
    {
        if (!_isGliding) return;

        _isGliding = false;

        if (_stateController != null && _stateController.GetCurrentState() == PlayerState.Glide)
        {
            PlayerState returnState = (_playerController != null && _playerController.IsGrounded)
                ? PlayerState.Idle
                : PlayerState.Jump;
            _stateController.ChangeState(returnState);
        }

        OnGlideStateChanged?.Invoke(false);
    }

    public void ResetAirJumps()
    {
        _remainingAirJumps = _maxAirJumps;
    }
}
