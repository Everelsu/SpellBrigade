using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace MorePlayers;

// Каждый подключившийся игрок получает цвет из пула SessionManager, а в палитре игры
// только 4 цвета — пятому достался бы пустой пул. Дополняем палитру до HardCap.
internal static class PlayerColors
{
    private static readonly Color[] Extra =
    {
        new(1.00f, 0.55f, 0.10f), new(0.20f, 0.85f, 0.95f), new(0.60f, 0.95f, 0.25f), new(0.95f, 0.35f, 0.85f),
        new(1.00f, 0.85f, 0.20f), new(0.60f, 0.45f, 1.00f), new(0.15f, 0.75f, 0.60f), new(1.00f, 0.55f, 0.65f),
        new(0.90f, 0.90f, 0.90f), new(0.95f, 0.45f, 0.35f), new(0.45f, 0.70f, 1.00f), new(0.75f, 0.75f, 0.30f),
    };

    public static void Ensure()
    {
        var session = SingletonPersistent<SessionManager>.Instance;
        if (session == null) return;
        var palette = session.playerColors;
        if (palette == null || palette.Count == 0 || palette.Count >= MorePlayersMod.HardCap) return;

        var added = new List<Color>();
        foreach (var c in Extra)
        {
            if (palette.Count >= MorePlayersMod.HardCap) break;
            if (Contains(palette, c)) continue;
            palette.Add(c);
            added.Add(c);
        }
        // сессия уже идёт — пул собран из старой палитры, новые цвета кладём и туда
        var pool = session.playerColorsPool;
        if (pool != null)
            foreach (var c in added)
                if (!Contains(pool, c)) pool.Add(c);
    }

    private static bool Contains(Il2CppSystem.Collections.Generic.List<Color> list, Color c)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var x = list[i];
            if (Mathf.Abs(x.r - c.r) < 0.01f && Mathf.Abs(x.g - c.g) < 0.01f && Mathf.Abs(x.b - c.b) < 0.01f) return true;
        }
        return false;
    }
}
