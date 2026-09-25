using System.Collections.Generic;
using Il2CppTMPro;
using SpellBrigade.Shared;

namespace PinnedChallenges;

// Подписи в Mod Menu и редакторе положения на всех языках игры. Язык берётся из
// настроек игры и меняется на лету вместе с ней.
internal static class Strings
{
    public const string HudScale = "hud_scale", UnpinAll = "unpin_all", Position = "position",
                        DragHint = "drag", KeysHint = "keys", Done = "done", Default = "default",
                        Opacity = "opacity", MaxPinned = "max_pinned", SizeHint = "size_hint",
                        TextOpacity = "text_opacity", GameStyle = "game_style";

    private static readonly string[] Keys = { HudScale, UnpinAll, Position, DragHint, KeysHint, Done, Default, Opacity, MaxPinned, SizeHint, TextOpacity, GameStyle };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Card size in runs", "Unpin all", "Card position",
            "Drag the cards with the mouse", "Enter or right click — done · R — default position", "Done", "Default", "Card background", "Max pinned", "Mouse wheel or the corner — size", "Text and icons", "Game-style background" },
        ["ru"] = new[] { "Размер карточек в забеге", "Открепить всё", "Положение карточек",
            "Перетащите карточки мышью", "Enter или ПКМ — готово · R — на место по умолчанию", "Готово", "По умолчанию", "Фон карточек", "Сколько можно закрепить", "Колесо мыши или уголок — размер", "Текст и значки", "Фон в стиле игры" },
        ["uk"] = new[] { "Розмір карток у забігу", "Відкріпити все", "Положення карток",
            "Перетягніть картки мишею", "Enter або ПКМ — готово · R — типове місце", "Готово", "Типово", "Фон карток", "Скільки можна закріпити", "Коліщатко миші або куточок — розмір", "Текст і значки", "Фон у стилі гри" },
        ["de"] = new[] { "Kartengröße im Lauf", "Alle lösen", "Kartenposition",
            "Ziehe die Karten mit der Maus", "Enter oder Rechtsklick — fertig · R — Standardposition", "Fertig", "Standard", "Kartenhintergrund", "Maximal angeheftet", "Mausrad oder Ecke — Größe", "Text und Symbole", "Hintergrund im Spielstil" },
        ["fr"] = new[] { "Taille des cartes en partie", "Tout désépingler", "Position des cartes",
            "Faites glisser les cartes avec la souris", "Entrée ou clic droit — terminé · R — position par défaut", "Terminé", "Par défaut", "Fond des cartes", "Épingles max.", "Molette ou coin — taille", "Texte et icônes", "Fond style jeu" },
        ["it"] = new[] { "Dimensione schede in partita", "Rimuovi tutti", "Posizione schede",
            "Trascina le schede con il mouse", "Invio o clic destro — fatto · R — posizione predefinita", "Fatto", "Predefinita", "Sfondo schede", "Massimo fissati", "Rotellina o angolo — dimensione", "Testo e icone", "Sfondo in stile gioco" },
        ["nl"] = new[] { "Kaartgrootte tijdens run", "Alles losmaken", "Kaartpositie",
            "Sleep de kaarten met de muis", "Enter of rechtsklik — klaar · R — standaardpositie", "Klaar", "Standaard", "Kaartachtergrond", "Max. vastgezet", "Muiswiel of hoek — grootte", "Tekst en pictogrammen", "Achtergrond in spelstijl" },
        ["pl"] = new[] { "Rozmiar kart w wyprawie", "Odepnij wszystko", "Położenie kart",
            "Przeciągnij karty myszą", "Enter lub PPM — gotowe · R — domyślne położenie", "Gotowe", "Domyślne", "Tło kart", "Maks. przypiętych", "Kółko myszy lub róg — rozmiar", "Tekst i ikony", "Tło w stylu gry" },
        ["pt-br"] = new[] { "Tamanho dos cartões na partida", "Desafixar tudo", "Posição dos cartões",
            "Arraste os cartões com o mouse", "Enter ou clique direito — pronto · R — posição padrão", "Pronto", "Padrão", "Fundo dos cartões", "Máximo fixados", "Roda do mouse ou canto — tamanho", "Texto e ícones", "Fundo no estilo do jogo" },
        ["pt"] = new[] { "Tamanho dos cartões na partida", "Desafixar tudo", "Posição dos cartões",
            "Arraste os cartões com o rato", "Enter ou clique direito — concluído · R — posição predefinida", "Concluído", "Predefinição", "Fundo dos cartões", "Máximo afixados", "Roda do rato ou canto — tamanho", "Texto e ícones", "Fundo ao estilo do jogo" },
        ["es"] = new[] { "Tamaño de tarjetas en partida", "Desfijar todo", "Posición de tarjetas",
            "Arrastra las tarjetas con el ratón", "Enter o clic derecho — listo · R — posición predeterminada", "Listo", "Predeterminado", "Fondo de tarjetas", "Máximo fijados", "Rueda del ratón o esquina — tamaño", "Texto e iconos", "Fondo al estilo del juego" },
        ["ja"] = new[] { "ラン中のカードサイズ", "すべて追跡解除", "カードの位置",
            "マウスでカードをドラッグ", "Enter または右クリックで完了 · R で初期位置", "完了", "初期位置", "カードの背景", "追跡できる最大数", "ホイールまたは角 — サイズ", "テキストとアイコン", "ゲーム風の背景" },
        ["ko"] = new[] { "런 중 카드 크기", "모두 추적 해제", "카드 위치",
            "마우스로 카드를 끌어 옮기세요", "Enter 또는 우클릭 — 완료 · R — 기본 위치", "완료", "기본값", "카드 배경", "최대 추적 수", "마우스 휠 또는 모서리 — 크기", "텍스트와 아이콘", "게임 스타일 배경" },
        ["zh-hans"] = new[] { "局内卡片大小", "取消全部追踪", "卡片位置",
            "用鼠标拖动卡片", "Enter 或右键 — 完成 · R — 默认位置", "完成", "默认", "卡片背景", "最多追踪", "滚轮或拖动角 — 大小", "文字和图标", "游戏风格背景" },
        ["zh-hant"] = new[] { "局內卡片大小", "取消全部追蹤", "卡片位置",
            "用滑鼠拖動卡片", "Enter 或右鍵 — 完成 · R — 預設位置", "完成", "預設", "卡片背景", "最多追蹤", "滾輪或拖動角 — 大小", "文字和圖示", "遊戲風格背景" },
        ["th"] = new[] { "ขนาดการ์ดระหว่างรอบ", "เลิกปักหมุดทั้งหมด", "ตำแหน่งการ์ด",
            "ลากการ์ดด้วยเมาส์", "Enter หรือคลิกขวา — เสร็จ · R — ตำแหน่งเริ่มต้น", "เสร็จ", "ค่าเริ่มต้น", "พื้นหลังการ์ด", "ปักหมุดได้สูงสุด", "ล้อเมาส์หรือมุม — ขนาด", "ข้อความและไอคอน", "พื้นหลังแบบในเกม" },
    };

    private static readonly Localizer Text = new(Keys, Table, () => PinnedChallengesMod.Log);

    public static string Get(string key) => Text.Get(key);

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key) => Text.Bind(text, key);
}
