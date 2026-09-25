using System;
using System.Collections.Generic;
using MelonLoader;

namespace SpellBrigade.ModMenu;

// Страницы для модов, которые ничего не регистрировали: по одной на категорию
// MelonPreferences. Собираются заново при каждой постройке меню — категории,
// созданные позже, тоже попадут.
internal static class AutoPages
{
    public static List<Page> Build()
    {
        var pages = new List<Page>();
        foreach (var category in MelonPreferences.Categories)
        {
            if (category == null || category.IsHidden || Menu.IsClaimed(category.Identifier)) continue;
            var c = category;
            var page = new Page("auto:" + c.Identifier, () => string.IsNullOrEmpty(c.DisplayName) ? c.Identifier : c.DisplayName);
            page.AddCategory(c);
            if (page.Items.Count > 0) pages.Add(page);
        }
        pages.Sort((a, b) => string.Compare(a.Title(), b.Title(), StringComparison.CurrentCultureIgnoreCase));
        return pages;
    }
}
