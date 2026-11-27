using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Basinc Plakasi ve Kapi/Hedef Tetikleyicisi (PressurePlateSwitch)
/// Uzerine cikildiginda (oyuncu veya agirlik/kutu) veya IInteractable ile tetiklendiginde
/// basinc plakasini asagi bastirir, bagli hedef transformu (kapi/bariyer/kopru) acar
/// ve OnActivated / OnDeactivated event'lerini calistirir.
/// Dikey Dilim: Prosedurel hareket, altin-kehribar renk parlama gecisi, ParticleSystem ve SettingsUI.PlaySFX.
/// </summary>
public class PressurePlateSwitch : MonoBehaviour, IInteractable
{
    private const string VFXRootName = "PlateVFX";

    [Header("--- Target & Movement Settings ---")]
    [Tooltip("Plaka aktiflesince hareket ettirilecek hedef nesne (Orn: Kapi / Bariyer)")]
    [SerializeField] private Transform _targetTransform;

    [Tooltip("Hedef nesnenin aktif durumdaki goreceli ofseti (Orn: yukari 3.5 metre)")]
    [SerializeField] private Vector3 _targetActiveOffset = new Vector3(0f, 3.5f, 0f);

    [Tooltip("Hedef nesnenin hareket hizi")]
    [SerializeField] private float _moveSpeed = 3.5f;

    [Tooltip("Plakadan inildiginde acik kalmaya devam etsin mi (tek seferlik kalici bulmaca)")]
    [SerializeField] private bool _stayPressedPermanently = false;

    [Tooltip("E tusu ile dogrudan etkilesime izin verilsin mi")]
    [SerializeField] private bool _allowManualInteraction = true;

    [Header("--- Plate Visual & Procedural Motion ---")]
    [Tooltip("Plakanin gorsel modeli (Asagi cokme hareketi icin)")]
    [SerializeField] private Transform _plateVisual;

    [Tooltip("Plaka basildiginda asagi inme derinligi")]
    [SerializeField] private float _depressionDepth = 0.12f;

    [Tooltip("Plakanin eski konumuna ve derinlige lerp hizi")]
    [SerializeField] private float _plateLerpSpeed = 10f;

    [Tooltip("Plaka uzerindeki MeshRenderer (Renk degisimi icin)")]
    [SerializeField] private Renderer _plateRenderer;

    [Tooltip("Aktifken parlayacak altin-sari renk")]
    [SerializeField] private Color _activeColor = new Color(1f, 0.835f, 0.310f, 1f);

    [Tooltip("Pasifken gorunecek koyu kehribar renk")]
    [SerializeField] private Color _inactiveColor = new Color(0.427f, 0.231f, 0f, 1f);

    [Header("--- Audio & Visual Effects ---")]
    [Tooltip("Aktivasyon aninda patlayan partikul efekti")]
    [SerializeField] private ParticleSystem _plateVFX;

    [Tooltip("Plakaya basildiginda calacak ses efekti")]
    [SerializeField] private AudioClip _activateSfx;

    [Tooltip("Plakadan inildiginde calacak ses efekti")]
    [SerializeField] private AudioClip _deactivateSfx;

    [Tooltip("Ses efektleri seviyesi")]
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.85f;

    [Header("--- Activation Filters ---")]
    [Tooltip("Basinc olusturabilecek minimum Rigidbody kutlesi")]
    [SerializeField] private float _minActivationMass = 0.5f;

    // Runtime State
    private bool _isPressed = false;
    private int _activationCount = 0;
    private int _deactivationCount = 0;
    private Vector3 _targetInitialPos;
    private Vector3 _plateVisualInitialLocalPos;
    private Transform _cachedTargetTransform;
    private Transform _cachedPlateVisual;
    private readonly HashSet<Collider> _activePressers = new HashSet<Collider>();
    private MaterialPropertyBlock _propBlock;

    // Events
    public event Action<PressurePlateSwitch> OnActivated;
    public event Action<PressurePlateSwitch> OnDeactivated;
    public event Action<bool> OnStateChanged;

