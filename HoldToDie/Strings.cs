using System.Collections.Generic;
using Il2CppTMPro;
using SpellBrigade.Shared;

namespace HoldToDie;

// Подписи в Mod Menu на всех языках игры. Язык берётся из настроек игры и меняется на лету.
internal static class Strings
{
    public const string Enabled = "enabled", Key = "key", HoldTime = "hold_time", Seconds = "seconds",
                        Mode = "mode", Revivable = "revivable", NoRevives = "no_revives", Hint = "hint",
                        KeyInControls = "key_in_controls",
                        Gamepad = "gamepad", NoButton = "no_button", DieHold = "die_hold";

    private static readonly string[] Keys = { Enabled, Key, HoldTime, Seconds, Mode, Revivable, NoRevives, Hint, KeyInControls, Gamepad, NoButton, DieHold };

    // порядок значений — как в Keys; {0} в Seconds — число секунд
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Enabled", "Key to hold", "Hold time", "{0} s", "Death", "Can be revived", "No revives",
            "No revives: if you are the last one standing (solo or host), the run ends right away. Otherwise it's a normal death.", "Key and controller button: Options → Controls → Die (hold). Keybinds Unlocked handles them.", "Controller button", "None", "Die (hold)" },
        ["ru"] = new[] { "Включено", "Клавиша (удерживать)", "Сколько держать", "{0} с", "Смерть", "С воскрешением", "Без воскрешений",
            "Без воскрешений: если вы последний живой (соло или хост), забег сразу заканчивается. Иначе — обычная смерть.", "Клавиша и кнопка контроллера — в Настройки → Управление → «Умереть (удерживать)», их задаёт Keybinds Unlocked.", "Кнопка контроллера", "Нет", "Умереть (удерживать)" },
        ["uk"] = new[] { "Увімкнено", "Клавіша (утримувати)", "Скільки тримати", "{0} с", "Смерть", "З воскресінням", "Без воскресінь",
            "Без воскресінь: якщо ви останній живий (соло або хост), забіг одразу закінчується. Інакше — звичайна смерть.", "Клавіша й кнопка контролера — у Налаштування → Керування → «Померти (утримувати)», їх задає Keybinds Unlocked.", "Кнопка контролера", "Немає", "Померти (утримувати)" },
        ["de"] = new[] { "Aktiviert", "Taste (halten)", "Haltedauer", "{0} s", "Tod", "Wiederbelebbar", "Ohne Wiederbelebung",
            "Ohne Wiederbelebung: Lebst du als Letzter (solo oder als Host), endet der Lauf sofort. Sonst ist es ein normaler Tod.", "Taste und Controller-Knopf: Optionen → Steuerung → „Sterben (halten)“, festgelegt über Keybinds Unlocked.", "Controller-Taste", "Keine", "Sterben (halten)" },
        ["fr"] = new[] { "Activé", "Touche (maintenir)", "Durée d'appui", "{0} s", "Mort", "Réanimable", "Sans réanimation",
            "Sans réanimation : si vous êtes le dernier en vie (solo ou hôte), la partie se termine aussitôt. Sinon, c'est une mort normale.", "Touche et bouton de manette : Options → Commandes → « Mourir (maintenir) », gérés par Keybinds Unlocked.", "Bouton de manette", "Aucun", "Mourir (maintenir)" },
        ["it"] = new[] { "Attivo", "Tasto (tieni premuto)", "Durata pressione", "{0} s", "Morte", "Rianimabile", "Senza rianimazioni",
            "Senza rianimazioni: se sei l'ultimo in vita (in solitario o come host), la partita finisce subito. Altrimenti è una morte normale.", "Tasto e pulsante del controller: Opzioni → Comandi → «Morire (tieni premuto)», gestiti da Keybinds Unlocked.", "Pulsante del controller", "Nessuno", "Morire (tieni premuto)" },
        ["nl"] = new[] { "Aan", "Toets (ingedrukt houden)", "Houdtijd", "{0} s", "Dood", "Reanimeerbaar", "Zonder reanimaties",
            "Zonder reanimaties: leef jij als laatste (solo of host), dan eindigt de run meteen. Anders is het een gewone dood.", "Toets en controllerknop: Opties → Besturing → ‘Sterven (ingedrukt houden)’, ingesteld via Keybinds Unlocked.", "Controllerknop", "Geen", "Sterven (ingedrukt houden)" },
        ["pl"] = new[] { "Włączone", "Klawisz (przytrzymaj)", "Czas przytrzymania", "{0} s", "Śmierć", "Z wskrzeszeniem", "Bez wskrzeszeń",
            "Bez wskrzeszeń: jeśli żyjesz jako ostatni (solo lub host), wyprawa kończy się od razu. W innym wypadku to zwykła śmierć.", "Klawisz i przycisk kontrolera: Opcje → Sterowanie → „Zgiń (przytrzymaj)”, ustawia je Keybinds Unlocked.", "Przycisk kontrolera", "Brak", "Zgiń (przytrzymaj)" },
        ["pt-br"] = new[] { "Ativado", "Tecla (segurar)", "Tempo segurando", "{0} s", "Morte", "Pode ser revivido", "Sem reviver",
            "Sem reviver: se você for o último vivo (solo ou anfitrião), a partida termina na hora. Caso contrário, é uma morte normal.", "Tecla e botão do controle: Opções → Controles → “Morrer (segurar)”, definidos pelo Keybinds Unlocked.", "Botão do controle", "Nenhum", "Morrer (segurar)" },
        ["pt"] = new[] { "Ativado", "Tecla (manter premida)", "Tempo a premir", "{0} s", "Morte", "Pode ser reanimado", "Sem reanimações",
            "Sem reanimações: se fores o último vivo (a solo ou anfitrião), a partida termina de imediato. Caso contrário, é uma morte normal.", "Tecla e botão do comando: Opções → Controlos → «Morrer (manter premido)», definidos pelo Keybinds Unlocked.", "Botão do comando", "Nenhum", "Morrer (manter premido)" },
        ["es"] = new[] { "Activado", "Tecla (mantener)", "Tiempo de pulsación", "{0} s", "Muerte", "Con reanimación", "Sin reanimaciones",
            "Sin reanimaciones: si eres el último con vida (en solitario o como anfitrión), la partida termina al instante. Si no, es una muerte normal.", "Tecla y botón del mando: Opciones → Controles → «Morir (mantener)», los gestiona Keybinds Unlocked.", "Botón del mando", "Ninguno", "Morir (mantener)" },
        ["es-mx"] = new[] { "Activado", "Tecla (mantener)", "Tiempo presionado", "{0} s", "Muerte", "Con reanimación", "Sin reanimaciones",
            "Sin reanimaciones: si eres el último con vida (en solitario o como anfitrión), la partida termina al instante. Si no, es una muerte normal.", "Tecla y botón del control: Opciones → Controles → «Morir (mantener)», los gestiona Keybinds Unlocked.", "Botón del control", "Ninguno", "Morir (mantener)" },
        ["ja"] = new[] { "有効", "長押しするキー", "長押し時間", "{0} 秒", "死亡", "復活あり", "復活なし",
            "復活なし：最後の生存者なら（ソロまたはホスト）ランはすぐに終了します。それ以外は通常の死亡です。", "キーとコントローラーのボタンは「オプション → 操作 → 死亡（長押し）」で設定します（Keybinds Unlocked）。", "コントローラーのボタン", "なし", "死亡（長押し）" },
        ["ko"] = new[] { "사용", "누르고 있을 키", "누르는 시간", "{0}초", "사망", "부활 가능", "부활 없음",
            "부활 없음: 마지막 생존자라면(솔로 또는 호스트) 런이 즉시 끝납니다. 그 외에는 일반 사망입니다.", "키와 컨트롤러 버튼은 옵션 → 조작 → '사망 (길게 누르기)'에서 설정합니다 (Keybinds Unlocked).", "컨트롤러 버튼", "없음", "사망 (길게 누르기)" },
        ["zh-hans"] = new[] { "启用", "长按按键", "长按时间", "{0} 秒", "死亡", "可复活", "不复活",
            "不复活：如果你是最后的幸存者（单人或主机），本局立即结束。否则为普通死亡。", "按键和手柄按钮在“选项 → 操作 → 死亡（长按）”中设置（Keybinds Unlocked）。", "手柄按钮", "无", "死亡（长按）" },
        ["zh-hant"] = new[] { "啟用", "長按按鍵", "長按時間", "{0} 秒", "死亡", "可復活", "不復活",
            "不復活：如果你是最後的倖存者（單人或主機），本局立即結束。否則為普通死亡。", "按鍵和手把按鈕在「選項 → 操作 → 死亡（長按）」中設定（Keybinds Unlocked）。", "手把按鈕", "無", "死亡（長按）" },
        ["th"] = new[] { "เปิดใช้", "ปุ่มที่ต้องกดค้าง", "เวลากดค้าง", "{0} วิ", "ความตาย", "ชุบชีวิตได้", "ไม่ชุบชีวิต",
            "ไม่ชุบชีวิต: ถ้าคุณเป็นคนสุดท้ายที่รอด (เล่นคนเดียวหรือโฮสต์) รอบจะจบทันที ไม่เช่นนั้นจะเป็นการตายปกติ", "ปุ่มและปุ่มคอนโทรลเลอร์ตั้งได้ที่ ตัวเลือก → การควบคุม → ตาย (กดค้าง) (Keybinds Unlocked)", "ปุ่มคอนโทรลเลอร์", "ไม่มี", "ตาย (กดค้าง)" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => HoldToDieMod.Log);

    public static string Get(string key) => Text.Get(key);

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key) => Text.Bind(text, key);
}
