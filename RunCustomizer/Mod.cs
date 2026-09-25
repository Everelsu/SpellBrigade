using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Preferences;
using SpellBrigade.Shared;
using UnityEngine;

[assembly: MelonInfo(typeof(RunCustomizer.RunCustomizerMod), "Run Customizer", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]
[assembly: MelonOptionalDependencies("ModMenu")] // без Mod Menu мод работает как раньше

namespace RunCustomizer;

public class RunCustomizerMod : MelonMod
{
    public static MelonLogger.Instance Log;

    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<int> ModeEntry;
    internal static MelonPreferences_Entry<float> CustomEnemy, CustomSpawnSpeed, CustomEnemyCount, CustomHealth;
    internal static MelonPreferences_Entry<bool> RandomBaseOnly;

    private int _hooksInstalled, _hooksTotal;
    private float _nextLobbySync;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        _category = MelonPreferences.CreateCategory("RunCustomizer", "Run Customizer");
        ModeEntry = _category.CreateEntry("DifficultyMode", 0, "Difficulty Mode",
            "0 = обычная (выбор игры), 1 = случайная, 2 = своя, 3 = случайная из базовых", validator: new ValueRange<int>(0, 3));
        CustomEnemy = _category.CreateEntry("CustomEnemyStrength", 2.05f, "Custom: Enemy Strength",
            "Своя сложность: здоровье и урон врагов (Normal 1.1, Hard 2.05, Nightmare 3)", validator: new ValueRange<float>(0.1f, 10f));
        CustomSpawnSpeed = _category.CreateEntry("CustomSpawnSpeed", 1.43f, "Custom: Spawn Speed",
            "Своя сложность: скорость появления врагов (Normal 1, Hard 1.43, Nightmare 2.5)", validator: new ValueRange<float>(0.1f, 10f));
        CustomEnemyCount = _category.CreateEntry("CustomEnemyCount", 1.25f, "Custom: Enemy Count",
            "Своя сложность: минимум врагов на карте (Normal 1, Hard 1.25, Nightmare 1.5)", validator: new ValueRange<float>(0.1f, 10f));
        CustomHealth = _category.CreateEntry("CustomHealthDrops", 0.5f, "Custom: Health Drops",
            "Своя сложность: шанс аптечек (Normal 1, Hard 0.5, Nightmare 0.25)", validator: new ValueRange<float>(0f, 10f));
        RandomBaseOnly = _category.CreateEntry("RandomBaseOnly", false, "Random: Base Difficulty Only",
            "Случайная сложность: false — случайно каждый параметр, true — случайно одна из Normal/Hard/Nightmare");
        _category.SaveToFile(false);

        ApplyPatches();
        Log.Msg($"{_hooksInstalled}/{_hooksTotal} hooks — difficulty mode: {Modes.Selected}");

        if (FindMelon("Mod Menu", "Relsev") != null)
            try { MenuPage.Register(); }
            catch (Exception e) { Log.Warning($"Mod Menu page: {e.Message}"); }
    }

    internal static void Save() => _category.SaveToFile(false);

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (sceneName is "MainMenu" or "StartMenu") Modes.OnBackToMenu();
    }

    public override void OnUpdate()
    {
        WheelSelect.Tick(); // колесо мыши над «< значение >» листает варианты
        if (Time.unscaledTime < _nextLobbySync) return;
        _nextLobbySync = Time.unscaledTime + 0.5f;
        try { Modes.SyncLobby(); }
        catch (Exception e) { Log.Error($"lobby sync: {e.Message}"); }
    }

    private void ApplyPatches()
    {
        // Геймплей (действует только у хоста в забеге со своим режимом)
        Patch(Method(typeof(EnemyDifficultyParameters), "GetMultiplierForDifficulty"), postfix: nameof(DifficultyHooks.EnemyMultiplierPostfix));
        Patch(Method(typeof(EnemyDifficultyParameters), "GetExtraDifficultyMultiplierForCycle"), postfix: nameof(DifficultyHooks.CycleMultiplierPostfix));
        Patch(AccessTools.PropertyGetter(typeof(WaveConfiguration), "SpawnIntervalInSecondsForDifficultyAndCycle"), postfix: nameof(DifficultyHooks.SpawnIntervalPostfix));
        Patch(AccessTools.PropertyGetter(typeof(WaveConfiguration), "MinNumberOfConcurrentEnemiesForDifficulty"), postfix: nameof(DifficultyHooks.MinEnemiesPostfix));
        Patch(Method(typeof(PickupSpawner), "DeterminePickUp"), prefix: nameof(DifficultyHooks.DeterminePickUpPrefix));
        Patch(Method(typeof(RunGoldCalculatorData), "GetGoldMultiplierForDifficulty"), postfix: nameof(DifficultyHooks.GoldMultiplierPostfix));
        Patch(Method(typeof(MainMenuManager), "PrepareForLevelStart"), postfix: nameof(OnLevelStartPostfix), patchClass: typeof(RunCustomizerMod));

        // Окно выбора сложности, значки в лобби и в забеге
        Patch(Method(typeof(DifficultiesPanel), "InitializeButtons"), postfix: nameof(DifficultyUi.InitializeButtonsPostfix), patchClass: typeof(DifficultyUi));
        Patch(Method(typeof(DifficultiesPanel), "Show"), postfix: nameof(DifficultyUi.ShowPostfix), patchClass: typeof(DifficultyUi));
        Patch(Method(typeof(DifficultiesPanel), "ShowDifficultyDetails"), postfix: nameof(DifficultyUi.ShowDifficultyDetailsPostfix), patchClass: typeof(DifficultyUi));
        Patch(Method(typeof(DifficultySelectionDisplayer), "DisplayDifficulty"), postfix: nameof(DifficultyUi.LobbyIconPostfix), patchClass: typeof(DifficultyUi));
        Patch(Method(typeof(DifficultyLevelVisualizer), "Start"), postfix: nameof(DifficultyUi.RunIconPostfix), patchClass: typeof(DifficultyUi));

        // Статистика забега: пауза и экран итогов
        Patch(Method(typeof(MissionStatsPanel), "Show"), postfix: nameof(StatsUi.MissionStatsPostfix), patchClass: typeof(StatsUi));
        Patch(Method(typeof(GameOverMissionStatsPanel), "SetValues"), postfix: nameof(StatsUi.GameOverStatsPostfix), patchClass: typeof(StatsUi));

        // Случайный волшебник
        Patch(Method(typeof(CharacterSelector), "InitializeCharacterButtons"), postfix: nameof(RandomWizard.InitializePostfix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(CharacterPanel), "OnSelected"), prefix: nameof(RandomWizard.PanelSelectedPrefix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(CharacterPanel), "OnHighlightStarted"), prefix: nameof(RandomWizard.PanelHighlightPrefix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(CharacterSelector), "MakeInitialSelection"), postfix: nameof(RandomWizard.InitialSelectionPostfix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(CharacterPanel), "Display"), postfix: nameof(RandomWizard.DisplayPostfix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(MainMenuManager), "StartLevelOnServerRpc"), prefix: nameof(RandomWizard.StartLevelPrefix), patchClass: typeof(RandomWizard));
        Patch(Method(typeof(MainMenuManager), "PrepareForLevelStart"), prefix: nameof(RandomWizard.PrepareForLevelStartPrefix), patchClass: typeof(RandomWizard));
    }

    private static void OnLevelStartPostfix()
    {
        try { Modes.OnLevelStart(); }
        catch (Exception e) { Log.Error($"level start: {e}"); }
    }

    private static MethodInfo Method(Type type, string name) => AccessTools.Method(type, name);

    private void Patch(MethodInfo original, string prefix = null, string postfix = null, Type patchClass = null)
    {
        _hooksTotal++;
        if (Hooks.Patch(HarmonyInstance, Log, original, patchClass ?? typeof(DifficultyHooks), prefix, postfix)) _hooksInstalled++;
    }
}
