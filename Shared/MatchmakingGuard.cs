using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Localization.Components;
using Object = UnityEngine.Object;

namespace SpellBrigade.Shared;

// Моды, которые меняют забег для всех (сложность, опыт команды, размер лобби), не должны
// сводить игроков с теми, у кого их нет: у тех ломаются значения. Пока такой мод активен,
// подбор игроков заблокирован намертво: забег идёт только с теми, кто уже в лобби, —
// друзьями по приглашению или по коду лобби.
//
// Как держим блокировку. Флаг подбора (MatchmakingPreferencesManager.IsMatchmakingEnabled)
// игра читает только в двух местах: RunPreparationManager.Prepare — там она «добирает
// отряд» из чужих лобби, — и окно настроек подбора. Приглашения и код лобби его не читают.
// • Prepare: на время вызова флаг «выкл» — забег идёт с теми, кто уже в лобби;
// • окно: переключатель «Подбор игроков» не реагирует на нажатия, вместо настроек поиска —
//   объяснение: какой мод мешает, почему и что сделать;
// • CanUseMatchmaking отвечает «нет» — экраны выбора мира/сложности не ждут подбора.
// Сохранённая настройка игры не трогается вовсе. (UpdatePreference не хукаем: у него пять
// Nullable<bool>-параметров, и трамплин Il2CppInterop на них падает.)
//
// Подключают моды, которые влияют на других: Register(...) с функцией причины (текст на
// языке игры или null, если сейчас не мешает). Хуки ставит первый из модов; причины всех
// модов лежат в общем списке AppDomain.
internal static class MatchmakingGuard
{
    private const string BlockersKey = "SpellBrigade.Matchmaking.Blockers"; // List<Func<string>>: «<b>Мод</b>: причина» или null
    private const string OwnerKey = "SpellBrigade.Matchmaking.Owner";
    private const string PanelName = "SpellBrigade_MatchmakingLock";
    private static readonly Color Gold = new(0.96f, 0.78f, 0.35f);

    private static bool _owner, _wasBlocked;
    private static MelonLogger.Instance _log;
    private static MatchmakingPreferencesModal _modal;
    private static TMP_Text _title, _body;
    private static float _pulse, _nextCheck;

