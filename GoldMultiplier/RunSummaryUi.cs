using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Localization.Components;
using Object = UnityEngine.Object;

namespace GoldXpMultiplier;

// Экран итогов забега: сначала игра честно досчитывает ванильные значения,
// потом сверху падает «x2» и бьёт по числу (ImpactFx) — оно меняется на умноженное.
// Начисляется всегда умноженное — подменяется только то, что видно на экране.
internal static class RunSummaryUi
{
    private const string BonusPanelName = "SBMod_GoldBonus";

    // --- золото: ванильные значения каждого EarnedGold, который умножил GoldPatch ---
    private sealed class GoldSnapshot
    {
        public int[] Vanilla;
        public int[] Modded;
        public float Mult;
        public int VanillaTotal, ModdedTotal;
    }

    private static readonly Dictionary<IntPtr, GoldSnapshot> Snapshots = new();
    private static readonly List<GoldSnapshot> RecentSnapshots = new();

    public static void RememberGold(EarnedGold modded, int[] vanilla, float mult, int vanillaTotal)
    {
        var snapshot = new GoldSnapshot
        {
            Vanilla = vanilla, Modded = GoldPatch.Read(modded), Mult = mult,
            VanillaTotal = vanillaTotal, ModdedTotal = modded.TotalGold,
        };
        if (Snapshots.Count > 64) Snapshots.Clear();
        Snapshots[modded.Pointer] = snapshot;
        RecentSnapshots.Add(snapshot);
        if (RecentSnapshots.Count > 16) RecentSnapshots.RemoveAt(0);
    }

    private static GoldSnapshot FindSnapshot(EarnedGold gold)
    {
        if (Snapshots.TryGetValue(gold.Pointer, out var s)) return s;
        // объект мог быть скопирован (запись итогов забега) — ищем по значениям
        var values = GoldPatch.Read(gold);
        for (int i = RecentSnapshots.Count - 1; i >= 0; i--)
            if (Same(RecentSnapshots[i].Modded, values)) return RecentSnapshots[i];
        return null;
    }

