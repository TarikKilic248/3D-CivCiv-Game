using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Check_T016
{
    [AutoDevCheck("T016")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        PlayerController scenePlayer = Object.FindAnyObjectByType<PlayerController>();
        if (scenePlayer == null)
            return AutoDevCheckResult.Fail("Demo.unity sahnesinde PlayerController bulunamadi.");

        bool saveScene = false;

        // 1. Demo.unity sahnesinde ChickRescueManager bilesenini idempotent kur
        ChickRescueManager sceneManager = Object.FindAnyObjectByType<ChickRescueManager>();
        if (sceneManager == null)
        {
            GameObject managerObj = new GameObject("ChickRescueManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create ChickRescueManager");
            sceneManager = managerObj.AddComponent<ChickRescueManager>();
            sceneManager.PlayerTransform = scenePlayer.transform;
            EditorUtility.SetDirty(managerObj);
            saveScene = true;
        }
        else if (sceneManager.PlayerTransform == null)
        {
            sceneManager.PlayerTransform = scenePlayer.transform;
            EditorUtility.SetDirty(sceneManager);
            saveScene = true;
        }

        // 2. Demo.unity Canvas altinda WaypointIndicatorUI bilesenini idempotent kur
        WaypointIndicatorUI sceneIndicator = Object.FindAnyObjectByType<WaypointIndicatorUI>();
        if (sceneIndicator == null)
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return AutoDevCheckResult.Fail("Demo.unity sahnesinde Canvas bulunamadi.");

            GameObject indicatorObj = new GameObject("WaypointIndicator", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(indicatorObj, "Create WaypointIndicator");
            indicatorObj.transform.SetParent(canvas.transform, false);

            RectTransform indRt = indicatorObj.GetComponent<RectTransform>();
            indRt.anchorMin = new Vector2(0.5f, 1f);
            indRt.anchorMax = new Vector2(0.5f, 1f);
            indRt.pivot = new Vector2(0.5f, 1f);
            indRt.anchoredPosition = new Vector2(0f, -55f);
            indRt.sizeDelta = new Vector2(170f, 44f);

            GameObject panelObj = new GameObject("IndicatorPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(indicatorObj.transform, false);
            RectTransform panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            GameObject arrowObj = new GameObject("ArrowImage", typeof(RectTransform), typeof(Image));
            arrowObj.transform.SetParent(panelObj.transform, false);
            RectTransform arrowRt = arrowObj.GetComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(0f, 0.5f);
            arrowRt.anchorMax = new Vector2(0f, 0.5f);
            arrowRt.pivot = new Vector2(0.5f, 0.5f);
            arrowRt.anchoredPosition = new Vector2(24f, 0f);
            arrowRt.sizeDelta = new Vector2(24f, 24f);

            GameObject textObj = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panelObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(46f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20f;
            tmp.text = "0m";

            sceneIndicator = indicatorObj.AddComponent<WaypointIndicatorUI>();
            sceneIndicator.ResolveDependencies();

            UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
            sceneIndicator.ApplyTheme(theme);

            EditorUtility.SetDirty(indicatorObj);
            saveScene = true;
        }

        // 3. Demo.unity sahnesinde ornek LostChickNPC nesnelerini idempotent kur
        LostChickNPC[] sceneChicks = Object.FindObjectsByType<LostChickNPC>(FindObjectsInactive.Include);
        if (sceneChicks == null || sceneChicks.Length == 0)
        {
            GameObject groupObj = new GameObject("[LostChicks]");
            Undo.RegisterCreatedObjectUndo(groupObj, "Create [LostChicks]");
            groupObj.transform.position = Vector3.zero;

            Vector3[] chickPositions = new Vector3[]
            {
                new Vector3(4.5f, 0.25f, 4.5f),
                new Vector3(-2.0f, 0.25f, 7.5f),
                new Vector3(6.5f, 0.25f, 10.5f)
            };

            for (int i = 0; i < chickPositions.Length; i++)
            {
                GameObject chickObj = new GameObject($"LostChick_0{i + 1}");
                chickObj.transform.SetParent(groupObj.transform, false);
                chickObj.transform.position = chickPositions[i];

                SphereCollider col = chickObj.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.8f;
                col.center = new Vector3(0f, 0.4f, 0f);

                GameObject visualRoot = new GameObject("VisualModel");
                visualRoot.transform.SetParent(chickObj.transform, false);

                // Govde
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Body";
                body.transform.SetParent(visualRoot.transform, false);
                body.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
                body.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                Object.DestroyImmediate(body.GetComponent<Collider>());

                // Bas
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(visualRoot.transform, false);
                head.transform.localScale = new Vector3(0.42f, 0.4f, 0.42f);
                head.transform.localPosition = new Vector3(0f, 0.55f, 0.12f);
                Object.DestroyImmediate(head.GetComponent<Collider>());

                // Gaga
                GameObject beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beak.name = "Beak";
                beak.transform.SetParent(visualRoot.transform, false);
                beak.transform.localScale = new Vector3(0.12f, 0.08f, 0.14f);
                beak.transform.localPosition = new Vector3(0f, 0.52f, 0.35f);
                Object.DestroyImmediate(beak.GetComponent<Collider>());

                LostChickNPC chick = chickObj.AddComponent<LostChickNPC>();
                chick.ResolveDependencies();
                chick.EnsureVFXSetup();

                sceneManager.RegisterChick(chick);
            }

            EditorUtility.SetDirty(groupObj);
            saveScene = true;
        }
        else
        {
            for (int i = 0; i < sceneChicks.Length; i++)
            {
                if (sceneChicks[i].EnsureVFXSetup())
                    saveScene = true;
            }
        }

        // 4. Davranissal Dogrulama (LostChickNPC, ChickRescueManager & WaypointIndicatorUI)
        GameObject testRoot = null;
        ChickRescueManager previousManagerInstance = ChickRescueManager.Instance;
        try
        {
            Vector3 origin = new Vector3(0f, 980f, 0f);

            testRoot = new GameObject("T016_BehavioralTestRoot");
            testRoot.transform.position = origin;

            // Test Oyuncusu
            GameObject testPlayer = new GameObject("TestPlayer");
            testPlayer.transform.SetParent(testRoot.transform, false);
            testPlayer.transform.position = origin;

            // Test Rescue Manager (EditMode'da Awake otomatik cagrilmadigi icin Instance baglanir)
            GameObject testManagerObj = new GameObject("TestRescueManager");
            testManagerObj.transform.SetParent(testRoot.transform, false);
            ChickRescueManager testManager = testManagerObj.AddComponent<ChickRescueManager>();
            testManager.PlayerTransform = testPlayer.transform;
            ChickRescueManager.Instance = testManager;

            // Test Civciv 1 (10 metre uzakta)
            GameObject chick1Obj = new GameObject("TestChick_1");
            chick1Obj.transform.SetParent(testRoot.transform, false);
            chick1Obj.transform.position = origin + new Vector3(0f, 0f, 10f);
            GameObject chick1Visual = new GameObject("VisualModel");
            chick1Visual.transform.SetParent(chick1Obj.transform, false);
            SphereCollider col1 = chick1Obj.AddComponent<SphereCollider>();
            col1.isTrigger = true;
            col1.radius = 0.8f;
            LostChickNPC chick1 = chick1Obj.AddComponent<LostChickNPC>();
            chick1.ResolveDependencies();
            chick1.EnsureVFXSetup();

            // Test Civciv 2 (25 metre uzakta)
            GameObject chick2Obj = new GameObject("TestChick_2");
            chick2Obj.transform.SetParent(testRoot.transform, false);
            chick2Obj.transform.position = origin + new Vector3(0f, 0f, 25f);
            GameObject chick2Visual = new GameObject("VisualModel");
            chick2Visual.transform.SetParent(chick2Obj.transform, false);
            SphereCollider col2 = chick2Obj.AddComponent<SphereCollider>();
            col2.isTrigger = true;
            col2.radius = 0.8f;
            LostChickNPC chick2 = chick2Obj.AddComponent<LostChickNPC>();
            chick2.ResolveDependencies();
            chick2.EnsureVFXSetup();

            // Test Canvas ve WaypointIndicatorUI
            GameObject testCanvasObj = new GameObject("TestCanvas", typeof(Canvas));
            testCanvasObj.transform.SetParent(testRoot.transform, false);

            GameObject testIndObj = new GameObject("TestIndicator", typeof(RectTransform));
            testIndObj.transform.SetParent(testCanvasObj.transform, false);

            GameObject panel = new GameObject("IndicatorPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(testIndObj.transform, false);

            GameObject arrow = new GameObject("ArrowImage", typeof(RectTransform), typeof(Image));
            arrow.transform.SetParent(panel.transform, false);

            GameObject text = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(panel.transform, false);

            WaypointIndicatorUI indicator = testIndObj.AddComponent<WaypointIndicatorUI>();
            indicator.ResolveDependencies();

            // Yöneticileri kaydet
            testManager.RegisterChick(chick1);
            testManager.RegisterChick(chick2);

            // A: Baslangic durumu ve IInteractable dogrulamasi
            if (testManager.TotalChickCount != 2)
                return AutoDevCheckResult.Fail($"TotalChickCount 2 olmali, ancak {testManager.TotalChickCount} bulundu.");
            if (testManager.RescuedChickCount != 0 || testManager.RemainingChickCount != 2)
                return AutoDevCheckResult.Fail("Baslangicta kurtarilmis civciv sayisi 0, kalan 2 olmali.");
            if (!chick1.CanInteract(null) || string.IsNullOrWhiteSpace(chick1.GetPromptText(null)))
                return AutoDevCheckResult.Fail("Kurtarilmamis civciv etkilesime hazir ve ipucu metnine sahip olmali.");
            if (chick1.RescueVFX == null)
                return AutoDevCheckResult.Fail("LostChickNPC RescueVFX kurulumu eksik.");

            indicator.UpdateIndicatorManual(origin, null);
            if (!ReferenceEquals(indicator.CurrentTargetChick, chick1))
                return AutoDevCheckResult.Fail("WaypointIndicatorUI en yakin civciv olan Chick_1'i hedeflemedi.");
            if (Mathf.Abs(indicator.CurrentDistance - 10f) > 0.5f)
                return AutoDevCheckResult.Fail($"Mesafe 10m olmali, ancak {indicator.CurrentDistance} hesaplandi.");
            if (!indicator.IsVisible)
                return AutoDevCheckResult.Fail("Kurtarilacak civciv varken WaypointIndicatorUI gorunur olmali.");
            if (!indicator.CurrentDistanceText.Contains("10"))
                return AutoDevCheckResult.Fail("Mesafe metni 10 sayisini icermiyor.");

            // B: Civciv 1 Kurtarma (IInteractable) ve Takip Kontrolu
            bool rescueEventFired = false;
            testManager.OnChickRescued += (_) => rescueEventFired = true;

            if (!chick1.Interact(null))
                return AutoDevCheckResult.Fail("chick1.Interact(null) true donmeliydi.");
            if (!chick1.IsRescued || chick1.CanInteract(null))
                return AutoDevCheckResult.Fail("chick1 kurtarildiktan sonra IsRescued=true ve CanInteract=false olmaliydi.");
            if (testManager.RescuedChickCount != 1 || testManager.RemainingChickCount != 1)
                return AutoDevCheckResult.Fail("chick1 kurtarildiktan sonra sayac 1/1 olmaliydi.");
            if (!rescueEventFired)
                return AutoDevCheckResult.Fail("OnChickRescued olayi tetiklenmedi.");
            if (!ReferenceEquals(chick1.FollowTarget, testPlayer.transform))
                return AutoDevCheckResult.Fail("Ilk kurtarilan civcivin FollowTarget'i oyuncu olmali.");

            float distBefore = Vector3.Distance(chick1.transform.position, testPlayer.transform.position);
            chick1.Step(0.2f);
            float distAfter = Vector3.Distance(chick1.transform.position, testPlayer.transform.position);
            if (distAfter >= distBefore)
                return AutoDevCheckResult.Fail("chick1.Step(0.2f) sonrasinda civciv oyuncuya dogru ilerlemedi.");
            if (chick1Visual.transform.localPosition.y <= 0.01f)
                return AutoDevCheckResult.Fail("Kurtarilma sonrasi prosedurel ziplama geri bildirimi olusmadi.");
            chick1.Step(0.3f);

            // C: WaypointIndicatorUI Guncellemesi (Chick 2'ye gecis)
            indicator.UpdateIndicatorManual(origin, null);
            if (!ReferenceEquals(indicator.CurrentTargetChick, chick2))
                return AutoDevCheckResult.Fail("chick1 kurtarildiktan sonra WaypointIndicatorUI en yakin unrescued civciv olan Chick_2'ye gecmeli.");
            if (Mathf.Abs(indicator.CurrentDistance - 25f) > 0.5f)
                return AutoDevCheckResult.Fail($"Yeni mesafe 25m olmali, ancak {indicator.CurrentDistance} hesaplandi.");
            if (!indicator.CurrentDistanceText.Contains("25"))
                return AutoDevCheckResult.Fail("Guncel mesafe metni 25 sayisini icermiyor.");

            // D: Civciv 2 Kurtarma ve Kuyruk Takibi (Kervan) Kontrolu
            bool allRescuedFired = false;
            testManager.OnAllChicksRescued += () => allRescuedFired = true;

            chick2.Rescue();
            if (!chick2.IsRescued)
                return AutoDevCheckResult.Fail("chick2.Rescue sonrasinda chick2.IsRescued = true olmaliydi.");
            if (testManager.RescuedChickCount != 2 || !testManager.AreAllChicksRescued || !allRescuedFired)
                return AutoDevCheckResult.Fail("Tum civcivler kurtarildiginda AreAllChicksRescued ve OnAllChicksRescued dogrulanmali.");
            if (!ReferenceEquals(chick2.FollowTarget, chick1.transform))
                return AutoDevCheckResult.Fail("Ikinci kurtarilan civcivin FollowTarget'i onundeki ilk civciv olmali (kuyruk takibi).");

            float queueDistBefore = Vector3.Distance(chick2.transform.position, chick1.transform.position);
            chick2.Step(0.5f);
            float queueDistAfter = Vector3.Distance(chick2.transform.position, chick1.transform.position);
            if (queueDistAfter >= queueDistBefore)
                return AutoDevCheckResult.Fail("chick2.Step(0.5f) sonrasinda kuyruktaki ondeki civcive yaklasmadi.");

            // E: Tum civcivler kurtarildiginda WaypointIndicatorUI'nin gizlenmesi
            indicator.UpdateIndicatorManual(origin, null);
            if (indicator.IsVisible)
                return AutoDevCheckResult.Fail("Tum civcivler kurtarildiginda WaypointIndicatorUI otomatik olarak gizlenmeli.");

            return AutoDevCheckResult.Pass("LostChickNPC, ChickRescueManager ve WaypointIndicatorUI davranislari basariyla dogrulandi.", saveScene: saveScene);
        }
        finally
        {
            ChickRescueManager.Instance = previousManagerInstance;
            if (testRoot != null)
            {
                Object.DestroyImmediate(testRoot);
            }
        }
    }
}
