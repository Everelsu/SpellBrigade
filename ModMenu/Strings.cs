using System;
using System.Collections.Generic;
using Il2CppTMPro;

namespace SpellBrigade.ModMenu;

// Тексты меню на всех языках игры. Язык берётся из настроек игры и меняется
// на лету вместе с ней; если в шрифте нет нужных символов — английский.
internal static class Strings
{
    public const string Tab = "tab", Mod = "mod", On = "on", Off = "off", Search = "search", NothingFound = "none",
                        Defaults = "defaults";

    private static readonly string[] Keys = { Tab, Mod, On, Off, Search, NothingFound, Defaults };

    // порядок значений — как в Keys
    private static readonly Dictionary<string, string[]> Table = new()
    {
        ["en"] = new[] { "Mods", "Mod", "On", "Off", "Search", "Nothing found", "Defaults" },
        ["ru"] = new[] { "Моды", "Мод", "Вкл", "Выкл", "Поиск", "Ничего не найдено", "По умолчанию" },
        ["uk"] = new[] { "Моди", "Мод", "Увімк", "Вимк", "Пошук", "Нічого не знайдено", "Типово" },
        ["de"] = new[] { "Mods", "Mod", "An", "Aus", "Suchen", "Nichts gefunden", "Standard" },
        ["fr"] = new[] { "Mods", "Mod", "Activé", "Désactivé", "Rechercher", "Aucun résultat", "Par défaut" },
        ["it"] = new[] { "Mod", "Mod", "Attivo", "Disattivo", "Cerca", "Nessun risultato", "Predefiniti" },
        ["nl"] = new[] { "Mods", "Mod", "Aan", "Uit", "Zoeken", "Niets gevonden", "Standaard" },
        ["pl"] = new[] { "Mody", "Mod", "Wł.", "Wył.", "Szukaj", "Nic nie znaleziono", "Domyślne" },
        ["pt-br"] = new[] { "Mods", "Mod", "Ligado", "Desligado", "Pesquisar", "Nada encontrado", "Padrão" },
        ["pt"] = new[] { "Mods", "Mod", "Ligado", "Desligado", "Pesquisar", "Nada encontrado", "Predefinições" },
        ["es"] = new[] { "Mods", "Mod", "Activado", "Desactivado", "Buscar", "No se encontró nada", "Predeterminado" },
        ["ja"] = new[] { "MOD", "MOD", "オン", "オフ", "検索", "見つかりません", "初期設定" },
        ["ko"] = new[] { "모드", "모드", "켜기", "끄기", "검색", "결과 없음", "기본값" },
        ["zh-hans"] = new[] { "模组", "模组", "开", "关", "搜索", "未找到", "默认" },
        ["zh-hant"] = new[] { "模組", "模組", "開", "關", "搜尋", "未找到", "預設" },
        ["th"] = new[] { "ม็อด", "ม็อด", "เปิด", "ปิด", "ค้นหา", "ไม่พบ", "ค่าเริ่มต้น" },
    };

    private static readonly Shared.Localizer Text = new(Keys, Table, () => ModMenuMod.Log);

    public static string Get(string key) => Text.Get(key);

    // Текст, который сам обновится при смене языка
    public static void Bind(TMP_Text text, string key) => Text.Bind(text, key);

    public static void OnLanguageChanged(Action listener) => Text.OnLanguageChanged(listener);
}
