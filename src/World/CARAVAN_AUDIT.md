# Caravan System UI Synchronization Audit

**Date:** January 2026
**Branch:** caravans
**Purpose:** Ensure caravan screens properly sync with game's visual UI and read from correct data sources.

## Executive Summary

This audit examined all caravan-related files in the RimWorld Access mod to verify:
1. Tab switching syncs with the game's visual tabs
2. Quantity changes trigger proper UI refreshes
3. Data is read from the same sources as the game's UI
4. Reflection is only used where necessary (no public API exists)

### Critical Issues Found: 4
### Minor Issues Found: 2
### Files Passing: 8

---

## Critical Issues (Must Fix)

### Issue 1: Tab Switching Not Synced with Game UI

**Files:** `CaravanFormationState.cs`, `SplitCaravanState.cs`
**Severity:** CRITICAL
**Confidence:** 100%

**Problem:** When user presses Left/Right to switch tabs, the mod updates its internal `currentTab` enum but does NOT update the game's `Dialog_FormCaravan.tab` or `Dialog_SplitCaravan.tab` field. The visual game UI stays on whatever tab it was on.

**Game Source Analysis:**
- `Dialog_FormCaravan.tab` - **private field** (line 44 in decompiled source)
- `Dialog_SplitCaravan.tab` - **private field** (line 28 in decompiled source)
- Both use private `Tab` enum: `Pawns=0, Items=1, TravelSupplies/FoodAndMedicine=2`
- Tab switching is just a field assignment with no side effects

**Fix Required:**
```csharp
// Add to CaravanFormationState.cs
private static void SyncGameTab()
{
    if (currentDialog == null) return;

    int gameTabValue = currentTab switch
    {
        Tab.Pawns => 0,
        Tab.Items => 1,
        Tab.TravelSupplies => 2,
        _ => 0
    };

    FieldInfo tabField = AccessTools.Field(typeof(Dialog_FormCaravan), "tab");
    tabField?.SetValue(currentDialog, gameTabValue);
}

// Call SyncGameTab() at end of NextTab() and PreviousTab()
```

Similar fix needed for `SplitCaravanState.cs` targeting `Dialog_SplitCaravan`.

---

### Issue 2: Cached MaxQuantity in QuantityMenuState

**File:** `src/UI/QuantityMenuState.cs`
**Line:** 61
**Severity:** CRITICAL
**Confidence:** 95%

**Problem:** `maxQuantity` is cached when menu opens and never refreshed. If items rot, despawn, or quantities change while menu is open, the cached value becomes stale.

**Current Code:**
```csharp
maxQuantity = transferable.MaxCount;
```

**Game Source:** `TransferableOneWay.MaxCount` dynamically calculates from `things` list every time (lines 71-82).

**Fix Required:**
```csharp
// Replace static field with dynamic property
private static int MaxQuantity => currentTransferable?.MaxCount ?? 0;

// Update all references to maxQuantity to use MaxQuantity instead
```

---

### Issue 3: Tab Filtering Logic Mismatch

**File:** `src/World/CaravanUIHelper.cs`
**Line:** 51
**Severity:** CRITICAL
**Confidence:** 95%

**Problem:** Extra null check not present in game code causes potential item miscategorization.

**Current Code:**
```csharp
(t.AnyThing.GetInnerIfMinified().def.IsBed &&
 t.AnyThing.GetInnerIfMinified().def.building != null &&  // <-- EXTRA CHECK
 t.AnyThing.GetInnerIfMinified().def.building.bed_caravansCanUse)
```

**Game Code (CaravanUIUtility.cs line 117):**
```csharp
(t.AnyThing.GetInnerIfMinified().def.IsBed &&
 t.AnyThing.GetInnerIfMinified().def.building.bed_caravansCanUse)
```

**Fix Required:** Remove the extra null check to match game behavior exactly:
```csharp
(t.AnyThing.GetInnerIfMinified().def.IsBed &&
 t.AnyThing.GetInnerIfMinified().def.building.bed_caravansCanUse)
```

