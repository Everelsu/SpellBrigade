using System.Collections.Generic;
using UnityEngine;

namespace SpellBrigade.ModMenu;

// Белые значки из dll (Icons/*.png), цвет задаётся через Image.color
internal static class Icons
{
    private static readonly Dictionary<string, Sprite> Cache = new();

    public static Sprite Search => Get("search.png");
    public static Sprite Reset => Get("reset.png");
    public static Sprite Chevron => Get("chevron.png");

    private static Sprite Get(string file)
    {
        if (Cache.TryGetValue(file, out var cached) && cached != null) return cached;
        using var stream = typeof(Icons).Assembly.GetManifestResourceStream(file);
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        ImageConversion.LoadImage(tex, bytes);
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return Cache[file] = sprite;
    }
}
