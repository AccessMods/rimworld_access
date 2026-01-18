# Interior/Obstacle Detection - Implementation Plan

## Overview

This plan covers detecting obstacles inside:
1. **Wall enclosures** - When wall blueprints form an enclosed area
2. **Zones** - When creating or extending zones

Both share common obstacle detection logic and scanner integration.

---

## Shared Infrastructure

### New File: `src/Building/ObstacleDetector.cs`

A shared helper for detecting obstacles in a set of cells.

```csharp
namespace RimWorldAccess
{
    public static class ObstacleDetector
    {
        /// <summary>
        /// Finds obstacles in a set of cells.
        /// </summary>
        public static List<ScannerItem> FindObstacles(
            Map map,
            IEnumerable<IntVec3> cells,
            IntVec3 cursorPosition)
        {
            var obstacles = new List<ScannerItem>();
            var processedThings = new HashSet<Thing>();

            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map))
                    continue;

                // Check for mineable rock/ore (natural rock edifice)
                Building edifice = cell.GetEdifice(map);
                if (edifice != null &&
                    edifice.def.building?.isNaturalRock == true &&
                    !processedThings.Contains(edifice))
                {
                    obstacles.Add(new ScannerItem(edifice, cursorPosition));
                    processedThings.Add(edifice);
                    continue; // Natural rock covers the whole cell
                }

                // Check other things at cell
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (processedThings.Contains(thing))
                        continue;

                    if (IsObstacle(thing))
                    {
                        obstacles.Add(new ScannerItem(thing, cursorPosition));
                        processedThings.Add(thing);
                    }
                }
            }

            // Sort by distance from cursor
            obstacles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return obstacles;
        }

        /// <summary>
        /// Determines if a thing is an obstacle.
        /// Obstacles: mineable tiles, structures, trees, items
        /// NOT obstacles: blueprints, frames, pawns
        /// </summary>
        public static bool IsObstacle(Thing thing)
        {
            if (thing == null)
                return false;

            // Blueprints/frames are NOT obstacles (user placed intentionally)
            if (thing.def.IsBlueprint || thing.def.IsFrame)
                return false;

            // Pawns are NOT obstacles
            if (thing is Pawn)
                return false;

            // Mineable tiles (natural rock, ore)
            if (thing is Building building && building.def.building?.isNaturalRock == true)
                return true;

            // Trees
            if (thing is Plant plant && plant.def.plant?.IsTree == true)
                return true;

            // Other structures/buildings
            if (thing is Building)
                return true;

            // Items on ground
            if (thing.def.category == ThingCategory.Item)
                return true;

            return false;
        }

        /// <summary>
        /// Adds obstacles to scanner as temporary category.
        /// </summary>
        public static void AddToScanner(List<ScannerItem> obstacles, string categoryName)
        {
            if (obstacles == null || obstacles.Count == 0)
            {
                ScannerState.RemoveTemporaryCategory();
                return;
            }

            ScannerState.CreateTemporaryCategory(categoryName, obstacles);
        }

        /// <summary>
        /// Removes obstacle category from scanner.
        /// </summary>
        public static void ClearFromScanner()
        {
            ScannerState.RemoveTemporaryCategory();
        }
    }
}
```

---

## Phase 1: Wall Enclosure Detection

### New File: `src/Building/EnclosureDetector.cs`

Detects when wall blueprints form enclosed areas.

