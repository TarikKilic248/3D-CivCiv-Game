using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("--- Data (ScriptableObject) ---")]
    [Tooltip("Karakter hareket parametrelerini içeren merkezi ScriptableObject varlığı")]
    [SerializeField] private PlayerStatsSO _playerStats;

    [Header("--- Orientation & Movement ---")]
    [Tooltip("Karakterin baktığı / yönlendiği Transform (Boş bırakılırsa bu nesnenin transformu kullanılır)")]
    [SerializeField] private Transform _playerOrientationTransform;
    [Tooltip("Yürüme hareket kuvveti çarpanı")]
    [SerializeField] private float _moveSpeed = 45f;
    [Tooltip("Normal yatay hareket hız sınırı (Max Speed)")]
    [SerializeField] private float _maxSpeed = 7f;

    [Header("--- Drag (Sürtünme) Settings ---")]
    [Tooltip("Zemindeyken uygulanan sürtünme (Inspector'dan canlı değiştirilebilir)")]
    [SerializeField] private float _groundDrag = 4f;
    [Tooltip("Havadayken uygulanan sürtünme")]
    [SerializeField] private float _airDrag = 0.2f;
    [Tooltip("Havadayken hareket kuvveti kontrol çarpanı")]
    [SerializeField] private float _airMultiplier = 0.4f;

    [Header("--- Jumping Settings ---")]
    [Tooltip("Zıplama kuvveti")]
    [SerializeField] private float _jumpForce = 7f;
    [Tooltip("Zıplamalar arası bekleme süresi (saniye)")]
    [SerializeField] private float _jumpCooldown = 0.25f;
    [Tooltip("Platform kenarından ayrıldıktan sonra zıplamaya izin verilen tolerans süresi (Coyote Time, saniye)")]
    [SerializeField] private float _coyoteTime = 0.15f;
    [Tooltip("Yere inmeden hemen önce basılan zıplama tuşunu hafızada tutma süresi (Jump Buffering, saniye)")]
    [SerializeField] private float _jumpBufferTime = 0.15f;
    private bool _canJump = true;
    private float _jumpCooldownTimer = 0f;
    private float _coyoteTimeCounter = 0f;
    private float _jumpBufferCounter = 0f;

    [Header("--- Sliding (Kayma) Settings ---")]
    [Tooltip("Slide tuşu (Eski Input / Inspector seçimi için)")]
    [SerializeField] private KeyCode _slideKey = KeyCode.LeftControl;
    [Tooltip("Slide anındaki maksimum yatay hız sınırı")]
    [SerializeField] private float _maxSlideSpeed = 13f;
    [Tooltip("Slide başladığında uygulanan ilk itme kuvveti")]
    [SerializeField] private float _slideImpulseForce = 12f;
    [Tooltip("Slide sırasında devam eden ek itme kuvveti")]
    [SerializeField] private float _slideSustainForce = 15f;
    [Tooltip("Slide sırasındaki sürtünme (düşük olursa daha kaygan kayar)")]
    [SerializeField] private float _slideDrag = 0.3f;
    [Tooltip("Maksimum kayma süresi (saniye). 0 ise tuşa basılı tutulduğu sürece devam eder.")]
    [SerializeField] private float _maxSlideDuration = 1.2f;
    [Tooltip("Slide sırasında collider boyutunu küçült (çömelme efekti)")]
    [SerializeField] private bool _adjustColliderOnSlide = true;
    [Tooltip("Slide esnasında collider yükseklik çarpanı")]
    [SerializeField][Range(0.2f, 1f)] private float _slideColliderMultiplier = 0.5f;

    [Header("--- Ground Check & Collider ---")]
    [Tooltip("Arayüzde (Inspector) eklediğiniz Collider. Boş bırakırsanız otomatik tespit edilir.")]
    [SerializeField] private Collider _playerCollider;
    [Tooltip("Zemin olarak kabul edilecek katmanlar")]
    [SerializeField] private LayerMask _groundLayer = ~0;
    [Tooltip("Zemin algılama ekstra mesafe toleransı")]
    [SerializeField] private float _groundCheckDistance = 0.25f;
    [Tooltip("Zemin algılama küre yarıçapı")]
    [SerializeField] private float _groundCheckRadius = 0.25f;

    [Header("--- Debug & Visuals ---")]
    [Tooltip("Scene görünümünde zemin algılama gizmosunu göster")]
    [SerializeField] private bool _showGroundGizmo = true;

    [Header("--- Animation Settings ---")]
    [Tooltip("Karakterin Animator bileşeni (Boş bırakılırsa otomatik bulunur)")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _idleAnimation = "Idle";
    [SerializeField] private string _moveAnimation = "Walk";
    [SerializeField] private string _jumpAnimation = "Jump";
    [SerializeField] private string _slideAnimation = "Run";

    [Header("--- State Machine ---")]
    [Tooltip("Karakter durum yöneticisi (StateController)")]
    [SerializeField] private StateController _stateController;

    // Özel Değişkenler
    private Rigidbody _playerRigidbody;
    private Vector3 _moveDirection;
    private float _horizontalInput, _verticalInput;
    private bool _isGrounded;
    private bool _isSliding = false;
    private float _slideTimer = 0f;
    private Vector3 _slideDirection;
    private PlayerState _lastAnimatedState = (PlayerState)(-1);
    private float _speedMultiplier = 1f;
    private bool _externalMovementOverride = false;

    // Public Erişimler
    public PlayerStatsSO PlayerStats => _playerStats;
    public bool IsGrounded => _isGrounded;
    public float SpeedMultiplier => _speedMultiplier;
    public bool IsExternalMovementOverridden => _externalMovementOverride;
    public bool IsSliding => _isSliding;
    public float MoveSpeed => _moveSpeed;
    public float MaxSpeed => _maxSpeed;
    public float JumpForce => _jumpForce;
    public float CoyoteTime => _coyoteTime;
    public float JumpBufferTime => _jumpBufferTime;
    public float HorizontalInput => _horizontalInput;
    public float VerticalInput => _verticalInput;
    public Transform OrientationTransform => _playerOrientationTransform != null ? _playerOrientationTransform : transform;

    // Collider orijinal boyutları
    private CapsuleCollider _capsuleCollider;
    private BoxCollider _boxCollider;
    private float _origCapsuleHeight;
    private Vector3 _origCapsuleCenter;
    private Vector3 _origBoxSize;
    private Vector3 _origBoxCenter;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
        if (_playerOrientationTransform == null)
        {
            var orient = transform.Find("Orientation");
            if (orient != null) _playerOrientationTransform = orient;
        }
        if (_stateController == null) _stateController = GetComponent<StateController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_playerStats != null)
        {
            SyncFieldsFromSO(_playerStats);
        }
    }

    private void Reset()
    {
        _playerCollider = GetComponent<Collider>();
        var orient = transform.Find("Orientation");
        if (orient != null) _playerOrientationTransform = orient;
        _stateController = GetComponent<StateController>();
        _animator = GetComponentInChildren<Animator>();
    }
