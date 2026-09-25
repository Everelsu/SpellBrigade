using System;
using Il2Cpp;

namespace KeybindsUnlocked;

// Своя привязка «Показать / скрыть заклинания». Настройка игры «Видимость заклинаний»
// (GlobalAlphaSetting) применяется со значением 0, а повторное нажатие возвращает
// сохранённое значение. В сохранение игры ничего не пишется — слайдер в настройках
// остаётся как был.
internal static class SpellVisibility
{
    private static bool _hidden;

    public static void Toggle()
    {
        var setting = SingletonPersistent<SettingsManager>.Instance?.GlobalAlphaSetting;
        if (setting == null) return;
        try
        {
            if (_hidden) setting.ApplySavedValue();
            else setting.Apply(Zero(setting.GetSavedValue()));
            _hidden = !_hidden;
            KeybindsMod.Log.Msg(_hidden ? "spells hidden" : "spells visible");
        }
        catch (Exception e) { KeybindsMod.Log.Warning($"spell visibility: {e.Message}"); }
    }

    // Ноль того же типа, что хранит игра (float, int или double)
    private static Il2CppSystem.Object Zero(Il2CppSystem.Object saved)
    {
        string type = saved != null ? saved.GetIl2CppType().Name : "Single";
        return type switch
        {
            "Int32" => Box(0),
            "Double" => Box(0d),
            _ => Box(0f),
        };
    }

    // Упаковать значение в объект IL2CPP (object в сигнатуре Apply)
    private static unsafe Il2CppSystem.Object Box(float value) => Box(Il2CppInterop.Runtime.Il2CppClassPointerStore<float>.NativeClassPtr, (IntPtr)(&value));
    private static unsafe Il2CppSystem.Object Box(int value) => Box(Il2CppInterop.Runtime.Il2CppClassPointerStore<int>.NativeClassPtr, (IntPtr)(&value));
    private static unsafe Il2CppSystem.Object Box(double value) => Box(Il2CppInterop.Runtime.Il2CppClassPointerStore<double>.NativeClassPtr, (IntPtr)(&value));

    private static Il2CppSystem.Object Box(IntPtr klass, IntPtr data) =>
        new(Il2CppInterop.Runtime.IL2CPP.il2cpp_value_box(klass, data));
}
