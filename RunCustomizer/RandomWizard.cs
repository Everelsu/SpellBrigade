using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RunCustomizer;

// Две кнопки в сетке волшебников, на месте пустых рамок в последнем ряду:
// «Случайный» — рулетка силуэтов останавливается на случайном открытом волшебнике и выбирает
//   его, как будто игрок кликнул по нему.
// «Сюрприз» — в лобби остаётся прежний волшебник (у себя на месте крутятся случайные модели),
//   случайный выбирается при старте забега — тем же путём, что и обычный выбор.
// Пока подсвечена или выбрана одна из них, слева её описание, в центре мелькают чёрные силуэты.
internal static class RandomWizard
{
    private enum Kind { Instant, Surprise }

    private static readonly Dictionary<Kind, string> Names = new()
    {
        [Kind.Instant] = "RC_RandomWizard",
        [Kind.Surprise] = "RC_SurpriseWizard",
    };

    private static bool _pendingSurprise;   // выбран «Сюрприз»
    private static Kind? _shown;            // что показано слева (держится, пока не подсвечен настоящий волшебник)
    private static bool _cycling, _rolling, _seatCycling;
    private static int _appliedFrame = -1;
    private static Color? _aliasColor;
    private static readonly List<SkinId> Skins = new();
    private static readonly System.Random Rnd = new();

    private static bool IsOurs(CharacterButton b) => b != null && b.name.StartsWith("RC_");

    // ---------- кнопки ----------

