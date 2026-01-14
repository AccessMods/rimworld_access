# Transport Pods System Analysis

This document provides a complete technical analysis of RimWorld's transport pod system to guide accessibility implementation.

## Design Principle: Sync With Game UI

**IMPORTANT:** Always sync accessibility features with the game's existing UI and notification systems rather than creating parallel systems. Benefits:

1. **Automatic updates** - When the game sends messages/alerts, our existing handlers catch them
2. **Consistency** - Users get the same information sighted players get
3. **Less maintenance** - Game updates don't break our features
4. **Less code** - Reuse existing game systems instead of duplicating logic

**Example:** The game already sends `Messages.Message("MessageFinishedLoadingTransporters")` when pod loading completes. Our existing message handling catches this automatically - no special transport pod code needed.

## Overview

Transport pods are a mid-to-late game transport system allowing players to launch pawns and cargo across the world map. The system consists of:

1. **Pod Launcher** (`Building_PodLauncher`) - The fuel tank that powers pod launches
2. **Transport Pod** (uses `CompTransporter` + `CompLaunchable_TransportPod`) - The actual pod that holds cargo
3. **Loading Dialog** (`Dialog_LoadTransporters`) - UI for assigning cargo to pods
4. **World Travel** (`TravellingTransporters`) - The world object representing pods in flight
5. **Arrival Actions** - Various outcomes when pods reach their destination

## Buildings and Components

### Pod Launcher (`Building_PodLauncher`)

The fuel source for transport pods. Key features:
- Requires chemfuel to operate
- Pods must be placed adjacent to launcher
- Has "auto-build pod" toggle to automatically queue new pods after launch
- Gizmos:
  - "Build Transport Pod" - Places blueprint at fueling port cell
  - "Auto-build Transport Pod" toggle

### Transport Pod

A building with two key components:

#### CompTransporter
Manages cargo storage and loading state:
- `innerContainer` - ThingOwner holding loaded items/pawns
- `leftToLoad` - List of TransferableOneWay items still being loaded
- `groupID` - Links multiple pods for group launching
- `LoadingInProgressOrReadyToLaunch` - True when pods are being loaded or ready
- `MassCapacity` / `MassUsage` - Weight limits

Key gizmos when loading:
- "Load transporter(s)" - Opens loading dialog
- "Cancel load" / "Unload" - Cancels loading process
- "Select previous/all/next transporter" - Navigation between grouped pods

#### CompLaunchable_TransportPod
Handles fuel connection and launch mechanics:
- `ConnectedToFuelingPort` - Whether adjacent to a fueled launcher
- `FuelLevel` - Current fuel from connected launcher
- `MaxLaunchDistanceAtFuelLevel()` - Calculates range based on fuel

Launch gizmo:
- "Launch" - Begins destination targeting on world map

## Loading Dialog (`Dialog_LoadTransporters`)

### UI Structure

```
+--------------------------------------------------+
|           Load Transport Pod(s)                   |
+--------------------------------------------------+
| Mass: 150/350 kg | Speed | Food | Visibility     |  <- Stats bar (optional)
+--------------------------------------------------+
|  [ Pawns ]  [ Items ]                            |  <- Tab buttons
+--------------------------------------------------+
|                                                  |
|  [TransferableOneWayWidget - scrollable list]    |  <- Main content
|  - Colonist 1                          [0] [+]   |
|  - Colonist 2                          [0] [+]   |
|  - Steel x500                         [0] [+50]  |
|                                                  |
+--------------------------------------------------+
| [Cancel]           [Reset]           [Accept]    |  <- Bottom buttons
+--------------------------------------------------+
```

### Tabs
- **Pawns tab** (`Tab.Pawns`): Lists all sendable pawns
- **Items tab** (`Tab.Items`): Lists all sendable items (non-pawn things)

### TransferableOneWayWidget
The same widget used for caravan formation:
- Displays items with quantity adjusters
- Shows mass, market value, nutrition info
- Supports sorting and sectioning

### Key Properties
- `MassCapacity` - Total mass capacity of all pods in group
- `MassUsage` - Current mass of assigned items
- `TilesPerDay` - Travel speed estimate (if showOverallStats enabled)
- `DaysWorthOfFood` - Food supply duration
- `Visibility` - Caravan visibility stat