    private const string Title = "title", Intro = "intro", Footer = "footer";
    private static readonly Localizer Text = new(new[] { Title, Intro, Footer }, new Dictionary<string, string[]>
    {
        ["en"] = new[] { "Matchmaking is locked by mods",
            "These mods change the game for everyone in the lobby, so players without them can't be matched with you:",
            "Play with friends: invite them or share the lobby code. Matchmaking comes back as soon as nothing above applies." },
        ["ru"] = new[] { "Подбор игроков заблокирован модами",
            "Эти моды меняют игру для всех в лобби, поэтому игроков без них к вам не подберёт:",
            "Играйте с друзьями: пригласите их или дайте код лобби. Подбор вернётся сам, как только ничего из списка не будет мешать." },
        ["uk"] = new[] { "Підбір гравців заблоковано модами",
            "Ці моди змінюють гру для всіх у лобі, тому гравців без них до вас не підбере:",
            "Грайте з друзями: запросіть їх або дайте код лобі. Підбір повернеться сам, щойно нічого зі списку не заважатиме." },
        ["de"] = new[] { "Matchmaking ist durch Mods gesperrt",
            "Diese Mods verändern das Spiel für alle in der Lobby, daher kannst du nicht mit Spielern ohne sie zusammengebracht werden:",
            "Spiele mit Freunden: Lade sie ein oder teile den Lobby-Code. Matchmaking kommt zurück, sobald nichts davon mehr zutrifft." },
        ["fr"] = new[] { "Matchmaking bloqué par des mods",
            "Ces mods modifient la partie pour tout le salon, vous ne pouvez donc pas être associé à des joueurs qui ne les ont pas :",
            "Jouez avec des amis : invitez-les ou partagez le code du salon. Le matchmaking revient dès que plus rien ci-dessus ne s'applique." },
        ["it"] = new[] { "Matchmaking bloccato dalle mod",
            "Queste mod cambiano la partita per tutta la lobby, quindi non puoi essere abbinato a chi non le ha:",
            "Gioca con gli amici: invitali o condividi il codice della lobby. Il matchmaking torna appena nulla di tutto ciò si applica." },
        ["nl"] = new[] { "Matchmaking geblokkeerd door mods",
            "Deze mods veranderen het spel voor iedereen in de lobby, dus je wordt niet gekoppeld aan spelers zonder deze mods:",
            "Speel met vrienden: nodig ze uit of deel de lobbycode. Matchmaking komt terug zodra niets hiervan meer geldt." },
        ["pl"] = new[] { "Dobieranie graczy zablokowane przez mody",
            "Te mody zmieniają grę wszystkim w lobby, więc gracze bez nich nie zostaną do ciebie dobrani:",
            "Graj ze znajomymi: zaproś ich lub podaj kod lobby. Dobieranie wróci samo, gdy nic z listy nie będzie przeszkadzać." },
        ["pt-br"] = new[] { "Matchmaking bloqueado por mods",
            "Estes mods mudam o jogo para todos no lobby, então jogadores sem eles não serão pareados com você:",
            "Jogue com amigos: convide-os ou compartilhe o código do lobby. O matchmaking volta assim que nada acima se aplicar." },
        ["pt"] = new[] { "Matchmaking bloqueado por mods",
            "Estes mods mudam o jogo para todos no lobby, por isso jogadores sem eles não serão emparelhados contigo:",
            "Joga com amigos: convida-os ou partilha o código do lobby. O matchmaking volta assim que nada acima se aplicar." },
        ["es"] = new[] { "Emparejamiento bloqueado por mods",
            "Estos mods cambian la partida para toda la sala, así que no se te emparejará con jugadores que no los tengan:",
            "Juega con amigos: invítalos o comparte el código de sala. El emparejamiento vuelve en cuanto nada de lo anterior aplique." },
        ["es-mx"] = new[] { "Emparejamiento bloqueado por mods",
            "Estos mods cambian la partida para toda la sala, así que no se te emparejará con jugadores que no los tengan:",
            "Juega con amigos: invítalos o comparte el código de sala. El emparejamiento regresa en cuanto nada de lo anterior aplique." },
        ["ja"] = new[] { "MODによりマッチングがロックされています",
            "これらのMODはロビー全員のゲームを変えるため、MODのないプレイヤーとはマッチングしません：",
            "フレンドと遊びましょう：招待するかロビーコードを共有してください。上記が当てはまらなくなればマッチングは戻ります。" },
        ["ko"] = new[] { "모드로 인해 매칭이 잠겼습니다",
            "이 모드들은 로비 전체의 게임을 바꾸므로, 모드가 없는 플레이어와는 매칭되지 않습니다:",
            "친구와 플레이하세요: 초대하거나 로비 코드를 공유하세요. 위 항목이 모두 해당되지 않으면 매칭이 다시 열립니다." },
        ["zh-hans"] = new[] { "匹配已被模组锁定",
            "这些模组会改变大厅里所有人的游戏，因此不会为你匹配没有这些模组的玩家：",
            "和好友一起玩：邀请他们或分享大厅代码。只要上面各项都不再适用，匹配就会恢复。" },
        ["zh-hant"] = new[] { "配對已被模組鎖定",
            "這些模組會改變大廳裡所有人的遊戲，因此不會為你配對沒有這些模組的玩家：",
            "和好友一起玩：邀請他們或分享大廳代碼。只要上面各項都不再適用，配對就會恢復。" },
        ["th"] = new[] { "การจับคู่ถูกล็อกโดยม็อด",
            "ม็อดเหล่านี้เปลี่ยนเกมของทุกคนในล็อบบี้ จึงจะไม่จับคู่คุณกับผู้เล่นที่ไม่มีม็อด:",
            "เล่นกับเพื่อน: เชิญเพื่อนหรือแชร์รหัสล็อบบี้ การจับคู่จะกลับมาเมื่อไม่มีข้อใดด้านบนเข้าเงื่อนไข" },
    }, () => _log);

