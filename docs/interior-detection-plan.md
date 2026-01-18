# Interior Detection for Shape Placement

## Problem Statement

When building walls, zones, or other structures, users need to know about obstacles or unusable tiles that are **inside** the enclosed area they're creating.

### Scenarios

1. **Rectangle shapes**: Easy - check all tiles within the rectangle bounds
2. **Line-based shapes**: Harder - user draws multiple line segments that form an enclosure. How do we detect which tiles are "interior"?

### User Story

- User builds walls in a shape (rectangle, L-shape, custom polygon)
- Inside that shape, there are 10 mineable tiles
- Those tiles can't be used for anything until mined
- User should be notified: "10 mineable tiles inside enclosed area"
- Those tiles should be added to an obstacles scanner category

### Key Questions

1. Does RimWorld already track "interior" vs "exterior" tiles for structures?
2. Does it detect enclosed rooms/areas automatically?
3. How does the game determine room boundaries?
4. Is there a mathematical/algorithmic approach we can use (flood fill, polygon detection)?

### Potential Game Systems to Investigate

- Room detection system (how does the game know a room is enclosed?)
- Zone systems (how do zones track their boundaries?)
- Blueprint placement (does it know about enclosures?)
- Region/district systems

## Research Findings

### Good News: RimWorld Has Built-In Systems!

**1. Room/Region System** (see `interior-detection-research-rooms.md`)
- `room.ProperRoom` = true means it's a proper enclosed interior
- `room.Cells` gets all interior cells
- `room.BorderCells` gets the perimeter (walls)
- `room.ContainedAndAdjacentThings` gets obstacles inside
- Uses flood-fill internally to detect contiguous areas

**2. Blueprint Enclosure Calculator** (see `interior-detection-research-zones.md`)
- `AnimalPenBlueprintEnclosureCalculator` specifically handles detecting enclosures for BLUEPRINTS
- Uses FloodFill from interior point
- If flood-fill reaches map edge → NOT enclosed
- If flood-fill doesn't reach map edge → IS enclosed

**3. FloodFiller Algorithm**
- `map.floodFiller.FloodFill()` - core algorithm for connectivity
- Takes a starting cell and a "can pass" predicate
- Returns all connected cells that pass the predicate

### Recommended Implementation Approach

**For Rectangle Shapes:**
- Simple: use `CellRect.Cells` to get interior, check each for obstacles

**For Line-Based Shapes (walls forming enclosure):**
1. After placing walls, trigger `map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms()`
2. Check if a new `Room` was created with `ProperRoom == true`
3. Use `room.Cells` to get interior tiles
4. Use `room.ContainedAndAdjacentThings` to find obstacles

**For Blueprints (before walls are built):**
1. Reference `AnimalPenBlueprintEnclosureCalculator`
2. Use FloodFill from a candidate interior point
3. Check if flood-fill is bounded by blueprints
4. If bounded, those cells are "interior"

### Key Classes to Use

| Class | Purpose |
|-------|---------|
| `Room.ProperRoom` | Is this an enclosed room? |
| `Room.Cells` | All interior cells |
| `Room.BorderCells` | Perimeter/wall cells |
| `Room.ContainedAndAdjacentThings` | Things inside |
| `FloodFiller.FloodFill()` | Connectivity algorithm |
| `CellRect.EdgeCells` | Perimeter of rectangle |
| `IntVec3.OnEdge(map)` | Is cell on map boundary? |

## Design Decisions (User Feedback)

### When to Check for Enclosures
- Check when **viewing mode is entered** (after each segment placement)
- This is when we're already checking for obstacles, so lump this check in
- Stay silent until actually enclosed - no "3 walls placed, incomplete" announcements

### What Counts as Interior Obstacles
Report these as obstacles inside an enclosure:
- ✅ Mineable tiles (rocks, ore)
- ✅ Existing structures/buildings
- ✅ Trees
- ✅ Items on ground
- ❌ Other blueprints (user placed them intentionally)

### Existing Walls and Mountains
- YES, take existing walls into account for enclosure detection
- Building into mountains is common - must handle this case
- A wall + mountain + 2 new walls = valid enclosure
- Map edge itself cannot be built near, so ignore literal map edge

### Multiple Enclosures
- If placement creates 2 separate enclosures, report both
- "2 enclosures formed: Area 1 has 5 obstacles, Area 2 has 3 obstacles"

### Scanner Integration
- Add interior obstacles to scanner category when enclosure detected
- Remove them at same time as regular obstacles (exit architect mode, cancel blueprints)

### Zone Logic (Simpler)
- Zones: Check every tile that makes up the zone for obstacles
- No enclosure detection needed - the zone IS the area
- **Zone extensions**: Only check NEW tiles being added, not existing zone tiles
  - Existing tiles likely already have items/plants (that's why they're zoned)

### Blueprint Treatment
- Treat wall blueprints as walls for enclosure detection
- A blueprint wall = a wall waiting to happen
- Focus on walls first, other building types later if needed

## Implementation Phases

### Phase 1: Wall Enclosure Detection
1. When viewing mode entered with wall blueprints
2. Use FloodFill to detect if walls form enclosure(s)
3. Consider existing walls AND new blueprints as boundaries
4. Consider mountains/impassable terrain as boundaries
5. Find interior tiles
6. Check interior for obstacles (mineable, structures, trees, items)
7. Report to user and add to scanner

### Phase 2: Zone Obstacle Detection
1. When zone is created or extended
2. Check all tiles in the zone (or just new tiles for extensions)
3. Report obstacles found
4. Add to scanner

### Phase 3: Multi-Enclosure Support
1. Detect when placement creates multiple separate enclosures
2. Report each separately with obstacle counts

