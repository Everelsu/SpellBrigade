using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace RunCustomizer;

// Тексты мода на языке игры. Язык берётся из настроек игры и меняется
// на лету вместе с ней; если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string RandomName = "rn", RandomDesc = "rd", CustomName = "cn", CustomDesc = "cd",
                        BaseName = "bn", BaseDesc = "bd", Enemy = "enemy", Spawn = "spawn", Count = "count", Health = "health",
                        RandomWizard = "rw", SurpriseWizard = "sw", WizardAlias = "w_alias",
                        WizardDescInstant = "w_instant", WizardDescSurprise = "w_surprise",
                        RandomKind = "kind", KindAll = "kind_all", KindBase = "kind_base", WizardChosen = "w_chosen";

    private static readonly string[] Keys =
        { RandomName, RandomDesc, CustomName, CustomDesc, BaseName, BaseDesc, Enemy, Spawn, Count, Health,
          RandomWizard, SurpriseWizard, WizardAlias, WizardDescInstant, WizardDescSurprise, RandomKind, KindAll, KindBase, WizardChosen };

    // порядок значений — как в Keys; языка нет в таблице — английский
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[]
        {
            "Random", "Every setting is rolled on its own, from easier than the easiest difficulty to harsher than the hardest. You'll see what you got when the run starts.",
            "Custom", "Set the difficulty yourself. The gold bonus follows the resulting difficulty.",
            "Random", "One of the difficulties you've unlocked is picked at random when the run starts.",
            "Enemy Strength", "Spawn Speed", "Enemy Count", "Health Drops",
            "Random Wizard", "Surprise Wizard", "Any of your unlocked wizards",
            "Click it and the roulette picks a random unlocked wizard for you.",
            "A random wizard is picked when the run starts. Until then the lobby shows your previous wizard — only you see random ones flicker on your seat.",
            "What's random", "Every setting", "Base difficulty", "Picked — you'll see who it is when the run starts",
        },
        ["ru"] = new[]
        {
            "Случайная", "Каждый параметр выпадает отдельно — от легче самой лёгкой угрозы до жёстче самой сильной. Что выпало, узнаете в начале забега.",
            "Своя", "Настройте сложность сами. Бонус золота — по итоговой сложности.",
            "Случайная", "В начале забега случайно выпадет одна из открытых сложностей.",
            "Сила врагов", "Скорость появления", "Число врагов", "Аптечки",
            "Случайный волшебник", "Волшебник-сюрприз", "Любой из открытых волшебников",
            "Нажмите — рулетка выберет случайного открытого волшебника.",
            "Случайный волшебник выпадет в начале забега. До тех пор в лобби стоит прежний — только у вас на месте мелькают случайные.",
            "Что выпадает", "Каждый параметр", "Базовая сложность", "Выбран — узнаете, кто это, в начале забега",
        },
        ["uk"] = new[]
        {
            "Випадкова", "Кожен параметр випадає окремо — від легшого за найлегшу загрозу до жорсткішого за найсильнішу. Що випало, дізнаєтеся на початку забігу.",
            "Своя", "Налаштуйте складність самі. Бонус золота — за підсумковою складністю.",
            "Випадкова", "На початку забігу випадково випаде одна з відкритих складностей.",
            "Сила ворогів", "Швидкість появи", "Кількість ворогів", "Аптечки",
            "Випадковий чарівник", "Чарівник-сюрприз", "Будь-який із відкритих чарівників",
            "Натисніть — рулетка обере випадкового відкритого чарівника.",
            "Випадковий чарівник випаде на початку забігу. До того в лобі стоїть попередній — лише у вас на місці миготять випадкові.",
            "Що випадає", "Кожен параметр", "Базова складність", "Обрано — дізнаєтеся, хто це, на початку забігу",
        },
        ["de"] = new[]
        {
            "Zufällig", "Jeder Wert wird einzeln ausgewürfelt – von leichter als die leichteste bis härter als die schwerste Schwierigkeit. Was du bekommst, siehst du zu Beginn des Durchlaufs.",
            "Eigene", "Stelle die Schwierigkeit selbst ein. Der Goldbonus richtet sich nach der Gesamtschwierigkeit.",
            "Zufällig", "Zu Beginn des Durchlaufs wird eine deiner freigeschalteten Schwierigkeiten zufällig gewählt.",
            "Gegnerstärke", "Spawn-Tempo", "Gegneranzahl", "Heiltränke",
            "Zufälliger Magier", "Überraschungsmagier", "Einer deiner freigeschalteten Magier",
            "Klicke – das Roulette wählt einen zufälligen freigeschalteten Magier für dich.",
            "Ein zufälliger Magier wird zu Beginn des Durchlaufs gewählt. Bis dahin zeigt die Lobby deinen bisherigen Magier – nur du siehst auf deinem Platz zufällige aufblitzen.",
            "Was ist zufällig", "Jeder Wert", "Grundschwierigkeit", "Gewählt – wer es ist, siehst du zu Beginn des Durchlaufs",
        },
        ["fr"] = new[]
        {
            "Aléatoire", "Chaque paramètre est tiré séparément, de plus facile que la difficulté la plus basse à plus dur que la plus haute. Vous découvrirez le résultat au début de la partie.",
            "Personnalisée", "Réglez la difficulté vous-même. Le bonus d'or suit la difficulté obtenue.",
            "Aléatoire", "Une des difficultés débloquées est tirée au sort au début de la partie.",
            "Force des ennemis", "Vitesse d'apparition", "Nombre d'ennemis", "Soins",
            "Mage aléatoire", "Mage surprise", "N'importe lequel de vos mages débloqués",
            "Cliquez et la roulette choisit pour vous un mage débloqué au hasard.",
            "Un mage est tiré au sort au début de la partie. D'ici là, le salon affiche votre mage précédent — vous seul voyez des mages au hasard défiler à votre place.",
            "Ce qui est aléatoire", "Chaque paramètre", "Difficulté de base", "Choisi — vous saurez qui c'est au début de la partie",
        },
        ["it"] = new[]
        {
            "Casuale", "Ogni parametro viene estratto a parte, da più facile della difficoltà più bassa a più duro della più alta. Scoprirai cosa è uscito all'inizio della partita.",
            "Personalizzata", "Imposta tu la difficoltà. Il bonus d'oro segue la difficoltà risultante.",
            "Casuale", "All'inizio della partita viene estratta una delle difficoltà sbloccate.",
            "Forza nemici", "Velocità di comparsa", "Numero di nemici", "Cure",
            "Mago casuale", "Mago sorpresa", "Uno qualsiasi dei tuoi maghi sbloccati",
            "Clicca e la roulette sceglie per te un mago sbloccato a caso.",
            "Il mago viene estratto all'inizio della partita. Fino ad allora la lobby mostra il mago precedente: solo tu vedi maghi casuali alternarsi al tuo posto.",
            "Cosa è casuale", "Ogni parametro", "Difficoltà base", "Scelto: scoprirai chi è all'inizio della partita",
        },
        ["nl"] = new[]
        {
            "Willekeurig", "Elke waarde wordt apart geloot, van makkelijker dan de makkelijkste tot zwaarder dan de zwaarste moeilijkheid. Wat je krijgt, zie je aan het begin van de run.",
            "Aangepast", "Stel de moeilijkheid zelf in. De goudbonus volgt de uiteindelijke moeilijkheid.",
            "Willekeurig", "Aan het begin van de run wordt een van je ontgrendelde moeilijkheden geloot.",
            "Vijandkracht", "Spawnsnelheid", "Aantal vijanden", "Genezing",
            "Willekeurige tovenaar", "Verrassingstovenaar", "Een van je ontgrendelde tovenaars",
            "Klik en de roulette kiest een willekeurige ontgrendelde tovenaar voor je.",
            "De tovenaar wordt aan het begin van de run geloot. Tot dan toont de lobby je vorige tovenaar — alleen jij ziet willekeurige tovenaars op je plek flitsen.",
            "Wat is willekeurig", "Elke waarde", "Basismoeilijkheid", "Gekozen — wie het is, zie je aan het begin van de run",
        },
        ["pl"] = new[]
        {
            "Losowy", "Każdy parametr losowany jest osobno — od łatwiejszego niż najniższy poziom do trudniejszego niż najwyższy. Co wypadło, zobaczysz na początku wyprawy.",
            "Własny", "Ustaw poziom trudności sam. Premia złota zależy od końcowej trudności.",
            "Losowy", "Na początku wyprawy losowany jest jeden z odblokowanych poziomów trudności.",
            "Siła wrogów", "Tempo pojawiania", "Liczba wrogów", "Apteczki",
            "Losowy mag", "Mag-niespodzianka", "Dowolny z odblokowanych magów",
            "Kliknij, a ruletka wybierze losowego odblokowanego maga.",
            "Mag zostanie wylosowany na początku wyprawy. Do tego czasu w poczekalni stoi poprzedni — tylko ty widzisz, jak na twoim miejscu migają losowi magowie.",
            "Co jest losowe", "Każdy parametr", "Poziom bazowy", "Wybrano — kto to, zobaczysz na początku wyprawy",
        },
        ["pt-br"] = new[]
        {
            "Aleatória", "Cada parâmetro é sorteado separadamente, de mais fácil que a menor dificuldade a mais difícil que a maior. Você verá o resultado no início da partida.",
            "Personalizada", "Ajuste a dificuldade você mesmo. O bônus de ouro segue a dificuldade resultante.",
            "Aleatória", "Uma das dificuldades desbloqueadas é sorteada no início da partida.",
            "Força dos inimigos", "Velocidade de surgimento", "Quantidade de inimigos", "Curas",
            "Mago aleatório", "Mago surpresa", "Qualquer um dos seus magos desbloqueados",
            "Clique e a roleta escolhe um mago desbloqueado aleatório para você.",
            "Um mago é sorteado no início da partida. Até lá, o lobby mostra seu mago anterior — só você vê magos aleatórios piscando no seu lugar.",
            "O que é aleatório", "Cada parâmetro", "Dificuldade base", "Escolhido — você saberá quem é no início da partida",
        },
        ["pt"] = new[]
        {
            "Aleatória", "Cada parâmetro é sorteado em separado, de mais fácil do que a dificuldade mais baixa a mais difícil do que a mais alta. Verá o resultado no início da partida.",
            "Personalizada", "Defina a dificuldade a seu gosto. O bónus de ouro acompanha a dificuldade resultante.",
            "Aleatória", "No início da partida é sorteada uma das dificuldades desbloqueadas.",
            "Força dos inimigos", "Velocidade de aparecimento", "Número de inimigos", "Curas",
            "Mago aleatório", "Mago surpresa", "Qualquer um dos seus magos desbloqueados",
            "Clique e a roleta escolhe um mago desbloqueado ao acaso.",
            "O mago é sorteado no início da partida. Até lá, o lobby mostra o seu mago anterior — só você vê magos aleatórios a piscar no seu lugar.",
            "O que é aleatório", "Cada parâmetro", "Dificuldade base", "Escolhido — saberá quem é no início da partida",
        },
        ["es"] = new[]
        {
            "Aleatoria", "Cada parámetro se sortea por separado, desde más fácil que la dificultad más baja hasta más duro que la más alta. Verás el resultado al empezar la partida.",
            "Personalizada", "Ajusta la dificultad tú mismo. La bonificación de oro sigue a la dificultad resultante.",
            "Aleatoria", "Al empezar la partida se sortea una de las dificultades desbloqueadas.",
            "Fuerza enemiga", "Velocidad de aparición", "Cantidad de enemigos", "Curación",
            "Mago aleatorio", "Mago sorpresa", "Cualquiera de tus magos desbloqueados",
            "Haz clic y la ruleta elegirá un mago desbloqueado al azar.",
            "El mago se sortea al empezar la partida. Hasta entonces, la sala muestra tu mago anterior; solo tú ves magos al azar parpadear en tu sitio.",
            "Qué es aleatorio", "Cada parámetro", "Dificultad base", "Elegido: sabrás quién es al empezar la partida",
        },
        ["ja"] = new[]
        {
            "ランダム", "各項目が個別に抽選されます。最も易しい難易度より易しいものから、最も難しい難易度より厳しいものまで。結果はラン開始時に分かります。",
            "カスタム", "難易度を自分で設定します。ゴールドボーナスは最終的な難易度に応じて決まります。",
            "ランダム", "ラン開始時に、解放済みの難易度からランダムに1つ選ばれます。",
            "敵の強さ", "出現速度", "敵の数", "回復アイテム",
            "ランダム魔法使い", "サプライズ魔法使い", "解放済みの魔法使いからどれか",
            "クリックするとルーレットが解放済みの魔法使いをランダムに選びます。",
            "魔法使いはラン開始時にランダムで決まります。それまでロビーには前の魔法使いが表示され、あなたの席でだけランダムな魔法使いが切り替わります。",
            "ランダムの対象", "各項目", "基本難易度", "選択済み — 誰になるかはラン開始時に分かります",
        },
        ["ko"] = new[]
        {
            "무작위", "각 항목이 따로 뽑힙니다. 가장 쉬운 난이도보다 쉬운 값부터 가장 어려운 난이도보다 가혹한 값까지. 결과는 런이 시작될 때 알 수 있습니다.",
            "사용자 지정", "난이도를 직접 설정하세요. 골드 보너스는 최종 난이도를 따릅니다.",
            "무작위", "런이 시작될 때 해금한 난이도 중 하나가 무작위로 선택됩니다.",
            "적 강도", "출현 속도", "적 수", "회복 아이템",
            "무작위 마법사", "깜짝 마법사", "해금한 마법사 중 아무나",
            "클릭하면 룰렛이 해금한 마법사 중 하나를 무작위로 골라 줍니다.",
            "마법사는 런이 시작될 때 무작위로 정해집니다. 그때까지 로비에는 이전 마법사가 보이며, 내 자리에서만 무작위 마법사가 번갈아 나타납니다.",
            "무작위 대상", "각 항목", "기본 난이도", "선택됨 — 누구인지는 런이 시작될 때 알 수 있습니다",
        },
        ["zh-hans"] = new[]
        {
            "随机", "每项数值分别随机，从比最低难度更简单到比最高难度更严酷。结果在对局开始时揭晓。",
            "自定义", "自行设置难度。金币加成随最终难度而定。",
            "随机", "对局开始时，从已解锁的难度中随机选择一个。",
            "敌人强度", "刷新速度", "敌人数量", "治疗掉落",
            "随机法师", "惊喜法师", "任一已解锁的法师",
            "点击后，轮盘会为你随机选出一位已解锁的法师。",
            "法师将在对局开始时随机决定。在此之前大厅显示你之前的法师——只有你会看到自己座位上随机法师轮流闪现。",
            "随机内容", "每项数值", "基础难度", "已选择——对局开始时揭晓是谁",
        },
        ["zh-hant"] = new[]
        {
            "隨機", "每項數值分別隨機，從比最低難度更簡單到比最高難度更嚴酷。結果在對局開始時揭曉。",
            "自訂", "自行設定難度。金幣加成隨最終難度而定。",
            "隨機", "對局開始時，從已解鎖的難度中隨機選擇一個。",
            "敵人強度", "出現速度", "敵人數量", "治療掉落",
            "隨機法師", "驚喜法師", "任一已解鎖的法師",
            "點擊後，輪盤會為你隨機選出一位已解鎖的法師。",
            "法師將在對局開始時隨機決定。在此之前大廳顯示你之前的法師——只有你會看到自己座位上隨機法師輪流閃現。",
            "隨機內容", "每項數值", "基礎難度", "已選擇——對局開始時揭曉是誰",
        },
        ["th"] = new[]
        {
            "สุ่ม", "แต่ละค่าจะถูกสุ่มแยกกัน ตั้งแต่ง่ายกว่าระดับที่ง่ายที่สุดไปจนถึงโหดกว่าระดับที่ยากที่สุด คุณจะรู้ผลเมื่อเริ่มรอบ",
            "กำหนดเอง", "ตั้งค่าความยากด้วยตัวเอง โบนัสทองจะเป็นไปตามความยากที่ได้",
            "สุ่ม", "เมื่อเริ่มรอบ จะสุ่มหนึ่งในระดับความยากที่ปลดล็อกแล้ว",
            "ความแข็งแกร่งของศัตรู", "ความเร็วการเกิด", "จำนวนศัตรู", "ยาฟื้นพลัง",
            "นักเวทย์สุ่ม", "นักเวทย์เซอร์ไพรส์", "นักเวทย์คนไหนก็ได้ที่ปลดล็อกแล้ว",
            "คลิกแล้ววงล้อจะสุ่มเลือกนักเวทย์ที่ปลดล็อกแล้วให้คุณ",
            "นักเวทย์จะถูกสุ่มเมื่อเริ่มรอบ ระหว่างนั้นล็อบบี้จะแสดงนักเวทย์คนเดิม — มีเพียงคุณที่เห็นนักเวทย์สุ่มสลับไปมาที่ที่นั่งของคุณ",
            "อะไรที่สุ่ม", "ทุกค่า", "ความยากพื้นฐาน", "เลือกแล้ว — จะรู้ว่าเป็นใครเมื่อเริ่มรอบ",
        },
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
        return i >= 0 && i < _current.Length ? _current[i] : key;
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
        catch (Exception e) { RunCustomizerMod.Log.Warning($"[lang] can't follow language changes: {e.Message}"); }
    }

    private static void Refresh()
    {
        TMP_Text sample = null;
        Bound.RemoveAll(b => b.text == null);
        foreach (var b in Bound) { sample = b.text; break; }
        _current = Resolve(sample);
        foreach (var (text, key) in Bound) text.text = Get(key);
        foreach (var listener in Listeners)
            try { listener(); } catch (Exception e) { RunCustomizerMod.Log.Warning($"[lang] {e.Message}"); }
    }

    private static string[] Resolve(TMP_Text fontSample)
    {
        string code = "en";
        try { code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en"; } catch { }
        string key = TableKey(code);
        if (!Table.ContainsKey(key)) key = "en";
        var table = Table[key];
        if (key != "en" && fontSample != null && !FontHasAll(fontSample, key, table))
        {
            RunCustomizerMod.Log.Warning($"[lang] font lacks glyphs for '{code}', using English");
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
