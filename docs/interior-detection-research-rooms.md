# Interior Detection Research - RimWorld Room and Region System

## Overview

RimWorld detects enclosed rooms using a hierarchical system: **Region → District → Room**. The game uses flood-fill algorithms to identify contiguous walkable areas and separates them by walls and doors.

## 1. Enclosed Room Detection: `ProperRoom` Property

**Location:** `Verse/Room.cs` lines 536-553

A room is considered "properly enclosed" (interior) when:

1. It does **NOT** touch the map edge
2. It contains at least ONE region of type `RegionType.Normal` (walkable interior floor)

```csharp
public bool ProperRoom
{
    get
    {
        if (TouchesMapEdge)
            return false;  // Not enclosed if at map boundary

        for (int i = 0; i < districts.Count; i++)
        {
            if (districts[i].RegionType == RegionType.Normal)
                return true;  // Enclosed!
        }
        return false;
    }
}
```

## 2. Region Type Detection System

**Location:** `Verse/RegionTypeUtility.cs`

Every map cell is classified into one of five types via `GetExpectedRegionType()`:

```csharp
public static RegionType GetExpectedRegionType(this IntVec3 c, Map map)
{
    if (!c.InBounds(map))
        return RegionType.None;

    if (c.GetDoor(map) != null)
        return RegionType.Portal;      // Door/opening

    if (c.GetFence(map) != null)
        return RegionType.Fence;       // Fence

    if (c.WalkableByNormal(map))
        return RegionType.Normal;      // Interior walkable floor

    // Check for full obstacles (walls)
    List<Thing> thingList = c.GetThingList(map);
    for (int i = 0; i < thingList.Count; i++)
    {
        if (thingList[i].def.Fillage == FillCategory.Full)
            return RegionType.None;    // Blocked by obstacle
    }

    return RegionType.ImpassableFreeAirExchange;  // Outdoor/vacuum
}
```

**RegionType Values:**
- `None` - Blocked by walls or fully-filled objects
- `Normal` - Walkable interior floor
- `Portal` - Door tiles (each door is a 1-cell region)
- `Fence` - Fence posts
- `ImpassableFreeAirExchange` - Outdoor/vacuum spaces

## 3. Region Creation: Flood-Fill Algorithm

**Location:** `Verse/RegionMaker.cs` and `Verse/FloodFiller.cs`

When the map updates, regions are created by expanding from a root cell using breadth-first flood-fill:

1. Start from a cell with unknown region
2. Determine its expected type via `GetExpectedRegionType()`
3. FloodFill expands to adjacent cardinal cells of the same type
4. Walls (FillCategory.Full) and doors automatically stop expansion
5. Result: A contiguous group of same-type cells = one Region

## 4. Boundary Detection

**Location:** `Verse/Room.cs`

Rooms provide properties to find boundary cells:

```csharp
public IEnumerable<IntVec3> BorderCells
{
    // All 8 adjacent neighbors that aren't in this room
}

public IEnumerable<IntVec3> BorderCellsCardinal
{
    // Only cardinal (4) neighbors that aren't in this room
}
```

## 5. Interior Obstacles and Contents

**Location:** `Verse/Room.cs` lines 481-506

Query what's inside a room:

```csharp
public List<Thing> ContainedAndAdjacentThings
{
    // Iterates through all regions in room
    // Collects all Things from each region's ListerThings
    // Returns deduplicated list
}
```

## 6. Key Classes and Files

| Class | Purpose | Location |
|-------|---------|----------|
| `Room` | Container for related regions, properties, stats | `Verse/Room.cs` |
| `Region` | Contiguous cells of same type | `Verse/Region.cs` |
| `District` | Contiguous regions of same type | `Verse/District.cs` |
| `RegionAndRoomUpdater` | Orchestrates room/region reconstruction | `Verse/RegionAndRoomUpdater.cs` |
| `RegionMaker` | Creates regions via flood-fill | `Verse/RegionMaker.cs` |
| `FloodFiller` | BFS algorithm | `Verse/FloodFiller.cs` |

## 7. Detecting User-Built Enclosures

**Approach:**

1. After wall placement, call `map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms()`
2. Check if new room formed where `ProperRoom == true`
3. Use `room.Cells` to get all interior cells
4. Use `room.BorderCells` to find walls around the room
5. Use `room.ContainedAndAdjacentThings` to find obstacles inside
