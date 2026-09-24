using System;
using System.Collections.Generic;
using Il2Cpp;

namespace GoldXpMultiplier;

// Лечение рангов магов, испорченных версиями мода 2.0–2.1: хук на
// CharacterRankCalculator.CalculateProgress (возвращает структуру) отдавал
// игре мусор, и в сохранение попадал ранг вроде -1708645056.
// При запуске ищем явно битые ранги и берём значения этого мага из самого
// свежего нормального слота сохранения (игра хранит кольцо из 10 слотов).
internal static class SaveDoctor
{
    private const int MaxSaneRank = 1000;

    public static void RepairCorruptedRanks()
    {
        var log = GoldXpMod.Log;
        var saveManager = SingletonPersistent<SaveManager>.Instance;
        var localPlayer = SingletonPersistent<LocalPlayer>.Instance;
        var rankManager = localPlayer?.GetCharacterRankManager();
        var live = rankManager?.RankProgressPerCharacter;
        if (saveManager == null || live == null) return;

        var broken = new List<CharacterId>();
        foreach (var kv in live)
            if (!IsSane(kv.Value)) broken.Add(kv.Key);
        if (broken.Count == 0) return;

        var slots = LoadAllSlots(saveManager);
        foreach (var id in broken)
        {
            string before = $"rank {live[id].CurrentRank} / progress {live[id].ProgressTowardsNextRank}";
            int rank = 1;
            float progress = 0f;
            string source = "reset to rank 1 (no healthy save slot)";
            foreach (var (slot, data) in slots)
            {
                var ranks = data.RankProgressPerCharacter;
                if (ranks == null || !ranks.ContainsKey(id) || !IsSane(ranks[id])) continue;
                rank = ranks[id].CurrentRank;
                progress = ranks[id].ProgressTowardsNextRank;
                source = $"restored from save slot {slot} ({data.SaveTimestamp})";
                break;
            }

            RankXp.Suspended = true;
            try { rankManager.SetRankProgression(id, rank, progress); }
            finally { RankXp.Suspended = false; }
            log.Warning($"[save] repaired {id}: {before} → " +
                        $"rank {rank} / progress {progress} — {source}");
        }

        saveManager.SavePlayerData();
        log.Msg($"[save] repaired {broken.Count} character rank(s) and saved");
    }

    private static bool IsSane(CharacterRankProgress p) =>
        p != null
        && p.CurrentRank >= 0 && p.CurrentRank <= MaxSaneRank
        && p.Prestige >= 0 && p.Prestige <= MaxSaneRank
        && !float.IsNaN(p.ProgressTowardsNextRank) && !float.IsInfinity(p.ProgressTowardsNextRank)
        && p.ProgressTowardsNextRank >= 0f && p.ProgressTowardsNextRank <= 1f
        && (p.ProgressTowardsNextRank == 0f || p.ProgressTowardsNextRank >= 1e-6f);

    // Все читаемые слоты, от самого нового к самому старому
    private static List<(int slot, PlayerSaveData data)> LoadAllSlots(SaveManager saveManager)
    {
        var result = new List<(int, PlayerSaveData)>();
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            try
            {
                var store = saveManager.CreateManagerForSlot(new RollingSaveSlot(i));
                if (store == null || !store.SaveExists()) continue;
                var data = store.Load<PlayerSaveData>(SaveManager.SaveKey);
                if (data != null) result.Add((i, data));
            }
            catch (Exception e)
            {
                GoldXpMod.Log.Warning($"[save] can't read slot {i}: {e.GetType().Name}: {e.Message}");
            }
        }
        result.Sort((a, b) => b.Item2.SaveTimestamp.Ticks.CompareTo(a.Item2.SaveTimestamp.Ticks));
        return result;
    }
}
