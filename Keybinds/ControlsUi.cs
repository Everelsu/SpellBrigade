using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KeybindsUnlocked;

// Панель «Управление» в настройках: вместо картинок с раскладкой — список действий
// с кнопками для клавиатуры/мыши и контроллера. Нажали на кнопку → нажали новую клавишу.
internal static class ControlsUi
{
    private const string RootName = "KeybindsUnlocked_Root";
    private const float CellWidth = 340f, CellHeight = 68f, RowHeight = 80f, ColumnGap = 24f, ScrollbarWidth = 6f;
    private const float LabelScale = 0.72f, SectionGap = 26f;
    private static readonly Color Gold = new(0.96f, 0.78f, 0.35f);

    private sealed class Row
    {
        public string LabelKey, LabelSuffix;
        public Slot Keyboard, Gamepad;
        public bool IsSection;
    }

    private sealed class Cell
    {
        public Slot Slot;
        public Button Button;
        public TMP_Text Text;
        public Color NormalColor;
        public bool Waiting;
    }

    private sealed class Screen
    {
        public GameObject Root;
        public ScrollRect Scroll;
        public List<GameObject> HiddenLayouts = new();
        public Dictionary<IntPtr, RectTransform> RowOfCell = new();
        public bool WasActive;
    }

    private static readonly List<Cell> Cells = new();
    private static readonly List<TMP_Text> Hints = new();
    private static readonly Color ConflictColor = new(1f, 0.6f, 0.2f);
    private static readonly List<Screen> Screens = new();
    private static IntPtr _lastSelected;
    private static bool _subscribed;

    // Постфикс SettingsPanel.SetupSubPanels: у каждого меню настроек (главное меню, пауза)
    // перестраиваем панель «Управление»
    public static void InjectPostfix(SettingsPanel __instance)
    {
        try { Inject(__instance); }
        catch (Exception e) { KeybindsMod.Log.Error($"can't build keybind list: {e}"); }
    }

