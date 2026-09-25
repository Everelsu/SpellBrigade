using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace RunCustomizer;

// Страница в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        Menu.AddPage("RunCustomizer", "Run Customizer")
            .Choice(() => Strings.Get(Strings.Difficulty),
                () => new[] { Strings.Get(Strings.Standard), Strings.Get(Strings.RandomName), Strings.Get(Strings.CustomName) },
                () => Modes.Selected switch { Mode.Vanilla => 0, Mode.Custom => 2, _ => 1 },
                i => Modes.Selected = i switch { 0 => Mode.Vanilla, 2 => Mode.Custom, _ => RandomMode },
                @default: 0)
            .Choice(() => Strings.Get(Strings.RandomKind),
                () => new[] { Strings.Get(Strings.KindAll), Strings.Get(Strings.KindBase) },
                () => RunCustomizerMod.RandomBaseOnly.Value ? 1 : 0,
                i =>
                {
                    bool baseOnly = i == 1;
                    if (RunCustomizerMod.RandomBaseOnly.Value == baseOnly) return;
                    RunCustomizerMod.RandomBaseOnly.Value = baseOnly;
                    RunCustomizerMod.Save();
                    if (Modes.Selected is Mode.Random or Mode.RandomBase) Modes.Selected = RandomMode;
                },
                @default: RunCustomizerMod.RandomBaseOnly.DefaultValue ? 1 : 0)
            .Header(() => Strings.Get(Strings.CustomName))
            .Number(() => Strings.Get(Strings.Enemy), 0.1f, 10f, 0.05f, () => RunCustomizerMod.CustomEnemy.Value, v => Set(RunCustomizerMod.CustomEnemy, v),
                Multiplier, RunCustomizerMod.CustomEnemy.DefaultValue)
            .Number(() => Strings.Get(Strings.Spawn), 0.1f, 10f, 0.05f, () => RunCustomizerMod.CustomSpawnSpeed.Value, v => Set(RunCustomizerMod.CustomSpawnSpeed, v),
                Multiplier, RunCustomizerMod.CustomSpawnSpeed.DefaultValue)
            .Number(() => Strings.Get(Strings.Count), 0.1f, 10f, 0.05f, () => RunCustomizerMod.CustomEnemyCount.Value, v => Set(RunCustomizerMod.CustomEnemyCount, v),
                Multiplier, RunCustomizerMod.CustomEnemyCount.DefaultValue)
            .Number(() => Strings.Get(Strings.Health), 0f, 10f, 0.05f, () => RunCustomizerMod.CustomHealth.Value, v => Set(RunCustomizerMod.CustomHealth, v),
                Multiplier, RunCustomizerMod.CustomHealth.DefaultValue);
    }

    private static Mode RandomMode => RunCustomizerMod.RandomBaseOnly.Value ? Mode.RandomBase : Mode.Random;

    private static string Multiplier(float v) => "x" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static void Set(MelonPreferences_Entry<float> entry, float value)
    {
        if (entry.Value == value) return;
        entry.Value = value;
        RunCustomizerMod.Save();
    }
}
