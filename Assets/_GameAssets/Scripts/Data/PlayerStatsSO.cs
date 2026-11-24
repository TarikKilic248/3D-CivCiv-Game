using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "CivCiv/Data/PlayerStatsSO")]
public class PlayerStatsSO : ScriptableObject
{
    [Header("--- Movement Settings ---")]
    public float MoveSpeed = 45f;
    public float MaxSpeed = 7f;
    public float GroundDrag = 4f;
    public float AirDrag = 0.2f;
    public float AirMultiplier = 0.4f;

    [Header("--- Jump Settings ---")]
    public float JumpForce = 7f;
    public float JumpCooldown = 0.25f;

    [Header("--- Slide Settings ---")]
    public float MaxSlideSpeed = 13f;
    public float SlideImpulseForce = 12f;
    public float SlideSustainForce = 15f;
    public float SlideDrag = 0.3f;

    [Header("--- Dash & Stamina Settings ---")]
    public float DashForce = 18f;
    public float DashCooldown = 1.2f;
    public float DashDuration = 0.2f;
    public float DashStaminaCost = 35f;
    public float MaxStamina = 100f;
    public float StaminaRegenRate = 20f;

    public void ValidateStats()
    {
        MoveSpeed = Mathf.Max(0.1f, MoveSpeed);
        MaxSpeed = Mathf.Max(0.1f, MaxSpeed);
        GroundDrag = Mathf.Max(0f, GroundDrag);
        AirDrag = Mathf.Max(0f, AirDrag);
        AirMultiplier = Mathf.Max(0.01f, AirMultiplier);

        JumpForce = Mathf.Max(0.1f, JumpForce);
        JumpCooldown = Mathf.Max(0.05f, JumpCooldown);

        MaxSlideSpeed = Mathf.Max(0.1f, MaxSlideSpeed);
        SlideImpulseForce = Mathf.Max(0f, SlideImpulseForce);
        SlideSustainForce = Mathf.Max(0f, SlideSustainForce);
        SlideDrag = Mathf.Max(0f, SlideDrag);

        DashForce = Mathf.Max(0.1f, DashForce);
        DashCooldown = Mathf.Max(0.05f, DashCooldown);
        DashDuration = Mathf.Max(0.05f, DashDuration);
        DashStaminaCost = Mathf.Max(0f, DashStaminaCost);

        MaxStamina = Mathf.Max(1f, MaxStamina);
        StaminaRegenRate = Mathf.Max(0f, StaminaRegenRate);
    }

    private void OnValidate()
    {
        ValidateStats();
    }
}
