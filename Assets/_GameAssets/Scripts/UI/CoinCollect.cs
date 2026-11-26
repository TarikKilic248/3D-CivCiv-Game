using System.Collections;
using UnityEngine;

public class CoinCollect : MonoBehaviour, ICollectable
{
    [SerializeField] private int value = 1;

    [Header("--- Visual & Feedback ---")]
    [Tooltip("Dönme hızı (derece/saniye)")]
    [SerializeField] private float rotateSpeed = 90f;

    [Tooltip("Süzülme (bobbing) dikey hareket genliği")]
    [SerializeField] private float bobAmplitude = 0.2f;

    [Tooltip("Süzülme (bobbing) hareket frekansı")]
    [SerializeField] private float bobFrequency = 2f;

    [Tooltip("Toplandığında küçülerek yok olma süresi (saniye)")]
    [SerializeField] private float shrinkDuration = 0.25f;

    [Tooltip("Toplama anında çalınacak ses efekti")]
    [SerializeField] private AudioClip collectSound;

    private Vector3 _startPosition;
    private float _bobTimer = 0f;
    private bool _isCollected = false;

    public int Value => value;
    public bool IsCollected => _isCollected;
    public float RotateSpeed => rotateSpeed;
    public float BobAmplitude => bobAmplitude;
    public float BobFrequency => bobFrequency;
    public float ShrinkDuration => shrinkDuration;
    public AudioClip CollectSound { get => collectSound; set => collectSound = value; }

    private void Awake()
    {
        _startPosition = transform.position;
    }

    private void Start()
    {
        if (_startPosition == Vector3.zero && transform.position != Vector3.zero)
        {
            _startPosition = transform.position;
        }
    }

    private void Update()
    {
        UpdateMotion(Time.deltaTime);
    }

    public void UpdateMotion(float deltaTime)
    {
        if (_isCollected) return;

        transform.Rotate(Vector3.up, rotateSpeed * deltaTime, Space.World);
        _bobTimer += deltaTime;
        float newY = _startPosition.y + Mathf.Sin(_bobTimer * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    public void Collect()
    {
        if (_isCollected)
            return;

        _isCollected = true;

        if (collectSound != null)
        {
            SettingsUI.PlaySFX(collectSound);
        }

        CoinManager coinManager = CoinManager.Instance != null
            ? CoinManager.Instance
            : FindAnyObjectByType<CoinManager>();

        if (coinManager != null)
        {
            coinManager.AddCoins(value);
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (Application.isPlaying && gameObject.activeInHierarchy)
        {
            StartCoroutine(ShrinkAndDestroyRoutine());
        }
        else
        {
            transform.localScale = Vector3.zero;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }

    private IEnumerator ShrinkAndDestroyRoutine()
    {
        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = shrinkDuration > 0f ? Mathf.Clamp01(elapsed / shrinkDuration) : 1f;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected)
            return;

        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        Collect();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _startPosition = transform.position;
        if (collectSound == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip");
            if (guids != null && guids.Length > 0)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.IndexOf("fx", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        collectSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        break;
                    }
                }
                if (collectSound == null && guids.Length > 0)
                {
                    collectSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }
    }
#endif
}
