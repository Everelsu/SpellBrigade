using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Unity.Netcode;

namespace MorePlayers;

// В лобби только 4 места. Если игроков больше, делим всех на «комнаты» по ≤4 человека
// (поровну и по порядку входа: 5 → 3+2, 8 → 4+4, 9 → 3+3+3). Каждый видит на местах
// свою комнату — сам он там всегда есть, — а полный состав открывается списком (PlayerList).
internal static class Rooms
{
    public const int Seats = 4;
    private static bool _inner; // повторный вызов метода с уже отфильтрованным списком

    public static int Count(int players) => Math.Max(1, (players + Seats - 1) / Seats);
    public static int Of(int index, int players) => players <= Seats ? 0 : index * Count(players) / players;

    public static ulong LocalClientId
    {
        get
        {
            var net = NetworkManager.Singleton;
            return net != null ? net.LocalClientId : ulong.MaxValue;
        }
    }

    public static int LocalIndex(List<MainMenuPlayerData> players)
    {
        ulong me = LocalClientId;
        for (int i = 0; players != null && i < players.Count; i++)
            if (players[i].ClientId == me) return i;
        return 0;
    }

    public static int LocalRoomSize(int players, int localIndex)
    {
        int room = Of(localIndex, players), size = 0;
        for (int i = 0; i < players; i++) if (Of(i, players) == room) size++;
        return size;
    }

    // --- хуки PlayerSeatUpdater / MainMenuCameraController ---

    // Лобби: места получают первые 4 игрока из списка — подсовываем только свою комнату
    public static bool UpdateSeatsPrefix(PlayerSeatUpdater __instance, List<MainMenuPlayerData> playerData)
    {
        if (_inner || playerData == null || playerData.Count <= Seats) return true;
        try
        {
            int n = playerData.Count, room = Of(LocalIndex(playerData), n);
            var mine = new List<MainMenuPlayerData>();
            for (int i = 0; i < n; i++)
                if (Of(i, n) == room) mine.Add(playerData[i]);
            _inner = true;
            __instance.UpdateSeats(mine);
            return false;
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"rooms: {e.Message}"); return true; }
        finally { _inner = false; }
    }

    // Итоги забега на местах лобби: то же самое, но по списку результатов
    public static bool UpdateSeatsWithDataPrefix(PlayerSeatUpdater __instance, List<MainMenuPlayerData> mainMenuPlayerData, IReadOnlyList<PlayerData> playerData)
    {
        if (_inner || playerData == null) return true;
        try
        {
            var all = playerData.Cast<IReadOnlyCollection<PlayerData>>();
            int n = all.Count;
            if (n <= Seats) return true;

            ulong me = LocalClientId;
            int local = 0;
            for (int i = 0; i < n; i++)
                if (playerData[i]?.PlayerIdentification?.ClientId == me) { local = i; break; }

            int room = Of(local, n);
            var mine = new List<PlayerData>();
            for (int i = 0; i < n; i++)
                if (Of(i, n) == room) mine.Add(playerData[i]);
            _inner = true;
            __instance.UpdateSeatsWithData(mainMenuPlayerData, mine.Cast<IReadOnlyList<PlayerData>>());
            return false;
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"rooms (results): {e.Message}"); return true; }
        finally { _inner = false; }
    }

    // Камера итогов берёт точку обзора по числу игроков — их всего 4, для 5+ игра падала бы
    public static bool SetRunOverviewViewPointPrefix(MainMenuCameraController __instance, int playerCount)
    {
        if (_inner || playerCount <= Seats) return true;
        try
        {
            int local = 0;
            var manager = NetworkSingleton<MainMenuManager>.Instance;
            if (manager != null) local = Math.Min(LocalIndex(manager.PlayerData), playerCount - 1);
            int size = LocalRoomSize(playerCount, local);
            int cameras = __instance.runOverviewCameras?.Length ?? Seats;
            _inner = true;
            __instance.SetRunOverviewViewPoint(Math.Clamp(size, 1, Math.Max(1, cameras)));
            return false;
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"rooms (camera): {e.Message}"); return true; }
        finally { _inner = false; }
    }
}
