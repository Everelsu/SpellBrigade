using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
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
            var values = new[] { (Strings.Enemy, p.Enemy), (Strings.Spawn, p.SpawnSpeed), (Strings.Count, p.EnemyCount), (Strings.Health, p.HealthDrops) };
            int at = row.GetSiblingIndex();

            // На экране итогов список фиксированной высоты: с кучей активных заветов места под
            // четыре строки нет, и они наезжают на блок золота. Тогда — компактный блок.
            float rowHeight = row.TryCast<RectTransform>()?.rect.height ?? 74f;
            var (free, spacing) = FreeSpace(parent);
            if (free >= values.Length * (rowHeight + spacing))
                foreach (var (key, multiplier) in values) AddRow(entry, key, multiplier, ++at);
            else
                AddCompact(entry, values, ++at, free - spacing, rowHeight);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[stats] {e}"); }
    }

    // Сколько места осталось в списке под нашими строками (и шаг между строками)
    private static (float free, float spacing) FreeSpace(Transform list)
    {
        var rect = list.TryCast<RectTransform>();
        var group = list.GetComponent<VerticalLayoutGroup>();
        if (rect == null) return (float.MaxValue, 0f);
        float spacing = group != null ? group.spacing : 0f;
        float used = group != null ? group.padding.top + group.padding.bottom : 0f;
        int count = 0;
        for (int i = 0; i < list.childCount; i++)
        {
            var child = list.GetChild(i);
            if (!child.gameObject.activeSelf || child.name.StartsWith(RowPrefix)) continue; // наши старые строки ещё не удалены
            used += child.TryCast<RectTransform>()?.rect.height ?? 0f;
            count++;
        }
        used += spacing * Math.Max(0, count - 1);

        // Список растёт сам (пауза) — предел ставит панель вокруг него; иначе — своя высота
        float capacity = rect.rect.height;
        var outer = list.parent?.TryCast<RectTransform>();
        if (list.GetComponent<ContentSizeFitter>() != null && outer != null)
            capacity = outer.rect.height + rect.anchoredPosition.y;
        return (capacity - used, spacing);
    }

    // Все четыре множителя одним блоком: 2×2, а если места совсем мало — одной строкой.
    // Шрифт подбирается сам (автоподбор TMP), подписи — шрифтом строки «Уровень угрозы».
    private static void AddCompact(DifficultyEntry entry, (string key, float value)[] values, int siblingIndex, float room, float rowHeight)
    {
        var template = entry.difficultyText;
        if (template == null) return;

        bool grid = room >= rowHeight;
        float height = Mathf.Clamp(room, 44f, 2f * rowHeight);

        var go = new GameObject(RowPrefix + "Compact");
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(entry.transform.parent, false);
        rect.SetSiblingIndex(siblingIndex);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        var element = go.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;

        int columns = grid ? 2 : values.Length, rows = grid ? 2 : 1;
        const float indent = 90f; // как у подписей строк игры (после значка)
        for (int i = 0; i < values.Length; i++)
        {
            int c = i % columns, r = i / columns;
            var text = Object.Instantiate(template.gameObject, rect).GetComponent<TMP_Text>();
            foreach (var loc in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
            text.name = "Stat";
            var tr = text.rectTransform;
            tr.anchorMin = new Vector2(c / (float)columns, 1f - (r + 1) / (float)rows);
            tr.anchorMax = new Vector2((c + 1) / (float)columns, 1f - r / (float)rows);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.offsetMin = new Vector2(c == 0 ? indent : 12f, 0f);
            tr.offsetMax = new Vector2(-8f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = (template.enableAutoSizing ? template.fontSizeMax : template.fontSize) * (grid ? 0.8f : 0.7f);
            text.color = Color.white;
            string shown = "x" + values[i].value.ToString("0.##", CultureInfo.InvariantCulture);
            text.text = $"<alpha=#B3>{Strings.Get(values[i].key)}  <alpha=#FF>{shown}"; // подпись приглушённая, множитель ярко
        }
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
