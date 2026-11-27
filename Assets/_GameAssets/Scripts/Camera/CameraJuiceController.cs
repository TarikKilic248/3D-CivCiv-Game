using System;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// CivCiv 3D - Kamera Hissiyatı ve Dinamik Efekt Yöneticisi (CameraJuiceController)
/// Dash/kayma anında dinamik FOV genişlemesi ve darbe anında sönümlü kamera sarsıntısı (Screen Shake) uygular.
/// </summary>
public class CameraJuiceController : MonoBehaviour
{
    public static CameraJuiceController Instance { get; private set; }

    [Header("--- References ---")]
    [Tooltip("Hedef Cinemachine Kamerası (Boş ise sahnede otomatik taranır)")]
    [SerializeField] private CinemachineCamera _cinemachineCamera;

    [Tooltip("Standart Unity Camera bileşeni")]
    [SerializeField] private Camera _fallbackCamera;

    [Tooltip("Sarsıntı uygulanacak transform (Kamera veya pivot nesnesi)")]
    [SerializeField] private Transform _shakeTargetTransform;

    [Header("--- Target Actors ---")]
    [Tooltip("Oyuncu kontrolcüsü (Kayma tespiti için)")]
    [SerializeField] private PlayerController _playerController;

    [Tooltip("Dash kontrolcüsü (Dash tespiti için)")]
    [SerializeField] private DashController _dashController;

    [Tooltip("Can bileşeni (Hasar sarsıntısı için)")]
    [SerializeField] private PlayerHealth _playerHealth;

    [Header("--- FOV (Görüş Açısı) Juice ---")]
    [Tooltip("Varsayılan kamera FOV değeri")]
    [SerializeField] private float _defaultFOV = 60f;

    [Tooltip("Dash ve kayma anında FOV artış miktarı")]
    [SerializeField] private float _fovBoostAmount = 10f;

    [Tooltip("FOV yumuşak geçiş hızı")]
    [SerializeField] private float _fovSmoothSpeed = 10f;

    [Header("--- Camera Shake Settings ---")]
    [Tooltip("Hasar anında tetiklenen varsayılan sarsıntı şiddeti")]
    [SerializeField] private float _damageShakeIntensity = 0.45f;

    [Tooltip("Hasar anında sarsıntı süresi (saniye)")]
    [SerializeField] private float _damageShakeDuration = 0.28f;

    [Tooltip("Sarsıntı titreşim frekansı")]
    [SerializeField] private float _shakeFrequency = 24f;

    // Çalışma Zamanı FOV Değişkenleri
    private float _currentFOV = 60f;
    private bool _manualBoostActive = false;
    private float _manualBoostTimer = 0f;

    // Çalışma Zamanı Shake Değişkenleri
    private float _shakeIntensity = 0f;
    private float _shakeDuration = 0f;
    private float _shakeTimer = 0f;
    private Vector3 _currentShakeOffset = Vector3.zero;
    private Vector3 _initialShakeLocalPosition = Vector3.zero;
    private int _lastObservedHealth = -1;

    // Public Properties
    public float DefaultFOV { get => _defaultFOV; set => _defaultFOV = value; }
    public float FOVBoostAmount { get => _fovBoostAmount; set => _fovBoostAmount = value; }
    public float BoostFOV => _defaultFOV + _fovBoostAmount;
    public float CurrentFOV => _currentFOV;
    public float TargetFOV => IsBoosting ? BoostFOV : _defaultFOV;
    public bool IsBoosting => _manualBoostActive || (_manualBoostTimer > 0f) ||
                              (_dashController != null && _dashController.IsDashing) ||
                              (_playerController != null && _playerController.IsSliding);

    public Vector3 CurrentShakeOffset => _currentShakeOffset;
    public float ShakeIntensity => _shakeIntensity;
    public float ShakeTimer => _shakeTimer;
    public float ShakeDuration => _shakeDuration;
    public CinemachineCamera TargetCamera { get => _cinemachineCamera; set => _cinemachineCamera = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveDependencies();
        _currentFOV = _defaultFOV;

        if (_shakeTargetTransform != null)
        {
            _initialShakeLocalPosition = _shakeTargetTransform.localPosition;
        }
    }

