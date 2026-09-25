using System.Runtime.CompilerServices;
using SpellBrigade.ModMenu;

namespace KeybindsUnlocked;

// Раздел в Mod Menu. Mod Menu необязателен: этот класс трогаем, только если он загружен,
// иначе ModMenu.dll даже не понадобится.
internal static class MenuPage
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register()
    {
        Menu.AddPage("KeybindsUnlocked", "Keybinds Unlocked")
            .Button(() => Strings.Get(Strings.ResetAll), () => { if (!Bindings.IsRebinding) Bindings.ResetAll(); });
    }
}
