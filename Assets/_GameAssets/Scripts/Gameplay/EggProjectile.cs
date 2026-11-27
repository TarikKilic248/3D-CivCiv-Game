using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Zipzip Yumurta Mermisi (EggProjectile)
/// Civciv tarafindan yay cizerek firlatilir; havada doner ve uzar (squash-stretch),
/// IDamageable hedefe veya zemine carptiginda hasar, geri tepme, kabuk kirilma VFX ve SFX tetikler.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class EggProjectile : MonoBehaviour
{
    private const string VFXRootName = "EggVFX";

    [Header("--- Damage & Physics ---")]
    [Tooltip("Carpisma aninda IDamageable hedefe verilecek hasar")]
    [SerializeField] private int _damage = 25;

    [Tooltip("Carpisma aninda Rigidbody hedeflere uygulanacak itme kuvveti")]
    [SerializeField] private float _knockbackForce = 6.0f;

    [Tooltip("Yumurtanin patlamadan once maksimum havada kalma suresi (saniye)")]
    [SerializeField] private float _maxLifetime = 6.0f;

    [Tooltip("Hasar verilecek hedeflerin katmanlari")]
    [SerializeField] private LayerMask _targetLayers = ~0;

    [Header("--- Visual & Procedural Motion ---")]
    [SerializeField] private Transform _visualRoot;
    [Tooltip("Ucus sirasinda takla atma/donus hizi (derece/sn)")]
    [SerializeField] private float _spinSpeed = 420f;
    [Tooltip("Ucus sirasinda yonlu esneme olcegi")]
    [SerializeField] private Vector3 _flightStretchScale = new Vector3(0.85f, 0.85f, 1.25f);
    [SerializeField] private float _poseLerpSpeed = 14f;

    [Header("--- VFX & Trail ---")]
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private ParticleSystem _breakVFX;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _launchSfx;
    [SerializeField] private AudioClip _breakSfx;
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.85f;

    // Component references
    private Rigidbody _rigidbody;
    private SphereCollider _collider;
    private Collider _shooterCollider;

    // Runtime state
    private bool _hasHit = false;
    private float _lifeTimer = 0f;
    private int _sfxPlayCount = 0;
    private Vector3 _baseVisualScale = Vector3.one;
    private Quaternion _baseVisualRotation = Quaternion.identity;

    // Events
    public event Action<EggProjectile, Collider, IDamageable> OnEggHit;
    public event Action<EggProjectile> OnEggDestroyed;

    // Properties
    public int Damage { get => _damage; set => _damage = value; }
    public float KnockbackForce { get => _knockbackForce; set => _knockbackForce = value; }
    public float MaxLifetime { get => _maxLifetime; set => _maxLifetime = value; }
    public bool HasHit => _hasHit;
    public Rigidbody Rigidbody => _rigidbody;
    public SphereCollider Collider => _collider;
    public TrailRenderer Trail => _trailRenderer;
    public ParticleSystem BreakVFX => _breakVFX;
    public Transform VisualRoot => _visualRoot;
    public int SfxPlayCount => _sfxPlayCount;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_collider == null) _collider = GetComponent<SphereCollider>();
        AutoFindDefaultAudioClips();
    }

    private void Reset()
    {
        ResolveDependencies();
        EnsureVFXSetup();
        AutoFindDefaultAudioClips();
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
    }

    public void ResolveDependencies()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_collider == null) _collider = GetComponent<SphereCollider>();

        if (_rigidbody != null)
        {
            _rigidbody.mass = 0.4f;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (_collider != null)
        {
            _collider.radius = 0.22f;
        }

        if (_visualRoot == null)
        {
            var visual = transform.Find("EggVisual");
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null && transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child.name != VFXRootName && child.GetComponent<Renderer>() != null)
                    {
                        visual = child;
                        break;
                    }
                }
            }
            if (visual == null) visual = transform;
            _visualRoot = visual;
        }
    }

    private void CacheBasePose()
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        _baseVisualRotation = _visualRoot.localRotation;
        _baseVisualScale = _visualRoot.localScale;
        if (_baseVisualScale.sqrMagnitude < 0.0001f)
        {
            _baseVisualScale = new Vector3(0.35f, 0.48f, 0.35f);
        }
    }

    private void Update()
    {
        Step(Time.deltaTime > 0f ? Time.deltaTime : 0.02f);
    }

    public void Step(float deltaTime)
    {
        if (_hasHit) return;

        _lifeTimer += deltaTime;
        if (_lifeTimer >= _maxLifetime)
        {
            Explode(transform.position, Vector3.up, null);
            return;
        }

        StepFeedback(deltaTime);
    }

    public void StepFeedback(float deltaTime)
    {
        if (_visualRoot == null || _visualRoot == transform) return;

        // Prosedurel takla / donus
        _visualRoot.Rotate(Vector3.right, _spinSpeed * deltaTime, Space.Self);

        // Ucus yonunde hafif uzama (squash-stretch)
        Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.forward;
        if (vel.sqrMagnitude > 0.1f)
        {
            Vector3 targetScale = Vector3.Scale(_baseVisualScale, _flightStretchScale);
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, targetScale, deltaTime * _poseLerpSpeed);
        }
        else
        {
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualScale, deltaTime * _poseLerpSpeed);
        }
    }

    /// <summary>
    /// Yumurtayi belirlenen baslangic hiziyla firlatir.
    /// </summary>
    public void Launch(Vector3 initialVelocity, Collider shooterCollider = null, int? overrideDamage = null)
    {
        ResolveDependencies();
        if (overrideDamage.HasValue)
        {
            _damage = overrideDamage.Value;
        }

        _shooterCollider = shooterCollider;
        if (_collider != null && shooterCollider != null)
        {
            Physics.IgnoreCollision(_collider, shooterCollider, true);
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = initialVelocity;
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.emitting = true;
        }

        if (_launchSfx != null)
        {
            SettingsUI.PlaySFX(_launchSfx, _sfxVolume * 0.75f);
            _sfxPlayCount++;
        }
    }

    /// <summary>
    /// Hedefe dogrudan isabet kontrolu (Editor testleri ve carpmalar icin)
    /// </summary>
    public bool HitTarget(Collider targetCollider, Vector3 hitPoint = default, Vector3 hitNormal = default)
    {
        if (_hasHit) return false;
        if (targetCollider != null && targetCollider == _shooterCollider) return false;

        Vector3 point = hitPoint != default ? hitPoint : (targetCollider != null ? targetCollider.bounds.center : transform.position);
        Vector3 normal = hitNormal != default ? hitNormal : -transform.forward;

        Explode(point, normal, targetCollider);
        return true;
    }

    public bool HitTarget(GameObject targetObj)
    {
        if (targetObj == null) return false;
        Collider col = targetObj.GetComponentInChildren<Collider>();
        return HitTarget(col, col != null ? col.bounds.center : targetObj.transform.position, Vector3.up);
    }

    private void Explode(Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (_hasHit) return;
        _hasHit = true;

        Vector3 hitDirection = Vector3.forward;
        if (_rigidbody != null && _rigidbody.linearVelocity.sqrMagnitude > 0.05f)
        {
            hitDirection = _rigidbody.linearVelocity.normalized;
        }
        else if (hitNormal.sqrMagnitude > 0.01f)
        {
            hitDirection = -hitNormal.normalized;
        }

        IDamageable damageable = null;
        if (hitCollider != null)
        {
            damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive && !damageable.IsInvincible)
            {
                damageable.TakeDamage(_damage, hitDirection);
            }

            // Rigidbody itme / geri tepme
            Rigidbody targetRb = hitCollider.attachedRigidbody ?? hitCollider.GetComponentInParent<Rigidbody>();
            if (targetRb != null && !targetRb.isKinematic)
            {
                targetRb.AddForce(hitDirection * _knockbackForce, ForceMode.Impulse);
                targetRb.linearVelocity += hitDirection * (_knockbackForce * 0.5f);
            }
        }

        // VFX & SFX Tetikleme
        TriggerBreakEffects(hitPoint, hitNormal);

        // Olay bildirimleri
        OnEggHit?.Invoke(this, hitCollider, damageable);
        OnEggDestroyed?.Invoke(this);

        // Kendini temizle (VFX ayrildiktan sonra objeyi yok et)
        if (Application.isPlaying)
        {
            Destroy(gameObject, 0.04f);
        }
        else
        {
            // Editorde test sirasinda visual/collider kapat
            if (_collider != null) _collider.enabled = false;
            if (_visualRoot != null && _visualRoot != transform) _visualRoot.gameObject.SetActive(false);
        }
    }

    private void TriggerBreakEffects(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_breakVFX != null)
        {
            _breakVFX.transform.SetParent(null, true);
            _breakVFX.transform.position = hitPoint;
            if (hitNormal.sqrMagnitude > 0.01f)
            {
                _breakVFX.transform.rotation = Quaternion.LookRotation(hitNormal);
            }
            _breakVFX.gameObject.SetActive(true);
            _breakVFX.Play(true);

            if (Application.isPlaying)
            {
                Destroy(_breakVFX.gameObject, 1.5f);
            }
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.transform.SetParent(null, true);
            _trailRenderer.emitting = false;
            if (Application.isPlaying)
            {
                Destroy(_trailRenderer.gameObject, 0.6f);
            }
        }

        SettingsUI.PlaySFX(_breakSfx, _sfxVolume);
        _sfxPlayCount++;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        if (_shooterCollider != null && collision.collider == _shooterCollider) return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up;
        HitTarget(collision.collider, point, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (_shooterCollider != null && other == _shooterCollider) return;

        // Eger trigger olan nesne IDamageable ise isabet say
        IDamageable d = other.GetComponentInParent<IDamageable>();
        if (d != null && d.IsAlive)
        {
            HitTarget(other, other.bounds.center, -transform.forward);
        }
    }

    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        ResolveDependencies();

        // 1. Egg Visual Mesh (Yumurta formu)
        if (_visualRoot == null || _visualRoot == transform)
        {
            Transform existingVisual = transform.Find("EggVisual");
            GameObject visualObj = existingVisual != null ? existingVisual.gameObject : null;
            if (visualObj == null)
            {
                visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualObj.name = "EggVisual";
                visualObj.transform.SetParent(transform, false);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localScale = new Vector3(0.35f, 0.48f, 0.35f);

                // Default collider kaldir
                Collider sphereCol = visualObj.GetComponent<Collider>();
                if (sphereCol != null)
                {
                    if (Application.isPlaying) Destroy(sphereCol);
                    else DestroyImmediate(sphereCol);
                }

                // Yumurta sarisi/kabugu materyali
                Renderer rend = visualObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = CreateEffectMaterial("EggShell_Mat", new Color(0.98f, 0.94f, 0.82f, 1f));
                    if (mat != null) rend.sharedMaterial = mat;
                }
                createdAny = true;
            }
            _visualRoot = visualObj.transform;
        }

        // 2. TrailRenderer (Ucus süzülmesi ve yay izi)
        if (_trailRenderer == null)
        {
            Transform existingTrail = transform.Find("EggTrail");
            GameObject trailObj = existingTrail != null ? existingTrail.gameObject : new GameObject("EggTrail");
            if (existingTrail == null)
            {
                trailObj.transform.SetParent(transform, false);
                trailObj.transform.localPosition = Vector3.zero;
                createdAny = true;
            }

            _trailRenderer = trailObj.GetComponent<TrailRenderer>();
            if (_trailRenderer == null)
            {
                _trailRenderer = trailObj.AddComponent<TrailRenderer>();
                createdAny = true;
            }

            ConfigureTrail(_trailRenderer);
        }

        // 3. Break VFX (Kabuk kirilma ve sari patlama partikulleri)
        if (_breakVFX == null)
        {
            Transform vfxRoot = transform.Find(VFXRootName);
            if (vfxRoot == null)
            {
                GameObject rootObj = new GameObject(VFXRootName);
                rootObj.transform.SetParent(transform, false);
                vfxRoot = rootObj.transform;
                createdAny = true;
            }

            Transform existingPs = vfxRoot.Find("ShellBreakVFX");
            GameObject psObj = existingPs != null ? existingPs.gameObject : new GameObject("ShellBreakVFX");
            if (existingPs == null)
            {
                psObj.transform.SetParent(vfxRoot, false);
                psObj.transform.localPosition = Vector3.zero;
                createdAny = true;
            }

            _breakVFX = psObj.GetComponent<ParticleSystem>();
            if (_breakVFX == null)
            {
                _breakVFX = psObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }

            ConfigureBreakParticles(_breakVFX);
        }

