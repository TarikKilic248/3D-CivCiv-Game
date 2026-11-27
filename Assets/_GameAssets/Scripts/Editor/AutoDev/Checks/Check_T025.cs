using UnityEditor;
using UnityEngine;

/// <summary>
/// CivCiv 3D - T025 Dogrulama Kontrolu
/// Yumurta Firlatma Yetenegi (EggProjectile & ChickEggThrower)
/// TryThrowEgg ile mermi olusumu, parabolik yay hareketi, cooldown ve cephane/yenilenme,
/// IDamageable hasar ve yonlu geri tepme ile OnEggHit olayinin davranissal testi.
/// </summary>
public static class Check_T025
{
    private class DummyDamageable : MonoBehaviour, IDamageable
    {
        public int CurrentHealth { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public bool IsAlive => CurrentHealth > 0;
        public bool IsInvincible => false;
        public int HitCount { get; private set; }
        public Vector3 LastHitDirection { get; private set; }

        public void TakeDamage(int damage) => TakeDamage(damage, Vector3.zero);
        public void TakeDamage(int damage, Vector3 dir)
        {
            CurrentHealth -= damage;
            HitCount++;
            LastHitDirection = dir;
        }

        public void Heal(int amount) => CurrentHealth += amount;
    }

    [AutoDevCheck("T025")]
    [AutoDevCheck("TryThrowEgg ile EggProjectile olustugunu, cooldown kontrolunun calistigini ve IDamageable hedefine carpinca hasar ile OnEggHit olayinin tetiklendigini dogrula.")]
    [AutoDevCheck("Check_TryThrowEgg ile EggProjectile olustugunu, cooldown kontrolunun calistigini ve IDamageable hedefine carpinca hasar ile OnEggHit olayinin tetiklendigini dogrula.")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Demo.unity sahnesindeki Player nesnesine ChickEggThrower bilesenini idempotent bagla
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            ChickEggThrower sceneThrower = player.GetComponent<ChickEggThrower>();
            if (sceneThrower == null)
            {
                sceneThrower = Undo.AddComponent<ChickEggThrower>(player.gameObject);
                sceneThrower.EnsureVFXSetup();
                EditorUtility.SetDirty(player.gameObject);
                saveScene = true;
            }
            else
            {
                if (sceneThrower.EnsureVFXSetup())
                {
                    EditorUtility.SetDirty(sceneThrower.gameObject);
                    saveScene = true;
                }
            }
        }

        // 2. Izole test ortaminda davranissal dogrulama
        GameObject throwerObj = null;
        GameObject dummyObj = null;
        EggProjectile spawnedEgg = null;

