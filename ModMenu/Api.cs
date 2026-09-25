using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBrigade.ModMenu;

/// <summary>
/// Вкладка «Моды» в настройках игры: один прокручиваемый список, в нём раздел на каждый мод
/// (разделы по алфавиту). Любой мод может добавить сюда свой раздел.
/// Настройки из MelonPreferences появляются и без этого API — автоматически,
/// по странице на категорию (скрытые категории и записи не показываются).
/// </summary>
/// <example>
/// <code>
/// Menu.AddPage("MyMod", "My Mod")
///     .Toggle("Enabled", () => enabled.Value, v => enabled.Value = v)
///     .Number("Speed", 0.5f, 3f, 0.25f, () => speed.Value, v => speed.Value = v)
///     .Button("Reset", ResetEverything);
/// </code>
/// </example>
public static class Menu
{
    internal static readonly List<Page> Pages = new();
    private static readonly HashSet<string> Claimed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Значения поменялись не из меню — перерисовать открытые строки.</summary>
    internal static event Action RefreshRequested;

    /// <summary>
    /// Страница мода. Повторный вызов с тем же id возвращает ту же страницу.
    /// Если id совпадает с именем категории MelonPreferences, её автоматическая страница не показывается.
    /// Регистрируйте в OnInitializeMelon: меню строится, когда игра создаёт окно настроек.
    /// </summary>
    public static Page AddPage(string id, Func<string> title)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("page id is required", nameof(id));
        foreach (var existing in Pages)
            if (existing.Id == id) return existing;
        var page = new Page(id, title ?? (() => id));
        Pages.Add(page);
        HideCategory(id); // своя страница заменяет автоматическую для категории с тем же именем
        return page;
    }

    public static Page AddPage(string id, string title) => AddPage(id, () => title);

    /// <summary>Не показывать автоматическую страницу для категории MelonPreferences.</summary>
    public static void HideCategory(string categoryIdentifier)
    {
        if (!string.IsNullOrEmpty(categoryIdentifier)) Claimed.Add(categoryIdentifier);
    }

    /// <summary>Перечитать значения во всех строках (например, после изменения настроек из кода).</summary>
    public static void Refresh() => RefreshRequested?.Invoke();

    internal static bool IsClaimed(string categoryIdentifier) => Claimed.Contains(categoryIdentifier);
}

/// <summary>Где в строке стоят кнопки.</summary>
public enum ButtonLayout
{
    /// <summary>Поровну на всю ширину строки.</summary>
    Stretch,
    /// <summary>Слева, ширина по тексту.</summary>
    Left,
    /// <summary>По центру, ширина по тексту.</summary>
    Center,
    /// <summary>Справа, ширина по тексту.</summary>
    Right,
    /// <summary>В колонке значений — там, где у настроек «&lt; значение &gt;».</summary>
    Value,
}

/// <summary>Кнопка для <see cref="Page.Buttons(MenuButton[])"/>: текст (может следовать за языком игры) и действие.</summary>
public readonly struct MenuButton
{
    internal readonly Func<string> Text;
    internal readonly Action OnClick;

    public MenuButton(Func<string> text, Action onClick)
    {
        Text = text;
        OnClick = onClick;
    }

    public MenuButton(string text, Action onClick) : this(() => text, onClick) { }
}

/// <summary>Страница мода: строки идут сверху вниз в порядке добавления. Методы возвращают страницу для цепочки вызовов.</summary>
public sealed class Page
{
    public string Id { get; }
    internal Func<string> Title;
    internal readonly List<Item> Items = new();

    internal Page(string id, Func<string> title)
    {
        Id = id;
        Title = title;
    }

    /// <summary>Заголовок раздела.</summary>
    public Page Header(Func<string> text) => Add(new HeaderItem { Label = text });
    public Page Header(string text) => Header(() => text);

    // Во всех строках со значением необязательный @default — стандартное значение: если текущее
    // от него отличается, рядом со строкой появляется кнопка сброса (и Delete на выбранной строке).

