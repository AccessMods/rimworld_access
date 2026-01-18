# Interior Detection Research - RimWorld Zones and Blueprints

## 1. Zone Cell Tracking

**File:** `Verse/Zone.cs`

Zones maintain coverage through a simple list:
- `public List<IntVec3> cells` - stores all cells in the zone
- `public void AddCell(IntVec3 c)` - adds a cell
- `public void RemoveCell(IntVec3 c)` - removes a cell
- `CheckContiguous()` - uses FloodFill to validate zone cells form a contiguous region

## 2. Blueprint Cell Tracking

**File:** `RimWorld/Blueprint.cs`

Blueprints use `CellRect` for rectangular areas:
- `Position` (IntVec3) - anchor cell
- `OccupiedRect()` - returns CellRect representing occupied area
- Blueprint cells accessed via `OccupiedRect().Cells`

## 3. Shape-Based Designators

**Files:** `Verse/DrawStyle_*.cs`

Each shape implements `Update(IntVec3 origin, IntVec3 target, List<IntVec3> buffer)`:

| Shape | Method | Output |
|-------|--------|--------|
| `DrawStyle_Line` | Straight H/V line | Line cells between points |
| `DrawStyle_FilledRectangle` | `CellRect.Cells` | All cells in rectangle |
| `DrawStyle_EmptyRectangle` | `CellRect.EdgeCells` | Only perimeter cells |
| `DrawStyle_FilledOval` | Distance formula | Ellipse interior |
| `DrawStyle_AngledLine` | Bresenham algorithm | Diagonal line |

## 4. CellRect - Core Area Class

**File:** `Verse/CellRect.cs`

Key properties:
- `IEnumerable<IntVec3> Cells` - all cells in rect
- `IEnumerable<IntVec3> EdgeCells` - only perimeter cells
- `IEnumerable<IntVec3> Corners` - the 4 corner cells
- `static CellRect FromLimits(IntVec3 origin, IntVec3 target)` - creates rect from corners

## 5. DesignationDragger - Placement System

**File:** `Verse/DesignationDragger.cs`

Central system for multi-cell placements:
- `dragCells` - the cells that will be affected
- `UpdateCellBuffer()` - calls DrawStyle.Update() to get affected cells
- Each cell validated against `CanDesignateCell()`

## 6. Enclosure Detection for Blueprints

**File:** `Verse/AnimalPenBlueprintEnclosureCalculator.cs`

This is highly relevant for detecting interiors with blueprints:

```csharp
// VisitPen(IntVec3 position, Map map)
// - Performs FloodFill from starting position
// - Checks if cells touch map edge
// - Sets isEnclosed = false if any cell reaches map edge
// - PassCheck validates cell walkability
```

Key checks:
- Uses `map.floodFiller.FloodFill()` to traverse connected cells
- Checks for fences: `thingDef.IsFence`
- Checks for doors: `thingDef.IsDoor`
- Checks for impassable: `def.passability == Traversability.Impassable`
- Checks map edge: `c.OnEdge(map)`

## 7. FloodFiller Algorithm

**File:** `Verse/FloodFiller.cs`

Core algorithm for detecting connected areas:
```csharp
public void FloodFill(
    IntVec3 root,                          // Starting cell
    Predicate<IntVec3> passCheck,          // Can we enter this cell?
    Func<IntVec3, int, bool> processor,    // Called for each cell
    int maxCellsToProcess = int.MaxValue,
    bool rememberParents = false,
    IEnumerable<IntVec3> extraRoots = null
)
```

Uses BFS (breadth-first search) with queue-based traversal.

## 8. Recommended Approach for Interior Detection

For detecting interior tiles when placing shapes:

1. **After shape placement is complete:**
   - Take cells from the shape
   - Apply FloodFill from a potential interior cell
   - If FloodFill reaches map edge → NOT enclosed
   - If FloodFill doesn't reach map edge → IS enclosed

2. **For blueprint enclosure detection:**
   - Use `AnimalPenBlueprintEnclosureCalculator` as reference
   - Check if placement would create an enclosure

3. **Key utilities:**
   - `CellRect.EdgeCells` - perimeter cells
   - `CellRect.Cells` - all cells
   - `FloodFiller.FloodFill()` - connectivity analysis
   - `IntVec3.OnEdge(map)` - check if on map boundary
