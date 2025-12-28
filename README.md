# RimWorld Access

Screen reader accessibility for RimWorld. Uses the Tolk library to communicate with NVDA, JAWS, and other screen readers. Falls back to Windows SAPI if no screen reader is detected.

> **Note:** This mod is an early version. Errors may be present. This documentation is a rough overview—for detailed questions and clarifications, join the [Discord server](https://discord.gg/Aecaqnbr).

## Table of Contents

- [Installation](#installation)
- [Main Menu](#main-menu)
- [Map Navigation](#map-navigation)
- [Tile Information (Keys 1-7)](#tile-information-keys-1-7)
- [Time Controls](#time-controls)
- [Build & Zone Systems](#build--zone-systems)
- [Colonist Actions](#colonist-actions)
- [Work Menu (F1)](#work-menu-f1)
- [Schedule Menu (F2)](#schedule-menu-f2)
- [Assign Menu (F3)](#assign-menu-f3)
- [Animals Menu (F4)](#animals-menu-f4)
- [Scanner System](#scanner-system)
- [World Map (F8)](#world-map-f8)
- [Colony Inventory (I)](#colony-inventory-i)
- [Trading System](#trading-system)
- [Other Shortcuts](#other-shortcuts)

## Installation

1. Install RimWorld and run it once.
2. Install MelonLoader for RimWorld. Run Mellonloader.Installer. Note that object navigation is required.
3. Copy `rimworld_access.dll`, `Tolk.dll`, and `nvdaControllerClient64.dll` to the RimWorld Mods folder.
4. Launch the game.

## Main Menu

| Key | Action |
|-----|--------|
| Arrow Keys | Navigate menu options |
| Enter | Select menu item |

## Map Navigation

| Key | Action |
|-----|--------|
| Arrow Keys | Move cursor one tile |
| I | Open colony inventory |
| Enter | Open inspect panel |
| Escape | Open pause menu |

Tiles announce: pawns, buildings, items, plants, terrain, zone, roof status, and coordinates.

## Tile Information (Keys 1-7)

| Key | Info |
|-----|------|
| 1 | Items and pawns at cursor |
| 2 | Flooring details (terrain, beauty, path cost) |
| 3 | Plant information (species, growth, harvestable) |
| 4 | Brightness and temperature |
| 5 | Room statistics (impressiveness, cleanliness, wealth) |
| 6 | Power information (status, network, generation/consumption) |
| 7 | Area information (home area, allowed areas) |

*Note: Shift+1/2/3 are time controls. Number keys only work without Shift held.*

## Time Controls

| Key | Action |
|-----|--------|
| Shift+1 | Normal speed |
| Shift+2 | Fast speed (3x normal) |
| Shift+3 | Superfast speed (6x normal) |
| Space | Pause/unpause |

## Build & Zone Systems

| Key | Action |
|-----|--------|
| Tab | Open architect menu (select category → tool → material → place with Space) |
| Space | Place building or toggle cell selection (zones) |
| Shift+Space | Cancel blueprint at cursor position |
| R | Rotate building |
| Z | Open zone menu (select type → Space to add/remove cells) |

## Colonist Actions

| Key | Action |
|-----|--------|
| , / . | Cycle previous/next colonist |
| Alt+C | Jump camera to selected colonist |
| ] | Open order menu |
| R | Toggle draft mode |
| G | Open gizmos for selected colonist/object |
| Alt+M | Display mood and thoughts |
| Alt+N | Display needs |
| Alt+H | Display health |
| Alt+F | Unforbid all items on map |
| F1 | Work menu |
| F2 | Schedule menu |
| F3 | Assign menu |

## Work Menu (F1)

| Key | Action |
|-----|--------|
| Up/Down | Navigate work types |
| Tab / Shift+Tab | Next/previous colonist |
| M | Toggle simple/manual priority mode |
| Space | Toggle work type (simple) or toggle disabled/priority 3 (manual) |
| 0-4 | Set priority directly (manual mode) |
| Shift+Up/Down | Reorder work type priority |
| Enter | Save and close |
| Escape | Cancel and close |

## Schedule Menu (F2)

| Key | Action |
|-----|--------|
| Up/Down | Navigate pawns |
| Left/Right | Navigate hours |
| Tab | Cycle assignment type (Anything/Work/Joy/Sleep/Meditate) |
| Space | Apply selection to cell |
| Shift+Right | Fill rest of row with selection |
| Ctrl+C / Ctrl+V | Copy/paste pawn schedule |
| Enter | Save and close |
| Escape | Cancel and close |

## Assign Menu (F3)

| Key | Action |
|-----|--------|
| Left/Right | Switch policy categories |
| Up/Down | Navigate policies |
| Enter | Apply policy to colonist |
| Tab | Next colonist |
| E | Open policy editor |
| Escape | Close |

Categories: Outfit, Food Restrictions, Drug Policies, Allowed Areas, Reading Policies (Ideology DLC).

## Animals Menu (F4)

| Key | Action |
|-----|--------|
| Up/Down | Navigate animals |
| Left/Right | Navigate property columns |
| Enter | Toggle checkbox / open dropdown |
| S | Sort by current column |
| Escape | Close |

Columns include: Name, Bond, Master, Slaughter, Gender, Age, Training, Follow settings, Area, Medical Care, Food Restriction, Release to Wild.

## Scanner System

Linear navigation through all map items by category. Always available during map navigation.

| Key | Action |
|-----|--------|
| Page Up/Down | Navigate items in subcategory |
| Ctrl+Page Up/Down | Switch categories |
| Shift+Page Up/Down | Switch subcategories |
| Alt+Page Up/Down | Navigate within bulk groups |
| Home | Jump cursor to current item |
| Alt+Home | Toggle auto-jump mode (cursor automatically follows scanner) |
| End | Read distance/direction to item |

Categories: Colonists, Tame Animals, Wild Animals, Buildings, Trees, Plants, Items, Mineable Tiles.

**Auto-jump mode:** When enabled, the map cursor automatically jumps to each item as you navigate with Page Up/Down. Distance calculations always update based on current cursor position.

## World Map (F8)

| Key | Action |
|-----|--------|
| F8 | Toggle world view |
| Arrow Keys | Navigate tiles (camera-relative) |
| Home | Jump to home settlement |
| End | Jump to nearest caravan |
| Page Up/Down | Cycle settlements |
| I | Read tile details |
| S | Open settlement browser |

### Caravan Controls

| Key | Action |
|-----|--------|
| C | Open caravan formation |
| Left/Right | Change tabs |
| Up/Down | Navigate items |
| Enter / + / - | Adjust quantities |
| D | Set destination |
| T | Send caravan |
| Shift+C | Reform caravan (temp maps) |
| , / . | Cycle caravans |
| ] | Orders on selected tile |

## Colony Inventory (I)

| Key | Action |
|-----|--------|
| Up/Down | Navigate categories/items |
| Left/Right | Collapse/expand |
| Enter | Activate (expand or execute action) |
| Escape | Close |

Actions per item: Jump to location, View details.

## Trading System

### List View

| Key | Action |
|-----|--------|
| Up/Down | Navigate items |
| Left/Right | Switch categories (Currency/Colony/Trader) |
| Enter | Enter quantity adjustment |
| A | Accept trade |
| G | Toggle gift mode |
| P | Price breakdown |
| B | Announce trade balance |
| R / Shift+R | Reset item / reset all |
| Escape | Close |

### Quantity Adjustment

| Key | Action |
|-----|--------|
| Up/Down or +/- | Adjust ±1 |
| Shift+Up/Down | Adjust ±10 |
| Ctrl+Up/Down | Adjust ±100 |
| Alt+Up/Down | Set to max sell/buy |
| Enter / Escape | Exit adjustment mode |

## Other Shortcuts

| Key | Action |
|-----|--------|
| L | View alerts and letters (messages, inbox, warnings) |
| F6 | Research menu |
| F7 | Quest menu |
| Delete | Delete save file (in save/load menu) |

---

**Questions?** Join the [Discord server](https://discord.gg/Aecaqnbr) for support and discussion.
