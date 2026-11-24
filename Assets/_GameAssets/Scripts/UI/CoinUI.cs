using UnityEngine;
using TMPro;

public class CoinUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI coinValueText;
    private bool isSubscribed;

    private void Awake()
    {
        if (coinValueText == null)
        {
            foreach (TextMeshProUGUI text in transform.root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == "CoinValueText")
                {
                    coinValueText = text;
                    break;
                }
            }
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsChanged -= HandleCoinsChanged;
            isSubscribed = false;
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || CoinManager.Instance == null)
            return;

        CoinManager.Instance.CoinsChanged += HandleCoinsChanged;
        isSubscribed = true;
        UpdateText(CoinManager.Instance.Coins);
    }

    private void HandleCoinsChanged(int coins)
    {
        UpdateText(coins);
    }

    private void UpdateText(int coins)
    {
        if (coinValueText == null)
        {
            Debug.LogError("CoinValueText reference is missing.", this);
            return;
        }

        coinValueText.text = $"x {coins}";
    }
}
