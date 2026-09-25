using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinnedChallenges;

// В забеге: закреплённые испытания компактными строками — иконка, описание, тонкая полоска
// прогресса и «109 / 1 066». Где на экране — выбирает игрок (LayoutEditor), по умолчанию
// в правом верхнем углу под кнопкой настроек.
// Описание берём у скрытой копии карточки из всплывашки «испытание выполнено»:
// игра сама подставляет в неё переведённый текст и числа, мы только переписываем его.
internal static class RunHud
{
    public const string RootName = "PinnedChallenges_Hud", HiddenName = "PinnedChallenges_Source";
    public const float Width = 400f, RowHeight = 64f, Spacing = 8f;
    private const float Pad = 8f, IconSize = 46f;
    private static readonly Color Plain = new(0.05f, 0.04f, 0.09f);

    private static float BackgroundAlpha => PinnedChallengesMod.OpacityEntry?.Value ?? 0.6f;
    private static float TextAlpha => PinnedChallengesMod.TextOpacityEntry?.Value ?? 1f;
    private static bool GameStyle => PinnedChallengesMod.GameStyleEntry?.Value ?? true;

    internal sealed class Card
    {
        public ChallengeId Id;
        public ChallengeConfiguration Config;
        public TMP_Text Source, Description, Progress;
        public GameObject Bar;
        public RectTransform Fill;
        public Image Background, Icon, BarBack, FillImage;
        public Image GameBackground; // фон карточки из интерфейса игры (у скрытой копии), если нашёлся
        public bool Done;
    }

    private static readonly List<Card> Cards = new();
    private static RectTransform _root;
    private static Canvas _canvas;
    private static CanvasGroup _group;
    private static bool _inRun, _built, _warned, _subscribed;
    private static float _nextTick;

    public static void RunStarted()
    {
        if (!_subscribed)
        {
            _subscribed = true;
            Pins.Changed += Rebuild; // «Открепить всё» из паузы — строки пропадают сразу
        }
        _inRun = true;
        _built = _warned = false;
        _nextTick = 0f;
    }

    public static void RunEnded()
    {
        _inRun = _built = false;
        Cards.Clear();
        if (_root != null) Object.Destroy(_root.gameObject);
        _root = null;
        _group = null;
    }

    private static void Rebuild()
    {
        if (!_inRun) return;
        RunEnded();
        RunStarted();
    }

    // Размер или место поменяли в меню (в том числе из паузы) — сразу же
    public static void Rescale()
    {
        if (_root == null || _canvas == null) return;
        Place(_root, _canvas, SavedPosition);
        Restyle(Cards);
    }

    // Фон (свой или как в игре) и прозрачность — отдельно у фона и у текста со значками
    public static void Restyle(List<Card> cards)
    {
        foreach (var card in cards)
        {
            if (card.Background == null) continue;
            var source = GameStyle ? card.GameBackground : null;
            if (source != null && source.sprite != null)
            {
                card.Background.sprite = source.sprite;
                card.Background.type = source.type;
                card.Background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                var c = source.color;
                card.Background.color = new Color(c.r, c.g, c.b, BackgroundAlpha);
            }
            else
            {
                card.Background.sprite = null;
                card.Background.type = Image.Type.Simple;
                card.Background.color = new Color(Plain.r, Plain.g, Plain.b, BackgroundAlpha);
            }
            Recolor(card);
        }
    }

    private static void Recolor(Card card)
    {
        float a = TextAlpha;
        if (card.Description != null) card.Description.color = WithAlpha(card.Done ? Ui.Gold : Color.white, a);
        if (card.Progress != null) card.Progress.color = WithAlpha(Ui.Gold, a);
        if (card.Icon != null) card.Icon.color = WithAlpha(Color.white, a);
        if (card.BarBack != null) card.BarBack.color = new Color(1f, 1f, 1f, 0.15f * a);
        if (card.FillImage != null) card.FillImage.color = WithAlpha(Ui.Gold, a);
    }

    private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

    public static void Tick()
    {
        HideUnderStore();
        if (!_inRun || Time.unscaledTime < _nextTick) return;
        _nextTick = Time.unscaledTime + 0.5f;

        var manager = SingletonPersistent<LocalPlayer>.Instance?.GetChallengeManager();
        if (manager == null) return;
        if (!_built) { _built = TryBuild(manager); return; }
        if (_root == null) { RunEnded(); return; } // интерфейс забега уничтожен вместе со сценой

        foreach (var card in Cards) Update(card, manager);
    }