    // reason: текст на языке игры, почему мод сейчас мешает подбору, или null — не мешает
    public static void Register(HarmonyLib.Harmony harmony, MelonLogger.Instance log, string modName, Func<string> reason)
    {
        var domain = AppDomain.CurrentDomain;
        if (domain.GetData(BlockersKey) is not List<Func<string>> list)
            domain.SetData(BlockersKey, list = new List<Func<string>>());
        list.Add(() => reason() is { } why ? $"<b>{modName}</b> — {why}" : null);

        if (domain.GetData(OwnerKey) is true) return;
        domain.SetData(OwnerKey, true);
        _owner = true;
        _log = log;
        int ok = 0;
        ok += Hook(harmony, typeof(RunPreparationManager), nameof(RunPreparationManager.Prepare), nameof(PreparePrefix), nameof(PreparePostfix));
        ok += Hook(harmony, typeof(RunPreparationManager), nameof(RunPreparationManager.CanUseMatchmaking), nameof(CanUsePrefix));
        ok += Hook(harmony, typeof(MatchmakingToggle), "Toggle", nameof(TogglePrefix));
        ok += Hook(harmony, typeof(MatchmakingPreferencesModal), "OnIsMatchmakingEnabledChanged", nameof(EnabledChangedPrefix));
        ok += Hook(harmony, typeof(MatchmakingPreferencesModal), "RefreshPanel", null, nameof(RefreshPanelPostfix));
        log.Msg($"matchmaking guard: {ok}/5 hooks");
    }

    private static int Hook(HarmonyLib.Harmony harmony, Type type, string method, string prefix, string postfix = null) =>
        Hooks.Patch(harmony, _log, type, method, typeof(MatchmakingGuard), prefix, postfix) ? 1 : 0;

    // Причины от всех модов, которые сейчас мешают (пусто — подбор свободен)
    public static List<string> Reasons()
    {
        var result = new List<string>();
        if (AppDomain.CurrentDomain.GetData(BlockersKey) is not List<Func<string>> list) return result;
        foreach (var check in list)
        {
            try { if (check() is { } why) result.Add(why); }
            catch { }
        }
        return result;
    }

    public static bool Blocked => Reasons().Count > 0;

    // --- флаг подбора у игры ---

    private static MatchmakingPreferencesManager Prefs => Singleton<MatchmakingPreferencesManager>.Instance;

    private static bool _prepareRestore;

    // Prepare читает флаг сам — на время вызова «выкл» (только в памяти, без сохранения)
    private static void PreparePrefix()
    {
        var reasons = Reasons();
        var prefs = Prefs;
        if (reasons.Count == 0 || prefs == null || !prefs.IsMatchmakingEnabled) return;
        prefs.IsMatchmakingEnabled = false;
        _prepareRestore = true;
        _log?.Msg($"run starts without matchmaking — {StripTags(string.Join("; ", reasons))}");
    }

    private static void PreparePostfix()
    {
        if (!_prepareRestore) return;
        _prepareRestore = false;
        var prefs = Prefs;
        if (prefs != null) prefs.IsMatchmakingEnabled = true;
    }

    private static bool CanUsePrefix(ref bool __result)
    {
        if (!Blocked) return true;
        __result = false;
        return false;
    }

    // --- окно настроек подбора ---

    // Нажатие на «Подбор игроков» (мышь, клавиатура, геймпад) — гасим и подсвечиваем объяснение
    private static bool TogglePrefix(MatchmakingToggle __instance)
    {
        if (!Blocked || !IsMatchmakingToggle(__instance)) return true;
        _pulse = 1f;
        return false;
    }

    private static bool EnabledChangedPrefix(MatchmakingPreferencesModal __instance, bool isOn)
    {
        if (!isOn || !Blocked) return true;
        RefreshPanelPostfix(__instance);
        return false;
    }

    private static bool IsMatchmakingToggle(MatchmakingToggle toggle) =>
        _modal != null && _modal.isMatchmakingEnabledToggle != null && _modal.isMatchmakingEnabledToggle.Pointer == toggle.Pointer;

    private static void RefreshPanelPostfix(MatchmakingPreferencesModal __instance)
    {
        try
        {
            _modal = __instance;
            Apply(__instance, Reasons());
        }
        catch (Exception e) { _log?.Warning($"matchmaking window: {e.Message}"); }
    }

