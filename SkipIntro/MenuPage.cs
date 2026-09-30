using System;
using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace SkipIntro;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register(MelonPreferences_Entry<bool> enabled, Action save)
    {
        Menu.AddPage("SkipIntro", "Skip Intro")
            .Toggle(() => Strings.Get(Strings.Enabled), () => enabled.Value,
                v =>
                {
                    if (enabled.Value == v) return;
                    enabled.Value = v;
                    save();
                },
                enabled.DefaultValue);
    }
}
