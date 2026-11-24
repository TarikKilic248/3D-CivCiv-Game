using UnityEngine;

public class CoinCollect : MonoBehaviour, ICollectable
{
    [SerializeField] private int value = 1;

    public void Collect()
    {
        if (CoinManager.Instance == null)
        {
            Debug.LogError("CoinManager is missing from the scene.", this);
            return;
        }

        if (CoinManager.Instance.AddCoins(value))
            Destroy(gameObject);

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        Collect();
    }
}
