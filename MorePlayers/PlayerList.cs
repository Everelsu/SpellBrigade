using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MorePlayers;

// Справа вверху лобби, под «Настройками подбора игроков»: «[Tab] Игроки 6/8 · Комната 1/2».
// По клику или Tab раскрывается полный список всех игроков по комнатам; у хоста там же
// выбор максимума игроков.
internal static class PlayerList
{
    private const float Right = 100f, Top = 262f, Width = 820f, RowHeight = 78f;

    private static GameObject _root;
    private static CanvasGroup _group, _lobbyGroup, _runSetupGroup;
    private static TMP_Text _template, _header;
    private static RectTransform _panel;
    private static GameObject _maxRow;
    private static TMP_Text _maxValue;
    private static readonly List<Row> Rows = new();
    private static bool _open;
    private static float _nextRefresh, _nextBuildTry;
    private static Sprite _ready, _notReady;
    private static Color _nameColor = Color.white, _hostColor = new(1f, 0.85f, 0.3f);

    private sealed class Row
    {
        public GameObject Go;
        public Image Dot, Portrait, Ready;
        public TMP_Text Text;
    }

    public static void Tick()
    {
        if (_root == null)
        {
            if (Time.unscaledTime < _nextBuildTry) return;
            _nextBuildTry = Time.unscaledTime + 1f;
            if (!TryBuild()) return;
        }

        float alpha = (_lobbyGroup != null ? _lobbyGroup.alpha : 1f) * (_runSetupGroup != null ? _runSetupGroup.alpha : 1f);
        var manager = NetworkSingleton<MainMenuManager>.Instance;
        if (manager == null || manager.PlayerData == null) alpha = 0f;
        _group.alpha = alpha;
        bool visible = alpha > 0.5f;
        _group.blocksRaycasts = _group.interactable = visible;
        if (!visible) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) Toggle();

        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.25f;
        Refresh(manager.PlayerData);
    }

    private static void Toggle()
    {
        _open = !_open;
        _nextRefresh = 0f;
    }

    private static void Refresh(Il2CppSystem.Collections.Generic.List<MainMenuPlayerData> players)
    {
        int n = players.Count, rooms = Rooms.Count(n), local = Rooms.LocalIndex(players);
        bool host = IsHost();
        _playerCount = n;
        if (host && n > MorePlayersMod.MaxPlayers) MorePlayersMod.SetMaxPlayers(n); // лимит не может быть меньше тех, кто уже здесь

        string text = "<color=#FFFFFF80>[Tab]</color>  " + (host ? $"{Strings.Get(Strings.Players)} {n}/{MorePlayersMod.MaxPlayers}" : $"{Strings.Get(Strings.Players)} {n}");
        if (rooms > 1) text += $"  ·  {Strings.Get(Strings.Room)} {Rooms.Of(local, n) + 1}/{rooms}";
        _header.text = text;

        _panel.gameObject.SetActive(_open);
        if (!_open) return;

        _maxRow.SetActive(host);
        _maxValue.text = MorePlayersMod.MaxPlayers.ToString();
        _less.interactable = MorePlayersMod.MaxPlayers > Math.Max(MorePlayersMod.MinPlayers, n);
        _more.interactable = MorePlayersMod.MaxPlayers < MorePlayersMod.HardCap;

        int used = 0;
        for (int room = 0; room < rooms; room++)
        {
            if (rooms > 1)
            {
                var title = GetRow(used++);
                string you = room == Rooms.Of(local, n) ? $"  ·  {Strings.Get(Strings.You)}" : "";
                title.Text.text = $"{Strings.Get(Strings.Room)} {room + 1}{you}";
                title.Text.color = new Color(1f, 1f, 1f, room == Rooms.Of(local, n) ? 0.85f : 0.45f);
                title.Text.fontStyle = FontStyles.Bold;
                title.Dot.enabled = title.Ready.enabled = title.Portrait.enabled = false;
            }
            for (int i = 0; i < n; i++)
            {
                if (Rooms.Of(i, n) != room) continue;
                var p = players[i];
                var row = GetRow(used++);
                string name = p.DisplayName.Value;
                if (string.IsNullOrEmpty(name)) name = "—";
                row.Text.text = name;
                row.Text.fontStyle = i == local ? FontStyles.Bold : FontStyles.Normal;
                row.Text.color = p.IsLeader ? _hostColor : _nameColor;
                row.Dot.enabled = true;
                row.Dot.color = p.Color;
                var portrait = Portrait(p.SkinId);
                row.Portrait.enabled = portrait != null;
                row.Portrait.sprite = portrait;
                row.Ready.enabled = (p.IsReady ? _ready : _notReady) != null;
                row.Ready.sprite = p.IsReady ? _ready : _notReady;
            }
        }
        for (int i = 0; i < Rows.Count; i++) Rows[i].Go.SetActive(i < used);
    }

    private static bool IsHost()
    {
        var net = Unity.Netcode.NetworkManager.Singleton;
        return net == null || !net.IsClient || net.IsHost || net.IsServer;
    }

    // --- построение ---

    private static bool TryBuild()
    {
        Transform lobby = null;
        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            if (canvas != null && canvas.name == "MainMenu_Canvas") { lobby = canvas.transform.Find("Lobby_Panel"); break; }
        if (lobby == null) return false;

        _template = lobby.Find("MultiplayerContextDisplayManager/MatchmakingPreferencesDisplay/MatchmakingPreferencesPrompt/Text")?.GetComponent<TMP_Text>()
                    ?? lobby.GetComponentInChildren<TMP_Text>(true);
        if (_template == null) return false;
        Strings.UseFont(_template);

        _lobbyGroup = lobby.GetComponent<CanvasGroup>();
        _runSetupGroup = lobby.Find("MainMenu_Panel/RunSetup_Panel")?.GetComponent<CanvasGroup>();
        FindSeatStyle();

        var root = NewRect("MorePlayers_List", lobby);
        _root = root.gameObject;
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
        root.anchoredPosition = new Vector2(-Right, -Top);
        root.sizeDelta = new Vector2(Width, 0f);
        _group = _root.AddComponent<CanvasGroup>();

        BuildHeader(root);
        BuildPanel(root);
        Rows.Clear();
        _open = false;
        _nextRefresh = 0f;
        return true;
    }

    private static void BuildHeader(RectTransform root)
    {
        var header = NewRect("Header", root);
        header.anchorMin = new Vector2(0f, 1f); header.anchorMax = Vector2.one; header.pivot = Vector2.one;
        header.sizeDelta = new Vector2(0f, RowHeight);
        header.anchoredPosition = Vector2.zero;
        _header = NewText("Text", header, TextAlignmentOptions.Right);
        _header.overflowMode = TextOverflowModes.Overflow;

        var button = header.gameObject.AddComponent<Button>();
        header.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // чтобы кликалось по всей строке
        button.transition = Selectable.Transition.None;
        NoNavigation(button);
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(Action)Toggle);
    }

    private static void BuildPanel(RectTransform root)
    {
        _panel = NewRect("Panel", root);
        _panel.anchorMin = new Vector2(0f, 1f); _panel.anchorMax = Vector2.one; _panel.pivot = Vector2.one;
        _panel.anchoredPosition = new Vector2(0f, -RowHeight - 14f);
        _panel.sizeDelta = new Vector2(0f, 0f);
        _panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.09f, 0.78f);
        var layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset { left = 28, right = 28, top = 18, bottom = 18 };
        layout.spacing = 2f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // «Макс. игроков  <  8  >» — только у хоста
        var row = NewRect("Max", _panel);
        _maxRow = row.gameObject;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight + 8f;
        var label = NewText("Label", row, TextAlignmentOptions.Left);
        Stretch(label.rectTransform, new Vector2(0f, 0f), new Vector2(-260f, 0f));
        label.text = Strings.Get(Strings.MaxPlayers);
        _maxLabel = label;

        // меньше, чем уже сидит в лобби, поставить нельзя
        _less = ArrowButton(row, "<", -200f, () =>
        {
            if (MorePlayersMod.MaxPlayers - 1 >= Math.Max(MorePlayersMod.MinPlayers, _playerCount))
                MorePlayersMod.SetMaxPlayers(MorePlayersMod.MaxPlayers - 1);
        });
        _maxValue = NewText("Value", row, TextAlignmentOptions.Center);
        Place(_maxValue.rectTransform, -100f, 110f);
        _maxValue.fontStyle = FontStyles.Bold;
        _more = ArrowButton(row, ">", 0f, () => MorePlayersMod.SetMaxPlayers(MorePlayersMod.MaxPlayers + 1));

        // колесо мыши над строкой — как стрелки
        SpellBrigade.Shared.WheelSelect.Register(row.gameObject, step =>
        {
            int value = MorePlayersMod.MaxPlayers + step;
            if (value < Math.Max(MorePlayersMod.MinPlayers, _playerCount)) return;
            MorePlayersMod.SetMaxPlayers(value);
            _nextRefresh = 0f;
        });
    }

    private static TMP_Text _maxLabel;

    private static Button _less, _more;
    private static int _playerCount;

    private static Button ArrowButton(RectTransform parent, string glyph, float x, Action onClick)
    {
        var text = NewText(glyph == "<" ? "Less" : "More", parent, TextAlignmentOptions.Center);
        text.text = glyph;
        text.fontStyle = FontStyles.Bold;
        Place(text.rectTransform, x, 90f);
        var button = text.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        NoNavigation(button);
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.4f);
        colors.pressedColor = new Color(0.8f, 0.65f, 0.3f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
        button.colors = colors;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(Action)(() =>
        {
            try { onClick(); _nextRefresh = 0f; }
            catch (Exception e) { MorePlayersMod.Log.Warning(e.Message); }
        }));
        return button;
    }

    private static Row GetRow(int index)
    {
        while (Rows.Count <= index)
        {
            var rect = NewRect($"Row{Rows.Count}", _panel);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;

            var dot = NewRect("Color", rect).gameObject.AddComponent<Image>();
            var d = dot.rectTransform;
            d.anchorMin = d.anchorMax = d.pivot = new Vector2(0f, 0.5f);
            d.sizeDelta = new Vector2(22f, 22f);
            d.anchoredPosition = new Vector2(4f, 0f);

            // портрет выбранного мага в его облике
            var face = NewRect("Portrait", rect).gameObject.AddComponent<Image>();
            face.preserveAspect = true;
            face.raycastTarget = false;
            var f = face.rectTransform;
            f.anchorMin = f.anchorMax = f.pivot = new Vector2(0f, 0.5f);
            f.sizeDelta = new Vector2(RowHeight - 6f, RowHeight - 6f);
            f.anchoredPosition = new Vector2(38f, 0f);

            var text = NewText("Name", rect, TextAlignmentOptions.Left);
            Stretch(text.rectTransform, new Vector2(38f + RowHeight + 8f, 0f), new Vector2(-80f, 0f));

            var ready = NewRect("Ready", rect).gameObject.AddComponent<Image>();
            ready.preserveAspect = true;
            var r = ready.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0.5f);
            r.sizeDelta = new Vector2(48f, 48f);
            r.anchoredPosition = Vector2.zero;

            Rows.Add(new Row { Go = rect.gameObject, Dot = dot, Portrait = face, Ready = ready, Text = text });
        }
        // строку «Макс. игроков» держим первой
        _maxLabel.text = Strings.Get(Strings.MaxPlayers);
        return Rows[index];
    }

    private static readonly Dictionary<SkinId, Sprite> Portraits = new();

    private static Sprite Portrait(SkinId skin)
    {
        if (Portraits.TryGetValue(skin, out var sprite) && sprite != null) return sprite;
        try
        {
            var resource = CharacterSkinResourceRepository.Get(skin);
            sprite = resource != null ? (resource.GameplayPortrait ?? resource.PortraitImage) : null;
        }
        catch { sprite = null; }
        Portraits[skin] = sprite;
        return sprite;
    }

    // Иконки готовности и цвета имён — как на местах в лобби
    private static void FindSeatStyle()
    {
        foreach (var panel in Resources.FindObjectsOfTypeAll<PlayerSeatPlayerDetailsPanel>())
        {
            if (panel == null || panel.gameObject.scene.name == null) continue;
            _nameColor = panel.defaultTextColor;
            _hostColor = panel.hostTextColor;
            _ready ??= panel.readyIcon != null ? panel.readyIcon.GetComponent<Image>()?.sprite : null;
            _notReady ??= panel.notReadyIcon != null ? panel.notReadyIcon.GetComponent<Image>()?.sprite : null;
            if (_ready != null && _notReady != null) break;
        }
    }

    // --- мелочи ---

    private static TMP_Text NewText(string name, Transform parent, TextAlignmentOptions alignment)
    {
        var go = Object.Instantiate(_template.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        foreach (var loc in go.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
        foreach (var fitter in go.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        foreach (var c in go.GetComponents<MonoBehaviour>())
            if (c != null && c.TryCast<TMP_Text>() == null) Object.DestroyImmediate(c); // TextReplacer и прочее
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        var text = go.GetComponent<TMP_Text>();
        text.text = "";
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = true;
        var r = text.rectTransform;
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        return text;
    }

    private static void NoNavigation(Selectable s)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.None; // не мешать навигации лобби с клавиатуры/геймпада
        s.navigation = nav;
    }

    private static void Place(RectTransform rect, float x, float width)
    {
        rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);
        rect.anchoredPosition = new Vector2(x, 0f);
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
