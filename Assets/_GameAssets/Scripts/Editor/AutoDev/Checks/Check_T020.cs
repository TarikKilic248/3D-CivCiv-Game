using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CivCiv 3D - T020 Doğrulama: Gece/Gündüz döngüsü, Başarım sistemi ve RuntimeSystemValidator.
/// </summary>
public static class Check_T020
{
    [AutoDevCheck("T020")]
    public static AutoDevCheckResult Run()
    {
        AutoDevVerifier.EnsureSceneOpen(AutoDevVerifier.DefaultScenePath);
        bool save = false;

        // 1. Varsayılan başarım asset'leri (idempotent)
        List<AchievementSO> defs = new List<AchievementSO>
        {
            EnsureAchievement("ach_coin_collector", "Altın Avcısı", "10 altın topla!", AchievementMetric.CoinsCollected, 10),
            EnsureAchievement("ach_chick_hero", "Civciv Kahramanı", "2 civcivi kurtar!", AchievementMetric.ChicksRescued, 2),
            EnsureAchievement("ach_night_owl", "Gece Kuşu", "Bir geceyi atlat!", AchievementMetric.NightsSurvived, 1)
        };

        // 2. RuntimeSystemValidator + eksik sistemleri tamamla
        RuntimeSystemValidator validator = RuntimeSystemValidator.FindSystem<RuntimeSystemValidator>();
        if (validator == null)
        {
            GameObject vObj = new GameObject("RuntimeSystemValidator");
            Undo.RegisterCreatedObjectUndo(vObj, "Create RuntimeSystemValidator");
            validator = vObj.AddComponent<RuntimeSystemValidator>();
            save = true;
        }
        RuntimeSystemValidator.ValidationReport report = validator.ValidateAndRepair(true);
        if (report.created.Count > 0) save = true;
        if (!report.IsHealthy) return AutoDevCheckResult.Fail("Kritik sistemler eksik: " + report);
        RuntimeSystemValidator.ValidationReport second = validator.ValidateAndRepair(true);
        if (second.created.Count != 0 || second.present.Count < 8)
            return AutoDevCheckResult.Fail("Validator idempotent degil veya eksik sayim: " + second);

        AchievementManager am = RuntimeSystemValidator.FindSystem<AchievementManager>();
        DayNightCycleController dn = RuntimeSystemValidator.FindSystem<DayNightCycleController>();
        SaveManager sm = RuntimeSystemValidator.FindSystem<SaveManager>();

        // 3. AchievementManager kablolama (SerializedObject)
        SerializedObject so = new SerializedObject(am);
        SerializedProperty listProp = so.FindProperty("_achievements");
        if (listProp.arraySize == 0)
        {
            listProp.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++) listProp.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            save = true;
        }
        SerializedProperty themeProp = so.FindProperty("_theme");
        if (themeProp.objectReferenceValue == null)
        {
            UIThemeSO themeAsset = AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
            if (themeAsset != null) { themeProp.objectReferenceValue = themeAsset; save = true; }
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return AutoDevCheckResult.Fail("Canvas bulunamadi.");
        if (am.EnsureNotificationUI(canvas.transform)) save = true;
        if (am.EnsureVFXSetup()) save = true;
        if (dn.EnsureVFXSetup()) save = true;
        if (save) { EditorUtility.SetDirty(am); EditorUtility.SetDirty(dn); }

        // Tema doğrulaması
        UIThemeSO t = UIStyleHelper.ResolveTheme(am.Theme);
        Image panelImg = am.NotificationPanel != null ? am.NotificationPanel.GetComponent<Image>() : null;
        if (panelImg == null) return AutoDevCheckResult.Fail("Basarim bildirim paneli Image icermiyor.");
        bool themed = t.panelSprite != null ? panelImg.sprite == t.panelSprite : (panelImg.color == t.panelFallbackColor && panelImg.GetComponent<Outline>() != null);
        if (!themed) return AutoDevCheckResult.Fail("Bildirim paneli UIThemeSO temasini kullanmiyor.");

        // 4. Gece/Gündüz davranışı
        Light sun = dn.ResolveSun();
        if (sun == null) return AutoDevCheckResult.Fail("Directional Light bulunamadi.");
        Quaternion r0 = sun.transform.rotation; float i0 = sun.intensity; Color c0 = sun.color;
        Color amb0 = RenderSettings.ambientLight; float t0 = dn.TimeOfDay;
        int dayEvents = 0; bool lastState = true;
        Action<bool> onDay = d => { dayEvents++; lastState = d; };
        string dnInfo;
        dn.OnDayStateChanged += onDay;
        try
        {
            dn.SetTimeOfDay(0.3f);
            Quaternion rMorning = sun.transform.rotation; float iMorning = sun.intensity;
            if (!dn.IsDay) return AutoDevCheckResult.Fail("t=0.3 gunduz olmaliydi.");
            dn.Step(dn.DayLengthSeconds * 0.2f);
            float noonAngle = Quaternion.Angle(rMorning, sun.transform.rotation);
            float iNoon = sun.intensity;
            if (Mathf.Abs(dn.TimeOfDay - 0.5f) > 0.01f || noonAngle < 50f)
                return AutoDevCheckResult.Fail($"Isik rotasyonu ilerlemedi: t={dn.TimeOfDay:F3}, aci={noonAngle:F1}");
            dn.Step(dn.DayLengthSeconds * 0.4f);
            if (dn.IsDay || dayEvents < 1 || lastState) return AutoDevCheckResult.Fail($"t=0.9 gece olmaliydi (events={dayEvents}).");
            if (sun.intensity >= iNoon || sun.intensity >= iMorning)
                return AutoDevCheckResult.Fail($"Gece yogunlugu dusmedi: gece={sun.intensity:F2}, ogle={iNoon:F2}");
            dnInfo = $"noonAngle={noonAngle:F1}, iNoon={iNoon:F2}, iNight={sun.intensity:F2}";
        }
        finally
        {
            dn.OnDayStateChanged -= onDay;
            dn.SetTimeOfDay(t0);
            sun.transform.rotation = r0; sun.intensity = i0; sun.color = c0;
            RenderSettings.ambientLight = amb0;
        }

        // 5. Başarım açılışı + SaveManager senkronu
        string tempPath = Path.Combine(Application.temporaryCachePath, "autodev_t020_" + Guid.NewGuid().ToString("N") + ".json");
        AchievementSO coinAch = defs[0];
        int unlockEvents = 0;
        Action<AchievementSO> onUnlock = a => unlockEvents++;
        try
        {
            sm.CustomSavePath = tempPath;
            sm.ResetData(true);
            am.SaveManagerOverride = sm;
            am.ResetProgress();
            am.OnAchievementUnlocked += onUnlock;

            am.ReportProgress(AchievementMetric.CoinsCollected, 9);
            if (am.IsUnlocked(coinAch) || unlockEvents != 0) return AutoDevCheckResult.Fail("Hedefe ulasmadan basarim acildi.");
            am.ReportProgress(AchievementMetric.CoinsCollected, 1);
            am.ReportProgress(AchievementMetric.CoinsCollected, 5);
            if (!am.IsUnlocked(coinAch) || unlockEvents != 1)
                return AutoDevCheckResult.Fail($"Basarim acilmadi veya olay sayisi hatali: {unlockEvents}");
            if (!am.IsNotificationShowing || !am.NotificationPanel.gameObject.activeSelf)
                return AutoDevCheckResult.Fail("Basarim bildirimi gosterilmedi.");
            am.StepNotification(10f);
            if (am.IsNotificationShowing) return AutoDevCheckResult.Fail("Bildirim sure sonunda kapanmadi.");
            if (!sm.CurrentData.unlockedAchievements.Contains(coinAch.AchievementId) || !File.ReadAllText(tempPath).Contains(coinAch.AchievementId))
                return AutoDevCheckResult.Fail("Basarim SaveManager/diske senkronlanmadi.");

            am.ResetProgress();
            sm.ResetData(false);
            sm.Load();
            am.LoadFromSave();
            if (!am.IsUnlocked(coinAch)) return AutoDevCheckResult.Fail("Kayittan basarim geri yuklenemedi.");
        }
        finally
        {
            am.OnAchievementUnlocked -= onUnlock;
            am.ResetProgress();
            am.SaveManagerOverride = null;
            if (File.Exists(tempPath)) { try { File.Delete(tempPath); } catch { } }
            sm.CustomSavePath = string.Empty;
            sm.Load();
        }

        return AutoDevCheckResult.Pass(
            "T020: Gece/gunduz isik dongusu, basarim acilisi + kayit senkronu ve RuntimeSystemValidator dogrulandi.",
            $"{dnInfo} | {second}", saveScene: save);
    }

    private static AchievementSO EnsureAchievement(string id, string name, string desc, AchievementMetric metric, int target)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_GameAssets/Data")) AssetDatabase.CreateFolder("Assets/_GameAssets", "Data");
        if (!AssetDatabase.IsValidFolder(AchievementSO.DefaultFolder)) AssetDatabase.CreateFolder("Assets/_GameAssets/Data", "Achievements");
        string path = $"{AchievementSO.DefaultFolder}/{id}.asset";
        AchievementSO a = AssetDatabase.LoadAssetAtPath<AchievementSO>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<AchievementSO>();
        a.AchievementId = id; a.DisplayName = name; a.Description = desc; a.Metric = metric; a.TargetValue = target;
        AssetDatabase.CreateAsset(a, path);
        AssetDatabase.SaveAssets();
        return a;
    }
}
