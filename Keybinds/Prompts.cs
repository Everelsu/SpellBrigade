using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KeybindsUnlocked;

// Подсказки клавиш в меню и в игре (иконки Q, E, LB…). Игра берёт картинку по действию
// из своей конфигурации и о переназначениях не знает. После неё подменяем картинку:
// ищем спрайт новой клавиши по имени (key_pc_*, key_xbox_*, …), а если в игре такого
// нет — рисуем клавишу с подписью.
internal static class Prompts
{
    private const string LabelName = "KeybindsUnlocked_Label";

    private static readonly Dictionary<IntPtr, InputDeviceType> LastDevice = new();
    // Одноимённых спрайтов бывает несколько (атлас меню, атлас HUD) с разными полями и опорной точкой
    private static Dictionary<string, List<Sprite>> _sprites;
    private static float _spritesBuiltAt = -100f;
    private static Sprite _keycap;

    public static void UpdateIconPostfix(ButtonPromptUpdater __instance, InputDeviceType inputDeviceType)
    {
        try
        {
            LastDevice[__instance.Pointer] = inputDeviceType;
            var image = __instance.image;
            if (image == null || __instance.configuration == null) return;
            var action = __instance.configuration.GetInputAction(__instance.actionType);
            if (action == null || action.actionMap == null) { SetLabel(image, null); return; }

            bool gamepad = inputDeviceType != InputDeviceType.KeyboardAndMouse;
            int index = Bindings.Resolve(new Slot(action.actionMap.name + "/" + action.name, gamepad));
            if (index < 0) { SetLabel(image, null); return; }

            var binding = action.bindings[index];
            string overridePath = binding.overridePath;
            if (string.IsNullOrEmpty(overridePath) || overridePath == binding.path) { SetLabel(image, null); return; }

            var sprite = FindSprite(overridePath, inputDeviceType, image.sprite);
            if (sprite != null)
            {
                image.sprite = sprite;
                SetLabel(image, null);
            }
            else
            {
                image.sprite = BlankKeycap();
                SetLabel(image, Bindings.KeyName(action, index, gamepad));
            }
        }
        catch (Exception e) { KeybindsMod.Log.Warning($"prompt icon: {e.Message}"); }
    }

    // После переназначения — перерисовать все подсказки на экране
    public static void RefreshAll()
    {
        InputDeviceType fallback = InputDeviceType.KeyboardAndMouse;
        try { var nav = SingletonPersistent<UINavigator>.Instance; if (nav != null) fallback = nav.CurrentInputDeviceType; } catch { }
        foreach (var updater in Resources.FindObjectsOfTypeAll<ButtonPromptUpdater>())
        {
            if (updater == null || updater.gameObject.scene.name == null) continue;
            var device = LastDevice.TryGetValue(updater.Pointer, out var d) ? d : fallback;
            try { updater.UpdateIcon(device); } catch { }
        }
    }