```csharp
namespace RimWorldAccess
{
    public class Enclosure
    {
        public List<IntVec3> InteriorCells { get; set; }
        public List<ScannerItem> Obstacles { get; set; }
        public int CellCount => InteriorCells?.Count ?? 0;
        public int ObstacleCount => Obstacles?.Count ?? 0;
    }

    public static class EnclosureDetector
    {
        private const int MAX_ENCLOSURE_CELLS = 10000; // Performance limit

        /// <summary>
        /// Detects enclosures formed by wall blueprints combined with existing walls/mountains.
        /// </summary>
        public static List<Enclosure> DetectEnclosures(List<Thing> blueprints, Map map)
        {
            if (map == null || blueprints == null || blueprints.Count == 0)
                return new List<Enclosure>();

            // Build set of wall blueprint positions
            var wallCells = new HashSet<IntVec3>();
            foreach (Thing blueprint in blueprints)
            {
                if (IsWallBlueprint(blueprint))
                    wallCells.Add(blueprint.Position);
            }

            if (wallCells.Count == 0)
                return new List<Enclosure>();

            // Find candidate interior cells (neighbors of walls that aren't walls)
            var candidates = FindCandidateInteriorCells(wallCells, map);

            // Flood fill from each candidate to find enclosures
            var enclosures = new List<Enclosure>();
            var processedCells = new HashSet<IntVec3>();
            var cursorPos = MapNavigationState.CurrentCursorPosition;

            foreach (IntVec3 candidate in candidates)
            {
                if (processedCells.Contains(candidate))
                    continue;

                var (isEnclosed, interiorCells) = TryFloodFill(candidate, wallCells, map);

                // Mark all cells as processed
                foreach (var cell in interiorCells)
                    processedCells.Add(cell);

                if (isEnclosed && interiorCells.Count > 0)
                {
                    var obstacles = ObstacleDetector.FindObstacles(map, interiorCells, cursorPos);
                    enclosures.Add(new Enclosure
                    {
                        InteriorCells = interiorCells,
                        Obstacles = obstacles
                    });
                }
            }

            return enclosures;
        }

        private static bool IsWallBlueprint(Thing thing)
        {
            if (thing?.def == null)
                return false;

            if (!thing.def.IsBlueprint && !thing.def.IsFrame)
                return false;

            if (thing.def.entityDefToBuild is ThingDef thingDef)
            {
                if (thingDef.building?.isWall == true)
                    return true;
                if (thingDef.passability == Traversability.Impassable)
                    return true;
            }

            return false;
        }

        private static HashSet<IntVec3> FindCandidateInteriorCells(HashSet<IntVec3> wallCells, Map map)
        {
            var candidates = new HashSet<IntVec3>();

            foreach (IntVec3 wallCell in wallCells)
            {
                // Check 4 cardinal neighbors
                foreach (IntVec3 offset in GenAdj.CardinalDirections)
                {
                    IntVec3 neighbor = wallCell + offset;

                    if (!neighbor.InBounds(map))
                        continue;

                    // Skip if also a wall blueprint
                    if (wallCells.Contains(neighbor))
                        continue;

                    // Skip if impassable (mountain, existing wall)
                    if (neighbor.Impassable(map))
                        continue;

                    candidates.Add(neighbor);
                }
            }

            return candidates;
        }

        private static (bool isEnclosed, List<IntVec3> cells) TryFloodFill(
            IntVec3 startCell,
            HashSet<IntVec3> wallBlueprintCells,
            Map map)
        {
            var foundCells = new List<IntVec3>();
            bool reachedOpenArea = false;

            // Pass check: can we enter this cell?
            Predicate<IntVec3> passCheck = (IntVec3 c) =>
            {
                if (!c.InBounds(map))
                    return false;

                // Wall blueprint = boundary
                if (wallBlueprintCells.Contains(c))
                    return false;

                // Impassable terrain (mountain, existing wall) = boundary
                if (c.Impassable(map))
                    return false;

                // Check for existing wall buildings/blueprints
                foreach (Thing thing in c.GetThingList(map))
                {
                    if (thing is Building && thing.def.building?.isWall == true)
                        return false;

                    if ((thing.def.IsBlueprint || thing.def.IsFrame) &&
                        thing.def.entityDefToBuild is ThingDef td &&
                        (td.building?.isWall == true || td.passability == Traversability.Impassable))
                        return false;
                }

                return true;
            };

            // Cell processor: collect cells, check for "escape"
            Func<IntVec3, int, bool> processor = (IntVec3 c, int dist) =>
            {
                foundCells.Add(c);

                // If we've gone too far, area is too large to be meaningful enclosure
                if (foundCells.Count >= MAX_ENCLOSURE_CELLS)
                {
                    reachedOpenArea = true;
                    return true; // Stop
                }

                return false; // Continue
            };

            // Run flood fill
            map.floodFiller.FloodFill(startCell, passCheck, processor);

            bool isEnclosed = !reachedOpenArea && foundCells.Count > 0;
            return (isEnclosed, foundCells);
        }
    }
}
```

### Modify: `src/Building/ViewingModeState.cs`

**Add fields** (after line ~46):
```csharp
private static List<Enclosure> detectedEnclosures = new List<Enclosure>();
```

**In `Enter()` method** (after obstacle detection, around line 234):
```csharp
// Detect wall enclosures
detectedEnclosures.Clear();
if (isBuildDesignator)
{
    detectedEnclosures = EnclosureDetector.DetectEnclosures(PlacedBlueprints, Find.CurrentMap);
}
```

