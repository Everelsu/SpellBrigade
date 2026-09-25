using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace PinnedChallenges;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    private static bool _registered;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        _registered = true;
        Menu.AddPage("PinnedChallenges", "Pinned Challenges")
            .Number(() => Strings.Get(Strings.HudScale), 0.5f, 2f, 0.05f,
                () => PinnedChallengesMod.HudScaleEntry.Value, v => Set(PinnedChallengesMod.HudScaleEntry, v),
                Percent, PinnedChallengesMod.HudScaleEntry.DefaultValue)
            .Toggle(() => Strings.Get(Strings.GameStyle),
                () => PinnedChallengesMod.GameStyleEntry.Value,
                v =>
                {
                    PinnedChallengesMod.GameStyleEntry.Value = v;
                    PinnedChallengesMod.Save();
                    RunHud.Rescale();
                    LayoutEditor.Restyle();
                },
                PinnedChallengesMod.GameStyleEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.Opacity), 0f, 1f, 0.05f,
                () => PinnedChallengesMod.OpacityEntry.Value, v => Set(PinnedChallengesMod.OpacityEntry, v),
                Percent, PinnedChallengesMod.OpacityEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.TextOpacity), 0.1f, 1f, 0.05f,
                () => PinnedChallengesMod.TextOpacityEntry.Value, v => Set(PinnedChallengesMod.TextOpacityEntry, v),
                Percent, PinnedChallengesMod.TextOpacityEntry.DefaultValue)
            .Number(() => Strings.Get(Strings.MaxPinned), 1f, 5f, 1f,
                () => PinnedChallengesMod.MaxPinsEntry.Value,
                v =>
                {
                    PinnedChallengesMod.MaxPinsEntry.Value = (int)v;
                    PinnedChallengesMod.Save();
                    Pins.Trim();
                },
                v => v.ToString("0"), PinnedChallengesMod.MaxPinsEntry.DefaultValue)
            .Buttons(new MenuButton(() => Strings.Get(Strings.Position), LayoutEditor.Open),
                     new MenuButton(() => Strings.Get(Strings.UnpinAll), Pins.Clear));
    }

    // Значения поменялись не из меню (редактор положения) — перечитать строки
    public static void Refresh()
    {
        if (_registered) RefreshMenu();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefreshMenu() => Menu.Refresh();

    private static string Percent(float v) => $"{v * 100f:0}%";

    // В забеге (меню открыто из паузы) карточки меняются сразу
    private static void Set(MelonPreferences_Entry<float> entry, float value)
    {
        if (entry.Value == value) return;
        entry.Value = value;
        PinnedChallengesMod.Save();
        RunHud.Rescale();
        LayoutEditor.Restyle();
    }
}
