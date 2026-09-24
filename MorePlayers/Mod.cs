using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

[assembly: MelonInfo(typeof(MorePlayers.MorePlayersMod), "More Players", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

namespace MorePlayers;

public class MorePlayersMod : MelonMod
{
    public static MelonLogger.Instance Log;

    public const int MinPlayers = 4, HardCap = 16; // лобби Steam/EOS создаются с запасом на HardCap

    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<int> _maxEntry;
    public static int MaxPlayers => Math.Clamp(_maxEntry?.Value ?? 8, MinPlayers, HardCap);

    private static readonly List<CodePatch> Patches = new();
    private float _nextColorCheck;
    private bool _listFailed;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _category = MelonPreferences.CreateCategory("MorePlayers", "More Players");
        _maxEntry = _category.CreateEntry("MaxPlayers", 8, "Max Players",
            $"Сколько игроков пускает хост ({MinPlayers}–{HardCap}). Мод должен стоять у всех игроков",
            validator: new ValueRange<int>(MinPlayers, HardCap));
        _category.SaveToFile(false);
        _maxEntry.OnEntryValueChanged.Subscribe((_, _) => ApplyLimit());

        // Лимит подключений: хост сравнивает число подключённых с 4
        Add("server full check", "Il2Cpp.NetworkStateMachine", "IsServerFull", CodePatch.CmpRegWith4, () => MaxPlayers);
        Add("connection status", "Il2Cpp.HostState", "GetConnectionStatus", CodePatch.CmpRegWith4, () => MaxPlayers);
        Add("connection approval", "Il2Cpp.HostState", "OnApprovalCheck", CodePatch.CmpRegWith4, () => MaxPlayers);
        // Лобби Steam и EOS: создаются с запасом, реальный лимит держит проверка выше
        Add("Steam lobby size", "Il2Cpp.SteamLobbyService+_InitializeAsHost_d__3", "MoveNext", CodePatch.MovWith4BeforeCall, () => HardCap);
        Add("EOS lobby size", "Il2Cpp.EOSLobbyService+_InitializeAsHost_d__24", "MoveNext",
            (i, n) => CodePatch.MovWith4(i, n) && i.Op0Kind == Iced.Intel.OpKind.Memory, () => HardCap);
        Add("EOS lobby options", "Il2Cpp.EOSLobbyService", "CreateLobbyOptions",
            (i, n) => CodePatch.MovWith4(i, n) && i.Op0Kind == Iced.Intel.OpKind.Memory, () => HardCap);
        // Сколько мест свободно в найденном лобби («4 − участников»)
        Add("lobby search slots", "Il2Cpp.EOSLobbySearcher+_GetLobbies_d__8", "MoveNext",
            (i, n) => CodePatch.MovWith4(i, n) && i.Op0Kind == Iced.Intel.OpKind.Register, () => MaxPlayers);
        Add("free slots", "Il2Cpp.EOSLobbySearcher", "GetSlotsAvailable",
            (i, n) => CodePatch.MovWith4(i, n) && i.Op0Kind == Iced.Intel.OpKind.Register, () => MaxPlayers);

        // Захват точки: множитель скорости знает только 1–4 игроков в зоне, для 5+ он 0 и захват
        // вставал. «Ровно 4» → «4 и больше» (одна инструкция перехода)
        Patches.Add(new CodePatch { Name = "capture speed", Type = "Il2Cpp.CapturePoint", Method = "IncreaseCaptureTime",
            Match = CodePatch.CmpRegWith1, NextOpcode = 0x7D, Value = () => 0x7D });
        Patches.Add(new CodePatch { Name = "capture speed (helper)", Type = "Il2Cpp.CapturePoint", Method = "GetPlayersInRangeSpeedMultiplier",
            Match = CodePatch.CmpRegWith1, NextOpcode = 0x7D, Value = () => 0x7D });

        // Радиус зон захвата и жертвенника рос только до 4 игроков — снимаем верхнюю границу
        // (jbe → jmp), дальше растёт с тем же шагом. Компилятор встроил расчёт в несколько мест
        foreach (var (type, method) in new[]
        {
            ("Il2Cpp.CapturePoint", "GetRadiusForNumberOfPlayers"), ("Il2Cpp.CapturePoint", "OnActiveVariantIndexChanged"),
            ("Il2Cpp.SacrificePoint", "GetRadiusForNumberOfPlayers"), ("Il2Cpp.SacrificePoint", "OnNetworkSpawn"),
        })
            Patches.Add(new CodePatch { Name = "zone radius", Type = type, Method = method,
                Match = CodePatch.RadiusClamp(), NextOpcode = 0xEB, Value = () => 0xEB });

        int found = 0;
        foreach (var p in Patches)
        {
            if (p.Locate()) { p.Apply(); found++; }
            else Log.Warning($"not found: {p.Name} ({p.Type}.{p.Method}) — this part of the limit stays at 4");
        }

        // Лобби на 4 места: каждый видит свою «комнату», полный список — по Tab
        int hooks = 0;
        hooks += Hook(typeof(PlayerSeatUpdater), nameof(PlayerSeatUpdater.UpdateSeats), typeof(Rooms), nameof(Rooms.UpdateSeatsPrefix));
        hooks += Hook(typeof(PlayerSeatUpdater), nameof(PlayerSeatUpdater.UpdateSeatsWithData), typeof(Rooms), nameof(Rooms.UpdateSeatsWithDataPrefix));
        hooks += Hook(typeof(MainMenuCameraController), nameof(MainMenuCameraController.SetRunOverviewViewPoint), typeof(Rooms), nameof(Rooms.SetRunOverviewViewPointPrefix));
        // В забеге: точки появления и таблица целей «загнать существ»
        int spawn = Hook(typeof(LevelStarter), nameof(LevelStarter.GameplayInitializationProcess), typeof(InRun), nameof(InRun.GameplayInitializationPrefix))
                  + Hook(typeof(LevelStarter), nameof(LevelStarter.OnNetworkSpawn), typeof(InRun), nameof(InRun.GameplayInitializationPrefix));
        int herd = Hook(typeof(HerdingObjectiveManager), nameof(HerdingObjectiveManager.Awake), typeof(InRun), nameof(InRun.HerdTargetsPrefix))
                 + Hook(typeof(HerdingObjectiveManager), nameof(HerdingObjectiveManager.OnNetworkSpawn), typeof(InRun), nameof(InRun.HerdTargetsPrefix));
        if (spawn == 0) Log.Warning("spawn points not hooked — runs with 5+ players will fail to start");
        if (herd == 0) Log.Warning("herding objective not hooked — it will fail with 5+ players");
        hooks += Math.Min(spawn, 1) + Math.Min(herd, 1);
        // Интерфейс на 4 игроков: командная статистика в итогах и портреты отряда
        hooks += Hook(typeof(TeamStatsPanel), nameof(TeamStatsPanel.SetValues), typeof(RunUi), nameof(RunUi.TeamStatsPrefix), nameof(RunUi.TeamStatsPostfix));
        hooks += Math.Min(1,
            Hook(typeof(GameplayPartyOverviewUI), nameof(GameplayPartyOverviewUI.AddPlayerOverview), typeof(RunUi), null, nameof(RunUi.PartyChangedPostfix))
          + Hook(typeof(GameplayPartyOverviewUI), nameof(GameplayPartyOverviewUI.RemovePlayerOverview), typeof(RunUi), null, nameof(RunUi.PartyChangedPostfix)));

        Log.Msg($"{found}/{Patches.Count} code patches, {hooks}/7 hooks — up to {MaxPlayers} players (everyone needs the mod)");
    }

    private int Hook(Type type, string method, Type patchClass, string prefix, string postfix = null)
    {
        var target = HarmonyLib.AccessTools.Method(type, method);
        if (target == null || SharedCodeGuard.FindMethodsSharingCode(target).Count > 0)
        {
            Log.Warning($"skipped hook {type.Name}.{method} (shared code)");
            return 0;
        }
        HarmonyInstance.Patch(target,
            prefix: prefix != null ? new HarmonyLib.HarmonyMethod(patchClass, prefix) : null,
            postfix: postfix != null ? new HarmonyLib.HarmonyMethod(patchClass, postfix) : null);
        return 1;
    }

    private static void Add(string name, string type, string method, Func<Iced.Intel.Instruction, Iced.Intel.Instruction, bool> match, Func<int> value) =>
        Patches.Add(new CodePatch { Name = name, Type = type, Method = method, Match = match, Value = value });

    private static void ApplyLimit()
    {
        foreach (var p in Patches) p.Apply();
        Log.Msg($"max players: {MaxPlayers}");
    }

    internal static void SetMaxPlayers(int value)
    {
        value = Math.Clamp(value, MinPlayers, HardCap);
        if (_maxEntry.Value == value) return;
        _maxEntry.Value = value;
        _category.SaveToFile(false);
    }

    public override void OnUpdate()
    {
        try { RunUi.Tick(); }
        catch (Exception e) { Log.Warning($"team stats: {e.Message}"); }
        try { PlayerList.Tick(); }
        catch (Exception e) { if (!_listFailed) { _listFailed = true; Log.Warning($"player list: {e}"); } }

        if (Time.unscaledTime < _nextColorCheck) return;
        _nextColorCheck = Time.unscaledTime + 1f;
        try { PlayerColors.Ensure(); }
        catch (Exception e) { Log.Warning($"player colors: {e.Message}"); }
    }
}
