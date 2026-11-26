using UnityEditor;
using UnityEngine;

public static class Check_T013
{
    [AutoDevCheck("T013")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. DefaultLootTable.asset ScriptableObject varligini kontrol et / idempotent olustur
        LootTableSO lootTable = AssetDatabase.LoadAssetAtPath<LootTableSO>(LootTableSO.DefaultAssetPath);
        if (lootTable == null)
        {
            lootTable = ScriptableObject.CreateInstance<LootTableSO>();

            GameObject coinPrefab = null;
            string[] coinGuids = AssetDatabase.FindAssets("Coin t:Prefab");
            for (int i = 0; i < coinGuids.Length; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(coinGuids[i]));
                if (p != null && p.GetComponent<CoinCollect>() != null) { coinPrefab = p; break; }
            }

            GameObject powerUpPrefab = null;
            string[] puGuids = AssetDatabase.FindAssets("PowerUp t:Prefab");
            for (int i = 0; i < puGuids.Length; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(puGuids[i]));
                if (p != null && p.GetComponent<PowerUpCollectable>() != null) { powerUpPrefab = p; break; }
            }

            lootTable.AddEntry(new LootTableSO.LootEntry(coinPrefab, 70f, 1, 3, directCoinAmount: 5));
            lootTable.AddEntry(new LootTableSO.LootEntry(powerUpPrefab, 30f, 1, 1, directCoinAmount: 10));

            if (!AssetDatabase.IsValidFolder("Assets/_GameAssets/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
            }
            AssetDatabase.CreateAsset(lootTable, LootTableSO.DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }

        // 2. Demo.unity sahnesinde ornek BreakableCrate nesnelerini kur
        BreakableCrate[] sceneCrates = Object.FindObjectsByType<BreakableCrate>(FindObjectsInactive.Include);
        if (sceneCrates == null || sceneCrates.Length == 0)
        {
            GameObject crateRoot = new GameObject("BreakableCrates");
            Undo.RegisterCreatedObjectUndo(crateRoot, "Create BreakableCrates");
            saveScene = true;

            Vector3[] spawnPositions = new Vector3[]
            {
                new Vector3(4.0f, 0.5f, 2.5f),
                new Vector3(1.85f, 0.5f, 5.5f)
            };

            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject crateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crateObj.name = $"BreakableCrate_{i + 1}";
                crateObj.transform.SetParent(crateRoot.transform, false);
                crateObj.transform.position = spawnPositions[i];
                crateObj.transform.localScale = Vector3.one * 0.9f;

                BreakableCrate crateComp = crateObj.AddComponent<BreakableCrate>();
                crateComp.LootTable = lootTable;
                crateComp.EnsureVFXSetup();
                EditorUtility.SetDirty(crateObj);
            }
        }
        else
        {
            for (int i = 0; i < sceneCrates.Length; i++)
            {
                if (sceneCrates[i].LootTable == null)
                {
                    sceneCrates[i].LootTable = lootTable;
                    saveScene = true;
                }
                if (sceneCrates[i].EnsureVFXSetup())
                {
                    saveScene = true;
                }
            }
        }

