using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class PatrolEnemyAI : MonoBehaviour, IDamageable
{
    private const string VFXRootName = "EnemyVFX";

    public enum EnemyState
    {
        Patrol,
        Chase,
        Defeated
    }

    [Header("--- Stats & Configuration ---")]
    [Tooltip("Dusman istatistik veri nesnesi")]
    [SerializeField] private EnemyStatsSO _stats;

    [Tooltip("Devriye hedef noktalari (bos ise baslangic konumu etrafinda devriye gezer)")]
    [SerializeField] private Transform[] _waypoints;

    [Header("--- Components & Hierarchy ---")]
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private Collider _collider;
    [SerializeField] private Transform _visualRoot;

    [Header("--- VFX Components ---")]
    [SerializeField] private ParticleSystem _hitVFX;
    [SerializeField] private ParticleSystem _defeatedVFX;
    [SerializeField] private ParticleSystem _aggroVFX;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _hitSfx;
    [SerializeField] private AudioClip _defeatSfx;
    [SerializeField] private AudioClip _aggroSfx;
    [SerializeField] private AudioClip _attackSfx;
    [SerializeField] private AudioClip _bounceSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.85f;

    // Runtime variables
    private EnemyState _currentState = EnemyState.Patrol;
    private int _currentHealth = 30;
    private bool _isInvincible = false;
    private int _currentWaypointIndex = 0;
    private float _waypointWaitTimer = 0f;
    private float _attackCooldownTimer = 0f;
    private PlayerController _targetPlayer;
    private Vector3 _spawnPosition;
    private List<Vector3> _resolvedWaypoints = new List<Vector3>();

    // Procedural animation state
    private float _walkAnimTimer = 0f;
    private float _feedbackTimer = 0f;
    private Vector3 _baseVisualLocalPos = Vector3.zero;
    private Quaternion _baseVisualLocalRot = Quaternion.identity;
    private Vector3 _baseVisualLocalScale = Vector3.one;

    // Events
    public event Action<EnemyState> OnStateChanged;
    public event Action<int, int> OnHealthChanged;
    public event Action<int, Vector3> OnHit;
    public event Action<PatrolEnemyAI> OnDefeated;

    // IDamageable implementation
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _stats != null ? _stats.MaxHealth : 30;
    public bool IsAlive => _currentHealth > 0 && _currentState != EnemyState.Defeated;
    public bool IsInvincible { get => _isInvincible; set => _isInvincible = value; }

    // Public properties
    public EnemyState CurrentState => _currentState;
    public EnemyStatsSO Stats { get => _stats; set { _stats = value; ApplyStats(); } }
    public Transform[] Waypoints { get => _waypoints; set { _waypoints = value; RefreshWaypoints(); } }
    public PlayerController TargetPlayer { get => _targetPlayer; set => _targetPlayer = value; }
    public Transform VisualRoot => _visualRoot;
    public ParticleSystem HitVFX => _hitVFX;
    public ParticleSystem DefeatedVFX => _defeatedVFX;
    public ParticleSystem AggroVFX => _aggroVFX;
    public int CurrentWaypointIndex => _currentWaypointIndex;

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveDependencies();
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
        _spawnPosition = transform.position;
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
        ApplyStats();
        RefreshWaypoints();
    }

    private void Start()
    {
        if (_targetPlayer == null)
        {
            _targetPlayer = FindPlayerInScene();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        StepFeedback(dt);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        Step(dt);
    }

    public void ResolveDependencies()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_collider == null) _collider = GetComponent<Collider>();
        if (_visualRoot == null)
        {
            var visual = transform.Find("VisualModel");
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null)
            {
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.transform != transform) visual = anim.transform;
            }
            if (visual == null) visual = transform;
            _visualRoot = visual;
        }
    }

    private void CacheBasePose()
    {
        if (_visualRoot == null) return;
        _baseVisualLocalPos = _visualRoot.localPosition;
        _baseVisualLocalRot = _visualRoot.localRotation;
        _baseVisualLocalScale = _visualRoot.localScale;
        if (_baseVisualLocalScale.sqrMagnitude < 0.0001f)
        {
            _baseVisualLocalScale = Vector3.one;
        }
    }

    public void ApplyStats()
    {
        int maxHp = _stats != null ? _stats.MaxHealth : 30;
        _currentHealth = Mathf.Clamp(_currentHealth <= 0 ? maxHp : _currentHealth, 1, maxHp);
    }

    public void RefreshWaypoints()
    {
        _resolvedWaypoints.Clear();
        if (_waypoints != null && _waypoints.Length > 0)
        {
            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] != null)
                {
                    _resolvedWaypoints.Add(_waypoints[i].position);
                }
            }
        }

        if (_resolvedWaypoints.Count == 0)
        {
            Vector3 center = _spawnPosition.sqrMagnitude > 0.0001f ? _spawnPosition : transform.position;
            _resolvedWaypoints.Add(center + Vector3.forward * 3f);
            _resolvedWaypoints.Add(center - Vector3.forward * 3f);
        }

        _currentWaypointIndex = Mathf.Clamp(_currentWaypointIndex, 0, _resolvedWaypoints.Count - 1);

        if (_resolvedWaypoints.Count > 1)
        {
            Vector3 toCurrent = _resolvedWaypoints[_currentWaypointIndex] - transform.position;
            toCurrent.y = 0f;
            if (toCurrent.sqrMagnitude <= 0.36f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _resolvedWaypoints.Count;
            }
        }
    }

    public void SetState(EnemyState newState)
    {
        if (_currentState == newState || _currentState == EnemyState.Defeated) return;

        EnemyState oldState = _currentState;
        _currentState = newState;

        if (newState == EnemyState.Chase)
        {
            PlayAggroFeedback();
        }

        OnStateChanged?.Invoke(_currentState);
    }

    public void Step(float deltaTime)
    {
        if (_currentState == EnemyState.Defeated) return;

        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= deltaTime;
        }

        if (_waypointWaitTimer > 0f)
        {
            _waypointWaitTimer -= deltaTime;
        }

        // Oyuncu takip ve algılama
        if (_targetPlayer == null)
        {
            _targetPlayer = FindPlayerInScene();
        }

        float detectDist = _stats != null ? _stats.DetectionRange : 7f;
        float loseDist = _stats != null ? _stats.LoseAggroRange : 10f;

        if (_targetPlayer != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, _targetPlayer.transform.position);

            if (_currentState == EnemyState.Patrol)
            {
                if (distToPlayer <= detectDist)
                {
                    SetState(EnemyState.Chase);
                }
            }
            else if (_currentState == EnemyState.Chase)
            {
                if (distToPlayer > loseDist)
                {
                    SetState(EnemyState.Patrol);
                }
            }
        }

        // Hareket yonetimi
        if (_currentState == EnemyState.Patrol)
        {
            ExecutePatrolStep(deltaTime);
        }
        else if (_currentState == EnemyState.Chase)
        {
            ExecuteChaseStep(deltaTime);
        }
    }

    private void ExecutePatrolStep(float deltaTime)
    {
        if (_resolvedWaypoints.Count == 0) RefreshWaypoints();
        if (_resolvedWaypoints.Count == 0) return;

        Vector3 targetPos = _resolvedWaypoints[_currentWaypointIndex];
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.36f) // ~0.6m tolerans
        {
            if (_waypointWaitTimer <= 0f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _resolvedWaypoints.Count;
                _waypointWaitTimer = _stats != null ? _stats.WaypointWaitTime : 1f;
            }
            StopHorizontalMovement();
            return;
        }

        if (_waypointWaitTimer > 0f)
        {
            StopHorizontalMovement();
            return;
        }

        float speed = _stats != null ? _stats.PatrolSpeed : 3f;
        MoveTowards(targetPos, speed, deltaTime);
    }

    private void ExecuteChaseStep(float deltaTime)
    {
        if (_targetPlayer == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        Vector3 targetPos = _targetPlayer.transform.position;
        float speed = _stats != null ? _stats.ChaseSpeed : 5.5f;
        MoveTowards(targetPos, speed, deltaTime);
    }

    private void MoveTowards(Vector3 targetPos, float speed, float deltaTime)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Vector3 moveDir = dir.normalized;

        Quaternion targetRot = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * deltaTime);

        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = new Vector3(moveDir.x * speed, _rigidbody.linearVelocity.y, moveDir.z * speed);
        }
        else
        {
            transform.position += moveDir * speed * deltaTime;
        }

        _walkAnimTimer += deltaTime * speed;
    }

    private void StopHorizontalMovement()
    {
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
        }
    }

    public void StepFeedback(float deltaTime)
    {
        if (_visualRoot == null || _visualRoot == transform) return;

        if (_currentState == EnemyState.Defeated)
        {
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, new Vector3(1.3f, 0.1f, 1.3f), deltaTime * 12f);
            return;
        }

        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= deltaTime;
            return;
        }

        // Prosedurel yuruyus / nefes salinimi (squash & stretch & tilt)
        float speed = _currentState == EnemyState.Chase ? 12f : 6f;
        float wobble = Mathf.Sin(_walkAnimTimer * speed);
        float tilt = wobble * (_currentState == EnemyState.Chase ? 7f : 4f);
        float scaleY = 1f + wobble * 0.08f;
        float scaleXZ = 1f - wobble * 0.04f;

        Quaternion swayRot = Quaternion.Euler(0f, 0f, tilt);
        Vector3 swayScale = new Vector3(_baseVisualLocalScale.x * scaleXZ, _baseVisualLocalScale.y * scaleY, _baseVisualLocalScale.z * scaleXZ);

        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRot * swayRot, deltaTime * 14f);
        _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, swayScale, deltaTime * 14f);
    }

    public void ResetVisualPoseImmediate()
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        _visualRoot.localPosition = _baseVisualLocalPos;
        _visualRoot.localRotation = _baseVisualLocalRot;
        _visualRoot.localScale = _baseVisualLocalScale;
        _feedbackTimer = 0f;
    }

    // IDamageable Implementation
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    public void TakeDamage(int damage, Vector3 hitDirection)
    {
        if (_currentState == EnemyState.Defeated || _isInvincible || damage <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        OnHit?.Invoke(damage, hitDirection);

        TriggerHitFeedback(hitDirection);

        if (_currentHealth <= 0)
        {
            Defeat(hitDirection);
        }
    }

    public void Heal(int amount)
    {
        if (_currentState == EnemyState.Defeated || amount <= 0) return;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    private void TriggerHitFeedback(Vector3 hitDirection)
    {
        _feedbackTimer = 0.25f;

        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localScale = Vector3.Scale(_baseVisualLocalScale, new Vector3(1.3f, 0.7f, 1.3f));
            Vector3 tiltAxis = hitDirection.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, hitDirection.normalized) : Vector3.right;
            _visualRoot.localRotation = _baseVisualLocalRot * Quaternion.AngleAxis(18f, tiltAxis);
        }

        if (_hitVFX != null)
        {
            _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _hitVFX.Play(true);
        }

        SettingsUI.PlaySFX(_hitSfx, _sfxVolume);
    }

    public void Defeat(Vector3 hitDirection = default)
    {
        if (_currentState == EnemyState.Defeated) return;

        SetState(EnemyState.Defeated);
        _currentHealth = 0;

        StopHorizontalMovement();
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
        }

        if (_collider != null)
        {
            _collider.enabled = false;
        }

        if (_defeatedVFX != null)
        {
            _defeatedVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _defeatedVFX.Play(true);
        }

        SettingsUI.PlaySFX(_defeatSfx, _sfxVolume);
        OnDefeated?.Invoke(this);

        if (Application.isPlaying)
        {
            Destroy(gameObject, 1.2f);
        }
    }

    private void PlayAggroFeedback()
    {
        if (_aggroVFX != null)
        {
            _aggroVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _aggroVFX.Play(true);
        }

        SettingsUI.PlaySFX(_aggroSfx, _sfxVolume);

        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localScale = Vector3.Scale(_baseVisualLocalScale, new Vector3(0.9f, 1.25f, 0.9f));
        }
    }

    // Carpısma ve Oyuncu Etkilesimi
    public bool HandlePlayerContact(PlayerController player, bool isStompFromAbove)
    {
        if (_currentState == EnemyState.Defeated || player == null) return false;

        if (isStompFromAbove)
        {
            // Ustten ziplama: Dusman ezilir, oyuncu yukari ziplar
            int stompDmg = _stats != null ? _stats.StompDamage : 30;
            TakeDamage(stompDmg, Vector3.down);

            Rigidbody playerRb = player.GetRigidbody();
            if (playerRb == null) playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                float bounceForce = _stats != null ? _stats.BounceForce : 11f;
                Vector3 vel = playerRb.linearVelocity;
                playerRb.linearVelocity = new Vector3(vel.x, bounceForce, vel.z);
            }

            SettingsUI.PlaySFX(_bounceSfx != null ? _bounceSfx : _hitSfx, _sfxVolume);
            return true;
        }
        else
        {
            // Yandan temas: Dusman oyuncuya hasar verir
            if (_attackCooldownTimer <= 0f)
            {
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && health.IsAlive && !health.IsInvincible)
                {
                    int dmg = _stats != null ? _stats.ContactDamage : 20;
                    Vector3 pushDir = (player.transform.position - transform.position).normalized;
                    pushDir.y = 0.25f;
                    health.TakeDamage(dmg, pushDir.normalized);

                    _attackCooldownTimer = _stats != null ? _stats.AttackCooldown : 1.0f;
                    SettingsUI.PlaySFX(_attackSfx != null ? _attackSfx : _hitSfx, _sfxVolume);

                    if (_visualRoot != null && _visualRoot != transform)
                    {
                        _visualRoot.localScale = Vector3.Scale(_baseVisualLocalScale, new Vector3(1.15f, 1.15f, 1.15f));
                    }
                    return true;
                }
            }
        }
        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessCollision(collision.collider, collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessCollision(other, null);
    }

    private void ProcessCollision(Collider col, Collision collision)
    {
        if (_currentState == EnemyState.Defeated || col == null) return;

        PlayerController player = col.GetComponentInParent<PlayerController>();
        if (player == null) return;

        bool isStomp = false;

        if (collision != null && collision.contacts != null && collision.contacts.Length > 0)
        {
            for (int i = 0; i < collision.contacts.Length; i++)
            {
                if (collision.contacts[i].normal.y < -0.45f)
                {
                    isStomp = true;
                    break;
                }
            }
        }

        if (!isStomp)
        {
            float playerBottomY = player.transform.position.y;
            float enemyTopY = transform.position.y + 0.35f;
            if (playerBottomY > enemyTopY)
            {
                isStomp = true;
            }
        }

        HandlePlayerContact(player, isStomp);
    }

    private PlayerController FindPlayerInScene()
    {
        return FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    // VFX Setup Helper
    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        Transform vfxRoot = transform.Find(VFXRootName);
        if (vfxRoot == null)
        {
            var rootObj = new GameObject(VFXRootName);
            rootObj.transform.SetParent(transform, false);
            vfxRoot = rootObj.transform;
            createdAny = true;
        }

        Material sharedMat = CreateDefaultEffectMaterial();

        if (_hitVFX == null)
        {
            Transform existing = vfxRoot.Find("HitVFX");
            GameObject hitObj = existing != null ? existing.gameObject : new GameObject("HitVFX");
            if (existing == null)
            {
                hitObj.transform.SetParent(vfxRoot, false);
                hitObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                createdAny = true;
            }

            _hitVFX = hitObj.GetComponent<ParticleSystem>();
            if (_hitVFX == null)
            {
                _hitVFX = hitObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureParticleSystem(_hitVFX, sharedMat, new Color(1f, 0.3f, 0.1f), 15);
        }

        if (_defeatedVFX == null)
        {
            Transform existing = vfxRoot.Find("DefeatedVFX");
            GameObject defObj = existing != null ? existing.gameObject : new GameObject("DefeatedVFX");
            if (existing == null)
            {
                defObj.transform.SetParent(vfxRoot, false);
                defObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                createdAny = true;
            }

            _defeatedVFX = defObj.GetComponent<ParticleSystem>();
            if (_defeatedVFX == null)
            {
                _defeatedVFX = defObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureParticleSystem(_defeatedVFX, sharedMat, new Color(1f, 0.85f, 0.2f), 30);
        }

        if (_aggroVFX == null)
        {
            Transform existing = vfxRoot.Find("AggroVFX");
            GameObject aggroObj = existing != null ? existing.gameObject : new GameObject("AggroVFX");
            if (existing == null)
            {
                aggroObj.transform.SetParent(vfxRoot, false);
                aggroObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                createdAny = true;
            }

            _aggroVFX = aggroObj.GetComponent<ParticleSystem>();
            if (_aggroVFX == null)
            {
                _aggroVFX = aggroObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureParticleSystem(_aggroVFX, sharedMat, new Color(0.95f, 0.15f, 0.1f), 8);
        }

#if UNITY_EDITOR
        AutoFindDefaultAudioClips();
#endif
        return createdAny;
    }

    private static Material CreateDefaultEffectMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private static void ConfigureParticleSystem(ParticleSystem ps, Material mat, Color color, int burstCount)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 3.5f;
        main.startSize = 0.2f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        if (emission.burstCount == 0)
        {
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) });
        }

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }
    }

#if UNITY_EDITOR
    private void AutoFindDefaultAudioClips()
    {
        if (_hitSfx == null) _hitSfx = FindAudioClipByName("Hurt", "Punch", "Hit");
        if (_defeatSfx == null) _defeatSfx = FindAudioClipByName("Explosion", "Break", "Death");
        if (_aggroSfx == null) _aggroSfx = FindAudioClipByName("Alert", "Click", "Coin");
        if (_attackSfx == null) _attackSfx = FindAudioClipByName("Dash", "Swoosh", "Hit");
        if (_bounceSfx == null) _bounceSfx = FindAudioClipByName("Jump", "Land", "Bounce");
    }

    private AudioClip FindAudioClipByName(params string[] searchTerms)
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
