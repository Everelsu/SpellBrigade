using System;
using System.Collections.Generic;
using Il2CppTMPro;
using SpellBrigade.Shared;

namespace MorePlayers;

// Тексты мода на всех языках игры. Язык берётся из настроек игры и меняется на лету;
// если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Players = "players", Room = "room", MaxPlayers = "max", You = "you",
                        Matchmaking = "mm";

    private static readonly string[] Keys = { Players, Room, MaxPlayers, You, Matchmaking };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Players", "Room", "Max players", "you", "the player limit is {0}: players without the mod can't join lobbies bigger than 4. Set it to 4 to use matchmaking." },
        ["ru"] = new[] { "Игроки", "Комната", "Макс. игроков", "вы", "лимит игроков {0}: игроки без мода не могут быть в лобби больше чем на 4. Поставьте 4, чтобы включить подбор." },
        ["uk"] = new[] { "Гравці", "Кімната", "Макс. гравців", "ви", "ліміт гравців {0}: гравці без мода не можуть бути в лобі більше ніж на 4. Поставте 4, щоб увімкнути підбір." },
        ["de"] = new[] { "Spieler", "Raum", "Max. Spieler", "du", "Spielerlimit ist {0}: Spieler ohne die Mod können keinen Lobbys über 4 beitreten. Stelle es auf 4, um Matchmaking zu nutzen." },
        ["fr"] = new[] { "Joueurs", "Salle", "Joueurs max", "vous", "la limite de joueurs est {0} : sans le mod, on ne peut pas rejoindre un salon de plus de 4. Mettez-la à 4 pour utiliser le matchmaking." },
        ["it"] = new[] { "Giocatori", "Stanza", "Giocatori max", "tu", "il limite di giocatori è {0}: chi non ha la mod non può entrare in lobby oltre 4. Impostalo a 4 per usare il matchmaking." },
        ["nl"] = new[] { "Spelers", "Kamer", "Max. spelers", "jij", "spelerslimiet is {0}: spelers zonder de mod kunnen niet in lobby's groter dan 4. Zet hem op 4 om matchmaking te gebruiken." },
        ["pl"] = new[] { "Gracze", "Pokój", "Maks. graczy", "ty", "limit graczy to {0}: gracze bez moda nie mogą być w lobby większym niż 4. Ustaw 4, aby używać dobierania." },
        ["pt-br"] = new[] { "Jogadores", "Sala", "Máx. de jogadores", "você", "o limite de jogadores é {0}: quem não tem o mod não entra em lobbies com mais de 4. Coloque 4 para usar o matchmaking." },
        ["pt"] = new[] { "Jogadores", "Sala", "Máx. de jogadores", "tu", "o limite de jogadores é {0}: quem não tem o mod não entra em lobbies com mais de 4. Põe 4 para usar o matchmaking." },
        ["es"] = new[] { "Jugadores", "Sala", "Máx. de jugadores", "tú", "el límite de jugadores es {0}: quien no tenga el mod no puede estar en salas de más de 4. Ponlo en 4 para usar el emparejamiento." },
        ["es-mx"] = new[] { "Jugadores", "Sala", "Máx. de jugadores", "tú", "el límite de jugadores es {0}: quien no tenga el mod no puede estar en salas de más de 4. Ponlo en 4 para usar el emparejamiento." },
        ["ja"] = new[] { "プレイヤー", "ルーム", "最大人数", "あなた", "プレイヤー上限が{0}です：MODのないプレイヤーは5人以上のロビーに入れません。マッチングを使うには4にしてください。" },
        ["ko"] = new[] { "플레이어", "방", "최대 인원", "나", "플레이어 제한이 {0}입니다: 모드가 없는 플레이어는 4명을 넘는 로비에 들어갈 수 없습니다. 매칭을 쓰려면 4로 설정하세요." },
        ["zh-hans"] = new[] { "玩家", "房间", "最大人数", "你", "玩家上限为 {0}：没有模组的玩家无法加入超过 4 人的大厅。设为 4 才能使用匹配。" },
        ["zh-hant"] = new[] { "玩家", "房間", "最大人數", "你", "玩家上限為 {0}：沒有模組的玩家無法加入超過 4 人的大廳。設為 4 才能使用配對。" },
        ["th"] = new[] { "ผู้เล่น", "ห้อง", "ผู้เล่นสูงสุด", "คุณ", "จำกัดผู้เล่นไว้ที่ {0}: ผู้เล่นที่ไม่มีม็อดเข้าล็อบบี้ที่เกิน 4 คนไม่ได้ ตั้งเป็น 4 เพื่อใช้การจับคู่" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => MorePlayersMod.Log);

    public static string Get(string key) => Text.Get(key);

    // шрифт, по которому проверяем, есть ли в нём символы языка
    public static void UseFont(TMP_Text sample) => Text.UseFont(sample);
}
