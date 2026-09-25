# Run Customizer

*Random and custom difficulty, plus a random wizard pick — right in the game's menus.*

Version 1.0.0 · The Spell Brigade 1.1.4 · MelonLoader 0.7

## Features

### Difficulty

Two new buttons appear next to the game's own difficulties in the **Threat Level** window.

- **Random** — every setting is rolled on its own: enemy strength, spawn speed, enemy count and health drops, from easier than the easiest difficulty to harsher than the hardest. You find out what you got when the run starts.
- Switch **What's random** under the description to **Base difficulty**, and instead one of the difficulties you've unlocked is picked at random.
- **Custom** — set those four settings yourself with the arrows under the description.
- **See what you got** — the pause menu and the end-of-run screen show the mode and the rolled multipliers instead of the usual threat level.
- **Fair rewards** — the gold bonus follows the resulting difficulty. World progress counts for the nearest game difficulty, never higher than what you've unlocked.

### Wizards

Two new cards at the bottom of the wizard list.

- **Random Wizard** — a roulette of wizard silhouettes spins and stops on a random unlocked wizard, who gets picked just like a normal click.
- **Surprise Wizard** — the wizard is picked at random when the run starts. Until then the lobby shows your previous wizard, and only you see random wizards flicker on your seat.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) (0.7.x) for The Spell Brigade and launch the game once.
2. Put **RunCustomizer.dll** into `<Game folder>\Mods\`
3. Launch the game → pick a difficulty or a wizard as usual.

## The numbers

All values are multipliers, x1 = the easiest game difficulty.

- **Enemy strength** (health and damage): game values x1.1 / x2.05 / x3, random range x0.5 – x4.5
- **Spawn speed**: game values x1 / x1.43 / x2.5, random range x0.75 – x4
- **Enemy count**: game values x1 / x1.25 / x1.5, random range x0.75 – x2
- **Health drops**: game values x1 / x0.5 / x0.25, random range x0.1 – x1.5

## FAQ

**Does it work in co-op?**  
Difficulty modes are chosen by the host and work on the host's side, since the host runs the enemies and drops. The random wizard works for every player who has the mod. Co-op got less testing than solo, so feedback is welcome.

**Where are my settings saved?**  
In `<Game folder>\UserData\MelonPreferences.cfg`, section `[RunCustomizer]`.

**How do I uninstall?**  
Delete **RunCustomizer.dll** from the Mods folder. The game goes back to its normal difficulties and wizard list.

**Is it compatible with other mods?**  
Yes... With all my mods. (I will change it if not)

## Changelog

**1.0.0**

- Initial release
