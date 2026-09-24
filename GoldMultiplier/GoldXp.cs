using System;
using Il2Cpp;

namespace GoldXpMultiplier;

// Золото за забег: Calculate возвращает EarnedGold с разбивкой (база + бонусы),
// а итог TotalGold считается из этих полей. Умножаем каждую часть, и начисляется
// ровно то, что показывает экран наград.
internal static class GoldPatch
{
    public static void Postfix(EarnedGold __result)
    {
        if (!GoldXpMod.Enabled || __result == null) return;
        float mult = GoldXpMod.GoldMultiplier;
        if (mult == 1f) return;

        int vanillaTotal = __result.TotalGold;
        int[] vanilla = Read(__result);
        Write(__result, Array.ConvertAll(vanilla, v => Scale(v, mult)));
        RunSummaryUi.RememberGold(__result, vanilla, mult, vanillaTotal);
    }

    // База и все бонусы в одном порядке
    public static int[] Read(EarnedGold g) => new[]
    {
        g._BaseGold_k__BackingField,
        g._BonusGoldFromFinalUpgrade_k__BackingField,
        g._BonusGoldFromGoldGainStat_k__BackingField,
        g._BonusGoldFromCurses_k__BackingField,
        g._BonusGoldFromDifficulty_k__BackingField,
        g._BonusGoldFromUnusedCharacter_k__BackingField,
    };

    private static void Write(EarnedGold g, int[] v)
    {
        g._BaseGold_k__BackingField                     = v[0];
        g._BonusGoldFromFinalUpgrade_k__BackingField    = v[1];
        g._BonusGoldFromGoldGainStat_k__BackingField    = v[2];
        g._BonusGoldFromCurses_k__BackingField          = v[3];
        g._BonusGoldFromDifficulty_k__BackingField      = v[4];
        g._BonusGoldFromUnusedCharacter_k__BackingField = v[5];
    }

    // Копия EarnedGold с заданными суммами (проценты бонусов — как в оригинале)
    public static EarnedGold Create(int[] values, EarnedGold percentagesFrom)
    {
        var g = new EarnedGold();
        Write(g, values);
        g._BonusGoldPercentageFromCurses_k__BackingField          = percentagesFrom._BonusGoldPercentageFromCurses_k__BackingField;
        g._BonusGoldPercentageFromDifficulty_k__BackingField      = percentagesFrom._BonusGoldPercentageFromDifficulty_k__BackingField;
        g._BonusGoldPercentageFromUnusedCharacter_k__BackingField = percentagesFrom._BonusGoldPercentageFromUnusedCharacter_k__BackingField;
        return g;
    }

    private static int Scale(int value, float mult)
    {
        if (value <= 0) return value;
        double scaled = Math.Round(value * (double)mult);
        return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
    }
}

// Опыт в забеге: весь опыт (сферы, финальные улучшения) идёт через
// PartyLevelManager.TryAddExperience.
internal static class XpPatch
{
    public static void Prefix(PartyLevelManager __instance, ref float experience)
    {
        if (experience <= 0f) return;
        float mult = GoldXpMod.Enabled ? GoldXpMod.XpMultiplier : 1f;
        float vanilla = experience;
        if (mult != 1f) experience *= mult;
        RunSummaryUi.TrackXp(__instance, vanilla, experience);
    }
}