**Modify `BuildEntryAnnouncement()`** to include enclosure info:
```csharp
// Add after existing announcement building
if (detectedEnclosures.Count > 0)
{
    int totalObstacles = detectedEnclosures.Sum(e => e.ObstacleCount);

    if (detectedEnclosures.Count == 1)
    {
        var enc = detectedEnclosures[0];
        if (totalObstacles > 0)
            announcement += $" Enclosure formed with {enc.CellCount} tiles. {totalObstacles} interior obstacles.";
        else
            announcement += $" Enclosure formed with {enc.CellCount} tiles.";
    }
    else
    {
        announcement += $" {detectedEnclosures.Count} enclosures formed.";
        for (int i = 0; i < detectedEnclosures.Count; i++)
        {
            announcement += $" Area {i + 1}: {detectedEnclosures[i].ObstacleCount} obstacles.";
        }
    }
}
```

**Modify `UpdateObstacleCategory()`** to include interior obstacles:
```csharp
// Add interior obstacles to the scanner category
foreach (var enclosure in detectedEnclosures)
{
    foreach (var obstacle in enclosure.Obstacles)
    {
        // Mark as interior obstacle
        var item = new ScannerItem(obstacle.Thing, cursorPos);
        item.Label = $"Interior: {obstacle.Label}";
        allObstacles.Add(item);
    }
}
```

**In `Reset()`** (around line 1033):
```csharp
detectedEnclosures.Clear();
```

---

## Phase 2: Zone Obstacle Detection

### Modify: `src/Building/ZoneCreationState.cs`

**In `CreateZone()` method** (after zone is created, around line 589):
```csharp
// Check for obstacles in the new zone
var obstacles = ObstacleDetector.FindObstacles(
    map,
    selectedCells,
    MapNavigationState.CurrentCursorPosition);

string obstacleInfo = "";
if (obstacles.Count > 0)
{
    obstacleInfo = $" {obstacles.Count} obstacles found.";
    ObstacleDetector.AddToScanner(obstacles, "Zone Obstacles");
}

TolkHelper.Speak($"{zoneName} created with {selectedCells.Count} cells.{obstacleInfo}");
```

**In `ExpandZone()` method** (track new cells and check only those):
```csharp
// Track which cells were actually added
List<IntVec3> newlyAddedCells = new List<IntVec3>();

// In the foreach loop where cells are added:
if (cell.InBounds(map) && !expandingZone.ContainsCell(cell))
{
    expandingZone.AddCell(cell);
    newlyAddedCells.Add(cell);
    addedCount++;
}

// After loop, check only NEW cells for obstacles
if (newlyAddedCells.Count > 0)
{
    var obstacles = ObstacleDetector.FindObstacles(
        map,
        newlyAddedCells,  // Only check NEW cells
        MapNavigationState.CurrentCursorPosition);

    if (obstacles.Count > 0)
    {
        message += $" {obstacles.Count} obstacles in new area.";
        ObstacleDetector.AddToScanner(obstacles, "Zone Obstacles");
    }
}
```

**In `Reset()` method**:
```csharp
ObstacleDetector.ClearFromScanner();
```

---

## Edge Cases

| Scenario | Handling |
|----------|----------|
| Walls don't form enclosure | No enclosure announcement |
| Multiple separate enclosures | Report each with obstacle count |
| Enclosure against mountain | Mountain treated as boundary |
| Enclosure using existing walls | Combined enclosure detected |
| Very large enclosure (>10000 cells) | Skip detection (performance) |
| Zone extension | Only check NEW cells |
| Zone shrinking | Skip obstacle check |
| Blueprints inside enclosure | NOT reported as obstacles |

---

## Testing Checklist

### Wall Enclosures
- [ ] Rectangle of walls → enclosure detected
- [ ] L-shaped walls (not enclosed) → no enclosure
- [ ] Walls + existing structure → combined enclosure
- [ ] Walls + mountain → mountain as boundary
- [ ] Mineable rock inside → reported
- [ ] Trees inside → reported
- [ ] Items inside → reported
- [ ] Blueprints inside → NOT reported
- [ ] Multiple enclosures → all detected

### Zones
- [ ] New zone with obstacles → reported
- [ ] New zone without obstacles → clean announcement
- [ ] Zone extension → only new cells checked
- [ ] Zone shrinking → no obstacle check
- [ ] Scanner navigation works
- [ ] Obstacles cleared on exit

---

## Files Summary

| File | Action |
|------|--------|
| `src/Building/ObstacleDetector.cs` | CREATE - shared obstacle detection |
| `src/Building/EnclosureDetector.cs` | CREATE - wall enclosure detection |
| `src/Building/ViewingModeState.cs` | MODIFY - trigger enclosure detection |
| `src/Building/ZoneCreationState.cs` | MODIFY - zone obstacle detection |
