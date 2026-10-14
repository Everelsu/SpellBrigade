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
                        Reveal = "reveal",
                        Matchmaking = "mm";

    private static readonly string[] Keys = { Tab, Enabled, On, Off, Gold, Xp, RankXp, Hint, Bonus, Reveal, Matchmaking };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Multipliers", "Mod Enabled", "On", "Off", "Gold per Run", "Run XP", "Wizard Rank XP",
            "Changes apply immediately. x1 = vanilla.\nIn co-op, run XP works when the host has the mod.", "Mod multiplier", "x2 reveal at the end of a run", "run XP multiplier is {0}: as host you change XP for the whole team. Set it to x1 to use matchmaking." },
        ["ru"] = new[] { "Множители", "Мод включён", "Вкл", "Выкл", "Золото за забег", "Опыт в забеге", "Опыт мага (ранг)",
            "Изменения применяются сразу. x1 — как в оригинале.\nОпыт в забеге в коопе работает, если мод стоит у хоста.", "Множитель мода", "Анимация x2 в итогах забега", "множитель опыта в забеге {0}: у хоста он меняет опыт всей команде. Поставьте x1, чтобы включить подбор." },
        ["uk"] = new[] { "Множники", "Мод увімкнено", "Увімк", "Вимк", "Золото за забіг", "Досвід у забігу", "Досвід мага (ранг)",
            "Зміни застосовуються одразу. x1 — як в оригіналі.\nДосвід у забігу в кооперативі працює, якщо мод встановлено в хоста.", "Множник мода", "Анімація x2 у підсумках забігу", "множник досвіду в забігу {0}: у хоста він змінює досвід усій команді. Поставте x1, щоб увімкнути підбір." },
        ["de"] = new[] { "Multiplikatoren", "Mod aktiviert", "An", "Aus", "Gold pro Durchlauf", "EP im Durchlauf", "Magier-Rang-EP",
            "Änderungen gelten sofort. x1 = Original.\nIm Koop wirken die EP im Durchlauf, wenn der Host die Mod hat.", "Mod-Multiplikator", "x2-Animation am Ende des Laufs", "Lauf-EP-Multiplikator ist {0}: Als Host änderst du die EP des ganzen Teams. Stelle ihn auf x1, um Matchmaking zu nutzen." },
        ["fr"] = new[] { "Multiplicateurs", "Mod activé", "Activé", "Désactivé", "Or par partie", "XP de partie", "XP de rang du mage",
            "Les changements s'appliquent immédiatement. x1 = jeu d'origine.\nEn coop, l'XP de partie fonctionne si l'hôte a le mod.", "Multiplicateur du mod", "Animation x2 en fin de partie", "le multiplicateur d'XP de partie est {0} : en tant qu'hôte, vous modifiez l'XP de toute l'équipe. Mettez-le à x1 pour utiliser le matchmaking." },
        ["it"] = new[] { "Moltiplicatori", "Mod attiva", "Attivo", "Disattivo", "Oro per partita", "PE della partita", "PE rango mago",
            "Le modifiche si applicano subito. x1 = originale.\nIn co-op, i PE della partita funzionano se l'host ha la mod.", "Moltiplicatore mod", "Animazione x2 a fine partita", "il moltiplicatore PE della partita è {0}: come host cambi i PE di tutta la squadra. Impostalo a x1 per usare il matchmaking." },
        ["nl"] = new[] { "Vermenigvuldigers", "Mod ingeschakeld", "Aan", "Uit", "Goud per run", "Run-XP", "Tovenaarsrang-XP",
            "Wijzigingen gelden direct. x1 = origineel.\nIn co-op werkt run-XP als de host de mod heeft.", "Mod-vermenigvuldiger", "x2-animatie aan het einde van de run", "run-XP-vermenigvuldiger is {0}: als host verander je de XP van het hele team. Zet hem op x1 om matchmaking te gebruiken." },
        ["pl"] = new[] { "Mnożniki", "Mod włączony", "Wł.", "Wył.", "Złoto za wyprawę", "PD w wyprawie", "PD rangi maga",
            "Zmiany działają od razu. x1 = oryginał.\nW trybie co-op PD w wyprawie działa, jeśli host ma moda.", "Mnożnik moda", "Animacja x2 w podsumowaniu wyprawy", "mnożnik PD w wyprawie to {0}: jako host zmieniasz PD całej drużynie. Ustaw x1, aby używać dobierania." },
        ["pt-br"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As mudanças valem na hora. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod", "Animação x2 no fim da partida", "o multiplicador de XP da partida é {0}: como anfitrião, você muda o XP de toda a equipe. Coloque x1 para usar o matchmaking." },
        ["pt"] = new[] { "Multiplicadores", "Mod ativado", "Ligado", "Desligado", "Ouro por partida", "XP da partida", "XP de nível do mago",
            "As alterações aplicam-se de imediato. x1 = original.\nNo co-op, o XP da partida funciona se o anfitrião tiver o mod.", "Multiplicador do mod", "Animação x2 no fim da partida", "o multiplicador de XP da partida é {0}: como anfitrião, mudas o XP de toda a equipa. Põe x1 para usar o matchmaking." },
        ["es"] = new[] { "Multiplicadores", "Mod activado", "Activado", "Desactivado", "Oro por partida", "XP de la partida", "XP de rango del mago",
            "Los cambios se aplican al instante. x1 = original.\nEn cooperativo, la XP de la partida funciona si el anfitrión tiene el mod.", "Multiplicador del mod", "Animación x2 al final de la partida", "el multiplicador de XP de la partida es {0}: como anfitrión cambias la XP de todo el equipo. Ponlo en x1 para usar el emparejamiento." },
        ["es-mx"] = new[] { "Multiplicadores", "Mod activado", "Activado", "Desactivado", "Oro por partida", "XP de la partida", "XP de rango del mago",
            "Los cambios se aplican al instante. x1 = original.\nEn cooperativo, la XP de la partida funciona si el anfitrión tiene el mod.", "Multiplicador del mod", "Animación x2 al final de la partida", "el multiplicador de XP de la partida es {0}: como anfitrión cambias la XP de todo el equipo. Ponlo en x1 para usar el emparejamiento." },
        ["ja"] = new[] { "倍率", "MOD有効", "オン", "オフ", "ラン毎のゴールド", "ラン中の経験値", "魔法使いランク経験値",
            "変更はすぐに反映されます。x1 = オリジナル。\n協力プレイでは、ホストがMODを導入している場合にラン中の経験値が有効です。", "MOD倍率", "ラン終了時の x2 演出", "ラン中の経験値倍率が{0}です：ホストだとチーム全員の経験値が変わります。マッチングを使うにはx1にしてください。" },
        ["ko"] = new[] { "배율", "모드 활성화", "켜기", "끄기", "런당 골드", "런 경험치", "마법사 랭크 경험치",
            "변경 사항은 즉시 적용됩니다. x1 = 원본.\n협동 플레이에서는 호스트에게 모드가 있어야 런 경험치가 적용됩니다.", "모드 배율", "런 종료 시 x2 연출", "런 경험치 배율이 {0}입니다: 호스트라면 팀 전체의 경험치가 바뀝니다. 매칭을 쓰려면 x1로 설정하세요." },
        ["zh-hans"] = new[] { "倍率", "启用模组", "开", "关", "每局金币", "局内经验", "法师等级经验",
            "更改立即生效。x1 = 原版。\n联机时，房主安装了模组，局内经验倍率才会生效。", "模组倍率", "结算时的 x2 动画", "局内经验倍率为 {0}：作为主机会改变全队的经验。设为 x1 才能使用匹配。" },
        ["zh-hant"] = new[] { "倍率", "啟用模組", "開", "關", "每局金幣", "局內經驗", "法師等級經驗",
            "變更立即生效。x1 = 原版。\n連線時，房主安裝了模組，局內經驗倍率才會生效。", "模組倍率", "結算時的 x2 動畫", "局內經驗倍率為 {0}：作為主機會改變全隊的經驗。設為 x1 才能使用配對。" },
        ["th"] = new[] { "ตัวคูณ", "เปิดใช้ม็อด", "เปิด", "ปิด", "ทองต่อรอบ", "EXP ในรอบ", "EXP แรงค์นักเวทย์",
            "การเปลี่ยนแปลงมีผลทันที x1 = ค่าเดิม\nในโหมดเล่นร่วมกัน EXP ในรอบจะทำงานเมื่อโฮสต์ติดตั้งม็อด", "ตัวคูณของม็อด", "แอนิเมชัน x2 ตอนจบรอบ", "ตัวคูณ XP ในรอบคือ {0}: ถ้าเป็นโฮสต์จะเปลี่ยน XP ของทั้งทีม ตั้งเป็น x1 เพื่อใช้การจับคู่" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => GoldXpMod.Log);

    public static string Get(string key) => Text.Get(key);

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key, TMP_Text fontSample = null) => Text.Bind(text, key, fontSample);

    public static void OnLanguageChanged(Action listener) => Text.OnLanguageChanged(listener);
}
