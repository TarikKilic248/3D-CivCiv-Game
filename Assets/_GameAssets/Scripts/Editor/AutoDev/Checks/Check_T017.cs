using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CivCiv 3D - T017 Doğrulama Kontrolü
/// JSON Kalıcı Kayıt ve Yıldız Puanlama Sistemi (SaveManager, LevelDataSO & LevelProgressionManager)
/// </summary>
public static class Check_T017
{
    [AutoDevCheck("T017")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);

        bool saveScene = false;

        // 1. Idempotent: DefaultLevelData.asset varlığını kontrol et ve gerekiyorsa oluştur
        string dataFolder = "Assets/_GameAssets/Data";
        if (!AssetDatabase.IsValidFolder(dataFolder))
        {
            AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        }

        LevelDataSO levelData = AssetDatabase.LoadAssetAtPath<LevelDataSO>(LevelDataSO.DefaultAssetPath);
        if (levelData == null)
        {
            levelData = ScriptableObject.CreateInstance<LevelDataSO>();
            levelData.LevelId = "Level_Demo";
            levelData.LevelDisplayName = "Demo Ciftlik";
            levelData.SceneName = "Demo";
            levelData.TargetCoins = 29;
            levelData.TargetChicks = 2;
            levelData.TimeLimit = 60f;
            levelData.Star1MinCoins = 10;
            levelData.Star1MinRemainingTime = 0f;
            levelData.Star2MinCoins = 20;
            levelData.Star2MinRemainingTime = 15f;
            levelData.Star3MinCoins = 28;
            levelData.Star3MinRemainingTime = 30f;

            AssetDatabase.CreateAsset(levelData, LevelDataSO.DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }

        // 2. Idempotent: Demo.unity sahnesinde SaveManager kur
        SaveManager sceneSaveManager = Object.FindAnyObjectByType<SaveManager>();
        if (sceneSaveManager == null)
        {
            GameObject saveObj = new GameObject("SaveManager");
            Undo.RegisterCreatedObjectUndo(saveObj, "Create SaveManager");
            sceneSaveManager = saveObj.AddComponent<SaveManager>();
            EditorUtility.SetDirty(saveObj);
            saveScene = true;
        }

        // 3. Idempotent: Demo.unity sahnesinde LevelProgressionManager kur
        LevelProgressionManager sceneProgression = Object.FindAnyObjectByType<LevelProgressionManager>();
        if (sceneProgression == null)
        {
            GameObject progObj = new GameObject("LevelProgressionManager");
            Undo.RegisterCreatedObjectUndo(progObj, "Create LevelProgressionManager");
            sceneProgression = progObj.AddComponent<LevelProgressionManager>();
            sceneProgression.LevelData = levelData;
            sceneProgression.EnsureVFXSetup();
            EditorUtility.SetDirty(progObj);
            saveScene = true;
        }
        else
        {
            if (sceneProgression.LevelData == null)
            {
                sceneProgression.LevelData = levelData;
                EditorUtility.SetDirty(sceneProgression);
                saveScene = true;
            }
            if (sceneProgression.EnsureVFXSetup())
            {
                EditorUtility.SetDirty(sceneProgression);
                saveScene = true;
            }
        }

        // 4. Davranışsal Doğrulama: Geçici JSON dosya yolu ile Save/Load/Reset döngüsü
        string tempSavePath = Path.Combine(Application.temporaryCachePath, "autodev_t017_save_" + System.Guid.NewGuid().ToString("N") + ".json");

        try
        {
            sceneSaveManager.CustomSavePath = tempSavePath;
            sceneSaveManager.ResetData(true);

            if (File.Exists(tempSavePath))
            {
                return AutoDevCheckResult.Fail("ResetData(true) diskteki test dosyasini silemedi.");
            }

            // Sayısal veri yaz ve kaydet
            sceneSaveManager.CurrentData.walletCoins = 60;
            sceneSaveManager.CurrentData.highScore = 150;
            sceneSaveManager.CurrentData.totalRescuedChicks = 3;
            sceneSaveManager.CurrentData.SetLevelStars("Level_Demo", 2);

            bool saveOk = sceneSaveManager.Save();
            if (!saveOk || !File.Exists(tempSavePath))
            {
                return AutoDevCheckResult.Fail("SaveManager.Save() basarisiz oldu veya JSON dosyasi diske yazilamadi.");
            }

            string rawJson = File.ReadAllText(tempSavePath);
            if (!rawJson.Contains("\"walletCoins\": 60") && !rawJson.Contains("\"walletCoins\":60"))
            {
                return AutoDevCheckResult.Fail($"JSON dosyasinda walletCoins dogru serilestirilemedi: {rawJson}");
            }

            // Hafızadaki veriyi sıfırla ve diskten yükle
            sceneSaveManager.ResetData(false);
            if (sceneSaveManager.CurrentData.walletCoins != 0)
            {
                return AutoDevCheckResult.Fail("ResetData(false) bellek verisini sifirlamadi.");
            }

            GameSaveData loadedData = sceneSaveManager.Load();
            if (loadedData.walletCoins != 60 || loadedData.highScore != 150 || loadedData.totalRescuedChicks != 3)
            {
                return AutoDevCheckResult.Fail($"Yuklenen veri uyusmuyor: Wallet={loadedData.walletCoins}, HighScore={loadedData.highScore}, Chicks={loadedData.totalRescuedChicks}");
            }

            if (loadedData.GetLevelStars("Level_Demo") != 2)
            {
                return AutoDevCheckResult.Fail($"Yuklenen bolum yildizi 2 olmaliydi: {loadedData.GetLevelStars("Level_Demo")}");
            }

            // 5. Davranışsal Doğrulama: 0, 1, 2, 3 Yıldız Eşik Hesabı
            int stars0 = sceneProgression.CalculateStars(5, 0f);
            int stars1 = sceneProgression.CalculateStars(10, 5f);
            int stars2 = sceneProgression.CalculateStars(20, 18f);
            int stars3 = sceneProgression.CalculateStars(28, 32f);

            if (stars0 != 0 || stars1 != 1 || stars2 != 2 || stars3 != 3)
            {
                return AutoDevCheckResult.Fail($"Yildiz hesaplama hatasi! 0={stars0}(b:0), 1={stars1}(b:1), 2={stars2}(b:2), 3={stars3}(b:3)");
            }

            // 6. Davranışsal Doğrulama: CompleteLevel entegrasyonu ve kalıcı kaydetme
            int earned = sceneProgression.CompleteLevel(28, 35f, 2);
            if (earned != 3 || sceneProgression.EarnedStars != 3)
            {
                return AutoDevCheckResult.Fail($"CompleteLevel 3 yildiz vermeliydi: {earned}");
            }

            if (sceneSaveManager.CurrentData.walletCoins != 88) // 60 + 28 = 88
            {
                return AutoDevCheckResult.Fail($"Cuzdan altini 88 olmaliydi: {sceneSaveManager.CurrentData.walletCoins}");
            }

            if (sceneSaveManager.CurrentData.totalRescuedChicks != 5) // 3 + 2 = 5
            {
                return AutoDevCheckResult.Fail($"Kurtarilan civciv 5 olmaliydi: {sceneSaveManager.CurrentData.totalRescuedChicks}");
            }

            if (sceneSaveManager.CurrentData.GetLevelStars("Level_Demo") != 3)
            {
                return AutoDevCheckResult.Fail($"Kazanilan yildiz kayda 3 olarak gecmeliydi: {sceneSaveManager.CurrentData.GetLevelStars("Level_Demo")}");
            }

            // Diske gerçekten yazıldığını yeni bir Load() ile teyit et
            sceneSaveManager.ResetData(false);
            GameSaveData diskVerify = sceneSaveManager.Load();
            if (diskVerify.walletCoins != 88 || diskVerify.GetLevelStars("Level_Demo") != 3)
            {
                return AutoDevCheckResult.Fail($"CompleteLevel sonrasi diske yazilan veri uyumsuz: Wallet={diskVerify.walletCoins}, Stars={diskVerify.GetLevelStars("Level_Demo")}");
            }

            // Geri bildirim adımı testi
            sceneProgression.StepFeedback(0.1f);
        }
        finally
        {
            if (File.Exists(tempSavePath))
            {
                try { File.Delete(tempSavePath); } catch { }
            }

            // Orijinal durumu temizle
            sceneSaveManager.CustomSavePath = string.Empty;
            sceneSaveManager.Load();
            sceneProgression.ResetState();
        }

        return AutoDevCheckResult.Pass(
            "T017: SaveManager JSON serilestirme, LevelDataSO esik hesabi ve LevelProgressionManager basariyla dogrulandi.",
            $"DefaultLevelData olusturuldu. Stars(0..3) dogrulandi. Save/Load/Reset ve CompleteLevel testleri gecti.",
            saveScene: saveScene
        );
    }
}
