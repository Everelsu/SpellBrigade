using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Preferences;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(GoldXpMultiplier.GoldXpMod), "Gold & XP Multiplier", "2.5.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]
[assembly: MelonOptionalDependencies("ModMenu")] // без Mod Menu — своя вкладка «Множители»

namespace GoldXpMultiplier;

public class GoldXpMod : MelonMod
{
    public static MelonLogger.Instance Log;

    // Настройки — UserData/MelonPreferences.cfg, секция [GoldMultiplier] (имя секции
    // и ключ "Multiplier" остались от 1.x, чтобы при обновлении настройки сохранились).
    // Менять удобнее в игре: Настройки → Множители.
    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<bool>  _enabledEntry;
    internal static MelonPreferences_Entry<float> _goldEntry;
    internal static MelonPreferences_Entry<float> _xpEntry;
    internal static MelonPreferences_Entry<float> _rankXpEntry;
    internal static MelonPreferences_Entry<bool>  _revealEntry;

    public static bool  Enabled          => _enabledEntry?.Value ?? true;
    public static float GoldMultiplier   => Sanitize(_goldEntry?.Value);
    public static float XpMultiplier     => Sanitize(_xpEntry?.Value);
    public static float RankXpMultiplier => Sanitize(_rankXpEntry?.Value);
    public static bool  Reveal           => _revealEntry?.Value ?? true;

    private int _hooksInstalled, _hooksTotal;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        var range = new ValueRange<float>(0f, 1000f);
        _category = MelonPreferences.CreateCategory("GoldMultiplier", "Gold & XP Multiplier");
        _enabledEntry = _category.CreateEntry("Enabled", true, "Enabled",
            "Включён ли мод");
        _goldEntry = _category.CreateEntry("Multiplier", 2.0f, "Gold Multiplier",
            "Множитель золота за забег (1.0 = ваниль). Умножается и база, и все бонусы", validator: range);
        _xpEntry = _category.CreateEntry("XpMultiplier", 2.0f, "Run XP Multiplier",
            "Множитель опыта в забеге (1.0 = ваниль). В коопе работает, если мод стоит у хоста", validator: range);
        _rankXpEntry = _category.CreateEntry("RankXpMultiplier", 1.0f, "Wizard Rank XP Multiplier",
            "Множитель опыта ранга мага за забег (1.0 = ваниль)", validator: range);
        _revealEntry = _category.CreateEntry("Reveal", true, "End-of-run x2 Reveal",
            "Экран итогов: сначала ванильные числа, потом удар множителя. false — сразу итоговые числа");
        _category.SaveToFile(false);

        WarnAboutOldVersion();
        ApplyPatches();
        Log.Msg($"{_hooksInstalled}/{_hooksTotal} hooks — gold x{GoldMultiplier}, run xp x{XpMultiplier}, " +
                $"rank xp x{RankXpMultiplier}{(Enabled ? "" : " (disabled)")}. Settings: Options → Multipliers");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (sceneName == "StartMenu") MelonCoroutines.Start(RepairSaveLater());
    }

    private static System.Collections.IEnumerator RepairSaveLater()
    {
        yield return new UnityEngine.WaitForSeconds(2f);
        try { SaveDoctor.RepairCorruptedRanks(); }
        catch (Exception e) { Log.Error($"rank repair failed: {e}"); }
    }

    internal static void SaveSettings() => _category.SaveToFile(false);

    public override void OnUpdate() => WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты

    private static float Sanitize(float? value)
    {
        float v = value ?? 1.0f;
        return float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? 1.0f : v;
    }

    // До 2.5.0 мод назывался SpellBrigadeGoldMultiplier.dll — если старый файл остался
    // рядом, оба мода умножали бы одно и то же.
    private static void WarnAboutOldVersion()
    {
        if (MelonBase.RegisteredMelons.Any(m => m.Info.Name == "SpellBrigadeGoldMultiplier"))
            Log.Warning("Old version found: delete Mods\\SpellBrigadeGoldMultiplier.dll, otherwise rewards are multiplied twice");
    }

    // Хуки ставятся по одному: если после обновления игры какой-то метод пропадёт,
    // остальное продолжит работать, а в логе будет понятная ошибка.
    private void ApplyPatches()
    {
        Patch(typeof(RunGoldCalculator), "Calculate", typeof(GoldPatch), postfix: nameof(GoldPatch.Postfix));

        // TryAddExperienceAndSkipStores не трогаем: в релизе это пустая отладочная
        // заглушка, её машинный код общий с ~2000 других пустых методов.
        Patch(typeof(PartyLevelManager), "TryAddExperience", typeof(XpPatch), prefix: nameof(XpPatch.Prefix));

        // Опыт ранга мага — только методы без структур в возвращаемом значении (см. RankXp)
        Patch(typeof(CharacterRankProgression), "SetRankProgression", typeof(RankXp), prefix: nameof(RankXp.SetRankProgressionPrefix));
        Patch(typeof(RunRecordFactory), "CalculateRankProgression", typeof(RankXp), postfix: nameof(RankXp.CalculateRankProgressionPostfix));

        // Экран итогов: сначала ванильные числа, потом удар множителя
        Patch(typeof(GameOverMissionStatsPanel), "SetValues", typeof(RunSummaryUi), postfix: nameof(RunSummaryUi.SetValuesPostfix));
        Patch(typeof(GameOverMissionStatsPanel), "SetXPEarned", typeof(RunSummaryUi), prefix: nameof(RunSummaryUi.SetXpEarnedPrefix));

        // Настройки: раздел в Mod Menu, если он установлен, иначе своя вкладка в меню настроек игры
        bool inModMenu = false;
        if (FindMelon("Mod Menu", "Relsev") != null)
            try { MenuPage.Register(); inModMenu = true; }
            catch (Exception e) { Log.Warning($"Mod Menu page: {e.Message} — using own settings tab"); }
        if (!inModMenu)
            Patch(typeof(SettingsPanel), "SetupSubPanels", typeof(SettingsTab),
                  prefix: nameof(SettingsTab.InjectPrefix), postfix: nameof(SettingsTab.LinkTabButtonsPostfix));
    }

    private void Patch(Type target, string method, Type patchClass, string prefix = null, string postfix = null)
    {
        _hooksTotal++;
        string name = $"{target.Name}.{method}";
        MethodInfo original = AccessTools.Method(target, method);

        // Хуки методов, возвращающих структуры (ValueTuple и т.п.), Il2CppInterop
        // обрабатывает неверно: игра получает мусор вместо результата.
        if (original != null && ReturnsStruct(original))
        {
            Log.Warning($"skipped {name}: it returns a struct ({original.ReturnType.Name}), hooking it corrupts the result");
            return;
        }

        if (Hooks.Patch(HarmonyInstance, Log, original, patchClass, prefix, postfix, name)) _hooksInstalled++;
    }

    private static bool ReturnsStruct(MethodInfo method)
    {
        Type type = method.ReturnType;
        if (type == typeof(void) || type.IsPrimitive || type.IsEnum) return false;
        return type.IsValueType || typeof(Il2CppSystem.ValueType).IsAssignableFrom(type);
    }
}
