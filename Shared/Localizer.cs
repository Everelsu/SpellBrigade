using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SpellBrigade.Shared;

// Тексты мода на языке игры. Язык берётся из настроек игры и меняется на лету вместе
// с ней; если в шрифте нет нужных символов — английский. Таблица: код языка → строки
// в порядке keys; языка нет в таблице — английский.
internal sealed class Localizer
{
    private readonly string[] _keys;
    private readonly Dictionary<string, string[]> _table;
    private readonly Func<MelonLogger.Instance> _log; // лог мода создаётся позже таблиц

    private readonly Dictionary<string, bool> _fontSupport = new();
    private readonly List<(TMP_Text text, Func<string> value)> _bound = new();
    private readonly List<Action> _listeners = new();
    private string[] _current;
    private TMP_Text _fontSample;
    private bool _subscribed;

    public Localizer(string[] keys, Dictionary<string, string[]> table, Func<MelonLogger.Instance> log)
    {
        _keys = keys;
        _table = table;
        _log = log;
    }

    public string Get(string key)
    {
        _current ??= Resolve(_fontSample);
        int i = Array.IndexOf(_keys, key);
        return i >= 0 && i < _current.Length ? _current[i] : key;
    }

    // Текст, который сам обновится при смене языка
    public void Bind(TMP_Text text, Func<string> value, TMP_Text fontSample = null)
    {
        if (text == null) return;
        EnsureSubscribed();
        _current ??= Resolve(fontSample ?? text);
        text.text = value();
        _bound.Add((text, value));
    }

    public void Bind(TMP_Text text, string key, TMP_Text fontSample = null) => Bind(text, () => Get(key), fontSample);

    public void OnLanguageChanged(Action listener)
    {
        EnsureSubscribed();
        _listeners.Add(listener);
    }

    // Шрифт, по которому проверяем, есть ли в нём символы языка (когда нет привязанных текстов)
    public void UseFont(TMP_Text sample)
    {
        _fontSample = sample;
        _current = null;
        EnsureSubscribed();
    }

    private void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        try
        {
            LocalizationSettings.add_SelectedLocaleChanged((Il2CppSystem.Action<Locale>)(Action<Locale>)(_ => Refresh()));
        }
        catch (Exception e) { _log()?.Warning($"[lang] can't follow language changes: {e.Message}"); }
    }

    private void Refresh()
    {
        _bound.RemoveAll(b => b.text == null);
        _current = Resolve(_bound.Count > 0 ? _bound[0].text : _fontSample);
        foreach (var (text, value) in _bound) text.text = value();
        foreach (var listener in _listeners)
            try { listener(); } catch (Exception e) { _log()?.Warning($"[lang] {e.Message}"); }
    }

    private string[] Resolve(TMP_Text fontSample)
    {
        string code = "en";
        try { code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en"; } catch { }
        string key = TableKey(code);
        if (key != "en" && fontSample != null && !FontHasAll(fontSample, key))
        {
            _log()?.Warning($"[lang] font lacks glyphs for '{code}', using English");
            key = "en";
        }
        return _table[key];
    }

    private string TableKey(string code)
    {
        code = code.ToLowerInvariant().Replace('_', '-');
        string key;
        if (code.StartsWith("zh"))
            key = code.Contains("hant") || code.Contains("tw") || code.Contains("hk") ? "zh-hant" : "zh-hans";
        else if (code.StartsWith("pt"))
            key = code.Contains("br") ? "pt-br" : "pt";
        else
            key = code.Split('-')[0];
        return _table.ContainsKey(key) ? key : "en";
    }

    private bool FontHasAll(TMP_Text sample, string key)
    {
        if (_fontSupport.TryGetValue(key, out bool ok)) return ok;
        ok = true;
        try
        {
            var font = sample.font;
            if (font != null)
                foreach (var s in _table[key])
                {
                    foreach (char c in s)
                        if (c > 127 && !char.IsWhiteSpace(c) && !font.HasCharacter(c, true, true)) { ok = false; break; }
                    if (!ok) break;
                }
        }
        catch { ok = true; }
        _fontSupport[key] = ok;
        return ok;
    }
}