    private static void Inject(SettingsPanel settings)
    {
        var subs = settings.subPanels;
        if (subs == null) return;
        Panel controls = null, general = null;
        for (int i = 0; i < subs.Count; i++)
        {
            var p = subs[i].Panel;
            if (p == null) continue;
            if (p.name.StartsWith("Controls")) controls = p;
            if (p.name.StartsWith("General")) general = p;
        }
        if (controls == null || general == null || controls.transform.Find(RootName) != null) return;

        var buttonTemplate = general.GetComponentInChildren<ClickableButton>(true);
        var title = controls.transform.Find("KeyboardLayout_Panel/Title_Text")?.GetComponent<TMP_Text>();
        var label = controls.transform.Find("KeyboardLayout_Panel/Prompts_Panel/Prompt_Panel (2)/Action_Text")?.GetComponent<TMP_Text>()
                    ?? controls.GetComponentInChildren<TMP_Text>(true);
        if (buttonTemplate == null || title == null || label == null) throw new InvalidOperationException("templates not found");

        Bindings.Tick(); // модель привязок нужна до постройки списка

        if (!_subscribed)
        {
            _subscribed = true;
            Bindings.Changed += RefreshAll;
            Strings.OnLanguageChanged(RefreshAll);
        }

        var screen = new Screen();
        for (int i = 0; i < controls.transform.childCount; i++)
        {
            var child = controls.transform.GetChild(i).gameObject;
            if (child.name.Contains("Layout_Panel")) { child.SetActive(false); screen.HiddenLayouts.Add(child); }
        }

        var root = NewRect(RootName, controls.transform);
        Stretch(root, new Vector2(100f, 30f), new Vector2(-40f, -50f));
        screen.Root = root.gameObject;

        // --- заголовки колонок ---
        var header = NewRect("Header", root);
        header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f); header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 70f); header.anchoredPosition = Vector2.zero;
        float listRight = -(ScrollbarWidth + 16f); // список уже на ширину полосы прокрутки
        ColumnTitle(title, header, Strings.Keyboard, listRight - (CellWidth + ColumnGap));
        ColumnTitle(title, header, Strings.Gamepad, listRight);

        // --- прокручиваемый список ---
        var viewport = NewRect("Viewport", root);
        Stretch(viewport, new Vector2(0f, 110f), new Vector2(-(ScrollbarWidth + 16f), -80f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var catcher = viewport.gameObject.AddComponent<Image>(); // чтобы колесо мыши работало и между строками
        catcher.color = new Color(0f, 0f, 0f, 0.001f);

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.spacing = 4f;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content; scroll.viewport = viewport;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;
        scroll.verticalScrollbar = BuildScrollbar(root);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        screen.Scroll = scroll;

        // подписи одного размера (у шаблона автоподбор: короткие выходили крупнее длинных)
        float labelSize = (label.enableAutoSizing ? label.fontSizeMax : label.fontSize) * LabelScale;
        var grid = new List<(Selectable kb, Selectable gp)>();
        int stripe = 0;
        bool firstSection = true;
        foreach (var row in Rows())
        {
            var rowRect = NewRect(row.IsSection ? "Section" : "Row", content);
            float height = row.IsSection ? RowHeight + (firstSection ? 0f : SectionGap) : RowHeight;
            rowRect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            var text = Object.Instantiate(label.gameObject, rowRect).GetComponent<TMP_Text>();
            StripLocalization(text);
            text.enableAutoSizing = false;
            text.fontSize = labelSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var tr = text.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0f, 0.5f);
            tr.offsetMin = new Vector2(16f, 0f); tr.offsetMax = new Vector2(-(2f * CellWidth + 2f * ColumnGap), 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            string key = row.LabelKey, suffix = row.LabelSuffix;
            Strings.Bind(text, () => Strings.Get(key) + suffix);

            if (row.IsSection)
            {
                // заголовок раздела: золотой, чуть крупнее, под ним тонкая линия на всю ширину
                text.fontStyle |= FontStyles.Bold;
                text.fontSize = labelSize * 1.05f;
                text.color = Gold;
                text.alignment = TextAlignmentOptions.BottomLeft;
                tr.offsetMin = new Vector2(0f, 14f);
                tr.offsetMax = Vector2.zero;
                var line = NewRect("Line", rowRect).gameObject.AddComponent<Image>();
                line.color = new Color(Gold.r, Gold.g, Gold.b, 0.25f);
                line.raycastTarget = false;
                var lr = line.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
                lr.sizeDelta = new Vector2(0f, 2f); lr.anchoredPosition = new Vector2(0f, 4f);
                stripe = 0;
                firstSection = false;
                continue;
            }

            // чередующийся фон строк — глазу легче связать подпись с её клавишами
            if (stripe++ % 2 == 1)
            {
                var band = rowRect.gameObject.AddComponent<Image>();
                band.color = new Color(1f, 1f, 1f, 0.035f);
                band.raycastTarget = false;
            }

            var kb = AddCell(buttonTemplate, label, rowRect, row.Keyboard, -(CellWidth + ColumnGap), screen);
            var gp = AddCell(buttonTemplate, label, rowRect, row.Gamepad, 0f, screen);
            if (kb != null) screen.RowOfCell[kb.gameObject.Pointer] = rowRect;
            if (gp != null) screen.RowOfCell[gp.gameObject.Pointer] = rowRect;
            if (kb != null || gp != null) grid.Add((kb, gp));
        }

        // --- низ: «Сбросить всё» и подсказка ---
        var footer = NewRect("Footer", root);
        footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, 100f); footer.anchoredPosition = Vector2.zero;

        var reset = NewButton(buttonTemplate, footer, "Reset");
        var resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = resetRect.anchorMax = new Vector2(0f, 0.5f); resetRect.pivot = new Vector2(0f, 0.5f);
        resetRect.sizeDelta = new Vector2(CellWidth, CellHeight); resetRect.anchoredPosition = Vector2.zero;
        Strings.Bind(reset.GetComponentInChildren<TMP_Text>(true), Strings.ResetAll);
        OnClick(reset, () => { if (!Bindings.IsRebinding) Bindings.ResetAll(); });
        var resetButton = reset.GetComponent<Button>();

        var hint = Object.Instantiate(label.gameObject, footer).GetComponent<TMP_Text>();
        StripLocalization(hint);
        var hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0f, 0.5f);
        hr.offsetMin = new Vector2(CellWidth + ColumnGap, 0f); hr.offsetMax = Vector2.zero;
        hint.enableAutoSizing = false;
        hint.fontSize = labelSize * 0.8f;
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        hint.textWrappingMode = TextWrappingModes.Normal;
        Hints.Add(hint);

        LinkNavigation(grid, resetButton);
        var navigateable = controls.TryCast<NavigateablePanel>();
        if (navigateable != null && grid.Count > 0)
            navigateable.firstSelected = (grid[0].kb ?? grid[0].gp).gameObject;

        Screens.Add(screen);
        RefreshAll();
    }

    private static IEnumerable<Row> Rows()
    {
        Row R(string key, string mapAction, bool kb = true, bool gp = true) => new Row
        {
            LabelKey = key,
            Keyboard = kb ? new Slot(mapAction, false) : null,
            Gamepad = gp ? new Slot(mapAction, true) : null,
        };
        Row Section(string key) => new Row { LabelKey = key, IsSection = true };

        yield return Section(Strings.InGame);
        foreach (var (key, part) in new[] { (Strings.MoveUp, "up"), (Strings.MoveDown, "down"), (Strings.MoveLeft, "left"), (Strings.MoveRight, "right") })
            yield return new Row { LabelKey = key, Keyboard = new Slot("Gameplay/Move", false, 0, part) };
        yield return new Row { LabelKey = Strings.MoveStick, Gamepad = new Slot("Gameplay/Move", true) { ControlType = "Vector2" } };
        yield return R(Strings.Pause, "Gameplay/OpenPauseMenu");
        yield return R(Strings.Stats, "Gameplay/ShowStats");
        yield return R(Strings.Tooltips, "Gameplay/ShowTooltips");
        yield return R(Strings.Emote, "Gameplay/ShowRadialMenu");
        yield return R(Strings.Ping, "Gameplay/SmartPing");
        // быстрый чат: клавиатура — composite 1 (цифры), контроллер — composite 0 (крестовина)
        foreach (var (n, part) in new[] { ("1", "up"), ("2", "left"), ("3", "right"), ("4", "down") })
            yield return new Row
            {
                LabelKey = Strings.QuickChat, LabelSuffix = " " + n,
                Keyboard = new Slot("Gameplay/QuickChat", false, 1, part),
                Gamepad = new Slot("Gameplay/QuickChat", true, 0, part),
            };
        yield return R(Strings.QuickChatClose, "Gameplay/QuickChatCancel");
        yield return R(Strings.ToggleSpells, Bindings.ModMap + "/" + Bindings.ToggleSpells);

        yield return Section(Strings.Menus);
        yield return Mirrored(R(Strings.NextTab, "UI/NextCategory"), "UI/NextStatCategory");
        yield return Mirrored(R(Strings.PrevTab, "UI/PreviousCategory"), "UI/PreviousStatCategory");
        yield return R(Strings.NextPage, "UI/NextSubCategory");
        yield return R(Strings.PrevPage, "UI/PreviousSubCategory");
        yield return R(Strings.Ready, "UI/Ready");
        yield return R(Strings.Invite, "UI/Invite");
        yield return R(Strings.Matchmaking, "UI/MatchmakingPreferences");
        yield return R(Strings.LobbyCode, "UI/LobbyCode");
        yield return R(Strings.SelectWizard, "UI/SelectCharacter");
        yield return R(Strings.Lore, "UI/ToggleLoreDisplay");
        yield return R(Strings.Prestige, "UI/Prestige");
        yield return R(Strings.Refund, "UI/SingleUpgradeRefund");
        yield return R(Strings.Options, "UI/Options");
        yield return R(Strings.Report, "UI/Report");
        yield return R(Strings.Continue, "UI/ContinueGameOver");
    }

    // Вкладки статистики переключаются теми же клавишами, что и обычные вкладки
    private static Row Mirrored(Row row, string mirror)
    {
        row.Keyboard?.Mirrors.Add(new Slot(mirror, false));
        row.Gamepad?.Mirrors.Add(new Slot(mirror, true));
        return row;
    }

    private static Selectable AddCell(ClickableButton template, TMP_Text label, RectTransform row, Slot slot, float x, Screen screen)
    {
        if (slot == null || !Bindings.CanRebind(slot))
        {
            var dash = Object.Instantiate(label.gameObject, row).GetComponent<TMP_Text>();
            StripLocalization(dash);
            dash.text = "—";
            dash.color = new Color(1f, 1f, 1f, 0.25f);
            dash.alignment = TextAlignmentOptions.Center;
            PlaceCell(dash.rectTransform, x);
            return null;
        }

        var go = NewButton(template, row, "Cell");
        PlaceCell(go.GetComponent<RectTransform>(), x);
        var cell = new Cell { Slot = slot, Button = go.GetComponent<Button>(), Text = go.GetComponentInChildren<TMP_Text>(true) };
        cell.NormalColor = cell.Text.color;
        cell.Text.enableAutoSizing = true;
        cell.Text.fontSizeMax = cell.Text.fontSize;
        cell.Text.fontSizeMin = cell.Text.fontSize * 0.5f;
        OnClick(go, () => MelonCoroutines.Start(StartRebind(cell)));
        Cells.Add(cell);
        return cell.Button;
    }

    private static IEnumerator StartRebind(Cell cell)
    {
        if (Bindings.IsRebinding || cell.Button == null) yield break;
        cell.Waiting = true;
        cell.Text.text = Strings.Get(Strings.PressKey);
        // даём отпустить кнопку/клавишу, которой нажали на ячейку
        float until = Time.unscaledTime + 0.2f;
        while (Time.unscaledTime < until) yield return null;
        Bindings.Rebind(cell.Slot, _ =>
        {
            cell.Waiting = false;
            RefreshAll();
        });
    }

    private static void RefreshAll()
    {
        Cells.RemoveAll(c => c.Button == null || c.Text == null);

        // Конфликт: одна клавиша у нескольких действий одного раздела и устройства,
        // и хотя бы одна из них переназначена игроком (штатные совпадения игры
        // срабатывают в разных ситуациях — их не подсвечиваем)
        var groups = new Dictionary<string, List<(Cell cell, bool overridden)>>();
        foreach (var cell in Cells)
        {
            var (path, overridden) = Bindings.State(cell.Slot);
            if (string.IsNullOrEmpty(path)) continue;
            string key = $"{cell.Slot.Map}|{cell.Slot.Gamepad}|{path.ToLowerInvariant()}";
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(Cell, bool)>();
            list.Add((cell, overridden));
        }
        var conflicted = new HashSet<Cell>();
        foreach (var list in groups.Values)
            if (list.Count > 1 && list.Exists(e => e.overridden))
                foreach (var e in list) conflicted.Add(e.cell);

        foreach (var cell in Cells)
        {
            if (!cell.Waiting) cell.Text.text = Bindings.Display(cell.Slot);
            cell.Text.color = conflicted.Contains(cell) ? ConflictColor : cell.NormalColor;
        }

        Hints.RemoveAll(h => h == null);
        string hint = Strings.Get(Strings.Hint);
        if (conflicted.Count > 0) hint += "\n<color=#FF9A33>" + Strings.Get(Strings.Conflict) + "</color>";
        foreach (var h in Hints) h.text = hint;
    }

    // Каждый кадр: прячем исходные картинки раскладки (их может включить сама игра)
    // и прокручиваем список к выбранной с клавиатуры/геймпада кнопке
    public static void Tick()
    {
        Screens.RemoveAll(s => s.Root == null);
        foreach (var s in Screens)
        {
            bool active = s.Root.activeInHierarchy;
            if (active && !s.WasActive) RefreshAll();
            s.WasActive = active;
            if (!active) continue;
            foreach (var layout in s.HiddenLayouts)
                if (layout != null && layout.activeSelf) layout.SetActive(false);
        }

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        IntPtr ptr = selected != null ? selected.Pointer : IntPtr.Zero;
        if (ptr == _lastSelected) return;
        _lastSelected = ptr;
        if (ptr == IntPtr.Zero) return;
        foreach (var s in Screens)
            if (s.RowOfCell.TryGetValue(ptr, out var row)) ScrollTo(s.Scroll, row);
    }

    private static void ScrollTo(ScrollRect scroll, RectTransform row)
    {
        if (scroll == null || row == null) return;
        var content = scroll.content;
        float viewHeight = scroll.viewport.rect.height;
        float top = -content.InverseTransformPoint(row.position).y - row.rect.height * (1f - row.pivot.y);
        float bottom = top + row.rect.height;
        float offset = content.anchoredPosition.y;
        if (top < offset) offset = top - 10f;
        else if (bottom > offset + viewHeight) offset = bottom - viewHeight + 10f;
        offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, content.rect.height - viewHeight));
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
    }

    // Сетка: вверх/вниз по колонке (пропуская «—»), влево/вправо между колонками
    private static void LinkNavigation(List<(Selectable kb, Selectable gp)> grid, Selectable reset)
    {
        Selectable Find(int from, int step, bool gamepadColumn)
        {
            for (int i = from; i >= 0 && i < grid.Count; i += step)
            {
                var s = gamepadColumn ? grid[i].gp : grid[i].kb;
                if (s != null) return s;
                var other = gamepadColumn ? grid[i].kb : grid[i].gp;
                if (other != null) return other;
            }
            return null;
        }

        for (int i = 0; i < grid.Count; i++)
        {
            var (kb, gp) = grid[i];
            if (kb != null) SetNav(kb, Find(i - 1, -1, false), Find(i + 1, 1, false) ?? reset, null, gp);
            if (gp != null) SetNav(gp, Find(i - 1, -1, true), Find(i + 1, 1, true) ?? reset, kb, null);
        }
        if (reset != null && grid.Count > 0)
            SetNav(reset, grid[^1].kb ?? grid[^1].gp, null, null, null);
    }

    private static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up; nav.selectOnDown = down; nav.selectOnLeft = left; nav.selectOnRight = right;
        s.navigation = nav;
    }

    // --- строительные мелочи ---

    private static GameObject NewButton(ClickableButton template, Transform parent, string name)
    {
        var go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        // у клона остаются сериализованные обработчики исходной кнопки («Сбросить сохранение»!) — выключаем
        var button = go.GetComponent<Button>();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) StripLocalization(t);
        // шаблон — «опасная» красная кнопка «Сбросить сохранение»: её белые картинки
        // подкрашены в красный через Image.color, возвращаем обычный белый
        foreach (var image in go.GetComponentsInChildren<Image>(true))
        {
            var c = image.color;
            image.color = new Color(1f, 1f, 1f, c.a);
        }
        return go;
    }

    private static void OnClick(GameObject button, Action action)
    {
        var hook = button.GetComponent<ClickableButton>();
        hook.add_OnClicked((Il2CppSystem.Action)(() =>
        {
            try { action(); }
            catch (Exception e) { KeybindsMod.Log.Error(e.ToString()); }
        }));
    }

    // Тонкая полоса прокрутки справа от списка
    private static Scrollbar BuildScrollbar(RectTransform root)
    {
        var bar = NewRect("Scrollbar", root);
        bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(1f, 0.5f);
        bar.offsetMin = new Vector2(-ScrollbarWidth, 110f); bar.offsetMax = new Vector2(0f, -80f);
        var track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(1f, 1f, 1f, 0.05f);

        var area = NewRect("Sliding Area", bar);
        Stretch(area, Vector2.zero, Vector2.zero);
        var handle = NewRect("Handle", area);
        Stretch(handle, Vector2.zero, Vector2.zero);
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
        var nav = scrollbar.navigation;
        nav.mode = Navigation.Mode.None; // не перехватывать фокус у списка при управлении с клавиатуры/геймпада
        scrollbar.navigation = nav;
        return scrollbar;
    }

    private static void PlaceCell(RectTransform rect, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(CellWidth, CellHeight);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    private static void ColumnTitle(TMP_Text template, RectTransform header, string key, float x)
    {
        var t = Object.Instantiate(template.gameObject, header).GetComponent<TMP_Text>();
        StripLocalization(t);
        t.fontSize *= 0.6f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = t.fontSize * 0.5f;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(CellWidth, 70f);
        r.anchoredPosition = new Vector2(x, 0f);
        Strings.Bind(t, key);
    }

    private static void StripLocalization(TMP_Text text)
    {
        foreach (var loc in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
