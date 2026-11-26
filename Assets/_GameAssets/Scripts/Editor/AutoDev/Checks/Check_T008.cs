using UnityEditor;
using UnityEngine;

public static class Check_T008
{
    [AutoDevCheck("T008")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        // 1. Demo.unity sahnesindeki karaktere PlayerHealth bilesenini ve VFX yapisini bagla
        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
            return AutoDevCheckResult.Fail("Demo.unity sahnesinde PlayerController bulunamadi.");

        bool saveScene = false;
        PlayerHealth sceneHealth = scenePlayer.GetComponent<PlayerHealth>();
        if (sceneHealth == null)
        {
            sceneHealth = Undo.AddComponent<PlayerHealth>(scenePlayer.gameObject);
            saveScene = true;
        }

        sceneHealth.ResolveDependencies();
        if (sceneHealth.EnsureVFXSetup())
            saveScene = true;

        if (sceneHealth.HurtParticles == null || sceneHealth.HealParticles == null)
            return AutoDevCheckResult.Fail("Sahne karakterinde HurtParticles veya HealParticles ParticleSystem bilesenleri eksik.");

        if (saveScene)
        {
            EditorUtility.SetDirty(sceneHealth);
            EditorUtility.SetDirty(scenePlayer.gameObject);
        }

        // 2. Izole test rig ile davranissal dogrulama (Hasar, i-frame engeli, iyilesme ve olumde GameOver)
        bool createdTempGM = false;
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            GameObject gmObj = new GameObject("Temp_GameManager");
            gameManager = gmObj.AddComponent<GameManager>();
            createdTempGM = true;
        }
        GameState originalGameState = gameManager.CurrentState;

        GameObject testPlayerObj = null;
        try
        {
            testPlayerObj = new GameObject("T008_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 950f, 0f);

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
            PlayerHealth health = testPlayerObj.AddComponent<PlayerHealth>();

            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(health, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Start");
            AutoDevVerifier.InvokeLifecycle(health, "Start");

            // Baslangic deger kontrolu
            if (health.CurrentHealth != 100 || health.MaxHealth != 100 || !health.IsAlive || health.IsInvincible)
                return AutoDevCheckResult.Fail($"Baslangic can degerleri hatali. Curr={health.CurrentHealth}, Max={health.MaxHealth}, Alive={health.IsAlive}, Inv={health.IsInvincible}");

            int healthChangedCount = 0;
            int lastReportedHealth = 0;
            health.OnHealthChanged += (curr, max) =>
            {
                healthChangedCount++;
                lastReportedHealth = curr;
            };

            bool diedEventFired = false;
            health.OnDied += () => { diedEventFired = true; };

            Quaternion baseRot = visualObj.transform.localRotation;
            Vector3 baseScale = visualObj.transform.localScale;

            // A: Ilk hasar ve dokunulmazlik (i-frame) baslangici
            health.TakeDamage(30, Vector3.back);
            health.StepFeedback(0.08f);

            if (health.CurrentHealth != 70)
                return AutoDevCheckResult.Fail($"TakeDamage(30) sonrasi can 70 olmaliydi. Mevcut: {health.CurrentHealth}");
            if (healthChangedCount != 1 || lastReportedHealth != 70)
                return AutoDevCheckResult.Fail($"OnHealthChanged olayi tetiklenmedi veya deger yanlis. Count={healthChangedCount}, Val={lastReportedHealth}");
            if (!health.IsInvincible)
                return AutoDevCheckResult.Fail("Hasar sonrasi IsInvincible true olmaliydi.");
            if (!health.HurtParticles.isPlaying)
                return AutoDevCheckResult.Fail("Hasar sonrasi HurtParticles oynatilmalidir.");

            float hurtAngle = Quaternion.Angle(visualObj.transform.localRotation, baseRot);
            float hurtScaleDelta = Vector3.Distance(visualObj.transform.localScale, baseScale);
            if (hurtAngle < 2f && hurtScaleDelta < 0.02f)
                return AutoDevCheckResult.Fail($"Hasar ani gorsel tepki/egim/basiklasma yetersiz! angle={hurtAngle:F1}, scaleDelta={hurtScaleDelta:F3}");

            // B: Dokunulmazlik suresi aktifken gelen ikinci TakeDamage cagrisi can azaltmamali
            health.TakeDamage(25);
            if (health.CurrentHealth != 70)
                return AutoDevCheckResult.Fail($"Dokunulmazlik suresinde hasar engellenmedi! Can 70 kalmaliydi, mevcut: {health.CurrentHealth}");
            if (healthChangedCount != 1)
                return AutoDevCheckResult.Fail("Dokunulmazlik sirasinda OnHealthChanged tekrar tetiklenmemeliydi.");

            // C: Dokunulmazlik suresini tamamla ve iyilesmeyi (Heal) dogrula
            for (int i = 0; i < 25; i++) health.StepFeedback(0.06f);
            if (health.IsInvincible)
                return AutoDevCheckResult.Fail("Dokunulmazlik suresi bittikten sonra IsInvincible false olmaliydi.");

            health.Heal(20);
            if (health.CurrentHealth != 90 || healthChangedCount != 2 || lastReportedHealth != 90)
                return AutoDevCheckResult.Fail($"Heal(20) sonrasi can 90 olmaliydi. Mevcut: {health.CurrentHealth}, Events={healthChangedCount}");

            health.Heal(50); // Maksimum can siniri testi
            if (health.CurrentHealth != 100)
                return AutoDevCheckResult.Fail($"Heal(50) sonrasi can MaxHealth (100) asilmamaliydi. Mevcut: {health.CurrentHealth}");

            // D: Olumcul hasar ve GameOver gecisi
            if (gameManager != null)
            {
                gameManager.SetState(GameState.Playing);
            }

            for (int i = 0; i < 25; i++) health.StepFeedback(0.06f);
            health.TakeDamage(100);

            if (health.CurrentHealth != 0 || health.IsAlive)
                return AutoDevCheckResult.Fail($"Olum sonrasi CurrentHealth=0 ve IsAlive=false olmaliydi. Curr={health.CurrentHealth}, Alive={health.IsAlive}");
            if (!diedEventFired)
                return AutoDevCheckResult.Fail("Can 0 oldugunda OnDied olayi tetiklenmedi.");

            if (gameManager != null && gameManager.CurrentState != GameState.GameOver)
                return AutoDevCheckResult.Fail($"Oyuncu olunce GameManager.CurrentState GameOver olmaliydi. Mevcut: {gameManager.CurrentState}");

            return AutoDevCheckResult.Pass(
                "T008: IDamageable, PlayerHealth can sistemi, i-frame engeli, geri bildirim VFX ve GameOver gecisi basariyla dogrulandi.",
                $"HurtAngle={hurtAngle:F1}, ScaleDelta={hurtScaleDelta:F3}, HealthEvents={healthChangedCount}",
                saveScene: saveScene
            );
        }
        finally
        {
            if (createdTempGM && gameManager != null)
            {
                Object.DestroyImmediate(gameManager.gameObject);
            }
            else if (gameManager != null)
            {
                gameManager.SetState(originalGameState);
            }
            if (testPlayerObj != null)
            {
                Object.DestroyImmediate(testPlayerObj);
            }
        }
    }
}