### Dialog Flow
1. Opens via `Command_LoadToTransporter.ProcessInput()`
2. Calls `CalculateAndRecacheTransferables()` to build item lists
3. User adjusts quantities via widget
4. "Accept" calls `TryAccept()`:
   - Validates: non-empty, within mass, reachable items
   - May show confirmation dialog if over caravan mass capacity
   - Calls `TransporterUtility.InitiateLoading()` to start hauling jobs
5. Pawns with hauling jobs move items to pods
6. When loading complete, pods become launchable

### Error Messages
- `"CantSendEmptyTransportPods"` - No items assigned
- `"TooBigTransportersMassUsage"` - Over pod mass capacity
- `"PawnCantReachTransporters"` - Pawn can't path to pods
- `"TransporterItemIsUnreachable"` - Item can't be hauled to pods

## Launch Targeting

When "Launch" gizmo is clicked:

### World Targeting
1. Camera jumps to world map via `CameraJumper.TryJump()`
2. `Find.WorldTargeter.BeginTargeting()` starts targeting mode
3. Draws range rings:
   - Outer ring: Maximum range at full fuel
   - Inner ring (if different): Current fuel range
4. Mouse-over labels show destination options

### Destination Validation
- `ChoseWorldTarget()` validates selection
- Checks: valid tile, within range, enough fuel
- Shows rejection messages for invalid targets

### Destination Options (Float Menu)
When clicking a valid tile, options appear based on what's there:

#### Empty Passable Tile
- "Form caravan here" - Lands and creates a caravan

#### Player Colony
- "Land at [colony name]" - Returns to player base

#### Friendly Settlement
- "Visit [settlement]" - Form caravan at settlement
- "Trade with [settlement]" - Opens trade dialog
- "Offer gifts to [settlement]" - Gift items for goodwill

#### Hostile Settlement
- "Attack [settlement] and drop at edge"
- "Attack [settlement] and drop in center"

#### Site (quest location, etc.)
- "Visit [site]" - Enter the map

#### Existing Caravan
- "Give items to [caravan]" - Merge with caravan

#### Impassable Tile
- "Contents will be lost" - Warned, items destroyed

## World Travel (`TravellingTransporters`)

### World Object
- Appears on world map as moving icon
- Interpolates position between start and destination
- Travel speed: constant 0.00025 per tick (very fast)

### Key Properties
- `destinationTile` - Target tile
- `arrivalAction` - What happens on arrival
- `traveledPct` - 0.0 to 1.0 progress
- `transporters` - List of ActiveTransporterInfo (pod contents)

### Travel Time
Speed is constant regardless of fuel or distance - pods travel in straight line. The `TraveledPctStepPerTick` calculates based on spherical distance.

## Arrival Actions

All inherit from `TransportersArrivalAction`:

### TransportersArrivalAction_FormCaravan
- Creates a caravan from pod contents
- Default when landing on empty passable tile
- Extracts pawns, gives items to caravan inventory

### TransportersArrivalAction_LandInSpecificCell
- Drops pods at specific map location
- Used when returning to player colony
- Spawns DropPodIncoming skyfallers

### TransportersArrivalAction_AttackSettlement
- Generates enemy settlement map if needed
- Drops pods via EdgeDrop or CenterDrop mode
- Sends letter about attack

### TransportersArrivalAction_VisitSettlement
- Forms caravan at friendly settlement
- Extends FormCaravan

### TransportersArrivalAction_Trade
- Forms caravan and opens trade dialog
- Extends VisitSettlement

### TransportersArrivalAction_GiveGift
- Transfers items as gift
- Calculates goodwill gain

### TransportersArrivalAction_GiveToCaravan
- Merges contents with existing caravan

### TransportersArrivalAction_VisitSite
- Enters a quest site map

### TransportersArrivalAction_VisitSpace (Odyssey DLC)
- Space-related functionality

## Fuel System

### Chemfuel Consumption
- `Props.fuelPerTile` - Fuel cost per world tile traveled
- `Props.minFuelCost` - Minimum fuel for any launch
- Range = fuel / fuelPerTile

### Fuel Requirements
- Must have enough fuel for distance
- All pods in group need fuel (via connected launchers)
- Fuel consumed on launch

## Pod Grouping

Multiple pods can be grouped for simultaneous launch:
- `CompTransporter.groupID` links pods
- `TransportersInGroup()` finds all pods with same ID
- All pods launch together, share one destination
- Mass capacity is summed across group

