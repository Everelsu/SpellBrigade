using System;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Preferences;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(SpellBrigade.ModMenu.ModMenuMod), "Mod Menu", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

namespace SpellBrigade.ModMenu;

public class ModMenuMod : MelonMod
{
    internal static MelonLogger.Instance Log;

    // UserData/MelonPreferences.cfg, секция [ModMenu]: какие разделы развёрнуты
    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<string> ExpandedEntry;

    internal static void Save() => _category.SaveToFile(false);

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _category = MelonPreferences.CreateCategory("ModMenu", "Mod Menu");
        ExpandedEntry = _category.CreateEntry("Expanded", "", "Expanded Sections",
            "Развёрнутые разделы в Настройки → Моды (id через запятую); остальные свёрнуты");
        _category.SaveToFile(false);

        // Пока печатаешь в поиске, навигатор игры не должен перехватывать выделение
        // (увидев клавиатуру, он выделяет первый элемент панели — и поле теряет фокус)
        Hooks.Patch(HarmonyInstance, Log, typeof(UINavigator), "Update", typeof(MenuTab), prefix: nameof(MenuTab.NavigatorUpdatePrefix));
        // Вкладка «Моды» в окне настроек (главное меню и пауза)
        bool inject = Hooks.Patch(HarmonyInstance, Log, typeof(SettingsPanel), "SetupSubPanels",
                                  typeof(MenuTab), prefix: nameof(MenuTab.InjectPrefix), postfix: nameof(MenuTab.LinkTabButtonsPostfix));
        if (inject) Log.Msg("ready — Options → Mods");
    }

    public override void OnUpdate()
    {
        WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты
        try { MenuTab.Tick(); }
        catch (Exception e) { Log.Error($"menu: {e}"); }
    }
}