        // 3. Davranissal Dogrulama (LootTableSO ve BreakableCrate mekanikleri)
        GameObject testPlayerObj = null;
        GameObject testCrateObj = null;
        GameObject dashCrateObj = null;
        try
        {
            // A: LootTableSO agirlikli secim ve guvenlik dogrulamasi
            LootTableSO emptyTable = ScriptableObject.CreateInstance<LootTableSO>();
            if (emptyTable.RollLoot() != null)
                return AutoDevCheckResult.Fail("Bos LootTableSO.RollLoot() null donmeliydi.");

            emptyTable.AddEntry(new LootTableSO.LootEntry(null, 0f));
            if (emptyTable.RollLoot() != null)
                return AutoDevCheckResult.Fail("Agirliklari 0 olan LootTableSO null donmeliydi.");

            emptyTable.ClearEntries();
            emptyTable.AddEntry(new LootTableSO.LootEntry(null, 60f, 2, 4, directCoinAmount: 5));
            var roll = emptyTable.RollLoot();
            if (roll == null || roll.directCoinAmount != 5 || roll.RollCount() < 2 || roll.RollCount() > 4)
                return AutoDevCheckResult.Fail("LootTableSO agirlikli girdi secimi veya RollCount araligi hatali.");

            // B: BreakableCrate can, gaga vurusu hasari ve ganimet uretimi
            testPlayerObj = new GameObject("T013_TestPlayer");
            testPlayerObj.transform.position = new Vector3(0f, 960f, 0f);

            CapsuleCollider pCol = testPlayerObj.AddComponent<CapsuleCollider>();
            pCol.height = 2f;
            pCol.radius = 0.4f;

            Rigidbody pRb = testPlayerObj.AddComponent<Rigidbody>();
            pRb.mass = 1f;

            testPlayerObj.AddComponent<StateController>();
            PlayerController player = testPlayerObj.AddComponent<PlayerController>();
            DashController dash = testPlayerObj.AddComponent<DashController>();
            ChickPeckAttack attack = testPlayerObj.AddComponent<ChickPeckAttack>();

            AutoDevVerifier.InvokeLifecycle(player, "Awake");
            AutoDevVerifier.InvokeLifecycle(dash, "Awake");
            AutoDevVerifier.InvokeLifecycle(attack, "Awake");
            AutoDevVerifier.InvokeLifecycle(player, "Start");
            AutoDevVerifier.InvokeLifecycle(attack, "Start");

            testCrateObj = new GameObject("T013_TestCrate");
            testCrateObj.transform.position = testPlayerObj.transform.position + testPlayerObj.transform.forward * 1.0f;
            BoxCollider crateCol = testCrateObj.AddComponent<BoxCollider>();
            crateCol.size = Vector3.one;

            BreakableCrate crate = testCrateObj.AddComponent<BreakableCrate>();
            crate.LootTable = emptyTable;
            crate.DirectCoinReward = 5;

            AutoDevVerifier.InvokeLifecycle(crate, "Awake");
            AutoDevVerifier.InvokeLifecycle(crate, "Start");

            if (crate.CurrentHealth != 25 || crate.MaxHealth != 25 || !crate.IsAlive || crate.IsBroken)
                return AutoDevCheckResult.Fail($"BreakableCrate baslangic durumlari hatali. Can={crate.CurrentHealth}, Broken={crate.IsBroken}");

            bool crateBrokenFired = false;
            crate.OnBroken += (c) => { crateBrokenFired = true; };

            // Kısmi hasar ve prosedürel geri bildirim kontrolü
            crate.TakeDamage(10, testPlayerObj.transform.forward);
            crate.StepFeedback(0.05f);
            if (crate.CurrentHealth != 15 || crate.IsBroken || !crate.IsAlive)
                return AutoDevCheckResult.Fail($"10 hasar sonrasi sandik cani 15 olmaliydi. Mevcut: {crate.CurrentHealth}");

            // ChickPeckAttack gaga vuruşu ile sandığı kırma testi
            Physics.SyncTransforms();
            if (!attack.PerformPeck())
                return AutoDevCheckResult.Fail("ChickPeckAttack.PerformPeck() sandiga vurusu basarisiz oldu.");

            if (!crate.IsBroken || crate.CurrentHealth > 0 || crate.IsAlive)
                return AutoDevCheckResult.Fail("Gaga vurusu sandigi kiramadi (IsBroken false veya Can > 0).");
            if (!crateBrokenFired)
                return AutoDevCheckResult.Fail("Sandik kirilinca OnBroken olayi tetiklenmedi.");
            if (crate.SpawnedDropCount <= 0)
                return AutoDevCheckResult.Fail("Sandik kirilinca ganimet/altin uretilmedi.");

            // C: Dash carpmasiyla kirilma kontrolu
            dashCrateObj = new GameObject("T013_DashCrate");
            dashCrateObj.transform.position = testPlayerObj.transform.position + testPlayerObj.transform.forward * 2.0f;
            BoxCollider dashCrateCol = dashCrateObj.AddComponent<BoxCollider>();
            dashCrateCol.size = Vector3.one;
            BreakableCrate dashCrate = dashCrateObj.AddComponent<BreakableCrate>();
            AutoDevVerifier.InvokeLifecycle(dashCrate, "Awake");

            dash.ExecuteDash();
            dashCrate.SendMessage("OnTriggerEnter", pCol, SendMessageOptions.DontRequireReceiver);

            if (!dashCrate.IsBroken)
                return AutoDevCheckResult.Fail("Dash sirasinda carpmada sandik aninda kirilmadi.");

            return AutoDevCheckResult.Pass("LootTableSO agirlikli secim ve BreakableCrate gaga/dash ile kirilma ve ganimet sistemi basariyla dogrulandi.", saveScene: saveScene);
        }
        finally
        {
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
            if (testCrateObj != null) Object.DestroyImmediate(testCrateObj);
            if (dashCrateObj != null) Object.DestroyImmediate(dashCrateObj);
        }
    }
}
