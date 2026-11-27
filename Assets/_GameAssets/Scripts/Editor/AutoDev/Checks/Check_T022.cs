using UnityEngine;

public static class Check_T022
{
    private class DummyDamageable : MonoBehaviour, IDamageable
    {
        public int CurrentHealth { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public bool IsAlive => CurrentHealth > 0;
        public bool IsInvincible => false;
        public int HitCount { get; private set; }
        public void TakeDamage(int damage) => TakeDamage(damage, Vector3.zero);
        public void TakeDamage(int damage, Vector3 dir) { CurrentHealth -= damage; HitCount++; }
        public void Heal(int amount) => CurrentHealth += amount;
    }

    [AutoDevCheck("T022")]
    public static AutoDevCheckResult Run()
    {
        GameObject hazardObj = null, dummyObj = null, playerObj = null;
        try
        {
            hazardObj = new GameObject("T022_Hazard");
            hazardObj.transform.position = new Vector3(0f, 980f, 0f);
            SwingingHazard hz = hazardObj.AddComponent<SwingingHazard>();
            AutoDevVerifier.InvokeLifecycle(hz, "Awake");

            if (hz.Trail == null || hz.HitVFX == null)
                return AutoDevCheckResult.Fail("TrailRenderer veya ParticleSystem olusmadi.");

            float max = hz.MaxAngle;
            int changes = 0;
            float prev = 0f;
            for (int i = 0; i < 200; i++)
            {
                hz.Step(0.02f);
                float a = hz.CurrentAngle;
                float z = hz.Pivot.localEulerAngles.z;
                if (z > 180f) z -= 360f;
                if (Mathf.Abs(z) > max + 0.01f)
                    return AutoDevCheckResult.Fail($"Pivot acisi aralik disi: {z}");
                if (Mathf.Abs(z - a) > 0.05f)
                    return AutoDevCheckResult.Fail($"Pivot acisi sin formulu ile uyusmuyor: {z} vs {a}");
                if (prev != 0f && a != 0f && Mathf.Sign(prev) != Mathf.Sign(a)) changes++;
                if (a != 0f) prev = a;
            }
            if (changes < 1)
                return AutoDevCheckResult.Fail("Aci isaret degistirmedi.");

            // Hasar sayisi + cooldown
            dummyObj = new GameObject("T022_Dummy");
            DummyDamageable dummy = dummyObj.AddComponent<DummyDamageable>();
            int fired = 0;
            hz.OnPlayerHit += _ => fired++;
            hz.Step(hz.HitCooldown + 0.1f);
            if (!hz.HitTarget(dummyObj) || dummy.HitCount != 1 || dummy.CurrentHealth != 100 - hz.Damage)
                return AutoDevCheckResult.Fail($"Ilk isabet hasari hatali. Hit={dummy.HitCount}");
            if (hz.HitTarget(dummyObj) || dummy.HitCount != 1)
                return AutoDevCheckResult.Fail("Cooldown icinde ikinci hasar verildi.");
            hz.Step(hz.HitCooldown + 0.05f);
            if (!hz.HitTarget(dummyObj) || dummy.HitCount != 2)
                return AutoDevCheckResult.Fail("Cooldown sonrasi hasar verilmedi.");
            if (fired != 2) return AutoDevCheckResult.Fail($"OnPlayerHit sayisi hatali: {fired}");

            // PlayerHealth + knockback
            playerObj = new GameObject("T022_Player");
            playerObj.transform.position = new Vector3(0f, 990f, 0f);
            playerObj.AddComponent<CapsuleCollider>();
            Rigidbody rb = playerObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            PlayerHealth ph = playerObj.AddComponent<PlayerHealth>();
            AutoDevVerifier.InvokeLifecycle(ph, "Awake");
            hz.Step(hz.HitCooldown + 0.05f);
            int before = ph.CurrentHealth;
            if (!hz.HitTarget(playerObj.GetComponent<Collider>()))
                return AutoDevCheckResult.Fail("PlayerHealth hedefine HitTarget basarisiz.");
            if (ph.CurrentHealth != before - hz.Damage)
                return AutoDevCheckResult.Fail($"PlayerHealth hasari hatali: {before}->{ph.CurrentHealth}");
            Vector3 v = rb.linearVelocity;
            float horiz = new Vector2(v.x, v.z).magnitude;
            if (horiz < hz.KnockbackForce * 0.8f || v.y <= 0f)
                return AutoDevCheckResult.Fail($"Knockback hizi hatali: yatay={horiz}, dikey={v.y}");
            if (hz.SfxPlayCount < 1)
                return AutoDevCheckResult.Fail("SFX kancasi cagrilmadi.");

            return AutoDevCheckResult.Pass("SwingingHazard salinim, hasar, cooldown ve knockback dogrulandi.");
        }
        finally
        {
            if (hazardObj != null) Object.DestroyImmediate(hazardObj);
            if (dummyObj != null) Object.DestroyImmediate(dummyObj);
            if (playerObj != null) Object.DestroyImmediate(playerObj);
        }
    }
}
