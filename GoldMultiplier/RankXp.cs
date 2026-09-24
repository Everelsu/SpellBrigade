using System;
using Il2Cpp;

namespace GoldXpMultiplier;

// Множитель опыта ранга мага.
// Сам расчёт (CharacterRankCalculator.CalculateProgress) хукать нельзя: он
// возвращает структуру, и Il2CppInterop портит результат (так сломался ранг
// в 2.0–2.1). Поэтому перехватываем уже готовый результат в двух местах:
//  • CharacterRankProgression.SetRankProgression — запись ранга в сохранение;
//  • RunRecordFactory.CalculateRankProgression — то, что показывает экран итогов.
// Прирост переводим в «эффективные минуты» (сколько минут стоит каждый ранг —
// публичный метод калькулятора), умножаем и заново проходим по рангам.
internal static class RankXp
{
    // SaveDoctor чинит ранги через SetRankProgression — это не опыт, не умножаем
    [ThreadStatic] public static bool Suspended;

    // Последний результат, посчитанный для экрана итогов: если игра потом
    // сохранит ровно его, повторно не умножаем
    private static (int r0, float p0, int r, float p) _lastScaled = (-1, 0, -1, 0);

    public static void SetRankProgressionPrefix(CharacterRankProgression __instance, CharacterId characterId,
                                                ref int rank, ref float rankProgress)
    {
        try
        {
            if (Suspended || !GoldXpMod.Enabled) return;
            float mult = GoldXpMod.RankXpMultiplier;
            if (mult == 1f) return;

            var current = __instance.GetOrCreateProgressForCharacter(characterId);
            if (current == null) return;
            int r0 = current.CurrentRank;
            float p0 = current.ProgressTowardsNextRank;

            if (_lastScaled.r0 == r0 && _lastScaled.p0 == p0 && _lastScaled.r == rank && _lastScaled.p == rankProgress)
                return;

            if (TryScale(__instance.rankCalculator, current.Prestige, r0, p0, rank, rankProgress, mult, out int r, out float p))
            {
                rank = r;
                rankProgress = p;
            }
        }
        catch (Exception e) { GoldXpMod.Log.Error($"[rank xp] {e}"); }
    }

    public static void CalculateRankProgressionPostfix(RankProgression __result, int prestige)
    {
        try
        {
            if (__result == null || !GoldXpMod.Enabled) return;
            float mult = GoldXpMod.RankXpMultiplier;
            if (mult == 1f) return;

            var calculator = SingletonPersistent<LocalPlayer>.Instance?.GetCharacterRankManager()?.rankCalculator;
            int r0 = __result.StartRank, r1 = __result.EndRank;
            float p0 = __result.StartRankProgress, p1 = __result.EndRankProgress;
            if (!TryScale(calculator, prestige, r0, p0, r1, p1, mult, out int r, out float p)) return;

            __result._EndRank_k__BackingField = r;
            __result._EndRankProgress_k__BackingField = p;
            _lastScaled = (r0, p0, r, p);
        }
        catch (Exception e) { GoldXpMod.Log.Error($"[rank xp] {e}"); }
    }

    private static bool TryScale(CharacterRankCalculator calc, int prestige, int r0, float p0, int r1, float p1,
                                 float mult, out int rank, out float progress)
    {
        rank = r1;
        progress = p1;
        if (calc == null || mult == 1f) return false;
        if (!Sane(r0, p0) || !Sane(r1, p1)) return false;
        if (r1 < r0 || (r1 == r0 && p1 <= p0)) return false;           // прироста нет
        if (r1 - r0 > 1000 || calc.IsMaxRank(r0) || calc.IsMaxRank(r1)) return false;

        double gained;
        if (r1 == r0)
            gained = (p1 - p0) * Minutes(calc, r0, prestige);
        else
        {
            gained = (1 - p0) * Minutes(calc, r0, prestige);
            for (int k = r0 + 1; k < r1; k++) gained += Minutes(calc, k, prestige);
            gained += p1 * Minutes(calc, r1, prestige);
        }
        if (!(gained > 0) || double.IsInfinity(gained)) return false;

        double remaining = gained * mult;
        int r = r0;
        double p = p0;
        while (true)
        {
            if (calc.IsMaxRank(r)) { p = 0; break; }
            double m = Minutes(calc, r, prestige);
            if (!(m > 0) || double.IsInfinity(m)) return false;
            double need = (1 - p) * m;
            if (remaining < need) { p += remaining / m; break; }
            remaining -= need;
            r++;
            p = 0;
            if (r - r0 > 10000) return false;
        }

        rank = r;
        progress = (float)Math.Min(p, 0.9999);
        return Sane(rank, progress);
    }

    private static double Minutes(CharacterRankCalculator calc, int rank, int prestige) =>
        calc.CalculateMinutesRequiredForNextRank(rank, prestige);

    private static bool Sane(int rank, float progress) =>
        rank >= 0 && rank <= 1000 && !float.IsNaN(progress) && progress >= 0f && progress <= 1f;
}
