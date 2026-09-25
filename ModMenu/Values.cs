using System;
using System.Collections.Generic;
using MelonLoader.Preferences;

namespace SpellBrigade.ModMenu;

// Какие значения предлагать в переключателе для чисел
internal static class Values
{
    private const int MaxOptions = 201;

    // «Круглые» значения — когда шагов слишком много или диапазон не задан
    private static readonly float[] Ladder =
    {
        0f, 0.05f, 0.1f, 0.2f, 0.25f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.75f, 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f, 1.75f,
        2f, 2.5f, 3f, 4f, 5f, 6f, 7f, 7.5f, 8f, 9f, 10f, 12f, 15f, 20f, 25f, 30f, 40f, 50f, 60f, 75f, 100f,
        150f, 200f, 250f, 500f, 750f, 1000f, 2500f, 5000f, 10000f,
    };

    public static List<float> Range(float min, float max, float step)
    {
        if (max < min) (min, max) = (max, min);
        if (step <= 0f || (max - min) / step + 1 > MaxOptions) return FromLadder(min, max, whole: false);
        var list = new List<float>();
        int count = (int)Math.Floor((max - min) / step + 1e-4) + 1;
        for (int i = 0; i < count; i++) list.Add((float)Math.Round(min + i * step, 4));
        if (list[^1] < max) list.Add(max);
        return list;
    }

    public static List<float> Candidates(float? min, float? max, bool whole)
    {
        if (whole && min.HasValue && max.HasValue && max - min + 1 <= MaxOptions)
            return Range(min.Value, max.Value, 1f);
        return FromLadder(min ?? float.MinValue, max ?? float.MaxValue, whole);
    }

    private static List<float> FromLadder(float min, float max, bool whole)
    {
        var list = new List<float>();
        if (min > float.MinValue) list.Add(min);
        foreach (var v in Ladder)
            if (v > min && v < max && (!whole || v == Math.Floor(v))) list.Add(v);
        if (max < float.MaxValue) list.Add(max);
        return list;
    }

    // Пределы из ValueRange<T> (MelonLoader), если он задан у записи
    public static (float? min, float? max) RangeOf(ValueValidator validator)
    {
        if (validator == null) return (null, null);
        var type = validator.GetType();
        var minProp = type.GetProperty("MinValue");
        var maxProp = type.GetProperty("MaxValue");
        if (minProp == null || maxProp == null) return (null, null);
        try
        {
            return (Convert.ToSingle(minProp.GetValue(validator)), Convert.ToSingle(maxProp.GetValue(validator)));
        }
        catch { return (null, null); }
    }
}
