using UnityEngine;

[CreateAssetMenu(fileName = "DefaultPlayerStats", menuName = "CivCiv/Player Stats")]
public class PlayerStatsSO : ScriptableObject
{
    [Header("--- Movement Settings ---")]
    [Tooltip("Yürüme hareket kuvveti çarpanı")]
    public float moveSpeed = 45f;

    [Tooltip("Normal yatay hareket hız sınırı (Max Speed)")]
    public float maxSpeed = 7f;

    [Header("--- Drag (Sürtünme) Settings ---")]
    [Tooltip("Zemindeyken uygulanan sürtünme")]
    public float groundDrag = 4f;

    [Tooltip("Havadayken uygulanan sürtünme")]
    public float airDrag = 0.2f;

    [Tooltip("Havadayken hareket kuvveti kontrol çarpanı")]
    public float airMultiplier = 0.4f;

    [Header("--- Jumping Settings ---")]
    [Tooltip("Zıplama kuvveti")]
    public float jumpForce = 7f;

    [Tooltip("Zıplamalar arası bekleme süresi (saniye)")]
    public float jumpCooldown = 0.25f;

    [Tooltip("Platform kenarından ayrıldıktan sonra zıplamaya izin verilen tolerans süresi (Coyote Time, saniye)")]
    public float coyoteTime = 0.15f;

    [Tooltip("Yere inmeden hemen önce basılan zıplama tuşunu hafızada tutma süresi (Jump Buffering, saniye)")]
    public float jumpBufferTime = 0.15f;

    [Header("--- Sliding (Kayma) Settings ---")]
    [Tooltip("Slide anındaki maksimum yatay hız sınırı")]
    public float maxSlideSpeed = 13f;

    [Tooltip("Slide başladığında uygulanan ilk itme kuvveti")]
    public float slideImpulseForce = 12f;

    [Tooltip("Slide sırasında devam eden ek itme kuvveti")]
    public float slideSustainForce = 15f;

    [Tooltip("Slide sırasındaki sürtünme (düşük olursa daha kaygan kayar)")]
    public float slideDrag = 0.3f;

    [Tooltip("Maksimum kayma süresi (saniye). 0 ise tuşa basılı tutulduğu sürece devam eder.")]
    public float maxSlideDuration = 1.2f;

    [Tooltip("Slide sırasında collider boyutunu küçült (çömelme efekti)")]
    public bool adjustColliderOnSlide = true;

    [Tooltip("Slide esnasında collider yükseklik çarpanı")]
    [Range(0.2f, 1f)]
    public float slideColliderMultiplier = 0.5f;

    [Header("--- Ground Check Settings ---")]
    [Tooltip("Zemin algılama ekstra mesafe toleransı")]
    public float groundCheckDistance = 0.25f;

    [Tooltip("Zemin algılama küre yarıçapı")]
    public float groundCheckRadius = 0.25f;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        groundDrag = Mathf.Max(0f, groundDrag);
        airDrag = Mathf.Max(0f, airDrag);
        airMultiplier = Mathf.Max(0f, airMultiplier);
        jumpForce = Mathf.Max(0f, jumpForce);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        maxSlideSpeed = Mathf.Max(0.1f, maxSlideSpeed);
        slideImpulseForce = Mathf.Max(0f, slideImpulseForce);
        slideSustainForce = Mathf.Max(0f, slideSustainForce);
        slideDrag = Mathf.Max(0f, slideDrag);
        maxSlideDuration = Mathf.Max(0f, maxSlideDuration);
        slideColliderMultiplier = Mathf.Clamp(slideColliderMultiplier, 0.2f, 1f);
        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
    }
}
