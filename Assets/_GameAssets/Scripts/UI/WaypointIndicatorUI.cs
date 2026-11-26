using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Hedef Pusulası ve Mesafe Göstergesi (WaypointIndicatorUI)
/// Ekranda en yakın kurtarılmamış kayıp civcivin yönünü (ok) ve mesafesini (metre) gösterir.
/// Tüm civcivler kurtarıldığında veya kurtarılacak civciv kalmadığında temizce gizlenir.
/// UIThemeSO / UIStyleHelper ile oyunun sıcak altın-sarı / turuncu / koyu kehribar stiline tam uygundur.
/// </summary>
[DisallowMultipleComponent]
public class WaypointIndicatorUI : MonoBehaviour
{
    [Header("--- Theme & Style ---")]
    [Tooltip("Arayüz görsel teması")]
    [SerializeField] private UIThemeSO _theme;

    [Header("--- UI References ---")]
    [Tooltip("Pusula gösterge paneli (kapsayıcı nesne)")]
    [SerializeField] private GameObject _indicatorPanel;

    [Tooltip("Mesafe metni (metre cinsinden gösterim)")]
    [SerializeField] private TextMeshProUGUI _distanceText;

    [Tooltip("Hedefe yönelen ok / pusula ibresi")]
    [SerializeField] private Image _arrowImage;

    [Tooltip("Ok döndürme transformu (boşsa _arrowImage'dan alınır)")]
    [SerializeField] private RectTransform _arrowRect;

    [Tooltip("İsteğe bağlı civciv ikonu")]
    [SerializeField] private Image _iconImage;

    [Header("--- Target & Camera ---")]
    [Tooltip("Ekran izdüşümü için ana kamera")]
    [SerializeField] private Camera _targetCamera;

    [Tooltip("Oyuncu dünya pozisyonu")]
    [SerializeField] private Transform _playerTransform;

    [Header("--- Display Options ---")]
    [Tooltip("Mesafe metni formatı (örn: {0}m)")]
    [SerializeField] private string _distanceFormat = "{0}m";

    // Runtime State
    private LostChickNPC _currentTargetChick;
    private float _currentDistance;