---

### Issue 4: Mass Calculation Uses BaseMass Instead of Actual Mass

**File:** `src/World/CaravanQuantityHelper.cs`
**Line:** 51
**Severity:** CRITICAL
**Confidence:** 88%

**Problem:** Uses `ThingDef.BaseMass` instead of actual mass with quality/condition modifiers. Creates inconsistency with `CaravanAnnouncementHelper` which uses `GetStatValue(StatDefOf.Mass)`.

**Current Code:**
```csharp
float itemMass = transferable.ThingDef?.BaseMass ?? 0f;
```

**Fix Required:**
```csharp
float itemMass = transferable.AnyThing?.GetStatValue(StatDefOf.Mass)
    ?? transferable.ThingDef?.BaseMass ?? 0f;
```

---

## Minor Issues (Should Fix)

### Issue 5: Missing SplitOff(1) for Apparel

**File:** `src/World/GearEquipMenuState.cs`
**Line:** 540
**Severity:** MINOR
**Confidence:** 85%

**Problem:** Doesn't use `SplitOff(1)` pattern that official game UI uses for apparel.

**Current Code:**
```csharp
targetPawn.apparel.Wear(apparel, dropReplacedApparel: false);
```

**Game Code (WITab_Caravan_Gear.cs line 477):**
```csharp
p.apparel.Wear((Apparel)apparel.SplitOff(1), dropReplacedApparel: false);
```

**Fix Required:**
```csharp
Apparel singleApparel = (Apparel)apparel.SplitOff(1);
targetPawn.apparel.Wear(singleApparel, dropReplacedApparel: false);
```

---

### Issue 6: Locked Apparel Check Missing

**File:** `src/World/GearEquipMenuState.cs`
**Line:** 484-499
**Severity:** MINOR
**Confidence:** 75%

**Problem:** Removes apparel from source without checking if it's locked.

**Fix Required:** Add check before removing apparel:
```csharp
if (sourceOwner.apparel?.IsLocked(apparel) == true)
{
    TolkHelper.Speak("Cannot remove locked apparel");
    return;
}
```

---

## Files Passing Audit (No Issues)

### CaravanInspectState.cs
- **Status:** PASS
- **Notes:** Read-only inspection, no reflection, reads from correct game sources
- **Data sources verified:** `PawnsListForReading`, `CaravanInventoryUtility.AllInventoryItems()`, `MassUsage`, `DaysWorthOfFood`

### CaravanAnnouncementHelper.cs
- **Status:** PASS
- **Notes:** Pure formatting helper, reads fresh data from game objects

### CaravanInputHelper.cs
- **Status:** PASS
- **Notes:** Correctly handles input routing without caching

### StatBreakdownState.cs
- **Status:** PASS
- **Notes:** Read-only parser for explanation text, no sync issues

### SplitCaravanPatch.cs
- **Status:** PASS
- **Notes:** Correctly triggers SplitCaravanState, proper lifecycle management

### CaravanFormationPatch.cs
- **Status:** PASS
- **Notes:** Window.OnCancelKeyPressed patch correctly blocks double-close

### GearEquipMenuState.cs (mostly)
- **Status:** PASS with minor issues
- **Notes:** Uses official RimWorld APIs, triggers proper refresh via `CaravanInspectState.RefreshTree()`

### SplitCaravanState.cs (mostly)
- **Status:** PASS except tab sync
- **Notes:** Correctly reads from properties to trigger lazy recalculation, properly calls `CountToTransferChanged()`

---

## Reflection Usage Summary

### Justified (No Public API Available)

