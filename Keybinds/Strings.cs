using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace KeybindsUnlocked;

// Тексты мода на всех языках игры. Язык берётся из настроек игры и меняется
// на лету вместе с ней; если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Keyboard = "kb", Gamepad = "gp", InGame = "game", Menus = "menus",
        MoveUp = "move_up", MoveDown = "move_down", MoveLeft = "move_left", MoveRight = "move_right", MoveStick = "move",
        Pause = "pause", Stats = "stats", Tooltips = "tooltips", Emote = "emote", Ping = "ping",
        QuickChat = "chat", QuickChatClose = "chat_close",
        NextTab = "next_tab", PrevTab = "prev_tab", NextPage = "next_page", PrevPage = "prev_page",
        Ready = "ready", Invite = "invite", Matchmaking = "matchmaking", LobbyCode = "lobby", SelectWizard = "select",
        Lore = "lore", Prestige = "prestige", Refund = "refund", Options = "options", Report = "report", Continue = "continue",
        PressKey = "press", Hint = "hint", ResetAll = "reset", Conflict = "conflict";

    private static readonly string[] Keys =
    {
        Keyboard, Gamepad, InGame, Menus, MoveUp, MoveDown, MoveLeft, MoveRight, MoveStick,
        Pause, Stats, Tooltips, Emote, Ping, QuickChat, QuickChatClose,
        NextTab, PrevTab, NextPage, PrevPage, Ready, Invite, Matchmaking, LobbyCode, SelectWizard,
        Lore, Prestige, Refund, Options, Report, Continue, PressKey, Hint, ResetAll, Conflict,
    };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Keyboard & Mouse", "Controller", "In a run", "Menus", "Move up", "Move down", "Move left", "Move right", "Move (stick)",
            "Pause", "Stats", "Tooltips", "Emote wheel", "Ping", "Quick chat", "Close quick chat",
            "Next tab", "Previous tab", "Next page", "Previous page", "Ready", "Invite", "Matchmaking", "Lobby code", "Select wizard",
            "Lore", "Prestige", "Refund upgrade", "Options", "Report", "Continue", "Press a key…",
            "Esc — cancel. Menu navigation (arrows, Space, Esc) always stays the same.", "Reset all", "Orange — the key is also used by another action here." },
        ["ru"] = new[] { "Клавиатура и мышь", "Контроллер", "В забеге", "Меню", "Идти вверх", "Идти вниз", "Идти влево", "Идти вправо", "Движение (стик)",
            "Пауза", "Статистика", "Подсказки", "Колесо эмоций", "Пинг", "Быстрый чат", "Закрыть быстрый чат",
            "Следующая вкладка", "Предыдущая вкладка", "Следующая страница", "Предыдущая страница", "Готов", "Пригласить", "Подбор игроков", "Код лобби", "Выбрать мага",
            "История", "Престиж", "Вернуть улучшение", "Параметры", "Пожаловаться", "Продолжить", "Нажмите клавишу…",
            "Esc — отмена. Навигация по меню (стрелки, пробел, Esc) не меняется.", "Сбросить всё", "Оранжевым — клавиша занята ещё одним действием в этом разделе." },
        ["uk"] = new[] { "Клавіатура і миша", "Контролер", "У забігу", "Меню", "Йти вгору", "Йти вниз", "Йти ліворуч", "Йти праворуч", "Рух (стік)",
            "Пауза", "Статистика", "Підказки", "Колесо емоцій", "Пінг", "Швидкий чат", "Закрити швидкий чат",
            "Наступна вкладка", "Попередня вкладка", "Наступна сторінка", "Попередня сторінка", "Готовий", "Запросити", "Підбір гравців", "Код лобі", "Вибрати мага",
            "Історія", "Престиж", "Повернути покращення", "Параметри", "Поскаржитися", "Продовжити", "Натисніть клавішу…",
            "Esc — скасувати. Навігація меню (стрілки, пробіл, Esc) не змінюється.", "Скинути все", "Помаранчевим — клавіша зайнята ще однією дією в цьому розділі." },
        ["de"] = new[] { "Tastatur & Maus", "Controller", "Im Durchlauf", "Menüs", "Nach oben", "Nach unten", "Nach links", "Nach rechts", "Bewegen (Stick)",
            "Pause", "Statistiken", "Tooltips", "Emote-Rad", "Ping", "Schnellchat", "Schnellchat schließen",
            "Nächster Tab", "Vorheriger Tab", "Nächste Seite", "Vorherige Seite", "Bereit", "Einladen", "Spielersuche", "Lobby-Code", "Magier wählen",
            "Hintergrund", "Prestige", "Verbesserung erstatten", "Optionen", "Melden", "Weiter", "Taste drücken…",
            "Esc — abbrechen. Die Menüsteuerung (Pfeile, Leertaste, Esc) bleibt immer gleich.", "Alles zurücksetzen", "Orange — die Taste ist hier auch einer anderen Aktion zugewiesen." },
        ["fr"] = new[] { "Clavier et souris", "Manette", "En partie", "Menus", "Aller en haut", "Aller en bas", "Aller à gauche", "Aller à droite", "Déplacement (stick)",
            "Pause", "Statistiques", "Infobulles", "Roue d'émotes", "Ping", "Chat rapide", "Fermer le chat rapide",
            "Onglet suivant", "Onglet précédent", "Page suivante", "Page précédente", "Prêt", "Inviter", "Matchmaking", "Code du salon", "Choisir le mage",
            "Histoire", "Prestige", "Rembourser l'amélioration", "Options", "Signaler", "Continuer", "Appuyez sur une touche…",
            "Échap — annuler. La navigation des menus (flèches, Espace, Échap) ne change pas.", "Tout réinitialiser", "En orange — la touche est aussi utilisée par une autre action ici." },
        ["it"] = new[] { "Tastiera e mouse", "Controller", "In partita", "Menu", "Muovi su", "Muovi giù", "Muovi a sinistra", "Muovi a destra", "Movimento (stick)",
            "Pausa", "Statistiche", "Suggerimenti", "Ruota delle emote", "Ping", "Chat rapida", "Chiudi chat rapida",
            "Scheda successiva", "Scheda precedente", "Pagina successiva", "Pagina precedente", "Pronto", "Invita", "Matchmaking", "Codice lobby", "Scegli mago",
            "Storia", "Prestigio", "Rimborsa potenziamento", "Opzioni", "Segnala", "Continua", "Premi un tasto…",
            "Esc — annulla. La navigazione dei menu (frecce, Spazio, Esc) resta invariata.", "Ripristina tutto", "In arancione — il tasto è usato anche da un'altra azione qui." },
        ["nl"] = new[] { "Toetsenbord & muis", "Controller", "In een run", "Menu's", "Omhoog", "Omlaag", "Naar links", "Naar rechts", "Bewegen (stick)",
            "Pauze", "Statistieken", "Tooltips", "Emotewiel", "Ping", "Snelchat", "Snelchat sluiten",
            "Volgend tabblad", "Vorig tabblad", "Volgende pagina", "Vorige pagina", "Klaar", "Uitnodigen", "Matchmaking", "Lobbycode", "Tovenaar kiezen",
            "Achtergrond", "Prestige", "Upgrade terugbetalen", "Opties", "Rapporteren", "Doorgaan", "Druk op een toets…",
            "Esc — annuleren. Menunavigatie (pijlen, spatie, Esc) blijft altijd hetzelfde.", "Alles resetten", "Oranje — de toets wordt hier ook door een andere actie gebruikt." },
        ["pl"] = new[] { "Klawiatura i mysz", "Kontroler", "W wyprawie", "Menu", "Ruch w górę", "Ruch w dół", "Ruch w lewo", "Ruch w prawo", "Ruch (gałka)",
            "Pauza", "Statystyki", "Podpowiedzi", "Koło emotek", "Ping", "Szybki czat", "Zamknij szybki czat",
            "Następna karta", "Poprzednia karta", "Następna strona", "Poprzednia strona", "Gotowy", "Zaproś", "Dobieranie graczy", "Kod lobby", "Wybierz maga",
            "Historia", "Prestiż", "Zwrot ulepszenia", "Opcje", "Zgłoś", "Kontynuuj", "Naciśnij klawisz…",
            "Esc — anuluj. Nawigacja w menu (strzałki, spacja, Esc) pozostaje bez zmian.", "Resetuj wszystko", "Na pomarańczowo — klawisz jest tu używany także przez inną akcję." },
        ["pt-br"] = new[] { "Teclado e mouse", "Controle", "Na partida", "Menus", "Mover para cima", "Mover para baixo", "Mover para a esquerda", "Mover para a direita", "Mover (analógico)",
            "Pausar", "Estatísticas", "Dicas", "Roda de emotes", "Ping", "Chat rápido", "Fechar chat rápido",
            "Próxima aba", "Aba anterior", "Próxima página", "Página anterior", "Pronto", "Convidar", "Matchmaking", "Código do lobby", "Escolher mago",
            "História", "Prestígio", "Reembolsar melhoria", "Opções", "Denunciar", "Continuar", "Pressione uma tecla…",
            "Esc — cancelar. A navegação dos menus (setas, Espaço, Esc) não muda.", "Redefinir tudo", "Em laranja — a tecla também é usada por outra ação aqui." },
        ["pt"] = new[] { "Teclado e rato", "Comando", "Na partida", "Menus", "Mover para cima", "Mover para baixo", "Mover para a esquerda", "Mover para a direita", "Mover (analógico)",
            "Pausa", "Estatísticas", "Dicas", "Roda de emotes", "Ping", "Chat rápido", "Fechar chat rápido",
            "Separador seguinte", "Separador anterior", "Página seguinte", "Página anterior", "Pronto", "Convidar", "Matchmaking", "Código do lobby", "Escolher mago",
            "História", "Prestígio", "Reembolsar melhoria", "Opções", "Denunciar", "Continuar", "Prima uma tecla…",
            "Esc — cancelar. A navegação dos menus (setas, Espaço, Esc) não muda.", "Repor tudo", "A laranja — a tecla também é usada por outra ação aqui." },
        ["es"] = new[] { "Teclado y ratón", "Mando", "En la partida", "Menús", "Mover arriba", "Mover abajo", "Mover a la izquierda", "Mover a la derecha", "Mover (stick)",
            "Pausa", "Estadísticas", "Consejos", "Rueda de gestos", "Ping", "Chat rápido", "Cerrar chat rápido",
            "Pestaña siguiente", "Pestaña anterior", "Página siguiente", "Página anterior", "Listo", "Invitar", "Emparejamiento", "Código de sala", "Elegir mago",
            "Historia", "Prestigio", "Reembolsar mejora", "Opciones", "Denunciar", "Continuar", "Pulsa una tecla…",
            "Esc — cancelar. La navegación de menús (flechas, Espacio, Esc) no cambia.", "Restablecer todo", "En naranja — la tecla también la usa otra acción aquí." },
        ["ja"] = new[] { "キーボード＆マウス", "コントローラー", "ラン中", "メニュー", "上へ移動", "下へ移動", "左へ移動", "右へ移動", "移動（スティック）",
            "ポーズ", "ステータス", "ツールチップ", "エモートホイール", "ピン", "クイックチャット", "クイックチャットを閉じる",
            "次のタブ", "前のタブ", "次のページ", "前のページ", "準備完了", "招待", "マッチメイキング", "ロビーコード", "魔法使いを選択",
            "ストーリー", "プレステージ", "強化を払い戻す", "オプション", "通報", "続ける", "キーを押してください…",
            "Esc — キャンセル。メニュー操作（矢印、スペース、Esc）は変わりません。", "すべてリセット", "オレンジ色 — このキーはここで別の操作にも割り当てられています。" },
        ["ko"] = new[] { "키보드 및 마우스", "컨트롤러", "런 중", "메뉴", "위로 이동", "아래로 이동", "왼쪽으로 이동", "오른쪽으로 이동", "이동 (스틱)",
            "일시정지", "통계", "툴팁", "이모트 휠", "핑", "빠른 채팅", "빠른 채팅 닫기",
            "다음 탭", "이전 탭", "다음 페이지", "이전 페이지", "준비", "초대", "매치메이킹", "로비 코드", "마법사 선택",
            "스토리", "프레스티지", "업그레이드 환불", "옵션", "신고", "계속", "키를 누르세요…",
            "Esc — 취소. 메뉴 조작(화살표, 스페이스, Esc)은 바뀌지 않습니다.", "모두 초기화", "주황색 — 이 키는 여기서 다른 동작에도 사용됩니다." },
        ["zh-hans"] = new[] { "键盘和鼠标", "手柄", "局内", "菜单", "向上移动", "向下移动", "向左移动", "向右移动", "移动（摇杆）",
            "暂停", "统计", "提示", "表情轮盘", "标记", "快捷聊天", "关闭快捷聊天",
            "下一个标签页", "上一个标签页", "下一页", "上一页", "准备", "邀请", "匹配", "大厅代码", "选择法师",
            "背景故事", "声望", "退还升级", "选项", "举报", "继续", "请按下按键…",
            "Esc — 取消。菜单操作（方向键、空格、Esc）保持不变。", "全部重置", "橙色 — 该按键在此处也被其他操作使用。" },
        ["zh-hant"] = new[] { "鍵盤和滑鼠", "手把", "局內", "選單", "向上移動", "向下移動", "向左移動", "向右移動", "移動（搖桿）",
            "暫停", "統計", "提示", "表情輪盤", "標記", "快捷聊天", "關閉快捷聊天",
            "下一個分頁", "上一個分頁", "下一頁", "上一頁", "準備", "邀請", "配對", "大廳代碼", "選擇法師",
            "背景故事", "聲望", "退還升級", "選項", "檢舉", "繼續", "請按下按鍵…",
            "Esc — 取消。選單操作（方向鍵、空白鍵、Esc）保持不變。", "全部重設", "橘色 — 該按鍵在此處也被其他操作使用。" },
        ["th"] = new[] { "คีย์บอร์ดและเมาส์", "คอนโทรลเลอร์", "ในรอบ", "เมนู", "เดินขึ้น", "เดินลง", "เดินซ้าย", "เดินขวา", "เดิน (สติ๊ก)",
            "หยุดชั่วคราว", "สถิติ", "คำแนะนำ", "วงล้ออีโมต", "ปิง", "แชตด่วน", "ปิดแชตด่วน",
            "แท็บถัดไป", "แท็บก่อนหน้า", "หน้าถัดไป", "หน้าก่อนหน้า", "พร้อม", "เชิญ", "จับคู่ผู้เล่น", "รหัสล็อบบี้", "เลือกนักเวทย์",
            "เรื่องราว", "เพรสทีจ", "คืนอัปเกรด", "ตัวเลือก", "รายงาน", "ดำเนินการต่อ", "กดปุ่ม…",
            "Esc — ยกเลิก การควบคุมเมนู (ลูกศร, Space, Esc) จะไม่เปลี่ยน", "รีเซ็ตทั้งหมด", "สีส้ม — ปุ่มนี้ถูกใช้กับการกระทำอื่นในหมวดนี้ด้วย" },
    };

    private static string[] _current;
    private static readonly Dictionary<string, bool> FontSupport = new();
    private static readonly List<(TMP_Text text, Func<string> value)> Bound = new();
    private static bool _subscribed;

    public static event Action LanguageChanged;

    public static string Get(string key)
    {
        _current ??= Resolve(null);
        int i = Array.IndexOf(Keys, key);
        return i >= 0 ? _current[i] : key;
    }

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, Func<string> value)
    {
        if (text == null) return;
        EnsureSubscribed();
        _current ??= Resolve(text);
        text.text = value();
        Bound.Add((text, value));
    }

    public static void Bind(TMP_Text text, string key) => Bind(text, () => Get(key));

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        try
        {
            LocalizationSettings.add_SelectedLocaleChanged((Il2CppSystem.Action<Locale>)(Action<Locale>)(_ => Refresh()));
        }
        catch (Exception e) { KeybindsMod.Log.Warning($"[lang] can't follow language changes: {e.Message}"); }
    }

    private static void Refresh()
    {
        Bound.RemoveAll(b => b.text == null);
        _current = Resolve(Bound.Count > 0 ? Bound[0].text : null);
        foreach (var (text, value) in Bound) text.text = value();
        try { LanguageChanged?.Invoke(); } catch (Exception e) { KeybindsMod.Log.Warning($"[lang] {e.Message}"); }
    }

    private static string[] Resolve(TMP_Text fontSample)
    {
        string code = "en";
        try { code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en"; } catch { }
        string key = TableKey(code);
        if (key != "en" && fontSample != null && !FontHasAll(fontSample, key))
        {
            KeybindsMod.Log.Warning($"[lang] font lacks glyphs for '{code}', using English");
            key = "en";
        }
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
