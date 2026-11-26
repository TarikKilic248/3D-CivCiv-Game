using UnityEngine;

public class TargetJump : MonoBehaviour
{
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpHeight = 2f;

    public float JumpForce => jumpForce;
    public float JumpHeight => jumpHeight;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        Rigidbody playerRigidbody = player.GetRigidbody();
        if (playerRigidbody == null)
        {
            playerRigidbody = other.GetComponentInParent<Rigidbody>();
        }

        LaunchRigidbody(playerRigidbody);
    }

    public void LaunchRigidbody(Rigidbody playerRigidbody)
    {
        if (playerRigidbody == null)
            return;

        // Zıplatma öncesi mevcut dikey hızı sıfırlayarak düşüş hızından bağımsız tutarlı fırlatma yüksekliği sağla
        Vector3 currentVelocity = playerRigidbody.linearVelocity;
        playerRigidbody.linearVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        Vector3 jumpDirection = Vector3.up * jumpHeight;
        playerRigidbody.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
    }
}