    // Public Properties
    public bool IsPressed => _isPressed;
    public Transform TargetTransform
    {
        get => _targetTransform;
        set
        {
            _targetTransform = value;
            _cachedTargetTransform = value;
            if (_targetTransform != null) _targetInitialPos = _targetTransform.position;
        }
    }
    public Vector3 TargetActiveOffset { get => _targetActiveOffset; set => _targetActiveOffset = value; }
    public Vector3 TargetInitialPos => _targetInitialPos;
    public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
    public bool StayPressedPermanently { get => _stayPressedPermanently; set => _stayPressedPermanently = value; }
    public bool AllowManualInteraction { get => _allowManualInteraction; set => _allowManualInteraction = value; }
    public Transform PlateVisual { get => _plateVisual; set => _plateVisual = value; }
    public ParticleSystem PlateVFX => _plateVFX;
    public int ActivationCount => _activationCount;
    public int DeactivationCount => _deactivationCount;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        EnsureVFXSetup();
        UpdateVisualsImmediate();
    }

    private void Update()
    {
        StepTargetMotion(Time.deltaTime);
        StepPlateVisual(Time.deltaTime);
    }

    public void ResolveDependencies()
    {
        if (_targetTransform != null && _cachedTargetTransform != _targetTransform)
        {
            _cachedTargetTransform = _targetTransform;
            _targetInitialPos = _targetTransform.position;
        }

        if (_plateVisual == null)
        {
            Transform pv = transform.Find("PlateVisual");
            if (pv == null) pv = transform.Find("Visual");
            _plateVisual = pv != null ? pv : transform;
        }

        if (_plateVisual != null)
        {
            if (_cachedPlateVisual != _plateVisual)
            {
                _cachedPlateVisual = _plateVisual;
                _plateVisualInitialLocalPos = _plateVisual.localPosition;
            }
            if (_plateRenderer == null)
            {
                _plateRenderer = _plateVisual.GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            }
        }
    }

    // --- IInteractable Implementation ---
    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!_allowManualInteraction) return false;
        if (_stayPressedPermanently && _isPressed) return false;
        return true;
    }

    public string GetPromptText(PlayerInteractor interactor)
    {
        return _isPressed ? "[E] Basınç Plakasını Bırak" : "[E] Basınç Plakasına Bas";
    }

    public bool Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return false;
        SetPressedState(!_isPressed, true);
        return true;
    }

    // --- State Management ---
    public void SetPressedState(bool pressed, bool triggerFeedback = true)
    {
        if (_isPressed == pressed) return;

        if (_stayPressedPermanently && _isPressed && !pressed)
        {
            return;
        }

        _isPressed = pressed;

        if (_isPressed)
        {
            _activationCount++;
            if (triggerFeedback)
            {
                PlayActivationFeedback();
            }
            OnActivated?.Invoke(this);
            OnStateChanged?.Invoke(true);
        }
        else
        {
            _deactivationCount++;
            if (triggerFeedback)
            {
                PlayDeactivationFeedback();
            }
            OnDeactivated?.Invoke(this);
            OnStateChanged?.Invoke(false);
        }

        ApplyMaterialColor();
    }

    public void TriggerManualPress()
    {
        SetPressedState(true, true);
    }

    public void TriggerManualRelease()
    {
        SetPressedState(false, true);
    }

    // --- Simulation & Stepping (for tests & game loop) ---
    public void Step(float deltaTime)
    {
        ResolveDependencies();
        StepTargetMotion(deltaTime);
        StepPlateVisual(deltaTime);
    }

    private void StepTargetMotion(float deltaTime)
    {
        if (_targetTransform == null) return;

        Vector3 desiredPos = _isPressed ? (_targetInitialPos + _targetActiveOffset) : _targetInitialPos;
        _targetTransform.position = Vector3.MoveTowards(_targetTransform.position, desiredPos, _moveSpeed * deltaTime);
    }

    private void StepPlateVisual(float deltaTime)
    {
        if (_plateVisual == null || _plateVisual == transform) return;

        Vector3 targetLocalPos = _plateVisualInitialLocalPos;
        if (_isPressed)
        {
            targetLocalPos.y -= _depressionDepth;
        }

        _plateVisual.localPosition = Vector3.Lerp(_plateVisual.localPosition, targetLocalPos, Mathf.Clamp01(deltaTime * _plateLerpSpeed));
    }

    private void UpdateVisualsImmediate()
    {
        if (_plateVisual != null && _plateVisual != transform)
        {
            Vector3 targetLocalPos = _plateVisualInitialLocalPos;
            if (_isPressed) targetLocalPos.y -= _depressionDepth;
            _plateVisual.localPosition = targetLocalPos;
        }
        ApplyMaterialColor();
    }

    private void ApplyMaterialColor()
    {
        if (_plateRenderer == null) return;

        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        _plateRenderer.GetPropertyBlock(_propBlock);
        Color c = _isPressed ? _activeColor : _inactiveColor;
        _propBlock.SetColor("_BaseColor", c);
        _propBlock.SetColor("_Color", c);
        _plateRenderer.SetPropertyBlock(_propBlock);
    }

    private void PlayActivationFeedback()
    {
        if (_plateVFX != null)
        {
            _plateVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _plateVFX.Play(true);
        }
        SettingsUI.PlaySFX(_activateSfx, _sfxVolume);
    }

    private void PlayDeactivationFeedback()
    {
        SettingsUI.PlaySFX(_deactivateSfx, _sfxVolume * 0.75f);
    }

    // --- Trigger Collisions (Physics Stepping) ---
    private void OnTriggerEnter(Collider other)
    {
        if (IsValidPresser(other))
        {
            _activePressers.Add(other);
            if (!_isPressed)
            {
                SetPressedState(true, true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && _activePressers.Remove(other))
        {
            _activePressers.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
            if (_activePressers.Count == 0 && _isPressed && !_stayPressedPermanently)
            {
                SetPressedState(false, true);
            }
        }
    }

    public void RegisterPresser(Collider presser)
    {
        if (presser == null) return;
        _activePressers.Add(presser);
        if (!_isPressed)
        {
            SetPressedState(true, true);
        }
    }

    public void UnregisterPresser(Collider presser)
    {
        if (presser == null) return;
        _activePressers.Remove(presser);
        if (_activePressers.Count == 0 && _isPressed && !_stayPressedPermanently)
        {
            SetPressedState(false, true);
        }
    }

    private bool IsValidPresser(Collider col)
    {
        if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) return false;

        if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null)
        {
            return true;
        }

        Rigidbody rb = col.attachedRigidbody;
        if (rb != null && rb.mass >= _minActivationMass)
        {
            return true;
        }

        return false;
    }

    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        ResolveDependencies();

        Transform vfxRoot = transform.Find(VFXRootName);
        if (vfxRoot == null)
        {
            var rootObj = new GameObject(VFXRootName);
            rootObj.transform.SetParent(transform, false);
            vfxRoot = rootObj.transform;
            createdAny = true;
        }

        if (_plateVFX == null)
        {
            Transform existing = vfxRoot.Find("PlateBurstVFX");
            GameObject fxObj = existing != null ? existing.gameObject : new GameObject("PlateBurstVFX");
            if (existing == null)
            {
                fxObj.transform.SetParent(vfxRoot, false);
                fxObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                _plateVFX = fxObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            else
            {
                _plateVFX = fxObj.GetComponent<ParticleSystem>() ?? fxObj.AddComponent<ParticleSystem>();
            }

            ConfigurePlateParticles(_plateVFX);
        }

#if UNITY_EDITOR
        if (_activateSfx == null)
        {
            _activateSfx = FindAudioClipByName("cozy_fx_1", "cozy_fx", "Click", "Land", "Bounce");
        }
        if (_deactivateSfx == null)
        {
            _deactivateSfx = FindAudioClipByName("cozy_fx_2", "cozy_fx", "Click", "Land");
        }
#endif

        return createdAny;
    }

    private static void ConfigurePlateParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 2.5f;
        main.startSize = 0.2f;
        main.startColor = new Color(1f, 0.835f, 0.310f, 1f); // Warm gold
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.7f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null) renderer.sharedMaterial = new Material(s);
        }
    }

#if UNITY_EDITOR
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