    private static bool TryBuild(ChallengeManager manager)
    {
        // выполненные за прошлые забеги больше не показываем
        foreach (var id in new List<ChallengeId>(Pins.All))
            if (manager.IsCompleted(id)) Pins.Remove(id);
        if (Pins.All.Count == 0) return true;

        var template = FindTemplate();
        var canvas = template != null ? RootCanvas(template.transform) : null;
        if (canvas == null)
        {
            if (!_warned) PinnedChallengesMod.Log.Warning("challenge card template not found yet, retrying");
            _warned = true;
            return false;
        }

        _canvas = canvas;
        _root = BuildBlock(canvas.transform, template, manager, Pins.All, Cards);
        _root.SetAsFirstSibling(); // меню паузы и прочие окна того же холста рисуются поверх
        _group = _root.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = _group.interactable = false;
        Place(_root, canvas, SavedPosition);
        PinnedChallengesMod.Log.Msg($"showing {Cards.Count} pinned challenge(s)");
        return true;
    }

    // На экране улучшения (StorePanel: карточки заклинаний, улучшений, артефактов) карточки
    // мешают — плавно гаснут вместе с его появлением и возвращаются, когда он закрылся
    private static void HideUnderStore()
    {
        if (_group == null) return;
        float store = 0f;
        try
        {
            var panel = Singleton<StorePanel>.Instance;
            if (panel != null && panel.isOpen) store = panel.group != null ? panel.group.alpha : 1f;
        }
        catch { }
        _group.alpha = 1f - Mathf.Clamp01(store);
    }

    // Размер и место считаем в пикселях экрана, а не в единицах холста: у холста забега и
    // холста редактора разный масштаб, а карточки должны выглядеть одинаково. Размер —
    // HudScale × (высота экрана / 1080) пикселей на единицу карточки, левый верхний угол —
    // в точке (HudX, HudY) долей экрана, и блок целиком остаётся на экране.
    public static Vector2 SavedPosition => new(PinnedChallengesMod.HudXEntry.Value, PinnedChallengesMod.HudYEntry.Value);

    public static float PixelsPerUnit => PinnedChallengesMod.HudScaleEntry.Value * Screen.height / 1080f;

