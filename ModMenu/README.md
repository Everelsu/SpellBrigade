# Mod Menu

An **Options → Mods** tab for The Spell Brigade (MelonLoader 0.7): one scrolling list with a section per mod, sorted by name. Everything is built from the game's own controls, so it works with mouse, keyboard and gamepad in the main menu and the pause menu.

## For players

| | |
|---|---|
| **Sections** | Each mod is a section you can fold: click its name (or press Enter / A on it). The menu remembers which ones are open; folded sections still show up in search results. |
| **Search** | Click the field or press **Ctrl+F** and type. Matching settings stay, the match is highlighted; typing a mod's name shows its whole section. **Enter** jumps to the first result, **Esc** leaves the field (the search stays), **×** clears it. While you type, the game doesn't react to the keyboard. |
| **Reset** | The ↻ icon to the right of a setting appears when it differs from the default; click it, or press **Delete** on the selected row. **Defaults ↻** next to a mod's name resets the whole section. |
| **Mouse wheel** | Over any `< value >` selector (in this menu, in other mods and in the game's own menus) the wheel changes the value. Elsewhere it scrolls the list. |

## Zero code: MelonPreferences

Every visible MelonPreferences category gets its own section automatically:

| Entry type | Shown as |
|---|---|
| `bool` | On / Off |
| `int`, `long`, `float`, `double` | value picker, limited by a `ValueRange<T>` validator if the entry has one |
| enum | list of names |
| `string` and others | not shown |

Hidden categories and entries (`is_hidden`) are skipped. Changes are saved to `MelonPreferences.cfg` right away, and reset goes back to the entry's default value.

## The API

Reference `ModMenu.dll` and register a page in `OnInitializeMelon`:

```csharp
using SpellBrigade.ModMenu;

Menu.AddPage("MyMod", () => Strings.Get("title"))      // title and labels can follow the game language
    .Header("General")
    .Toggle("Enabled", () => enabled.Value, v => enabled.Value = v, @default: true)
    .Choice("Mode", new[] { "Easy", "Hard" }, () => mode, i => mode = i, @default: 0)
    .Number("Speed", 0.5f, 3f, 0.25f, () => speed.Value, v => speed.Value = v, v => $"x{v}", @default: 1f)
    .Number("Gold", new[] { 1f, 2f, 5f, 10f }, () => gold.Value, v => gold.Value = v)   // presets instead of a range
    .Entry(someMelonPreferencesEntry)                 // any MelonPreferences entry, typed automatically, resettable
    .Note("Small grey explanatory text")
    .Action("Card position", "Edit", OpenEditor)       // label on the left, button in the value column
    .Buttons(new MenuButton("Import", Import), new MenuButton("Export", Export))  // several buttons in one line
    .Button("Reset everything", ResetEverything)       // one button, centered
    .Custom(150f, row => BuildAnythingYouWant(row));  // your own UI; return a Selectable for gamepad focus, or null
```

### Buttons

| Call | Looks like |
|---|---|
| `.Button(text, onClick)` | one button, centered, as wide as its text |
| `.Button(text, onClick, ButtonLayout.Left / Right / Stretch / Value)` | same, placed left, right, full width, or in the value column |
| `.Buttons(a, b, c)` | several buttons in one line, sharing the full width |
| `.Buttons(ButtonLayout.Right, a, b)` | several buttons in one line, as wide as their text, aligned right (also `Left`, `Center`, `Value`) |
| `.Action(label, button, onClick)` | a setting-like row: label on the left, the button where values are |

`MenuButton` takes a `string` or a `Func<string>` (to follow the game language). On a gamepad, left/right moves between the buttons of one line.

- `@default` is optional. With it, the row gets the reset icon (and **Delete**) whenever the current value differs, and the mod's title gets **Defaults ↻** to reset the whole section.
- A page with the same id is returned again, so several parts of a mod can add to one page.
- A page whose id matches a MelonPreferences category replaces that category's automatic section. `Entry` / `Category` hide the automatic section of their category too; `Menu.HideCategory(id)` hides one without adding anything.
- `Menu.Refresh()` re-reads all values, e.g. after you changed settings from code.
- The tab is built when the game creates its settings window, so register pages in `OnInitializeMelon`.

### Optional dependency

To keep your mod working without Mod Menu, mark it optional and touch the API only when it's loaded:

```csharp
[assembly: MelonOptionalDependencies("ModMenu")]

if (FindMelon("Mod Menu", "Relsev") != null) MenuPage.Register(); // MenuPage is the only class that uses the API
```

See `RunCustomizer/MenuPage.cs` or `PinnedChallenges/MenuPage.cs` for complete examples.
