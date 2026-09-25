using System;
using System.Collections.Generic;
using Il2CppTMPro;
using SpellBrigade.Shared;

namespace GoldXpMultiplier;

// Тексты мода на всех языках игры. Язык берётся из настроек игры и меняется
// на лету вместе с ней; если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Tab = "tab", Enabled = "enabled", On = "on", Off = "off", Gold = "gold",
                        Xp = "xp", RankXp = "rank", Hint = "hint", Bonus = "bonus",
                        Reveal = "reveal";

    private static readonly string[] Keys = { Tab, Enabled, On, Off, Gold, Xp, RankXp, Hint, Bonus, Reveal };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Multipliers", "Mod Enabled", "On", "Off", "Gold per Run", "Run XP", "Wizard Rank XP",
            "Changes apply immediately. x1 = vanilla.\nIn co-op, run XP works when the host has the mod.", "Mod multiplier", "x2 reveal at the end of a run" },
        ["ru"] = new[] { "Множители", "Мод включён", "Вкл", "Выкл", "Золото за забег", "Опыт в забеге", "Опыт мага (ранг)",
            "Изменения применяются сразу. x1 — как в оригинале.\nОпыт в забеге в коопе работает, если мод стоит у хоста.", "Множитель мода", "Анимация x2 в итогах забега" },
        ["uk"] = new[] { "Множники", "Мод увімкнено", "Увімк", "Вимк", "Золото за забіг", "Досвід у забігу", "Досвід мага (ранг)",
            "Зміни застосовуються одразу. x1 — як в оригіналі.\nДосвід у забігу в кооперативі працює, якщо мод встановлено в хоста.", "Множник мода", "Анімація x2 у підсумках забігу" },
        ["de"] = new[] { "Multiplikatoren", "Mod aktiviert", "An", "Aus", "Gold pro Durchlauf", "EP im Durchlauf", "Magier-Rang-EP",
            "Änderungen gelten sofort. x1 = Original.\nIm Koop wirken die EP im Durchlauf, wenn der Host die Mod hat.", "Mod-Multiplikator", "x2-Animation am Ende des Laufs" },
        ["fr"] = new[] { "Multiplicateurs", "Mod activé", "Activé", "Désactivé", "Or par partie", "XP de partie", "XP de rang du mage",
            "Les changements s'appliquent immédiatement. x1 = jeu d'origine.\nEn coop, l'XP de partie fonctionne si l'hôte a le mod.", "Multiplicateur du mod", "Animation x2 en fin de partie" },
        ["it"] = new[] { "Moltiplicatori", "Mod attiva", "Attivo", "Disattivo", "Oro per partita", "PE della partita", "PE rango mago",
            "Le modifiche si applicano subito. x1 = originale.\nIn co-op, i PE della partita funzionano se l'host ha la mod.", "Moltiplicatore mod", "Animazione x2 a fine partita" },
        ["nl"] = new[] { "Vermenigvuldigers", "Mod ingeschakeld", "Aan", "Uit", "Goud per run", "Run-XP", "Tovenaarsrang-XP",
            "Wijzigingen gelden direct. x1 = origineel.\nIn co-op werkt run-XP als de host de mod heeft.", "Mod-vermenigvuldiger", "x2-animatie aan het einde van de run" },
        ["pl"] = new[] { "Mnożniki", "Mod włączony", "Wł.", "Wył.", "Złoto za wyprawę", "PD w wyprawie", "PD rangi maga",
            "Zmiany działają od razu. x1 = oryginał.\nW trybie co-op PD w wyprawie działa, jeśli host ma moda.", "Mnożnik moda", "Animacja x2 w podsumowaniu wyprawy" },
        ["pt-br"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As mudanças valem na hora. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod", "Animação x2 no fim da partida" },
        ["pt"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As alterações aplicam-se de imediato. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod", "Animação x2 no fim da partida" },
        ["es"] = new[] { "Multiplicadores", "Mod activado", "Activado", "Desactivado", "Oro por partida", "XP de la partida", "XP de rango del mago",
            "Los cambios se aplican al instante. x1 = original.\nEn cooperativo, la XP de la partida funciona si el anfitrión tiene el mod.", "Multiplicador del mod", "Animación x2 al final de la partida" },
        ["ja"] = new[] { "倍率", "MOD有効", "オン", "オフ", "ラン毎のゴールド", "ラン中の経験値", "魔法使いランク経験値",
            "変更はすぐに反映されます。x1 = オリジナル。\n協力プレイでは、ホストがMODを導入している場合にラン中の経験値が有効です。", "MOD倍率", "ラン終了時の x2 演出" },
        ["ko"] = new[] { "배율", "모드 활성화", "켜기", "끄기", "런당 골드", "런 경험치", "마법사 랭크 경험치",
            "변경 사항은 즉시 적용됩니다. x1 = 원본.\n협동 플레이에서는 호스트에게 모드가 있어야 런 경험치가 적용됩니다.", "모드 배율", "런 종료 시 x2 연출" },
        ["zh-hans"] = new[] { "倍率", "启用模组", "开", "关", "每局金币", "局内经验", "法师等级经验",
            "更改立即生效。x1 = 原版。\n联机时，房主安装了模组，局内经验倍率才会生效。", "模组倍率", "结算时的 x2 动画" },
        ["zh-hant"] = new[] { "倍率", "啟用模組", "開", "關", "每局金幣", "局內經驗", "法師等級經驗",
            "變更立即生效。x1 = 原版。\n連線時，房主安裝了模組，局內經驗倍率才會生效。", "模組倍率", "結算時的 x2 動畫" },
        ["th"] = new[] { "ตัวคูณ", "เปิดใช้ม็อด", "เปิด", "ปิด", "ทองต่อรอบ", "EXP ในรอบ", "EXP แรงค์นักเวทย์",
            "การเปลี่ยนแปลงมีผลทันที x1 = ค่าเดิม\nในโหมดเล่นร่วมกัน EXP ในรอบจะทำงานเมื่อโฮสต์ติดตั้งม็อด", "ตัวคูณของม็อด", "แอนิเมชัน x2 ตอนจบรอบ" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => GoldXpMod.Log);

    public static string Get(string key) => Text.Get(key);

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key, TMP_Text fontSample = null) => Text.Bind(text, key, fontSample);

    public static void OnLanguageChanged(Action listener) => Text.OnLanguageChanged(listener);
}
