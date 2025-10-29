RimWorld Access - screen reader accessibility for RimWorld
==========================================

OVERVIEW
--------
This mod adds keyboard navigation and screen reader support to RimWorld.
All selections are copied to the clipboard for screen reader accessibility.

Installation:
1. Install RimWorld, and run it once to set up the folder structure correctly.
2. Install mellon loader for RimWorld.
3. Copy the RimWorld_access.dll to the RimWorld mods folder.
4. Launch the game.  UI text should be coppied to your clipboard.

MAIN MENU
---------
Arrow Keys    Navigate menu options
Enter         Select menu item


IN-GAME NAVIGATION
------------------
Arrow Keys    Move map cursor (announces terrain, buildings, items, pawns)
comma (,): Cycle to previous colonist.
period (.): Cycle to next colonist.
Enter         Open inspection menu at cursor position
Escape        Open pause menu

(Note: All menus use Arrow Keys to navigate, Enter to confirm, Escape to close)


BUILD SYSTEM (Press A)
----------------------
1. Select category (Orders, Structure, Production, etc.)
2. Select build tool (Wall, Door, Bed, etc.)
3. If required, select material (Wood, Steel, etc.)
4. Use Arrow Keys to position, space to place, enter to confirm, or Escape to cancel


ZONE SYSTEM (Press Z)
---------------------
1. Select zone type (Growing zone, Stockpile, etc.)
2. Use Arrow Keys to navigate, space to add/remove cells, enter to confirm, or escape to cancel.


SCHEDULE MENU (Press S)
------------------------
Manage colonist work schedules across all 24 hours:

Arrow Keys    Navigate grid (Up/Down: pawns, Left/Right: hours)
Tab           Cycle assignment type and apply to current cell (Anything/Work/Joy/Sleep/Meditate)
Space         Apply current selection to cell
Shift+Right   Fill rest of row with selected assignment
Ctrl+C        Copy current pawn's entire schedule
Ctrl+V        Paste copied schedule to current pawn
Enter         Save all pending changes and close
Escape        Cancel changes and close

Navigation: Use arrows to move through the grid. Current cell shows pawn name, hour, and assignment.
Changes are marked as "(pending)" until you press Enter to save.


COLONIST ACTIONS
----------------
]             Open order menu (includes draft mode orders like Move, Attack)
R             Toggle draft mode for selected colonist
Alt+M         Display mood information and thoughts
Alt+N         Display needs
Alt+H         Display health
Alt+W         Open work menu (Space toggles work types, Enter confirms)
Alt+G         Display gear
S             Open schedule menu (manage colonist timetables)


QUICK NAVIGATION (Press J)
---------------------------
Opens jump menu with categories:
- Colonists
- Items
- Events
- Buildings

Left/Right    Expand/collapse categories
Up/Down       Navigate items
Enter         Jump to selected item


OTHER SHORTCUTS
---------------
Delete        Delete selected save file (in save/load menu)
p      open research project selection menu.

ADDITIONAL NOTES
----------------
This is a closed beta.  Many features are not supported, and those that are may have unexpected bugs or interactions.  