    public static void Place(RectTransform block, Canvas canvas, Vector2 fraction)
    {
        float factor = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        block.localScale = Vector3.one * (PixelsPerUnit / factor);
        block.anchorMin = block.anchorMax = new Vector2(0.5f, 0.5f);
        block.pivot = new Vector2(0f, 1f);
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), TopLeft(block, fraction), camera, out var local))
            block.localPosition = new Vector3(local.x, local.y, 0f);
    }

    // Левый верхний угол на экране (в пикселях) для долей экрана — так, чтобы блок влез целиком
    public static Vector2 TopLeft(RectTransform block, Vector2 fraction)
    {
        Vector2 size = block.sizeDelta * PixelsPerUnit;
        float x = Mathf.Clamp(fraction.x * Screen.width, 0f, Mathf.Max(0f, Screen.width - size.x));
        float y = Mathf.Clamp(fraction.y * Screen.height, Mathf.Min(Screen.height, size.y), Screen.height);
        return new Vector2(x, y);
    }

    // Блок строк для списка испытаний: в забеге и в предпросмотре редактора положения
    public static RectTransform BuildBlock(Transform parent, ChallengePanel template, ChallengeManager manager,
                                           IEnumerable<ChallengeId> ids, List<Card> cards)
    {
        var root = Ui.NewRect(RootName, parent);
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = new Vector2(0f, 1f);

        var hidden = Ui.NewRect(HiddenName, root);
        hidden.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

        var configs = manager.ConfigurationForChallenges;
        float y = 0f;
        foreach (var id in ids)
        {
            if (configs == null || !configs.ContainsKey(id)) continue;
            var card = CreateCard(template, root, hidden, configs[id], y);
            cards.Add(card);
            Update(card, manager);
            y += RowHeight + Spacing;
        }
        root.sizeDelta = new Vector2(Width, Mathf.Max(RowHeight, y - Spacing));
        return root;
    }

    public static ChallengePanel FindTemplate()
    {
        var notification = Singleton<ChallengeNotification>.Instance;
        if (notification != null && notification.challengeUI != null) return notification.challengeUI;
        foreach (var n in Resources.FindObjectsOfTypeAll<ChallengeNotification>())
            if (n != null && n.gameObject.scene.name != null && n.challengeUI != null) return n.challengeUI;
        return null;
    }

    // Для предпросмотра в меню годится любая карточка испытания, хоть префаб
    public static ChallengePanel FindAnyTemplate()
    {
        var template = FindTemplate();
        if (template != null) return template;
        foreach (var panel in Resources.FindObjectsOfTypeAll<ChallengePanel>())
            if (panel != null && panel.descriptionLocalizer != null) return panel;
        return null;
    }

    // Самый верхний Canvas над объектом. GetComponentInParent<T>(true) в Il2CppInterop
    // падает с VerificationException, поэтому идём по родителям сами.
    private static Canvas RootCanvas(Transform t)
    {
        Canvas found = null;
        for (; t != null; t = t.parent)
        {
            var canvas = t.GetComponent<Canvas>();
            if (canvas != null) found = canvas;
        }
        return found;
    }

    private static Card CreateCard(ChallengePanel template, RectTransform root, RectTransform hidden, ChallengeConfiguration config, float y)
    {
        // скрытая копия карточки — источник переведённого описания
        var copy = Object.Instantiate(template.gameObject, hidden);
        copy.SetActive(true);
        foreach (var animator in copy.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        var panel = copy.GetComponent<ChallengePanel>();
        panel.Show(config);
        var gameBackground = LargestImage(copy, panel.iconImage);
        var source = panel.descriptionLocalizer != null ? panel.descriptionLocalizer.GetComponent<TMP_Text>() : null;
        source ??= copy.GetComponentInChildren<TMP_Text>(true);

        var row = Ui.NewRect($"Challenge_{config.Id}", root);
        Ui.Place(row, 0f, y, Width, RowHeight);
        var background = row.gameObject.AddComponent<Image>();
        background.raycastTarget = false;

        var icon = Ui.NewImage("Icon", row, Color.white);
        icon.sprite = config.Icon;
        icon.preserveAspect = true;
        Ui.Place(icon.rectTransform, Pad, (RowHeight - IconSize) / 2f, IconSize, IconSize);

        float textX = Pad + IconSize + 10f, textWidth = Width - textX - Pad;

        var description = Ui.CloneText(source, row, "Description");
        description.fontSize = 19f;
        description.color = Color.white;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;
        description.maxVisibleLines = 2;
        Ui.Place(description.rectTransform, textX, 5f, textWidth, RowHeight - 22f);

        var bar = Ui.NewImage("Bar", row, new Color(1f, 1f, 1f, 0.15f));
        Ui.Place(bar.rectTransform, textX, RowHeight - 11f, textWidth - 118f, 4f);
        var fill = Ui.NewImage("Fill", bar.transform, Ui.Gold).rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        var progress = Ui.CloneText(source, row, "Progress");
        progress.fontSize = 16f;
        progress.fontStyle = FontStyles.Bold;
        progress.color = Ui.Gold;
        progress.alignment = TextAlignmentOptions.Right;
        Ui.Place(progress.rectTransform, Width - Pad - 110f, RowHeight - 19f, 110f, 16f);

        var card = new Card { Id = config.Id, Config = config, Source = source, Description = description, Progress = progress,
                              Bar = bar.gameObject, Fill = fill, Background = background, Icon = icon, BarBack = bar,
                              FillImage = fill.GetComponent<Image>(), GameBackground = gameBackground };
        Restyle(new List<Card> { card });
        return card;
    }

    // Фон карточки игры — самая большая картинка со спрайтом (кроме иконки испытания)
    private static Image LargestImage(GameObject root, Image exclude)
    {
        Image best = null;
        float bestArea = 0f;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image == null || image.sprite == null) continue;
            if (exclude != null && (image.Pointer == exclude.Pointer || image.transform.IsChildOf(exclude.transform))) continue;
            var size = image.rectTransform.rect.size;
            float area = size.x * size.y;
            if (area > bestArea) { bestArea = area; best = image; }
        }
        return best;
    }

    public static void Update(Card card, ChallengeManager manager)
    {
        if (card.Description == null) return;

        string text = card.Source != null ? card.Source.text : "";
        if (card.Description.text != text) card.Description.text = text; // язык мог смениться

        bool done = manager.IsCompleted(card.Id), binary = card.Config.IsBinaryChallenge();
        var progress = manager.ProgressForChallenges;
        int value = progress != null && progress.ContainsKey(card.Id) ? progress[card.Id].Value : 0;
        int target = Math.Max(1, card.Config.TargetValue);
        float fraction = done ? 1f : Mathf.Clamp01((float)value / target);

        card.Bar.SetActive(!binary || done);
        card.Fill.anchorMax = new Vector2(fraction, 1f);
        if (card.Done != done) { card.Done = done; Recolor(card); }
        string counter = done || binary ? "" : $"{Format(Math.Min(value, target))} / {Format(target)}";
        if (card.Progress.text != counter) card.Progress.text = counter;
    }

    private static string Format(int n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
