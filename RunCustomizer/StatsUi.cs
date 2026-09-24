using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using Object = UnityEngine.Object;

namespace RunCustomizer;

// «Уровень угрозы» в статистике (пауза и экран итогов): для «Случайной» и «Своей»
// вместо базовой сложности — название режима и строки с выпавшими множителями.
internal static class StatsUi
{
    private const string RowPrefix = "RC_Stat_";
    private static readonly Dictionary<IntPtr, Color> OriginalColors = new();

    public static void MissionStatsPostfix(MissionStatsPanel __instance) => Apply(__instance.difficultyEntry);
    public static void GameOverStatsPostfix(GameOverMissionStatsPanel __instance) => Apply(__instance.difficultyEntry);

    private static void Apply(DifficultyEntry entry)
    {
        try
        {
            if (entry == null) return;
            var row = entry.transform;
            var parent = row.parent;

            // строки прошлого показа
            if (parent != null)
                for (int i = parent.childCount - 1; i >= 0; i--)
                    if (parent.GetChild(i).name.StartsWith(RowPrefix)) Object.Destroy(parent.GetChild(i).gameObject);

            var mode = Modes.LastRunMode;
            var value = entry.difficultyText;
            if (mode is not (Mode.Random or Mode.Custom))
            {
                // ванильный забег — возвращаем то, что мы могли поменять раньше
                if (entry.difficultyLocalizer != null && !entry.difficultyLocalizer.enabled)
                {
                    entry.difficultyLocalizer.enabled = true;
                    entry.difficultyLocalizer.RefreshString();
                }
                if (value != null && OriginalColors.TryGetValue(value.Pointer, out var color)) value.color = color;
                return;
            }

            if (value != null)
            {
                if (!OriginalColors.ContainsKey(value.Pointer)) OriginalColors[value.Pointer] = value.color;
                if (entry.difficultyLocalizer != null) entry.difficultyLocalizer.enabled = false;
                value.text = Strings.Get(mode == Mode.Custom ? Strings.CustomName : Strings.RandomName);
                value.color = DifficultyUi.NameColors[mode];
            }
            if (entry.icon != null) entry.icon.sprite = DifficultyUi.Icon(mode);
            if (parent == null) return;

            var p = Modes.Active;
            int at = row.GetSiblingIndex();
            AddRow(entry, Strings.Enemy, p.Enemy, ++at);
            AddRow(entry, Strings.Spawn, p.SpawnSpeed, ++at);
            AddRow(entry, Strings.Count, p.EnemyCount, ++at);
            AddRow(entry, Strings.Health, p.HealthDrops, ++at);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[stats] {e}"); }
    }

    // Копия строки «Уровень угрозы»: подпись — параметр, значение — множитель, без значка
    private static void AddRow(DifficultyEntry entry, string labelKey, float multiplier, int siblingIndex)
    {
        var go = Object.Instantiate(entry.gameObject, entry.transform.parent);
        go.name = RowPrefix + labelKey;
        go.transform.SetSiblingIndex(siblingIndex);
        Object.DestroyImmediate(go.GetComponent<DifficultyEntry>());
        foreach (var loc in go.GetComponentsInChildren<LocalizeStringEvent>(true)) Object.DestroyImmediate(loc);

        string valuePath = PathFrom(entry.transform, entry.difficultyText?.transform);
        string iconPath = PathFrom(entry.transform, entry.icon?.transform);
        var valueText = valuePath != null ? Find(go.transform, valuePath)?.GetComponent<TMP_Text>() : null;
        string shown = "x" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);

        bool labelled = false;
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (valueText != null && text.Pointer == valueText.Pointer) continue;
            if (!labelled) { labelled = true; text.text = Strings.Get(labelKey); }
        }
        if (valueText != null)
        {
            valueText.text = labelled ? shown : $"{Strings.Get(labelKey)} {shown}";
            valueText.color = Color.white;
        }
        // значок прячем, но место оставляем — чтобы подписи стояли ровно
        var icon = iconPath != null ? Find(go.transform, iconPath)?.GetComponent<UnityEngine.UI.Image>() : null;
        if (icon != null) icon.color = new Color(1f, 1f, 1f, 0f);
    }

    private static string PathFrom(Transform root, Transform child)
    {
        if (child == null) return null;
        if (child == root) return "";
        var parts = new List<string>();
        for (var t = child; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }

    private static Transform Find(Transform root, string path) => path == "" ? root : root.Find(path);
}
