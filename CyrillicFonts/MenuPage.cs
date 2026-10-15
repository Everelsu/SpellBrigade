using System.Globalization;
using System.Runtime.CompilerServices;
using MelonLoader;
using SpellBrigade.ModMenu;

namespace CyrillicFonts;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен.
internal static class MenuPage
{
    private static readonly string[] Styles = { "Гибрид", "Классика (Irish Grover)", "Рисованный" };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        Menu.AddPage("CyrillicFonts", "Cyrillic Fonts")
            .Choice("Шрифт заголовков", Styles, () => (int)CyrillicFontsMod.HeadingStyleEntry.Value,
                i => Set(CyrillicFontsMod.HeadingStyleEntry, (HeadingStyle)i), (int)CyrillicFontsMod.HeadingStyleEntry.DefaultValue)
            .Number("Размер кириллицы в заголовках", 0.5f, 2f, 0.05f, () => CyrillicFontsMod.HeadingScaleEntry.Value,
                v => Set(CyrillicFontsMod.HeadingScaleEntry, v), Percent, 1f)
            .Number("Размер кириллицы в тексте", 0.5f, 2f, 0.05f, () => CyrillicFontsMod.TextScaleEntry.Value,
                v => Set(CyrillicFontsMod.TextScaleEntry, v), Percent, 1f)
            .Note("Гибрид — латинские по форме буквы от Irish Grover, остальные рисованные.\n" +
                  "Классика — вся кириллица собрана из букв Irish Grover. Рисованный — весь алфавит рисованный.");
    }

    private static string Percent(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static void Set<T>(MelonPreferences_Entry<T> entry, T value)
    {
        if (Equals(entry.Value, value)) return;
        entry.Value = value;
        entry.Category.SaveToFile(false);
    }
}
