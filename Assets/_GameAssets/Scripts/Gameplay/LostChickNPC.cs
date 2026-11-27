using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Kayıp CivCiv NPC Bileşeni (LostChickNPC)
/// Haritada kurtarılmayı bekleyen sevimli yavru civciv.
/// IInteractable arayüzü ile (E tuşu) veya doğrudan fiziksel temasla kurtarılır.
/// Kurtarıldıktan sonra oyuncuyu veya önündeki civcivi kuyruk halinde takip eder.
/// Dikey Dilim Standardı: Prosedürel seğirme/zıplama, ParticleSystem VFX ve SettingsUI.PlaySFX ses kancası içerir.
/// </summary>
[DisallowMultipleComponent]
public class LostChickNPC : MonoBehaviour, IInteractable
{
    private const string VFXRootName = "RescueVFXRoot";

    [Header("--- Visual & Model ---")]
    [Tooltip("Prosedürel hareket ve zıplama uygulanacak görsel kök nesnesi")]
    [SerializeField] private Transform _visualRoot;

    [Header("--- Rescue Feedback ---")]
    [Tooltip("Kurtarılma anında patlayacak yıldız/ışık partikül efekti")]
    [SerializeField] private ParticleSystem _rescueVFX;

    [Tooltip("Kurtarılma anında çalacak sevimli cikleme/kurtarma sesi")]
    [SerializeField] private AudioClip _rescueSfx;

    [Tooltip("Ses seviyesi (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.9f;

    [Header("--- Interaction & Contact ---")]
    [Tooltip("E tuşu ile etkileşimle kurtarılabilir mi?")]
    [SerializeField] private bool _rescueOnInteract = true;

    [Tooltip("Oyuncu temas ettiğinde otomatik kurtarılsın mı?")]
    [SerializeField] private bool _rescueOnTriggerEnter = true;

    [Tooltip("Etkileşim ipucu metni")]
    [SerializeField] private string _promptText = "Civcivi Kurtar (E)";

    [Header("--- Follow Settings ---")]
    [Tooltip("Takip edilecek hedef (Oyuncu veya önceki civciv)")]
    [SerializeField] private Transform _followTarget;

    [Tooltip("Takip mesafesi / durma aralığı (metre)")]
    [SerializeField] private float _followDistance = 1.3f;

    [Tooltip("Takip hareket hızı (metre/saniye)")]
    [SerializeField] private float _followSpeed = 5.5f;

    [Tooltip("Dönüş hızı (derece/saniye çarpanı)")]
    [SerializeField] private float _turnSpeed = 10f;

    // Runtime State
    private bool _isRescued;
    private float _idleBobTimer;
    private float _waddleTimer;
    private float _rescueFeedbackTimer;
    private Vector3 _baseVisualLocalPos = Vector3.zero;
    private Vector3 _baseVisualLocalScale = Vector3.one;
    private Quaternion _baseVisualLocalRot = Quaternion.identity;
    private Transform _cachedPoseTarget;

    // Events
    public event Action<LostChickNPC> OnRescued;

    // Public Properties
    public bool IsRescued => _isRescued;
    public Transform FollowTarget { get => _followTarget; set => _followTarget = value; }
    public float FollowDistance { get => _followDistance; set => _followDistance = value; }
    public float FollowSpeed { get => _followSpeed; set => _followSpeed = value; }
    public float TurnSpeed { get => _turnSpeed; set => _turnSpeed = value; }
    public Transform VisualRoot { get => _visualRoot; set => _visualRoot = value; }
    public ParticleSystem RescueVFX => _rescueVFX;
    public AudioClip RescueSfx { get => _rescueSfx; set => _rescueSfx = value; }

    private void Awake()
    {
        ResolveDependencies();
        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.RegisterChick(this);
        }
    }

    private void Start()
    {
        EnsureVFXSetup();
        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.RegisterChick(this);
        }
    }

