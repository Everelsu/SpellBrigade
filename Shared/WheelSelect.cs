using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBrigade.Shared;

// Колесо мыши над переключателем «< значение >» листает варианты: вверх — следующий,
// вниз — предыдущий. Работает у всех переключателей — и игры, и модов. Пока курсор над
// переключателем внутри прокручиваемого списка, список колесом не листается, чтобы одно
// движение не делало двух дел.
// Tick() зовут из OnUpdate все наши моды; отметка кадра лежит в AppDomain, поэтому
// сколько бы модов ни стояло, за кадр работает только один и шаг делается один.
internal static class WheelSelect
{
    private const string FrameKey = "SpellBrigade.WheelSelect.Frame";
    private const string CustomKey = "SpellBrigade.WheelSelect.Custom";

    // Кадр обрабатывает тот мод, чей OnUpdate идёт первым, — всегда один и тот же,
    // поэтому своё состояние у каждой копии не мешает
    private sealed class State
    {
        public ScrollRect Blocked;
        public float BlockedSensitivity;
        public Vector2 LastMouse = new(-1f, -1f);
    }

    private static readonly State Own = new();

    // Свои поля со значением (не Selector) от всех модов. Тип только из mscorlib и Unity —
    // одинаковый во всех dll, поэтому список общий
    private static List<(GameObject target, Action<int> step)> Custom
    {
        get
        {
            var domain = AppDomain.CurrentDomain;
            if (domain.GetData(CustomKey) is List<(GameObject, Action<int>)> list) return list;
            list = new List<(GameObject, Action<int>)>();
            domain.SetData(CustomKey, list);
            return list;
        }
    }

    // Колесо над target вызывает step(+1 / -1)
    public static void Register(GameObject target, Action<int> step)
    {
        if (target != null && step != null) Custom.Add((target, step));
    }

    public static void Tick()
    {
        try { TickCore(); }
        catch { } // удобство, а не функция мода — сбой не должен ломать OnUpdate вызвавшего
    }

    private static void TickCore()
    {
        int frame = Time.frameCount;
        var domain = AppDomain.CurrentDomain;
        if (domain.GetData(FrameKey) is int done && done == frame) return;
        domain.SetData(FrameKey, frame);
        if (domain.GetData("SpellBrigade.Modal") is true) return; // поверх открыто окно мода — под ним ничего не листаем

        var mouse = Mouse.current;
        if (mouse == null) return;
        var state = Own;
        Vector2 position = mouse.position.ReadValue();
        float wheel = mouse.scroll.ReadValue().y;
        bool moved = (position - state.LastMouse).sqrMagnitude > 0.25f;
        if (!moved && Mathf.Abs(wheel) < 0.01f) return;
        state.LastMouse = position;

        var (selector, custom, rect) = Hovered(position);
        BlockScroll(state, rect != null ? ScrollRectAbove(rect) : null);
        if (Mathf.Abs(wheel) < 0.01f || rect == null) return;

        int step = wheel > 0f ? 1 : -1;
        try
        {
            if (selector != null) { if (step > 0) selector.SelectNext(); else selector.SelectPrevious(); }
            else custom?.Invoke(step);
        }
        catch { }
    }

    private static (Selector selector, Action<int> custom, RectTransform rect) Hovered(Vector2 position)
    {
        var custom = Custom;
        custom.RemoveAll(c => c.target == null);
        foreach (var (target, step) in custom)
            if (target.activeInHierarchy && Contains(target.GetComponent<RectTransform>(), position))
                return (null, step, target.GetComponent<RectTransform>());

        foreach (var selector in Object.FindObjectsOfType<Selector>())
        {
            if (selector == null || !selector.IsInteractable()) continue;
            var rect = selector.GetComponent<RectTransform>();
            if (Contains(rect, position)) return (selector, null, rect);
        }
        return (null, null, null);
    }

    // Точка внутри прямоугольника и не обрезана маской прокрутки, прямоугольник виден
    private static bool Contains(RectTransform rect, Vector2 position)
    {
        if (rect == null) return false;
        Canvas root = null;
        for (var t = rect.transform; t != null; t = t.parent)
        {
            var group = t.GetComponent<CanvasGroup>();
            if (group != null && (group.alpha < 0.01f || !group.blocksRaycasts)) return false;
            var canvas = t.GetComponent<Canvas>();
            if (canvas != null) root = canvas;
        }
        if (root == null) return false;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(rect, position, camera)) return false;
        for (var t = rect.parent; t != null; t = t.parent)
            if (t.GetComponent<RectMask2D>() != null && !RectTransformUtility.RectangleContainsScreenPoint(t.GetComponent<RectTransform>(), position, camera))
                return false;
        return true;
    }

    private static ScrollRect ScrollRectAbove(RectTransform rect)
    {
        for (var t = rect.parent; t != null; t = t.parent)
        {
            var scroll = t.GetComponent<ScrollRect>();
            if (scroll != null) return scroll;
        }
        return null;
    }

    private static void BlockScroll(State state, ScrollRect scroll)
    {
        bool same = state.Blocked != null && scroll != null && state.Blocked.Pointer == scroll.Pointer;
        if (same) return;
        if (state.Blocked != null) state.Blocked.scrollSensitivity = state.BlockedSensitivity;
        state.Blocked = scroll;
        if (scroll == null) return;
        state.BlockedSensitivity = scroll.scrollSensitivity;
        scroll.scrollSensitivity = 0f;
    }
}
