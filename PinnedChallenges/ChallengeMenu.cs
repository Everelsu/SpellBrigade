using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinnedChallenges;

// Меню испытаний: вся карточка становится кнопкой «закрепить/открепить», в правом верхнем
// углу — булавка: тусклая, если не закреплено, золотая — если закреплено. У выполненных её нет.
internal static class ChallengeMenu
{
    private const string PinName = "PinnedChallenges_Pin", HitName = "PinnedChallenges_Hit";
    private const float PinSize = 34f;
    private static readonly Color Dim = new(1f, 1f, 1f, 0.28f);

    private sealed class Card
    {
        public ChallengeProgressPanel Panel;
        public Image Pin;
        public ChallengeId Id;
    }

    private static readonly Dictionary<IntPtr, Card> Cards = new();
    private static bool _subscribed;

    public static void ShowPostfix(ChallengeProgressPanel __instance, ChallengeConfiguration configuration)
    {
        try
        {
            if (__instance == null || configuration == null) return;
            if (__instance.transform.parent?.name == RunHud.HiddenName) return; // наши же копии для текста в забеге
            if (!_subscribed) { _subscribed = true; Pins.Changed += RefreshAll; }
            if (!Cards.TryGetValue(__instance.Pointer, out var card) || card.Pin == null)
                Cards[__instance.Pointer] = card = new Card { Panel = __instance, Pin = CreatePin(__instance) };
            card.Id = configuration.Id;
            Refresh(card);
        }
        catch (Exception e) { PinnedChallengesMod.Log.Warning($"challenge card: {e.Message}"); }
    }

    private static Image CreatePin(ChallengeProgressPanel panel)
    {
        foreach (var name in new[] { PinName, HitName })
        {
            var old = panel.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // Прозрачная кнопка поверх всей карточки: клик в любом месте закрепляет. Отдельным
        // объектом, чтобы при повторной настройке карточки пересоздаваться целиком и не
        // навешивать второй обработчик (два переключения за клик = ничего). Наведение
        // (подсказка о блокировке) и прокрутка колесом/перетаскиванием доходят до карточки
        // и списка как раньше.
        var hit = Ui.NewRect(HitName, panel.transform);
        hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one;
        hit.offsetMin = hit.offsetMax = Vector2.zero;
        hit.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        IntPtr key = panel.Pointer;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(Action)(() =>
        {
            try
            {
                if (Cards.TryGetValue(key, out var card) && !IsCompleted(card.Id)) Pins.Toggle(card.Id);
            }
            catch (Exception e) { PinnedChallengesMod.Log.Warning($"pin: {e.Message}"); }
        }));

        var pin = Ui.NewImage(PinName, panel.transform, Dim);
        pin.sprite = Ui.Pin;
        pin.preserveAspect = true;
        var r = pin.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = Vector2.one;
        r.sizeDelta = new Vector2(PinSize, PinSize);
        r.anchoredPosition = new Vector2(-14f, -10f);
        return pin;
    }

    private static void RefreshAll()
    {
        var dead = new List<IntPtr>();
        foreach (var (key, card) in Cards)
        {
            if (card.Panel == null || card.Pin == null) { dead.Add(key); continue; }
            Refresh(card);
        }
        foreach (var key in dead) Cards.Remove(key);
    }

    private static void Refresh(Card card)
    {
        bool completed = IsCompleted(card.Id); // выполненное закреплять незачем
        card.Pin.gameObject.SetActive(!completed);
        card.Pin.color = Pins.IsPinned(card.Id) ? Ui.Gold : Dim;
    }

    internal static bool IsCompleted(ChallengeId id)
    {
        try { return SingletonPersistent<LocalPlayer>.Instance?.GetChallengeManager()?.IsCompleted(id) ?? false; }
        catch { return false; }
    }
}
