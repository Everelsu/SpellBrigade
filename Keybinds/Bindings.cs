using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

namespace KeybindsUnlocked;

// Одна ячейка таблицы: какая привязка какого действия.
// Composite >= 0 — часть составной привязки (например, «W» у Move = composite 0, part "up"),
// иначе — первая прямая привязка нужного устройства.
internal sealed class Slot
{
    public string Map, Action;
    public bool Gamepad;
    public int Composite = -1;
    public string Part;
    public string ControlType = "Button";
    public List<Slot> Mirrors = new(); // действия, которые должны получить ту же клавишу

    public Slot(string mapAction, bool gamepad, int composite = -1, string part = null)
    {
        var p = mapAction.Split('/');
        Map = p[0]; Action = p[1]; Gamepad = gamepad; Composite = composite; Part = part;
    }

    public string Id => $"{Map}/{Action}/{(Gamepad ? "gp" : "kb")}/{Composite}/{Part}";
}

// Переназначения клавиш: хранятся в UserData/KeybindsUnlocked.json в стандартном
// формате Input System и применяются ко всем экземплярам набора действий игры
// (компоненты игры создают свои копии Controls — их может быть несколько).
internal static class Bindings
{
    private const string ModelName = "KeybindsUnlocked_Model";

    private sealed class BindingInfo
    {
        public string Name, Path;
        public bool IsComposite, IsPart;
    }

