using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
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
    [SerializeField] [Range(0.2f, 1f)] private float _slideColliderMultiplier = 0.5f;

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

    // Özel Değişkenler
    private Rigidbody _playerRigidbody;
    private Vector3 _moveDirection;
    private float _horizontalInput, _verticalInput;
    private bool _isGrounded;
    private bool _isSliding = false;
    private float _slideTimer = 0f;
    private Vector3 _slideDirection;

    // Collider orijinal boyutları
    private CapsuleCollider _capsuleCollider;
    private BoxCollider _boxCollider;
    private float _origCapsuleHeight;
    private Vector3 _origCapsuleCenter;
    private Vector3 _origBoxSize;
    private Vector3 _origBoxCenter;

    private void Awake()
    {
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
        if (_playerOrientationTransform == null) _playerOrientationTransform = transform;

        _moveDirection = _playerOrientationTransform.forward * _verticalInput + _playerOrientationTransform.right * _horizontalInput;

        if (_isSliding)
        {
            // Slide halindeyken slide yönünde kuvvet uygula
            _playerRigidbody.AddForce(_slideDirection * _slideSustainForce, ForceMode.Force);
        }
        else if (_isGrounded)
        {
            // Yerde normal hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * _moveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            // Havadayken kontrollü hareket
            _playerRigidbody.AddForce(_moveDirection.normalized * _moveSpeed * 10f * _airMultiplier, ForceMode.Force);
        }
    }

    private void LimitSpeed()
    {
        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);

        // Slide durumuna göre uygun maksimum hız sınırı seçilir
        float currentMaxSpeed = _isSliding ? _maxSlideSpeed : _maxSpeed;

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
            _slideDirection = _playerOrientationTransform.forward;
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
}