    /// <summary>Вкл/Выкл.</summary>
    public Page Toggle(Func<string> label, Func<bool> get, Action<bool> set, bool? @default = null) =>
        Add(new OptionItem
        {
            Label = label,
            Read = () => (new List<string> { Strings.Get(Strings.On), Strings.Get(Strings.Off) }, get() ? 0 : 1),
            Apply = i => set(i == 0),
            IsDefault = @default.HasValue ? () => get() == @default.Value : null,
            Reset = @default.HasValue ? () => set(@default.Value) : null,
        });
    public Page Toggle(string label, Func<bool> get, Action<bool> set, bool? @default = null) => Toggle(() => label, get, set, @default);

    /// <summary>Выбор из списка; get/set/@default — номер варианта.</summary>
    public Page Choice(Func<string> label, Func<IList<string>> options, Func<int> get, Action<int> set, int? @default = null) =>
        Add(new OptionItem
        {
            Label = label,
            Read = () => (new List<string>(options()), get()),
            Apply = set,
            IsDefault = @default.HasValue ? () => get() == @default.Value : null,
            Reset = @default.HasValue ? () => set(@default.Value) : null,
        });
    public Page Choice(string label, IList<string> options, Func<int> get, Action<int> set, int? @default = null) =>
        Choice(() => label, () => options, get, set, @default);

    /// <summary>Число от min до max с шагом step. format — как показывать значение (по умолчанию «0.##»).</summary>
    public Page Number(Func<string> label, float min, float max, float step, Func<float> get, Action<float> set,
                       Func<float, string> format = null, float? @default = null) =>
        Numbers(label, Values.Range(min, max, step), get, set, format, @default);
    public Page Number(string label, float min, float max, float step, Func<float> get, Action<float> set,
                       Func<float, string> format = null, float? @default = null) =>
        Number(() => label, min, max, step, get, set, format, @default);

    /// <summary>Число из заданного списка значений (например, пресеты x0.5, x1, x2…).</summary>
    public Page Number(Func<string> label, IEnumerable<float> values, Func<float> get, Action<float> set,
                       Func<float, string> format = null, float? @default = null) =>
        Numbers(label, new List<float>(values), get, set, format, @default);
    public Page Number(string label, IEnumerable<float> values, Func<float> get, Action<float> set,
                       Func<float, string> format = null, float? @default = null) =>
        Number(() => label, values, get, set, format, @default);

    /// <summary>Пояснение мелким текстом (можно в несколько строк через \n).</summary>
    public Page Note(Func<string> text) => Add(new NoteItem { Label = text });
    public Page Note(string text) => Note(() => text);

    /// <summary>Одна кнопка. По умолчанию — по центру строки, ширина по тексту.</summary>
    public Page Button(Func<string> text, Action onClick, ButtonLayout layout = ButtonLayout.Center) =>
        Buttons(layout, new MenuButton(text, onClick));
    public Page Button(string text, Action onClick, ButtonLayout layout = ButtonLayout.Center) =>
        Button(() => text, onClick, layout);

    /// <summary>Несколько кнопок в одну строку — поровну на всю ширину.</summary>
    public Page Buttons(params MenuButton[] buttons) => Buttons(ButtonLayout.Stretch, buttons);

    /// <summary>Несколько кнопок в одну строку с выбранной раскладкой (геймпад ходит между ними влево/вправо).</summary>
    public Page Buttons(ButtonLayout layout, params MenuButton[] buttons) =>
        buttons == null || buttons.Length == 0 ? this : Add(new ButtonItem { Layout = layout, Buttons = new List<MenuButton>(buttons) });

    /// <summary>Строка-действие: подпись слева, кнопка справа — там же, где значения у настроек.</summary>
    public Page Action(Func<string> label, Func<string> button, Action onClick) =>
        Add(new ButtonItem { Label = label, Layout = ButtonLayout.Value, Buttons = new List<MenuButton> { new(button, onClick) } });
    public Page Action(string label, string button, Action onClick) => Action(() => label, () => button, onClick);

    /// <summary>
    /// Произвольная строка высотой height (в единицах интерфейса игры, обычная строка — 150).
    /// build получает пустой RectTransform шириной с обычную строку и возвращает то,
    /// что должно получать фокус с клавиатуры/геймпада (или null).
    /// </summary>
    public Page Custom(float height, Func<RectTransform, Selectable> build) =>
        Add(new CustomItem { Height = height, Build = build });

