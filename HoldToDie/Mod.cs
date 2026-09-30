using System;
using MelonLoader;
using MelonLoader.Preferences;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(HoldToDie.HoldToDieMod), "Hold to Die", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]
// без Mod Menu настройки — в MelonPreferences.cfg; с Keybinds Unlocked клавиша — в его списке управления
[assembly: MelonOptionalDependencies("ModMenu", "KeybindsUnlocked")]

namespace HoldToDie;

public class HoldToDieMod : MelonMod
{
    public static MelonLogger.Instance Log;

    // UserData/MelonPreferences.cfg, секция [HoldToDie]
    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<bool> EnabledEntry, NoRevivesEntry;
    internal static MelonPreferences_Entry<string> KeyEntry, GamepadEntry;
    internal static MelonPreferences_Entry<float> HoldTimeEntry;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _category = MelonPreferences.CreateCategory("HoldToDie", "Hold to Die");
        EnabledEntry = _category.CreateEntry("Enabled", true, "Enabled", "Удерживать клавишу в забеге — умереть");
        KeyEntry = _category.CreateEntry("Key", "K", "Key",
            "Клавиша, названия как в Unity Input System: K, Backspace, Delete, End, F9…");
        GamepadEntry = _category.CreateEntry("GamepadButton", "L3", "Controller Button",
            "Кнопка контроллера: L3, R3, Select или None. С Keybinds Unlocked клавиша и кнопка — в его списке управления");
        HoldTimeEntry = _category.CreateEntry("HoldTime", 2f, "Hold Time", "Сколько секунд держать клавишу",
            validator: new ValueRange<float>(0.5f, 5f));
        NoRevivesEntry = _category.CreateEntry("NoRevives", false, "No Revives",
            "true — без воскрешений: если вы последний живой (соло или хост), забег сразу заканчивается");
        _category.SaveToFile(false);

        // Без Keybinds Unlocked клавиша видна в родном «Настройки → Управление»
        Hooks.Patch(HarmonyInstance, Log, typeof(Il2Cpp.SettingsPanel), nameof(Il2Cpp.SettingsPanel.SetupSubPanels),
                    typeof(ControlsRow), postfix: nameof(ControlsRow.InjectPostfix));

        Log.Msg("ready — hold the key in a run to die");

        if (FindMelon("Mod Menu", "Relsev") != null)
            try { MenuPage.Register(); }
            catch (Exception e) { Log.Warning($"Mod Menu page: {e.Message}"); }
    }

    internal static void Save() => _category.SaveToFile(false);

    public override void OnUpdate()
    {
        WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты
        try { Death.Tick(); }
        catch (Exception e) { Log.Error(e); Death.Reset(); }
        try { ControlsRow.Tick(); }
        catch (Exception e) { Log.Warning($"Options → Controls row: {e.Message}"); }
    }
}
