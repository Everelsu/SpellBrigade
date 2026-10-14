using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;

namespace CyrillicFonts;

// Файлы шрифтов: лежат ресурсами в dll, на диск — в UserData\CyrillicFonts (TextMeshPro умеет
// создавать шрифт только из файла). Для каждого знаем роль (заголовки / текст) и толщину.
internal static class FontFiles
{
    public enum Role { Heading, Text }

    public sealed class Face
    {
        public string Path;
        public Role Role;
        public int Weight;   // OS/2 usWeightClass: 300 Light, 400 Regular, 600 SemiBold, 700 Bold, 900 Black
        public bool Italic;
    }

    public static readonly List<Face> Faces = new();

    public static string Folder => System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "CyrillicFonts");

    public static int Extract()
    {
        Directory.CreateDirectory(Folder);
        var assembly = typeof(FontFiles).Assembly;
        var names = assembly.GetManifestResourceNames();

        // шрифты от прошлых версий мода, которых больше нет в dll, — убираем
        foreach (var file in Directory.GetFiles(Folder))
            if (Array.IndexOf(names, System.IO.Path.GetFileName(file)) < 0)
                try { File.Delete(file); } catch { }

        foreach (var name in names)
        {
            if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)) continue;
            using var stream = assembly.GetManifestResourceStream(name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();

            string path = System.IO.Path.Combine(Folder, name);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length) File.WriteAllBytes(path, bytes);

            // роль — по имени файла: Irish Grover (с нашей кириллицей) — заголовки, Titillium — текст
            Role? role = name.Contains("irishgrover", StringComparison.OrdinalIgnoreCase) ? Role.Heading
                       : name.Contains("titillium", StringComparison.OrdinalIgnoreCase) ? Role.Text : null;
            if (role == null) { CyrillicFontsMod.Log.Warning($"{name}: unknown font, skipped"); continue; }

            var (weight, italic) = ReadStyle(bytes);
            Faces.Add(new Face { Path = path, Role = role.Value, Weight = weight, Italic = italic });
            CyrillicFontsMod.Log.Msg($"font {name}: {role}, weight {weight}{(italic ? ", italic" : "")}");
        }
        return Faces.Count;
    }

    // Толщина и курсив из таблицы OS/2 (usWeightClass, fsSelection бит 0)
    private static (int weight, bool italic) ReadStyle(byte[] font)
    {
        try
        {
            int tables = ReadU16(font, 4);
            for (int i = 0; i < tables; i++)
            {
                int record = 12 + i * 16;
                if (font[record] == 'O' && font[record + 1] == 'S' && font[record + 2] == '/' && font[record + 3] == '2')
                {
                    int offset = (int)ReadU32(font, record + 8);
                    return (ReadU16(font, offset + 4), (ReadU16(font, offset + 62) & 1) != 0);
                }
            }
        }
        catch { }
        return (400, false);
    }

    private static int ReadU16(byte[] b, int at) => (b[at] << 8) | b[at + 1];
    private static uint ReadU32(byte[] b, int at) => (uint)((b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3]);

    // Ближайшая по толщине (и курсиву, если есть) кириллица для шрифта игры
    public static Face Pick(Role role, int weight, bool italic)
    {
        Face best = null;
        int bestScore = int.MaxValue;
        foreach (var face in Faces)
        {
            if (face.Role != role) continue;
            int score = Math.Abs(face.Weight - weight) + (face.Italic == italic ? 0 : 50);
            if (score < bestScore) { best = face; bestScore = score; }
        }
        return best;
    }
}
