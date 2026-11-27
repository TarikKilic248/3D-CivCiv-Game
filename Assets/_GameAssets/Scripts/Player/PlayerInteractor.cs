using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Oyuncu Etkilesim Yoneticisi (PlayerInteractor)
/// Oyuncunun cevresindeki IInteractable bilesenlerini tespit eder, en yakin olanini secer
/// ve E tusu (veya eski/yeni giris sistemi) ile etkilesimi tetikler.
/// Dikey dilim standardina uygun gorsel hareket, ParticleSystem ve SFX geri bildirimine sahiptir.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    private const string VFXRootName = "InteractionVFX";

    [Header("--- Interaction Settings ---")]
    [Tooltip("Etkilesim algilama yaricapi (metre)")]
    [SerializeField] private float _interactionRadius = 3.0f;

    [Tooltip("Etkilesim icin taranacak katmanlar")]
    [SerializeField] private LayerMask _interactionLayers = ~0;

    [Tooltip("Etkilesim tusu (Varsayilan: E)")]
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    [Tooltip("Etkilesim arama merkez noktasi (Bos ise oyuncu transformu kullanilir)")]
    [SerializeField] private Transform _interactionOrigin;

    [Header("--- Visual & Procedural Feedback ---")]
    [Tooltip("Gorsel squash/bounce hareketi uygulanacak model koku")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("Etkilesim aninda olusacak kivilcim / halka efekti")]
    [SerializeField] private ParticleSystem _interactionVFX;

    [Tooltip("Etkilesim aninda calacak ses efekti")]
    [SerializeField] private AudioClip _interactSfx;

    [Tooltip("Ses efekti seviyesi")]
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.85f;

    [Tooltip("Etkilesim anindaki procedural squash olcegi")]
    [SerializeField] private Vector3 _interactSquashScale = new Vector3(1.15f, 0.88f, 1.15f);

    [Tooltip("Geri bildirim suresi (saniye)")]
    [SerializeField] private float _feedbackDuration = 0.2f;

    [Tooltip("Varsayilan poza donus hizi")]
    [SerializeField] private float _poseLerpSpeed = 12f;

    [Header("--- UI Prompt (Optional) ---")]
    [Tooltip("Etkilesim ipucunu gosterecek TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI _promptText;

    [Tooltip("Ipucu paneli / kapsayici nesne")]
    [SerializeField] private GameObject _promptPanel;

    [Tooltip("Arayuz temasi")]
    [SerializeField] private UIThemeSO _theme;

    // Runtime State
    private IInteractable _currentInteractable;
    private int _interactionCount;
    private float _feedbackTimer;
    private Vector3 _baseVisualLocalScale = Vector3.one;
    private Quaternion _baseVisualLocalRot = Quaternion.identity;
    private Transform _cachedPoseTarget;
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // Events
    public event Action<IInteractable> OnInteracted;
    public event Action<IInteractable> OnFocusChanged;

    // Properties
    public IInteractable CurrentInteractable => _currentInteractable;
    public float InteractionRadius { get => _interactionRadius; set => _interactionRadius = value; }
    public LayerMask InteractionLayers { get => _interactionLayers; set => _interactionLayers = value; }
    public KeyCode InteractionKey { get => _interactionKey; set => _interactionKey = value; }
    public int InteractionCount => _interactionCount;
    public ParticleSystem InteractionVFX => _interactionVFX;
    public Transform VisualRoot { get => _visualRoot; set => _visualRoot = value; }
    public Transform InteractionOrigin { get => _interactionOrigin; set => _interactionOrigin = value; }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        EnsureVFXSetup();
        UpdatePromptDisplay();
    }

    private void Update()
    {
        // 1. En yakin etkilesilebilir nesneyi tespit et
        IInteractable nearest = FindNearestInteractable();
        if (!ReferenceEquals(_currentInteractable, nearest))
        {
            _currentInteractable = nearest;
            OnFocusChanged?.Invoke(_currentInteractable);
            UpdatePromptDisplay();
        }

        // 2. Girdi kontrolu
        bool interactPressed = CheckInteractionInput();
        if (interactPressed)
        {
            TryInteract();
        }

        // 3. Procedural hareket ve zamanlayicilar
        StepFeedback(Time.deltaTime);
    }

    private void OnDisable()
    {
        ResetVisualPoseImmediate();
        if (_promptPanel != null) _promptPanel.SetActive(false);
    }

    public void ResolveDependencies()
    {
        if (_interactionOrigin == null)
        {
            _interactionOrigin = transform;
        }

        if (_visualRoot == null)
        {
            Transform v = transform.Find("VisualModel");
            if (v == null) v = transform.Find("Visual");
            if (v == null) v = transform.Find("Model");
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
            _baseVisualLocalScale = target.localScale;
            _baseVisualLocalRot = target.localRotation;
        }
    }

    public void ResetVisualPoseImmediate()
    {
        if (_cachedPoseTarget != null)
        {
            _cachedPoseTarget.localScale = _baseVisualLocalScale;
            _cachedPoseTarget.localRotation = _baseVisualLocalRot;
        }
        _feedbackTimer = 0f;
    }

    /// <summary>
    /// Yaricap icindeki en yakin gecerli IInteractable nesnesini dondurur.
    /// </summary>
    public IInteractable FindNearestInteractable()
    {
        Vector3 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
        IInteractable closest = null;
        float minDistanceSqr = _interactionRadius * _interactionRadius;

        // 1. Fiziksel Overlap kontrolu
        int hitCount = Physics.OverlapSphereNonAlloc(origin, _interactionRadius, _overlapBuffer, _interactionLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = col.GetComponent<IInteractable>();
            }

            if (interactable != null && interactable.CanInteract(this))
            {
                float distSqr = (interactable.transform.position - origin).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closest = interactable;
                }
            }
        }

        // 2. Sahnedeki nesneler icin guvenli yedek arama (collider henuz physics step almamis testler icin)
        if (closest == null)
        {
            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                MonoBehaviour mb = allBehaviours[i];
                if (mb == null || !mb.enabled || !mb.gameObject.activeInHierarchy || mb.transform == transform || mb.transform.IsChildOf(transform)) continue;

                if (mb is IInteractable interactable && interactable.CanInteract(this))
                {
                    float distSqr = (interactable.transform.position - origin).sqrMagnitude;
                    if (distSqr <= minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        closest = interactable;
                    }
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Odaktaki veya yakinindaki etkilesimli nesneyi tetiklemeyi dener.
    /// </summary>
    public bool TryInteract()
    {
        IInteractable target = _currentInteractable ?? FindNearestInteractable();
        if (target == null || !target.CanInteract(this))
        {
            return false;
        }

        bool success = target.Interact(this);
        if (success)
        {
            _interactionCount++;
            TriggerFeedback();
            OnInteracted?.Invoke(target);
            UpdatePromptDisplay();
        }

        return success;
    }

    /// <summary>
    /// Test ve simülasyonlar için deterministik tick metodu.
    /// </summary>
    public void Step(float deltaTime)
    {
        ResolveDependencies();
        IInteractable nearest = FindNearestInteractable();
        if (!ReferenceEquals(_currentInteractable, nearest))
        {
            _currentInteractable = nearest;
            OnFocusChanged?.Invoke(_currentInteractable);
        }
        StepFeedback(deltaTime);
        UpdatePromptDisplay();
    }

    public void TriggerFeedback()
    {
        ResolveDependencies();
        _feedbackTimer = _feedbackDuration;

        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localScale = Vector3.Scale(_baseVisualLocalScale, _interactSquashScale);
        }

        if (_interactionVFX != null)
        {
            _interactionVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _interactionVFX.Play(true);
        }

        SettingsUI.PlaySFX(_interactSfx, _sfxVolume);
    }

    public void StepFeedback(float deltaTime)
    {
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= deltaTime;
        }

        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualLocalScale, deltaTime * _poseLerpSpeed);
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRot, deltaTime * _poseLerpSpeed);
        }
    }

    private void UpdatePromptDisplay()
    {
        if (_promptText != null)
        {
            if (_currentInteractable != null)
            {
                _promptText.text = _currentInteractable.GetPromptText(this);
                if (_promptPanel != null) _promptPanel.SetActive(true);
            }
            else
            {
                _promptText.text = string.Empty;
                if (_promptPanel != null) _promptPanel.SetActive(false);
            }
        }
    }

    private bool CheckInteractionInput()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.eKey.wasPressedThisFrame) pressed = true;
        }
#endif
        try
        {
            if (!pressed && Input.GetKeyDown(_interactionKey))
            {
                pressed = true;
            }
        }
        catch
        {
            // Input System fallback
        }

        return pressed;
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

        if (_interactionVFX == null)
        {
            Transform existing = vfxRoot.Find("InteractSparkleVFX");
            GameObject fxObj = existing != null ? existing.gameObject : new GameObject("InteractSparkleVFX");
            if (existing == null)
            {
                fxObj.transform.SetParent(vfxRoot, false);
                fxObj.transform.localPosition = new Vector3(0f, 0.5f, 0.6f);
                _interactionVFX = fxObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            else
            {
                _interactionVFX = fxObj.GetComponent<ParticleSystem>() ?? fxObj.AddComponent<ParticleSystem>();
            }

            ConfigureSparkleParticles(_interactionVFX);
        }

#if UNITY_EDITOR
        if (_interactSfx == null)
        {
            _interactSfx = FindAudioClipByName("cozy_fx_1", "cozy_fx", "Click", "Coin", "PowerUp");
        }
#endif

        return createdAny;
    }

    private static void ConfigureSparkleParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.3f;
        main.startSpeed = 2.0f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 0.835f, 0.310f, 1f); // Primary gold
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

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
