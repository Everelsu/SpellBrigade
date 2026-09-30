using System;
using Il2Cpp;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HoldToDie;

// Держишь клавишу — вокруг волшебника заполняется кольцо, заполнилось — смерть.
// Убиваем тем же вызовом, которым игра снимает здоровье за переброс улучшений
// (HealthReroller.UseReroll): RemoveHealthWithoutModifiers не смотрит на неуязвимость
// и уклонение, а смерть дальше идёт обычным путём игры — призрак, круг воскрешения,
// жетоны воскрешения, конец забега.
internal static class Death
{
    private const float RingSize = 170f;
    private static readonly Color Back = new(0f, 0f, 0f, 0.45f), Fill = new(0.93f, 0.27f, 0.22f, 0.95f);

    private static float _held;
    private static bool _waitRelease; // после смерти ждём, пока отпустят клавишу
    private static bool _noRevives;   // хост держит жетоны воскрешения на нуле до конца забега

    private static string _keyName;
    private static Key _key;

    private static GameObject _overlay;
    private static RectTransform _canvas, _ring;
    private static Image _fill;

    public static void Tick()
    {
        var manager = Singleton<GameplayPlayerManager>.Instance;
        if (manager == null) _noRevives = false; // забег закончился
        else if (_noRevives && manager.AlivePlayers.Count == 0) ZeroRevives();

        bool down = Pressed();
        if (!down) _waitRelease = false;
        var player = down && !_waitRelease ? LocalPlayer(manager) : null;
        if (player == null)
        {
            Reset();
            return;
        }

        _held += Time.unscaledDeltaTime;
        float progress = _held / HoldToDieMod.HoldTimeEntry.Value;
        if (progress < 1f)
        {
            Show(player, progress);
            return;
        }

        Reset();
        _waitRelease = true;
        Kill(manager, player);
    }

    public static void Reset()
    {
        _held = 0f;
        if (_overlay != null) _overlay.SetActive(false);
    }

    private static bool Pressed()
    {
        if (!HoldToDieMod.EnabledEntry.Value) return false;
        if (AppDomain.CurrentDomain.GetData("SpellBrigade.Typing") is true) return false; // печатают в поиске Mod Menu

        // С Keybinds Unlocked клавиша и кнопка контроллера настраиваются там, в «Управлении»
        if (AppDomain.CurrentDomain.GetData("SpellBrigade.Input.HoldToDie") is InputAction action) return action.IsPressed();

        string name = HoldToDieMod.KeyEntry.Value;
        if (name != _keyName)
        {
            _keyName = name;
            if (!Enum.TryParse(name, true, out _key) || _key == Key.None)
            {
                _key = Key.None;
                HoldToDieMod.Log.Warning($"unknown key \"{name}\" — use names like K, Backspace, Delete, End, F9");
            }
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && _key != Key.None && keyboard[_key].isPressed) return true;

        var pad = Gamepad.current;
        if (pad == null) return false;
        return HoldToDieMod.GamepadEntry.Value switch
        {
            "L3" => pad.leftStickButton.isPressed,
            "R3" => pad.rightStickButton.isPressed,
            "Select" => pad.selectButton.isPressed,
            _ => false,
        };
    }

    // Кнопки контроллера на выбор (без Keybinds Unlocked). Игра не занимает L3; R3 и Select
    // у неё свободны в забеге, но R3 у Keybinds Unlocked — «показать / скрыть заклинания».
    public static readonly string[] GamepadButtons = { "L3", "R3", "Select", "None" };

    public static string GamepadName(string value) => value == "None" ? null : value;

    // Иконка игры для кнопки (в стиле Xbox, как в родной колонке «Контроллер»)
    public static string GamepadSprite(string value) => value switch
    {
        "L3" => "key_xbox_L",
        "R3" => "key_xbox_R",
        "Select" => "key_xbox_select",
        _ => null,
    };

    private static bool? _keybinds;
    public static bool KeybindsInstalled => _keybinds ??= MelonLoader.MelonBase.FindMelon("Keybinds Unlocked", "Relsev") != null;

    // Свой живой волшебник, если сейчас можно умереть: идёт забег, нет паузы и окна улучшений
    private static GameplayPlayer LocalPlayer(GameplayPlayerManager manager)
    {
        if (manager == null || Time.timeScale <= 0f) return null;
        var pause = Singleton<PauseMenuManager>.Instance;
        if (pause != null && pause.IsOpen) return null;

        var alive = manager.AlivePlayers;
        for (int i = 0; i < alive.Count; i++)
        {
            var player = alive[i];
            if (player != null && manager.IsPlayerLocalPlayer(player)) return player;
        }
        return null;
    }

    private static void Kill(GameplayPlayerManager manager, GameplayPlayer player)
    {
        var net = NetworkManager.Singleton;
        bool host = net != null && net.IsServer;
        // Жетоны общие на всю команду, поэтому обнуляем их, только когда эта смерть и так
        // конец забега: мы хост и живых больше нет. Тогда игра не воскресит последнего
        // и сразу покажет итоги.
        if (HoldToDieMod.NoRevivesEntry.Value && host && manager.AlivePlayers.Count == 1)
        {
            _noRevives = true;
            ZeroRevives();
        }

        var health = player.HealthContainer;
        health.RemoveHealthWithoutModifiers(999999f, new HealthRerollDamageInfo(), false);
        HoldToDieMod.Log.Msg(_noRevives ? "died — no revives, the run ends" : "died");
    }

    private static void ZeroRevives()
    {
        var revives = NetworkSingleton<ReviveManager>.Instance;
        if (revives != null && revives.CurrentRevives.Value > 0) revives.CurrentRevives.Value = 0;
    }

    private static void Show(GameplayPlayer player, float progress)
    {
        var camera = Camera.main;
        if (camera == null) return;
        if (_overlay == null) Build();
        _overlay.SetActive(true);

        Vector3 screen = camera.WorldToScreenPoint(player.GetPosition());
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, new Vector2(screen.x, screen.y), null, out var local);
        _ring.anchoredPosition = local;
        _fill.fillAmount = progress;
    }

    private static void Build()
    {
        _overlay = new GameObject("HoldToDie_Ring");
        Object.DontDestroyOnLoad(_overlay);
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        var scaler = _overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvas = _overlay.GetComponent<RectTransform>();

        var ring = RingSprite();
        _ring = NewImage("Ring", _canvas, ring, Back).rectTransform;
        _ring.sizeDelta = new Vector2(RingSize, RingSize);
        _fill = NewImage("Fill", _ring, ring, Fill);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Radial360;
        _fill.fillOrigin = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
        var fr = _fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>().SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Белое кольцо со сглаженными краями, цвет задаёт Image.color
    private static Sprite RingSprite()
    {
        const int size = 128;
        const float outer = 63f, inner = 52f, center = (size - 1) / 2f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
            float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
