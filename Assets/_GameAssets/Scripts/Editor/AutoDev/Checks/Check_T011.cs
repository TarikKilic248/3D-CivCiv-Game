using UnityEditor;
using UnityEngine;

public static class Check_T011
{
    [AutoDevCheck("T011")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        GameObject[] temp = new GameObject[4];
        try
        {
            // --- MovingPlatform ---
            temp[0] = new GameObject("T011_Moving");
            temp[0].transform.position = new Vector3(0f, 980f, 0f);
            temp[0].AddComponent<BoxCollider>();
            MovingPlatform mp = temp[0].AddComponent<MovingPlatform>();
            AutoDevVerifier.InvokeLifecycle(mp, "Awake");

            temp[1] = new GameObject("T011_Rider");
            temp[1].transform.position = mp.PointA + Vector3.up;
            Rigidbody riderRb = temp[1].AddComponent<Rigidbody>();
            riderRb.useGravity = false;
            mp.RegisterRider(riderRb);
            Vector3 riderStart = riderRb.position;

            for (int i = 0; i < 20; i++) mp.Step(0.02f);
            Vector3 moved = temp[0].transform.position - mp.PointA;
            if (moved.x < 0.5f)
                return AutoDevCheckResult.Fail($"MovingPlatform hareket etmedi: {moved}");
            if (Vector3.Distance(riderRb.position - riderStart, moved) > 0.001f)
                return AutoDevCheckResult.Fail("Platform delta hareketi oyuncuya aktarilmadi.");
            for (int i = 0; i < 400; i++) mp.Step(0.02f);
            if (Vector3.Distance(temp[0].transform.position, mp.PointB) > 0.01f && Vector3.Distance(temp[0].transform.position, mp.PointA) > 0.01f
                && temp[0].transform.position.x < mp.PointA.x - 0.01f)
                return AutoDevCheckResult.Fail("Platform noktalar disina cikti.");
            if (mp.SfxPlayCount < 1 || mp.DustVFX == null)
                return AutoDevCheckResult.Fail("MovingPlatform SFX/VFX kancasi calismadi.");

            // --- FallingPlatform ---
            temp[2] = new GameObject("T011_Falling");
            temp[2].transform.position = new Vector3(10f, 980f, 0f);
            temp[2].AddComponent<BoxCollider>();
            FallingPlatform fp = temp[2].AddComponent<FallingPlatform>();
            AutoDevVerifier.InvokeLifecycle(fp, "Awake");
            Vector3 fpStart = fp.StartPosition;
            if (!fp.Trigger() || fp.State != FallingPlatform.PlatformState.Shaking)
                return AutoDevCheckResult.Fail("FallingPlatform Shaking durumuna gecmedi.");
            if (fp.Trigger()) return AutoDevCheckResult.Fail("Shaking sirasinda ikinci tetikleme kabul edildi.");
            if (fp.SfxPlayCount != 1 || fp.CrumbleVFX == null || !fp.CrumbleVFX.isPlaying)
                return AutoDevCheckResult.Fail("FallingPlatform SFX/VFX calismadi.");
            fp.Step(0.1f);
            if (fp.VisualRoot.localPosition == Vector3.zero)
                return AutoDevCheckResult.Fail("Sarsinti olusmadi.");
            fp.Step(fp.FallDelay);
            if (fp.State != FallingPlatform.PlatformState.Falling)
                return AutoDevCheckResult.Fail($"Gecikme sonrasi Falling degil: {fp.State}");
            for (int i = 0; i < 25; i++) fp.Step(0.02f);
            if (temp[2].transform.position.y >= fpStart.y - 0.3f)
                return AutoDevCheckResult.Fail("Platform dusmedi.");
            fp.Step(fp.RespawnDelay);
            if (fp.State != FallingPlatform.PlatformState.Idle || Vector3.Distance(temp[2].transform.position, fpStart) > 0.001f)
                return AutoDevCheckResult.Fail("Platform respawn olmadi.");

            // --- WindCurrentZone ---
            temp[3] = new GameObject("T011_Wind");
            temp[3].transform.position = new Vector3(20f, 980f, 0f);
            temp[3].AddComponent<BoxCollider>();
            WindCurrentZone wind = temp[3].AddComponent<WindCurrentZone>();
            AutoDevVerifier.InvokeLifecycle(wind, "Awake");

            GameObject p = new GameObject("T011_WindPlayer");
            try
            {
                Rigidbody prb = p.AddComponent<Rigidbody>();
                prb.useGravity = false;
                p.AddComponent<CapsuleCollider>();
                p.AddComponent<StateController>();
                PlayerController pc = p.AddComponent<PlayerController>();
                ChickGlideController glide = p.AddComponent<ChickGlideController>();
                AutoDevVerifier.InvokeLifecycle(pc, "Awake");
                AutoDevVerifier.InvokeLifecycle(glide, "Awake");

                prb.linearVelocity = Vector3.zero;
                wind.ApplyWind(prb, 0.1f);
                float normalVy = prb.linearVelocity.y;
                if (normalVy <= 0.5f) return AutoDevCheckResult.Fail($"Ruzgar yukari itmedi: {normalVy:F2}");

                glide.StartGlide();
                prb.linearVelocity = Vector3.zero;
                wind.ApplyWind(prb, 0.1f);
                float glideVy = prb.linearVelocity.y;
                if (glide.IsGliding && glideVy < normalVy * 1.5f)
                    return AutoDevCheckResult.Fail($"Suzulme carpani uygulanmadi: {glideVy:F2} vs {normalVy:F2}");
                if (wind.SfxPlayCount < 1 || wind.WindVFX == null || !wind.WindVFX.isPlaying)
                    return AutoDevCheckResult.Fail("Wind SFX/VFX calismadi.");
            }
            finally { Object.DestroyImmediate(p); }

            AutoDevCheckResult ok = AutoDevCheckResult.Pass("MovingPlatform, FallingPlatform ve WindCurrentZone dogrulandi.");
            return ok;
        }
        finally
        {
            foreach (GameObject g in temp) if (g != null) Object.DestroyImmediate(g);
        }
    }
}