    private void Start()
    {
        ResolveDependencies();
        SubscribeEvents();
        ApplyFOVToCameras(_currentFOV);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    public void ResolveDependencies()
    {
        // 1. CinemachineCamera bul
        if (_cinemachineCamera == null)
        {
            ThirdPersonCameraController tpc = FindAnyObjectByType<ThirdPersonCameraController>();
            if (tpc != null && tpc.ActiveCamera != null)
            {
                _cinemachineCamera = tpc.ActiveCamera;
            }
            else
            {
                var cams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
                if (cams.Length > 0)
                {
                    _cinemachineCamera = cams[0];
                }
            }
        }

        // 2. Fallback camera
        if (_fallbackCamera == null)
        {
            _fallbackCamera = Camera.main;
            if (_fallbackCamera == null)
            {
                _fallbackCamera = FindAnyObjectByType<Camera>();
            }
        }

        // Başlangıç varsayılan FOV oku
        if (_cinemachineCamera != null)
        {
            _defaultFOV = _cinemachineCamera.Lens.FieldOfView;
            if (_shakeTargetTransform == null)
            {
                _shakeTargetTransform = _cinemachineCamera.transform;
            }
        }
        else if (_fallbackCamera != null)
        {
            _defaultFOV = _fallbackCamera.fieldOfView;
            if (_shakeTargetTransform == null)
            {
                _shakeTargetTransform = _fallbackCamera.transform;
            }
        }

        if (_shakeTargetTransform != null && _initialShakeLocalPosition == Vector3.zero)
        {
            _initialShakeLocalPosition = _shakeTargetTransform.localPosition;
        }

        // 3. Karakter referansları
        if (_playerController == null)
        {
            _playerController = FindAnyObjectByType<PlayerController>();
        }
        if (_playerController != null)
        {
            if (_dashController == null)
            {
                _dashController = _playerController.GetComponent<DashController>();
            }
            if (_playerHealth == null)
            {
                _playerHealth = _playerController.GetComponent<PlayerHealth>();
            }
        }
        else
        {
            if (_dashController == null) _dashController = FindAnyObjectByType<DashController>();
            if (_playerHealth == null) _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _lastObservedHealth = _playerHealth.CurrentHealth;
        }
    }

    public void SubscribeEvents()
    {
        UnsubscribeEvents();

        if (_dashController != null)
        {
            _dashController.OnDashStarted += HandleDashStarted;
            _dashController.OnDashEnded += HandleDashEnded;
        }

        if (_playerController != null)
        {
            _playerController.OnSlideStarted += HandleSlideStarted;
            _playerController.OnSlideEnded += HandleSlideEnded;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_dashController != null)
        {
            _dashController.OnDashStarted -= HandleDashStarted;
            _dashController.OnDashEnded -= HandleDashEnded;
        }

        if (_playerController != null)
        {
            _playerController.OnSlideStarted -= HandleSlideStarted;
            _playerController.OnSlideEnded -= HandleSlideEnded;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleDashStarted() => SetBoostActive(true);
    private void HandleDashEnded() => SetBoostActive(false);
    private void HandleSlideStarted() => SetBoostActive(true);
    private void HandleSlideEnded() => SetBoostActive(false);

    private void HandleHealthChanged(int current, int max)
    {
        if (_lastObservedHealth > 0 && current < _lastObservedHealth)
        {
            TriggerShake(_damageShakeIntensity, _damageShakeDuration);
        }
        _lastObservedHealth = current;
    }

    public void SetBoostActive(bool active)
    {
        _manualBoostActive = active;
        if (!active)
        {
            _manualBoostTimer = 0f;
        }
    }

    public void TriggerFOVBoost(float duration, float extraFOV = -1f)
    {
        if (extraFOV > 0f)
        {
            _fovBoostAmount = extraFOV;
        }
        _manualBoostTimer = Mathf.Max(_manualBoostTimer, duration);
    }

    /// <summary>
    /// Belirtilen şiddet ve sürede sönümlü kamera sarsıntısı başlatır.
    /// </summary>
    public void TriggerShake(float intensity, float duration, float frequency = 24f)
    {
        if (intensity <= 0f || duration <= 0f) return;

        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        _shakeDuration = duration;
        _shakeTimer = duration;
        _shakeFrequency = frequency;
    }

    public void Step(float deltaTime)
    {
        StepFOV(deltaTime);
        StepShake(deltaTime);
    }

    public void StepFOV(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (_manualBoostTimer > 0f)
        {
            _manualBoostTimer = Mathf.Max(0f, _manualBoostTimer - deltaTime);
        }

        float target = TargetFOV;
        float t = 1f - Mathf.Exp(-_fovSmoothSpeed * deltaTime);
        _currentFOV = Mathf.Lerp(_currentFOV, target, t);

        ApplyFOVToCameras(_currentFOV);
    }

    public void StepShake(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (_shakeTimer > 0f)
        {
            _shakeTimer = Mathf.Max(0f, _shakeTimer - deltaTime);
            float decay = _shakeDuration > 0f ? Mathf.Clamp01(_shakeTimer / _shakeDuration) : 0f;

            // Harmonik ve Perlin destekli sönümlü ofset hesaplama
            float timeSample = Time.time * _shakeFrequency;
            float nx = (Mathf.PerlinNoise(timeSample, 0.15f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0.35f, timeSample) - 0.5f) * 2f;
            float nz = (Mathf.Sin(timeSample * 1.5f)) * 0.5f;

            _currentShakeOffset = new Vector3(nx, ny, nz) * (_shakeIntensity * decay);

            if (_shakeTimer <= 0f)
            {
                _currentShakeOffset = Vector3.zero;
                _shakeIntensity = 0f;
            }
        }
        else
        {
            _currentShakeOffset = Vector3.zero;
            _shakeIntensity = 0f;
        }
    }

    private void ApplyFOVToCameras(float fov)
    {
        if (_cinemachineCamera != null)
        {
            var lens = _cinemachineCamera.Lens;
            lens.FieldOfView = fov;
            _cinemachineCamera.Lens = lens;
        }

        if (_fallbackCamera != null)
        {
            _fallbackCamera.fieldOfView = fov;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        Step(dt);
    }

    private void LateUpdate()
    {
        // Sarsıntı ofsetini kamera veya hedef nesneye uygula
        if (_shakeTargetTransform != null)
        {
            _shakeTargetTransform.localPosition = _initialShakeLocalPosition + _currentShakeOffset;
        }
    }
}
