using System;
using System.Collections.Generic;
using Il2Cpp;

namespace PinnedChallenges;

// Список закреплённых испытаний. Храним имена ChallengeId, а не числа: имя переживёт
// обновление игры, даже если разработчики переставят значения в enum.
internal static class Pins
{
    public static int Max => Math.Clamp(PinnedChallengesMod.MaxPinsEntry?.Value ?? 3, 1, 5);

    private static readonly List<ChallengeId> Ids = new();

    public static IReadOnlyList<ChallengeId> All => Ids;

    public static event Action Changed;

    public static bool IsPinned(ChallengeId id) => Ids.Contains(id);

    // Закрепить или открепить; четвёртое закрепление вытесняет самое старое
    public static void Toggle(ChallengeId id)
    {
        if (!Ids.Remove(id))
        {
            Ids.Add(id);
            while (Ids.Count > Max) Ids.RemoveAt(0);
        }
        Save();
        PinnedChallengesMod.Log.Msg($"pinned: {(Ids.Count > 0 ? string.Join(", ", Ids) : "none")}");
    }

    // Лимит уменьшили — лишние (самые старые) открепляются
    public static void Trim()
    {
        if (Ids.Count <= Max) return;
        while (Ids.Count > Max) Ids.RemoveAt(0);
        Save();
    }

    public static void Clear()
    {
        if (Ids.Count == 0) return;
        Ids.Clear();
        Save();
    }

    public static void Remove(ChallengeId id)
    {
        if (Ids.Remove(id)) Save();
    }

    public static void Load()
    {
        Ids.Clear();
        foreach (string name in (PinnedChallengesMod.PinnedEntry.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse(name, out ChallengeId id) && !Ids.Contains(id) && Ids.Count < Max) Ids.Add(id);
            else PinnedChallengesMod.Log.Warning($"ignored pinned challenge '{name}'");
    }

    private static void Save()
    {
        PinnedChallengesMod.PinnedEntry.Value = string.Join(",", Ids);
        PinnedChallengesMod.Save();
        try { Changed?.Invoke(); }
        catch (Exception e) { PinnedChallengesMod.Log.Warning($"pins changed: {e.Message}"); }
    }
}
