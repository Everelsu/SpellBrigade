using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(KeybindsUnlocked.KeybindsMod), "Keybinds Unlocked", "1.0.1", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

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
        bool list = Patch(typeof(SettingsPanel), "SetupSubPanels", typeof(ControlsUi), postfix: nameof(ControlsUi.InjectPostfix));
        bool prompts = Patch(typeof(ButtonPromptUpdater), "UpdateIcon", typeof(Prompts), postfix: nameof(Prompts.UpdateIconPostfix));
        if (list && prompts) Log.Msg("ready — Options → Controls");
        else Log.Warning($"partially loaded: key list {(list ? "ok" : "unavailable")}, on-screen key icons {(prompts ? "ok" : "unavailable")}");
    }

    public override void OnUpdate()
    {
        try
        {
            Bindings.Tick();
            ControlsUi.Tick();
            Prompts.Tick();
        }
        catch (Exception e) { Log.Error(e.ToString()); }
    }

    private bool Patch(Type target, string method, Type patchClass, string prefix = null, string postfix = null)
    {
        string name = $"{target.Name}.{method}";
        try
        {
            MethodInfo original = AccessTools.Method(target, method)
                                  ?? throw new MissingMethodException(target.FullName, method);

            // IL2CPP склеивает одинаковый машинный код разных методов: хук такого метода
            // задел бы и все его «двойники». Такие методы не трогаем.
            var sharedWith = SharedCodeGuard.FindMethodsSharingCode(original);
            if (sharedWith.Count > 0)
            {
                Log.Warning($"skipped {name}: its native code is shared with {sharedWith.Count} other method(s)");
                return false;
            }

            HarmonyInstance.Patch(original,
                prefix:  prefix  != null ? new HarmonyMethod(AccessTools.Method(patchClass, prefix))  : null,
                postfix: postfix != null ? new HarmonyMethod(AccessTools.Method(patchClass, postfix)) : null);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"failed to hook {name}: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }
}
