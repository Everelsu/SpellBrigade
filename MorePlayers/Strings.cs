using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

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

    private static string[] _current;
    private static readonly Dictionary<string, bool> FontSupport = new();
    private static bool _subscribed;
    private static TMP_Text _fontSample;

    public static string Get(string key)
    {
        _current ??= Resolve();
        int i = Array.IndexOf(Keys, key);
        return i >= 0 ? _current[i] : key;
    }

    // шрифт, по которому проверяем, есть ли в нём символы языка
    public static void UseFont(TMP_Text sample)
    {
        _fontSample = sample;
        _current = null;
        if (_subscribed) return;
        _subscribed = true;
        try
        {
            LocalizationSettings.add_SelectedLocaleChanged((Il2CppSystem.Action<Locale>)(Action<Locale>)(_ => _current = null));
        }
        catch (Exception e) { MorePlayersMod.Log.Warning($"[lang] can't follow language changes: {e.Message}"); }
    }

    private static string[] Resolve()
    {
        string code = "en";
        try { code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en"; } catch { }
        string key = TableKey(code);
        if (key != "en" && _fontSample != null && !FontHasAll(_fontSample, key)) key = "en";
        return Table[key];
    }

    private static string TableKey(string code)
    {
        code = code.ToLowerInvariant().Replace('_', '-');
        if (code.StartsWith("zh"))
            return code.Contains("hant") || code.Contains("tw") || code.Contains("hk") ? "zh-hant" : "zh-hans";
        if (code.StartsWith("pt")) return code.Contains("br") ? "pt-br" : "pt";
        string lang = code.Split('-')[0];
        return Table.ContainsKey(lang) ? lang : "en";
    }

    private static bool FontHasAll(TMP_Text sample, string key)
    {
        if (FontSupport.TryGetValue(key, out bool ok)) return ok;
        ok = true;
        try
        {
            var font = sample.font;
            if (font != null)
                foreach (var s in Table[key])
                {
                    foreach (char c in s)
                        if (c > 127 && !char.IsWhiteSpace(c) && !font.HasCharacter(c, true, true)) { ok = false; break; }
                    if (!ok) break;
                }
        }
        catch { ok = true; }
        FontSupport[key] = ok;
        return ok;
    }
}
