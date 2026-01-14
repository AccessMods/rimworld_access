# UI Module

## Purpose
Generic dialog navigation and windowless menu systems used across all modules.

## Files
**Patches:** DialogInterceptionPatch.cs, MessageBoxAccessibilityPatch.cs
**States:** WindowlessDialogState.cs, WindowlessFloatMenuState.cs, WindowlessPauseMenuState.cs, WindowlessSaveMenuState.cs, WindowlessOptionsMenuState.cs, WindowlessConfirmationState.cs, GiveNameDialogState.cs
**Utilities:** Dialog_NameAllowedArea.cs, DialogElementExtractor.cs, StatsHelper.cs, MenuHelper.cs, TypeaheadSearchHelper.cs, TabularMenuHelper.cs

## Key Shortcuts
- **Escape** - Pause menu
- **Arrow Keys** - Navigate menus
- **Enter** - Confirm
- **Delete** - Delete save file (in save menu)

## Architecture
Windowless menus replace RimWorld's FloatMenu windows with keyboard-navigable alternatives. DialogInterceptionPatch handles all Dialog_NodeTree instances.

## Dependencies
**Requires:** ScreenReader/, Input/
**Used by:** All modules (confirmation dialogs, float menus used everywhere)

## Common Patterns
### Windowless Menu Pattern
```csharp
WindowlessFloatMenuState.Open(options);
// Up/Down navigate, Enter executes, Escape closes
```

### Dialog Navigation
All Dialog_NodeTree instances automatically get keyboard navigation via DialogInterceptionPatch.

### TabularMenuHelper Pattern
Generic helper for tabular menus (rows x columns) with:
- Row/column navigation with wrap-around
- Sorting with item preservation
- Typeahead search integration
- Cell announcement building

```csharp
// Create helper with data access delegates
tableHelper = new TabularMenuHelper<TItem>(
    getColumnCount: () => 9,
    getItemLabel: item => item.Name,
    getColumnName: idx => columns[idx],
    getColumnValue: (item, idx) => GetValue(item, idx),
    sortByColumn: (items, col, desc) => SortItems(items, col, desc)
);

// Use helper methods
tableHelper.SelectNextRow(itemCount);
tableHelper.SelectNextColumn();
tableHelper.ToggleSortByCurrentColumn(items, out direction);
tableHelper.HandleTypeahead(c, items, out newIndex);
tableHelper.BuildCellAnnouncement(item, count, includeLabel);
```

**Used by:** Animals/AnimalsMenuState, Animals/WildlifeMenuState

## Testing
- [ ] Pause menu accessible
- [ ] Save/load menu functional
- [ ] Options menu navigable
- [ ] Confirmation dialogs work
- [ ] Float menus accessible
