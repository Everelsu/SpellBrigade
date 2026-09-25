using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinnedChallenges;

// Положение и размер карточек мышью: поверх всего экрана — затемнение и предпросмотр карточек
// (закреплённые испытания или пример). Тащишь карточки — двигаются, колесо над ними или
// золотой уголок — размер. Enter / правая кнопка / Esc / «Готово» — сохранить,
// R / «По умолчанию» — вернуть место и размер.
// Положение хранится долями экрана, поэтому одинаково при любом разрешении.
internal static class LayoutEditor
{
    private const string ModalKey = "SpellBrigade.Modal"; // пока открыт — колесо не листает переключатели под ним
    private static readonly Color Dim = new(0f, 0f, 0f, 0.6f), ButtonColor = new(0.1f, 0.08f, 0.16f, 0.95f);

    private static GameObject _overlay;
    private static RectTransform _canvas, _block;
    private static Canvas _canvasComponent;
    private static Vector2 _fraction; // левый верхний угол карточек, доли экрана (сохраняется при закрытии)
    private static Image _frame, _handle;
    private static TMP_Text _sizeLabel;
    private const float MinScale = 0.5f, MaxScale = 2f, HandleSize = 26f;
    private static readonly List<RunHud.Card> Cards = new();
    private static ChallengeManager _manager;
    private static GameObject _previousSelection;
    private static bool _dragging, _resizing;
    private static Vector2 _grab;
    private static float _nextUpdate;

    public static void Open()
    {
        if (_overlay != null) return;
        _manager = SingletonPersistent<LocalPlayer>.Instance?.GetChallengeManager();
        var template = RunHud.FindAnyTemplate();
        if (_manager == null || template == null)
        {
            PinnedChallengesMod.Log.Warning("can't open the position editor yet: challenges aren't loaded");
            return;
        }

        _overlay = new GameObject("PinnedChallenges_Layout");
        Object.DontDestroyOnLoad(_overlay);
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = _overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _overlay.AddComponent<GraphicRaycaster>();
        _canvas = _overlay.GetComponent<RectTransform>();
        _canvasComponent = canvas;
        _fraction = RunHud.SavedPosition;

        var dim = Ui.NewImage("Dim", _canvas, Dim);
        dim.raycastTarget = true; // клики не проходят в меню под редактором
        Stretch(dim.rectTransform);

        _block = RunHud.BuildBlock(_canvas, template, _manager, PreviewIds(), Cards);
        _frame = Ui.NewImage("Frame", _block, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.18f));
        var fr = _frame.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(-12f, -12f); fr.offsetMax = new Vector2(12f, 12f);
        fr.SetAsFirstSibling();

        // уголок для размера — справа внизу
        _handle = Ui.NewImage("Resize", _block, Ui.Gold);
        var hr = _handle.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(HandleSize, HandleSize);
        hr.anchoredPosition = new Vector2(6f, -6f);

