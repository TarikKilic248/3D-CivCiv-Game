using UnityEngine;
using TMPro;

public class ClockUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private RectTransform stick;
    [SerializeField] private float startingSeconds = 60f;
    [SerializeField] private float secondsAddedPerCoinValue = 5f;
    [SerializeField] private float stickMoveSpeed = 12f;

    private float remainingSeconds;
    private float currentStickAngle;
    private bool isSubscribed;

    private void Awake()
    {
        remainingSeconds = startingSeconds;

        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (stick == null)
        {
            Transform stickTransform = transform.Find("Stick");
            if (stickTransform != null)
                stick = stickTransform as RectTransform;
        }

        currentStickAngle = GetTargetAngle();
        if (stick != null)
            stick.localRotation = Quaternion.Euler(0f, 0f, currentStickAngle);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        UpdateTimerUI();
    }

    private void OnDisable()
    {
        if (isSubscribed && CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsCollected -= AddCoinTime;
            isSubscribed = false;
        }
    }

    private void Update()
    {
        if (remainingSeconds > 0f)
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);

        UpdateTimerUI();
    }

    private void Subscribe()
    {
        if (isSubscribed || CoinManager.Instance == null)
            return;

        CoinManager.Instance.CoinsCollected += AddCoinTime;
        isSubscribed = true;
        UpdateTimerUI();
    }

    private void AddCoinTime(int coinValue)
    {
        remainingSeconds += coinValue * secondsAddedPerCoinValue;
        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(remainingSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        if (stick == null)
            return;

        float targetAngle = GetTargetAngle();
        if (stickMoveSpeed > 0f && Time.unscaledDeltaTime > 0f)
        {
            currentStickAngle = Mathf.Lerp(
                currentStickAngle,
                targetAngle,
                1f - Mathf.Exp(-stickMoveSpeed * Time.unscaledDeltaTime));
        }
        else
        {
            currentStickAngle = targetAngle;
        }

        stick.localRotation = Quaternion.Euler(0f, 0f, currentStickAngle);
    }

    private float GetTargetAngle()
    {
        float baseSeconds = startingSeconds > 0f ? startingSeconds : 60f;
        float normalizedRemaining = remainingSeconds / baseSeconds;
        return -360f * (1f - normalizedRemaining);
    }
}