    private static bool Same(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // --- опыт забега: сколько было до и после множителя в текущем забеге ---
    private static IntPtr _xpRunId;
    private static double _xpVanilla, _xpModded;

    public static void TrackXp(PartyLevelManager manager, float vanilla, float modded)
    {
        if (manager == null) return;
        if (manager.Pointer != _xpRunId) { _xpRunId = manager.Pointer; _xpVanilla = 0; _xpModded = 0; }
        _xpVanilla += vanilla;
        _xpModded += modded;
    }

    // --- то, что покажем на экране ---
    private sealed class Pending
    {
        public EarnedGoldUI GoldUi;
        public EarnedGold ModdedGold;
        public GoldSnapshot Gold;
        public TMP_Text XpText;
        public float XpVanilla, XpModded, XpMult;
        public Transform ShakeRoot;
    }

    private static Pending _pending;
    private static bool _sequenceQueued;

    // Постфикс GameOverMissionStatsPanel.SetValues: панель уже получила умноженное
    // золото (EarnedGoldUI.Set — однострочный сеттер, его код общий с десятками
    // других, хукать нельзя), подменяем то, что будет анимироваться, на ванильное.
    public static void SetValuesPostfix(GameOverMissionStatsPanel __instance)
    {
        try
        {
            var ui = __instance.earnedGoldUI;
            var gold = ui?.earnedGold;
            if (gold == null) return;
            var snapshot = FindSnapshot(gold);
            if (snapshot == null || snapshot.Mult == 1f || snapshot.VanillaTotal == snapshot.ModdedTotal) return;

            Queue().GoldUi = ui;
            _pending.ShakeRoot = __instance.transform;
            _pending.ModdedGold = gold;
            _pending.Gold = snapshot;
            ui.earnedGold = GoldPatch.Create(snapshot.Vanilla, gold);
        }
        catch (Exception e) { GoldXpMod.Log.Error($"[summary] gold: {e}"); }
    }

    // Префикс GameOverMissionStatsPanel.SetXPEarned: отдаём экрану ванильный опыт
    public static void SetXpEarnedPrefix(GameOverMissionStatsPanel __instance, ref float xpEarned)
    {
        try
        {
            if (!(_xpModded > 0) || !(_xpVanilla > 0) || Math.Abs(_xpModded - _xpVanilla) < 0.5) return;
            float vanilla = (float)(xpEarned * (_xpVanilla / _xpModded));
            if (Math.Round(vanilla) == Math.Round(xpEarned)) return;

            Queue().XpText = __instance.xpEarnedValue;
            _pending.ShakeRoot = __instance.transform;
            _pending.XpMult = GoldXpMod.XpMultiplier;
            _pending.XpVanilla = vanilla;
            _pending.XpModded = xpEarned;
            xpEarned = vanilla;
        }
        catch (Exception e) { GoldXpMod.Log.Error($"[summary] xp: {e}"); }
    }

    private static Pending Queue()
    {
        _pending ??= new Pending();
        if (!_sequenceQueued)
        {
            _sequenceQueued = true;
            MelonCoroutines.Start(Sequence());
        }
        return _pending;
    }

    private static IEnumerator Sequence()
    {
        yield return null; // даём SetValues выставить всё остальное
        var job = _pending;
        _pending = null;
        _sequenceQueued = false;
        if (job == null) yield break;

        TMP_Text goldText = job.GoldUi != null ? job.GoldUi.totalGoldEarnedText : null;
        if (goldText != null && job.Gold != null)
        {
            // ждём, пока игра досчитает ванильное золото и число постоит
            yield return WaitForValue(goldText, job.Gold.VanillaTotal);
            var ui = job.GoldUi;
            yield return ImpactFx.Slam(goldText, goldText, FormatMult(job.Gold.Mult),
                FormatLike(goldText.text, job.Gold.ModdedTotal), job.ShakeRoot, () =>
                {
                    ShowBonusPanel(ui, job.Gold.Mult);
                    PlayBonusSound(ui);
                });
            // если экран перерисуется ещё раз — пусть уже с настоящими (умноженными) числами
            if (ui != null && job.ModdedGold != null) ui.earnedGold = job.ModdedGold;
        }

        if (job.XpText != null)
        {
            yield return WaitVisible(job.XpText);
            string mult = FormatMult(job.XpMult);
            yield return ImpactFx.Slam(job.XpText, goldText ?? job.XpText, mult,
                FormatLike(job.XpText.text, Mathf.RoundToInt(job.XpModded)) + Suffix(job.XpMult),
                job.ShakeRoot, () => PlayBonusSound(job.GoldUi));
        }
    }

    private static void ShowBonusPanel(EarnedGoldUI ui, float mult)
    {
        if (ui == null) return;
        var panel = GetOrCreateBonusPanel(ui, out var valueText);
        if (panel == null) return;
        panel.SetActive(true);
        if (valueText != null) valueText.text = FormatMult(mult);
    }

    private static void PlayBonusSound(EarnedGoldUI ui)
    {
        try { if (ui != null) ui.bonusGoldSoundPlayer?.Play(); } catch { }
    }

    private static IEnumerator WaitForValue(TMP_Text text, int value)
    {
        float timeout = Time.unscaledTime + 120f;
        float stableSince = -1f;
        while (text != null && Time.unscaledTime < timeout)
        {
            if (text.isActiveAndEnabled && ParseInt(text.text) == value)
            {
                if (stableSince < 0f) stableSince = Time.unscaledTime;
                else if (Time.unscaledTime - stableSince > 0.35f) yield break;
            }
            else stableSince = -1f;
            yield return null;
        }
    }

    private static IEnumerator WaitVisible(TMP_Text text)
    {
        float timeout = Time.unscaledTime + 120f;
        while (text != null && !text.isActiveAndEnabled && Time.unscaledTime < timeout) yield return null;
        float until = Time.unscaledTime + 0.3f;
        while (Time.unscaledTime < until) yield return null;
    }

    // Плашка бонуса «x2» — копия родной плашки бонуса за сложность
    private static GameObject GetOrCreateBonusPanel(EarnedGoldUI ui, out TMP_Text valueText)
    {
        valueText = null;
        try
        {
            var template = ui.difficultyGoldBonus;
            var templatePanel = template?.Panel;
            if (templatePanel == null) return null;
            var parent = templatePanel.transform.parent;

            Transform existing = parent.Find(BonusPanelName);
            GameObject panel;
            if (existing != null) panel = existing.gameObject;
            else
            {
                panel = Object.Instantiate(templatePanel, parent);
                panel.name = BonusPanelName;
                Transform last = templatePanel.transform;
                foreach (var other in new[] { ui.cursesGoldBonus?.Panel, ui.unusedCharacterGoldBonus?.Panel })
                    if (other != null && other.transform.parent == parent && other.transform.GetSiblingIndex() > last.GetSiblingIndex())
                        last = other.transform;
                panel.transform.SetSiblingIndex(last.GetSiblingIndex() + 1);
            }

            string valuePath = template.Text != null ? RelativePath(templatePanel.transform, template.Text.transform) : null;
            foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (valuePath != null && RelativePath(panel.transform, t.transform) == valuePath)
                {
                    foreach (var loc in t.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
                    valueText = t;
                }
                else if (existing == null) SettingsTab.BindText(t, Strings.Bonus);
            }
            panel.SetActive(false);
            return panel;
        }
        catch (Exception e)
        {
            GoldXpMod.Log.Warning($"[summary] bonus panel: {e.Message}");
            return null;
        }
    }

    private static string RelativePath(Transform root, Transform t)
    {
        if (t == root) return "";
        var sb = new StringBuilder(t.name);
        while (t.parent != null && t.parent != root) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }

    private static string FormatMult(float mult) =>
        "x" + mult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string Suffix(float mult) => $" <color=#FFD54A><size=75%>{FormatMult(mult)}</size></color>";

    private static int ParseInt(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        long v = 0;
        bool any = false;
        foreach (char c in s)
        {
            if (c == '<') break; // rich text суффикс
            if (c >= '0' && c <= '9') { v = v * 10 + (c - '0'); any = true; if (v > int.MaxValue) return -1; }
        }
        return any ? (int)v : -1;
    }

    // Форматируем так же, как игра: если в числе был разделитель тысяч — ставим такой же
    private static string FormatLike(string sample, int value)
    {
        char? separator = null;
        if (!string.IsNullOrEmpty(sample))
            for (int i = 1; i < sample.Length - 1; i++)
                if (!char.IsDigit(sample[i]) && char.IsDigit(sample[i - 1]) && char.IsDigit(sample[i + 1]))
                { separator = sample[i]; break; }

        string digits = Math.Abs(value).ToString();
        if (separator == null || digits.Length <= 3) return (value < 0 ? "-" : "") + digits;
        var sb = new StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(separator.Value);
            sb.Append(digits[i]);
        }
        return (value < 0 ? "-" : "") + sb;
    }
}
