using UnityEngine;

public class TargetJump : MonoBehaviour
{
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpHeight = 2f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        Rigidbody playerRigidbody = other.GetComponentInParent<Rigidbody>();
        if (playerRigidbody != null)
        {
            Vector3 jumpDirection = Vector3.up * jumpHeight;
            playerRigidbody.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
        }
    }
}