        var font = Cards.Count > 0 ? Cards[0].Description : null;
        if (font != null)
        {
            var hint = Text(font, "Hint", Strings.DragHint, 34f, Color.white, FontStyles.Bold);
            PlaceTop(hint.rectTransform, -70f, 60f);
            var size = Text(font, "Size", Strings.SizeHint, 22f, new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal);
            PlaceTop(size.rectTransform, -125f, 36f);
            var keys = Text(font, "Keys", Strings.KeysHint, 22f, new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal);
            PlaceTop(keys.rectTransform, -160f, 36f);
            _sizeLabel = Ui.CloneText(font, _canvas, "Scale");
            _sizeLabel.fontSize = 20f;
            _sizeLabel.fontStyle = FontStyles.Bold;
            _sizeLabel.color = Ui.Gold;
            _sizeLabel.alignment = TextAlignmentOptions.BottomLeft;
            AddButton(font, Strings.Default, -160f, ResetPosition);
            AddButton(font, Strings.Done, 160f, Close);
        }

        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        EventSystem.current?.SetSelectedGameObject(null); // стрелки и Enter не должны трогать меню под нами
        AppDomain.CurrentDomain.SetData(ModalKey, true);
        _dragging = _resizing = false;
    }

    // Фон/размер поменяли в меню, пока редактор открыт
    public static void Restyle()
    {
        if (_overlay == null) return;
        RunHud.Restyle(Cards);
        SetScale(PinnedChallengesMod.HudScaleEntry.Value);
    }

    public static void Tick()
    {
        if (_overlay == null) return;
        if (_block == null) { Close(); return; }

        // как в забеге — те же пиксели экрана
        RunHud.Place(_block, _canvasComponent, _fraction);

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.rKey.wasPressedThisFrame) ResetPosition();
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 pointer = mouse.position.ReadValue();
            bool overHandle = RectTransformUtility.RectangleContainsScreenPoint(_handle.rectTransform, pointer, null);
            bool over = overHandle || RectTransformUtility.RectangleContainsScreenPoint(_block, pointer, null);
            if (mouse.rightButton.wasPressedThisFrame) { Close(); return; }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (overHandle) _resizing = true;
                else if (over)
                {
                    _dragging = true;
                    _grab = RunHud.TopLeft(_block, _fraction) - pointer;
                }
            }
            if (!mouse.leftButton.isPressed) _dragging = _resizing = false;
            if (_dragging) MoveTo(pointer + _grab);
            if (_resizing) ResizeTo(pointer);

            float wheel = mouse.scroll.ReadValue().y;
            if (over && Mathf.Abs(wheel) > 0.01f)
                SetScale(PinnedChallengesMod.HudScaleEntry.Value + (wheel > 0f ? 0.05f : -0.05f));

            bool active = _dragging || _resizing;
            _frame.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, active ? 0.45f : over ? 0.3f : 0.18f);
            _handle.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, _resizing || overHandle ? 1f : 0.7f);
        }

        if (_sizeLabel != null)
        {
            // «100%» над правым верхним углом карточек
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            _block.GetWorldCorners(corners);
            _sizeLabel.rectTransform.position = corners[1] + new Vector3(0f, 8f, 0f);
            _sizeLabel.rectTransform.sizeDelta = new Vector2(200f, 30f);
            _sizeLabel.rectTransform.pivot = Vector2.zero;
            _sizeLabel.text = $"{PinnedChallengesMod.HudScaleEntry.Value * 100f:0}%";
        }

        // описания приходят из локализации не сразу — подтягиваем
        if (Time.unscaledTime >= _nextUpdate)
        {
            _nextUpdate = Time.unscaledTime + 0.25f;
            foreach (var card in Cards) RunHud.Update(card, _manager);
        }
    }

    // Левый верхний угол карточек — в точку экрана (пиксели), не вылезая за края
    private static void MoveTo(Vector2 screenPoint)
    {
        _fraction = new Vector2(screenPoint.x / Screen.width, screenPoint.y / Screen.height);
        Vector2 clamped = RunHud.TopLeft(_block, _fraction);
        _fraction = new Vector2(clamped.x / Screen.width, clamped.y / Screen.height);
        RunHud.Place(_block, _canvasComponent, _fraction);
    }

    // Ширина по курсору — левый верхний угол карточек остаётся на месте
    private static void ResizeTo(Vector2 pointer)
    {
        float width = pointer.x - RunHud.TopLeft(_block, _fraction).x;
        SetScale(width / (RunHud.Width * Screen.height / 1080f));
    }

    private static void SetScale(float scale)
    {
        scale = Mathf.Clamp(Mathf.Round(scale * 100f) / 100f, MinScale, MaxScale);
        PinnedChallengesMod.HudScaleEntry.Value = scale;
        if (_block != null) MoveTo(RunHud.TopLeft(_block, _fraction)); // не вылезти за край после увеличения
    }

    private static void ResetPosition()
    {
        _fraction = new Vector2(PinnedChallengesMod.DefaultX, PinnedChallengesMod.DefaultY);
        SetScale(PinnedChallengesMod.HudScaleEntry.DefaultValue);
    }

    private static void Close()
    {
        if (_overlay == null) return;
        PinnedChallengesMod.HudXEntry.Value = Mathf.Clamp01(_fraction.x);
        PinnedChallengesMod.HudYEntry.Value = Mathf.Clamp01(_fraction.y);
        PinnedChallengesMod.Save();
        Object.Destroy(_overlay);
        _overlay = null;
        Cards.Clear();
        AppDomain.CurrentDomain.SetData(ModalKey, false);
        if (_previousSelection != null) EventSystem.current?.SetSelectedGameObject(_previousSelection);
        RunHud.Rescale(); // в забеге (редактор открыт из паузы) карточки встают на новое место сразу
        MenuPage.Refresh(); // в Mod Menu — новый размер, а не тот, что был до редактора
        PinnedChallengesMod.Log.Msg($"cards position: {PinnedChallengesMod.HudXEntry.Value:0.###}, {PinnedChallengesMod.HudYEntry.Value:0.###}");
    }

    // Закреплённые, а если их нет — первое невыполненное испытание как пример
    private static List<ChallengeId> PreviewIds()
    {
        var ids = new List<ChallengeId>(Pins.All);
        if (ids.Count > 0) return ids;
        var configs = _manager.ConfigurationForChallenges;
        foreach (ChallengeId id in Enum.GetValues(typeof(ChallengeId)))
        {
            if (configs == null || !configs.ContainsKey(id)) continue;
            var config = configs[id];
            if (config == null || config.Disabled || config.IsComingSoon || _manager.IsCompleted(id)) continue;
            ids.Add(id);
            break;
        }
        return ids;
    }

    private static TMP_Text Text(TMP_Text font, string name, string key, float fontSize, Color color, FontStyles style)
    {
        var text = Ui.CloneText(font, _canvas, name);
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        Strings.Bind(text, key);
        return text;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1600f, height);
    }

    private static void AddButton(TMP_Text font, string key, float x, Action onClick)
    {
        var rect = Ui.NewRect(key, _canvas);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 70f);
        rect.sizeDelta = new Vector2(280f, 72f);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.45f);
        colors.pressedColor = new Color(0.8f, 0.65f, 0.3f);
        button.colors = colors;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(Action)(() =>
        {
            try { onClick(); }
            catch (Exception e) { PinnedChallengesMod.Log.Warning($"position editor: {e.Message}"); }
        }));

        var label = Ui.CloneText(font, rect, "Label");
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);
        Strings.Bind(label, key);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
