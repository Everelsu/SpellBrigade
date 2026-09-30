using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HoldToDie;

// Без Keybinds Unlocked игрок иначе не узнает свою клавишу: добавляем строку «Умереть
// (удерживать)» в родной экран «Настройки → Управление» — под «Быстрой статистикой»,
// в обе колонки (клавиатура и контроллер), с иконками игры. С Keybinds Unlocked этот экран
// заменяет его список, а клавиша живёт там.
internal static class ControlsRow
{
    private const string RowName = "HoldToDie_Row";

    // Иконки есть только у клавиш, которые занимает сама игра (key_pc_k, key_xbox_L нет).
    // Тогда берём родную клавишу из строки-шаблона, закрываем её символ заливкой цвета
    // клавиши и пишем свой — рамка остаётся игровой.
    private static readonly Color KeyFill = new(0.149f, 0.125f, 0.188f); // #262030, как внутри иконок игры

    private sealed class Cell
    {
        public Image Icon, Cover;
        public Sprite Template;
        public TMP_Text Caption;
        public bool Gamepad;
    }

    private static readonly List<Cell> Cells = new();
    private static readonly Dictionary<string, Sprite> SpriteCache = new();
    private static string _shownKey, _shownPad;
    private static float _nextCheck;

    // Постфикс SettingsPanel.SetupSubPanels (главное меню и пауза)
    public static void InjectPostfix(SettingsPanel __instance)
    {
        try
        {
            if (Death.KeybindsInstalled) return;
            var subs = __instance.subPanels;
            if (subs == null) return;
            for (int i = 0; i < subs.Count; i++)
            {
                var panel = subs[i].Panel;
                if (panel == null || !panel.name.StartsWith("Controls")) continue;
                // KeyboardLayout_Panel и колонка контроллера рядом — у обеих внутри Prompts_Panel
                var controls = panel.transform;
                for (int c = 0; c < controls.childCount; c++)
                {
                    var column = controls.GetChild(c);
                    if (!column.name.Contains("Layout_Panel")) continue;
                    AddRow(column, "Prompts_Panel", !column.name.Contains("Keyboard"));
                }
            }
            _shownKey = _shownPad = null; // иконки выставит Tick
        }
        catch (Exception e) { HoldToDieMod.Log.Warning($"can't add the key to Options → Controls: {e.Message}"); }
    }

    private static void AddRow(Transform controls, string path, bool gamepad)
    {
        var prompts = controls.Find(path);
        if (prompts == null || prompts.childCount == 0 || prompts.Find(RowName) != null) return;

        // шаблон — последняя строка («Быстрая статистика»): подпись и одна иконка
        var template = prompts.GetChild(prompts.childCount - 1).gameObject;
        var row = Object.Instantiate(template, prompts);
        row.name = RowName;

        TMP_Text label = null;
        Image icon = null;
        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
            if (label == null) label = text;
        foreach (var image in row.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || !image.sprite.name.StartsWith("key_", StringComparison.OrdinalIgnoreCase)) continue;
            if (icon == null) icon = image;
            else image.gameObject.SetActive(false); // у шаблона могло быть несколько иконок
        }
        if (label == null || icon == null)
        {
            Object.Destroy(row);
            HoldToDieMod.Log.Warning($"Options → Controls: unexpected layout in {controls.name}/{path}, key row skipped");
            return;
        }

        foreach (var loc in row.GetComponentsInChildren<LocalizeStringEvent>(true)) Object.Destroy(loc);
        Strings.Bind(label, Strings.DieHold);

        // заливка поверх символа родной клавиши (внутренняя тёмная часть — примерно 11–89 %)
        var cover = new GameObject("Cover").AddComponent<Image>();
        var cr = cover.rectTransform;
        cr.SetParent(icon.transform, false);
        cr.anchorMin = new Vector2(0.11f, 0.11f); cr.anchorMax = new Vector2(0.89f, 0.89f);
        cr.offsetMin = cr.offsetMax = Vector2.zero;
        cover.color = KeyFill;
        cover.raycastTarget = false;

        var caption = Object.Instantiate(label.gameObject, icon.transform).GetComponent<TMP_Text>();
        foreach (var loc in caption.GetComponents<LocalizeStringEvent>()) Object.Destroy(loc);
        var tr = caption.rectTransform;
        tr.anchorMin = cr.anchorMin; tr.anchorMax = cr.anchorMax;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        caption.alignment = TextAlignmentOptions.Center;
        caption.fontStyle = FontStyles.Bold;
        caption.color = Color.white;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 6f;
        caption.fontSizeMax = label.fontSize * 0.7f;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.margin = Vector4.zero;

        Cells.Add(new Cell { Icon = icon, Cover = cover, Template = icon.sprite, Caption = caption, Gamepad = gamepad });
        HoldToDieMod.Log.Msg($"Options → Controls: key row added ({(gamepad ? "controller" : "keyboard")}, template {template.name} / {icon.sprite.name})");
    }

    // Клавишу поменяли в Mod Menu — обновляем иконки (проверка пару раз в секунду, дёшево)
    public static void Tick()
    {
        if (Cells.Count == 0 || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;
        Cells.RemoveAll(c => c.Icon == null);

        string key = HoldToDieMod.KeyEntry.Value, pad = HoldToDieMod.GamepadEntry.Value;
        if (key == _shownKey && pad == _shownPad) return;
        _shownKey = key;
        _shownPad = pad;

        foreach (var cell in Cells)
        {
            string name = cell.Gamepad ? Death.GamepadSprite(pad) : "key_pc_" + key.ToLowerInvariant();
            string caption = cell.Gamepad ? Death.GamepadName(pad) : key;
            var sprite = name == null ? null : FindSprite(name, cell.Template);
            cell.Icon.gameObject.SetActive(caption != null); // кнопка «Нет» — пустое место
            bool own = sprite != null;
            cell.Icon.sprite = own ? sprite : cell.Template;
            // у контроллера шаблон — кнопка другой формы: без иконки просто пишем «L3»
            cell.Icon.enabled = own || !cell.Gamepad;
            cell.Cover.gameObject.SetActive(!own && !cell.Gamepad);
            cell.Caption.gameObject.SetActive(!own);
            cell.Caption.text = caption ?? "";
            if (cell.Gamepad && !own)
            {
                var tr = cell.Caption.rectTransform; // подпись на всю клавишу, а не на её середину
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            }
        }
    }

    // Спрайт игры по имени (key_pc_k, key_xbox_L…): загруженный или из атласа.
    // Одноимённых бывает несколько (меню, HUD) — берём с той же текстуры, что у шаблона.
    private static Sprite FindSprite(string name, Sprite reference)
    {
        if (SpriteCache.TryGetValue(name, out var cached) && cached != null) return cached;
        Sprite found = null;
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (s == null || !s.name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            found ??= s;
            if (reference != null && s.texture == reference.texture) { found = s; break; }
        }
        if (found == null)
            foreach (var atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
            {
                try { found = atlas.GetSprite(name); } catch { }
                if (found != null) { found.hideFlags = HideFlags.DontUnloadUnusedAsset; break; }
            }
        if (found != null) SpriteCache[name] = found;
        return found;
    }
}
