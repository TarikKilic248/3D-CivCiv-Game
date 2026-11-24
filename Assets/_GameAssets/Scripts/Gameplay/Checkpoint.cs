using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public static Vector3 ActiveRespawnPoint { get; private set; }
    public static bool HasCheckpoint { get; private set; }

    public static void SetCheckpoint(Vector3 position)
    {
        ActiveRespawnPoint = position;
        HasCheckpoint = true;
    }

    public static void ResetCheckpoints()
    {
        ActiveRespawnPoint = Vector3.zero;
        HasCheckpoint = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        SetCheckpoint(transform.position + Vector3.up * 0.5f);
    }
}
