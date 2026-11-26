using System;
using UnityEditor;
using UnityEngine;

public static class Check_T006
{
    [AutoDevCheck("T006")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            return AutoDevCheckResult.Fail("Sahne uzerinde PlayerController bulunamadi!");
        }

        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider == null)
        {
            playerCollider = player.GetComponentInChildren<Collider>();
        }
        if (playerCollider == null)
        {
            return AutoDevCheckResult.Fail("Player uzerinde Collider bulunamadi!");
        }

        Rigidbody rb = player.GetRigidbody();
        if (rb == null)
        {
            rb = player.GetComponent<Rigidbody>();
        }
        if (rb == null)
        {
            return AutoDevCheckResult.Fail("Player uzerinde Rigidbody bulunamadi!");
        }

        bool saveScene = false;

        // 1. Sahne Idempotent Kurulumu: Checkpoint_Start
        Checkpoint startCheckpoint = null;
        Checkpoint[] sceneCheckpoints = UnityEngine.Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include);
        foreach (var cp in sceneCheckpoints)
        {
            if (cp.name == "Checkpoint_Start" || cp.IsDefaultCheckpoint)
            {
                startCheckpoint = cp;
                break;
            }
        }

        if (startCheckpoint == null)
        {
            GameObject cpObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cpObj.name = "Checkpoint_Start";
            cpObj.transform.position = new Vector3(1.85f, 0.05f, 2.25f);
            cpObj.transform.localScale = new Vector3(2.5f, 0.1f, 2.5f);

            Collider primCol = cpObj.GetComponent<Collider>();
            if (primCol != null) UnityEngine.Object.DestroyImmediate(primCol);

            BoxCollider triggerCol = cpObj.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.center = new Vector3(0f, 1f, 0f);
            triggerCol.size = new Vector3(2.5f, 2f, 2.5f);

            startCheckpoint = cpObj.AddComponent<Checkpoint>();
            startCheckpoint.IsDefaultCheckpoint = true;
            startCheckpoint.IndicatorRenderer = cpObj.GetComponent<Renderer>();

            Undo.RegisterCreatedObjectUndo(cpObj, "Create Checkpoint_Start");
            saveScene = true;
        }

        // 2. Sahne Idempotent Kurulumu: FallKillZone
        HazardZone killZone = null;
        HazardZone[] sceneHazards = UnityEngine.Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Include);
        foreach (var hz in sceneHazards)
        {
            if (hz.name == "FallKillZone")
            {
                killZone = hz;
                break;
            }
        }

        if (killZone == null)
        {
            GameObject hzObj = new GameObject("FallKillZone");
            hzObj.transform.position = new Vector3(0f, -10f, 0f);
            BoxCollider col = hzObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(300f, 2f, 300f);

            killZone = hzObj.AddComponent<HazardZone>();

            Undo.RegisterCreatedObjectUndo(hzObj, "Create FallKillZone");
            saveScene = true;
        }

        // 3. Davranissal Dogrulama: Checkpoint Aktivasyonu ve Olay Geri Bildirimi
        Vector3 initialPlayerPos = player.transform.position;
        Vector3 initialLinearVel = rb.linearVelocity;
        Vector3 initialAngularVel = rb.angularVelocity;

        GameObject testCpObj = new GameObject("T006_TestCheckpoint");
        testCpObj.transform.position = new Vector3(25f, 1f, 25f);
        BoxCollider testCpCol = testCpObj.AddComponent<BoxCollider>();
        testCpCol.isTrigger = true;
        Checkpoint testCp = testCpObj.AddComponent<Checkpoint>();

        bool checkpointEventFired = false;
        Action<Checkpoint> onCpActivated = (cp) =>
        {
            if (cp == testCp) checkpointEventFired = true;
        };
        Checkpoint.OnCheckpointActivated += onCpActivated;

        testCp.OnTriggerEnter(playerCollider);
        Checkpoint.OnCheckpointActivated -= onCpActivated;

        if (!checkpointEventFired)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            return AutoDevCheckResult.Fail("Oyuncu temas ettiginde Checkpoint.OnCheckpointActivated olayi tetiklenmedi!");
        }

        if (!testCp.IsActivated)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            return AutoDevCheckResult.Fail("Oyuncu temas sonrasi Checkpoint.IsActivated degeri true olmadi!");
        }

        if (Vector3.Distance(Checkpoint.ActiveRespawnPoint, testCp.RespawnPosition) > 0.05f)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            return AutoDevCheckResult.Fail($"Checkpoint.ActiveRespawnPoint beklenen konuma guncellenmedi! Beklenen: {testCp.RespawnPosition}, Gercek: {Checkpoint.ActiveRespawnPoint}");
        }

        // 4. Davranissal Dogrulama: HazardZone Isinlanma ve Hiz Sifirlama
        rb.linearVelocity = new Vector3(6f, -14f, 4f);
        rb.angularVelocity = new Vector3(1.5f, 2.5f, 3.5f);
        player.transform.position = new Vector3(80f, -25f, 80f);
        Physics.SyncTransforms();

        GameObject testHazardObj = new GameObject("T006_TestHazard");
        BoxCollider hazCol = testHazardObj.AddComponent<BoxCollider>();
        hazCol.isTrigger = true;
        HazardZone testHazard = testHazardObj.AddComponent<HazardZone>();

        bool hazardEventFired = false;
        Vector3 respawnTargetRecorded = Vector3.zero;
        Action<PlayerController, Vector3> onHazard = (p, pos) =>
        {
            hazardEventFired = true;
            respawnTargetRecorded = pos;
        };
        HazardZone.OnPlayerHazardTriggered += onHazard;

        testHazard.OnTriggerEnter(playerCollider);
        HazardZone.OnPlayerHazardTriggered -= onHazard;

        if (!hazardEventFired)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            UnityEngine.Object.DestroyImmediate(testHazardObj);
            return AutoDevCheckResult.Fail("HazardZone tetiklendiginde OnPlayerHazardTriggered olayi firlatilmadi!");
        }

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            UnityEngine.Object.DestroyImmediate(testHazardObj);
            return AutoDevCheckResult.Fail($"HazardZone sonrasi Rigidbody linearVelocity sifirlanmadi! Mevcut: {rb.linearVelocity}");
        }

        if (rb.angularVelocity.sqrMagnitude > 0.001f)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            UnityEngine.Object.DestroyImmediate(testHazardObj);
            return AutoDevCheckResult.Fail($"HazardZone sonrasi Rigidbody angularVelocity sifirlanmadi! Mevcut: {rb.angularVelocity}");
        }

        if (Vector3.Distance(player.transform.position, testCp.RespawnPosition) > 0.1f)
        {
            UnityEngine.Object.DestroyImmediate(testCpObj);
            UnityEngine.Object.DestroyImmediate(testHazardObj);
            return AutoDevCheckResult.Fail($"HazardZone sonrasi oyuncu aktif checkpoint konumuna isinlanmadi! Oyuncu: {player.transform.position}, Checkpoint: {testCp.RespawnPosition}");
        }

        // 5. Test Temizligi ve Oyuncu Durumunu Geri Yukleme
        UnityEngine.Object.DestroyImmediate(testCpObj);
        UnityEngine.Object.DestroyImmediate(testHazardObj);

        if (startCheckpoint != null)
        {
            startCheckpoint.Activate(false);
        }
        else
        {
            Checkpoint.ResetActiveCheckpoint();
        }

        rb.linearVelocity = initialLinearVel;
        rb.angularVelocity = initialAngularVel;
        rb.position = initialPlayerPos;
        player.transform.position = initialPlayerPos;
        Physics.SyncTransforms();

        return AutoDevCheckResult.Pass(
            "T006 basariyla dogrulandi: Checkpoint aktivasyonu, ActiveRespawnPoint guncellemesi ve HazardZone hiz sifirlamali isinlanma tam calisiyor.",
            $"SaveScene={saveScene}",
            saveScene
        );
    }
}
