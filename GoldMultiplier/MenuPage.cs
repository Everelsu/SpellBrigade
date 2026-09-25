using System.Globalization;
using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace GoldXpMultiplier;

// Раздел в Mod Menu вместо своей вкладки «Множители». Mod Menu необязателен: этот класс
// трогаем, только если он загружен, иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        Menu.AddPage("GoldMultiplier", "Gold & XP Multiplier")
            .Toggle(() => Strings.Get(Strings.Enabled), () => GoldXpMod._enabledEntry.Value, v => Store(GoldXpMod._enabledEntry, v),
                GoldXpMod._enabledEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.Gold), SettingsTab.Presets, () => GoldXpMod._goldEntry.Value, v => Store(GoldXpMod._goldEntry, v),
                Format, GoldXpMod._goldEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.Xp), SettingsTab.Presets, () => GoldXpMod._xpEntry.Value, v => Store(GoldXpMod._xpEntry, v),
                Format, GoldXpMod._xpEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.RankXp), SettingsTab.Presets, () => GoldXpMod._rankXpEntry.Value, v => Store(GoldXpMod._rankXpEntry, v),
                Format, GoldXpMod._rankXpEntry.DefaultValue)
            .Toggle(() => Strings.Get(Strings.Reveal), () => GoldXpMod._revealEntry.Value, v => Store(GoldXpMod._revealEntry, v),
                GoldXpMod._revealEntry.DefaultValue)
            .Note(() => Strings.Get(Strings.Hint));
    }

    private static string Format(float v) => "x" + v.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Store<T>(MelonPreferences_Entry<T> entry, T value)
    {
        if (Equals(entry.Value, value)) return;
        entry.Value = value;
        GoldXpMod.SaveSettings();
    }
}
