using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComboUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _comboText;
    [SerializeField] private Image _comboTimerFill;

    private bool _isSubscribed;
    private int _lastStreak;
    private Coroutine _punchRoutine;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        if (_comboText != null)
        {
            _baseScale = _comboText.transform.localScale;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        if (ComboManager.Instance != null)
        {
            UpdateComboView(
                ComboManager.Instance.CurrentStreak,
                ComboManager.Instance.CurrentMultiplier,
                ComboManager.Instance.RemainingComboTimeNormalized);
        }
        else
        {
            UpdateComboView(0, 1, 0f);
        }
    }

    private void OnDisable()
    {
        if (_isSubscribed && ComboManager.Instance != null)
        {
            ComboManager.Instance.OnComboUpdated -= UpdateComboView;
            _isSubscribed = false;
        }
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (_isSubscribed || ComboManager.Instance == null)
            return;

        ComboManager.Instance.OnComboUpdated += UpdateComboView;
        _isSubscribed = true;
    }

    private void UpdateComboView(int streak, int multiplier, float normalizedTime)
    {
        bool showCombo = multiplier > 1 && streak > 0;

        if (_comboText != null)
        {
            _comboText.gameObject.SetActive(showCombo);
            if (showCombo)
            {
                _comboText.text = $"x{multiplier} COMBO ({streak})";
                if (streak > _lastStreak && gameObject.activeInHierarchy)
                {
                    if (_punchRoutine != null) StopCoroutine(_punchRoutine);
                    _punchRoutine = StartCoroutine(PunchScaleRoutine());
                }
            }
        }

        if (_comboTimerFill != null)
        {
            _comboTimerFill.gameObject.SetActive(showCombo);
            _comboTimerFill.fillAmount = Mathf.Clamp01(normalizedTime);
        }

        _lastStreak = streak;
    }

    private IEnumerator PunchScaleRoutine()
    {
        if (_comboText == null)
            yield break;

        Transform tr = _comboText.transform;
        const float duration = 0.16f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleFactor = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            tr.localScale = _baseScale * scaleFactor;
            yield return null;
        }

        tr.localScale = _baseScale;
        _punchRoutine = null;
    }
}
