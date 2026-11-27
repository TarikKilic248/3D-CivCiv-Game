using System.Reflection;
using UnityEngine;

public static class Check_T021
{
    [AutoDevCheck("T021")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        GameObject padObj = null, playerObj = null;
        try
        {
            padObj = new GameObject("T021_Pad");
            padObj.transform.position = new Vector3(0f, 970f, 0f);
            padObj.AddComponent<BoxCollider>();
            BouncePad pad = padObj.AddComponent<BouncePad>();
            AutoDevVerifier.InvokeLifecycle(pad, "Awake");

            playerObj = new GameObject("T021_Player");
            playerObj.transform.position = padObj.transform.position + Vector3.up * 2f;
            playerObj.AddComponent<CapsuleCollider>();
            Rigidbody rb = playerObj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            playerObj.AddComponent<StateController>();
            PlayerController pc = playerObj.AddComponent<PlayerController>();
            ChickGlideController glide = playerObj.AddComponent<ChickGlideController>();
            ChickPeckAttack attack = playerObj.AddComponent<ChickPeckAttack>();
            AutoDevVerifier.InvokeLifecycle(pc, "Awake");
            AutoDevVerifier.InvokeLifecycle(glide, "Awake");
            AutoDevVerifier.InvokeLifecycle(attack, "Awake");

            int events = 0;
            pad.OnBounced += _ => events++;

            // Normal bounce
            typeof(ChickGlideController).GetField("_remainingAirJumps", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(glide, 0);
            rb.linearVelocity = new Vector3(0f, -12f, 0f);
            if (!pad.ApplyBounce(rb, false))
                return AutoDevCheckResult.Fail("ApplyBounce ilk tetiklemede basarisiz.");
            float normalY = rb.linearVelocity.y;
            if (normalY < pad.BounceForce * 0.9f)
                return AutoDevCheckResult.Fail($"Dikey hiz yetersiz: {normalY:F2} < {pad.BounceForce * 0.9f:F2}");
            if (glide.RemainingAirJumps != glide.MaxAirJumps)
                return AutoDevCheckResult.Fail("ResetAirJumps cagrilmadi.");
            if (events != 1) return AutoDevCheckResult.Fail($"OnBounced sayisi hatali: {events}");

            // Visual feedback
            if (pad.VisualRoot.localScale.y >= 1f)
                return AutoDevCheckResult.Fail($"Squash olusmadi: {pad.VisualRoot.localScale.y:F2}");
            if (pad.BounceVFX == null || !pad.BounceVFX.isPlaying)
                return AutoDevCheckResult.Fail("ParticleSystem oynatilmadi.");
            if (pad.ActiveTrail == null || !pad.ActiveTrail.emitting)
                return AutoDevCheckResult.Fail("TrailRenderer emitting=false.");
            if (pad.SfxPlayCount != 1)
                return AutoDevCheckResult.Fail("SFX kancasi cagrilmadi.");

            // Cooldown
            rb.linearVelocity = new Vector3(0f, -5f, 0f);
            if (pad.ApplyBounce(rb, false) || !Mathf.Approximately(rb.linearVelocity.y, -5f) || events != 1)
                return AutoDevCheckResult.Fail("Cooldown icinde ikinci tetikleme hizi degistirdi veya event yayinladi.");

            // Squash recovers
            pad.StepFeedback(pad.Cooldown + 1f);
            if (Vector3.Distance(pad.VisualRoot.localScale, Vector3.one) > 0.05f)
                return AutoDevCheckResult.Fail($"Scale geri donmedi: {pad.VisualRoot.localScale}");

            // Ground pound bounce
            rb.linearVelocity = Vector3.zero;
            attack.StartGroundPound();
            if (!attack.IsGroundPounding)
                return AutoDevCheckResult.Fail("Ground pound baslatilamadi.");
            if (!pad.ApplyBounce(rb))
                return AutoDevCheckResult.Fail("Ground pound sirasinda bounce basarisiz.");
            float poundY = rb.linearVelocity.y;
            if (poundY < normalY * 1.3f)
                return AutoDevCheckResult.Fail($"Ground pound hizi yetersiz: {poundY:F2} < {normalY * 1.3f:F2}");

            return AutoDevCheckResult.Pass($"BouncePad dogrulandi. Normal={normalY:F1}, Pound={poundY:F1}");
        }
        finally
        {
            if (playerObj != null) Object.DestroyImmediate(playerObj);
            if (padObj != null) Object.DestroyImmediate(padObj);
        }
    }
}
