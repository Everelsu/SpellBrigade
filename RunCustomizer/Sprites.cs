using System.Collections.Generic;
using UnityEngine;

namespace RunCustomizer;

// Картинки мода лежат внутри dll (Icons/*.png)
internal static class Sprites
{
    private static readonly Dictionary<string, Sprite> Cache = new();

    private static readonly Dictionary<string, Texture2D> Textures = new();

    public static Sprite Get(string file) => Get(file, 0f);

    // aspect > 0 — вырезаем из середины картинки кусок с такими пропорциями (ширина / высота),
    // чтобы она заполнила ячейку целиком, а не вписалась с полосами
    public static Sprite Get(string file, float aspect)
    {
        string key = aspect > 0f ? $"{file}@{aspect:0.###}" : file;
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = Texture(file);
        float w = tex.width, h = tex.height;
        if (aspect > 0f)
        {
            if (w / h > aspect) w = h * aspect;
            else h = w / aspect;
        }
        var rect = new Rect((tex.width - w) / 2f, (tex.height - h) / 2f, w, h);
        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return Cache[key] = sprite;
    }

    private static Texture2D Texture(string file)
    {
        if (Textures.TryGetValue(file, out var cached) && cached != null) return cached;
        using var stream = typeof(Sprites).Assembly.GetManifestResourceStream(file);
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        ImageConversion.LoadImage(tex, bytes);
        return Textures[file] = tex;
    }
}