        try
        {
            throwerObj = new GameObject("T025_TestThrower");
            throwerObj.transform.position = new Vector3(0f, 950f, 0f);
            throwerObj.transform.rotation = Quaternion.identity;
            ChickEggThrower thrower = throwerObj.AddComponent<ChickEggThrower>();
            AutoDevVerifier.InvokeLifecycle(thrower, "Awake");

            if (thrower.ThrowMuzzleVFX == null || thrower.ThrowOrigin == null)
            {
                return AutoDevCheckResult.Fail("ChickEggThrower MuzzleVFX veya ThrowOrigin kurulumu eksik.");
            }

            // Test 1: TryThrowEgg ile EggProjectile olusumu ve yay hareketi hizi
            int initialAmmo = thrower.CurrentAmmo;
            spawnedEgg = thrower.TryThrowEgg();
            if (spawnedEgg == null)
            {
                return AutoDevCheckResult.Fail("TryThrowEgg() cagrisi null EggProjectile dondurdu.");
            }

            if (thrower.ThrownCount != 1)
            {
                return AutoDevCheckResult.Fail($"ThrownCount beklenen 1 degil: {thrower.ThrownCount}");
            }

            if (thrower.CurrentAmmo != initialAmmo - 1)
            {
                return AutoDevCheckResult.Fail($"Yumurta firlatma sonrasi cephane dusmedi: {thrower.CurrentAmmo} != {initialAmmo - 1}");
            }

            Vector3 vel = spawnedEgg.Rigidbody != null ? spawnedEgg.Rigidbody.linearVelocity : Vector3.zero;
            if (vel.z <= 0.1f || vel.y <= 0.1f)
            {
                return AutoDevCheckResult.Fail($"EggProjectile ileri ve yukari yay hizina sahip degil. Vel={vel}");
            }

            // Test 2: Cooldown kontrolu (bekleme suresince tekrar atilamaz)
            if (thrower.CanThrow)
            {
                return AutoDevCheckResult.Fail("Firlatmadan hemen sonra CanThrow true kaldi (cooldown calismadi).");
            }

            EggProjectile blockedEgg = thrower.TryThrowEgg();
            if (blockedEgg != null)
            {
                return AutoDevCheckResult.Fail("Cooldown suresince ikinci firlatma engellenmedi.");
            }

            if (thrower.ThrownCount != 1)
            {
                return AutoDevCheckResult.Fail("Cooldown sirasinda ThrownCount artti.");
            }

            // Test 3: Cooldown suresinin tamamlanmasi
            thrower.Step(thrower.Cooldown + 0.05f);
            if (!thrower.CanThrow)
            {
                return AutoDevCheckResult.Fail("Cooldown suresi tamamlandiktan sonra CanThrow true olmadi.");
            }

            // Test 4: Cephane otomatik yenilenmesi (Recharge)
            int ammoBeforeRecharge = thrower.CurrentAmmo;
            thrower.Step(thrower.RechargeDelay + 0.1f);
            if (thrower.CurrentAmmo <= ammoBeforeRecharge)
            {
                return AutoDevCheckResult.Fail($"RechargeDelay sonrasi cephane yenilenmedi: {thrower.CurrentAmmo} <= {ammoBeforeRecharge}");
            }

            // Test 5: EggProjectile VFX ve Trail kurulumu
            if (spawnedEgg.Trail == null || spawnedEgg.BreakVFX == null || spawnedEgg.VisualRoot == null)
            {
                return AutoDevCheckResult.Fail("EggProjectile TrailRenderer, BreakVFX veya VisualRoot kurulumu eksik.");
            }

            // Test 6: IDamageable hedef carpisma, hasar iletimi ve OnEggHit olayi
            dummyObj = new GameObject("T025_DummyTarget");
            dummyObj.transform.position = new Vector3(0f, 950f, 5f);
            SphereCollider dummyCol = dummyObj.AddComponent<SphereCollider>();
            dummyCol.radius = 0.5f;
            Rigidbody dummyRb = dummyObj.AddComponent<Rigidbody>();
            dummyRb.useGravity = false;
            dummyRb.linearVelocity = Vector3.zero;

            DummyDamageable dummy = dummyObj.AddComponent<DummyDamageable>();

            bool eventTriggered = false;
            IDamageable hitTargetReported = null;
            spawnedEgg.OnEggHit += (egg, col, target) =>
            {
                eventTriggered = true;
                hitTargetReported = target;
            };

            int initialHealth = dummy.CurrentHealth;
            int eggDamage = spawnedEgg.Damage;

            bool hitSuccess = spawnedEgg.HitTarget(dummyCol);
            if (!hitSuccess)
            {
                return AutoDevCheckResult.Fail("spawnedEgg.HitTarget() basarisiz oldu.");
            }

            if (dummy.HitCount != 1)
            {
                return AutoDevCheckResult.Fail($"Dummy hedef TakeDamage almadi. HitCount={dummy.HitCount}");
            }

            if (dummy.CurrentHealth != initialHealth - eggDamage)
            {
                return AutoDevCheckResult.Fail($"Dummy hasar miktari hatali: {dummy.CurrentHealth} != {initialHealth - eggDamage}");
            }

            if (!eventTriggered || hitTargetReported != dummy)
            {
                return AutoDevCheckResult.Fail("OnEggHit olayi tetiklenmedi veya IDamageable hedef referansi eslesmedi.");
            }

            if (!spawnedEgg.HasHit)
            {
                return AutoDevCheckResult.Fail("Carpisma sonrasi EggProjectile.HasHit true olmadi.");
            }

            if (spawnedEgg.SfxPlayCount < 1)
            {
                return AutoDevCheckResult.Fail("Kabuk kirilma SFX kancasi calismadi.");
            }

            return AutoDevCheckResult.Pass(
                $"Yumurta firlatma yetenegi basariyla dogrulandi. Damage={eggDamage}, Cooldown={thrower.Cooldown:F2}s, Ammo={thrower.MaxAmmo}",
                "",
                saveScene);
        }
        finally
        {
            if (spawnedEgg != null && spawnedEgg.gameObject != null)
            {
                Object.DestroyImmediate(spawnedEgg.gameObject);
            }
            if (dummyObj != null) Object.DestroyImmediate(dummyObj);
            if (throwerObj != null) Object.DestroyImmediate(throwerObj);
        }
    }
}
