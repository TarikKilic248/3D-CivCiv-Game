using System;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [Tooltip("Varsayılan başlangıç kontrol noktası mı?")]
    [SerializeField] private bool _isDefaultCheckpoint = false;

    [Tooltip("Yeniden doğma pozisyonu için özel Transform (boşsa nesnenin pozisyonu kullanılır)")]
    [SerializeField] private Transform _respawnPointTransform;

    [Tooltip("Yeniden doğma pozisyonu ekstra öteleme")]
    [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

    [Header("Feedback Settings")]
    [Tooltip("Görsel geri bildirim için Renderer bileşeni")]
    [SerializeField] private Renderer _indicatorRenderer;

    [Tooltip("Aktifleştiğinde atanacak renk")]
    [SerializeField] private Color _activeColor = Color.green;

    [Tooltip("Aktifleştiğinde oynatılacak parçacık efekti")]
    [SerializeField] private ParticleSystem _activateParticles;

    [Tooltip("Aktifleştiğinde çalınacak ses efekti")]
    [SerializeField] private AudioClip _activateSound;

    // Static State
    private static Checkpoint _activeCheckpoint;
    private static Vector3 _activeRespawnPoint = Vector3.zero;
    private static bool _hasActiveRespawnPoint = false;

    public static Checkpoint ActiveCheckpoint => _activeCheckpoint;

    public static Vector3 ActiveRespawnPoint
    {
        get
        {
            if (_activeCheckpoint != null)
            {
                return _activeCheckpoint.RespawnPosition;
            }
            return _activeRespawnPoint;
        }
        set
        {
            _activeRespawnPoint = value;
            _hasActiveRespawnPoint = true;
        }
    }

    public static bool HasActiveRespawnPoint => _hasActiveRespawnPoint || _activeCheckpoint != null;

    public static event Action<Checkpoint> OnCheckpointActivated;
    public event Action OnActivated;

    // Instance State
    private bool _isActivated = false;

    public bool IsActivated => _isActivated;
    public bool IsDefaultCheckpoint
    {
        get => _isDefaultCheckpoint;
        set => _isDefaultCheckpoint = value;
    }
    public Transform RespawnPointTransform
    {
        get => _respawnPointTransform;
        set => _respawnPointTransform = value;
    }
    public Vector3 SpawnOffset
    {
        get => _spawnOffset;
        set => _spawnOffset = value;
    }
    public Renderer IndicatorRenderer
    {
        get => _indicatorRenderer;
        set => _indicatorRenderer = value;
    }

    public Vector3 RespawnPosition
    {
        get
        {
            Vector3 basePos = _respawnPointTransform != null ? _respawnPointTransform.position : transform.position;
            return basePos + _spawnOffset;
        }
    }

    private void Awake()
    {
        if (_isDefaultCheckpoint || _activeCheckpoint == null)
        {
            Activate(false);
        }
    }

    private void OnDestroy()
    {
        if (_activeCheckpoint == this)
        {
            _activeCheckpoint = null;
        }
    }

    public static void ResetActiveCheckpoint()
    {
        _activeCheckpoint = null;
        _activeRespawnPoint = Vector3.zero;
        _hasActiveRespawnPoint = false;
    }

    public static void SetActiveCheckpoint(Checkpoint checkpoint, bool triggerFeedback = true)
    {
        if (checkpoint != null)
        {
            checkpoint.Activate(triggerFeedback);
        }
    }

    public void Activate(bool triggerFeedback = true)
    {
        _activeCheckpoint = this;
        _activeRespawnPoint = RespawnPosition;
        _hasActiveRespawnPoint = true;
        _isActivated = true;

        if (triggerFeedback)
        {
            PlayFeedback();
            OnCheckpointActivated?.Invoke(this);
            OnActivated?.Invoke();
        }
    }

    public void PlayFeedback()
    {
        if (_indicatorRenderer != null)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            _indicatorRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", _activeColor);
            mpb.SetColor("_BaseColor", _activeColor);
            _indicatorRenderer.SetPropertyBlock(mpb);
        }

        if (_activateParticles != null)
        {
            _activateParticles.Play();
        }

        if (_activateSound != null)
        {
            AudioSource.PlayClipAtPoint(_activateSound, RespawnPosition);
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other, out PlayerController player))
        {
            if (_activeCheckpoint != this || !_isActivated)
            {
                Activate(true);
            }
        }
    }

    private bool IsPlayer(Collider other, out PlayerController player)
    {
        player = null;
        if (other == null) return false;

        player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }
        if (player == null && other.attachedRigidbody != null)
        {
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        }

        return player != null;
    }
}