    private static string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, "KeybindsUnlocked.json");

    private static InputActionAsset _model;                 // своя копия: на ней захват клавиш и отображение
    private static Dictionary<string, List<BindingInfo>> _layout; // "Map/Action" → привязки в порядке индексов
    private static string _overrides = "";
    private static int _version = 1;
    private static readonly Dictionary<int, int> AppliedVersion = new();
    private static float _nextScan;
    private static bool _dirty = true;

    private static RebindingOperation _operation;
    private static readonly List<InputActionMap> SuspendedMaps = new();

    public static event Action Changed;
    public static bool IsRebinding => _operation != null;

    public static void Init()
    {
        try { if (File.Exists(FilePath)) _overrides = DropBrokenOverrides(File.ReadAllText(FilePath)); }
        catch (Exception e) { KeybindsMod.Log.Warning($"can't read {FilePath}: {e.Message}"); }

        try
        {
            InputSystem.add_onActionChange((Il2CppSystem.Action<Il2CppSystem.Object, InputActionChange>)
                (Action<Il2CppSystem.Object, InputActionChange>)((_, change) =>
                {
                    if (change == InputActionChange.ActionMapEnabled) _dirty = true;
                }));
        }
        catch (Exception e) { KeybindsMod.Log.Warning($"can't watch input changes: {e.Message}"); }
    }

    // 1.0.0 мог записать «нажатие указателя» вместо кнопки мыши — такие переназначения выбрасываем
    private static string DropBrokenOverrides(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains("/press\"")) return json;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var kept = new List<string>();
            foreach (var b in doc.RootElement.GetProperty("bindings").EnumerateArray())
            {
                string path = b.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                if (path.EndsWith("/press", StringComparison.OrdinalIgnoreCase) || path.Contains("anyKey", StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(b.GetRawText());
            }
            string cleaned = "{\"bindings\":[" + string.Join(",", kept) + "]}";
            File.WriteAllText(FilePath, cleaned);
            return cleaned;
        }
        catch { return json; }
    }

    // Каждый кадр: новые экземпляры набора действий получают переназначения
    public static void Tick()
    {
        if (!_dirty && Time.unscaledTime < _nextScan) return;
        _dirty = false;
        _nextScan = Time.unscaledTime + 1f;
        ApplyToAll();
    }

    private static bool IsGameAsset(InputActionAsset a) =>
        a != null && a.name != ModelName && a.FindActionMap("Gameplay", false) != null && a.FindActionMap("UI", false) != null;

    private static void ApplyToAll()
    {
        bool appliedAny = false;
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (!IsGameAsset(asset)) continue;
            EnsureModel(asset);
            int id = asset.GetInstanceID();
            if (AppliedVersion.TryGetValue(id, out int v) && v == _version) continue;
            try
            {
                ApplyOverrides(asset);
                AppliedVersion[id] = _version;
                appliedAny = true;
            }
            catch (Exception e) { KeybindsMod.Log.Warning($"can't apply keybinds to '{asset.name}': {e.Message}"); }
        }
        // экран мог успеть нарисовать иконки клавиш до того, как новый набор получил
        // переназначения — перерисовываем
        if (appliedAny && !string.IsNullOrEmpty(_overrides)) Applied?.Invoke();
    }

    public static event Action Applied;

    private static void ApplyOverrides(InputActionAsset asset)
    {
        var collection = asset.Cast<IInputActionCollection2>();
        InputActionRebindingExtensions.RemoveAllBindingOverrides(collection);
        if (!string.IsNullOrEmpty(_overrides))
            InputActionRebindingExtensions.LoadBindingOverridesFromJson(collection, _overrides, true);
    }

    private static void EnsureModel(InputActionAsset template)
    {
        if (_model != null) return;
        string json = template.ToJson();
        _model = InputActionAsset.FromJson(json);
        _model.name = ModelName;
        _model.hideFlags = HideFlags.HideAndDontSave;
        _layout = ParseLayout(json);
        ApplyOverrides(_model);
    }

    public static bool Ready => _model != null;

    // Порядок привязок у действия = порядок строк "bindings" карты с этим действием
    private static Dictionary<string, List<BindingInfo>> ParseLayout(string json)
    {
        var layout = new Dictionary<string, List<BindingInfo>>();
        using var doc = JsonDocument.Parse(json);
        foreach (var map in doc.RootElement.GetProperty("maps").EnumerateArray())
        {
            string mapName = map.GetProperty("name").GetString();
            foreach (var b in map.GetProperty("bindings").EnumerateArray())
            {
                string key = mapName + "/" + b.GetProperty("action").GetString();
                if (!layout.TryGetValue(key, out var list)) layout[key] = list = new List<BindingInfo>();
                list.Add(new BindingInfo
                {
                    Name = b.TryGetProperty("name", out var n) ? n.GetString() : "",
                    Path = b.TryGetProperty("path", out var p) ? p.GetString() : "",
                    IsComposite = b.TryGetProperty("isComposite", out var c) && c.GetBoolean(),
                    IsPart = b.TryGetProperty("isPartOfComposite", out var pc) && pc.GetBoolean(),
                });
            }
        }
        return layout;
    }

    public static int Resolve(Slot slot)
    {
        if (_layout == null || !_layout.TryGetValue(slot.Map + "/" + slot.Action, out var list)) return -1;
        if (slot.Composite >= 0)
        {
            int seen = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsComposite) { seen++; continue; }
                if (seen == slot.Composite && list[i].IsPart &&
                    string.Equals(list[i].Name, slot.Part, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }
        string[] prefixes = slot.Gamepad ? new[] { "<Gamepad>" } : new[] { "<Keyboard>", "<Mouse>" };
        foreach (var prefix in prefixes)
            for (int i = 0; i < list.Count; i++)
                if (!list[i].IsComposite && !list[i].IsPart && list[i].Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return i;
        return -1;
    }

    private static InputAction ModelAction(Slot slot) => _model?.FindAction($"{slot.Map}/{slot.Action}", false);

    public static string Display(Slot slot)
    {
        var action = ModelAction(slot);
        int index = Resolve(slot);
        if (action == null || index < 0) return "—";
        try
        {
            string s = KeyName(action, index, slot.Gamepad);
            return string.IsNullOrWhiteSpace(s) ? "—" : s;
        }
        catch { return "?"; }
    }

    // Какая клавиша сейчас стоит в ячейке и переназначена ли она игроком
    public static (string Path, bool Overridden) State(Slot slot)
    {
        var action = ModelAction(slot);
        int index = Resolve(slot);
        if (action == null || index < 0) return (null, false);
        var binding = action.bindings[index];
        string overridePath = binding.overridePath;
        return (binding.effectivePath, !string.IsNullOrEmpty(overridePath) && overridePath != binding.path);
    }

    // Название клавиши. Для клавиатуры — физическое латинское (как на иконках игры),
    // иначе Unity подписывает по текущей раскладке Windows: на русской D показалась бы как «В».
    public static string KeyName(InputAction action, int index, bool gamepad)
    {
        if (gamepad)
            return InputActionRebindingExtensions.GetBindingDisplayString(action, index, default(InputBinding.DisplayStringOptions));
        return InputControlPath.ToHumanReadableString(action.bindings[index].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames, null);
    }

    public static bool CanRebind(Slot slot) => ModelAction(slot) != null && Resolve(slot) >= 0;

    // Ждём нажатия новой клавиши. Пока ждём, ввод игры выключен — иначе, например,
    // Esc закрыл бы меню, а клавиши ходили бы по кнопкам.
    public static void Rebind(Slot slot, Action<bool> done)
    {
        var action = ModelAction(slot);
        int index = Resolve(slot);
        if (action == null || index < 0 || _operation != null) { done?.Invoke(false); return; }

        SuspendGameInput();
        try
        {
            var op = InputActionRebindingExtensions.PerformInteractiveRebinding(action, index)
                .WithExpectedControlType(slot.ControlType)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10f)
                .OnMatchWaitForAnother(0.1f);
            if (slot.Gamepad) op = op.WithControlsHavingToMatchPath("<Gamepad>");
            else
                // клавиатура и настоящие кнопки мыши; «нажатие указателя» (<Pointer>/press) и левую
                // кнопку не берём — они срабатывают от любого клика, в том числе по самой ячейке
                op = op.WithControlsHavingToMatchPath("<Keyboard>")
                       .WithControlsHavingToMatchPath("<Mouse>/rightButton")
                       .WithControlsHavingToMatchPath("<Mouse>/middleButton")
                       .WithControlsHavingToMatchPath("<Mouse>/forwardButton")
                       .WithControlsHavingToMatchPath("<Mouse>/backButton")
                       .WithControlsExcluding("<Pointer>/press")
                       .WithControlsExcluding("<Keyboard>/anyKey");

            op = op.OnComplete((Il2CppSystem.Action<RebindingOperation>)(Action<RebindingOperation>)(_ => Finish(slot, action, index, true, done)))
                   .OnCancel((Il2CppSystem.Action<RebindingOperation>)(Action<RebindingOperation>)(_ => Finish(slot, action, index, false, done)));
            _operation = op;
            op.Start();
        }
        catch (Exception e)
        {
            KeybindsMod.Log.Error($"rebinding failed: {e}");
            _operation = null;
            ResumeGameInput();
            done?.Invoke(false);
        }
    }

    public static void CancelRebind()
    {
        try { _operation?.Cancel(); } catch { }
    }

    private static void Finish(Slot slot, InputAction action, int index, bool success, Action<bool> done)
    {
        try { _operation?.Dispose(); } catch { }
        _operation = null;
        ResumeGameInput();

        if (success)
        {
            try
            {
                string path = action.bindings[index].effectivePath;
                foreach (var mirror in slot.Mirrors)
                {
                    var mirrorAction = ModelAction(mirror);
                    int mirrorIndex = Resolve(mirror);
                    if (mirrorAction != null && mirrorIndex >= 0)
                        InputActionRebindingExtensions.ApplyBindingOverride(mirrorAction, mirrorIndex, path);
                }
                Commit();
            }
            catch (Exception e) { KeybindsMod.Log.Error($"can't save keybind: {e}"); }
        }
        done?.Invoke(success);
    }

    public static void ResetAll()
    {
        if (_model == null) return;
        InputActionRebindingExtensions.RemoveAllBindingOverrides(_model.Cast<IInputActionCollection2>());
        Commit();
    }

    private static void Commit()
    {
        _overrides = InputActionRebindingExtensions.SaveBindingOverridesAsJson(_model.Cast<IInputActionCollection2>()) ?? "";
        try { File.WriteAllText(FilePath, _overrides); }
        catch (Exception e) { KeybindsMod.Log.Warning($"can't write {FilePath}: {e.Message}"); }
        _version++;
        ApplyToAll();
        Changed?.Invoke();
    }

    private static void SuspendGameInput()
    {
        SuspendedMaps.Clear();
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (!IsGameAsset(asset)) continue;
            foreach (var mapName in new[] { "Gameplay", "UI" })
            {
                var map = asset.FindActionMap(mapName, false);
                if (map != null && map.enabled) { map.Disable(); SuspendedMaps.Add(map); }
            }
        }
    }

    private static void ResumeGameInput()
    {
        foreach (var map in SuspendedMaps)
            try { map?.Enable(); } catch { }
        SuspendedMaps.Clear();
    }
}