| File | Target | Member | Reason |
|------|--------|--------|--------|
| CaravanFormationState | Dialog_FormCaravan | `tab` field | Private, needed for tab sync |
| CaravanFormationState | Dialog_FormCaravan | `Notify_TransferablesChanged()` | Private method, needed for UI refresh |
| CaravanFormationState | Dialog_FormCaravan | `TilesPerDay`, `DaysWorthOfFood`, `Visibility` | Private properties for stats |
| CaravanFormationState | Dialog_FormCaravan | `destinationTile`, `autoSelectTravelSupplies` | Private fields for route planning |
| SplitCaravanState | Dialog_SplitCaravan | `tab` field | Private, needed for tab sync |
| SplitCaravanState | Dialog_SplitCaravan | `transferables` field | Private list |
| SplitCaravanState | Dialog_SplitCaravan | `caravan` field | Private, no public getter |
| SplitCaravanState | Dialog_SplitCaravan | `CountToTransferChanged()` | Private method, needed for UI refresh |
| SplitCaravanState | Dialog_SplitCaravan | All stat properties | Private, lazy calculation |

### Not Needed (Public API Exists)

| File | Current | Should Use |
|------|---------|------------|
| CaravanFormationState | `currentDialog.transferables` | Already correct - field is public |
| All files | `transferable.AdjustTo()` | Already correct - method is public |
| All files | `transferable.CountToTransfer` | Already correct - getter is public |

---

## Data Source Verification

### Correct Sources Used

| Data | Source | Matches Game UI |
|------|--------|-----------------|
| Transferables list | `Dialog_FormCaravan.transferables` | Yes - same list |
| Pawn list | `Caravan.PawnsListForReading` | Yes - same list |
| Inventory items | `CaravanInventoryUtility.AllInventoryItems()` | Yes - same method |
| Mass usage | `currentDialog.MassUsage` property | Yes - same property |
| Days of food | `currentCaravan.DaysWorthOfFood` | Yes - same property |
| Equipment | `pawn.equipment.Primary` | Yes - same property |
| Apparel | `pawn.apparel.WornApparel` | Yes - same list |

---

## Implementation Plan

### Phase 1: Tab Synchronization
1. Add `SyncGameTab()` method to `CaravanFormationState.cs`
2. Add `SyncGameTab()` method to `SplitCaravanState.cs`
3. Call after each tab switch in `NextTab()` and `PreviousTab()`
4. Test: Visual tabs should change when pressing Left/Right

### Phase 2: Fix Stale Data Issues
1. Replace `maxQuantity` field with `MaxQuantity` property in `QuantityMenuState.cs`
2. Update all references to use dynamic property
3. Test: Quantity limits should update if items change

### Phase 3: Fix Filtering and Mass Calculation
1. Remove extra null check in `CaravanUIHelper.cs` line 51
2. Change `BaseMass` to `GetStatValue(StatDefOf.Mass)` in `CaravanQuantityHelper.cs`
3. Test: Items should appear in correct tabs, mass calculations should match

### Phase 4: Minor Fixes
1. Add `SplitOff(1)` pattern in `GearEquipMenuState.cs`
2. Add locked apparel check
3. Test: Gear equipping should work with stacked items

---

## Testing Checklist

After implementing fixes:

- [ ] Tab switching: Visual tabs change when pressing Left/Right in caravan formation
- [ ] Tab switching: Visual tabs change when pressing Left/Right in split caravan
- [ ] Quantity menu: Max quantity updates if items despawn during menu
- [ ] Tab filtering: Beds appear in TravelSupplies tab correctly
- [ ] Mass calculation: "Add max" respects actual mass with quality modifiers
- [ ] Gear equip: Stacked apparel handles correctly
- [ ] Gear equip: Locked apparel cannot be removed
- [ ] All existing functionality still works

---

## Appendix: Game Source File Locations

For future reference, key decompiled game files:

```
decompiled/RimWorld/Dialog_FormCaravan.cs
decompiled/RimWorld.Planet/Dialog_SplitCaravan.cs
decompiled/RimWorld/Transferable.cs
decompiled/RimWorld/TransferableOneWay.cs
decompiled/RimWorld/TransferableOneWayWidget.cs
decompiled/RimWorld/TransferableUIUtility.cs
decompiled/RimWorld/CaravanUIUtility.cs
decompiled/RimWorld/WITab_Caravan_Gear.cs
decompiled/RimWorld/CaravanInventoryUtility.cs
```