## Key Accessibility Considerations

### Loading Dialog
1. Two tabs (Pawns, Items) with transferable lists
2. Quantity adjustment per item (same as caravan formation)
3. Mass capacity display and validation
4. Bottom buttons: Cancel, Reset, Accept

### Launch Targeting
1. World map targeting mode
2. Range rings (visual - need audio alternative)
3. Destination label on hover
4. Float menu for destination options

### Status Information
- Loading progress (items remaining)
- Ready to launch state
- Fuel level and range
- Mass usage vs capacity

### Gizmo Commands
Loading state:
- "Load" / "Set cargo" - Opens dialog
- "Cancel load" / "Unload"
- Select prev/all/next in group

Ready state:
- "Launch" - Begins targeting

### World Object
- Travelling pods on world map
- Arrival notifications

## Pod Placement

Transport pods must be placed on a **specific side** of the pod launcher, determined by the launcher's rotation. The valid placement cell is 90 degrees clockwise from where the launcher is facing:

| Launcher Facing | Pod Must Be Placed |
|-----------------|-------------------|
| North | East side |
| East | South side |
| South | West side |
| West | North side |

This is enforced by `PlaceWorker_NeedsFuelingPort` which checks `FuelingPortUtility.GetFuelingPortCell()`.

## Accessibility Implementation TODOs

### Map Navigation Announcements

**Pod Launcher (building or blueprint):**
- The launcher is a multi-tile building
- The fueling port cell is **adjacent to** the launcher, not part of it
- Use `FuelingPortUtility.GetFuelingPortCell(launcher)` to get the adjacent cell where pods connect
- When cursor is on launcher tiles: announce "Pod Launcher"
- When cursor is on an empty cell that is a fueling port cell: announce "Fueling port for [launcher]. Place pod here."
- Pod rotation doesn't matter - pod just needs to occupy the fueling port cell
- Fueling port cell location changes when launcher is rotated (R key)

**Transport Pod:**
- Announce connection status to fueling port
- Use `CompLaunchable_TransportPod.ConnectedToFuelingPort` property
- Example: "Transport Pod, connected to launcher" or "Transport Pod, not connected to fueling port"
- Helps verify correct placement without triggering error messages

### Loading Dialog
- Keyboard navigation for Pawns/Items tabs
- Quantity adjustment (reuse patterns from caravan formation)
- Mass capacity announcements
- Accept/Cancel/Reset button access

### Launch Targeting (World Map Destination Selection)

**Scanner Integration:**
- Extend world map scanner to announce fuel cost for each destination
- Format: "[Location name], [distance] tiles, [fuel cost] chemfuel" or "[Location name], not enough fuel"
- User navigates with scanner to find destination

**Destination Selection Flow:**
1. User presses Launch gizmo → enters world map targeting mode
2. Use scanner to browse destinations (fuel cost announced per tile)
3. Press Enter on desired tile to select
4. If only one action available → executes immediately
5. If multiple options (attack edge/center, trade, visit, etc.) → float menu appears
6. Navigate float menu and press Enter to confirm action

**Landing at Existing Maps (colonies, maps with pawns):**
- Float menu option: "Land in existing map"
- Selecting this switches to local map view
- `Find.Targeter.BeginTargeting()` starts local map targeting mode
- User must choose exact landing cell on the map

**Reuse Existing Placement Mode (ArchitectPlacementPatch.cs):**
- Transport pod landing uses `Find.Targeter.IsTargeting` (similar to `Find.DesignatorManager`)
- Extend existing placement handling to also check for `Find.Targeter.IsTargeting`
- Same pattern: arrow keys move `MapNavigationState.CurrentCursorPosition`
- Space/Enter confirms target cell
- Escape cancels targeting
- No rotation needed for pod landing

**Fuel Information:**
- Announce current fuel level and max range when targeting mode opens
- Per-tile: announce if reachable and fuel cost
- Note: Pods are always one-way (destroyed on launch). Pawns form a caravan and walk back.

### World Travel
- Announce when pods are in transit
- Arrival notifications with destination info

## Shuttles (Royalty DLC)

Shuttles share ~90% of the same code and UI as transport pods:

**Shared (accessibility work applies to both):**
- `Dialog_LoadTransporters` - Same loading dialog
- `CompTransporter` - Same cargo/loading component
- `CompLaunchable` - Same launch mechanics
- World map targeting - Same destination selection
- Arrival actions - Same options (form caravan, attack, etc.)