    private static void Apply(MatchmakingPreferencesModal modal, List<string> reasons)
    {
        bool blocked = reasons.Count > 0;
        var toggle = modal.isMatchmakingEnabledToggle;
        if (toggle != null)
        {
            if (blocked)
            {
                toggle.isOn = false; // только вид: без события и без сохранения
                toggle.RefreshUI();
            }
            var group = toggle.GetComponent<CanvasGroup>() ?? toggle.gameObject.AddComponent<CanvasGroup>();
            group.alpha = blocked ? 0.4f : 1f;
        }
        if (blocked && modal.matchmakingPreferencesObject != null) modal.matchmakingPreferencesObject.SetActive(false);

        var panel = modal.transform.Find(PanelName);
        if (panel == null && blocked) panel = BuildPanel(modal);
        if (panel == null) return;
        panel.gameObject.SetActive(blocked);
        if (!blocked) return;

        var body = new StringBuilder();
        body.Append(Text.Get(Intro)).Append('\n');
        foreach (var why in reasons) body.Append("\n•  ").Append(why);
        body.Append("\n\n<alpha=#B3>").Append(Text.Get(Footer));
        _title.text = Text.Get(Title);
        _body.text = body.ToString();
    }

    // Объяснение на месте настроек поиска (их игра и так прячет при выключенном подборе)
    private static Transform BuildPanel(MatchmakingPreferencesModal modal)
    {
        var template = modal.transform.Find("Matchmaking_Description")?.GetComponent<TMP_Text>()
                       ?? modal.GetComponentInChildren<TMP_Text>(true);
        if (template == null) return null;

        var panel = new GameObject(PanelName).AddComponent<RectTransform>();
        panel.SetParent(modal.transform, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(250f, -1060f); // под строкой «Подбор игроков»
        panel.sizeDelta = new Vector2(2250f, 760f);

        float size = template.enableAutoSizing ? template.fontSizeMax : template.fontSize;
        _title = CloneText(template, panel, "Title", size * 1.25f, Gold, FontStyles.Bold);
        var tr = _title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0f, 1f);
        tr.sizeDelta = new Vector2(0f, size * 1.8f); tr.anchoredPosition = Vector2.zero;

        _body = CloneText(template, panel, "Body", size, Color.white, FontStyles.Normal);
        var br = _body.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.pivot = new Vector2(0f, 1f);
        br.offsetMin = Vector2.zero; br.offsetMax = new Vector2(0f, -size * 2f);
        _body.textWrappingMode = TextWrappingModes.Normal;
        _body.enableAutoSizing = true; // длинный список модов ужмётся, а не вылезет за экран
        _body.fontSizeMin = size * 0.6f;
        _body.fontSizeMax = size;
        return panel;
    }

    private static TMP_Text CloneText(TMP_Text template, Transform parent, string name, float size, Color color, FontStyles style)
    {
        var text = Object.Instantiate(template.gameObject, parent).GetComponent<TMP_Text>();
        foreach (var loc in text.GetComponents<LocalizeStringEvent>()) Object.Destroy(loc);
        foreach (var mb in text.GetComponents<MonoBehaviour>())
            if (mb != null && mb.TryCast<TMP_Text>() == null) Object.Destroy(mb); // TextReplacer и прочее — не нужны
        text.name = name;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

    // Причины могут поменяться в лобби (выбрали другую сложность) — держим флаг и окно в актуальном виде
    public static void Tick()
    {
        if (!_owner) return;
        if (_pulse > 0f && _title != null)
        {
            _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime * 2.5f);
            _title.transform.localScale = Vector3.one * (1f + 0.08f * _pulse);
            _title.color = Color.Lerp(Gold, new Color(1f, 0.45f, 0.35f), _pulse);
        }
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.25f;

        var reasons = Reasons();
        bool blocked = reasons.Count > 0;
        if (blocked != _wasBlocked)
        {
            _wasBlocked = blocked;
            _log?.Msg(blocked ? $"matchmaking locked — {StripTags(string.Join("; ", reasons))}" : "matchmaking unlocked");
            if (_modal != null && _modal.gameObject.activeInHierarchy)
            {
                try { _modal.RefreshPanel(); } catch { } // игра перерисует окно, постфикс добавит блокировку
            }
        }
        else if (blocked && _modal != null && _modal.gameObject.activeInHierarchy && _body != null)
            Apply(_modal, reasons); // текст причин мог смениться (другая сложность, другой множитель)
    }
}