    /// <summary>
    /// Запись MelonPreferences: bool — Вкл/Выкл, числа — выбор значения (в пределах ValueRange,
    /// если он задан), enum — список. Строки не показываются. Изменения сразу сохраняются в cfg,
    /// сброс — к значению по умолчанию из CreateEntry.
    /// </summary>
    public Page Entry(MelonPreferences_Entry entry, Func<string> label = null)
    {
        if (entry == null) return this;
        Menu.HideCategory(entry.Category?.Identifier);
        return AddEntry(entry, label);
    }

    /// <summary>Все видимые записи категории MelonPreferences; её автоматическая страница прячется.</summary>
    public Page Category(MelonPreferences_Category category)
    {
        if (category == null) return this;
        Menu.HideCategory(category.Identifier);
        return AddCategory(category);
    }

    internal Page AddCategory(MelonPreferences_Category category)
    {
        foreach (var entry in category.Entries)
            if (entry != null && !entry.IsHidden) AddEntry(entry, null);
        return this;
    }

    private Page AddEntry(MelonPreferences_Entry entry, Func<string> label)
    {
        label ??= () => string.IsNullOrEmpty(entry.DisplayName) ? entry.Identifier : entry.DisplayName;
        var type = entry.GetReflectedType();
        void Store(object value)
        {
            if (Equals(entry.BoxedValue, value)) return;
            entry.BoxedValue = value;
            entry.Category?.SaveToFile(false);
        }

        Page page;
        if (type == typeof(bool))
            page = Toggle(label, () => (bool)entry.BoxedValue, v => Store(v));
        else if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            page = Choice(label, () => names,
                () => Array.IndexOf(names, entry.BoxedValue.ToString()),
                i => Store(Enum.Parse(type, names[i])));
        }
        else if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
        {
            bool whole = type == typeof(int) || type == typeof(long);
            var (min, max) = Values.RangeOf(entry.Validator);
            var candidates = Values.Candidates(min, max, whole);
            page = Numbers(label, candidates,
                () => Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture),
                v => Store(Convert.ChangeType(whole ? Math.Round(v) : v, type, CultureInfo.InvariantCulture)),
                null, null);
        }
        else return this; // строки и прочее из меню не редактируются

        var item = (OptionItem)Items[^1];
        item.IsDefault = () => entry.GetValueAsString() == entry.GetDefaultValueAsString();
        item.Reset = () =>
        {
            entry.ResetToDefault();
            entry.Category?.SaveToFile(false);
        };
        return page;
    }

    private Page Numbers(Func<string> label, IReadOnlyList<float> candidates, Func<float> get, Action<float> set,
                         Func<float, string> format, float? @default)
    {
        format ??= v => v.ToString("0.##", CultureInfo.InvariantCulture);
        var shown = new List<float>(); // значения, показанные при последнем чтении
        return Add(new OptionItem
        {
            Label = label,
            Read = () =>
            {
                float current = get();
                shown.Clear();
                shown.AddRange(candidates);
                if (!shown.Contains(current)) { shown.Add(current); shown.Sort(); }
                return (shown.ConvertAll(v => format(v)), shown.IndexOf(current));
            },
            Apply = i => { if (i >= 0 && i < shown.Count) set(shown[i]); },
            IsDefault = @default.HasValue ? () => Math.Abs(get() - @default.Value) < 1e-4f : null,
            Reset = @default.HasValue ? () => set(@default.Value) : null,
        });
    }

    private Page Add(Item item)
    {
        Items.Add(item);
        return this;
    }
}

internal abstract class Item
{
    public Func<string> Label;
}

internal sealed class HeaderItem : Item
{
    public bool IsPageTitle;
}

internal sealed class NoteItem : Item { }

internal sealed class ButtonItem : Item // Label — подпись слева (у Action), у кнопок без подписи null
{
    public ButtonLayout Layout;
    public List<MenuButton> Buttons;
}

internal sealed class CustomItem : Item
{
    public float Height;
    public Func<RectTransform, Selectable> Build;
}

// Всё, что показывается родным переключателем игры «< значение >»
internal sealed class OptionItem : Item
{
    public Func<(List<string> options, int current)> Read;
    public Action<int> Apply;
    public Func<bool> IsDefault; // null — сбросить нельзя
    public Action Reset;
}
