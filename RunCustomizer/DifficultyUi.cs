using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RunCustomizer;

// Окно выбора сложности: рядом с тремя родными кнопками — «Случайная» и «Своя».
// Кнопки клонируются из родной, поэтому выглядят и звучат как игровые.
// Выбор своего режима = клик по родной кнопке базовой сложности (игра сама выставит её
// в лобби и запомнит) + наш режим поверх. Клик по родной кнопке возвращает ванильный режим.
// Под описанием — строки настроек: «что выпадает» у «Случайной», четыре параметра у «Своей».
internal static class DifficultyUi
{
    private const string ButtonPrefix = "RC_Mode_", RowsName = "RC_Rows";
    private const float RowHeight = 80f;

    // Кнопки окна: RandomBase живёт под кнопкой Random (переключатель «что выпадает»)
    private static readonly Mode[] Buttons = { Mode.Random, Mode.Custom };

    public static readonly Dictionary<Mode, Color> NameColors = new()
    {
        [Mode.Random]     = new Color(0.84f, 0.48f, 1f),
        [Mode.Custom]     = new Color(0.37f, 0.88f, 0.94f),
        [Mode.RandomBase] = new Color(0.84f, 0.48f, 1f),
    };

    private static readonly HashSet<IntPtr> SubscribedBaseButtons = new();
    private static bool _clickingForMode;

    public static Sprite Icon(Mode mode) =>
        Sprites.Get(mode switch { Mode.Random => "random.png", Mode.Custom => "custom.png", _ => "randombase.png" });

    private static Mode ButtonOf(Mode mode) => mode == Mode.RandomBase ? Mode.Random : mode;
    private static Mode RandomMode => RunCustomizerMod.RandomBaseOnly.Value ? Mode.RandomBase : Mode.Random;

    // ---------- хуки DifficultiesPanel ----------

    public static void InitializeButtonsPostfix(DifficultiesPanel __instance)
    {
        try { Build(__instance); }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
    }

