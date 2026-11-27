using UnityEngine;

public static class Check_T023
{
    [AutoDevCheck("T023")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        GameObject spawnerObj = null, playerObj = null, comboObj = null;
        try
        {
            spawnerObj = new GameObject("T023_Spawner");
            FloatingTextSpawner spawner = spawnerObj.AddComponent<FloatingTextSpawner>();
            AutoDevVerifier.InvokeLifecycle(spawner, "Awake");

            // 1. Spawn call returns active FloatingText with theme palette
            Vector3 origin = new Vector3(0f, 10f, 0f);
            FloatingText ft = spawner.Spawn("+1", origin, FloatingTextKind.Gold);
            if (ft == null || !ft.gameObject.activeSelf || !ft.IsAlive)
                return AutoDevCheckResult.Fail("Spawn aktif bir FloatingText dondurmedi.");
            if (ft.TextMesh == null || ft.TextMesh.text != "+1")
                return AutoDevCheckResult.Fail("FloatingText metni beklenen '+1' degil.");
            if (ft.TextMesh.color == Color.white || ft.TextMesh.color == Color.gray)
                return AutoDevCheckResult.Fail("FloatingText rengi tema yerine duz beyaz/gri kalmis.");

            // 2. Step: Y increases monotonically, Alpha decreases monotonically, Scale overshoots 1.0 and converges
            float prevY = ft.transform.position.y;
            float prevAlpha = ft.CurrentAlpha;
            float maxScale = 0f;
            bool overshot = false;

            for (int i = 0; i < 40; i++)
            {
                ft.Step(0.025f);
                float curY = ft.transform.position.y;
                float curAlpha = ft.CurrentAlpha;
                float curScale = ft.transform.localScale.x;

                if (curY < prevY - 0.0001f)
                    return AutoDevCheckResult.Fail($"Pozisyon Y artmadi: {curY:F3} < {prevY:F3}");
                if (curAlpha > prevAlpha + 0.0001f)
                    return AutoDevCheckResult.Fail($"Alfa monoton azalmadi: {curAlpha:F3} > {prevAlpha:F3}");

                if (curScale > 1.001f) overshot = true;
                if (curScale > maxScale) maxScale = curScale;

                prevY = curY;
                prevAlpha = curAlpha;
            }

            if (!overshot)
                return AutoDevCheckResult.Fail($"Pop-in sirasinda scale 1.0'i asmadi (overshoot yok). Max: {maxScale:F3}");
            if (Mathf.Abs(ft.transform.localScale.x - 1f) > 0.05f)
                return AutoDevCheckResult.Fail($"Scale 1.0'a yakinasmadi: {ft.transform.localScale.x:F3}");

            // Step until deactivation
            for (int i = 0; i < 50; i++) ft.Step(0.05f);
            if (ft.gameObject.activeSelf || ft.IsAlive)
                return AutoDevCheckResult.Fail("Omur sonunda nesne deaktif olmadi.");

            // 3. Pool reuse
            int poolBefore = spawner.PoolCount;
            FloatingText reused = spawner.Spawn("+5", origin, FloatingTextKind.Gold);
            if (reused != ft || spawner.PoolCount != poolBefore)
                return AutoDevCheckResult.Fail("Deaktif FloatingText yeniden kullanilmadi (havuz buyudu).");

            // 4. Combo text: ParticleSystem and warm orange
            FloatingText comboFt = spawner.Spawn("x3 KOMBO!", origin, FloatingTextKind.Combo);
            if (comboFt.ComboParticles == null || !comboFt.ComboParticles.isPlaying)
                return AutoDevCheckResult.Fail("Kombo yazisinda ParticleSystem oynatilmadi.");
            if (comboFt.TextMesh.color.r < 0.5f)
                return AutoDevCheckResult.Fail("Kombo yazisi turuncu renkte degil.");

            // 5. Automatic event subscriptions: PlayerHealth and ComboManager
            playerObj = new GameObject("T023_Player");
            playerObj.AddComponent<Rigidbody>();
            PlayerHealth ph = playerObj.AddComponent<PlayerHealth>();
            AutoDevVerifier.InvokeLifecycle(ph, "Awake");

            comboObj = new GameObject("T023_Combo");
            ComboManager cm = comboObj.AddComponent<ComboManager>();
            AutoDevVerifier.InvokeLifecycle(cm, "Awake");

            spawner.BindTargets(null, ph, cm, playerObj.transform);

            // PlayerHealth damage triggers damage text
            ph.TakeDamage(15);
            bool foundDamage = false;
            for (int i = 0; i < spawner.Pool.Count; i++)
            {
                FloatingText item = spawner.Pool[i];
                if (item.gameObject.activeSelf && item.Kind == FloatingTextKind.Damage && item.TextMesh.text.Contains("15"))
                {
                    foundDamage = true;
                    break;
                }
            }
            if (!foundDamage)
                return AutoDevCheckResult.Fail("PlayerHealth hasari sonrasi FloatingText otomatik olusmadi.");

            // ComboManager streak triggers combo text
            cm.RegisterCoinPickup(1);
            cm.RegisterCoinPickup(1);
            bool foundCombo = false;
            for (int i = 0; i < spawner.Pool.Count; i++)
            {
                FloatingText item = spawner.Pool[i];
                if (item.gameObject.activeSelf && item.Kind == FloatingTextKind.Combo && item.TextMesh.text.Contains("KOMBO"))
                {
                    foundCombo = true;
                    break;
                }
            }
            if (!foundCombo)
                return AutoDevCheckResult.Fail("ComboManager streak sonrasi kombo FloatingText otomatik olusmadi.");

            return AutoDevCheckResult.Pass($"FloatingTextSpawner dogrulandi. MaxScale={maxScale:F2}, Havuz={spawner.PoolCount}");
        }
        finally
        {
            if (comboObj != null) Object.DestroyImmediate(comboObj);
            if (playerObj != null) Object.DestroyImmediate(playerObj);
            if (spawnerObj != null) Object.DestroyImmediate(spawnerObj);
        }
    }
}
