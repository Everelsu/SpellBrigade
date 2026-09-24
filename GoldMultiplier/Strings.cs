using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace GoldXpMultiplier;

// Тексты мода на всех языках игры. Язык берётся из настроек игры и меняется
// на лету вместе с ней; если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Tab = "tab", Enabled = "enabled", On = "on", Off = "off", Gold = "gold",
                        Xp = "xp", RankXp = "rank", Hint = "hint", Bonus = "bonus";

    private static readonly string[] Keys = { Tab, Enabled, On, Off, Gold, Xp, RankXp, Hint, Bonus };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Multipliers", "Mod Enabled", "On", "Off", "Gold per Run", "Run XP", "Wizard Rank XP",
            "Changes apply immediately. x1 = vanilla.\nIn co-op, run XP works when the host has the mod.", "Mod multiplier" },
        ["ru"] = new[] { "Множители", "Мод включён", "Вкл", "Выкл", "Золото за забег", "Опыт в забеге", "Опыт мага (ранг)",
            "Изменения применяются сразу. x1 — как в оригинале.\nОпыт в забеге в коопе работает, если мод стоит у хоста.", "Множитель мода" },
        ["uk"] = new[] { "Множники", "Мод увімкнено", "Увімк", "Вимк", "Золото за забіг", "Досвід у забігу", "Досвід мага (ранг)",
            "Зміни застосовуються одразу. x1 — як в оригіналі.\nДосвід у забігу в кооперативі працює, якщо мод встановлено в хоста.", "Множник мода" },
        ["de"] = new[] { "Multiplikatoren", "Mod aktiviert", "An", "Aus", "Gold pro Durchlauf", "EP im Durchlauf", "Magier-Rang-EP",
            "Änderungen gelten sofort. x1 = Original.\nIm Koop wirken die EP im Durchlauf, wenn der Host die Mod hat.", "Mod-Multiplikator" },
        ["fr"] = new[] { "Multiplicateurs", "Mod activé", "Activé", "Désactivé", "Or par partie", "XP de partie", "XP de rang du mage",
            "Les changements s'appliquent immédiatement. x1 = jeu d'origine.\nEn coop, l'XP de partie fonctionne si l'hôte a le mod.", "Multiplicateur du mod" },
        ["it"] = new[] { "Moltiplicatori", "Mod attiva", "Attivo", "Disattivo", "Oro per partita", "PE della partita", "PE rango mago",
            "Le modifiche si applicano subito. x1 = originale.\nIn co-op, i PE della partita funzionano se l'host ha la mod.", "Moltiplicatore mod" },
        ["nl"] = new[] { "Vermenigvuldigers", "Mod ingeschakeld", "Aan", "Uit", "Goud per run", "Run-XP", "Tovenaarsrang-XP",
            "Wijzigingen gelden direct. x1 = origineel.\nIn co-op werkt run-XP als de host de mod heeft.", "Mod-vermenigvuldiger" },
        ["pl"] = new[] { "Mnożniki", "Mod włączony", "Wł.", "Wył.", "Złoto za wyprawę", "PD w wyprawie", "PD rangi maga",
            "Zmiany działają od razu. x1 = oryginał.\nW trybie co-op PD w wyprawie działa, jeśli host ma moda.", "Mnożnik moda" },
        ["pt-br"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As mudanças valem na hora. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod" },
        ["pt"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As alterações aplicam-se de imediato. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod" },
        ["es"] = new[] { "Multiplicadores", "Mod activado", "Activado", "Desactivado", "Oro por partida", "XP de la partida", "XP de rango del mago",
            "Los cambios se aplican al instante. x1 = original.\nEn cooperativo, la XP de la partida funciona si el anfitrión tiene el mod.", "Multiplicador del mod" },
        ["ja"] = new[] { "倍率", "MOD有効", "オン", "オフ", "ラン毎のゴールド", "ラン中の経験値", "魔法使いランク経験値",
            "変更はすぐに反映されます。x1 = オリジナル。\n協力プレイでは、ホストがMODを導入している場合にラン中の経験値が有効です。", "MOD倍率" },
        ["ko"] = new[] { "배율", "모드 활성화", "켜기", "끄기", "런당 골드", "런 경험치", "마법사 랭크 경험치",
            "변경 사항은 즉시 적용됩니다. x1 = 원본.\n협동 플레이에서는 호스트에게 모드가 있어야 런 경험치가 적용됩니다.", "모드 배율" },
        ["zh-hans"] = new[] { "倍率", "启用模组", "开", "关", "每局金币", "局内经验", "法师等级经验",
            "更改立即生效。x1 = 原版。\n联机时，房主安装了模组，局内经验倍率才会生效。", "模组倍率" },
        ["zh-hant"] = new[] { "倍率", "啟用模組", "開", "關", "每局金幣", "局內經驗", "法師等級經驗",
            "變更立即生效。x1 = 原版。\n連線時，房主安裝了模組，局內經驗倍率才會生效。", "模組倍率" },
        ["th"] = new[] { "ตัวคูณ", "เปิดใช้ม็อด", "เปิด", "ปิด", "ทองต่อรอบ", "EXP ในรอบ", "EXP แรงค์นักเวทย์",
            "การเปลี่ยนแปลงมีผลทันที x1 = ค่าเดิม\nในโหมดเล่นร่วมกัน EXP ในรอบจะทำงานเมื่อโฮสต์ติดตั้งม็อด", "ตัวคูณของม็อด" },
    };

    private static string[] _current;
    private static readonly Dictionary<string, bool> FontSupport = new();
    private static readonly List<(TMP_Text text, string key)> Bound = new();
    private static readonly List<Action> Listeners = new();
    private static bool _subscribed;

    public static string Get(string key)
    {
        _current ??= Resolve(null);
        int i = Array.IndexOf(Keys, key);
        return i >= 0 ? _current[i] : key;
    }

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key, TMP_Text fontSample = null)
    {
        if (text == null) return;
        EnsureSubscribed();
        if (_current == null) _current = Resolve(fontSample ?? text);
        text.text = Get(key);
        Bound.Add((text, key));
    }

    public static void OnLanguageChanged(Action listener)
    {
        EnsureSubscribed();
        Listeners.Add(listener);
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        try
        {
            LocalizationSettings.add_SelectedLocaleChanged((Il2CppSystem.Action<Locale>)(Action<Locale>)(_ => Refresh()));
        }
        catch (Exception e) { GoldXpMod.Log.Warning($"[lang] can't follow language changes: {e.Message}"); }
    }

    private static void Refresh()
    {
        TMP_Text sample = null;
        Bound.RemoveAll(b => b.text == null);
        foreach (var b in Bound) { sample = b.text; break; }
        _current = Resolve(sample);
        foreach (var (text, key) in Bound) text.text = Get(key);
        foreach (var listener in Listeners)
            try { listener(); } catch (Exception e) { GoldXpMod.Log.Warning($"[lang] {e.Message}"); }
    }

    private static string[] Resolve(TMP_Text fontSample)
    {
        string code = "en";
        try { code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en"; } catch { }
        string key = TableKey(code);
        var table = Table[key];
        if (key != "en" && fontSample != null && !FontHasAll(fontSample, key, table))
        {
            GoldXpMod.Log.Warning($"[lang] font lacks glyphs for '{code}', using English");
            key = "en";
            table = Table[key];
        }
        return table;
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

    private static bool FontHasAll(TMP_Text sample, string key, string[] table)
    {
        if (FontSupport.TryGetValue(key, out bool ok)) return ok;
        ok = true;
        try
        {
            var font = sample.font;
            if (font != null)
                foreach (var s in table)
                    foreach (char c in s)
                        if (c > 127 && !char.IsWhiteSpace(c) && !font.HasCharacter(c, true, true)) { ok = false; break; }
        }
        catch { ok = true; }
        FontSupport[key] = ok;
        return ok;
    }
}
