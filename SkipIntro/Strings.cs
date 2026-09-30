using System.Collections.Generic;
using SpellBrigade.Shared;

namespace SkipIntro;

// Подпись в Mod Menu на всех языках игры
internal static class Strings
{
    public const string Enabled = "enabled";

    private static readonly string[] Keys = { Enabled };

    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Skip logos and intro videos" },
        ["ru"] = new[] { "Пропускать логотипы и вступительные ролики" },
        ["uk"] = new[] { "Пропускати логотипи й вступні ролики" },
        ["de"] = new[] { "Logos und Intro-Videos überspringen" },
        ["fr"] = new[] { "Passer les logos et les vidéos d'intro" },
        ["it"] = new[] { "Salta loghi e video introduttivi" },
        ["nl"] = new[] { "Logo's en introvideo's overslaan" },
        ["pl"] = new[] { "Pomijaj loga i filmy wprowadzające" },
        ["pt-br"] = new[] { "Pular logos e vídeos de abertura" },
        ["pt"] = new[] { "Saltar logótipos e vídeos de introdução" },
        ["es"] = new[] { "Saltar logotipos y vídeos de introducción" },
        ["es-mx"] = new[] { "Omitir logotipos y videos de introducción" },
        ["ja"] = new[] { "ロゴとオープニングムービーをスキップ" },
        ["ko"] = new[] { "로고와 인트로 영상 건너뛰기" },
        ["zh-hans"] = new[] { "跳过标志和开场视频" },
        ["zh-hant"] = new[] { "跳過標誌和開場影片" },
        ["th"] = new[] { "ข้ามโลโก้และวิดีโอเปิดเกม" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => SkipIntroMod.Log);

    public static string Get(string key) => Text.Get(key);
}
