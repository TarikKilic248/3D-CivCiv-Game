using UnityEngine;

public class CoinCollect : MonoBehaviour
{
    [SerializeField] private int value = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        if (CoinManager.Instance == null)
        {
            Debug.LogError("CoinManager is missing from the scene.", this);
            return;
        }

        if (CoinManager.Instance.AddCoins(value))
            Destroy(gameObject);
    }
}
