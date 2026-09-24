using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoldXpMultiplier;

// Вкладка мода в меню настроек игры (рядом с General / Video / Audio / Controls).
// Кнопка и панель клонируются из родных, поэтому выглядят и звучат как игровые.
// Подписи — на языке игры (Strings), управление — как у родных строк.
// Значения хранятся в том же MelonPreferences.cfg.
internal static class SettingsTab
{
    private const string ButtonName = "SBMod_Button";
    private const string PanelName  = "SBMod_Panel";

    private static readonly float[] Presets =
        { 0.5f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 7.5f, 10f, 15f, 20f, 25f, 50f, 100f };

    private sealed class Row
    {
        public Selector Selector;
        public Func<List<string>> Options;
        public Func<string> Current;
        public Action<string> Apply;
    }

    private static readonly List<Row> Rows = new();
    private static bool _followsLanguage;

    // Префикс SettingsPanel.SetupSubPanels: успеваем добавить свою вкладку в subPanels
    // до того, как игра подпишет кнопки вкладок.
    public static void InjectPrefix(SettingsPanel __instance)
    {
        try { Inject(__instance); }
        catch (Exception e) { GoldXpMod.Log.Error($"[settings tab] inject failed: {e}"); }
    }

    private static void Inject(SettingsPanel settings)
    {
        var subs = settings.subPanels;
        if (subs == null || subs.Count == 0) return;
        for (int i = 0; i < subs.Count; i++)
            if (subs[i].Panel != null && subs[i].Panel.name == PanelName) return;

        SettingsPanel.SubPanel panelTemplate = null, buttonTemplate = null;
        int insertAt = subs.Count;
        for (int i = 0; i < subs.Count; i++)
        {
            var s = subs[i];
            if (s.Button == null || s.Panel == null) continue;
            if (s.Panel.name.StartsWith("General")) panelTemplate = s;
            if (s.Button.name.StartsWith("Controls")) buttonTemplate = s;
            if (s.Button.name.StartsWith("Credits")) insertAt = i;
        }
        panelTemplate ??= subs[0];
        buttonTemplate ??= subs[0];

        if (!_followsLanguage)
        {
            _followsLanguage = true;
            Strings.OnLanguageChanged(() => MelonCoroutines.Start(RefreshNextFrame(onlyVisible: false)));
        }

        // --- кнопка вкладки ---
        var buttonGo = Object.Instantiate(buttonTemplate.Button.gameObject, buttonTemplate.Button.transform.parent);
        buttonGo.name = ButtonName;
        if (insertAt < subs.Count)
            buttonGo.transform.SetSiblingIndex(subs[insertAt].Button.transform.GetSiblingIndex());
        BindText(buttonGo.GetComponentInChildren<TMP_Text>(true), Strings.Tab);
        MuteSerializedClicks(buttonGo.GetComponent<Button>());
        var button = buttonGo.GetComponent<SelectableButton>();

        // --- панель ---
        var panelGo = Object.Instantiate(panelTemplate.Panel.gameObject, panelTemplate.Panel.transform.parent);
        panelGo.name = PanelName;
        panelGo.SetActive(false);
        var panel = panelGo.GetComponent<NavigateablePanel>();

        var container = panelGo.GetComponentInChildren<VerticalLayoutGroup>(true)?.transform
                        ?? throw new InvalidOperationException("row container not found");

        Transform rowTemplate = null;
        for (int i = 0; i < container.childCount && rowTemplate == null; i++)
        {
            var child = container.GetChild(i);
            if (child.name.StartsWith("DamageNumbers")) rowTemplate = child;
        }
        for (int i = 0; i < container.childCount && rowTemplate == null; i++)
        {
            var child = container.GetChild(i);
            if (child.GetComponentInChildren<Selector>(true) != null) rowTemplate = child;
        }
        if (rowTemplate == null) throw new InvalidOperationException("selector row template not found");

        rowTemplate.SetParent(null, false);
        for (int i = container.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(container.GetChild(i).gameObject);
        StripGameLogic(rowTemplate.gameObject);

        var selectors = new List<Selectable>
        {
            AddToggleRow(container, rowTemplate, Strings.Enabled, GoldXpMod._enabledEntry),
            AddMultiplierRow(container, rowTemplate, Strings.Gold, GoldXpMod._goldEntry),
            AddMultiplierRow(container, rowTemplate, Strings.Xp, GoldXpMod._xpEntry),
            AddMultiplierRow(container, rowTemplate, Strings.RankXp, GoldXpMod._rankXpEntry),
        };
        // Только вверх/вниз: влево/вправо у строки меняют значение, а не уводят фокус
        for (int i = 0; i < selectors.Count; i++)
            SetExplicit(selectors[i],
                up:    i > 0 ? selectors[i - 1] : null,
                down:  i < selectors.Count - 1 ? selectors[i + 1] : null,
                left:  null,
                right: null);
        AddHint(container, rowTemplate);
        Object.DestroyImmediate(rowTemplate.gameObject);

        panel.firstSelected = selectors[0].gameObject;
        RegisterInputReceivers(settings, panelTemplate.Panel.GetComponentInChildren<Selector>(true), selectors);

        var sub = new SettingsPanel.SubPanel { Button = button, Panel = panel };
        subs.Insert(insertAt, sub);

        // Значения могли поменяться в другом экземпляре меню или в cfg — обновляем при открытии
        button.add_OnClicked((Il2CppSystem.Action)(Action)(() => MelonCoroutines.Start(RefreshNextFrame())));

    }

    // Постфикс SettingsPanel.SetupSubPanels: у кнопок вкладок явная навигация
    // (у «Управления» «вниз» ведёт сразу на «Авторов»), встраиваем свою кнопку в цепочку
    public static void LinkTabButtonsPostfix(SettingsPanel __instance)
    {
        try
        {
            var subs = __instance.subPanels;
            if (subs == null) return;
            var buttons = new List<Button>();
            Selectable ourFirstRow = null;
            for (int i = 0; i < subs.Count; i++)
            {
                var b = subs[i].Button != null ? subs[i].Button.GetComponent<Button>() : null;
                if (b != null) buttons.Add(b);
                if (subs[i].Panel != null && subs[i].Panel.name == PanelName)
                    ourFirstRow = subs[i].Panel.GetComponentInChildren<Selector>(true);
            }
            if (buttons.Count < 2) return;
            buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            var firstUp = buttons[0].navigation.selectOnUp;
            var lastDown = buttons[buttons.Count - 1].navigation.selectOnDown;
            Selectable templateRight = null;
            foreach (var b in buttons)
                if (b.name != ButtonName && b.navigation.selectOnRight != null) { templateRight = b.navigation.selectOnRight; break; }

            for (int i = 0; i < buttons.Count; i++)
            {
                var nav = buttons[i].navigation;
                bool ours = buttons[i].name == ButtonName;
                SetExplicit(buttons[i],
                    up:    i > 0 ? buttons[i - 1] : firstUp,
                    down:  i < buttons.Count - 1 ? buttons[i + 1] : lastDown,
                    left:  ours ? null : nav.selectOnLeft,
                    right: ours ? (templateRight != null ? ourFirstRow : null) : nav.selectOnRight);
            }
        }
        catch (Exception e) { GoldXpMod.Log.Error($"[settings tab] navigation: {e}"); }
    }

    private static void SetExplicit(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (s == null) return;
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        s.navigation = nav;
    }

    // Стрелки влево/вправо игра раздаёт через UIInputRouter только переключателям,
    // перечисленным в UIPanel.inputReceivers (список задан в редакторе игры).
    // Добавляем свои строки в тот же UIPanel, что и родные.
    private static void RegisterInputReceivers(SettingsPanel settings, Selector nativeSelector, List<Selectable> selectors)
    {
        var candidates = new List<UIPanel>();
        foreach (var p in settings.GetComponentsInParent<UIPanel>(true)) candidates.Add(p);
        foreach (var p in settings.GetComponentsInChildren<UIPanel>(true)) candidates.Add(p);

        UIPanel owner = null;
        if (nativeSelector != null)
            foreach (var p in candidates)
            {
                var list = p.inputReceivers;
                if (list == null) continue;
                for (int i = 0; i < list.Count && owner == null; i++)
                    if (list[i] != null && list[i].Pointer == nativeSelector.Pointer) owner = p;
                if (owner != null) break;
            }
        owner ??= candidates.Count > 0 ? candidates[0] : null;
        if (owner == null) { GoldXpMod.Log.Warning("[settings tab] no UIPanel — arrow keys won't change values"); return; }

        owner.inputReceivers ??= new Il2CppSystem.Collections.Generic.List<IUIInputReceiver>();
        var router = SingletonPersistent<UIInputRouter>.Instance;
        bool ownerIsOpen = false;
        if (router?.currentReceivers != null && nativeSelector != null)
            for (int i = 0; i < router.currentReceivers.Count; i++)
                if (router.currentReceivers[i] != null && router.currentReceivers[i].Pointer == nativeSelector.Pointer) ownerIsOpen = true;

        foreach (var s in selectors)
        {
            var receiver = s.TryCast<IUIInputReceiver>() ?? new IUIInputReceiver(s.Pointer);
            owner.inputReceivers.Add(receiver);
            if (ownerIsOpen) router.Register(receiver); // панель уже открыта — её OnOpen мы пропустили
        }
    }

    private static Selectable AddToggleRow(Transform container, Transform template, string labelKey,
                                           MelonPreferences_Entry<bool> entry)
    {
        return AddRow(container, template, labelKey, new Row
        {
            Options = () => new List<string> { Strings.Get(Strings.On), Strings.Get(Strings.Off) },
            Current = () => Strings.Get(entry.Value ? Strings.On : Strings.Off),
            Apply   = option => Store(entry, option == Strings.Get(Strings.On)),
        });
    }

    private static Selectable AddMultiplierRow(Transform container, Transform template, string labelKey,
                                               MelonPreferences_Entry<float> entry)
    {
        return AddRow(container, template, labelKey, new Row
        {
            Options = () =>
            {
                var values = new List<float>(Presets);
                if (!values.Contains(entry.Value)) { values.Add(entry.Value); values.Sort(); }
                return values.ConvertAll(Format);
            },
            Current = () => Format(entry.Value),
            Apply   = option =>
            {
                if (float.TryParse(option.TrimStart('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    Store(entry, v);
            },
        });
    }

    private static Selectable AddRow(Transform container, Transform template, string labelKey, Row row)
    {
        var go = Object.Instantiate(template.gameObject, container);
        go.name = "SBMod_" + labelKey;
        go.SetActive(true);

        var selector = go.GetComponentInChildren<Selector>(true);
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            if (!text.transform.IsChildOf(selector.transform))
                BindText(text, labelKey);

        selector.localizationMode = Selector.LocalizationMode.None;
        if (selector.valueLocalizeString != null) selector.valueLocalizeString.enabled = false;

        row.Selector = selector;
        selector.add_OnOptionSelected((Il2CppSystem.Action<string>)(Action<string>)(option =>
        {
            try { row.Apply(option); }
            catch (Exception e) { GoldXpMod.Log.Error($"[settings tab] {e}"); }
        }));
        Rows.Add(row);
        Show(row);
        return selector;
    }

    private static void AddHint(Transform container, Transform template)
    {
        var go = Object.Instantiate(template.gameObject, container);
        go.name = "SBMod_Hint";
        go.SetActive(true);
        Object.DestroyImmediate(go.GetComponentInChildren<Selector>(true).gameObject);

        var text = go.GetComponentInChildren<TMP_Text>(true);
        BindText(text, Strings.Hint);
        text.fontSize *= 0.7f;
        text.alignment = TextAlignmentOptions.TopLeft;
        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(0f, 140f);
        rt.anchoredPosition = new Vector2(0f, -40f);
    }

    private static System.Collections.IEnumerator RefreshNextFrame(bool onlyVisible = true)
    {
        yield return null;
        Rows.RemoveAll(r => r.Selector == null);
        foreach (var row in Rows)
            if (!onlyVisible || row.Selector.isActiveAndEnabled) Show(row);
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

    private static void Store<T>(MelonPreferences_Entry<T> entry, T value)
    {
        if (Equals(entry.Value, value)) return;
        entry.Value = value;
        GoldXpMod.SaveSettings();
    }

    private static string Format(float v) => "x" + v.ToString("0.##", CultureInfo.InvariantCulture);

    // Убираем родную локализацию текста и привязываем его к переводам мода
    internal static void BindText(TMP_Text text, string key)
    {
        if (text == null) return;
        foreach (var loc in text.GetComponents<LocalizeStringEvent>())
            Object.DestroyImmediate(loc);
        Strings.Bind(text, key);
    }

    // Убираем с клона строки всё, что привязывает её к настройке игры
    // (SettingReactor, скрытие по платформе, замена навигации и т.п.)
    private static void StripGameLogic(GameObject row)
    {
        foreach (var behaviour in row.GetComponents<MonoBehaviour>())
            if (behaviour.GetIl2CppType().Name != "Image")
                Object.DestroyImmediate(behaviour);
        foreach (var text in row.GetComponentsInChildren<TextReplacer>(true))
            Object.DestroyImmediate(text);
    }

    // У клона кнопки остаются сериализованные обработчики исходной кнопки — выключаем их.
    private static void MuteSerializedClicks(Button button)
    {
        if (button == null) return;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }
}