**Shuttle-specific differences:**
- Additional `CompShuttle` component
- Reusable (not destroyed on launch)
- May have `requiredItems` and `requiredPawns` for quests
- Has `autoload` toggle gizmo
- Single-unit only (no grouping like transport pods)
- Different message: "MessageFinishedLoadingShuttle"

**Conclusion:** Transport pod accessibility work covers shuttles automatically. Minor shuttle-specific features (required items display, autoload) are just gizmos.

## Code Reference (Decompiled Sources)

All paths relative to `decompiled/` folder.

### Loading Dialog

**File:** `RimWorld/Dialog_LoadTransporters.cs`

**Public methods to call:**
- `MassCapacity` (property) - Total mass capacity of all pods
- `MassUsage` (property) - Current assigned mass
- `TilesPerDay` (property) - Travel speed stat
- `DaysWorthOfFood` (property) - Food duration stat
- `Visibility` (property) - Caravan visibility stat

**Private fields (REFLECTION REQUIRED):**
- `Tab tab` - Current selected tab (enum: `Pawns`, `Items`)
- `List<TransferableOneWay> transferables` - All transferable items
- `TransferableOneWayWidget pawnsTransfer` - Pawns tab widget
- `TransferableOneWayWidget itemsTransfer` - Items tab widget
- `List<CompTransporter> transporters` - Pod group being loaded

**Key methods:**
- `CalculateAndRecacheTransferables()` - Builds item lists (called on open)
- `TryAccept()` - Validates and starts loading
- `DoWindowContents(Rect)` - Main UI draw method (patch for input)
- `PostOpen()` - Called when dialog opens (hook for state init)
- `PostClose()` - Called when dialog closes (hook for cleanup)

### Fueling Port Utility

**File:** `RimWorld/FuelingPortUtility.cs`

**Public static methods (no reflection needed):**
- `GetFuelingPortCell(Building podLauncher)` → `IntVec3` - Returns adjacent cell for pod placement
- `FuelingPortGiverAtFuelingPortCell(IntVec3 c, Map map)` → `Building` - Finds launcher for a fueling port cell
- `LaunchableAt(IntVec3 c, Map map)` → `CompLaunchable` - Gets launchable comp at cell

### Transport Pod Components

**File:** `RimWorld/CompTransporter.cs`

**Public properties/methods:**
- `LoadingInProgressOrReadyToLaunch` (property) - True when loading/ready
- `AnyInGroupHasAnythingLeftToLoad` (property) - True if still loading
- `MassCapacity` (property) - Pod mass limit
- `MassUsage` (property) - Current loaded mass
- `innerContainer` (field) - ThingOwner with loaded contents
- `TransportersInGroup(Map)` → `List<CompTransporter>` - All pods in group

**Private fields (REFLECTION REQUIRED):**
- `int groupID` - Links pods together
- `List<TransferableOneWay> leftToLoad` - Items still being hauled

**File:** `RimWorld/CompLaunchable.cs` (base class)

**Public properties/methods:**
- `FuelInLeastFueledFuelingPortSource` (property) - Current fuel level
- `MaxLaunchDistance` (property) - Max range at full fuel
- `MaxLaunchDistanceAtFuelLevel(float fuel)` - Range at given fuel
- `ChoseWorldTarget(GlobalTargetInfo target)` - Handles destination selection
- `StartChoosingDestination()` - Begins world targeting mode

**Private fields (REFLECTION REQUIRED):**
- `CompTransporter cachedCompTransporter` - Linked transporter

**File:** `RimWorld/CompLaunchable_TransportPod.cs` (subclass)

**Public properties:**
- `ConnectedToFuelingPort` (property) - True if adjacent to fueled launcher
- `FuelLevel` (property) - Fuel from connected launcher

### World Map Targeting

**File:** `RimWorld/CompLaunchable.cs` (method `StartChoosingDestination`)

**Key calls to intercept/sync:**
```csharp
CameraJumper.TryJump(CameraJumper.GetWorldTarget(...));
Find.WorldSelector.ClearSelection();
Find.WorldTargeter.BeginTargeting(
    ChoseWorldTarget,           // Action<GlobalTargetInfo>
    canTargetTiles: true,
    TargeterMouseAttachment,    // Texture
    closeWorldTabWhenFinished: false,
    onUpdate: OnUpdate,         // Action draws range rings
    extraLabelGetter: TargetingLabelGetter  // Func<GlobalTargetInfo, string>
);
```

