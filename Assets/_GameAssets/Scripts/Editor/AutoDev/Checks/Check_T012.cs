using UnityEditor;
using UnityEngine;

public static class Check_T012
{
    private class DummyDamageable : MonoBehaviour, IDamageable
    {
        public int CurrentHealth { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public bool IsAlive => CurrentHealth > 0;
        public bool IsInvincible => false;
        public int HitCount { get; private set; }
        public Vector3 LastHitDir { get; private set; }

        public void TakeDamage(int damage) => TakeDamage(damage, Vector3.zero);
        public void TakeDamage(int damage, Vector3 hitDirection)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            HitCount++;
            LastHitDir = hitDirection;
        }
        public void Heal(int amount) => CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    [AutoDevCheck("T012")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
            return AutoDevCheckResult.Fail("Demo.unity sahnesinde PlayerController bulunamadi.");

        bool saveScene = false;
        ChickPeckAttack sceneAttack = scenePlayer.GetComponent<ChickPeckAttack>();
        if (sceneAttack == null)
        {
            sceneAttack = Undo.AddComponent<ChickPeckAttack>(scenePlayer.gameObject);
            saveScene = true;
        }

        sceneAttack.ResolveDependencies();
        if (sceneAttack.EnsureVFXSetup())
            saveScene = true;

        if (sceneAttack.PeckVFX == null || sceneAttack.GroundPoundImpactVFX == null)
            return AutoDevCheckResult.Fail("Sahne karakterinde PeckVFX veya GroundPoundImpactVFX eksik.");

        if (saveScene)
        {
            EditorUtility.SetDirty(sceneAttack);
            EditorUtility.SetDirty(scenePlayer.gameObject);
        }

        GameObject testPlayerObj = null;
        GameObject dummyObj = null;
        try
        {
            testPlayerObj = new GameObject("T012_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 960f, 0f);

            GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualObj.name = "VisualModel";
            visualObj.transform.SetParent(testPlayerObj.transform, false);
            Object.DestroyImmediate(visualObj.GetComponent<Collider>());

            CapsuleCollider col = testPlayerObj.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;

            Rigidbody rb = testPlayerObj.AddComponent<Rigidbody>();
            rb.mass = 1f;

            testPlayerObj.AddComponent<StateController>();
            PlayerController player = testPlayerObj.AddComponent<PlayerController>();
            ChickPeckAttack attack = testPlayerObj.AddComponent<ChickPeckAttack>();

            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(attack, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Start");
            AutoDevVerifier.InvokeLifecycle(attack, "Start");

            dummyObj = new GameObject("T012_DummyTarget");
            dummyObj.transform.position = testPlayerObj.transform.position + testPlayerObj.transform.forward * 1.0f;
            BoxCollider dummyCol = dummyObj.AddComponent<BoxCollider>();
            dummyCol.size = Vector3.one;
            DummyDamageable dummy = dummyObj.AddComponent<DummyDamageable>();

            Physics.SyncTransforms();

            // A: PerformPeck() dogrulamasi
            if (!attack.CanPeck)
                return AutoDevCheckResult.Fail("PerformPeck baslangicta hazir (CanPeck=true) olmaliydi.");

            Quaternion baseRot = visualObj.transform.localRotation;
            Vector3 baseScale = visualObj.transform.localScale;

            if (!attack.PerformPeck())
                return AutoDevCheckResult.Fail("PerformPeck() cagrisi basarisiz oldu.");

            attack.StepFeedback(0.06f);
            float peckAngleDelta = Quaternion.Angle(visualObj.transform.localRotation, baseRot);
            float peckScaleDelta = Vector3.Distance(visualObj.transform.localScale, baseScale);

            if (dummy.CurrentHealth != 100 - attack.PeckDamage || dummy.HitCount != 1)
                return AutoDevCheckResult.Fail($"Gaga vurusu sahte hedefe hasar iletmedi. Can={dummy.CurrentHealth}, Hit={dummy.HitCount}");
            if (attack.PeckCooldownTimer <= 0f || attack.CanPeck)
                return AutoDevCheckResult.Fail("PerformPeck sonrasi cooldown uygulanmadi.");
            if (attack.PeckExecutionCount != 1 || attack.PeckHitCount != 1)
                return AutoDevCheckResult.Fail($"Peck sayaclari hatali. Exec={attack.PeckExecutionCount}, Hit={attack.PeckHitCount}");
            if (peckAngleDelta < 1f && peckScaleDelta < 0.02f)
                return AutoDevCheckResult.Fail("PerformPeck prosedurel hareket/egim olusmadi.");

            // Cooldown temizleme
            attack.Step(attack.PeckCooldown + 0.1f);
            if (!attack.CanPeck)
                return AutoDevCheckResult.Fail("Sure dolunca CanPeck true olmaliydi.");

            // B: Havadan Yere Cakilma (StartGroundPound & OnGroundPoundImpact)
            dummyObj.transform.position = testPlayerObj.transform.position + new Vector3(0.5f, -0.5f, 0.5f);
            Physics.SyncTransforms();

            int healthBeforePound = dummy.CurrentHealth;
            if (!attack.StartGroundPound())
                return AutoDevCheckResult.Fail("Havadayken StartGroundPound() basarisiz oldu.");

            if (!attack.IsGroundPounding)
                return AutoDevCheckResult.Fail("StartGroundPound sonrasi IsGroundPounding true olmaliydi.");
            if (rb.linearVelocity.y > -15f)
                return AutoDevCheckResult.Fail($"Guclu negatif dikey hiz uygulanmadi. Y Hiz={rb.linearVelocity.y:F1}");

            attack.OnGroundPoundImpact();

            if (attack.IsGroundPounding)
                return AutoDevCheckResult.Fail("Darbe sonrasi IsGroundPounding false olmaliydi.");
            if (dummy.CurrentHealth != healthBeforePound - attack.GroundPoundDamage)
                return AutoDevCheckResult.Fail($"Yere cakilma darbesi alan hasari iletmedi. Beklenen={healthBeforePound - attack.GroundPoundDamage}, Mevcut={dummy.CurrentHealth}");
            if (attack.GroundPoundCooldownTimer <= 0f)
                return AutoDevCheckResult.Fail("GroundPound sonrasi cooldown uygulanmadi.");

            return AutoDevCheckResult.Pass("ChickPeckAttack gaga vurusu ve havadan yere cakilma mekanikleri basariyla dogrulandi.", saveScene: saveScene);
        }
        finally
        {
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
            if (dummyObj != null) Object.DestroyImmediate(dummyObj);
        }
    }
}
