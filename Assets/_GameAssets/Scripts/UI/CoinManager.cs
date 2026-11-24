using UnityEngine;
using System;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    public int Coins { get; private set; }
    public event Action<int> CoinsChanged;
    public event Action<int> CoinsCollected;

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
        CoinsChanged?.Invoke(Coins);
        CoinsCollected?.Invoke(amount);
        Debug.Log($"Coins: {Coins}", this);
        return true;
    }
}
