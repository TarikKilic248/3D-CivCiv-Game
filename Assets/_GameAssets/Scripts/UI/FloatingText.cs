using System;
using UnityEngine;
using TMPro;

/// <summary>
/// CivCiv 3D - Uçan Metin Türleri (Altın, Hasar, Kombo)
/// </summary>
public enum FloatingTextKind
{
    Gold,
    Damage,
    Combo,
    Coin = Gold
}

/// <summary>
/// CivCiv 3D - Dünya Uzayında Süzülen Geri Bildirim Metni (FloatingText)
/// Pop-in scale overshoot, yukarı süzülme, kameraya billboard ve alfa sönümlenmesi uygular.
/// UIThemeSO üzerinden altın-sarı / turuncu / kehribar çizgi film paletine uyar.
/// </summary>
public class FloatingText : MonoBehaviour
{
    [Header("--- Components ---")]
    [SerializeField] private TMP_Text _textMesh;
    [SerializeField] private ParticleSystem _comboParticles;

    [Header("--- Animation & Motion ---")]
    [SerializeField] private float _defaultLifetime = 1.0f;
    [SerializeField] private float _floatSpeed = 1.5f;
    [SerializeField] private float _popInDuration = 0.25f;

    [Header("--- Audio ---")]
    [SerializeField] private AudioClip _comboPopClip;

    private Vector3 _startPosition;
    private Color _baseColor;
    private float _age;
    private float _duration;
    private bool _isAlive;
    private float _currentAlpha = 1f;
    private FloatingTextKind _kind;
    private Camera _camera;

    public bool IsAlive => _isAlive;
    public float CurrentAlpha => _currentAlpha;
    public FloatingTextKind Kind => _kind;
    public TMP_Text TextMesh => _textMesh;
    public ParticleSystem ComboParticles => _comboParticles;
    public AudioClip ComboPopClip { get => _comboPopClip; set => _comboPopClip = value; }
    public float FloatSpeed { get => _floatSpeed; set => _floatSpeed = value; }
    public float Duration { get => _duration; set => _duration = value; }
    public float PopInDuration { get => _popInDuration; set => _popInDuration = value; }

    private void Awake()
    {
        if (_textMesh == null)
        {
            _textMesh = GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        if (_isAlive)
        {
            Step(Time.deltaTime);
        }
    }

    public void Initialize(
        string text,
        Vector3 worldPos,
        FloatingTextKind kind,
        UIThemeSO theme = null,
        AudioClip comboSfx = null,
        float lifetime = 1.0f,
        float floatSpeed = 1.5f)
    {
        if (_textMesh == null)
        {
            _textMesh = GetComponent<TMP_Text>();
            if (_textMesh == null)
            {
                _textMesh = gameObject.AddComponent<TextMeshPro>();
            }
        }

        UIThemeSO t = UIStyleHelper.ResolveTheme(theme);
        _textMesh.text = text;
        UIStyleHelper.ApplyTextStyle(_textMesh, t, kind == FloatingTextKind.Combo, kind == FloatingTextKind.Combo ? 6.5f : 5.0f);

        switch (kind)
        {
            case FloatingTextKind.Gold:
                _baseColor = t != null ? t.primaryGold : new Color(1f, 0.835f, 0.310f, 1f);
                break;
            case FloatingTextKind.Damage:
                _baseColor = t != null ? t.lowWarningColor : new Color(0.898f, 0.224f, 0.208f, 1f);
                break;
            case FloatingTextKind.Combo:
                _baseColor = t != null ? t.accentOrange : new Color(1f, 0.596f, 0f, 1f);
                break;
            default:
                _baseColor = t != null ? t.primaryGold : new Color(1f, 0.835f, 0.310f, 1f);
                break;
        }

        _textMesh.color = _baseColor;
        _textMesh.alignment = TextAlignmentOptions.Center;
        if (Application.isPlaying)
        {
            _textMesh.outlineColor = t != null ? t.darkAmberBorder : new Color(0.427f, 0.231f, 0f, 1f);
            _textMesh.outlineWidth = 0.2f;
        }

        transform.position = worldPos;
        _startPosition = worldPos;
        _kind = kind;
        _duration = Mathf.Max(0.1f, lifetime);
        _floatSpeed = floatSpeed;
        _age = 0f;
        _isAlive = true;
        _currentAlpha = 1f;
        transform.localScale = Vector3.zero;

        if (comboSfx != null)
        {
            _comboPopClip = comboSfx;
        }

        gameObject.SetActive(true);
        Billboard();

        if (kind == FloatingTextKind.Combo)
        {
            EnsureParticleSystem();
            if (_comboParticles != null)
            {
                _comboParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _comboParticles.Play(true);
            }
            if (_comboPopClip != null)
            {
                SettingsUI.PlaySFX(_comboPopClip, 0.8f);
            }
        }
    }

    public void Step(float deltaTime)
    {
        if (!_isAlive || deltaTime <= 0f) return;

        _age += deltaTime;

        // 1. Dikey pozisyon monoton artar (yukarı süzülme)
        transform.position += Vector3.up * (_floatSpeed * deltaTime);

        // 2. Alfa monoton azalır
        float progress = Mathf.Clamp01(_age / _duration);
        _currentAlpha = Mathf.Clamp01(1f - progress);
        if (_textMesh != null)
        {
            Color c = _baseColor;
            c.a = _currentAlpha;
            _textMesh.color = c;
            _textMesh.alpha = _currentAlpha;
        }

        // 3. Pop-in overshoot ölçek animasyonu
        float scale = 1f;
        if (_popInDuration > 0.0001f && _age < _popInDuration)
        {
            float p = Mathf.Clamp01(_age / _popInDuration);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            scale = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
        }
        else
        {
            scale = 1f;
        }
        transform.localScale = Vector3.one * scale;

        // 4. Billboard
        Billboard();

        // 5. Ömür sonunda deaktif et ve havuza iade et
        if (_age >= _duration)
        {
            _isAlive = false;
            gameObject.SetActive(false);
        }
    }

    public void Billboard()
    {
        if (_camera == null)
        {
            _camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        }

        if (_camera != null)
        {
            transform.rotation = _camera.transform.rotation;
        }
    }

    public void EnsureParticleSystem()
    {
        if (_comboParticles != null) return;

        Transform child = transform.Find("ComboSparks");
        if (child == null)
        {
            GameObject sparkObj = new GameObject("ComboSparks");
            sparkObj.transform.SetParent(transform, false);
            child = sparkObj.transform;
        }

        _comboParticles = child.GetComponent<ParticleSystem>();
        if (_comboParticles == null)
        {
            _comboParticles = child.gameObject.AddComponent<ParticleSystem>();
        }

        var main = _comboParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 2f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 0.6f, 0.1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 20;

        var emission = _comboParticles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

        var shape = _comboParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        ParticleSystemRenderer rend = child.GetComponent<ParticleSystemRenderer>();
        if (rend != null && rend.sharedMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s != null)
            {
                Material mat = new Material(s);
                mat.hideFlags = HideFlags.HideAndDontSave;
                rend.sharedMaterial = mat;
            }
        }
    }
}
