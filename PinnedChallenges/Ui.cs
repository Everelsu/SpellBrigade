using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinnedChallenges;

internal static class Ui
{
    public static readonly Color Gold = new(0.95f, 0.76f, 0.31f);

    private static Sprite _pin;

    // Белая кнопка из dll (Icons/pin.png), цвет задаём через Image.color
    public static Sprite Pin
    {
        get
        {
            if (_pin != null) return _pin;
            using var stream = typeof(Ui).Assembly.GetManifestResourceStream("pin.png");
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            ImageConversion.LoadImage(tex, bytes);
            _pin = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _pin.hideFlags = HideFlags.HideAndDontSave;
            return _pin;
        }
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Прямоугольник от левого верхнего угла родителя: x, y — отступ, w, h — размер
    public static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // Копия текста игры (тот же шрифт и материал) без локализации и прочих скриптов
    public static TMP_Text CloneText(TMP_Text template, Transform parent, string name)
    {
        var go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        foreach (var loc in go.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(loc);
        foreach (var fitter in go.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        foreach (var element in go.GetComponents<LayoutElement>()) Object.DestroyImmediate(element);
        foreach (var c in go.GetComponents<MonoBehaviour>())
            if (c != null && c.TryCast<TMP_Text>() == null) Object.DestroyImmediate(c);
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);

        var text = go.GetComponent<TMP_Text>();
        text.text = "";
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.transform.localScale = Vector3.one;
        return text;
    }
}
