using UnityEngine;

/// <summary>
/// CivCiv 3D - Karakter Hareket Adım Sesi Tetikleyicisi (FootstepAudioTrigger)
/// Yürüme/koşma sırasında kat edilen mesafeyi biriktirerek veya Animator Event üzerinden rastgele pitch'li adım sesleri çalar.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FootstepAudioTrigger : MonoBehaviour
{
    [Header("--- Audio References ---")]
    [Tooltip("Merkezi ses kütüphanesi (Boş ise SpatialAudioManager üzerinden alınır)")]
    [SerializeField] private AudioLibrarySO _audioLibrary;

    [Tooltip("Doğrudan atanabilir alternatif adım sesleri")]
    [SerializeField] private AudioClip[] _footstepClips;

    [Header("--- Step Distance Settings ---")]
    [Tooltip("İki adım arasındaki mesafe eşiği (metre)")]
    [SerializeField] private float _stepDistance = 1.6f;

    [Tooltip("Adım sayımı için gereken minimum yatay hareket hızı")]
    [SerializeField] private float _minSpeedThreshold = 0.5f;

    [Tooltip("Adım sesi yüksekliği")]
    [SerializeField][Range(0f, 1f)] private float _footstepVolume = 0.65f;

    [Tooltip("Rastgele adım pitch aralığı")]
    [SerializeField] private float _minPitch = 0.88f;
    [SerializeField] private float _maxPitch = 1.14f;

    // Bağımlılıklar
    private PlayerController _playerController;
    private Rigidbody _rigidbody;

    // Çalışma Zamanı Değişkenleri
    private float _accumulatedDistance = 0f;
    public int FootstepCount { get; private set; } = 0;
    public float AccumulatedDistance => _accumulatedDistance;
    public float StepDistance { get => _stepDistance; set => _stepDistance = Mathf.Max(0.1f, value); }
    public float FootstepVolume { get => _footstepVolume; set => _footstepVolume = Mathf.Clamp01(value); }

    private void Awake()
    {
        ResolveDependencies();
    }

    public void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_audioLibrary == null && SpatialAudioManager.Instance != null)
        {
            _audioLibrary = SpatialAudioManager.Instance.Library;
        }
    }

    private void Update()
    {
        ResolveDependencies();

        bool isGrounded = _playerController != null ? _playerController.IsGrounded : true;
        float speed = 0f;

        if (_rigidbody != null)
        {
            Vector3 vel = _rigidbody.linearVelocity;
            speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        }

        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        StepFootstep(dt, speed, isGrounded);
    }

    /// <summary>
    /// Adım mesafesi birikimini ve tetiklemesini deterministik adım olarak yürütür.
    /// </summary>
    public void StepFootstep(float deltaTime, float currentSpeed, bool isGrounded)
    {
        if (deltaTime <= 0f) return;

        if (isGrounded && currentSpeed >= _minSpeedThreshold)
        {
            _accumulatedDistance += currentSpeed * deltaTime;
            if (_accumulatedDistance >= _stepDistance)
            {
                _accumulatedDistance = 0f;
                TriggerFootstep();
            }
        }
        else
        {
            // Durulduğunda birikim sıfırlanır
            _accumulatedDistance = Mathf.Max(0f, _accumulatedDistance - deltaTime * 0.5f);
        }
    }

    /// <summary>
    /// Adım sesini çalar ve sayacı artırır.
    /// </summary>
    public void TriggerFootstep()
    {
        FootstepCount++;

        AudioClip clip = GetFootstepClip();
        if (clip == null) return;

        if (SpatialAudioManager.Instance != null)
        {
            SpatialAudioManager.Instance.PlaySoundAtPoint(clip, transform.position, _footstepVolume, _minPitch, _maxPitch);
        }
        else
        {
            SettingsUI.PlaySFX(clip, _footstepVolume);
        }
    }

    /// <summary>
    /// Animator animasyon olayları (Animation Event) tarafından doğrudan çağrılabilir.
    /// </summary>
    public void OnFootstepAnimationEvent()
    {
        TriggerFootstep();
    }

    private AudioClip GetFootstepClip()
    {
        if (_footstepClips != null && _footstepClips.Length > 0)
        {
            int idx = UnityEngine.Random.Range(0, _footstepClips.Length);
            return _footstepClips[idx];
        }

        if (_audioLibrary != null)
        {
            return _audioLibrary.GetRandomFootstep();
        }

        return null;
    }

    public void ResetCounters()
    {
        FootstepCount = 0;
        _accumulatedDistance = 0f;
    }
}
