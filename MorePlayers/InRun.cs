using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace MorePlayers;

// Места в забеге, где игра рассчитана ровно на 4 игроков
internal static class InRun
{
    // На уровне 4 точки появления, а игра берёт spawnPoints[номер игрока] — на пятом
    // старт уровня падал бы у всех. Лишним игрокам даём те же точки по кругу.
    public static void GameplayInitializationPrefix(LevelStarter __instance)
    {
        try
        {
            var points = __instance.spawnPoints;
            if (points == null || points.Count == 0) return;
            int original = points.Count;
            for (int i = original; i < MorePlayersMod.HardCap; i++) points.Add(points[i % original]);
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"spawn points: {e.Message}"); }
    }

    // «Загнать существ»: сколько целей на сколько игроков — таблица на 1–4.
    // Для 5+ продолжаем её с тем же шагом, что между 3 и 4 игроками.
    public static void HerdTargetsPrefix(HerdingObjectiveManager __instance)
    {
        try
        {
            var table = __instance.herdTargetsForPlayerCounts;
            if (table == null || table.Length == 0) return;

            int maxCount = 0, maxTargets = 0, prevTargets = 0;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i]._PlayerCount_k__BackingField == MorePlayersMod.HardCap) return; // уже дополнена
                if (table[i]._PlayerCount_k__BackingField > maxCount)
                {
                    maxCount = table[i]._PlayerCount_k__BackingField;
                    maxTargets = table[i]._HerdTargetCount_k__BackingField;
                }
            }
            for (int i = 0; i < table.Length; i++)
                if (table[i]._PlayerCount_k__BackingField == maxCount - 1) prevTargets = table[i]._HerdTargetCount_k__BackingField;
            int step = Math.Max(1, maxTargets - prevTargets);

            var extended = new Il2CppStructArray<HerdingObjectiveManager.HerdTargetsForPlayerCount>(table.Length + MorePlayersMod.HardCap - maxCount);
            for (int i = 0; i < table.Length; i++) extended[i] = table[i];
            for (int count = maxCount + 1, i = table.Length; count <= MorePlayersMod.HardCap; count++, i++)
            {
                var entry = table[0];
                entry._PlayerCount_k__BackingField = count;
                entry._HerdTargetCount_k__BackingField = maxTargets + step * (count - maxCount);
                extended[i] = entry;
            }
            __instance.herdTargetsForPlayerCounts = extended;
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"herding targets: {e.Message}"); }
    }
}
