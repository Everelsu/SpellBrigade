using System.Runtime.CompilerServices;
using SpellBrigade.ModMenu;

namespace MorePlayers;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        Menu.AddPage("MorePlayers", "More Players")
            .Number(() => Strings.Get(Strings.MaxPlayers), MorePlayersMod.MinPlayers, MorePlayersMod.HardCap, 1f,
                () => MorePlayersMod.MaxPlayers, v => MorePlayersMod.SetMaxPlayers((int)v), v => v.ToString("0"),
                MorePlayersMod._maxEntry.DefaultValue);
    }
}
