using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Civciv Kurtarma Yöneticisi (ChickRescueManager)
/// Sahnedeki tüm kayıp civcivleri takip eder, kurtarılma durumunu ve kuyruk takibini yönetir.
/// WaypointIndicatorUI için en yakın kurtarılmamış civcivi belirler.
/// </summary>
[DisallowMultipleComponent]
public class ChickRescueManager : MonoBehaviour
{
    public static ChickRescueManager Instance { get; set; }

    [Header("--- Player & Chain Follow ---")]
    [Tooltip("Kurtarılan ilk civcivin takip edeceği oyuncu referansı")]
    [SerializeField] private Transform _playerTransform;

    [Tooltip("Kurtarılan civcivlerin ardışık kuyruk aralığı (metre)")]
    [SerializeField] private float _chickQueueSpacing = 1.3f;

    // Koleksiyonlar
    private readonly List<LostChickNPC> _allChicks = new List<LostChickNPC>();
    private readonly List<LostChickNPC> _rescuedChicks = new List<LostChickNPC>();

    // Olaylar (Events)
    public event Action<LostChickNPC> OnChickRescued;
    public event Action<int, int> OnRescueProgressChanged; // (rescuedCount, totalCount)
    public event Action OnAllChicksRescued;

    // Public Properties
    public int TotalChickCount => _allChicks.Count;
    public int RescuedChickCount => _rescuedChicks.Count;
    public int RemainingChickCount => Mathf.Max(0, TotalChickCount - RescuedChickCount);
    public bool AreAllChicksRescued => TotalChickCount > 0 && RescuedChickCount >= TotalChickCount;
    public IReadOnlyList<LostChickNPC> RescuedChicks => _rescuedChicks;
    public IReadOnlyList<LostChickNPC> AllChicks => _allChicks;
    public Transform PlayerTransform { get => _playerTransform; set => _playerTransform = value; }
    public float ChickQueueSpacing { get => _chickQueueSpacing; set => _chickQueueSpacing = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolvePlayerReference();
        RefreshChickList();
    }

    private void Start()
    {
        ResolvePlayerReference();
        RefreshChickList();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResolvePlayerReference()
    {
        if (_playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }
    }

    /// <summary>
    /// Sahnedeki tüm LostChickNPC bileşenlerini bularak listeyi günceller.
    /// Unity 6 API: FindObjectsByType(FindObjectsInactive.Include)
    /// </summary>
    public void RefreshChickList()
    {
        LostChickNPC[] found = FindObjectsByType<LostChickNPC>(FindObjectsInactive.Include);
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                RegisterChick(found[i]);
            }
        }
    }

    /// <summary>
    /// Bir civcivi yöneticinin kayıt defterine ekler.
    /// </summary>
    public void RegisterChick(LostChickNPC chick)
    {
        if (chick == null) return;

        if (!_allChicks.Contains(chick))
        {
            _allChicks.Add(chick);

            if (chick.IsRescued && !_rescuedChicks.Contains(chick))
            {
                _rescuedChicks.Add(chick);
            }

            OnRescueProgressChanged?.Invoke(RescuedChickCount, TotalChickCount);
        }
    }

    /// <summary>
    /// Bir civcivi yöneticiden çıkarır.
    /// </summary>
    public void UnregisterChick(LostChickNPC chick)
    {
        if (chick == null) return;

        bool removed = _allChicks.Remove(chick);
        _rescuedChicks.Remove(chick);

        if (removed)
        {
            OnRescueProgressChanged?.Invoke(RescuedChickCount, TotalChickCount);
        }
    }

    /// <summary>
    /// Civciv kurtarıldığında çağrılır; takip hedefini kuyruk zincirine bağlar ve olayları tetikler.
    /// </summary>
    public void RescueChick(LostChickNPC chick)
    {
        if (chick == null) return;

        if (!_allChicks.Contains(chick))
        {
            _allChicks.Add(chick);
        }

        if (!_rescuedChicks.Contains(chick))
        {
            _rescuedChicks.Add(chick);

            ResolvePlayerReference();

            // Kuyruk Takip Mantığı:
            // 1. kurtarılan civciv -> Oyuncuyu takip eder
            // 2., 3., ... civcivler -> Kendilerinden bir önceki kurtarılmış civcivi takip eder (civciv kervanı!)
            if (_rescuedChicks.Count == 1)
            {
                chick.FollowTarget = _playerTransform;
            }
            else
            {
                LostChickNPC previousChick = _rescuedChicks[_rescuedChicks.Count - 2];
                chick.FollowTarget = previousChick != null ? previousChick.transform : _playerTransform;
            }

            chick.FollowDistance = _chickQueueSpacing;

            OnChickRescued?.Invoke(chick);
            OnRescueProgressChanged?.Invoke(RescuedChickCount, TotalChickCount);

            if (AreAllChicksRescued)
            {
                OnAllChicksRescued?.Invoke();
            }
        }
    }

    /// <summary>
    /// Belirtilen dünya konumuna en yakın, henüz kurtarılmamış civcivi döndürür.
    /// </summary>
    public LostChickNPC GetNearestUnrescuedChick(Vector3 origin)
    {
        LostChickNPC nearest = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < _allChicks.Count; i++)
        {
            LostChickNPC chick = _allChicks[i];
            if (chick == null || !chick.gameObject.activeInHierarchy || chick.IsRescued)
                continue;

            float distSqr = (chick.transform.position - origin).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = chick;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Testler ve bölüm yeniden başlatmaları için durumu sıfırlar.
    /// </summary>
    public void ResetState()
    {
        _allChicks.Clear();
        _rescuedChicks.Clear();
        OnRescueProgressChanged?.Invoke(0, 0);
    }
}