    private static void EnsureSprites()
    {
        if (_sprites != null && Time.unscaledTime - _spritesBuiltAt <= 5f) return;
        _sprites = new Dictionary<string, List<Sprite>>();
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name.StartsWith("key_", StringComparison.OrdinalIgnoreCase))
            {
                string key = s.name.ToLowerInvariant();
                if (!_sprites.TryGetValue(key, out var list)) _sprites[key] = list = new List<Sprite>();
                list.Add(s);
            }
        _spritesBuiltAt = Time.unscaledTime;
    }

    // reference — родная иконка подсказки: берём спрайт из того же атласа, иначе того же размера.
    // Иначе после смены сцены попадался спрайт из другого атласа, и иконка съезжала на текст.
    private static Sprite FindSprite(string path, InputDeviceType device, Sprite reference)
    {
        EnsureSprites();
        foreach (var name in Candidates(path, device))
            if (_sprites.TryGetValue(name.ToLowerInvariant(), out var list))
            {
                var best = BestMatch(list, reference);
                if (best != null) return best;
            }

        // не загруженные иконки часто лежат в атласе вместе с остальными
        foreach (var atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
        {
            if (atlas == null) continue;
            foreach (var name in Candidates(path, device))
            {
                Sprite fromAtlas = null;
                try { fromAtlas = atlas.GetSprite(name); } catch { }
                if (fromAtlas == null) continue;
                fromAtlas.hideFlags = HideFlags.DontUnloadUnusedAsset;
                _sprites[name.ToLowerInvariant()] = new List<Sprite> { fromAtlas };
                return fromAtlas;
            }
        }
        return null;
    }

    private static Sprite BestMatch(List<Sprite> list, Sprite reference)
    {
        list.RemoveAll(s => s == null);
        if (list.Count == 0) return null;
        if (reference != null)
        {
            foreach (var s in list)
                if (s.texture == reference.texture) return s;
            foreach (var s in list)
                if (Mathf.Abs(s.rect.width - reference.rect.width) < 1f && Mathf.Abs(s.rect.height - reference.rect.height) < 1f
                    && (s.pivot - reference.pivot).sqrMagnitude < 1f)
                    return s;
        }
        return list[0];
    }

    private static IEnumerable<string> Candidates(string path, InputDeviceType device)
    {
        int slash = path.IndexOf('/');
        if (slash < 0) yield break;
        string layout = path.Substring(0, slash).Trim('<', '>').ToLowerInvariant();
        string control = path.Substring(slash + 1).ToLowerInvariant();

        if (layout == "keyboard")
        {
            string key = control switch
            {
                "leftshift" or "rightshift" or "shift" => "shift",
                "leftctrl" or "rightctrl" or "ctrl" => "ctrl",
                "leftalt" or "rightalt" or "alt" => "alt",
                "escape" => "esc",
                "enter" or "numpadenter" => "enter",
                _ => control,
            };
            yield return "key_pc_" + key;
            yield break;
        }
        if (layout == "mouse")
        {
            string key = control switch
            {
                "leftbutton" => "mouse1", "rightbutton" => "mouse2", "middlebutton" => "mouse3",
                "backbutton" => "mouse4", "forwardbutton" => "mouse5", _ => control,
            };
            yield return "key_pc_" + key;
            yield break;
        }
        if (layout != "gamepad") yield break;

        string c = control.Replace("dpad/", "");
        string xbox = c switch
        {
            "buttonsouth" => "a", "buttoneast" => "b", "buttonwest" => "x", "buttonnorth" => "y",
            "leftshoulder" => "lb", "rightshoulder" => "rb", "lefttrigger" => "lt", "righttrigger" => "rt",
            "start" => "start", "select" => "select", "leftstickpress" => "l", "rightstickpress" => "r", _ => c,
        };
        string ps = c switch
        {
            "buttonsouth" => "cross", "buttoneast" => "circle", "buttonwest" => "square", "buttonnorth" => "triangle",
            "leftshoulder" => "l1", "rightshoulder" => "r1", "lefttrigger" => "l2", "righttrigger" => "r2",
            "start" => "options", "select" => "share", _ => c,
        };
        string deck = c switch
        {
            "buttonsouth" => "a", "buttoneast" => "b", "buttonwest" => "x", "buttonnorth" => "y",
            "leftshoulder" => "l1", "rightshoulder" => "r1", "lefttrigger" => "l2", "righttrigger" => "r2",
            "start" => "start", "select" => "select", _ => c,
        };

        switch (device)
        {
            case InputDeviceType.PlayStation:
                yield return "key_playstation_" + ps;
                if (c == "leftstickpress") yield return "key_ps_L3";
                if (c == "rightstickpress") yield return "key_ps_R3";
                break;
            case InputDeviceType.SteamDeck:
                yield return "key_steamdeck_" + deck;
                yield return "key_playstation_" + ps; // у Steam Deck игра сама берёт часть иконок PlayStation
                break;
        }
        yield return "key_xbox_" + xbox;
        if (xbox.Length == 1) yield return "key_xbox_" + xbox.ToUpperInvariant(); // key_xbox_R — нажатие стика
    }

    private static void SetLabel(Image image, string text)
    {
        var existing = image.transform.Find(LabelName);
        if (text == null)
        {
            if (existing != null) Object.Destroy(existing.gameObject);
            return;
        }

        TMP_Text label;
        if (existing != null) label = existing.GetComponent<TMP_Text>();
        else
        {
            var go = new GameObject(LabelName);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(image.transform, false);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            label = go.AddComponent<TextMeshProUGUI>();
            var font = FindFont(image.transform);
            if (font != null) label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 6f; label.fontSizeMax = 200f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }
        var r = label.rectTransform;
        float padX = _inner.width * 0.04f, padY = _inner.height * 0.02f;
        r.anchorMin = new Vector2(_inner.xMin + padX, _inner.yMin + padY);
        r.anchorMax = new Vector2(_inner.xMax - padX, _inner.yMax - padY);
        label.text = text.ToUpperInvariant();
        if (!Unstyled.Contains(label)) Unstyled.Add(label);
    }

    private static readonly List<TMP_Text> Unstyled = new();

    // Буквы на иконках игры жирнее обычного шрифта — добавляем белую обводку. Её можно
    // выставить только когда текст уже на экране (у ещё не показанного TextMeshPro нет материала).
    public static void Tick()
    {
        for (int i = Unstyled.Count - 1; i >= 0; i--)
        {
            var label = Unstyled[i];
            if (label == null) { Unstyled.RemoveAt(i); continue; }
            if (!label.isActiveAndEnabled) continue;
            try
            {
                label.outlineWidth = 0.14f;
                label.outlineColor = new Color32(255, 255, 255, 255);
            }
            catch { }
            Unstyled.RemoveAt(i);
        }
        FixPromptTextWidths();
    }

    // Баг игры: подпись у подсказки («Продолжить», «Выйти в лобби») тянется ContentSizeFitter'ом,
    // но после раунда ширина остаётся от английского слова. Русское длиннее, текст по центру —
    // и наезжает на иконку клавиши. Раз в полсекунды пересчитываем ширину, где она не сходится.
    private static readonly List<(TMP_Text Text, ContentSizeFitter Fitter)> PromptTexts = new();
    private static float _nextPromptScan, _nextWidthCheck;

    private static void FixPromptTextWidths()
    {
        float now = Time.unscaledTime;
        if (now < _nextWidthCheck) return;
        _nextWidthCheck = now + 0.5f;

        if (now >= _nextPromptScan)
        {
            _nextPromptScan = now + 3f;
            PromptTexts.Clear();
            foreach (var updater in Object.FindObjectsOfType<ButtonPromptUpdater>())
                foreach (var text in updater.GetComponentsInChildren<TMP_Text>(false))
                {
                    var fitter = text.GetComponent<ContentSizeFitter>();
                    if (fitter != null && fitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize)
                        PromptTexts.Add((text, fitter));
                    else
                        ReportClipped(text); // без фиттера не трогаем — только пишем в лог, как устроена
                }
        }

        foreach (var (text, fitter) in PromptTexts)
        {
            if (text == null || fitter == null || !text.isActiveAndEnabled) continue;
            float preferred = text.preferredWidth;
            if (Mathf.Abs(text.rectTransform.rect.width - preferred) <= 2f) continue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
            // фиттер не всегда успевает — ставим ширину по тексту сами, как сделал бы он
            if (Mathf.Abs(text.rectTransform.rect.width - preferred) > 2f)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferred);
        }
    }

    private static readonly HashSet<IntPtr> Reported = new();

    // Подпись у иконки шире своего прямоугольника, а фиттера нет — пишем устройство в лог
    // (один раз на подпись), чтобы поправить точно, не задевая остальной текст
    private static void ReportClipped(TMP_Text text)
    {
        if (text == null || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) return;
        var r = text.rectTransform;
        if (text.preferredWidth <= r.rect.width + 2f || !Reported.Add(text.Pointer)) return;
        var path = new List<string>();
        for (var t = text.transform; t != null && path.Count < 6; t = t.parent) path.Insert(0, t.name);
        var parentComponents = new List<string>();
        if (text.transform.parent != null)
            foreach (var c in text.transform.parent.GetComponents<Component>()) parentComponents.Add(c.GetIl2CppType().Name);
        KeybindsMod.Log.Msg($"[prompt] '{text.text}' {string.Join("/", path)}: width {r.rect.width:0} < text {text.preferredWidth:0}, " +
                            $"align {text.horizontalAlignment}, pivot {r.pivot.x:0.##}, anchors {r.anchorMin.x:0.##}-{r.anchorMax.x:0.##}, " +
                            $"wrap {text.textWrappingMode}, parent [{string.Join(", ", parentComponents)}]");
    }

    private static TMP_FontAsset FindFont(Transform near)
    {
        for (var t = near; t != null; t = t.parent)
        {
            var text = t.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null) return text.font;
        }
        var any = Object.FindObjectOfType<TMP_Text>();
        return any != null ? any.font : null;
    }

    private static Sprite _blank;
    // внутренняя (тёмная) часть клавиши в долях от всей иконки — по ней ставим букву
    private static Rect _inner = new(0.11f, 0.11f, 0.78f, 0.78f);

    // Пустая клавиша из оригинальной иконки игры: берём, например, key_pc_q и заливаем
    // внутреннюю часть цветом фона клавиши — буква пропадает, рамка остаётся родной.
    // Текстуры иконок не читаются напрямую, поэтому копируем через RenderTexture.
    // При упаковке Unity обрезает прозрачные поля (textureRect меньше rect) — возвращаем
    // их, иначе клавиша растягивается на весь слот и выглядит крупнее родных.
    private static Sprite BlankKeycap()
    {
        if (_blank != null) return _blank;
        EnsureSprites();
        Sprite source = null;
        foreach (var name in new[] { "key_pc_q", "key_pc_e", "key_pc_r", "key_pc_f", "key_pc_t", "key_pc_c", "key_pc_v", "key_pc_x" })
            if (_sprites.TryGetValue(name, out var list) && (source = BestMatch(list, null)) != null) break;
        if (source == null) return Keycap();

        try
        {
            var texture = source.texture;
            var trimmed = source.textureRect;
            var full = source.rect;
            var offset = source.textureRectOffset;
            int w = Mathf.RoundToInt(trimmed.width), h = Mathf.RoundToInt(trimmed.height);
            int fw = Mathf.Max(w, Mathf.RoundToInt(full.width)), fh = Mathf.Max(h, Mathf.RoundToInt(full.height));
            int ox = Mathf.Clamp(Mathf.RoundToInt(offset.x), 0, fw - w), oy = Mathf.Clamp(Mathf.RoundToInt(offset.y), 0, fh - h);

            var rt = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var piece = new Texture2D(w, h, TextureFormat.RGBA32, false);
            piece.ReadPixels(new Rect(trimmed.x, trimmed.y, w, h), 0, 0);
            piece.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var px = piece.GetPixels32();
            Object.Destroy(piece);
            int midX = w / 2, midY = h / 2;
            int left = InnerEdge(px, w, midY, 0, 1, true);
            int right = InnerEdge(px, w, midY, w - 1, -1, true);
            int bottom = InnerEdge(px, w, midX, 0, 1, false, h);
            int top = InnerEdge(px, w, midX, h - 1, -1, false, h);
            if (left < 0 || right < 0 || bottom < 0 || top < 0 || right - left < 4 || top - bottom < 4)
                return Keycap();

            // цвет фона берём поглубже внутри: у самой рамки пиксели смешаны с белым
            var fill = px[midY * w + left + Math.Max(2, (right - left) / 8)];
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                px[y * w + x] = fill;

            // собираем иконку полного размера: обрезанная часть на своём месте, вокруг прозрачно
            var fullPx = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(fw * fh);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                fullPx[(y + oy) * fw + (x + ox)] = px[y * w + x];

            var copy = new Texture2D(fw, fh, TextureFormat.RGBA32, false);
            copy.SetPixels32(fullPx);
            copy.Apply(false, true);
            copy.hideFlags = HideFlags.HideAndDontSave;

            _inner = new Rect((left + ox) / (float)fw, (bottom + oy) / (float)fh,
                              (right - left + 1) / (float)fw, (top - bottom + 1) / (float)fh);
            _blank = Sprite.Create(copy, new Rect(0, 0, fw, fh), new Vector2(source.pivot.x / fw, source.pivot.y / fh), source.pixelsPerUnit);
            _blank.hideFlags = HideFlags.HideAndDontSave;
            return _blank;
        }
        catch (Exception e)
        {
            KeybindsMod.Log.Warning($"can't copy the game's key icon: {e.Message}");
            return Keycap();
        }
    }

    // Идём от края к центру: прозрачное → белая рамка → первая тёмная точка внутри
    private static int InnerEdge(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32> px,
                                 int w, int line, int start, int step, bool horizontal, int h = 0)
    {
        int length = horizontal ? w : h;
        bool seenBorder = false;
        for (int i = start; i >= 0 && i < length; i += step)
        {
            var c = horizontal ? px[line * w + i] : px[i * w + line];
            bool bright = c.a > 128 && c.r > 170 && c.g > 170 && c.b > 170;
            if (bright) seenBorder = true;
            else if (seenBorder && c.a > 128) return i;
        }
        return -1;
    }

    // Запасной вариант, если родную иконку скопировать не удалось.
    // Клавиша в стиле игровых иконок: тёмная, с толстой белой рамкой и чуть скруглёнными углами
    private static Sprite Keycap()
    {
        if (_keycap != null) return _keycap;
        const int size = 64;
        const float radius = 4f, border = 6f, pad = 1f;
        var fill = new Color32(38, 32, 48, 235);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(size * size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // знаковое расстояние до скруглённого прямоугольника (отрицательное — внутри)
            float half = size / 2f - pad;
            float qx = Mathf.Abs(x + 0.5f - size / 2f) - (half - radius);
            float qy = Mathf.Abs(y + 0.5f - size / 2f) - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float shape = Mathf.Clamp01(0.5f - d);                 // вся клавиша
            float inner = Mathf.Clamp01(0.5f - (d + border));      // внутренняя тёмная часть
            var c = Color32.Lerp(new Color32(255, 255, 255, 255), fill, inner);
            c.a = (byte)(c.a * shape);
            pixels[y * size + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        _keycap = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _inner = new Rect((pad + border) / size, (pad + border) / size, 1f - 2f * (pad + border) / size, 1f - 2f * (pad + border) / size);
        _keycap.hideFlags = HideFlags.HideAndDontSave;
        return _keycap;
    }
}