    private void OnDestroy()
    {
        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.UnregisterChick(this);
        }
    }

    public void ResolveDependencies()
    {
        if (_visualRoot == null)
        {
            Transform v = transform.Find("VisualModel");
            if (v == null) v = transform.Find("Visual");
            if (v == null) v = transform.Find("Model");
            if (v == null && transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name != VFXRootName && child.GetComponentInChildren<Renderer>() != null)
                    {
                        v = child;
                        break;
                    }
                }
            }
            _visualRoot = v != null ? v : transform;
        }

        CacheBasePose();
    }

    private void CacheBasePose()
    {
        Transform target = _visualRoot != null ? _visualRoot : transform;
        if (_cachedPoseTarget != target)
        {
            _cachedPoseTarget = target;
            _baseVisualLocalPos = target.localPosition;
            _baseVisualLocalScale = target.localScale;
            _baseVisualLocalRot = target.localRotation;
            if (_baseVisualLocalScale.sqrMagnitude < 0.0001f)
            {
                _baseVisualLocalScale = Vector3.one;
            }
        }
    }

    public void ResetVisualPoseImmediate()
    {
        if (_cachedPoseTarget != null)
        {
            if (_cachedPoseTarget != transform)
            {
                _cachedPoseTarget.localPosition = _baseVisualLocalPos;
                _cachedPoseTarget.localRotation = _baseVisualLocalRot;
            }
            _cachedPoseTarget.localScale = _baseVisualLocalScale;
        }
        _rescueFeedbackTimer = 0f;
    }

    private void Update()
    {
        Step(Time.deltaTime);
    }

    /// <summary>
    /// Hem Update hem de Editör davranışsal testleri için deterministik adım fonksiyonu.
    /// </summary>
    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (_isRescued)
        {
            StepRescuedBehavior(deltaTime);
        }
        else
        {
            StepIdleWaitingBehavior(deltaTime);
        }

        StepProceduralFeedback(deltaTime);
    }

    private void StepIdleWaitingBehavior(float deltaTime)
    {
        _idleBobTimer += deltaTime * 4f;

        // Kurtarılmayı beklerken canlı görünmesi için hafif nefes alma / zıplama
        if (_visualRoot != null && _rescueFeedbackTimer <= 0f)
        {
            float bobY = Mathf.Abs(Mathf.Sin(_idleBobTimer)) * 0.06f;
            float squash = 1f + Mathf.Sin(_idleBobTimer) * 0.05f;
            if (_visualRoot != transform)
            {
                _visualRoot.localPosition = _baseVisualLocalPos + new Vector3(0f, bobY, 0f);
            }
            _visualRoot.localScale = new Vector3(
                _baseVisualLocalScale.x * (2f - squash),
                _baseVisualLocalScale.y * squash,
                _baseVisualLocalScale.z * (2f - squash)
            );
        }
    }

    private void StepRescuedBehavior(float deltaTime)
    {
        if (_followTarget == null) return;

        Vector3 toTarget = _followTarget.position - transform.position;
        // Y bileşenini düzleştirerek yer düzleminde takip sağla
        Vector3 planarOffset = new Vector3(toTarget.x, 0f, toTarget.z);
        float planarDistance = planarOffset.magnitude;

        if (planarDistance > _followDistance)
        {
            Vector3 moveDir = planarOffset.normalized;
            Vector3 moveDelta = moveDir * (_followSpeed * deltaTime);

            // Hedefi aşmamak için sınırla
            if (moveDelta.magnitude > (planarDistance - _followDistance))
            {
                moveDelta = moveDir * (planarDistance - _followDistance);
            }

            transform.position += moveDelta;

            // Hareket yönüne doğru yumuşak dönüş
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnSpeed * deltaTime);
            }

            // Sevimli paytak paytak yürüme (waddle) ve zıplama
            _waddleTimer += deltaTime * 12f;
            if (_visualRoot != null && _visualRoot != transform && _rescueFeedbackTimer <= 0f)
            {
                float hopY = Mathf.Abs(Mathf.Sin(_waddleTimer)) * 0.12f;
                float rollZ = Mathf.Sin(_waddleTimer) * 7f;
                _visualRoot.localPosition = _baseVisualLocalPos + new Vector3(0f, hopY, 0f);
                _visualRoot.localRotation = _baseVisualLocalRot * Quaternion.Euler(0f, 0f, rollZ);
            }
        }
        else
        {
            // Durduğunda orijinal poza yumuşak dönüş
            if (_visualRoot != null && _rescueFeedbackTimer <= 0f)
            {
                if (_visualRoot != transform)
                {
                    _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, _baseVisualLocalPos, 10f * deltaTime);
                    _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRot, 10f * deltaTime);
                }
                _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualLocalScale, 10f * deltaTime);
            }
        }
    }

    private void StepProceduralFeedback(float deltaTime)
    {
        if (_rescueFeedbackTimer > 0f)
        {
            _rescueFeedbackTimer -= deltaTime;
            if (_visualRoot != null)
            {
                // Kurtarılma sevinci: Zıplama + Squash & Stretch
                float progress = Mathf.Clamp01(_rescueFeedbackTimer / 0.4f);
                float jumpCurve = Mathf.Sin(progress * Mathf.PI);
                if (_visualRoot != transform)
                {
                    _visualRoot.localPosition = _baseVisualLocalPos + new Vector3(0f, jumpCurve * 0.35f, 0f);
                }
                _visualRoot.localScale = new Vector3(
                    _baseVisualLocalScale.x * (1f - jumpCurve * 0.2f),
                    _baseVisualLocalScale.y * (1f + jumpCurve * 0.35f),
                    _baseVisualLocalScale.z * (1f - jumpCurve * 0.2f)
                );
            }

            if (_rescueFeedbackTimer <= 0f)
            {
                ResetVisualPoseImmediate();
            }
        }
    }

    /// <summary>
    /// Civcivi kurtarır. İlerlemeyi artırır, VFX/SFX patlatır ve takip hedefini bağlar.
    /// </summary>
    public void Rescue(Transform targetOverride = null)
    {
        if (_isRescued) return;

        _isRescued = true;

        // Hedef ataması veya ChickRescueManager üzerinden zincir bağlama
        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.RescueChick(this);
        }
        
        if (_followTarget == null && targetOverride != null)
        {
            _followTarget = targetOverride;
        }

        // Dikey Dilim: Görsel & Ses Geri Bildirimi
        _rescueFeedbackTimer = 0.4f;

        if (_rescueVFX != null)
        {
            _rescueVFX.Play();
        }

        if (_rescueSfx != null)
        {
            SettingsUI.PlaySFX(_rescueSfx, _sfxVolume);
        }

        OnRescued?.Invoke(this);
    }

    // --- IInteractable Arayüzü Uygulaması ---
    public bool CanInteract(PlayerInteractor interactor)
    {
        return _rescueOnInteract && !_isRescued;
    }

    public string GetPromptText(PlayerInteractor interactor)
    {
        return _promptText;
    }

    public bool Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return false;

        Transform target = interactor != null ? interactor.transform : null;
        Rescue(target);
        return true;
    }

    // --- Fiziksel Temas ile Kurtarma ---
    private void OnTriggerEnter(Collider other)
    {
        if (!_rescueOnTriggerEnter || _isRescued) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            Rescue(other.transform);
        }
    }

    /// <summary>
    /// Dikey dilim standardına uygun altın sarısı pırıltı/yıldız partikül efektini hazırlar.
    /// </summary>
    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        ResolveDependencies();

        Transform vfxRoot = transform.Find(VFXRootName);
        if (vfxRoot == null)
        {
            var rootObj = new GameObject(VFXRootName);
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            vfxRoot = rootObj.transform;
            createdAny = true;
        }

        if (_rescueVFX == null)
        {
            Transform existing = vfxRoot.Find("RescueSparkleParticles");
            GameObject fxObj = existing != null ? existing.gameObject : new GameObject("RescueSparkleParticles");
            if (existing == null)
            {
                fxObj.transform.SetParent(vfxRoot, false);
                fxObj.transform.localPosition = Vector3.zero;
                _rescueVFX = fxObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            else
            {
                _rescueVFX = fxObj.GetComponent<ParticleSystem>() ?? fxObj.AddComponent<ParticleSystem>();
            }

            ConfigureSparkleParticles(_rescueVFX);
        }

#if UNITY_EDITOR
        if (_rescueSfx == null)
        {
            _rescueSfx = FindAudioClipByName("cozy_fx_1", "cozy_fx", "Click", "Coin", "PowerUp");
        }
#endif

        return createdAny;
    }

    private static void ConfigureSparkleParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = 0.45f;
        main.startSpeed = 2.5f;
        main.startSize = 0.22f;
        main.startColor = new Color(1f, 0.85f, 0.25f, 1f); // Sıcak altın sarısı
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null) renderer.sharedMaterial = new Material(s) { hideFlags = HideFlags.DontSave };
        }
    }

#if UNITY_EDITOR
    private static AudioClip FindAudioClipByName(params string[] searchTerms)
    {
        for (int i = 0; i < searchTerms.Length; i++)
        {
            string[] guids = AssetDatabase.FindAssets($"{searchTerms[i]} t:AudioClip");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) return clip;
            }
        }
        return null;
    }
#endif
}
