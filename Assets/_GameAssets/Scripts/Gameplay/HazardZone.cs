using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HazardZone : MonoBehaviour
{
    [Header("Hazard Settings")]
    [Tooltip("Özel yeniden doğma noktası (boşsa Checkpoint.ActiveRespawnPoint kullanılır)")]
    [SerializeField] private Transform _overrideRespawnTransform;

    [Tooltip("Işınlanma anında çalınacak ses efekti")]
    [SerializeField] private AudioClip _hazardSound;

    [Tooltip("Işınlanma anında oynatılacak parçacık efekti")]
    [SerializeField] private ParticleSystem _hazardVFX;

    public static event Action<PlayerController, Vector3> OnPlayerHazardTriggered;
    public event Action<PlayerController> OnHazardEntered;

    public Transform OverrideRespawnTransform
    {
        get => _overrideRespawnTransform;
        set => _overrideRespawnTransform = value;
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other, out PlayerController player))
        {
            OnHazardEntered?.Invoke(player);
            RespawnPlayer(player);
        }
    }

    private bool IsPlayer(Collider other, out PlayerController player)
    {
        player = null;
        if (other == null) return false;

        player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }
        if (player == null && other.attachedRigidbody != null)
        {
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        }

        return player != null;
    }

    public void RespawnPlayer(PlayerController player)
    {
        if (player == null) return;

        Vector3 targetPos = GetRespawnPosition(player);

        Rigidbody rb = player.GetRigidbody();
        if (rb == null)
        {
            rb = player.GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = targetPos;
        }

        player.transform.position = targetPos;
        Physics.SyncTransforms();

        DashController dash = player.GetComponent<DashController>();
        if (dash != null)
        {
            dash.EndDash();
        }

        if (_hazardVFX != null)
        {
            _hazardVFX.transform.position = targetPos;
            _hazardVFX.Play();
        }

        if (_hazardSound != null)
        {
            AudioSource.PlayClipAtPoint(_hazardSound, targetPos);
        }

        OnPlayerHazardTriggered?.Invoke(player, targetPos);
        Debug.Log($"[HazardZone] Player '{player.name}' respawned to {targetPos} and velocity reset to zero.");
    }

    public Vector3 GetRespawnPosition(PlayerController player)
    {
        if (_overrideRespawnTransform != null)
        {
            return _overrideRespawnTransform.position;
        }

        if (Checkpoint.HasActiveRespawnPoint)
        {
            return Checkpoint.ActiveRespawnPoint;
        }

        Checkpoint[] checkpoints = UnityEngine.Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include);
        foreach (var cp in checkpoints)
        {
            if (cp.IsDefaultCheckpoint)
            {
                cp.Activate(false);
                return cp.RespawnPosition;
            }
        }

        if (checkpoints.Length > 0)
        {
            checkpoints[0].Activate(false);
            return checkpoints[0].RespawnPosition;
        }

        return player != null ? player.transform.position : Vector3.zero;
    }
}
