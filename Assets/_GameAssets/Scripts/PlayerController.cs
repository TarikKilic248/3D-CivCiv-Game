using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("--- Data Architecture (ScriptableObject) ---")]
    [Tooltip("Karakter istatistiklerini içeren ScriptableObject (Atanırsa değerleri otomatik yükler)")]
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
    private bool _canJump = true;

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
    private float _speedMultiplier = 1f;
    private PlayerState _lastAnimatedState = (PlayerState)(-1);

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
        if (_playerStats != null)
        {
            ApplyStatsFromSO(_playerStats);
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
        _isGrounded = CheckGround();

        HandleInputs();

        SetStates();

        UpdateAnimation();

        // Slide süresi yönetimi
        if (_isSliding)
        {
            if (_maxSlideDuration > 0f)
            {
                _slideTimer -= Time.deltaTime;
                if (_slideTimer <= 0f)
                {
                    StopSlide();
                }
            }

            // Karakter tamamen durduysa slide'ı bitir
            Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);
            if (flatVel.magnitude < 0.5f)
            {
                StopSlide();
            }
        }
    }

    private void FixedUpdate()
    {
        ApplyDrag();
        MovePlayer();
        LimitSpeed();
    }

    private void HandleInputs()
    {
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

        _horizontalInput = h;
        _verticalInput = v;

        // Slide Başlatma / Durdurma
        if (slidePressed && !_isSliding && _isGrounded)
        {
            StartSlide();
        }
        else if (_isSliding && !slideHeld && _maxSlideDuration <= 0f)
        {
            StopSlide();
        }

        // Zıplama
        if (jumpPressed && _canJump && _isGrounded)
        {
            if (_isSliding) StopSlide();
            Jump();
        }
    }

    private void MovePlayer()
    {
        Transform moveRef = _playerOrientationTransform != null ? _playerOrientationTransform : transform;

        _moveDirection = moveRef.forward * _verticalInput + moveRef.right * _horizontalInput;

        if (_isSliding)
        {
            // Slide halindeyken slide yönünde kuvvet uygula
            _playerRigidbody.AddForce(_slideDirection * _slideSustainForce, ForceMode.Force);
        }
        else if (_isGrounded)
        {
            // Yerde normal hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * (_moveSpeed * _speedMultiplier * 10f), ForceMode.Force);
        }
        else
        {
            // Havadayken kontrollü hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * (_moveSpeed * _speedMultiplier * 10f * _airMultiplier), ForceMode.Force);
        }
    }

    private void LimitSpeed()
    {
        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);

        // Slide durumuna göre uygun maksimum hız sınırı seçilir
        float currentMaxSpeed = (_isSliding ? _maxSlideSpeed : _maxSpeed) * _speedMultiplier;

        if (flatVel.magnitude > currentMaxSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * currentMaxSpeed;
            _playerRigidbody.linearVelocity = new Vector3(limitedVel.x, _playerRigidbody.linearVelocity.y, limitedVel.z);
        }
    }

    private void ApplyDrag()
    {
        // Inspector'dan değiştirilen drag değerleri anında Rigidbody'ye yansıtılır
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
        _canJump = false;

        // Dikey hızı sıfırlayıp net bir zıplama kuvveti uygula
        Vector3 currentVel = _playerRigidbody.linearVelocity;
        _playerRigidbody.linearVelocity = new Vector3(currentVel.x, 0f, currentVel.z);

        _playerRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

        SetAnimTrigger(_hashJump);

        Invoke(nameof(ResetJump), _jumpCooldown);
    }

    private void ResetJump()
    {
        _canJump = true;
    }

    private void StartSlide()
    {
        _isSliding = true;
        _slideTimer = _maxSlideDuration;

        // Slide yönü: Karakter hareket ediyorsa hareket yönü, duruyorsa baktığı yön
        if (_moveDirection.sqrMagnitude > 0.01f)
        {
            _slideDirection = _moveDirection.normalized;
        }
        else
        {
            Transform moveRef = _playerOrientationTransform != null ? _playerOrientationTransform : transform;
            _slideDirection = moveRef.forward;
        }

        // Anlık itiş kuvveti (Impulse)
        _playerRigidbody.AddForce(_slideDirection * _slideImpulseForce, ForceMode.Impulse);

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

        var movementDirection = GetMovementDirection();
        var currentState = _stateController.GetCurrentState();

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

    public void ApplyStatsFromSO(PlayerStatsSO stats)
    {
        if (stats == null) return;
        _playerStats = stats;
        _moveSpeed = stats.MoveSpeed;
        _maxSpeed = stats.MaxSpeed;
        _groundDrag = stats.GroundDrag;
        _airDrag = stats.AirDrag;
        _airMultiplier = stats.AirMultiplier;
        _jumpForce = stats.JumpForce;
        _jumpCooldown = stats.JumpCooldown;
        _maxSlideSpeed = stats.MaxSlideSpeed;
        _slideImpulseForce = stats.SlideImpulseForce;
        _slideSustainForce = stats.SlideSustainForce;
        _slideDrag = stats.SlideDrag;
    }

    public Rigidbody GetRigidbody() => _playerRigidbody;

    public Transform GetOrientation() => _playerOrientationTransform != null ? _playerOrientationTransform : transform;

    public bool IsGrounded() => _isGrounded;

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Max(0.1f, multiplier);
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
        if (_animator == null) return;
        _animatorParams.Clear();
        foreach (var p in _animator.parameters)
        {
            _animatorParams.Add(p.nameHash);
        }
    }

    private void UpdateAnimation()
    {
        if (_animator == null) return;

        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);
        float speed = flatVel.magnitude;
        bool isMoving = speed > 0.15f && _isGrounded && !_isSliding;

        PlayerState state = _stateController != null
            ? _stateController.GetCurrentState()
            : (isMoving ? PlayerState.Move : PlayerState.Idle);

        SetAnimBool(_hashIsMoving, isMoving);
        SetAnimBool(_hashWalking, state == PlayerState.Move);
        SetAnimBool(_hashRunning, false);
        SetAnimFloat(_hashSpeed, speed);
        SetAnimFloat(_hashMotionScale, _maxSpeed > 0f ? speed / _maxSpeed : speed);
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
