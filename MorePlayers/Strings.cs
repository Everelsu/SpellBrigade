using System;
using System.Collections.Generic;
using Il2CppTMPro;
using SpellBrigade.Shared;

namespace MorePlayers;

// Тексты мода на всех языках игры. Язык берётся из настроек игры и меняется на лету;
// если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Players = "players", Room = "room", MaxPlayers = "max", You = "you";

    private static readonly string[] Keys = { Players, Room, MaxPlayers, You };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Players", "Room", "Max players", "you" },
        ["ru"] = new[] { "Игроки", "Комната", "Макс. игроков", "вы" },
        ["uk"] = new[] { "Гравці", "Кімната", "Макс. гравців", "ви" },
        ["de"] = new[] { "Spieler", "Raum", "Max. Spieler", "du" },
        ["fr"] = new[] { "Joueurs", "Salle", "Joueurs max", "vous" },
        ["it"] = new[] { "Giocatori", "Stanza", "Giocatori max", "tu" },
        ["nl"] = new[] { "Spelers", "Kamer", "Max. spelers", "jij" },
        ["pl"] = new[] { "Gracze", "Pokój", "Maks. graczy", "ty" },
        ["pt-br"] = new[] { "Jogadores", "Sala", "Máx. de jogadores", "você" },
        ["pt"] = new[] { "Jogadores", "Sala", "Máx. de jogadores", "tu" },
        ["es"] = new[] { "Jugadores", "Sala", "Máx. de jugadores", "tú" },
        ["ja"] = new[] { "プレイヤー", "ルーム", "最大人数", "あなた" },
        ["ko"] = new[] { "플레이어", "방", "최대 인원", "나" },
        ["zh-hans"] = new[] { "玩家", "房间", "最大人数", "你" },
        ["zh-hant"] = new[] { "玩家", "房間", "最大人數", "你" },
        ["th"] = new[] { "ผู้เล่น", "ห้อง", "ผู้เล่นสูงสุด", "คุณ" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => MorePlayersMod.Log);

    public static string Get(string key) => Text.Get(key);

    // шрифт, по которому проверяем, есть ли в нём символы языка
    public static void UseFont(TMP_Text sample) => Text.UseFont(sample);
}
