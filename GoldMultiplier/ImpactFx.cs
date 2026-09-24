using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GoldXpMultiplier;

// «x2» падает сверху экрана и с силой бьёт по числу: число меняется и подпрыгивает,
// панель трясётся, разлетаются искры, а сам «x2» сплющивается и растворяется.
internal static class ImpactFx
{
    private const float FallDuration   = 0.42f;
    private const float ImpactDuration = 0.55f;
    private static readonly Color SparkColor = new(1f, 0.84f, 0.3f, 1f);

    private sealed class Spark
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Spin;
    }

    public static IEnumerator Slam(TMP_Text target, TMP_Text style, string label, string newText,
                                   Transform shakeRoot, Action onImpact)
    {
        if (target == null) yield break;
        var root = target.canvas != null ? target.canvas.rootCanvas?.GetComponent<RectTransform>() : null;
        if (root == null) { target.text = newText; onImpact?.Invoke(); yield break; }

        // куда бить: визуальный центр текущего числа
        target.ForceMeshUpdate(false, false);
        Vector3 end = root.InverseTransformPoint(target.transform.TransformPoint(target.textBounds.center));
        Vector3 start = end + new Vector3(0f, root.rect.height * 0.6f, 0f);

        var slam = CreateLabel(root, style ?? target, label);
        var slamRect = slam.rectTransform;

        // --- падение: ускоряется, чуть крутится, растёт прозрачность ---
        float t0 = Time.unscaledTime;
        while (true)
        {
            if (target == null || slam == null) { Cleanup(slam, null); yield break; }
            float k = Mathf.Clamp01((Time.unscaledTime - t0) / FallDuration);
            float e = k * k * k;
            slamRect.localPosition = Vector3.LerpUnclamped(start, end, e);
            float scale = Mathf.Lerp(2.4f, 1f, e);
            slamRect.localScale = new Vector3(scale * 0.85f, scale * 1.15f, 1f); // вытягивается в полёте
            slamRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-16f, 0f, e));
            slam.alpha = Mathf.Clamp01(k * 5f);
            if (k >= 1f) break;
            yield return null;
        }

        // --- удар ---
        target.text = newText;
        try { onImpact?.Invoke(); } catch (Exception ex) { GoldXpMod.Log.Warning($"[fx] {ex.Message}"); }

        var sparks = SpawnSparks(root, end, 26);
        var flash = SpawnFlash(root, end);
        Vector3 targetScale = target.transform.localScale;
        Vector3 shakeBase = shakeRoot != null ? shakeRoot.localPosition : Vector3.zero;

        t0 = Time.unscaledTime;
        float last = t0;
        while (true)
        {
            float now = Time.unscaledTime;
            float dt = now - last;
            last = now;
            float k = Mathf.Clamp01((now - t0) / ImpactDuration);

            // число подпрыгивает: резко раздувается и пружинит обратно
            if (target != null)
                target.transform.localScale = targetScale * (1f + 0.55f * Punch(k));

            // тряска панели, затухает
            if (shakeRoot != null)
            {
                float amp = 22f * (1f - k) * (1f - k);
                shakeRoot.localPosition = shakeBase + new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp), 0f);
            }

            // «x2» сплющивается от удара, отскакивает вверх и тает
            if (slam != null)
            {
                float squash = Mathf.Exp(-9f * k);
                slamRect.localScale = new Vector3(1f + 0.6f * squash + 0.25f * k, 1f - 0.45f * squash + 0.25f * k, 1f);
                slamRect.localPosition = end + new Vector3(0f, 70f * Mathf.Sin(k * Mathf.PI * 0.5f), 0f);
                slam.alpha = 1f - k * k;
            }

            // вспышка-волна от удара: быстро растёт и гаснет
            if (flash != null)
            {
                float f = 1f - Mathf.Pow(1f - k, 3f);
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 3.2f, f);
                var fc = flash.color;
                fc.a = 0.8f * (1f - f);
                flash.color = fc;
            }

            foreach (var s in sparks)
            {
                if (s.Rect == null) continue;
                s.Velocity += new Vector2(0f, -1800f) * dt;
                s.Rect.localPosition += (Vector3)(s.Velocity * dt);
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.Rect.localEulerAngles.z + s.Spin * dt);
                var c = s.Image.color;
                c.a = 1f - k;
                s.Image.color = c;
                s.Rect.localScale = Vector3.one * (1f - 0.6f * k);
            }

            if (k >= 1f) break;
            yield return null;
        }

        if (target != null) target.transform.localScale = targetScale;
        if (shakeRoot != null) shakeRoot.localPosition = shakeBase;
        Cleanup(slam, sparks);
        if (flash != null) Object.Destroy(flash.gameObject);
    }

    // затухающая пружина: 0 → пик → колебания → 0
    private static float Punch(float k) => Mathf.Exp(-6f * k) * Mathf.Sin(k * Mathf.PI * 3.5f + 0.35f) * (k < 0.02f ? k / 0.02f : 1f);

    private static TMP_Text CreateLabel(RectTransform root, TMP_Text style, string label)
    {
        var go = new GameObject("SBMod_Slam");
        go.AddComponent<RectTransform>();
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.fontStyle = style.fontStyle;
        text.color = style.color;
        text.enableVertexGradient = style.enableVertexGradient;
        text.colorGradient = style.colorGradient;
        text.fontSize = style.fontSize * 1.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = label;
        text.rectTransform.sizeDelta = new Vector2(800f, 400f);
        return text;
    }

    private static List<Spark> SpawnSparks(RectTransform root, Vector3 at, int count)
    {
        var list = new List<Spark>();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("SBMod_Spark");
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(root, false);
            go.transform.SetAsLastSibling();
            var image = go.AddComponent<Image>();
            image.sprite = RoundSprite();
            image.color = i % 3 == 0 ? Color.white : SparkColor;
            image.raycastTarget = false;
            float size = Random.Range(22f, 48f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localPosition = at;
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            float angle = Random.Range(10f, 170f) * Mathf.Deg2Rad; // веером вверх и в стороны
            float speed = Random.Range(500f, 1100f);
            list.Add(new Spark
            {
                Rect = rect, Image = image,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Spin = Random.Range(-720f, 720f),
            });
        }
        return list;
    }

    private static Image SpawnFlash(RectTransform root, Vector3 at)
    {
        var sprite = RoundSprite();
        if (sprite == null) return null; // без круглого спрайта вспышка была бы квадратом
        var go = new GameObject("SBMod_Flash");
        var rect = go.AddComponent<RectTransform>();
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 0.9f, 0.55f, 0.8f);
        image.raycastTarget = false;
        rect.sizeDelta = new Vector2(320f, 320f);
        rect.localPosition = at;
        return image;
    }

    private static Sprite _round;

    // Мягкий светящийся круг (радиальный градиент), генерируется один раз
    private static Sprite RoundSprite()
    {
        if (_round != null) return _round;
        try
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(size * size);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 в центре, 1 на краю
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // плавный край
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            _round = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _round.hideFlags = HideFlags.HideAndDontSave;
        }
        catch (Exception e) { GoldXpMod.Log.Warning($"[fx] no round sprite: {e.Message}"); }
        return _round;
    }

    private static void Cleanup(TMP_Text slam, List<Spark> sparks)
    {
        if (slam != null) Object.Destroy(slam.gameObject);
        if (sparks == null) return;
        foreach (var s in sparks)
            if (s.Rect != null) Object.Destroy(s.Rect.gameObject);
    }
}
