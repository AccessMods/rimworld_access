# Building Module

Construction, zones, areas, and shape-based multi-cell placement.

## File Groups

**Architect Menu** - `ArchitectState.cs`, `ArchitectTreeState.cs`, `ArchitectHelper.cs`, `ArchitectMenuPatch.cs`, `ArchitectPlacementPatch.cs`
Category/tool selection with treeview navigation. ArchitectState tracks mode (Category/Tool/Material/Placement).

**Shape Placement** - `ShapePlacementState.cs`, `ShapeHelper.cs`, `ShapePreviewHelper.cs`, `ShapeSelectionMenuState.cs`
Two-point placement workflow (Line, Rectangle, Oval). ShapeHelper wraps RimWorld's DrawStyle classes.

**Zones** - `ZoneCreationState.cs`, `ZoneCreationPatch.cs`, `ZoneUndoTracker.cs`, `ZoneRenameState.cs`
Zone create/expand/shrink with undo. Uses ShapePreviewHelper for shape selection.

**Areas** - `AreaPatch.cs`, `AreaPaintingState.cs`, `WindowlessAreaState.cs`
Allowed areas and home zone management.

**Analysis** - `ObstacleDetector.cs`, `EnclosureDetector.cs`
Find obstacles blocking placement; detect rooms formed by blueprints.

**Post-Placement** - `ViewingModeState.cs`, `SelectionPreviewPatch.cs`
Review results, undo segments, navigate obstacles via ScannerState.

**Building Controls** - `BuildingComponentsHelper.cs`, `*ComponentState.cs`, `*ControlState.cs`
Toggle flickable/refuel/door/forbid settings on buildings.

## Key Entry Point

`DesignatorManagerPatch.Postfix` intercepts ALL designator selections and routes to ShapePlacementState.

## Designator Type Checking

Use ShapeHelper methods (not manual type checks):
- `IsBuildDesignator()` - Designator_Build
- `IsZoneDesignator()` - Zone hierarchy
- `IsCellsDesignator()` - Mine, etc.
- `IsDeleteDesignator()` - Zone shrink
- `IsOrderDesignator()` - Hunt, Haul, Tame

## Two-Point Selection Pattern

```csharp
var previewHelper = new ShapePreviewHelper();
previewHelper.SetCurrentShape(ShapeType.FilledRectangle);
previewHelper.SetFirstCorner(cell, "[Context]");
previewHelper.UpdatePreview(cursor);  // plays sound on count change
previewHelper.SetSecondCorner(cell, "[Context]");
var cells = previewHelper.PreviewCells;
```

## Zone Undo Pattern

```csharp
ZoneUndoTracker.CaptureBeforeState(zone, map, isShrink);
designator.DesignateMultiCell(cells);
ZoneUndoTracker.CaptureAfterState(map);
ZoneUndoTracker.AddSegment();
```
