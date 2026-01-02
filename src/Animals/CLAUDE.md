# Animals Module

## Purpose
Tame animal and wildlife management with tabular navigation.

## Files
**Patches:** AnimalsMenuPatch.cs, WildlifeMenuPatch.cs
**States:** AnimalsMenuState.cs, WildlifeMenuState.cs
**Helpers:** AnimalsMenuHelper.cs, WildlifeMenuHelper.cs

## Key Shortcuts
- **Arrow Keys Up/Down** - Navigate animal list (rows)
- **Arrow Keys Left/Right** - Navigate columns
- **Enter** - Interact with current cell
- **Home/End** - Jump to first/last animal
- **Alt+S** - Sort by current column
- **Type letters** - Typeahead search

## Architecture

Both menus use `TabularMenuHelper<Pawn>` (from UI module) for shared navigation:
- Row/column navigation with wrap-around
- Sorting with item preservation
- Typeahead search integration
- Cell announcement building

### AnimalsMenuState (Colony Animals)
- 14+ columns (Name, Bond, Master, Slaughter, training columns, etc.)
- Dynamic training columns based on TrainableDef
- Submenu system for Master, AllowedArea, MedicalCare, FoodRestriction
- Default sort: Name ascending

### WildlifeMenuState (Wild Animals)
- 9 fixed columns (Name, Gender, LifeStage, Age, BodySize, Health, Pregnant, Hunt, Tame)
- Simple toggle interactions (Hunt, Tame)
- Default sort: BodySize descending

### TabularMenuHelper Integration
Both state classes create a `TabularMenuHelper<Pawn>` in `Open()`:
```csharp
tableHelper = new TabularMenuHelper<Pawn>(
    getColumnCount: MenuHelper.GetTotalColumnCount,
    getItemLabel: MenuHelper.GetAnimalName,
    getColumnName: MenuHelper.GetColumnName,
    getColumnValue: MenuHelper.GetColumnValue,
    sortByColumn: (items, col, desc) => MenuHelper.SortByColumn(items.ToList(), col, desc),
    defaultSortColumn: 0,
    defaultSortDescending: false
);
```

## Dependencies
**Requires:** ScreenReader/, Input/, UI/TabularMenuHelper

## Testing
- [ ] Animals menu navigation (rows, columns)
- [ ] Wildlife menu navigation (rows, columns)
- [ ] Typeahead search in both menus
- [ ] Sorting by column in both menus
- [ ] Cell interactions (toggles, submenus)
- [ ] Submenu navigation in Animals menu
