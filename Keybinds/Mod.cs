using System;
using Il2Cpp;
using MelonLoader;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(KeybindsUnlocked.KeybindsMod), "Keybinds Unlocked", "1.0.1", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]
[assembly: MelonOptionalDependencies("ModMenu")] // без Mod Menu мод работает как раньше

namespace KeybindsUnlocked;

public class KeybindsMod : MelonMod
{
    public static MelonLogger.Instance Log;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Bindings.Init();
        Bindings.Changed += Prompts.RefreshAll;
        Bindings.Applied += Prompts.RefreshAll;
        bool list = Hooks.Patch(HarmonyInstance, Log, typeof(SettingsPanel), "SetupSubPanels", typeof(ControlsUi), postfix: nameof(ControlsUi.InjectPostfix));
        bool prompts = Hooks.Patch(HarmonyInstance, Log, typeof(ButtonPromptUpdater), "UpdateIcon", typeof(Prompts), postfix: nameof(Prompts.UpdateIconPostfix));
        if (list && prompts) Log.Msg("ready — Options → Controls");
        else Log.Warning($"partially loaded: key list {(list ? "ok" : "unavailable")}, on-screen key icons {(prompts ? "ok" : "unavailable")}");

        if (FindMelon("Mod Menu", "Relsev") != null)
            try { MenuPage.Register(); }
            catch (Exception e) { Log.Warning($"Mod Menu page: {e.Message}"); }
    }

    public override void OnUpdate()
    {
        WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты
        try
        {
            Bindings.Tick();
            ControlsUi.Tick();
            Prompts.Tick();
            // своя клавиша; пока печатают в поиске Mod Menu — молчит
            if (Bindings.ToggleSpellsPressed() && AppDomain.CurrentDomain.GetData("SpellBrigade.Typing") is not true)
                SpellVisibility.Toggle();
        }
        catch (Exception e) { Log.Error(e.ToString()); }
    }
}
