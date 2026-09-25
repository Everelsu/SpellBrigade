using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellBrigade.ModMenu;

// Пока печатаешь в поиске, игра не должна слышать клавиатуру: иначе Q/E переключают
// вкладки настроек, WASD и стрелки гоняют выделение, Esc закрывает окно.
// Выключаем все включённые действия карты «UI», кроме мыши, и включаем их обратно.
internal static class KeyboardCapture
{
    private static readonly string[] PointerActions =
        { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "TrackedDevicePosition", "TrackedDeviceOrientation" };

    private static readonly List<InputAction> Disabled = new();

    public static bool Active { get; private set; }

    public static void Begin()
    {
        if (Active) return;
        Active = true;
        AppDomain.CurrentDomain.SetData("SpellBrigade.Typing", true); // клавиши других модов тоже молчат
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            var map = asset != null ? asset.FindActionMap("UI", false) : null;
            if (map == null || !map.enabled) continue;
            var actions = map.actions;
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || !action.enabled || Array.IndexOf(PointerActions, action.name) >= 0) continue;
                action.Disable();
                Disabled.Add(action);
            }
        }
    }

    public static void End()
    {
        if (!Active) return;
        Active = false;
        AppDomain.CurrentDomain.SetData("SpellBrigade.Typing", false);
        foreach (var action in Disabled)
            try { action?.Enable(); } catch { }
        Disabled.Clear();
    }
}