    // Public Properties
    public UIThemeSO Theme => _theme;
    public GameObject IndicatorPanel => _indicatorPanel;
    public TextMeshProUGUI DistanceText => _distanceText;
    public Image ArrowImage => _arrowImage;
    public RectTransform ArrowRect => _arrowRect;
    public LostChickNPC CurrentTargetChick => _currentTargetChick;
    public float CurrentDistance => _currentDistance;
    public string CurrentDistanceText => _distanceText != null ? _distanceText.text : string.Empty;
    public bool IsVisible => _indicatorPanel != null ? _indicatorPanel.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        ResolveDependencies();
        ApplyTheme(_theme);
    }

    private void Start()
    {
        ResolveDependencies();
        SubscribeEvents();
        UpdateIndicatorManual(GetPlayerPosition(), _targetCamera);
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void ResolveDependencies()
    {
        if (_indicatorPanel == null)
        {
            Transform p = transform.Find("IndicatorPanel");
            _indicatorPanel = p != null ? p.gameObject : gameObject;
        }

        if (_distanceText == null)
        {
            _distanceText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_arrowImage == null)
        {
            Transform arrowTr = transform.Find("ArrowImage") ?? transform.Find("IndicatorPanel/ArrowImage");
            if (arrowTr != null)
            {
                _arrowImage = arrowTr.GetComponent<Image>();
            }
        }

        if (_arrowRect == null && _arrowImage != null)
        {
            _arrowRect = _arrowImage.rectTransform;
        }

        if (_iconImage == null)
        {
            Transform iconTr = transform.Find("IconImage") ?? transform.Find("IndicatorPanel/IconImage");
            if (iconTr != null)
            {
                _iconImage = iconTr.GetComponent<Image>();
            }
        }

        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        if (_playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }
    }

    public void SubscribeEvents()
    {
        UnsubscribeEvents();

        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.OnRescueProgressChanged += HandleRescueProgressChanged;
            ChickRescueManager.Instance.OnAllChicksRescued += HandleAllChicksRescued;
        }
    }

    public void UnsubscribeEvents()
    {
        if (ChickRescueManager.Instance != null)
        {
            ChickRescueManager.Instance.OnRescueProgressChanged -= HandleRescueProgressChanged;
            ChickRescueManager.Instance.OnAllChicksRescued -= HandleAllChicksRescued;
        }
    }

    private void HandleRescueProgressChanged(int rescued, int total)
    {
        UpdateIndicatorManual(GetPlayerPosition(), _targetCamera);
    }

    private void HandleAllChicksRescued()
    {
        SetIndicatorActive(false);
    }

    private void Update()
    {
        Vector3 playerPos = GetPlayerPosition();
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        UpdateIndicatorManual(playerPos, _targetCamera);
    }

    private Vector3 GetPlayerPosition()
    {
        if (_playerTransform != null)
        {
            return _playerTransform.position;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            _playerTransform = pc.transform;
            return _playerTransform.position;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Hem Update hem de Editör doğrulama testleri için deterministik pusula ve mesafe hesaplayıcısı.
    /// </summary>
    public void UpdateIndicatorManual(Vector3 playerPos, Camera cam)
    {
        // 1. En yakın kurtarılmamış civcivi bul
        if (ChickRescueManager.Instance != null)
        {
            _currentTargetChick = ChickRescueManager.Instance.GetNearestUnrescuedChick(playerPos);
        }
        else
        {
            _currentTargetChick = FindNearestUnrescuedChickFallback(playerPos);
        }

        // 2. Kurtarılmamış civciv kalmadıysa göstergeyi gizle
        if (_currentTargetChick == null || _currentTargetChick.IsRescued)
        {
            _currentDistance = 0f;
            SetIndicatorActive(false);
            return;
        }

        // 3. Göstergeyi aktif yap ve mesafe metnini güncelle
        SetIndicatorActive(true);

        Vector3 targetPos = _currentTargetChick.transform.position;
        _currentDistance = Vector3.Distance(playerPos, targetPos);

        if (_distanceText != null)
        {
            int meters = Mathf.RoundToInt(_currentDistance);
            _distanceText.text = string.Format(_distanceFormat, meters);
        }

        // 4. Ok yönünü hedefe doğru döndür
        if (_arrowRect != null)
        {
            float angle = 0f;

            if (cam != null)
            {
                Vector3 screenPoint = cam.WorldToScreenPoint(targetPos);
                bool isBehind = screenPoint.z < 0f;
                if (isBehind)
                {
                    screenPoint *= -1f;
                }

                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 screenDir = new Vector2(screenPoint.x - screenCenter.x, screenPoint.y - screenCenter.y);

                if (screenDir.sqrMagnitude > 0.001f)
                {
                    angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg - 90f;
                }
            }
            else
            {
                // Kamera yoksa (ör. izole test ortamı) XZ yatay düzlem açısı
                Vector3 dir = targetPos - playerPos;
                angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }

            _arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private LostChickNPC FindNearestUnrescuedChickFallback(Vector3 origin)
    {
        LostChickNPC[] all = FindObjectsByType<LostChickNPC>(FindObjectsInactive.Include);
        if (all == null || all.Length == 0) return null;

        LostChickNPC nearest = null;
        float minSqr = float.MaxValue;
        for (int i = 0; i < all.Length; i++)
        {
            LostChickNPC chick = all[i];
            if (chick == null || !chick.gameObject.activeInHierarchy || chick.IsRescued) continue;

            float distSqr = (chick.transform.position - origin).sqrMagnitude;
            if (distSqr < minSqr)
            {
                minSqr = distSqr;
                nearest = chick;
            }
        }
        return nearest;
    }

    public void SetIndicatorActive(bool active)
    {
        if (_indicatorPanel != null && _indicatorPanel != gameObject)
        {
            if (_indicatorPanel.activeSelf != active)
            {
                _indicatorPanel.SetActive(active);
            }
        }
        else
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }

    /// <summary>
    /// UIThemeSO / UIStyleHelper ile arayüz renklerini, koyu kehribar kontur ve altın sarısı tonları uygular.
    /// </summary>
    public void ApplyTheme(UIThemeSO themeOverride = null)
    {
        if (themeOverride != null)
        {
            _theme = themeOverride;
        }

        UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);

        ResolveDependencies();

        if (_indicatorPanel != null)
        {
            UIStyleHelper.ApplyPanelStyle(_indicatorPanel, activeTheme, false);
        }

        if (_distanceText != null)
        {
            UIStyleHelper.ApplyTextStyle(_distanceText, activeTheme, false, 20f);
        }

        if (_arrowImage != null)
        {
            _arrowImage.color = activeTheme.primaryGold;
        }

        if (_iconImage != null)
        {
            _iconImage.color = activeTheme.primaryGold;
        }
    }
}
