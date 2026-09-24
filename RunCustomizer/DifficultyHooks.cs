using System;
using HarmonyLib;
using Il2Cpp;
using Unity.Netcode;
using UnityEngine;

namespace RunCustomizer;

// Сложность в игре — не набор данных, а switch по Difficulty прямо в коде (значения
// в Modes.Base*). Многие такие switch встроены в вызывающие методы, поэтому ловим
// методы, которые их используют, и пересчитываем результат под свои параметры:
// результат × (наше значение / значение базовой сложности забега).
internal static class DifficultyHooks
{
    private static int Base => Math.Clamp((int)Modes.RunDifficulty, 0, 2);

    // Здоровье и урон врагов (GetHealthMultiplier / GetAttackMultiplier зовут его)
    public static void EnemyMultiplierPostfix(ref float __result)
    {
        if (Modes.InRun) __result = Modes.Active.Enemy;
    }

    // Рост силы врагов с каждым циклом бесконечного режима — в той же пропорции
    public static void CycleMultiplierPostfix(ref float __result)
    {
        if (Modes.InRun) __result *= Modes.Active.Enemy / Modes.BaseEnemy[Base];
    }

    public static void SpawnIntervalPostfix(ref float __result)
    {
        if (Modes.InRun) __result *= Modes.BaseSpawnSpeed[Base] / Modes.Active.SpawnSpeed;
    }

    public static void MinEnemiesPostfix(ref int __result)
    {
        if (Modes.InRun)
            __result = Math.Max(1, (int)Math.Round(__result * Modes.Active.EnemyCount / Modes.BaseEnemyCount[Base]));
    }

    // PickupSpawner.DeterminePickUp целиком (множитель аптечек встроен в него).
    // Повторяет игру: шанс аптечки = кривая(здоровье игрока) × сложность × множитель дропа,
    // иначе магнит с шансом 0.0016 × множитель дропа, если на карте хватает опыта.
    public static bool DeterminePickUpPrefix(PickupSpawner __instance, float pickupChanceMultiplier,
                                             float spawningPlayerHealthPercentage, ref NetworkObject __result)
    {
        if (!Modes.InRun) return true;
        try
        {
            float roll = UnityEngine.Random.value;
            float health = 0f;
            if (HealthPickup.AmountOfHealthPickupsOnMap < __instance.maxAmountOfHealthPickupsOnMap)
                health = __instance.healthPickupChanceCurve.Evaluate(spawningPlayerHealthPercentage)
                         * Modes.Active.HealthDrops * pickupChanceMultiplier;

            if (health > roll) __result = __instance.healthPickupPrefab;
            else if (pickupChanceMultiplier * 0.0016f + health > roll && __instance.EnoughXPOrbsToAttract())
                __result = __instance.magnetPickupPrefab;
            else __result = null;
            return false;
        }
        catch (Exception e)
        {
            RunCustomizerMod.Log.Error($"pickup roll failed, using the game's: {e.Message}");
            return true;
        }
    }

    // Бонус золота за сложность: между значениями базовых сложностей по «тяжести» параметров
    private static bool _readingBase;
    private static float[] _baseGold;

    public static void GoldMultiplierPostfix(RunGoldCalculatorData __instance, Difficulty difficulty, ref float __result)
    {
        if (!Modes.InRun || _readingBase || difficulty != Modes.RunDifficulty) return;
        try
        {
            if (_baseGold == null)
            {
                _readingBase = true;
                _baseGold = new float[3];
                for (int d = 0; d < 3; d++) _baseGold[d] = __instance.GetGoldMultiplierForDifficulty((Difficulty)d);
            }
            // Экран итогов печатает значение как есть — округляем до целых процентов
            float v = Math.Max(0f, Interpolate(_baseGold, Modes.Score(Modes.Active)));
            bool fraction = Math.Max(_baseGold[1], _baseGold[2]) <= 5f; // 0.25 = 25% или 25 = 25%
            __result = fraction ? (float)Math.Round(v, 2) : (float)Math.Round(v);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"gold bonus: {e.Message}"); }
        finally { _readingBase = false; }
    }

    public static float Interpolate(float[] points, float position)
    {
        int seg = position < 1f ? 0 : 1;
        return points[seg] + (points[seg + 1] - points[seg]) * (position - seg);
    }
}
