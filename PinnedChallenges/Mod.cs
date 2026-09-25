using System;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Preferences;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(PinnedChallenges.PinnedChallengesMod), "Pinned Challenges", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]
[assembly: MelonOptionalDependencies("ModMenu")] // без Mod Menu мод работает как раньше

namespace PinnedChallenges;

public class PinnedChallengesMod : MelonMod
{
    public static MelonLogger.Instance Log;

    // UserData/MelonPreferences.cfg, секция [PinnedChallenges]
    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<string> PinnedEntry;
    internal static MelonPreferences_Entry<float> HudScaleEntry, HudXEntry, HudYEntry, OpacityEntry, TextOpacityEntry;
    internal static MelonPreferences_Entry<int> MaxPinsEntry;
    internal static MelonPreferences_Entry<bool> GameStyleEntry;

    // Справа вверху, вплотную к верхнему краю, левее кнопки настроек (выбрано на глаз в игре)
    internal const float DefaultX = 0.7354167f, DefaultY = 0.9861111f;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _category = MelonPreferences.CreateCategory("PinnedChallenges", "Pinned Challenges");
        PinnedEntry = _category.CreateEntry("Pinned", "", "Pinned",
            "Закреплённые испытания через запятую (ChallengeId). Удобнее закреплять кликом в меню испытаний");
        HudScaleEntry = _category.CreateEntry("HudScale", 1f, "HUD Scale",
            "Размер строк с испытаниями в забеге", validator: new ValueRange<float>(0.3f, 2f));
        // Левый верхний угол блока в долях экрана; по умолчанию — справа сверху, под кнопкой настроек.
        // Удобнее двигать мышью: Mod Menu → Pinned Challenges → «Положение карточек»
        OpacityEntry = _category.CreateEntry("Opacity", 0.6f, "Card Background", "Непрозрачность фона карточек (0 — без фона, 1 — сплошной)",
            validator: new ValueRange<float>(0f, 1f));
        TextOpacityEntry = _category.CreateEntry("TextOpacity", 1f, "Text and Icons", "Непрозрачность текста, значков и полоски прогресса",
            validator: new ValueRange<float>(0.1f, 1f));
        GameStyleEntry = _category.CreateEntry("GameStyle", true, "Game-style Background",
            "Фон карточек как у уведомлений игры (false — простая тёмная плашка)");
        MaxPinsEntry = _category.CreateEntry("MaxPinned", 3, "Max Pinned", "Сколько испытаний можно закрепить (1–5)",
            validator: new ValueRange<int>(1, 5));
        HudXEntry = _category.CreateEntry("HudX", DefaultX, "HUD X", "Левый край карточек, доля ширины экрана (0 — слева, 1 — справа)",
            validator: new ValueRange<float>(0f, 1f));
        HudYEntry = _category.CreateEntry("HudY", DefaultY, "HUD Y", "Верх карточек, доля высоты экрана (0 — снизу, 1 — сверху)",
            validator: new ValueRange<float>(0f, 1f));
        _category.SaveToFile(false);
        Pins.Load();

        // Меню испытаний: клик по карточке закрепляет её
        bool menu = Hooks.Patch(HarmonyInstance, Log, typeof(ChallengeProgressPanel), nameof(ChallengeProgressPanel.Show),
                                typeof(ChallengeMenu), postfix: nameof(ChallengeMenu.ShowPostfix));
        // Забег: трекеры испытаний живут ровно столько, сколько идёт забег
        bool start = Hooks.Patch(HarmonyInstance, Log, typeof(GameplayChallengeTrackerController), "Start",
                                 typeof(RunHud), postfix: nameof(RunHud.RunStarted));
        bool end = Hooks.Patch(HarmonyInstance, Log, typeof(GameplayChallengeTrackerController), "OnDestroy",
                               typeof(RunHud), prefix: nameof(RunHud.RunEnded));

        if (menu && start && end) Log.Msg($"ready — {Pins.All.Count} pinned. Click a challenge card to pin it");
        else Log.Warning($"partially loaded: pinning {(menu ? "ok" : "unavailable")}, in-run cards {(start && end ? "ok" : "unavailable")}");

        if (FindMelon("Mod Menu", "Relsev") != null)
            try { MenuPage.Register(); }
            catch (Exception e) { Log.Warning($"Mod Menu page: {e.Message}"); }
    }

    internal static void Save() => _category.SaveToFile(false);

    public override void OnUpdate()
    {
        WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты
        try { RunHud.Tick(); }
        catch (Exception e) { Log.Error($"in-run cards: {e}"); RunHud.RunEnded(); }
        try { LayoutEditor.Tick(); }
        catch (Exception e) { Log.Error($"position editor: {e}"); }
    }
}