    public static void InitializePostfix(CharacterSelector __instance)
    {
        try { AddButtons(__instance); }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] {e}"); }
    }

    private static void AddButtons(CharacterSelector selector)
    {
        var used = selector.initializedCharacterButtons;
        if (used == null || used.Count == 0) return;

        Skins.Clear();
        foreach (var b in used)
            if (b?.resource != null && !b.resource.Disabled) Skins.Add(b.resource.DefaultSkin);

        // Кнопке нужен настоящий открытый волшебник: игра проверяет его «замок» при нажатии.
        // На выбор он не влияет — клики по нашим кнопкам обрабатываем сами.
        var placeholder = RandomCandidate(selector, null)?.resource;
        if (placeholder == null) return;

        var last = used[used.Count - 1];
        var grid = last.transform.parent;
        int after = 0;
        foreach (var b in used)
            if (b != null && b.transform.parent == grid) after = Math.Max(after, b.transform.GetSiblingIndex());

        foreach (var kind in new[] { Kind.Instant, Kind.Surprise })
        {
            var ours = Find(selector, kind);
            if (ours == null)
            {
                var go = Object.Instantiate(last.gameObject, grid);
                go.name = Names[kind];
                go.transform.SetSiblingIndex(++after);
                ours = go.GetComponent<CharacterButton>();

                var selectable = go.GetComponent<Selectable>();
                if (selectable != null)
                {
                    var nav = selectable.navigation;
                    nav.mode = Navigation.Mode.Automatic; // соседей для геймпада Unity подберёт сама
                    selectable.navigation = nav;
                }

                var k = kind;
                ours.add_OnSelected((Il2CppSystem.Action)(Action)(() => OnClicked(selector, k)));
                ours.eventHook?.add_OnHighlightStarted((Il2CppSystem.Action)(Action)(() =>
                {
                    try { Show(PanelOf(selector), k); }
                    catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] {e}"); }
                }));

                // Сетка — волшебники и пустые рамки-заглушки до ровного ряда: занимаем место одной из них
                for (int i = grid.childCount - 1; i >= 0; i--)
                {
                    var child = grid.GetChild(i);
                    // только клетки сетки: рядом лежит служебный Audio (без RectTransform) — его не трогаем
                    if (child.gameObject.activeSelf && child.GetComponent<RectTransform>() != null
                        && child.GetComponent<CharacterButton>() == null)
                    {
                        child.gameObject.SetActive(false);
                        break;
                    }
                }
            }

            ours.gameObject.SetActive(true);
            ours.Initialize(placeholder);
            var portrait = ours.iconImage;
            if (portrait != null)
            {
                var size = portrait.rectTransform.rect.size;
                float aspect = size.x > 1f && size.y > 1f ? size.x / size.y : 0f;
                portrait.sprite = Sprites.Get(kind == Kind.Instant ? "random_wizard.png" : "surprise_wizard.png", aspect);
                portrait.preserveAspect = false;
            }
            if (ours.extraGoldBonusPanel != null) ours.extraGoldBonusPanel.SetActive(false);
        }

        ShowSelected(selector, _pendingSurprise);
    }

    private static void OnClicked(CharacterSelector selector, Kind kind)
    {
        try
        {
            if (_rolling) return;
            var panel = PanelOf(selector);
            if (kind == Kind.Surprise)
            {
                _pendingSurprise = true;
                selector.soundPlayer?.PlayCharacterSelectSound(); // тот же звук, что при выборе волшебника
                ShowSelected(selector, true);
                Show(panel, Kind.Surprise);
                if (!_seatCycling) MelonCoroutines.Start(CycleSeatModel());
                return;
            }
            var current = SingletonPersistent<LocalPlayer>.Instance?.GetSelectedCharacter();
            var target = RandomCandidate(selector, current);
            if (target != null) MelonCoroutines.Start(Roulette(selector, panel, target));
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] {e}"); }
    }

    // Подсвечен настоящий волшебник — слева снова он. Хуки на панели, а не на селекторе:
    // обработчики селектора игра встроила в лямбды, и хук на них не срабатывает.
    public static void PanelHighlightPrefix()
    {
        if (!_rolling) _shown = null;
    }

    // Игрок выбрал конкретного волшебника — сюрприз отменяется
    public static void PanelSelectedPrefix(CharacterPanel __instance)
    {
        if (_rolling) return;
        _shown = null;
        if (!_pendingSurprise) return;
        _pendingSurprise = false;
        var selector = __instance.selector;
        if (selector != null) ShowSelected(selector, false);
    }

    // Окно открылось заново и игра подсветила текущего волшебника — возвращаем «Сюрприз»
    public static void InitialSelectionPostfix(CharacterSelector __instance)
    {
        if (!_pendingSurprise) return;
        ShowSelected(__instance, true);
        Show(PanelOf(__instance), Kind.Surprise);
    }

    // Рамка «выбрано»: на «Сюрпризе», если он выбран, иначе на настоящем волшебнике
    private static void ShowSelected(CharacterSelector selector, bool surpriseSelected)
    {
        var instant = Find(selector, Kind.Instant);
        if (instant?.animator != null) instant.animator.SetBool(CharacterButton.IsSelected, false);
        var surprise = Find(selector, Kind.Surprise);
        if (surprise?.animator != null) surprise.animator.SetBool(CharacterButton.IsSelected, surpriseSelected);
        var real = selector.selectedCharacterButton;
        if (real?.animator != null && !IsOurs(real)) real.animator.SetBool(CharacterButton.IsSelected, !surpriseSelected);
    }

    // ---------- левая часть экрана и силуэты ----------

    // Игра показала волшебника: если держится наш — показываем его поверх, иначе убираем наше
    public static void DisplayPostfix(CharacterPanel __instance, bool isLocked)
    {
        try
        {
            if (_shown.HasValue) Show(__instance, _shown.Value);
            else Restore(__instance, isLocked);
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] {e}"); }
    }

    private static void Show(CharacterPanel panel, Kind kind)
    {
        if (panel == null) return;
        _shown = kind;

        var toggler = panel.loreAndDetailsDisplayToggler;
        if (toggler != null)
        {
            toggler.ToggleLoreDisplay(true);
            toggler.toggleLoreDisplayPrompt?.gameObject.SetActive(false);
        }
        var lore = panel.characterLoreDisplay;
        bool chosen = kind == Kind.Surprise && _pendingSurprise;
        SetText(panel.characterInfoDisplay?.characterVisualizer?.nameLocalizer,
                Strings.Get(kind == Kind.Instant ? Strings.RandomWizard : Strings.SurpriseWizard));
        SetText(lore?.aliasLocalizer, Strings.Get(chosen ? Strings.WizardChosen : Strings.WizardAlias));
        var alias = lore?.aliasLocalizer?.GetComponent<TMP_Text>();
        if (alias != null)
        {
            _aliasColor ??= alias.color;
            alias.color = chosen ? new Color(0.25f, 0.95f, 0.3f) : _aliasColor.Value;
        }
        SetText(lore?.descriptionLocalizer, Strings.Get(kind == Kind.Instant ? Strings.WizardDescInstant : Strings.WizardDescSurprise));
        panel.characterRankDisplay?.gameObject.SetActive(false);
        panel.lockedCharacterInfoDisplay?.gameObject.SetActive(false);

        // игра могла только что показать настоящую модель — сразу в силуэт
        var model = panel.localCharacterDisplay?.image;
        if (model != null) model.color = Color.black;
        if (!_cycling && !_rolling) MelonCoroutines.Start(CycleSilhouettes(panel));
    }

    private static void Restore(CharacterPanel panel, bool isLocked)
    {
        var lore = panel.characterLoreDisplay;
        foreach (var loc in new[] { panel.characterInfoDisplay?.characterVisualizer?.nameLocalizer, lore?.aliasLocalizer, lore?.descriptionLocalizer })
            if (loc != null && !loc.enabled) { loc.enabled = true; loc.RefreshString(); }
        var alias = lore?.aliasLocalizer?.GetComponent<TMP_Text>();
        if (alias != null && _aliasColor.HasValue) alias.color = _aliasColor.Value;
        panel.loreAndDetailsDisplayToggler?.toggleLoreDisplayPrompt?.gameObject.SetActive(true);
        // последний кадр силуэтов мог перекрасить модель — возвращаем как у игры
        panel.localCharacterDisplay?.SetDarkened(isLocked);
    }

    private static void SetText(LocalizeStringEvent loc, string text)
    {
        if (loc == null) return;
        loc.enabled = false;
        var tmp = loc.GetComponent<TMP_Text>() ?? loc.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
    }

    private static int _lastSkin = -1;

    private static SkinId NextSkin()
    {
        int i = Rnd.Next(Skins.Count);
        if (i == _lastSkin && Skins.Count > 1) i = (i + 1) % Skins.Count;
        _lastSkin = i;
        return Skins[i];
    }

    private static void ShowSilhouette(CharacterPanel panel, SkinId skin)
    {
        var display = panel.localCharacterDisplay;
        display.Display(skin);
        if (display.image != null) display.image.color = Color.black;
    }

    // Модели волшебников по очереди, залитые чёрным — как у закрытых
    private static IEnumerator CycleSilhouettes(CharacterPanel panel)
    {
        _cycling = true;
        while (_shown.HasValue && !_rolling && panel != null && panel.isActiveAndEnabled && Skins.Count > 0)
        {
            try { ShowSilhouette(panel, NextSkin()); }
            catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] silhouettes: {e.Message}"); break; }
            yield return new WaitForSeconds(0.35f);
        }
        _cycling = false;
    }

    // «Случайный»: силуэты мелькают всё медленнее и останавливаются на выпавшем волшебнике
    private static IEnumerator Roulette(CharacterSelector selector, CharacterPanel panel, CharacterButton target)
    {
        _rolling = true;
        Show(panel, Kind.Instant);
        float delay = 0.05f;
        while (delay < 0.3f && panel != null && Skins.Count > 0)
        {
            try { ShowSilhouette(panel, NextSkin()); }
            catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] roulette: {e.Message}"); break; }
            yield return new WaitForSeconds(delay);
            delay *= 1.18f;
        }
        _rolling = false;
        _shown = null;
        try { selector.OnCharacterButtonSelected(target); } // как клик игрока по выпавшему волшебнику (со звуком)
        catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] {e}"); }
    }

    // «Сюрприз» в лобби: на своём месте крутятся случайные волшебники (видно только себе)
    private static IEnumerator CycleSeatModel()
    {
        _seatCycling = true;
        while (_pendingSurprise && Skins.Count > 0)
        {
            try
            {
                var seat = Singleton<PlayerSeatUpdater>.Instance?.GetLocalPlayerSeat();
                seat?.characterModelVisualizer?.Show(NextSkin(), false, false);
            }
            catch { /* в забеге мест нет — ждём возврата в лобби */ }
            yield return new WaitForSeconds(0.6f);
        }
        _seatCycling = false;
        try { Singleton<PlayerSeatUpdater>.Instance?.RefreshSeats(); } catch { }
    }

    // Блок описания у игры только для показа: его CanvasGroup не пропускает клики.
    // Разрешаем клики у групп между элементом и панелью (прозрачность при затухании — остаётся игры).
    internal static void AllowClicks(Transform from, Transform upTo)
    {
        for (var t = from.parent; t != null && t != upTo; t = t.parent)
        {
            var group = t.GetComponent<CanvasGroup>();
            if (group == null) continue;
            group.blocksRaycasts = true;
            group.interactable = true;
        }
    }

    // ---------- сюрприз при старте ----------

    // Хост: до рассылки старта
    public static void StartLevelPrefix()
    {
        if (Modes.IsHost) ApplySurprise();
    }

    // Клиент: пришёл сигнал старта, сцена забега ещё грузится — успеваем сообщить хосту
    public static void PrepareForLevelStartPrefix()
    {
        if (!Modes.IsHost) ApplySurprise();
    }

    private static void ApplySurprise()
    {
        if (!_pendingSurprise || _appliedFrame == Time.frameCount) return;
        _appliedFrame = Time.frameCount;
        try
        {
            CharacterSelector selector = null;
            foreach (var s in Resources.FindObjectsOfTypeAll<CharacterSelector>())
                if (s != null && s.initializedCharacterButtons?.Count > 0) { selector = s; break; }
            var target = selector != null ? RandomCandidate(selector, null) : null;
            if (target == null) { RunCustomizerMod.Log.Warning("[random wizard] no wizard to pick"); return; }
            target.ConfirmSelectedCharacter(); // тот же путь, что и обычный выбор
            RunCustomizerMod.Log.Msg($"[random wizard] surprise: {target.resource.Id}");
        }
        catch (Exception e) { RunCustomizerMod.Log.Error($"[random wizard] surprise failed, keeping the current wizard: {e}"); }
    }

    // ---------- общее ----------

    private static CharacterButton RandomCandidate(CharacterSelector selector, CharacterId? exclude)
    {
        var candidates = new List<CharacterButton>();
        foreach (var b in selector.initializedCharacterButtons)
        {
            var r = b?.resource;
            if (r == null || IsOurs(b) || r.Disabled || r.IsLockedByProgression()) continue;
            candidates.Add(b);
        }
        if (exclude.HasValue && candidates.Count > 1) candidates.RemoveAll(b => b.resource.Id == exclude.Value);
        return candidates.Count > 0 ? candidates[Rnd.Next(candidates.Count)] : null;
    }

    private static CharacterPanel PanelOf(CharacterSelector selector)
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<CharacterPanel>())
            if (p != null && p.selector != null && p.selector.Pointer == selector.Pointer) return p;
        return null;
    }

    // Наши кнопки не входят в списки селектора — ищем их в сетке
    private static CharacterButton Find(CharacterSelector selector, Kind kind)
    {
        var used = selector.initializedCharacterButtons;
        if (used == null || used.Count == 0 || used[0] == null) return null;
        var t = used[0].transform.parent.Find(Names[kind]);
        return t != null ? t.GetComponent<CharacterButton>() : null;
    }
}