#endif

    private void Awake()
    {
        // 0. Merkezi ScriptableObject verilerini uygula
        if (_playerStats != null)
        {
            SyncFieldsFromSO(_playerStats);
        }

        // 1. Rigidbody Kurulumu
        _playerRigidbody = GetComponent<Rigidbody>();
        if (_playerRigidbody == null)
        {
            _playerRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _playerRigidbody.freezeRotation = true;
        _playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _playerRigidbody.linearDamping = _groundDrag;

        // 2. Yönlendirme Transformu
        if (_playerOrientationTransform == null)
        {
            _playerOrientationTransform = transform;
        }

        // 3. Collider Kurulumu (Inspector'da atanmadıysa bileşeni otomatik bul)
        if (_playerCollider == null)
        {
            _playerCollider = GetComponent<Collider>();
        }

        // 4. StateController Kurulumu
        if (_stateController == null)
        {
            _stateController = GetComponent<StateController>();
            if (_stateController == null)
            {
                _stateController = gameObject.AddComponent<StateController>();
            }
        }

        // 5. Animator Kurulumu
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
        if (_animator != null)
        {
            // Hareket Rigidbody ile yönetildiği için animasyon root motion'ı fiziğe karışmamalı.
            _animator.applyRootMotion = false;
        }
        CacheAnimatorParameters();

        CacheColliderProperties();

        _canJump = true;
        _jumpCooldownTimer = 0f;
        _coyoteTimeCounter = 0f;
        _jumpBufferCounter = 0f;
        _externalMovementOverride = false;
    }

    private void SyncFieldsFromSO(PlayerStatsSO stats)
    {
        if (stats == null) return;

        _moveSpeed = stats.moveSpeed;
        _maxSpeed = stats.maxSpeed;
        _groundDrag = stats.groundDrag;
        _airDrag = stats.airDrag;
        _airMultiplier = stats.airMultiplier;
        _jumpForce = stats.jumpForce;
        _jumpCooldown = stats.jumpCooldown;
        _coyoteTime = stats.coyoteTime;
        _jumpBufferTime = stats.jumpBufferTime;
        _maxSlideSpeed = stats.maxSlideSpeed;
        _slideImpulseForce = stats.slideImpulseForce;
        _slideSustainForce = stats.slideSustainForce;
        _slideDrag = stats.slideDrag;
        _maxSlideDuration = stats.maxSlideDuration;
        _adjustColliderOnSlide = stats.adjustColliderOnSlide;
        _slideColliderMultiplier = stats.slideColliderMultiplier;
        _groundCheckDistance = stats.groundCheckDistance;
        _groundCheckRadius = stats.groundCheckRadius;
    }

    public void ApplyStatsFromSO(PlayerStatsSO stats)
    {
        if (stats == null) return;

        _playerStats = stats;
        SyncFieldsFromSO(stats);

        if (_playerRigidbody == null)
        {
            _playerRigidbody = GetComponent<Rigidbody>();
        }

        if (_playerRigidbody != null && !_externalMovementOverride)
        {
            if (_isSliding)
            {
                _playerRigidbody.linearDamping = _slideDrag;
            }
            else
            {
                _playerRigidbody.linearDamping = _groundDrag;
            }
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetExternalMovementOverride(bool isOverridden)
    {
        if (_externalMovementOverride == isOverridden)
            return;

        _externalMovementOverride = isOverridden;

        if (_playerRigidbody == null)
        {
            _playerRigidbody = GetComponent<Rigidbody>();
        }

        if (_externalMovementOverride)
        {
            if (_isSliding)
            {
                StopSlide();
            }

            if (_playerRigidbody != null)
            {
                _playerRigidbody.linearDamping = 0f;
            }
        }
        else
        {
            if (_playerRigidbody != null)
            {
                ApplyDrag();
            }
        }
    }

    public Rigidbody GetRigidbody()
    {
        if (_playerRigidbody == null)
        {
            _playerRigidbody = GetComponent<Rigidbody>();
        }
        return _playerRigidbody;
    }

    public bool IsInputBlocked()
    {
        if (SettingsUI.IsOpen)
            return true;

        if (Time.timeScale <= 0f || Mathf.Approximately(Time.timeScale, 0f))
            return true;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return true;

        return false;
    }

    public bool RefreshGroundState()
    {
        _isGrounded = CheckGround();
        return _isGrounded;
    }

    public void StepTimers(float deltaTime)
    {
        _isGrounded = CheckGround();

        if (IsInputBlocked())
        {
            ClearBlockedInputs();
            return;
        }

        UpdateJumpTimers(deltaTime);
        TryConsumeBufferedJump();
        UpdateSlide(deltaTime);
    }

    private void ClearBlockedInputs()
    {
        _horizontalInput = 0f;
        _verticalInput = 0f;
        _moveDirection = Vector3.zero;
        _jumpBufferCounter = 0f;

        if (_isSliding)
        {
            StopSlide();
        }
    }

    private void CacheColliderProperties()
    {
        if (_playerCollider is CapsuleCollider capsule)
        {
            _capsuleCollider = capsule;
            _origCapsuleHeight = capsule.height;
            _origCapsuleCenter = capsule.center;
        }
        else if (_playerCollider is BoxCollider box)
        {
            _boxCollider = box;
            _origBoxSize = box.size;
            _origBoxCenter = box.center;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _isGrounded = CheckGround();

        bool blocked = IsInputBlocked();
        if (!blocked)
        {
            UpdateJumpTimers(dt);
        }

        HandleInputs();

        if (!blocked)
        {
            TryConsumeBufferedJump();
            UpdateSlide(dt);
        }

        SetStates();
        UpdateAnimation();
    }

    private void UpdateJumpTimers(float deltaTime)
    {
        if (deltaTime < 0f) deltaTime = 0f;

        if (!_canJump)
        {
            _jumpCooldownTimer -= deltaTime;
            if (_jumpCooldownTimer <= 0f)
            {
                ResetJump();
            }
        }

        if (_isGrounded && _canJump)
        {
            _coyoteTimeCounter = _coyoteTime;
        }
        else
        {
            _coyoteTimeCounter = Mathf.Max(0f, _coyoteTimeCounter - deltaTime);
        }

        if (_jumpBufferCounter > 0f)
        {
            _jumpBufferCounter = Mathf.Max(0f, _jumpBufferCounter - deltaTime);
        }
    }

    private void UpdateSlide(float deltaTime)
    {
        if (!_isSliding) return;

        if (_maxSlideDuration > 0f)
        {
            _slideTimer -= Mathf.Max(0f, deltaTime);
            if (_slideTimer <= 0f)
            {
                StopSlide();
                return;
            }
        }

        // Karakter tamamen durduysa slide'ı bitir
        if (_playerRigidbody != null)
        {
            Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);
            if (flatVel.magnitude < 0.5f)
            {
                StopSlide();
            }
        }
    }

    private void FixedUpdate()
    {
        if (_externalMovementOverride)
        {
            return;
        }

        ApplyDrag();
        MovePlayer();
        LimitSpeed();
    }

    private void HandleInputs()
    {
        if (IsInputBlocked())
        {
            ClearBlockedInputs();
            return;
        }

        float h = 0f;
        float v = 0f;
        bool jumpPressed = false;
        bool slidePressed = false;
        bool slideHeld = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;

            if (kb.spaceKey.wasPressedThisFrame) jumpPressed = true;
            if (kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame) slidePressed = true;
            if (kb.leftCtrlKey.isPressed || kb.cKey.isPressed) slideHeld = true;
        }
#endif

        // Eski Input fallback
        try
        {
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }

            if (!jumpPressed && Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
            if (!slidePressed && Input.GetKeyDown(_slideKey)) slidePressed = true;
            if (!slideHeld && Input.GetKey(_slideKey)) slideHeld = true;
        }
        catch
        {
            // Input System paketi tek başına aktifse eski API sessizce atlanır
        }

        ProcessInputs(h, v, jumpPressed, slidePressed, slideHeld);
    }

    public void ProcessInputs(float horizontal, float vertical, bool jumpPressed, bool slidePressed, bool slideHeld)
    {
        if (IsInputBlocked())
        {
            ClearBlockedInputs();
            return;
        }

        _horizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
        _verticalInput = Mathf.Clamp(vertical, -1f, 1f);

        // Jump Buffering: zıplama tuşuna basıldığında tampon süresini başlat
        if (jumpPressed)
        {
            _jumpBufferCounter = _jumpBufferTime;
        }

        // Slide Başlatma / Durdurma
        if (slidePressed && !_isSliding && _isGrounded && !_externalMovementOverride)
        {
            StartSlide();
        }
        else if (_isSliding && !slideHeld && _maxSlideDuration <= 0f)
        {
            StopSlide();
        }

        // Zıplama (Coyote Time & Jump Buffering destekli)
        TryConsumeBufferedJump(jumpPressed);
    }

    private bool TryConsumeBufferedJump(bool jumpPressedThisFrame = false)
    {
        if (_externalMovementOverride || !_canJump || IsInputBlocked())
            return false;

        bool hasJumpIntent = jumpPressedThisFrame || _jumpBufferCounter > 0f;
        bool hasGroundSupport = _isGrounded || _coyoteTimeCounter > 0f;

        if (hasJumpIntent && hasGroundSupport)
        {
            if (_isSliding) StopSlide();
            Jump();
            return true;
        }

        return false;
    }

    private void MovePlayer()
    {
        if (_externalMovementOverride || _playerRigidbody == null)
            return;

        if (IsInputBlocked())
        {
            _moveDirection = Vector3.zero;
            return;
        }

        Transform moveRef = _playerOrientationTransform != null ? _playerOrientationTransform : transform;

        _moveDirection = moveRef.forward * _verticalInput + moveRef.right * _horizontalInput;

        float effectiveMoveSpeed = _moveSpeed * _speedMultiplier;

        if (_isSliding)
        {
            // Slide halindeyken slide yönünde kuvvet uygula
            _playerRigidbody.AddForce(_slideDirection * (_slideSustainForce * _speedMultiplier), ForceMode.Force);
        }
        else if (_isGrounded)
        {
            // Yerde normal hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * effectiveMoveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            // Havadayken kontrollü hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * effectiveMoveSpeed * 10f * _airMultiplier, ForceMode.Force);
        }
    }

    private void LimitSpeed()
    {
        if (_externalMovementOverride || _playerRigidbody == null)
            return;

        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);

        // Slide durumuna ve harici hız çarpanına göre uygun maksimum hız sınırı seçilir
        float currentMaxSpeed = (_isSliding ? _maxSlideSpeed : _maxSpeed) * _speedMultiplier;

        if (flatVel.magnitude > currentMaxSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * currentMaxSpeed;
            _playerRigidbody.linearVelocity = new Vector3(limitedVel.x, _playerRigidbody.linearVelocity.y, limitedVel.z);
        }
    }

    private void ApplyDrag()
    {
        if (_externalMovementOverride || _playerRigidbody == null)
            return;

        // Inspector'dan veya SO'dan değiştirilen drag değerleri anında Rigidbody'ye yansıtılır
        if (_isSliding)
        {
            _playerRigidbody.linearDamping = _slideDrag;
        }
        else if (_isGrounded)
        {
            _playerRigidbody.linearDamping = _groundDrag;
        }
        else
        {
            _playerRigidbody.linearDamping = _airDrag;
        }
    }

    private void Jump()
    {
        if (_playerRigidbody == null)
            return;

        _canJump = false;
        _jumpCooldownTimer = _jumpCooldown;
        _coyoteTimeCounter = 0f;
        _jumpBufferCounter = 0f;

        // Dikey hızı sıfırlayıp net bir zıplama kuvveti uygula
        Vector3 currentVel = _playerRigidbody.linearVelocity;
        _playerRigidbody.linearVelocity = new Vector3(currentVel.x, 0f, currentVel.z);

        _playerRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

        SetAnimTrigger(_hashJump);
    }

    private void ResetJump()
    {
        _canJump = true;
        _jumpCooldownTimer = 0f;
    }

    private void StartSlide()
    {
        if (_playerRigidbody == null)
            return;

        _isSliding = true;
        _slideTimer = _maxSlideDuration;

        Transform moveRef = _playerOrientationTransform != null ? _playerOrientationTransform : transform;
        Vector3 inputDir = moveRef.forward * _verticalInput + moveRef.right * _horizontalInput;

        // Slide yönü: Karakter hareket ediyorsa hareket yönü, duruyorsa baktığı yön
        if (inputDir.sqrMagnitude > 0.01f)
        {
            _slideDirection = inputDir.normalized;
        }
        else if (_moveDirection.sqrMagnitude > 0.01f)
        {
            _slideDirection = _moveDirection.normalized;
        }
        else
        {
            _slideDirection = moveRef.forward;
        }

        // Anlık itiş kuvveti (Impulse)
        _playerRigidbody.AddForce(_slideDirection * (_slideImpulseForce * _speedMultiplier), ForceMode.Impulse);

        // Collider boyutunu küçült
        if (_adjustColliderOnSlide)
        {
            ShrinkCollider();
        }
    }

    private void StopSlide()
    {
        _isSliding = false;

        // Collider boyutunu eski haline getir
        if (_adjustColliderOnSlide)
        {
            RestoreCollider();
        }
    }

    private void ShrinkCollider()
    {
        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _origCapsuleHeight * _slideColliderMultiplier;
            _capsuleCollider.center = new Vector3(_origCapsuleCenter.x, _origCapsuleCenter.y * _slideColliderMultiplier, _origCapsuleCenter.z);
        }
        else if (_boxCollider != null)
        {
            _boxCollider.size = new Vector3(_origBoxSize.x, _origBoxSize.y * _slideColliderMultiplier, _origBoxSize.z);
            _boxCollider.center = new Vector3(_origBoxCenter.x, _origBoxCenter.y * _slideColliderMultiplier, _origBoxCenter.z);
        }
    }

    private void RestoreCollider()
    {
        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _origCapsuleHeight;
            _capsuleCollider.center = _origCapsuleCenter;
        }
        else if (_boxCollider != null)
        {
            _boxCollider.size = _origBoxSize;
            _boxCollider.center = _origBoxCenter;
        }
    }

    private Vector3 GetGroundCheckOrigin()
    {
        if (_playerCollider != null)
        {
            // Collider'ın alt tabanının biraz üstünden başlat
            return new Vector3(_playerCollider.bounds.center.x, _playerCollider.bounds.min.y + _groundCheckRadius, _playerCollider.bounds.center.z);
        }
        return transform.position + Vector3.up * _groundCheckRadius;
    }

    private bool CheckGround()
    {
        Vector3 origin = GetGroundCheckOrigin();
        float castDistance = _groundCheckDistance;

        // SphereCast ile zemini güvenle algıla
        RaycastHit[] hits = Physics.SphereCastAll(origin, _groundCheckRadius, Vector3.down, castDistance, _groundLayer, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            // Kendi collider'ına çarpmayı engelle
            if (_playerCollider != null && hit.collider == _playerCollider) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;

            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showGroundGizmo) return;

        Vector3 origin = GetGroundCheckOrigin();
        Gizmos.color = _isGrounded ? Color.green : Color.red;

        // Başlangıç küresi
        Gizmos.DrawWireSphere(origin, _groundCheckRadius);

        // Bitiş küresi ve bağlantı ışını
        Vector3 endPoint = origin + Vector3.down * _groundCheckDistance;
        Gizmos.DrawWireSphere(endPoint, _groundCheckRadius);
        Gizmos.DrawLine(origin, endPoint);
    }

    private void SetStates()
    {
        if (_stateController == null) return;

        var currentState = _stateController.GetCurrentState();
        if (currentState == PlayerState.Dash)
        {
            return;
        }

        var movementDirection = GetMovementDirection();

        var newStateToSet = currentState switch
        {
            _ when !_isGrounded || !_canJump => PlayerState.Jump,
            _ when _isSliding && movementDirection != Vector3.zero => PlayerState.Slide,
            _ when _isSliding && movementDirection == Vector3.zero => PlayerState.SlideIdle,
            _ when _isGrounded && movementDirection != Vector3.zero => PlayerState.Move,
            _ when _isGrounded && movementDirection == Vector3.zero => PlayerState.Idle,
            _ => currentState
        };

        if (newStateToSet != currentState)
        {
            _stateController.ChangeState(newStateToSet);
            Debug.Log($"[Player State] Durum Değişti: {currentState} -> {newStateToSet}");
        }
    }

    private Vector3 GetMovementDirection()
    {
        return _moveDirection.normalized;
    }

    public void SetMovementReference(Transform movementReference)
    {
        if (movementReference != null)
        {
            _playerOrientationTransform = movementReference;
        }
    }

    #region Animation Handling
    // Animasyon Parametre Hash'leri
    private static readonly int _hashIsMoving = Animator.StringToHash("isMoving");
    private static readonly int _hashWalking = Animator.StringToHash("Walking");
    private static readonly int _hashRunning = Animator.StringToHash("Running");
    private static readonly int _hashSpeed = Animator.StringToHash("Speed");
    private static readonly int _hashMotionScale = Animator.StringToHash("MotionScale");
    private static readonly int _hashDirX = Animator.StringToHash("DirX");
    private static readonly int _hashDirZ = Animator.StringToHash("DirZ");
    private static readonly int _hashIsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int _hashIsSliding = Animator.StringToHash("isSliding");
    private static readonly int _hashState = Animator.StringToHash("State");
    private static readonly int _hashJump = Animator.StringToHash("Jump");

    private readonly System.Collections.Generic.HashSet<int> _animatorParams = new System.Collections.Generic.HashSet<int>();

    private void CacheAnimatorParameters()
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;
        _animatorParams.Clear();
        foreach (var p in _animator.parameters)
        {
            _animatorParams.Add(p.nameHash);
        }
    }

    private void UpdateAnimation()
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || _playerRigidbody == null) return;

        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);
        float speed = flatVel.magnitude;
        bool isMoving = speed > 0.15f && _isGrounded && !_isSliding;

        PlayerState state = _stateController != null
            ? _stateController.GetCurrentState()
            : (isMoving ? PlayerState.Move : PlayerState.Idle);

        float effectiveMaxSpeed = _maxSpeed * _speedMultiplier;

        SetAnimBool(_hashIsMoving, isMoving);
        SetAnimBool(_hashWalking, state == PlayerState.Move);
        SetAnimBool(_hashRunning, false);
        SetAnimFloat(_hashSpeed, speed);
        SetAnimFloat(_hashMotionScale, effectiveMaxSpeed > 0f ? speed / effectiveMaxSpeed : speed);
        SetAnimFloat(_hashDirX, _horizontalInput);
        SetAnimFloat(_hashDirZ, _verticalInput);
        SetAnimBool(_hashIsGrounded, _isGrounded);
        SetAnimBool(_hashIsSliding, _isSliding);

        if (state != _lastAnimatedState)
        {
            PlayStateAnimation(state);
            _lastAnimatedState = state;
        }

        SetAnimInt(_hashState, (int)state);
    }

    private void SetAnimBool(int hash, bool value)
    {
        if (_animatorParams.Contains(hash)) _animator.SetBool(hash, value);
    }

    private void SetAnimFloat(int hash, float value)
    {
        if (_animatorParams.Contains(hash)) _animator.SetFloat(hash, value);
    }

    private void SetAnimInt(int hash, int value)
    {
        if (_animatorParams.Contains(hash)) _animator.SetInteger(hash, value);
    }

    private void SetAnimTrigger(int hash)
    {
        if (_animatorParams.Contains(hash)) _animator.SetTrigger(hash);
    }

    private void PlayStateAnimation(PlayerState state)
    {
        string stateName = state switch
        {
            PlayerState.Dash => _slideAnimation,
            PlayerState.Move => _moveAnimation,
            PlayerState.Jump => _jumpAnimation,
            PlayerState.Slide => _slideAnimation,
            PlayerState.SlideIdle => _idleAnimation,
            _ => _idleAnimation
        };

        int stateHash = Animator.StringToHash(stateName);
        if (_animator.HasState(0, stateHash))
        {
            _animator.CrossFadeInFixedTime(stateHash, 0.12f);
        }

        if (state == PlayerState.Jump)
        {
            SetAnimTrigger(_hashJump);
        }
    }

    #endregion
}
