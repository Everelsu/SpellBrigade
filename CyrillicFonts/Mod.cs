using System;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

[assembly: MelonInfo(typeof(CyrillicFonts.CyrillicFontsMod), "Cyrillic Fonts", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

namespace CyrillicFonts;

// Шрифты игры (Irish Grover — заголовки и кнопки, Titillium Web — остальной текст) без
// кириллицы: русские и украинские буквы TextMeshPro берёт из запасного NotInter, и вся
// стилистика пропадает. Мод ставит первыми в запасные шрифты те же шрифты с кириллицей:
// Titillium Web [RUS by Daymarius] и Irish Grover Cyrillic — кириллица, собранная из глифов
// самого Irish Grover (tools/build_irish_cyrillic.py). Латиница остаётся оригинальной.
public class CyrillicFontsMod : MelonMod
{
    public static MelonLogger.Instance Log;

    // UserData/MelonPreferences.cfg, секция [CyrillicFonts]
    internal static MelonPreferences_Entry<bool> EnabledEntry;
    internal static MelonPreferences_Entry<float> HeadingScaleEntry, TextScaleEntry;

    private float _nextScan;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        var category = MelonPreferences.CreateCategory("CyrillicFonts", "Cyrillic Fonts");
        EnabledEntry = category.CreateEntry("Enabled", true, "Enabled", "Кириллица шрифтами в стиле игры");
        // ручки подстройки: размер кириллицы относительно латиницы (1 — подобрано автоматически по высоте заглавных)
        HeadingScaleEntry = category.CreateEntry("HeadingScale", 1f, "Heading Scale",
            "Размер кириллицы в заголовках и кнопках", validator: new ValueRange<float>(0.5f, 2f));
        TextScaleEntry = category.CreateEntry("TextScale", 1f, "Text Scale",
            "Размер кириллицы в остальном тексте", validator: new ValueRange<float>(0.5f, 2f));
        category.SaveToFile(false);

        if (!EnabledEntry.Value) { Log.Msg("disabled"); return; }
        int fonts = FontFiles.Extract();
        Log.Msg(fonts > 0 ? $"ready — {fonts} font file(s)" : "no font files inside the dll — nothing to do");
    }

    // Шрифты игры грузятся вместе со сценами — проверяем новые раз в пару секунд и после загрузки сцены
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) => _nextScan = 0f;

    public override void OnUpdate()
    {
        if (!EnabledEntry.Value || Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + 2f;
        try { FontPatcher.Scan(); }
        catch (Exception e) { Log.Error(e); _nextScan = float.MaxValue; }
    }
}
