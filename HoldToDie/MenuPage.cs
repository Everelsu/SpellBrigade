using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace HoldToDie;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    // клавиши, которые игра не занимает; своя из MelonPreferences.cfg добавится в конец
    private static readonly List<string> KeyNames = new() { "K", "L", "Backspace", "Delete", "End", "Insert", "F8", "F9", "F10" };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        var page = Menu.AddPage("HoldToDie", "Hold to Die")
            .Toggle(() => Strings.Get(Strings.Enabled), () => HoldToDieMod.EnabledEntry.Value, v => Set(HoldToDieMod.EnabledEntry, v),
                HoldToDieMod.EnabledEntry.DefaultValue);
        // с Keybinds Unlocked клавиша (и кнопка контроллера) — в «Настройки → Управление»
        if (MelonBase.FindMelon("Keybinds Unlocked", "Relsev") != null)
            page.Note(() => Strings.Get(Strings.KeyInControls));
        else
            page.Choice(() => Strings.Get(Strings.Key), () => KeyOptions(), () => KeyOptions().IndexOf(HoldToDieMod.KeyEntry.Value),
                i => Set(HoldToDieMod.KeyEntry, KeyOptions()[i]), KeyNames.IndexOf(HoldToDieMod.KeyEntry.DefaultValue));
        page.Number(() => Strings.Get(Strings.HoldTime), 0.5f, 5f, 0.5f, () => HoldToDieMod.HoldTimeEntry.Value,
                v => Set(HoldToDieMod.HoldTimeEntry, v),
                v => string.Format(Strings.Get(Strings.Seconds), v.ToString("0.0", CultureInfo.InvariantCulture)),
                HoldToDieMod.HoldTimeEntry.DefaultValue)
            .Choice(() => Strings.Get(Strings.Mode), () => new[] { Strings.Get(Strings.Revivable), Strings.Get(Strings.NoRevives) },
                () => HoldToDieMod.NoRevivesEntry.Value ? 1 : 0, i => Set(HoldToDieMod.NoRevivesEntry, i == 1), 0)
            .Note(() => Strings.Get(Strings.Hint));
    }

    private static List<string> KeyOptions()
    {
        string current = HoldToDieMod.KeyEntry.Value;
        if (KeyNames.Contains(current)) return KeyNames;
        var options = new List<string>(KeyNames) { current };
        return options;
    }

    private static void Set<T>(MelonPreferences_Entry<T> entry, T value)
    {
        if (Equals(entry.Value, value)) return;
        entry.Value = value;
        HoldToDieMod.Save();
    }
}
