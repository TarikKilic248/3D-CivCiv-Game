using UnityEngine;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    public int Coins { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool AddCoins(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Coin amount must be greater than zero. Received: {amount}", this);
            return false;
        }

        Coins += amount;
        Debug.Log($"Coins: {Coins}", this);
        return true;
    }
}