    public static void ShowPostfix(DifficultiesPanel __instance)
    {
        try
        {
            RefreshSelection(__instance);
            if (Modes.Selected != Mode.Vanilla) ShowModeDetails(__instance, ButtonOf(Modes.Selected));
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
    }

    // Игра показала описание родной сложности — возвращаем её локализацию и прячем наши строки
    public static void ShowDifficultyDetailsPostfix(DifficultiesPanel __instance)
    {
        try
        {
            foreach (var loc in Localizers(__instance))
            {
                if (loc == null || loc.enabled) continue;
                loc.enabled = true;
                loc.RefreshString();
            }
            ShowRows(__instance, null);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
    }

    private static void Build(DifficultiesPanel panel)
    {
        var buttons = panel.difficultyButtons;
        if (buttons == null || buttons.Count == 0) return;

        DifficultySelectionButton template = null;
        foreach (var b in buttons)
        {
            if (b == null || b.name.StartsWith(ButtonPrefix)) continue;
            template ??= b;
            if (SubscribedBaseButtons.Add(b.Pointer))
                b.add_OnClicked((Il2CppSystem.Action)(Action)(() =>
                {
                    if (_clickingForMode) return;
                    Modes.Selected = Mode.Vanilla;
                    RefreshSelection(panel);
                    RefreshLobbyIcons();
                }));
        }
        if (template == null) return;

        var parent = template.transform.parent;
        if (parent.Find(ButtonPrefix + Mode.Random) != null) return;

        foreach (var mode in Buttons)
        {
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = ButtonPrefix + mode;
            go.transform.SetAsLastSibling();
            var button = go.GetComponent<DifficultySelectionButton>();
            // Show ищет кнопку текущей сложности через Single — у клона не должно быть родного значения
            button._Difficulty_k__BackingField = (Difficulty)(100 + (int)mode);
            MuteSerializedClicks(go.GetComponent<Button>());
            var icon = Icon(mode == Mode.Random ? RandomMode : mode);
            if (button.iconImage != null) button.iconImage.sprite = icon;
            if (button.iconImageLocked != null) button.iconImageLocked.sprite = icon;

            var m = mode;
            button.add_OnClicked((Il2CppSystem.Action)(Action)(() => Select(panel, m == Mode.Random ? RandomMode : m)));
            button.add_OnHighlight((Il2CppSystem.Action)(Action)(() => ShowModeDetails(panel, m)));
        }

        BuildRows(panel);
        RefreshSelection(panel);
    }

    private static void Select(DifficultiesPanel panel, Mode mode)
    {
        try
        {
            Modes.Selected = mode;
            Modes.Reroll();
            var baseDifficulty = Modes.Pending.Base;

            // Клик за игрока по родной кнопке базовой сложности: игра сама выставит её
            // в лобби (и разошлёт по сети) и запомнит как предпочтение
            foreach (var b in panel.difficultyButtons)
                if (b != null && !b.name.StartsWith(ButtonPrefix) && b.Difficulty == baseDifficulty)
                {
                    _clickingForMode = true;
                    try { b.OnClicked?.Invoke(); }
                    finally { _clickingForMode = false; }
                    break;
                }

            RefreshSelection(panel);
            ShowModeDetails(panel, ButtonOf(mode));
            RefreshLobbyIcons();
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
    }

    // Рамка «выбрано» — на кнопке текущего режима
    private static void RefreshSelection(DifficultiesPanel panel)
    {
        var buttons = panel.difficultyButtons;
        if (buttons == null) return;
        var selected = Modes.Selected;
        Transform parent = null;

        foreach (var b in buttons)
        {
            if (b == null || b.name.StartsWith(ButtonPrefix)) continue;
            parent ??= b.transform.parent;
            if (selected != Mode.Vanilla) b.Deselect();
            else if (b.Difficulty == panel.selectedDifficulty) b.Select();
        }
        if (parent == null) return;
        foreach (var m in Buttons)
        {
            var ours = parent.Find(ButtonPrefix + m)?.GetComponent<DifficultySelectionButton>();
            if (ours == null) continue;
            if (selected != Mode.Vanilla && ButtonOf(selected) == m) ours.Select(); else ours.Deselect();
        }
    }

    // mode — кнопка окна (Random или Custom)
    private static void ShowModeDetails(DifficultiesPanel panel, Mode mode)
    {
        try
        {
            foreach (var loc in Localizers(panel))
                if (loc != null) loc.enabled = false;

            var shown = mode == Mode.Random ? RandomMode : mode;
            var (nameKey, descKey) = shown switch
            {
                Mode.Random => (Strings.RandomName, Strings.RandomDesc),
                Mode.Custom => (Strings.CustomName, Strings.CustomDesc),
                _           => (Strings.BaseName, Strings.BaseDesc),
            };
            if (panel.difficultyNameText != null)
            {
                panel.difficultyNameText.text = Strings.Get(nameKey);
                panel.difficultyNameText.color = NameColors[shown];
            }
            SetText(panel.difficultyDescriptionLocalizer, Strings.Get(descKey));
            SetText(panel.difficultyExplanationLocalizer, shown == Mode.Random ? RangesText() : "");
            // Строка бонуса золота стоит там же, где наши настройки; бонус описан в тексте выше
            panel.difficultyGoldBonusLocalizer?.gameObject.SetActive(false);
            ShowRows(panel, mode);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
    }

    private static string RangesText()
    {
        static string R(string key, (float Min, float Max) r) =>
            $"{Strings.Get(key)} x{r.Min.ToString("0.##", CultureInfo.InvariantCulture)}–{r.Max.ToString("0.##", CultureInfo.InvariantCulture)}";
        return R(Strings.Enemy, Modes.RangeEnemy) + "  ·  " + R(Strings.Spawn, Modes.RangeSpawnSpeed) + "\n" +
               R(Strings.Count, Modes.RangeEnemyCount) + "  ·  " + R(Strings.Health, Modes.RangeHealth);
    }

    // Смена «что выпадает» — значок «Случайной» переворачивается, как монетка, на другую сторону
    private static void FlipRandomIcon(DifficultiesPanel panel)
    {
        Transform parent = null;
        foreach (var b in panel.difficultyButtons)
            if (b != null) { parent = b.transform.parent; break; }
        var image = parent?.Find(ButtonPrefix + Mode.Random)?.GetComponent<DifficultySelectionButton>()?.iconImage;
        if (image != null) MelonCoroutines.Start(Flip(image, Icon(RandomMode)));
    }

    private static System.Collections.IEnumerator Flip(Image image, Sprite to)
    {
        const float half = 0.12f;
        var t = image.transform;
        for (float time = 0f; time < half && image != null; time += Time.unscaledDeltaTime)
        {
            t.localScale = new Vector3(1f - time / half, 1f, 1f);
            yield return null;
        }
        if (image == null) yield break;
        image.sprite = to;
        for (float time = 0f; time < half && image != null; time += Time.unscaledDeltaTime)
        {
            float k = time / half;
            t.localScale = new Vector3(k, 1f + 0.12f * Mathf.Sin(k * Mathf.PI), 1f); // лёгкий «подскок»
            yield return null;
        }
        if (image != null) t.localScale = Vector3.one;
    }

    private static IEnumerable<LocalizeStringEvent> Localizers(DifficultiesPanel p) => new[]
        { p.difficultyNameLocalizer, p.difficultyDescriptionLocalizer, p.difficultyExplanationLocalizer, p.difficultyGoldBonusLocalizer };

    private static void SetText(LocalizeStringEvent loc, string text)
    {
        if (loc == null) return;
        var tmp = loc.GetComponent<TMP_Text>() ?? loc.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
    }

    // ---------- строки настроек под описанием ----------

    private sealed class Row
    {
        public Mode Owner;          // у какой кнопки показывается
        public Selector Selector;
        public Func<List<string>> Options;
        public Func<string> Current;
        public Action<string> Apply;
    }

    private static readonly List<Row> Rows = new();

    private static void BuildRows(DifficultiesPanel panel)
    {
        // Explanation_Text растягивается по тексту — строки встают сразу под ним
        var anchor = panel.difficultyExplanationLocalizer?.transform;
        if (anchor == null) { RunCustomizerMod.Log.Warning("[difficulty ui] no details area — settings rows unavailable"); return; }

        var template = FindSelectorRow();
        if (template == null) { RunCustomizerMod.Log.Warning("[difficulty ui] no selector row to copy — settings rows unavailable"); return; }

        var container = new GameObject(RowsName, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        var rt = container.GetComponent<RectTransform>();
        rt.SetParent(anchor, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(1000f, 4 * RowHeight);
        var layout = container.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        Rows.RemoveAll(r => r.Selector == null);
        var randomRows = new List<Selectable>
        {
            AddRow(container.transform, template, Mode.Random, Strings.RandomKind, new Row
            {
                Options = () => new List<string> { Strings.Get(Strings.KindAll), Strings.Get(Strings.KindBase) },
                Current = () => Strings.Get(RunCustomizerMod.RandomBaseOnly.Value ? Strings.KindBase : Strings.KindAll),
                Apply   = option =>
                {
                    bool baseOnly = option == Strings.Get(Strings.KindBase);
                    if (RunCustomizerMod.RandomBaseOnly.Value == baseOnly) return;
                    RunCustomizerMod.RandomBaseOnly.Value = baseOnly;
                    RunCustomizerMod.Save();
                    // Без клика по родной кнопке: он закрывал окно. Сложность лобби подтянет SyncLobby.
                    if (Modes.Selected is Mode.Random or Mode.RandomBase)
                    {
                        Modes.Selected = RandomMode;
                        Modes.SyncLobby();
                        RefreshLobbyIcons();
                    }
                    ShowModeDetails(panel, Mode.Random);
                    FlipRandomIcon(panel);
                },
            }),
        };
        var customRows = new List<Selectable>
        {
            AddValueRow(container.transform, template, Strings.Enemy, RunCustomizerMod.CustomEnemy,
                        new[] { 0.5f, 0.75f, 1f, 1.1f, 1.25f, 1.5f, 1.75f, 2f, 2.05f, 2.25f, 2.5f, 2.75f, 3f, 3.5f, 4f, 4.5f }),
            AddValueRow(container.transform, template, Strings.Spawn, RunCustomizerMod.CustomSpawnSpeed,
                        new[] { 0.75f, 1f, 1.25f, 1.43f, 1.75f, 2f, 2.5f, 3f, 4f }),
            AddValueRow(container.transform, template, Strings.Count, RunCustomizerMod.CustomEnemyCount,
                        new[] { 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f }),
            AddValueRow(container.transform, template, Strings.Health, RunCustomizerMod.CustomHealth,
                        new[] { 0.1f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f }),
        };
        LinkVertically(randomRows);
        LinkVertically(customRows);

        RegisterInputReceivers(panel, randomRows, customRows);
        container.SetActive(false);
    }

    private static Selectable AddValueRow(Transform container, Transform template, string labelKey,
                                          MelonPreferences_Entry<float> entry, float[] values) =>
        AddRow(container, template, Mode.Custom, labelKey, new Row
        {
            Options = () =>
            {
                var list = new List<float>(values);
                if (!list.Contains(entry.Value)) { list.Add(entry.Value); list.Sort(); }
                return list.ConvertAll(Format);
            },
            Current = () => Format(entry.Value),
            Apply   = option =>
            {
                if (!float.TryParse(option.TrimStart('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return;
                if (entry.Value == v) return;
                entry.Value = v;
                RunCustomizerMod.Save();
                if (Modes.Selected == Mode.Custom) Modes.Reroll();
            },
        });

    private static Transform FindSelectorRow()
    {
        foreach (var selector in Resources.FindObjectsOfTypeAll<Selector>())
        {
            var row = selector.transform.parent;
            if (row != null && row.GetComponentInChildren<TMP_Text>(true) != null && !row.name.StartsWith("RC"))
                return row;
        }
        return null;
    }

    private static Selectable AddRow(Transform container, Transform template, Mode owner, string labelKey, Row row)
    {
        var go = Object.Instantiate(template.gameObject, container);
        go.name = "RC_" + labelKey;
        go.SetActive(true);
        StripGameLogic(go);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000f, RowHeight);

        var selector = go.GetComponentInChildren<Selector>(true);

        // Подпись — первый текст вне переключателя; остальные (серое пояснение под строкой) убираем
        bool labelled = false;
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.IsChildOf(selector.transform)) continue;
            if (labelled) { Object.Destroy(text.gameObject); continue; }
            labelled = true;
            foreach (var loc in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
            Strings.Bind(text, labelKey);
        }

        selector.localizationMode = Selector.LocalizationMode.None;
        if (selector.valueLocalizeString != null) selector.valueLocalizeString.enabled = false;
        AddArrow(selector, selector.previousButton, "<");
        AddArrow(selector, selector.nextButton, ">");

        row.Owner = owner;
        row.Selector = selector;
        selector.add_OnOptionSelected((Il2CppSystem.Action<string>)(Action<string>)(option =>
        {
            try { row.Apply(option); }
            catch (Exception e) { RunCustomizerMod.Log.Error($"[difficulty ui] {e}"); }
        }));
        Rows.Add(row);
        Show(row);
        return selector;
    }

    // Стрелки переключателя игра показывает только при наведении — рисуем постоянные,
    // чтобы было видно, что значение меняется кликом
    private static void AddArrow(Selector selector, Button button, string glyph)
    {
        if (button == null || selector.valueText == null) return;
        var go = Object.Instantiate(selector.valueText.gameObject, button.transform);
        go.name = "RC_Arrow";
        foreach (var loc in go.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
        var text = go.GetComponent<TMP_Text>();
        text.text = glyph;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        // правая кнопка у игры — отзеркаленная левая: разворачиваем текст обратно
        var t = button.transform;
        if ((t.lossyScale.x < 0f) ^ (t.right.x < 0f)) rt.localScale = new Vector3(-1f, 1f, 1f);
    }

    private static void Show(Row row)
    {
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var o in row.Options()) options.Add(o);
        string current = row.Current();
        row.Selector.SetOptions(options);
        row.Selector.SetSelectedOption(current);
        row.Selector.SetNonLocalizedValueText(current);
    }

    private static string Format(float v) => "x" + v.ToString("0.##", CultureInfo.InvariantCulture);

    // owner == null — прячем все строки
    private static void ShowRows(DifficultiesPanel panel, Mode? owner)
    {
        var container = panel.difficultyExplanationLocalizer?.transform.Find(RowsName);
        if (container == null) return;
        bool any = false;
        foreach (var row in Rows)
        {
            if (row.Selector == null) continue;
            bool visible = owner == row.Owner;
            var rowGo = row.Selector.transform.parent.gameObject;
            rowGo.SetActive(visible);
            if (visible) { any = true; Show(row); }
        }
        container.gameObject.SetActive(any);
        if (any) RandomWizard.AllowClicks(container, panel.transform);
    }

    // ---------- значок сложности в лобби и в забеге ----------

    public static void LobbyIconPostfix(DifficultySelectionDisplayer __instance)
    {
        try
        {
            if (Modes.Selected != Mode.Vanilla && Modes.IsHost && __instance.iconImage != null)
                __instance.iconImage.sprite = Icon(Modes.Selected);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[lobby icon] {e}"); }
    }

    private static void RefreshLobbyIcons()
    {
        var mm = NetworkSingleton<MainMenuManager>.Instance;
        if (mm == null) return;
        foreach (var displayer in Object.FindObjectsOfType<DifficultySelectionDisplayer>())
            displayer.DisplayDifficulty(mm.CurrentDifficulty.Value);
    }

    // «Случайная базовая» здесь раскрывается — остаётся выпавшая родная иконка
    public static void RunIconPostfix(DifficultyLevelVisualizer __instance)
    {
        try
        {
            if (Modes.InRun && __instance.iconImage != null)
                __instance.iconImage.sprite = Icon(Modes.Selected);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[run icon] {e}"); }
    }

    // ---------- общие мелочи UI (как во вкладке Gold & XP) ----------

    private static void LinkVertically(List<Selectable> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var nav = rows[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? rows[i - 1] : null;
            nav.selectOnDown = i < rows.Count - 1 ? rows[i + 1] : null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            rows[i].navigation = nav;
        }
    }

    // Стрелки влево/вправо игра раздаёт через UIInputRouter только переключателям
    // из UIPanel.inputReceivers — добавляем свои строки в UIPanel окна сложности.
    private static void RegisterInputReceivers(DifficultiesPanel panel, params List<Selectable>[] groups)
    {
        var owner = panel.GetComponent<UIPanel>() ?? panel.GetComponentInChildren<UIPanel>(true);
        if (owner == null) { RunCustomizerMod.Log.Warning("[difficulty ui] no UIPanel — arrow keys won't change values"); return; }
        owner.inputReceivers ??= new Il2CppSystem.Collections.Generic.List<IUIInputReceiver>();
        foreach (var group in groups)
            foreach (var s in group)
                owner.inputReceivers.Add(s.TryCast<IUIInputReceiver>() ?? new IUIInputReceiver(s.Pointer));
    }

    private static void StripGameLogic(GameObject row)
    {
        foreach (var behaviour in row.GetComponents<MonoBehaviour>())
            if (behaviour.GetIl2CppType().Name != "Image")
                Object.DestroyImmediate(behaviour);
        foreach (var text in row.GetComponentsInChildren<TextReplacer>(true))
            Object.DestroyImmediate(text);
    }

    private static void MuteSerializedClicks(Button button)
    {
        if (button == null) return;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }
}
