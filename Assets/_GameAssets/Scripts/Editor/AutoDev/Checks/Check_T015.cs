using UnityEditor;
using UnityEngine;

public static class Check_T015
{
    [AutoDevCheck("T015")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
            return AutoDevCheckResult.Fail("Demo.unity sahnesinde PlayerController bulunamadi.");

        bool saveScene = false;

        // 1. Oyuncuya PlayerInteractor bilesenini idempotent ekle ve VFX'i kur
        PlayerInteractor sceneInteractor = scenePlayer.GetComponent<PlayerInteractor>();
        if (sceneInteractor == null)
        {
            sceneInteractor = Undo.AddComponent<PlayerInteractor>(scenePlayer.gameObject);
            saveScene = true;
        }

        sceneInteractor.ResolveDependencies();
        if (sceneInteractor.EnsureVFXSetup())
            saveScene = true;

        if (sceneInteractor.InteractionVFX == null)
            return AutoDevCheckResult.Fail("Sahne oyuncusunda InteractionVFX eksik.");

        if (saveScene)
        {
            EditorUtility.SetDirty(sceneInteractor);
            EditorUtility.SetDirty(scenePlayer.gameObject);
        }

        // 2. Demo.unity sahnesinde ornek PressurePlateSwitch ve hedef kapi duzenegini idempotent kur
        PressurePlateSwitch[] scenePlates = Object.FindObjectsByType<PressurePlateSwitch>(FindObjectsInactive.Include);
        if (scenePlates == null || scenePlates.Length == 0)
        {
            GameObject puzzleRoot = new GameObject("PressurePlatePuzzle");
            Undo.RegisterCreatedObjectUndo(puzzleRoot, "Create PressurePlatePuzzle");
            puzzleRoot.transform.position = new Vector3(4.5f, 0.25f, 2.5f);

            GameObject plateObj = new GameObject("PressurePlate");
            plateObj.transform.SetParent(puzzleRoot.transform, false);
            BoxCollider triggerCol = plateObj.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(1.6f, 0.5f, 1.6f);
            triggerCol.center = new Vector3(0f, 0.25f, 0f);

            GameObject plateVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateVisual.name = "PlateVisual";
            plateVisual.transform.SetParent(plateObj.transform, false);
            plateVisual.transform.localScale = new Vector3(1.5f, 0.16f, 1.5f);
            plateVisual.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            Object.DestroyImmediate(plateVisual.GetComponent<Collider>());

            GameObject gateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateObj.name = "PuzzleGate";
            gateObj.transform.SetParent(puzzleRoot.transform, false);
            gateObj.transform.localScale = new Vector3(0.4f, 3.0f, 2.6f);
            gateObj.transform.localPosition = new Vector3(3.0f, 1.5f, 0f);

            PressurePlateSwitch plateSwitch = plateObj.AddComponent<PressurePlateSwitch>();
            plateSwitch.TargetTransform = gateObj.transform;
            plateSwitch.TargetActiveOffset = new Vector3(0f, 3.2f, 0f);
            plateSwitch.ResolveDependencies();
            plateSwitch.EnsureVFXSetup();

            EditorUtility.SetDirty(puzzleRoot);
            saveScene = true;
        }
        else
        {
            for (int i = 0; i < scenePlates.Length; i++)
            {
                if (scenePlates[i].EnsureVFXSetup())
                    saveScene = true;
            }
        }

        // 3. Davranissal Dogrulama (IInteractable, PlayerInteractor & PressurePlateSwitch)
        GameObject testPlayerObj = null;
        GameObject testPlateObj = null;
        GameObject testGateObj = null;
        GameObject testWeightObj = null;

        try
        {
            Vector3 origin = new Vector3(0f, 960f, 0f);

            testPlayerObj = new GameObject("T015_TestPlayer");
            testPlayerObj.transform.position = origin;
            GameObject visualObj = new GameObject("VisualModel");
            visualObj.transform.SetParent(testPlayerObj.transform, false);
            PlayerInteractor interactor = testPlayerObj.AddComponent<PlayerInteractor>();
            interactor.InteractionRadius = 3.5f;

            testPlateObj = new GameObject("T015_TestPlate");
            testPlateObj.transform.position = origin + new Vector3(0f, 0f, 1.5f);
            BoxCollider plateCol = testPlateObj.AddComponent<BoxCollider>();
            plateCol.isTrigger = true;
            plateCol.size = new Vector3(1.5f, 0.6f, 1.5f);

            GameObject testPlateVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testPlateVisual.name = "PlateVisual";
            testPlateVisual.transform.SetParent(testPlateObj.transform, false);
            testPlateVisual.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            Object.DestroyImmediate(testPlateVisual.GetComponent<Collider>());

            testGateObj = new GameObject("T015_TestGate");
            testGateObj.transform.position = origin + new Vector3(4f, 0f, 0f);

            PressurePlateSwitch plate = testPlateObj.AddComponent<PressurePlateSwitch>();
            plate.TargetTransform = testGateObj.transform;
            plate.TargetActiveOffset = new Vector3(0f, 3.0f, 0f);
            plate.MoveSpeed = 6f;

            AutoDevVerifier.InvokeLifecycle(interactor, "Awake");
            AutoDevVerifier.InvokeLifecycle(interactor, "Start");
            AutoDevVerifier.InvokeLifecycle(plate, "Awake");
            AutoDevVerifier.InvokeLifecycle(plate, "Start");
            Physics.SyncTransforms();

            bool focusFired = false;
            bool interactedFired = false;
            bool activatedFired = false;
            bool deactivatedFired = false;
            interactor.OnFocusChanged += (_) => focusFired = true;
            interactor.OnInteracted += (_) => interactedFired = true;
            plate.OnActivated += (_) => activatedFired = true;
            plate.OnDeactivated += (_) => deactivatedFired = true;

            // A: Yakinlik tespiti ve ipucu metni
            interactor.Step(0.05f);
            if (!ReferenceEquals(interactor.CurrentInteractable, plate) || !focusFired)
                return AutoDevCheckResult.Fail("PlayerInteractor menzil icindeki PressurePlateSwitch nesnesini tespit edemedi.");
            if (string.IsNullOrWhiteSpace(plate.GetPromptText(interactor)))
                return AutoDevCheckResult.Fail("PressurePlateSwitch bos etkilesim ipucu metni dondurdu.");

            // B: Manuel etkilesim ile aktivasyon, hedef hareketi ve prosedurel hareket
            float gateStartY = testGateObj.transform.position.y;
            float plateVisualStartY = testPlateVisual.transform.localPosition.y;
            Vector3 playerBaseScale = visualObj.transform.localScale;

            if (!interactor.TryInteract())
                return AutoDevCheckResult.Fail("PlayerInteractor.TryInteract() basarisiz oldu.");
            if (!plate.IsPressed || plate.ActivationCount != 1 || interactor.InteractionCount != 1)
                return AutoDevCheckResult.Fail($"Aktivasyon durumu veya sayaclari hatali. Pressed={plate.IsPressed}, Act={plate.ActivationCount}, Int={interactor.InteractionCount}");
            if (!interactedFired || !activatedFired)
                return AutoDevCheckResult.Fail("OnInteracted veya OnActivated event'i tetiklenmedi.");
            if (Vector3.Distance(visualObj.transform.localScale, playerBaseScale) < 0.02f)
                return AutoDevCheckResult.Fail("Etkilesim sonrasi oyuncu prosedurel squash geri bildirimi olusmadi.");

            for (int i = 0; i < 10; i++) plate.Step(0.1f);
            if (testGateObj.transform.position.y <= gateStartY + 2.5f)
                return AutoDevCheckResult.Fail($"Hedef kapi aktif konuma hareket etmedi. Y={testGateObj.transform.position.y:F2}");
            if (testPlateVisual.transform.localPosition.y >= plateVisualStartY - 0.05f)
                return AutoDevCheckResult.Fail($"Plaka gorseli asagi cokmedi. Y={testPlateVisual.transform.localPosition.y:F3}");

            // C: Tekrar etkilesim ile birakma
            if (!interactor.TryInteract() || plate.IsPressed || !deactivatedFired)
                return AutoDevCheckResult.Fail("Ikinci etkilesimde plaka serbest birakilmadi veya OnDeactivated tetiklenmedi.");

            // D: Agirlik/Presser kaydi ve kalici basili kalma (StayPressedPermanently)
            testWeightObj = new GameObject("T015_Weight");
            BoxCollider weightCol = testWeightObj.AddComponent<BoxCollider>();
            plate.StayPressedPermanently = true;
            plate.RegisterPresser(weightCol);
            if (!plate.IsPressed)
                return AutoDevCheckResult.Fail("RegisterPresser plakayi aktiflestirmedi.");
            plate.UnregisterPresser(weightCol);
            if (!plate.IsPressed)
                return AutoDevCheckResult.Fail("StayPressedPermanently=true iken plaka serbest kalmamaliydi.");

            // E: Menzil disi kontrolu
            plate.StayPressedPermanently = false;
            testPlayerObj.transform.position = origin + new Vector3(0f, 0f, 20f);
            Physics.SyncTransforms();
            interactor.Step(0.05f);
            if (interactor.CurrentInteractable != null || interactor.TryInteract())
                return AutoDevCheckResult.Fail("Menzil disindaki nesneyle etkilesime izin verilmemeliydi.");

            return AutoDevCheckResult.Pass("IInteractable, PlayerInteractor ve PressurePlateSwitch mekanikleri basariyla dogrulandi.", saveScene: saveScene);
        }
        finally
        {
            if (testPlayerObj != null) Object.DestroyImmediate(testPlayerObj);
            if (testPlateObj != null) Object.DestroyImmediate(testPlateObj);
            if (testGateObj != null) Object.DestroyImmediate(testGateObj);
            if (testWeightObj != null) Object.DestroyImmediate(testWeightObj);
        }
    }
}
