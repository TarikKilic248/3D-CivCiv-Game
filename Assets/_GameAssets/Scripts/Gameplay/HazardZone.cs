using UnityEngine;

public class HazardZone : MonoBehaviour
{
    [SerializeField] private Transform _fallbackSpawnPoint;

    private Vector3 _initialPlayerPosition;
    private bool _hasInitialPosition;

    private void Start()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            _initialPlayerPosition = player.transform.position;
            _hasInitialPosition = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        RespawnPlayer(player);
    }

    public void RespawnPlayer(PlayerController player)
    {
        if (player == null)
            return;

        Rigidbody rb = player.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 targetPos;
        if (Checkpoint.HasCheckpoint)
        {
            targetPos = Checkpoint.ActiveRespawnPoint;
        }
        else if (_fallbackSpawnPoint != null)
        {
            targetPos = _fallbackSpawnPoint.position;
        }
        else if (_hasInitialPosition)
        {
            targetPos = _initialPlayerPosition;
        }
        else
        {
            targetPos = Vector3.up * 1.5f;
        }

        player.transform.position = targetPos;
        if (rb != null)
        {
            rb.position = targetPos;
        }
    }
}
