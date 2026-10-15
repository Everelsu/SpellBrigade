using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace CyrillicFonts;

// Находим шрифты игры и ставим кириллицу первой в их запасные шрифты (fallbackFontAssetTable).
// TextMeshPro берёт из запасных только те буквы, которых нет в основном шрифте, — латиница
// остаётся родной.
internal static class FontPatcher
{
    private static readonly HashSet<IntPtr> Patched = new();
    private static readonly List<(TMP_FontAsset original, TMP_FontAsset cyrillic)> Inserted = new();
    private static readonly Dictionary<string, TMP_FontAsset> Created = new(); // путь файла + шейдер → наш шрифт

    // Имя шрифта игры → роль, толщина, курсив
    private static bool Describe(string name, out FontFiles.Role role, out int weight, out bool italic)
    {
        role = FontFiles.Role.Text; weight = 400; italic = false;
        if (name.StartsWith("IrishGrover", StringComparison.OrdinalIgnoreCase)) { role = FontFiles.Role.Heading; return true; }
        if (!name.StartsWith("TitilliumWeb-", StringComparison.OrdinalIgnoreCase)) return false;

        string style = name.Substring("TitilliumWeb-".Length).Replace(" SDF", "").Trim();
        italic = style.EndsWith("Italic", StringComparison.OrdinalIgnoreCase);
        if (italic) style = style.Substring(0, style.Length - "Italic".Length);
        weight = style.ToLowerInvariant() switch
        {
            "extralight" => 200, "light" => 300, "" or "regular" => 400, "semibold" => 600, "bold" => 700, "black" => 900, _ => 400,
        };
        return true;
    }

    public static void Scan()
    {
        if (FontFiles.Faces.Count == 0) return;
        bool any = false;
        foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset == null || Patched.Contains(asset.Pointer)) continue;
            if (!Describe(asset.name, out var role, out int weight, out bool italic)) continue;
            Patched.Add(asset.Pointer);

            var face = FontFiles.Pick(role, weight, italic);
            if (face == null) continue;
            var cyrillic = Create(face, asset);
            if (cyrillic == null) continue;

            var fallbacks = asset.fallbackFontAssetTable;
            if (fallbacks == null)
            {
                fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                asset.fallbackFontAssetTable = fallbacks;
            }
            fallbacks.Insert(0, cyrillic);
            Inserted.Add((asset, cyrillic));
            any = true;
            CyrillicFontsMod.Log.Msg($"{asset.name} ← {System.IO.Path.GetFileName(face.Path)}");
        }
        if (any) RefreshTexts();
    }

    // Сменился вид или размер: меняем нашу кириллицу в запасных шрифтах на новую и перерисовываем тексты
    public static void Reapply()
    {
        for (int i = 0; i < Inserted.Count; i++)
        {
            var (original, old) = Inserted[i];
            if (original == null) continue;
            if (!Describe(original.name, out var role, out int weight, out bool italic)) continue;
            var face = FontFiles.Pick(role, weight, italic);
            var cyrillic = face != null ? Create(face, original) : null;
            if (cyrillic == null) continue;
            var fallbacks = original.fallbackFontAssetTable;
            fallbacks.Remove(old);
            fallbacks.Insert(0, cyrillic);
            Inserted[i] = (original, cyrillic);
        }
        RefreshTexts();
    }

    private static TMP_FontAsset Create(FontFiles.Face face, TMP_FontAsset original)
    {
        var shader = original.material != null ? original.material.shader : null;
        string key = face.Path + "|" + (shader != null ? shader.name : "");
        if (Created.TryGetValue(key, out var existing) && existing != null) return WithScale(existing, original, face.Role);

        TMP_FontAsset asset;
        try { asset = TMP_FontAsset.CreateFontAsset(face.Path, 0, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024); }
        catch (Exception e) { CyrillicFontsMod.Log.Warning($"{face.Path}: {e.Message}"); return null; }
        if (asset == null) { CyrillicFontsMod.Log.Warning($"{face.Path}: TextMeshPro couldn't load the font"); return null; }

        asset.name = System.IO.Path.GetFileNameWithoutExtension(face.Path) + " SDF (Cyrillic Fonts)";
        asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
        Object.DontDestroyOnLoad(asset);
        // шейдер — как у шрифта игры: в сборке игры стандартного может не быть
        if (shader != null && asset.material != null) asset.material.shader = shader;
        Created[key] = asset;
        return WithScale(asset, original, face.Role);
    }

    // Подгоняем размер: заглавные кириллицы той же высоты, что заглавные латиницы оригинала
    private static TMP_FontAsset WithScale(TMP_FontAsset cyrillic, TMP_FontAsset original, FontFiles.Role role)
    {
        try
        {
            var o = original.faceInfo;
            var c = cyrillic.faceInfo;
            float scale = 1f;
            if (o.pointSize > 0 && c.pointSize > 0 && o.capLine > 0 && c.capLine > 0)
                scale = (o.capLine / o.pointSize) / (c.capLine / c.pointSize) * (o.scale > 0 ? o.scale : 1f);
            scale *= role == FontFiles.Role.Heading ? CyrillicFontsMod.HeadingScaleEntry.Value : CyrillicFontsMod.TextScaleEntry.Value;
            c.scale = Mathf.Clamp(scale, 0.5f, 2f);
            cyrillic.faceInfo = c;
        }
        catch (Exception e) { CyrillicFontsMod.Log.Warning($"size match: {e.Message}"); }
        return cyrillic;
    }

    // Уже нарисованные тексты перестраиваем — иначе они останутся со старыми буквами до следующей правки
    private static void RefreshTexts()
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.font == null || !Patched.Contains(text.font.Pointer)) continue;
            try { text.SetAllDirty(); } catch { }
        }
    }
}
