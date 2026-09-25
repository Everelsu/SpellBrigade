using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBrigade.ModMenu;

// Вкладка «Моды» в окне настроек игры. Кнопка и панель — копии родных (General/Controls),
// строки — родные переключатели «< значение >» и кнопки, поэтому всё выглядит, звучит и
// управляется мышью/клавиатурой/геймпадом как в игре.
//
// Разметка строки настройки (единицы интерфейса игры, строка шириной 1200):
//   [подпись — до стрелок переключателя] [‹ значение ›] [↻]
// Стрелки переключателя выходят за его края примерно на 135, поэтому подпись заканчивается
// раньше, а значок сброса стоит отдельной колонкой справа от правой стрелки — у всех строк
// на одной вертикали. У заголовка мода в той же колонке — «По умолчанию ↻» для всего раздела.
internal static class MenuTab
{
    private const string ButtonName = "ModMenu_Button", PanelName = "ModMenu_Panel";
    private const float SectionGap = 24f, NoteHeight = 110f, ScrollbarX = 40f, ScrollbarWidth = 6f;
    private const float RowGap = 24f, OptionRowHeight = 126f, HeaderHeight = 110f, SubHeaderHeight = 90f; // шаг настроек — 150, как у игры
    private const float TopArea = 140f, SearchX = 100f, SearchHeight = 80f; // поиск над списком
    private const float ArrowReach = 150f;                     // насколько стрелки выходят за переключатель
    private const float IconColumn = ArrowReach + 60f;         // центр колонки сброса правее строки
    private const float IconSize = 56f;
    private const float LabelScale = 0.9f, LabelHeight = 130f; // подписи одного размера, длинные — в 2 строки
    private const float ButtonHeight = 110f, ButtonRowHeight = 126f, ButtonGap = 30f, ButtonPadding = 150f, MinButtonWidth = 320f;
    // Подсветка стрелок и колонка сброса вылезают за правый край панели — маска прокрутки
    // режет только сверху и снизу, по бокам даём запас
    private const float SideSlack = 400f;
    private static readonly Color Gold = new(0.96f, 0.78f, 0.35f), NoteColor = new(1f, 1f, 1f, 0.55f), Dim = new(1f, 1f, 1f, 0.55f);

    private sealed class Row
    {
        public Item Item;
        public GameObject Go;
        public TMP_Text Label;
        public Selector Selector;
        public List<string> Options = new();
        public readonly List<Selectable> Focus = new(); // что получает фокус; у строки кнопок — все слева направо
        public string Raw = "";                          // подпись без подсветки поиска
        public GameObject Reset;                         // «к стандартному», видна, когда значение не стандартное
        public readonly List<(TMP_Text text, LayoutElement element, MenuButton spec)> Buttons = new();
    }

    // Раздел мода: заголовок (он же кнопка «свернуть/развернуть»), отступ перед ним
    // (кроме первого) и строки
    private sealed class Section
    {
        public string Id;
        public Row Title;
        public RectTransform Spacer, Chevron;
        public GameObject ResetAll;
        public bool Expanded;
        public readonly List<Row> Rows = new();
    }

    private sealed class Screen
    {
        public GameObject Panel;
        public NavigateablePanel Navigateable;
        public Button Tab;
        public ScrollRect Scroll;
        public readonly List<Row> Rows = new();
        public readonly List<Section> Sections = new();
        public GameObject Empty;
        public TMP_InputField Search;
        public Image SearchLine;
        public GameObject Clear;
        public string Query = "";
        public readonly Dictionary<IntPtr, RectTransform> RowOf = new();
        public readonly Dictionary<IntPtr, Row> RowByFocus = new();
        public Selectable First;
        public bool WasActive;
    }

    // Шаблоны строк из родной панели «Общие»
    private sealed class Templates
    {
        public Transform SelectorRow;
        public GameObject Button;   // родная кнопка (ClickableButton) — для кнопок и действий
        public Sprite Banner;       // подсветка вкладки настроек («Общие», «Видео»…) — для заголовков модов
        public Sprite Arrow;        // стрелка родного переключателя — для «свернуть/развернуть»
        public float RowWidth, ValueWidth, LabelSize;
    }

    private static readonly List<Screen> Screens = new();
    private static bool _subscribed, _refreshing, _wasTyping;
    private static IntPtr _lastSelected;
    private static HashSet<string> _expanded; // развёрнутые разделы (id страниц), общие для всех экземпляров меню

    // --- постройка ---

    // Префикс SettingsPanel.SetupSubPanels: добавляем вкладку в subPanels до того,
    // как игра подпишет кнопки вкладок
    public static void InjectPrefix(SettingsPanel __instance)
    {
        try { Inject(__instance); }
        catch (Exception e) { ModMenuMod.Log.Error($"can't build the Mods tab: {e}"); }
    }

