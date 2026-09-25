# Gold & XP Multiplier

*Earn more gold, level faster and rank up your wizards — all configurable.*

Version 2.5.0 · The Spell Brigade 1.1.4 · MelonLoader 0.7

## Features

- **Gold multiplier** — multiplies all gold earned at the end of a run: the base reward *and* every bonus (difficulty, curses, unused wizard, final upgrades).
- **Run XP multiplier** — all experience collected during a run is multiplied, so you level up and pick spells more often.
- **Wizard Rank XP multiplier** — your wizard ranks up faster after each run. Works with prestige and stops cleanly at max rank.
- **In-game settings tab** — a new **Multipliers** tab in **Options**, next to Video, Audio and Controls. Works with mouse, keyboard and gamepad, in the main menu and the pause menu. Changes apply instantly.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) (0.7.x) for The Spell Brigade and launch the game once.
2. Put **GoldXpMultiplier.dll** into `<Game folder>\Mods\`
3. Launch the game → **Options → Multipliers**. Done!

## Settings

**Options → Multipliers** in the main menu or the pause menu:

- **Mod Enabled** — On / Off
- **Gold per Run** — x0.5 … x100
- **Run XP** — x0.5 … x100
- **Wizard Rank XP** — x0.5 … x100

x1 = vanilla. Everything is saved to `<Game folder>\UserData\MelonPreferences.cfg`, so you can also type any custom value there:

```ini
[GoldMultiplier]
Enabled = true
Multiplier = 2.0        # gold per run
XpMultiplier = 2.0      # run XP
RankXpMultiplier = 1.0  # wizard rank XP
```

## Co-op

- **Gold** and **Wizard Rank XP** are yours alone — each player who wants them needs the mod.
- **Run XP** is shared by the party, so it follows the **host's** setting.

## FAQ

**How do I uninstall?**  
Delete **GoldXpMultiplier.dll** from the Mods folder. Everything you earned stays.

**Something doesn't work after a game update?**  
Each feature hooks the game separately. If an update breaks one of them, the rest keep working and the log (`MelonLoader\Latest.log`) says which one — please post it in [GitHub issues](https://github.com/Everelsu/SpellBrigade/issues).

**Is it compatible with other mods?**  
Yes, including Keybinds Unlocked — both add their own things to the Options menu.

## Languages

English, Русский, Українська, Deutsch, Français, Italiano, Nederlands, Polski, Português (Brasil), Português (Portugal), Español (España), Español (México), 日本語, 한국어, 简体中文, 繁體中文, ไทย

## Changelog

**2.5.0**

- Updated for The Spell Brigade 1.1.4
- New: Run XP multiplier
- New: Wizard Rank XP multiplier
- New: in-game "Multipliers" tab in Options (mouse, keyboard, gamepad)
- New: end-of-run "x2 slam" reveal on the reward screen
- New: translations for all 17 game languages
- New: automatic repair of corrupted wizard ranks
- Gold multiplier now applies to every bonus and shows correctly on the reward screen

**1.0**

- Initial release: configurable end-of-run gold multiplier
