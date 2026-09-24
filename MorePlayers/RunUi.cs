using System;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using Object = UnityEngine.Object;

namespace MorePlayers;

// Интерфейс, рассчитанный на 4 игроков: командная статистика в итогах забега
// и портреты отряда во время забега
internal static class RunUi
{
    // --- Итоги: командная статистика ---
    // В таблице 4 колонки. Показываем по комнате (как в лобби), Tab — следующая комната.

    private static TeamStatsPanel _panel;
    private static EndOfRunRecord _record;
    private static int _room = -1, _rooms;
    private static Il2CppSystem.Collections.Generic.List<PlayerData> _all;
    private static TMP_Text _label;
    private static bool _inner;

    public static void TeamStatsPrefix(TeamStatsPanel __instance, EndOfRunRecord record)
    {
        try
        {
            var all = record?._PlayerData_k__BackingField;
            if (all == null || all.Count <= Rooms.Seats) { HideLabel(); return; }

            if (!_inner || _panel != __instance || _record != record)
            {
                _panel = __instance;
                _record = record;
                _room = LocalRoom(all);
            }
            _rooms = Rooms.Count(all.Count);
            _room = ((_room % _rooms) + _rooms) % _rooms;

            var mine = new Il2CppSystem.Collections.Generic.List<PlayerData>();
            for (int i = 0; i < all.Count; i++)
                if (Rooms.Of(i, all.Count) == _room) mine.Add(all[i]);
            _all = all;
            record._PlayerData_k__BackingField = mine; // вернём в постфиксе
            ShowLabel(__instance);
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"team stats: {e.Message}"); }
    }

    public static void TeamStatsPostfix(EndOfRunRecord record)
    {
        if (_all == null || record == null) return;
        record._PlayerData_k__BackingField = _all;
        _all = null;
    }

    private static int LocalRoom(Il2CppSystem.Collections.Generic.List<PlayerData> all)
    {
        ulong me = Rooms.LocalClientId;
        for (int i = 0; i < all.Count; i++)
            if (all[i]?.PlayerIdentification?.ClientId == me) return Rooms.Of(i, all.Count);
        return 0;
    }

    public static void Tick()
    {
        if (_panel == null || _record == null || _rooms <= 1) return;
        if (!_panel.gameObject.activeInHierarchy) return;
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame) return;

        _room++;
        _inner = true;
        try { _panel.SetValues(_record); }
        catch (Exception e) { MorePlayersMod.Log.Warning($"team stats: {e.Message}"); }
        finally { _inner = false; }
    }

    private static void ShowLabel(TeamStatsPanel panel)
    {
        if (_label == null || _label.transform.parent != panel.transform)
        {
            var template = panel.GetComponentInChildren<TMP_Text>(true);
            if (template == null) return;
            var go = Object.Instantiate(template.gameObject, panel.transform);
            go.name = "MorePlayers_Room";
            foreach (var loc in go.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
            for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            _label = go.GetComponent<TMP_Text>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.raycastTarget = false;
            var r = _label.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(900f, 60f);
            r.anchoredPosition = new Vector2(0f, 12f);
        }
        _label.gameObject.SetActive(true);
        _label.text = $"<color=#FFFFFF80>[Tab]</color>  {Strings.Get(Strings.Room)} {_room + 1}/{_rooms}";
    }

    private static void HideLabel()
    {
        if (_label != null) _label.gameObject.SetActive(false);
        _rooms = 0;
    }

    // --- Забег: портреты отряда ---
    // Игра рассчитана максимум на 3 портрета союзников; при большем отряде уменьшаем
    // всю панель, прижимая её к тому краю экрана, к которому она привязана.

    private static Vector3 _baseScale = Vector3.zero;

    public static void PartyChangedPostfix(GameplayPartyOverviewUI __instance)
    {
        try
        {
            var list = __instance.playerOverviews;
            if (list == null) return;
            int count = list.Count, vanillaMax = 3;
            ulong me = Rooms.LocalClientId;
            for (int i = 0; i < count; i++)
                if (list[i] != null && list[i].Identity != null && list[i].Identity.OwnerClientId == me) { vanillaMax = 4; break; }

            var rect = __instance.transform.TryCast<RectTransform>();
            if (rect == null) return;
            if (_baseScale == Vector3.zero || __instance.transform.localScale == Vector3.one) _baseScale = rect.localScale;
            float k = count <= vanillaMax ? 1f : Mathf.Max(0.45f, vanillaMax / (float)count);

            // опорная точка — угол привязки, чтобы панель не уезжала с края экрана
            var pivot = (rect.anchorMin + rect.anchorMax) * 0.5f;
            if (rect.pivot != pivot)
            {
                var delta = pivot - rect.pivot;
                var size = rect.rect.size;
                rect.pivot = pivot;
                rect.anchoredPosition += new Vector2(delta.x * size.x * rect.localScale.x, delta.y * size.y * rect.localScale.y);
            }
            rect.localScale = _baseScale * k;
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"party overview: {e.Message}"); }
    }
}