    private static void Inject(SettingsPanel settings)
    {
        var subs = settings.subPanels;
        if (subs == null || subs.Count == 0) return;
        for (int i = 0; i < subs.Count; i++)
            if (subs[i].Panel != null && subs[i].Panel.name == PanelName) return;

        var pages = new List<Page>(Menu.Pages);
        pages.AddRange(AutoPages.Build());
        pages.RemoveAll(p => p.Items.Count == 0);
        if (pages.Count == 0) return;
        pages.Sort((a, b) => string.Compare(Safe(a.Title, a.Id), Safe(b.Title, b.Id), StringComparison.CurrentCultureIgnoreCase));

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

        if (!_subscribed)
        {
            _subscribed = true;
            Strings.OnLanguageChanged(RefreshAll);
            Menu.RefreshRequested += RefreshAll;
        }

        // --- кнопка вкладки ---
        var buttonGo = Object.Instantiate(buttonTemplate.Button.gameObject, buttonTemplate.Button.transform.parent);
        buttonGo.name = ButtonName;
        if (insertAt < subs.Count)
            buttonGo.transform.SetSiblingIndex(subs[insertAt].Button.transform.GetSiblingIndex());
        var tabText = buttonGo.GetComponentInChildren<TMP_Text>(true);
        StripLocalization(tabText);
        Strings.Bind(tabText, Strings.Tab);
        MuteSerializedClicks(buttonGo.GetComponent<Button>());
        var button = buttonGo.GetComponent<SelectableButton>();

        // --- панель ---
        var panelGo = Object.Instantiate(panelTemplate.Panel.gameObject, panelTemplate.Panel.transform.parent);
        panelGo.name = PanelName;
        panelGo.SetActive(false);
        var panel = panelGo.GetComponent<NavigateablePanel>();
        var screen = new Screen { Panel = panelGo, Navigateable = panel, Tab = buttonGo.GetComponent<Button>() };

        var container = panelGo.GetComponentInChildren<VerticalLayoutGroup>(true)?.GetComponent<RectTransform>()
                        ?? throw new InvalidOperationException("row container not found");
        var t = new Templates();
        Transform buttonRow = null;
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child.name.StartsWith("DamageNumbers")) t.SelectorRow = child;
            if (buttonRow == null && child.GetComponentInChildren<ClickableButton>(true) != null) buttonRow = child;
        }
        for (int i = 0; i < container.childCount && t.SelectorRow == null; i++)
            if (container.GetChild(i).GetComponentInChildren<Selector>(true) != null) t.SelectorRow = container.GetChild(i);
        if (t.SelectorRow == null) throw new InvalidOperationException("selector row template not found");

        t.SelectorRow.SetParent(null, false);
        buttonRow?.SetParent(null, false);
        for (int i = container.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(container.GetChild(i).gameObject);
        StripGameLogic(t.SelectorRow.gameObject);
        t.RowWidth = t.SelectorRow.GetComponent<RectTransform>().sizeDelta.x;
        var sampleSelector = t.SelectorRow.GetComponentInChildren<Selector>(true);
        t.ValueWidth = sampleSelector.GetComponent<RectTransform>().sizeDelta.x;
        t.Arrow = sampleSelector.nextButton != null && sampleSelector.nextButton.image != null ? sampleSelector.nextButton.image.sprite : null;
        t.Banner = buttonTemplate.Button.transform.Find("Button_Image")?.GetComponent<Image>()?.sprite;
        var layoutGroup = container.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.spacing = RowGap;
        var sampleLabel = FirstLabel(t.SelectorRow.gameObject);
        t.LabelSize = (sampleLabel.enableAutoSizing ? sampleLabel.fontSizeMax : sampleLabel.fontSize) * LabelScale;
        if (buttonRow != null)
        {
            t.Button = buttonRow.GetComponentInChildren<ClickableButton>(true).gameObject;
            t.Button.transform.SetParent(null, false);
            Object.DestroyImmediate(buttonRow.gameObject);
        }

        var panelRect = panelGo.GetComponent<RectTransform>();
        BuildScrollView(screen, panelRect, container);
        BuildSearch(screen, panelRect, t);

        // --- разделы модов ---
        for (int p = 0; p < pages.Count; p++)
        {
            var page = pages[p];
            var section = new Section { Id = page.Id, Expanded = Expanded.Contains(page.Id) };
            if (p > 0) section.Spacer = Spacer(container, t.RowWidth, SectionGap);
            section.Title = AddRow(screen, new HeaderItem { Label = page.Title, IsPageTitle = true }, container, t, page.Id);
            if (section.Title == null) continue;
            MakeCollapsible(screen, section, t);
            foreach (var item in page.Items)
            {
                var row = AddRow(screen, item, container, t, page.Id);
                if (row != null) section.Rows.Add(row);
            }
            section.ResetAll = SectionReset(section, t);
            screen.Sections.Add(section);
        }
        screen.Empty = TextRow(t, container, out var empty);
        empty.color = NoteColor;
        Strings.Bind(empty, Strings.NothingFound);
        screen.Empty.SetActive(false);
        Object.DestroyImmediate(t.SelectorRow.gameObject);
        if (t.Button != null) Object.DestroyImmediate(t.Button);

        var selectors = new List<Selectable>();
        foreach (var row in screen.Rows)
            if (row.Selector != null) selectors.Add(row.Selector);
        RegisterInputReceivers(settings, panelTemplate.Panel.GetComponentInChildren<Selector>(true), selectors);

        subs.Insert(insertAt, new SettingsPanel.SubPanel { Button = button, Panel = panel });
        Screens.Add(screen);
        Refresh(screen);
        Filter(screen, resetScroll: true); // свёрнутые разделы, навигация
    }

    private static HashSet<string> Expanded
    {
        get
        {
            if (_expanded != null) return _expanded;
            _expanded = new HashSet<string>();
            foreach (var id in (ModMenuMod.ExpandedEntry?.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                _expanded.Add(id);
            return _expanded;
        }
    }

    // Заголовок мода — кнопка на всю строку: клик, Enter или A на геймпаде сворачивают/разворачивают.
    // При наведении и выборе под ним та же подсветка, что у вкладок настроек игры, слева — стрелка
    // родного переключателя (вправо — свёрнут, вниз — развёрнут).
    private static void MakeCollapsible(Screen screen, Section section, Templates t)
    {
        var header = section.Title;
        var catcher = header.Go.GetComponent<Image>() ?? header.Go.AddComponent<Image>(); // клик по всей строке
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        var banner = NewRect("Highlight", header.Go.transform).gameObject.AddComponent<Image>();
        banner.sprite = t.Banner;
        banner.raycastTarget = false;
        var br = banner.rectTransform;
        br.anchorMin = new Vector2(0f, 0.5f); br.anchorMax = new Vector2(1f, 0.5f); br.pivot = new Vector2(0f, 0.5f);
        br.offsetMin = new Vector2(-40f, -HeaderHeight / 2f); br.offsetMax = new Vector2(t.Banner != null ? -t.ValueWidth * 0.3f : 0f, HeaderHeight / 2f);
        br.SetAsFirstSibling();

        var button = header.Go.AddComponent<Button>();
        button.targetGraphic = banner;
        var colors = button.colors;
        var tint = t.Banner != null ? Color.white : Gold; // без родной картинки — просто лёгкая заливка
        float scale = t.Banner != null ? 1f : 0.15f;
        colors.normalColor = new Color(tint.r, tint.g, tint.b, 0f);
        colors.highlightedColor = new Color(tint.r, tint.g, tint.b, 0.75f * scale);
        colors.selectedColor = new Color(tint.r, tint.g, tint.b, 1f * scale);
        colors.pressedColor = new Color(tint.r, tint.g, tint.b, 1f * scale);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        string id = section.Id;
        button.onClick.AddListener((UnityAction)(Action)(() => Toggle(id)));

        var arrow = NewRect("Arrow", header.Go.transform).gameObject.AddComponent<Image>();
        arrow.sprite = t.Arrow ?? Icons.Chevron;
        arrow.preserveAspect = true;
        arrow.color = new Color(1f, 1f, 1f, 0.85f);
        arrow.raycastTarget = false;
        var ar = arrow.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(0f, 0.5f);
        ar.pivot = new Vector2(0.5f, 0.5f);
        ar.sizeDelta = new Vector2(52f, 52f);
        ar.anchoredPosition = new Vector2(26f, 0f);
        section.Chevron = ar;
        header.Label.rectTransform.offsetMin = new Vector2(72f, header.Label.rectTransform.offsetMin.y);

        header.Focus.Add(button);
        screen.RowOf[button.gameObject.Pointer] = header.Go.GetComponent<RectTransform>();
        screen.RowByFocus[button.gameObject.Pointer] = header;
    }

    private static void SetHeight(GameObject row, float height)
    {
        var rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        var element = row.GetComponent<LayoutElement>();
        if (element != null) element.preferredHeight = height;
    }

    private static void Toggle(string id)
    {
        bool expand = !Expanded.Contains(id);
        if (expand) Expanded.Add(id); else Expanded.Remove(id);
        ModMenuMod.ExpandedEntry.Value = string.Join(",", Expanded);
        ModMenuMod.Save();
        Screens.RemoveAll(s => s.Panel == null);
        foreach (var screen in Screens)
        {
            foreach (var section in screen.Sections)
                if (section.Id == id) section.Expanded = expand;
            Filter(screen, resetScroll: false);
        }
    }

    private static Row AddRow(Screen screen, Item item, RectTransform container, Templates t, string pageId)
    {
        try
        {
            var row = CreateRow(item, container, t);
            if (row == null) return null;
            row.Go.name = $"ModMenu_{pageId}_{screen.Rows.Count}";
            foreach (var focus in row.Focus)
            {
                screen.RowOf[focus.gameObject.Pointer] = row.Go.GetComponent<RectTransform>();
                screen.RowByFocus[focus.gameObject.Pointer] = row;
            }
            screen.Rows.Add(row);
            return row;
        }
        catch (Exception e)
        {
            ModMenuMod.Log.Warning($"page '{pageId}': skipped a row — {e.Message}");
            return null;
        }
    }

    private static Row CreateRow(Item item, RectTransform container, Templates t)
    {
        var row = new Row { Item = item };
        switch (item)
        {
            case OptionItem option:
                row.Go = Object.Instantiate(t.SelectorRow.gameObject, container);
                SetHeight(row.Go, OptionRowHeight);
                row.Label = FirstLabel(row.Go);
                StyleSettingLabel(row.Label, t);
                row.Selector = SetupSelector(row.Go, value =>
                {
                    int index = row.Options.IndexOf(value);
                    if (index < 0) return;
                    try { option.Apply(index); }
                    catch (Exception e) { ModMenuMod.Log.Error($"setting '{row.Raw}': {e}"); }
                    UpdateReset(row);
                });
                row.Focus.Add(row.Selector);
                if (option.Reset != null)
                    row.Reset = IconButton(row.Go.transform, "ModMenu_Reset", () =>
                    {
                        ResetRow(row);
                        EventSystem.current?.SetSelectedGameObject(row.Selector.gameObject); // фокус — обратно на строку
                    });
                break;

            case HeaderItem header:
                row.Go = TextRow(t, container, out row.Label);
                if (header.IsPageTitle)
                {
                    // имя мода — белым и крупнее, как названия вкладок настроек
                    SetHeight(row.Go, HeaderHeight);
                    row.Label.fontSize = t.LabelSize * 1.25f;
                    row.Label.color = Color.white;
                    row.Label.rectTransform.offsetMax = new Vector2(-(t.ValueWidth * 0.6f), row.Label.rectTransform.offsetMax.y);
                }
                else
                {
                    // подзаголовок внутри раздела — мелкий золотой
                    SetHeight(row.Go, SubHeaderHeight);
                    row.Label.fontSize = t.LabelSize * 0.8f;
                    row.Label.fontStyle |= FontStyles.Bold;
                    row.Label.color = Gold;
                    row.Label.characterSpacing = 4f;
                }
                break;

            case NoteItem:
                row.Go = TextRow(t, container, out row.Label);
                row.Label.fontSize = t.LabelSize * 0.75f;
                row.Label.color = NoteColor;
                row.Label.alignment = TextAlignmentOptions.TopLeft;
                row.Label.textWrappingMode = TextWrappingModes.Normal;
                var noteRect = row.Go.GetComponent<RectTransform>();
                noteRect.sizeDelta = new Vector2(noteRect.sizeDelta.x, NoteHeight);
                var nr = row.Label.rectTransform;
                nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.pivot = new Vector2(0f, 1f);
                nr.offsetMin = nr.offsetMax = Vector2.zero;
                break;

            case ButtonItem buttons:
                if (t.Button == null || buttons.Buttons.Count == 0) return null;
                BuildButtons(row, buttons, container, t);
                break;

            case CustomItem custom:
                var rect = Spacer(container, t.RowWidth, custom.Height);
                rect.name = "Custom";
                row.Go = rect.gameObject;
                var focus = custom.Build?.Invoke(rect);
                if (focus != null) row.Focus.Add(focus);
                break;
        }
        return row;
    }

    // Строка кнопок: раскладка из ButtonLayout, у Action — ещё и подпись слева
    private static void BuildButtons(Row row, ButtonItem item, RectTransform container, Templates t)
    {
        RectTransform rect;
        if (item.Label != null)
        {
            row.Go = TextRow(t, container, out row.Label);
            StyleSettingLabel(row.Label, t);
            rect = row.Go.GetComponent<RectTransform>();
        }
        else
        {
            rect = Spacer(container, t.RowWidth, ButtonRowHeight);
            row.Go = rect.gameObject;
        }
        rect.sizeDelta = new Vector2(t.RowWidth, ButtonRowHeight);
        var element = row.Go.GetComponent<LayoutElement>() ?? row.Go.AddComponent<LayoutElement>();
        element.preferredHeight = ButtonRowHeight;

        var area = NewRect("Buttons", rect);
        if (item.Layout == ButtonLayout.Value)
        {
            // над колонкой значений, включая место под стрелками
            area.anchorMin = area.anchorMax = new Vector2(1f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.anchoredPosition = new Vector2(-t.ValueWidth / 2f, 0f);
            area.sizeDelta = new Vector2(t.ValueWidth + 2f * ArrowReach, ButtonHeight);
        }
        else
        {
            area.anchorMin = new Vector2(0f, 0.5f); area.anchorMax = new Vector2(1f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(0f, ButtonHeight);
        }
        var layout = area.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = ButtonGap;
        layout.childAlignment = item.Layout switch
        {
            ButtonLayout.Left => TextAnchor.MiddleLeft,
            ButtonLayout.Right => TextAnchor.MiddleRight,
            _ => TextAnchor.MiddleCenter,
        };
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = item.Layout == ButtonLayout.Stretch;

        foreach (var spec in item.Buttons)
        {
            var go = Object.Instantiate(t.Button, area);
            go.name = "Button";
            go.SetActive(true);
            MuteSerializedClicks(go.GetComponent<Button>());
            // шаблон — красная «Сбросить сохранение»: возвращаем картинкам обычный белый
            foreach (var image in go.GetComponentsInChildren<Image>(true))
                image.color = new Color(1f, 1f, 1f, image.color.a);
            var text = go.GetComponentInChildren<TMP_Text>(true);
            StripLocalization(text);
            text.enableAutoSizing = false;
            text.fontSize = t.LabelSize * 0.85f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Center;

            var size = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            size.preferredHeight = ButtonHeight;
            if (item.Layout == ButtonLayout.Stretch) { size.flexibleWidth = 1f; size.preferredWidth = 0f; }

            var action = spec.OnClick;
            go.GetComponent<ClickableButton>().add_OnClicked((Il2CppSystem.Action)(Action)(() =>
            {
                try { action?.Invoke(); }
                catch (Exception e) { ModMenuMod.Log.Error($"button '{text.text}': {e}"); }
                RefreshAll();
            }));
            row.Focus.Add(go.GetComponent<Button>());
            row.Buttons.Add((text, size, spec));
        }
    }

    // Подпись настройки: одного размера во всех строках, длинная — в две строки
    private static void StyleSettingLabel(TMP_Text label, Templates t)
    {
        label.enableAutoSizing = false;
        label.fontSize = t.LabelSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.maxVisibleLines = 2;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var r = label.rectTransform;
        // до левой стрелки переключателя
        r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(0f, -LabelHeight / 2f);
        r.offsetMax = new Vector2(-(t.ValueWidth + ArrowReach + 10f), LabelHeight / 2f);
    }

    // Строка только с текстом — копия обычной строки без переключателя
    private static GameObject TextRow(Templates t, RectTransform container, out TMP_Text label)
    {
        var go = Object.Instantiate(t.SelectorRow.gameObject, container);
        Object.DestroyImmediate(go.GetComponentInChildren<Selector>(true).gameObject);
        label = FirstLabel(go);
        label.enableAutoSizing = false;
        label.fontSize = t.LabelSize;
        var r = label.rectTransform;
        r.offsetMax = new Vector2(0f, r.offsetMax.y); // текст на всю ширину строки
        return go;
    }

    private static RectTransform Spacer(RectTransform container, float width, float height)
    {
        var rect = NewRect("Spacer", container);
        rect.sizeDelta = new Vector2(width, height);
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.preferredWidth = width;
        return rect;
    }

    // Значок ↻ в колонке сброса (правее стрелок переключателя)
    private static GameObject IconButton(Transform row, string name, Action onClick)
    {
        var icon = NewRect(name, row).gameObject.AddComponent<Image>();
        icon.sprite = Icons.Reset;
        icon.preserveAspect = true;
        var r = icon.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(IconSize, IconSize);
        r.anchoredPosition = new Vector2(IconColumn, 0f);
        Clickable(icon, onClick);
        icon.gameObject.SetActive(false);
        return icon.gameObject;
    }

    // Тусклое → золотое при наведении, без навигации (не мешает списку)
    private static Button Clickable(Graphic graphic, Action onClick)
    {
        graphic.color = Color.white;
        var button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        var colors = button.colors;
        colors.normalColor = Dim;
        colors.highlightedColor = Gold;
        colors.selectedColor = Dim;
        colors.pressedColor = new Color(Gold.r * 0.8f, Gold.g * 0.8f, Gold.b * 0.8f);
        button.colors = colors;
        NoNavigation(button);
        button.onClick.AddListener((UnityAction)(Action)(() =>
        {
            try { onClick(); }
            catch (Exception e) { ModMenuMod.Log.Error(e.ToString()); }
        }));
        return button;
    }

    // «По умолчанию ↻» в заголовке мода: сбросить весь раздел. Видно, когда в нём что-то изменено.
    private static GameObject SectionReset(Section section, Templates t)
    {
        bool resettable = false;
        foreach (var row in section.Rows) resettable |= row.Reset != null;
        if (!resettable) return null;

        var group = NewRect("ModMenu_ResetAll", section.Title.Go.transform);
        group.anchorMin = group.anchorMax = new Vector2(1f, 0.5f);
        group.pivot = new Vector2(1f, 0.5f);
        group.anchoredPosition = new Vector2(IconColumn + IconSize / 2f, 0f);
        group.sizeDelta = new Vector2(t.ValueWidth + ArrowReach, IconSize);

        void ResetSection()
        {
            foreach (var row in section.Rows)
                if (row.Item is OptionItem { Reset: not null } option && !SafeIsDefault(option))
                    try { option.Reset(); }
                    catch (Exception e) { ModMenuMod.Log.Error($"reset '{row.Raw}': {e}"); }
            RefreshAll();
        }

        var icon = NewRect("Icon", group).gameObject.AddComponent<Image>();
        icon.sprite = Icons.Reset;
        icon.preserveAspect = true;
        var ir = icon.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(1f, 0.5f);
        ir.pivot = new Vector2(1f, 0.5f);
        ir.sizeDelta = new Vector2(IconSize, IconSize);
        Clickable(icon, ResetSection);

        var text = Object.Instantiate(section.Title.Label.gameObject, group).GetComponent<TMP_Text>();
        text.gameObject.name = "Text";
        text.fontStyle = FontStyles.Normal;
        text.fontSize = t.LabelSize * 0.75f;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = true;
        for (int i = text.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(text.transform.GetChild(i).gameObject);
        var tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.pivot = new Vector2(1f, 0.5f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = new Vector2(-(IconSize + 14f), 0f);
        Strings.Bind(text, Strings.Defaults);
        Clickable(text, ResetSection);

        group.gameObject.SetActive(false);
        return group.gameObject;
    }

    private static Selector SetupSelector(GameObject rowGo, Action<string> onSelected)
    {
        var selector = rowGo.GetComponentInChildren<Selector>(true);
        selector.localizationMode = Selector.LocalizationMode.None;
        if (selector.valueLocalizeString != null) selector.valueLocalizeString.enabled = false;
        selector.add_OnOptionSelected((Il2CppSystem.Action<string>)(Action<string>)(option =>
        {
            if (_refreshing) return; // значение выставили мы сами, а не игрок
            try { onSelected(option); }
            catch (Exception e) { ModMenuMod.Log.Error(e.ToString()); }
        }));
        return selector;
    }

    private static void BuildScrollView(Screen screen, RectTransform panel, RectTransform container)
    {
        var viewport = NewRect("ModMenu_Viewport", panel);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = new Vector2(0f, 0f);
        viewport.offsetMax = new Vector2(SideSlack, -TopArea);
        viewport.gameObject.AddComponent<RectMask2D>();
        var catcher = viewport.gameObject.AddComponent<Image>(); // колесо мыши работает и между строками
        catcher.color = new Color(0f, 0f, 0f, 0.001f);

        container.SetParent(viewport, false);
        container.anchorMin = container.anchorMax = container.pivot = new Vector2(0f, 1f);
        container.anchoredPosition = Vector2.zero;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = container;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;
        scroll.verticalScrollbar = BuildScrollbar(panel);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        screen.Scroll = scroll;
    }

    // Тонкая полоса прокрутки у левого края панели, между рамкой и подписями
    private static Scrollbar BuildScrollbar(RectTransform panel)
    {
        var bar = NewRect("ModMenu_Scrollbar", panel);
        bar.anchorMin = Vector2.zero; bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, 0.5f);
        bar.offsetMin = new Vector2(ScrollbarX, 40f); bar.offsetMax = new Vector2(ScrollbarX + ScrollbarWidth, -TopArea);
        bar.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

        var area = NewRect("Sliding Area", bar);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
        var handle = NewRect("Handle", area);
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var colors = scrollbar.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.3f);
        colors.highlightedColor = Gold;
        colors.pressedColor = Gold;
        colors.selectedColor = new Color(1f, 1f, 1f, 0.3f);
        scrollbar.colors = colors;
        NoNavigation(scrollbar); // не перехватывать фокус у списка
        return scrollbar;
    }

    // --- поиск ---

    // Поле поиска над списком. Строка находится по своей подписи, раздел — по имени мода,
    // совпадения подсвечиваются. Ctrl+F — в поиск, Enter — к первому найденному, Esc — выйти
    // из поля (текст остаётся), «×» — очистить. Пока печатаешь, игра клавиатуру не слышит.
    private static void BuildSearch(Screen screen, RectTransform panel, Templates t)
    {
        var rowCopy = Object.Instantiate(t.SelectorRow.gameObject);
        var template = FirstLabel(rowCopy); // шрифт и материал подписей настроек
        template.enableAutoSizing = false;
        template.fontSize = t.LabelSize;

        var box = NewRect("ModMenu_Search", panel);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
        box.anchoredPosition = new Vector2(SearchX, -(TopArea - SearchHeight) / 2f);
        box.sizeDelta = new Vector2(t.RowWidth, SearchHeight);
        var background = box.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.35f);

        var underline = NewRect("Underline", box).gameObject.AddComponent<Image>();
        underline.color = new Color(1f, 1f, 1f, 0.2f);
        screen.SearchLine = underline;
        underline.raycastTarget = false;
        var ur = underline.rectTransform;
        ur.anchorMin = Vector2.zero; ur.anchorMax = new Vector2(1f, 0f); ur.pivot = new Vector2(0.5f, 0f);
        ur.sizeDelta = new Vector2(0f, 3f);

        var icon = NewRect("Icon", box).gameObject.AddComponent<Image>();
        icon.sprite = Icons.Search;
        icon.color = new Color(1f, 1f, 1f, 0.6f);
        icon.raycastTarget = false;
        var ir = icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(46f, 46f);
        ir.anchoredPosition = new Vector2(24f, 0f);

        var area = NewRect("Text Area", box);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(90f, 6f); area.offsetMax = new Vector2(-90f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        template.transform.SetParent(area, false);
        Object.Destroy(rowCopy);
        var placeholder = PlainText(template, "Placeholder");
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        Strings.Bind(placeholder, Strings.Search);
        var text = PlainText(Object.Instantiate(template.gameObject, area).GetComponent<TMP_Text>(), "Text");
        text.color = Color.white;

        // «×» справа — очистить
        var clear = PlainText(Object.Instantiate(template.gameObject, box).GetComponent<TMP_Text>(), "Clear");
        clear.text = "×";
        clear.alignment = TextAlignmentOptions.Center;
        clear.fontSize *= 1.3f;
        clear.raycastTarget = true;
        var cr = clear.rectTransform;
        cr.anchorMin = new Vector2(1f, 0f); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(1f, 0.5f);
        cr.offsetMin = new Vector2(-80f, 0f); cr.offsetMax = Vector2.zero;
        screen.Clear = clear.gameObject;

        var input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = text.font;
        input.pointSize = text.fontSize;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.restoreOriginalTextOnEscape = false; // Esc выходит из поля, но найденное остаётся
        input.caretColor = Gold;
        input.customCaretColor = true;
        input.caretWidth = 3;
        input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
        input.targetGraphic = background;
        NoNavigation(input); // в поиск — мышью или Ctrl+F; по списку он не мешает
        screen.Search = input;

        Clickable(clear, () =>
        {
            input.text = "";
            input.ActivateInputField();
        });
        screen.Clear.SetActive(false);

        input.onValueChanged.AddListener((UnityAction<string>)(Action<string>)(query =>
        {
            screen.Query = query ?? "";
            screen.Clear.SetActive(screen.Query.Length > 0);
            try { Filter(screen); }
            catch (Exception e) { ModMenuMod.Log.Warning($"search: {e.Message}"); }
        }));
        // Enter — сразу к первой найденной настройке (дальше стрелками/геймпадом)
        input.onSubmit.AddListener((UnityAction<string>)(Action<string>)(_ =>
        {
            if (screen.First != null) EventSystem.current?.SetSelectedGameObject(screen.First.gameObject);
        }));
    }

    private static TMP_Text PlainText(TMP_Text text, string name)
    {
        text.gameObject.name = name;
        text.gameObject.SetActive(true);
        text.text = "";
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        var r = text.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        return text;
    }

    // Что видно: без поиска — заголовки всех модов и строки развёрнутых; с поиском —
    // совпадения (если совпало имя мода — весь раздел), свёрнутость не мешает
    private static void Filter(Screen screen, bool resetScroll = true)
    {
        string query = screen.Query.Trim();
        bool searching = query.Length > 0, any = false, first = true;
        foreach (var section in screen.Sections)
        {
            bool titleHit = !searching || Matches(section.Title.Raw, query);
            bool open = searching || section.Expanded;
            bool matched = titleHit;
            foreach (var row in section.Rows)
            {
                bool hit = titleHit || (row.Item is not NoteItem && Matches(row.Raw, query));
                matched |= hit;
                row.Go.SetActive(open && hit);
                ShowLabel(row, query);
            }
            bool visible = !searching || matched;
            section.Title.Go.SetActive(visible);
            ShowLabel(section.Title, query);
            if (section.Chevron != null) section.Chevron.localEulerAngles = new Vector3(0f, 0f, open ? -90f : 0f);
            if (section.Spacer != null) section.Spacer.gameObject.SetActive(visible && !first);
            if (visible) { any = true; first = false; }
        }
        screen.Empty.SetActive(!any);
        if (resetScroll) screen.Scroll.content.anchoredPosition = new Vector2(screen.Scroll.content.anchoredPosition.x, 0f);
        Relink(screen);
    }

    private static bool Matches(string text, string query) =>
        text != null && text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;

    // Подпись с золотой подсветкой найденного
    private static void ShowLabel(Row row, string query)
    {
        if (row.Label == null || row.Item is NoteItem) return;
        string text = row.Raw ?? "";
        if (query.Length > 0)
        {
            var sb = new System.Text.StringBuilder();
            int from = 0, at;
            while ((at = text.IndexOf(query, from, StringComparison.CurrentCultureIgnoreCase)) >= 0)
            {
                sb.Append(text, from, at - from).Append("<mark=#F5C54259>").Append(text, at, query.Length).Append("</mark>");
                from = at + query.Length;
            }
            text = sb.Append(text, from, text.Length - from).ToString();
        }
        if (row.Label.text != text) row.Label.text = text;
    }

    // Навигация по видимым строкам: вверх/вниз между строками (в строке кнопок — к кнопке
    // под/над текущей), влево/вправо — только между кнопками одной строки: у переключателя
    // влево/вправо меняют значение
    private static void Relink(Screen screen)
    {
        var rows = new List<Row>();
        foreach (var row in screen.Rows)
            if (row.Focus.Count > 0 && row.Go.activeSelf) rows.Add(row);

        static Selectable Pick(Row row, int column) => row.Focus[Math.Min(column, row.Focus.Count - 1)];
        for (int i = 0; i < rows.Count; i++)
        {
            var focus = rows[i].Focus;
            for (int j = 0; j < focus.Count; j++)
                SetExplicit(focus[j],
                    up:    i > 0 ? Pick(rows[i - 1], j) : null,
                    down:  i < rows.Count - 1 ? Pick(rows[i + 1], j) : null,
                    left:  j > 0 ? focus[j - 1] : null,
                    right: j < focus.Count - 1 ? focus[j + 1] : null);
        }
        screen.First = rows.Count > 0 ? rows[0].Focus[0] : null;
        if (screen.First != null) screen.Navigateable.firstSelected = screen.First.gameObject;
        if (screen.Tab != null)
        {
            var nav = screen.Tab.navigation;
            nav.selectOnRight = screen.First;
            screen.Tab.navigation = nav;
        }
    }

    // --- значения ---

    private static void RefreshAll()
    {
        Screens.RemoveAll(s => s.Panel == null);
        foreach (var s in Screens)
        {
            Refresh(s);
            if (s.Query.Trim().Length > 0) Filter(s); // подписи могли смениться вместе с языком
        }
    }

    private static void Refresh(Screen screen)
    {
        _refreshing = true;
        try
        {
            foreach (var row in screen.Rows)
            {
                try { RefreshRow(row, screen.Query.Trim()); }
                catch (Exception e) { ModMenuMod.Log.Warning($"can't show '{row.Raw}': {e.Message}"); }
            }
            foreach (var section in screen.Sections)
            {
                if (section.ResetAll == null) continue;
                bool changed = false;
                foreach (var row in section.Rows) changed |= row.Reset != null && row.Reset.activeSelf;
                if (section.ResetAll.activeSelf != changed) section.ResetAll.SetActive(changed);
            }
        }
        finally { _refreshing = false; }
    }

    private static void RefreshRow(Row row, string query)
    {
        if (row.Label != null && row.Item.Label != null)
        {
            row.Raw = Safe(row.Item.Label, "");
            if (row.Item is NoteItem) row.Label.text = row.Raw;
            else ShowLabel(row, query);
        }

        if (row.Item is OptionItem option)
        {
            var (options, current) = option.Read();
            row.Options = options;
            ShowOptions(row.Selector, options, current);
            UpdateReset(row);
        }

        if (row.Buttons.Count > 0)
        {
            var all = new System.Text.StringBuilder(row.Label != null ? row.Raw : "");
            foreach (var (text, element, spec) in row.Buttons)
            {
                string value = Safe(spec.Text, "");
                if (text.text != value) text.text = value;
                all.Append(' ').Append(value);
                if (element.flexibleWidth <= 0f) // ширина по тексту
                    element.preferredWidth = Mathf.Max(MinButtonWidth, text.GetPreferredValues(value).x + ButtonPadding);
            }
            if (row.Label == null) row.Raw = all.ToString(); // поиск находит строку кнопок по их тексту
        }
    }

    private static void UpdateReset(Row row)
    {
        if (row.Reset == null || row.Item is not OptionItem option) return;
        bool changed = !SafeIsDefault(option);
        if (row.Reset.activeSelf != changed) row.Reset.SetActive(changed);
    }

    private static bool SafeIsDefault(OptionItem option)
    {
        try { return option.IsDefault == null || option.IsDefault(); }
        catch { return true; }
    }

    private static void ResetRow(Row row)
    {
        if (row.Item is not OptionItem { Reset: not null } option) return;
        try { option.Reset(); }
        catch (Exception e) { ModMenuMod.Log.Error($"reset '{row.Raw}': {e}"); }
        RefreshAll();
    }

    private static void ShowOptions(Selector selector, List<string> options, int current)
    {
        if (selector == null) return;
        var list = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var o in options) list.Add(o);
        selector.SetOptions(list);
        if (current < 0 || current >= options.Count) return;
        selector.SetSelectedOption(options[current]);
        selector.SetNonLocalizedValueText(options[current]);
    }

    private static string Safe(Func<string> text, string fallback)
    {
        try { return text?.Invoke() ?? fallback; }
        catch { return fallback; }
    }

    // --- каждый кадр ---

    // При открытии вкладки перечитать значения (могли поменяться в cfg или в другом
    // экземпляре меню), клавиатура — поиску, пока в нём курсор, горячие клавиши,
    // список прокручивается за выбранной строкой
    public static void Tick()
    {
        Screens.RemoveAll(s => s.Panel == null);
        if (Screens.Count == 0) { KeyboardCapture.End(); return; }
        foreach (var s in Screens)
        {
            bool active = s.Panel.activeInHierarchy;
            if (active && !s.WasActive)
            {
                Refresh(s);
                s.Scroll.content.anchoredPosition = new Vector2(s.Scroll.content.anchoredPosition.x, 0f);
            }
            s.WasActive = active;
        }

        bool typing = IsTyping(out var open);
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        // Поле потеряло фокус не по воле игрока (не Esc, не Enter, не клик мимо) — возвращаем
        bool leftOnPurpose =
            (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
        if (_wasTyping && !typing && open != null && open.Search != null && !leftOnPurpose)
        {
            open.Search.Select();
            open.Search.ActivateInputField();
            open.Search.MoveTextEnd(false);
            typing = true;
        }
        _wasTyping = typing;
        if (typing) KeyboardCapture.Begin();
        else KeyboardCapture.End();
        if (open?.SearchLine != null) open.SearchLine.color = typing ? Gold : new Color(1f, 1f, 1f, 0.2f);

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        IntPtr ptr = selected != null ? selected.Pointer : IntPtr.Zero;

        if (open != null && !typing && keyboard != null)
        {
            // Ctrl+F — в поиск
            if (keyboard.fKey.wasPressedThisFrame && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && open.Search != null)
            {
                open.Search.Select();
                open.Search.ActivateInputField();
            }
            // Delete / Backspace на выбранной строке — к стандартному значению
            if ((keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                && open.RowByFocus.TryGetValue(ptr, out var focused))
                ResetRow(focused);
        }

        if (ptr == _lastSelected) return;
        _lastSelected = ptr;
        if (ptr == IntPtr.Zero) return;
        foreach (var s in Screens)
            if (s.RowOf.TryGetValue(ptr, out var row)) ScrollTo(s.Scroll, row);
    }

    private static bool IsTyping(out Screen open)
    {
        open = null;
        bool typing = false;
        foreach (var s in Screens)
        {
            if (s.Panel == null || !s.WasActive) continue;
            open = s;
            if (s.Search != null && s.Search.isFocused) typing = true;
        }
        return typing;
    }

    // Префикс UINavigator.Update: пока печатаешь в поиске, навигатор игры спит — иначе,
    // заметив клавиатуру, он выделяет первый элемент панели и поле теряет фокус
    public static bool NavigatorUpdatePrefix()
    {
        try { return !IsTyping(out _); }
        catch { return true; }
    }

    private static void ScrollTo(ScrollRect scroll, RectTransform row)
    {
        if (scroll == null || row == null) return;
        var content = scroll.content;
        float viewHeight = scroll.viewport.rect.height;
        float top = -content.InverseTransformPoint(row.position).y - row.rect.height * (1f - row.pivot.y);
        float bottom = top + row.rect.height;
        float offset = content.anchoredPosition.y;
        // запас сверху — чтобы был виден заголовок мода над первой строкой
        if (top - 150f < offset) offset = top - 150f;
        else if (bottom + 60f > offset + viewHeight) offset = bottom + 60f - viewHeight;
        offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, content.rect.height - viewHeight));
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
    }

    // --- навигация и ввод ---

    // Постфикс SettingsPanel.SetupSubPanels: у кнопок вкладок явная навигация,
    // встраиваем свою кнопку в цепочку; «вправо» с неё — в первую строку списка
    public static void LinkTabButtonsPostfix(SettingsPanel __instance)
    {
        try
        {
            var subs = __instance.subPanels;
            if (subs == null) return;
            var buttons = new List<Button>();
            Selectable first = null;
            for (int i = 0; i < subs.Count; i++)
            {
                var b = subs[i].Button != null ? subs[i].Button.GetComponent<Button>() : null;
                if (b != null) buttons.Add(b);
                if (subs[i].Panel != null && subs[i].Panel.name == PanelName)
                    foreach (var s in Screens)
                        if (s.Panel != null && s.Panel.Pointer == subs[i].Panel.gameObject.Pointer) first = s.First;
            }
            if (buttons.Count < 2) return;
            buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            var firstUp = buttons[0].navigation.selectOnUp;
            var lastDown = buttons[buttons.Count - 1].navigation.selectOnDown;
            for (int i = 0; i < buttons.Count; i++)
            {
                var nav = buttons[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? buttons[i - 1] : firstUp;
                nav.selectOnDown = i < buttons.Count - 1 ? buttons[i + 1] : lastDown;
                if (buttons[i].name == ButtonName) { nav.selectOnLeft = null; nav.selectOnRight = first; }
                buttons[i].navigation = nav;
            }
        }
        catch (Exception e) { ModMenuMod.Log.Error($"tab navigation: {e}"); }
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

    private static void NoNavigation(Selectable s)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.None;
        s.navigation = nav;
    }

    // Стрелки влево/вправо игра раздаёт через UIInputRouter только переключателям,
    // перечисленным в UIPanel.inputReceivers (список задан в редакторе игры).
    // Добавляем свои в тот же UIPanel, что и родные.
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
        if (owner == null) { ModMenuMod.Log.Warning("no UIPanel — arrow keys won't change values"); return; }

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

    // --- мелочи ---

    // Первая подпись строки (не внутри переключателя); остальные подписи шаблона прячем
    private static TMP_Text FirstLabel(GameObject row)
    {
        var selector = row.GetComponentInChildren<Selector>(true);
        TMP_Text first = null;
        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (selector != null && text.transform.IsChildOf(selector.transform)) continue;
            StripLocalization(text);
            if (first == null) first = text;
            else text.gameObject.SetActive(false);
        }
        return first;
    }

    private static void StripLocalization(TMP_Text text)
    {
        if (text == null) return;
        foreach (var loc in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
        foreach (var replacer in text.GetComponents<TextReplacer>()) Object.DestroyImmediate(replacer);
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

    // У клона кнопки остаются сериализованные обработчики исходной — выключаем их
    private static void MuteSerializedClicks(Button button)
    {
        if (button == null) return;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
}