**For fuel cost calculation:**
- `FuelNeededToLaunchAtDist(float dist)` - Private method, REFLECTION REQUIRED
- Or calculate: `dist * Props.fuelPerTile` (Props is `CompProperties_Launchable`)

### Float Menu Options

**File:** `RimWorld.Planet/MapParent.cs` (lines 248-268)

**Method:** `GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)`

Returns float menu options for landing at existing maps. Key code:
```csharp
yield return new FloatMenuOption("LandInExistingMap".Translate(Label), delegate
{
    Map map = Map;
    Current.Game.CurrentMap = map;
    CameraJumper.TryHideWorld();
    Find.Targeter.BeginTargeting(TargetingParameters.ForDropPodsDestination(), delegate(LocalTargetInfo x)
    {
        launchAction(base.Tile, new TransportersArrivalAction_LandInSpecificCell(this, x.Cell, ...));
    }, ...);
});
```

**File:** `RimWorld.Planet/Settlement.cs`

**Method:** `GetTransportersFloatMenuOptions(...)` - Adds trade, attack, visit options

### Arrival Actions

**File:** `RimWorld.Planet/TransportersArrivalAction.cs` (base class)

**Subclasses (all in `RimWorld.Planet/`):**
- `TransportersArrivalAction_FormCaravan.cs` - Land and form caravan
- `TransportersArrivalAction_LandInSpecificCell.cs` - Land at specific map cell
- `TransportersArrivalAction_AttackSettlement.cs` - Attack hostile settlement
- `TransportersArrivalAction_VisitSettlement.cs` - Visit friendly settlement
- `TransportersArrivalAction_Trade.cs` - Trade with settlement
- `TransportersArrivalAction_GiveGift.cs` - Gift items
- `TransportersArrivalAction_GiveToCaravan.cs` - Merge with caravan
- `TransportersArrivalAction_VisitSite.cs` - Enter quest site

### World Travel

**File:** `RimWorld.Planet/TravellingTransporters.cs`

**Public properties:**
- `destinationTile` (field) - Target world tile
- `Tile` (property, inherited) - Current tile
- `DrawPos` (property) - Interpolated position

**Private fields (REFLECTION REQUIRED):**
- `float traveledPct` - 0.0 to 1.0 progress
- `TransportersArrivalAction arrivalAction` - What happens on arrival

### Shuttle Detection

**File:** `RimWorld/DropPodUtility.cs` (lines 90-113)

**Public extension methods:**
- `IsShuttle(this List<CompTransporter>)` → `bool` - Returns true if single pod with CompShuttle
- `AsShuttle(this List<CompTransporter>)` → `CompShuttle` - Gets CompShuttle if present

**File:** `RimWorld/CompShuttle.cs`

**Public properties:**
- `requiredItems` (field) - Quest-required items list
- `requiredPawns` (field) - Quest-required pawns list
- `Autoload` (property) - Whether autoload is enabled

### Message Keys (for syncing with game notifications)

```csharp
"MessageFinishedLoadingTransporters"  // Pod loading complete
"MessageFinishedLoadingShuttle"       // Shuttle loading complete
"MessageTransportPodsArrived"         // Pods arrived at destination
"MessageShuttleArrived"               // Shuttle arrived
"CantSendEmptyTransportPods"          // Error: no cargo
"TooBigTransportersMassUsage"         // Error: over capacity
```

### Local Map Targeting

**Key system:** `Find.Targeter`

**Properties to check:**
- `Find.Targeter.IsTargeting` - True when in targeting mode

**To confirm target:**
- Invoke the action callback passed to `BeginTargeting()`
- Or simulate: `Find.Targeter.StopTargeting()` after setting target

**Targeting parameters:** `TargetingParameters.ForDropPodsDestination()` - Defined in `RimWorld/TargetingParameters.cs`

## Similar Patterns (Existing Accessible Code)

The loading dialog uses `TransferableOneWayWidget`, same as:
- `Dialog_FormCaravan` - See `src/World/CaravanFormationState.cs`
- `Dialog_SplitCaravan` - See `src/World/SplitCaravanState.cs`

Reuse patterns from these files for quantity adjustment, tab navigation, and item announcements.
