using UnityEditor;
using UnityEngine;

public static class Check_T014
{
    [AutoDevCheck("T014")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. DefaultEnemyStats.asset ScriptableObject varligini kontrol et / idempotent olustur
        EnemyStatsSO stats = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(EnemyStatsSO.DefaultAssetPath);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<EnemyStatsSO>();
            if (!AssetDatabase.IsValidFolder("Assets/_GameAssets/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
            }
            AssetDatabase.CreateAsset(stats, EnemyStatsSO.DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }

        // 2. Demo.unity sahnesinde ornek PatrolEnemyAI nesnesini kur
        PatrolEnemyAI[] sceneEnemies = Object.FindObjectsByType<PatrolEnemyAI>(FindObjectsInactive.Include);
        if (sceneEnemies == null || sceneEnemies.Length == 0)
        {
            GameObject enemyGroup = new GameObject("PatrolEnemies");
            Undo.RegisterCreatedObjectUndo(enemyGroup, "Create PatrolEnemies");
            saveScene = true;

            GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObj.name = "PatrolEnemy";
            enemyObj.transform.SetParent(enemyGroup.transform, false);
            enemyObj.transform.position = new Vector3(6.5f, 1f, 6.5f);

            GameObject visualObj = new GameObject("VisualModel");
            visualObj.transform.SetParent(enemyObj.transform, false);

            MeshFilter mf = enemyObj.GetComponent<MeshFilter>();
            MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();
            if (mf != null && mr != null)
            {
                MeshFilter vmf = visualObj.AddComponent<MeshFilter>();
                vmf.sharedMesh = mf.sharedMesh;
                MeshRenderer vmr = visualObj.AddComponent<MeshRenderer>();
                vmr.sharedMaterials = mr.sharedMaterials;
                Object.DestroyImmediate(mr);
                Object.DestroyImmediate(mf);
            }

            Rigidbody rb = enemyObj.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            GameObject wp1 = new GameObject("Waypoint_1");
            wp1.transform.SetParent(enemyGroup.transform, false);
            wp1.transform.position = new Vector3(6.5f, 0.5f, 6.5f);

            GameObject wp2 = new GameObject("Waypoint_2");
            wp2.transform.SetParent(enemyGroup.transform, false);
            wp2.transform.position = new Vector3(10.5f, 0.5f, 6.5f);

            PatrolEnemyAI enemyComp = enemyObj.AddComponent<PatrolEnemyAI>();
            enemyComp.Stats = stats;
            enemyComp.Waypoints = new Transform[] { wp1.transform, wp2.transform };
            enemyComp.EnsureVFXSetup();

            EditorUtility.SetDirty(enemyObj);
        }
        else
        {
            for (int i = 0; i < sceneEnemies.Length; i++)
            {
                if (sceneEnemies[i].Stats == null)
                {
                    sceneEnemies[i].Stats = stats;
                    saveScene = true;
                }
                if (sceneEnemies[i].EnsureVFXSetup())
                {
                    saveScene = true;
                }
            }
        }

        // 3. Davranissal Dogrulama (PatrolEnemyAI ve EnemyStatsSO mekanikleri)
        GameObject testEnemyObj = null;
        GameObject testPlayerObj = null;
        GameObject wpObjA = null;
        GameObject wpObjB = null;

        try
        {
            // A: EnemyStatsSO deger kontrolu
            if (stats.PatrolSpeed <= 0f || stats.ChaseSpeed <= 0f || stats.DetectionRange <= 0f ||
                stats.MaxHealth <= 0 || stats.ContactDamage <= 0 || stats.BounceForce <= 0f)
            {
                return AutoDevCheckResult.Fail("EnemyStatsSO baslangic degerleri gecersiz veya pozitif degil.");
            }

            // B: Izole test nesnelerinin kurulumu
            testEnemyObj = new GameObject("T014_TestEnemy");
            testEnemyObj.transform.position = new Vector3(0f, 960f, 0f);

            GameObject enemyVisual = new GameObject("VisualModel");
            enemyVisual.transform.SetParent(testEnemyObj.transform, false);

            CapsuleCollider enemyCol = testEnemyObj.AddComponent<CapsuleCollider>();
            enemyCol.height = 2f;
            enemyCol.radius = 0.5f;

            Rigidbody enemyRb = testEnemyObj.AddComponent<Rigidbody>();
            enemyRb.mass = 2f;

            PatrolEnemyAI enemy = testEnemyObj.AddComponent<PatrolEnemyAI>();
            enemy.Stats = stats;

            wpObjA = new GameObject("T014_WP_A");
            wpObjA.transform.position = new Vector3(0f, 960f, 0f);
            wpObjB = new GameObject("T014_WP_B");
            wpObjB.transform.position = new Vector3(0f, 960f, 10f);
            enemy.Waypoints = new Transform[] { wpObjA.transform, wpObjB.transform };

            testPlayerObj = new GameObject("T014_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 960f, 30f); // Baslangicta menzil disi
            CapsuleCollider pCol = testPlayerObj.AddComponent<CapsuleCollider>();
            pCol.height = 2f;
            pCol.radius = 0.4f;
            Rigidbody pRb = testPlayerObj.AddComponent<Rigidbody>();
            pRb.mass = 1f;

            testPlayerObj.AddComponent<StateController>();
            PlayerController player = testPlayerObj.AddComponent<PlayerController>();
            PlayerHealth playerHealth = testPlayerObj.AddComponent<PlayerHealth>();

            AutoDevVerifier.InvokeLifecycle(enemy, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(playerHealth, "Awake");

            enemy.TargetPlayer = player;

            // C: Devriye hareketi dogrulamasi
            if (enemy.CurrentState != PatrolEnemyAI.EnemyState.Patrol)
                return AutoDevCheckResult.Fail("Dusman baslangicta Patrol durumunda olmaliydi.");

            Vector3 startPos = testEnemyObj.transform.position;
            enemy.Step(0.2f);
            if (testEnemyObj.transform.position.z <= startPos.z && enemyRb.linearVelocity.z <= 0.01f)
                return AutoDevCheckResult.Fail("Devriye durumunda hedef waypoint yonunde hareket olusmadi.");

            // D: Oyuncu yaklasma ve Chase durumuna gecis
            testPlayerObj.transform.position = testEnemyObj.transform.position + Vector3.forward * 4f; // Algilama menzili ici
            enemy.Step(0.05f);
            if (enemy.CurrentState != PatrolEnemyAI.EnemyState.Chase)
                return AutoDevCheckResult.Fail("Oyuncu algilama menziline girdiginde Chase durumuna gecilmedi.");

            // E: Yandan temas ile oyuncuya hasar verme
            int initialPlayerHp = playerHealth.CurrentHealth;
            bool hitHandled = enemy.HandlePlayerContact(player, isStompFromAbove: false);
            if (!hitHandled || playerHealth.CurrentHealth >= initialPlayerHp)
                return AutoDevCheckResult.Fail($"Yandan temasta oyuncuya hasar iletilmedi. Onceki={initialPlayerHp}, Mevcut={playerHealth.CurrentHealth}");

            // F: Ustten ziplama / Stomp ile ezilme ve oyuncunun yukari firlatilmasi
            bool defeatedEventFired = false;
            enemy.OnDefeated += (e) => { defeatedEventFired = true; };

            int hpBeforePartialHit = enemy.CurrentHealth;
            enemy.TakeDamage(5, Vector3.forward);
            if (enemy.CurrentHealth >= hpBeforePartialHit)
                return AutoDevCheckResult.Fail($"IDamageable.TakeDamage dusmana hasar vermedi. Onceki={hpBeforePartialHit}, Mevcut={enemy.CurrentHealth}");

            int enemyHpBeforeStomp = enemy.CurrentHealth;
            pRb.linearVelocity = Vector3.zero;
            bool stompHandled = enemy.HandlePlayerContact(player, isStompFromAbove: true);
            if (!stompHandled || enemy.CurrentHealth >= enemyHpBeforeStomp)
                return AutoDevCheckResult.Fail($"Ustten ziplamada dusman hasar almadi. Onceki={enemyHpBeforeStomp}, Mevcut={enemy.CurrentHealth}");
            if (pRb.linearVelocity.y <= 0f)
                return AutoDevCheckResult.Fail("Ustten ziplama sonrasi oyuncuya yukari ziplatma kuvveti uygulanmadi.");

            // G: IDamageable ile tam hasar alma ve Defeated durumuna gecis
            if (enemy.IsAlive)
            {
                enemy.TakeDamage(100, Vector3.forward);
            }

            if (enemy.CurrentState != PatrolEnemyAI.EnemyState.Defeated || enemy.IsAlive || enemy.CurrentHealth > 0)
                return AutoDevCheckResult.Fail("Dusman canı sifirlandiginda Defeated durumuna gecmedi veya IsAlive false olmadi.");
            if (!defeatedEventFired)
                return AutoDevCheckResult.Fail("Dusman yenildiginde OnDefeated olayi tetiklenmedi.");

            enemy.StepFeedback(0.05f);
            if (enemyCol.enabled)
                return AutoDevCheckResult.Fail("Defeated durumunda collider devre disi birakilmaliydi.");

            return AutoDevCheckResult.Pass("PatrolEnemyAI devriye hareketi, Chase gecisi, yandan temas hasari, ustten ezilme ve Defeated mekanikleri basariyla dogrulandi.", saveScene: saveScene);
        }
        finally
        {
            if (testEnemyObj != null) Object.DestroyImmediate(testEnemyObj);
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
            if (wpObjA != null) Object.DestroyImmediate(wpObjA);
            if (wpObjB != null) Object.DestroyImmediate(wpObjB);
        }
    }
}