#if UNITY_EDITOR
        AutoFindDefaultAudioClips();
#endif

        return createdAny;
    }

    private static Material CreateEffectMaterial(string matName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material mat = new Material(shader) { name = matName };
            mat.color = color;
            return mat;
        }
        return null;
    }

    private static void ConfigureTrail(TrailRenderer trail)
    {
        trail.time = 0.35f;
        trail.startWidth = 0.22f;
        trail.endWidth = 0.02f;
        trail.autodestruct = false;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.65f, 0.15f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        Material trailMat = CreateEffectMaterial("EggTrail_Mat", new Color(1f, 0.9f, 0.3f, 0.8f));
        if (trailMat != null) trail.sharedMaterial = trailMat;
    }

    private static void ConfigureBreakParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.4f;
        main.startSpeed = 4.8f;
        main.startSize = 0.14f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.98f, 0.95f, 0.85f, 1f), // Beyaz kabuk
            new Color(1f, 0.62f, 0.12f, 1f)     // Kehribar sarisi
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Material mat = CreateEffectMaterial("EggShellShards_Mat", Color.white);
            if (mat != null) renderer.sharedMaterial = mat;
        }
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_launchSfx != null && _breakSfx != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        if (guids == null || guids.Length == 0) return;
        AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (fallback == null) return;

        if (_launchSfx == null) _launchSfx = fallback;
        if (_breakSfx == null) _breakSfx = fallback;
#endif
    }
}
