using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Orientation & Movement")]
    [SerializeField] private Transform _playerOriantationTransform;
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _groundDrag = 4f;

    [Header("Jumping")]
    [SerializeField] private float _jumpForce = 5f;

    private Rigidbody _playerRigidbody;
    private Vector3 _moveDirection;
    private float _horizantalInput, _verticalInput;

    private void Awake()
    {
        // 1. Rigidbody kontrolü ve güvenliği
        _playerRigidbody = GetComponent<Rigidbody>();
        if (_playerRigidbody == null)
        {
            _playerRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        // Karakterin dik durması ve yuvarlanmaması için rotasyonları donduruyoruz
        _playerRigidbody.freezeRotation = true;
        _playerRigidbody.linearDamping = _groundDrag; // Unity 6 / URP sürtünme desteği
        _playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // 2. Collider kontrolü (Synty karakterlerinde varsayılan olarak collider bulunmaz)
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.radius = 0.3f;
        }

        // 3. Oryantasyon atanmamışsa kendi transform'unu kullan
        if (_playerOriantationTransform == null)
        {
            _playerOriantationTransform = transform;
        }
    }

    private void Update()
    {
        SetInputs();
    }

    private void FixedUpdate()
    {
        SetPlayerMovement();
        SpeedControl();
    }

    private void SetInputs()
    {
        float h = 0f;
        float v = 0f;

        // 1. New Input System desteği (W-S-A-D ve Yön Okları)
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.spaceKey.wasPressedThisFrame)
            {
                _playerRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            }
        }
#endif

        // 2. Eski Input Sistemi desteği (New Input aktif değilse veya Gamepad için fallback)
        try
        {
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }
        }
        catch
        {
            // New Input System kısıtlaması varsa sessizce devam edilir
        }

        _horizantalInput = h;
        _verticalInput = v;
    }

    private void SetPlayerMovement()
    {
        if (_playerOriantationTransform == null)
        {
            _playerOriantationTransform = transform;
        }

        // İleri ve sağ yönünde hareket vektörü
        _moveDirection = _playerOriantationTransform.forward * _verticalInput + _playerOriantationTransform.right * _horizantalInput;

        if (_moveDirection.sqrMagnitude > 0.001f)
        {
            _playerRigidbody.AddForce(_moveDirection.normalized * _moveSpeed * 10f, ForceMode.Force);
        }
    }

    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(_playerRigidbody.linearVelocity.x, 0f, _playerRigidbody.linearVelocity.z);

        // Hız sınırını aşarsa sınırla
        if (flatVel.magnitude > _moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * _moveSpeed;
            _playerRigidbody.linearVelocity = new Vector3(limitedVel.x, _playerRigidbody.linearVelocity.y, limitedVel.z);
        }
    }
}
