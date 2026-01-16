using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// State for viewing mode after shape-based placement.
    /// Works for all designator types: Buildings (blueprints), Orders (Hunt, Haul), Zones, and Cells (Mine).
    /// Allows reviewing obstacles and making adjustments.
    /// Supports multi-segment placement with = (add more) and - (remove last).
    ///
    /// Obstacle navigation is delegated to ScannerState via a temporary "Obstacles" category.
    /// This category is a proper scanner category that:
    /// - Can be cycled to using Ctrl+PageUp/Down like other categories
    /// - Supports Page Up/Down to navigate between obstacles
    /// - Uses Home to jump to the current obstacle
    /// - Is automatically removed when exiting viewing mode
    /// </summary>
    public static class ViewingModeState
    {
        private static bool isActive = false;

        // Track segments separately so we can remove just the last one
        // For Build designators: List of Things (blueprints)
        // For Orders/Zones/Cells: List is empty, we use cellSegments instead
        private static List<List<Thing>> segments = new List<List<Thing>>();

        // Track cells per segment for non-Build designators (Orders, Zones, Cells)
        // These use DesignationManager to undo, not Thing.Destroy()
        private static List<List<IntVec3>> cellSegments = new List<List<IntVec3>>();

        // Track obstacle cells for segment logic (knowing which cells failed)
        // Navigation is handled by ScannerState's temporary category
        private static List<IntVec3> obstacleCells = new List<IntVec3>();

        // Track order targets (things that were designated) - for Hunt, Haul, Tame, etc.
        // These are Things (animals, items) that the order designator targeted
        private static List<Thing> orderTargets = new List<Thing>();

        // Track order target cells (cells that were designated) - for Mine, Cancel, etc.
        // These are cells where cell-based orders were placed
        private static List<IntVec3> orderTargetCells = new List<IntVec3>();

        // Store the designator used for placement (for adding new blueprints)
        private static Designator activeDesignator = null;

        // Store the shape type used for placement (for restoring after undo)
        private static ShapeType usedShapeType = ShapeType.Manual;

        // Store scanner focus position for restore
        private static IntVec3 savedCursorPosition = IntVec3.Invalid;

        // Track if we're temporarily out adding more shapes (don't clear segments on re-entry)
        private static bool isAddingMore = false;

        // Track whether this is a Build designator (for undo behavior)
        private static bool isBuildDesignator = false;

        // Track whether this is an Order designator (Hunt, Mine, Cancel, etc.)
        private static bool isOrderDesignator = false;

        #region Properties

        /// <summary>
        /// Whether viewing mode is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// List of all blueprints placed across all segments.
        /// Only populated for Build designators.
        /// </summary>
        public static List<Thing> PlacedBlueprints
        {
            get
            {
                var all = new List<Thing>();
                foreach (var segment in segments)
                {
                    all.AddRange(segment);
                }
                return all;
            }
        }

        /// <summary>
        /// List of all cells designated across all segments.
        /// For non-Build designators (Orders, Zones, Cells).
        /// </summary>
        public static List<IntVec3> PlacedCells
        {
            get
            {
                var all = new List<IntVec3>();
                foreach (var segment in cellSegments)
                {
                    all.AddRange(segment);
                }
                return all;
            }
        }

        /// <summary>
        /// Total count of placed items (blueprints for Build, cells for others).
        /// </summary>
        public static int PlacedCount
        {
            get
            {
                if (isBuildDesignator)
                    return PlacedBlueprints.Count;
                return PlacedCells.Count;
            }
        }

        /// <summary>
        /// Number of segments placed.
        /// </summary>
        public static int SegmentCount => isBuildDesignator ? segments.Count : cellSegments.Count;

        /// <summary>
        /// List of cells where placement failed due to obstacles.
        /// </summary>
        public static List<IntVec3> ObstacleCells => obstacleCells;

        /// <summary>
        /// List of Things that were designated by order operations (Hunt, Haul, etc.).
        /// </summary>
        public static List<Thing> OrderTargets => orderTargets;

        /// <summary>
        /// List of cells that were designated by cell-based order operations (Mine, Cancel, etc.).
        /// </summary>
        public static List<IntVec3> OrderTargetCells => orderTargetCells;

        /// <summary>
        /// Whether the current designator is an Order type (Hunt, Mine, Cancel, etc.).
        /// </summary>
        public static bool IsOrderDesignator => isOrderDesignator;

        #endregion

        #region State Management

        /// <summary>
        /// Enters viewing mode with the results from shape placement.
        /// Can be called multiple times to add more segments.
        /// Works for all designator types: Build (blueprints), Orders, Zones, and Cells.
        /// </summary>
        /// <param name="result">The placement result from ShapePlacementState</param>
        /// <param name="designator">The designator used for placement (for adding new items)</param>
        /// <param name="shapeType">The shape type used for placement (for restoring after undo)</param>
        public static void Enter(PlacementResult result, Designator designator = null, ShapeType shapeType = ShapeType.Manual)
        {
            if (result == null)
            {
                Log.Warning("[ViewingModeState] Enter called with null result");
                return;
            }

            // First time entering - save cursor and initialize
            // But if we're adding more shapes, keep existing segments
            if (!isActive && !isAddingMore)
            {
                SaveFocus();
                ScannerState.SaveFocus();
                segments.Clear();
                cellSegments.Clear();
                obstacleCells.Clear();
                orderTargets.Clear();
                orderTargetCells.Clear();
                isBuildDesignator = ShapeHelper.IsBuildDesignator(designator);
                isOrderDesignator = ShapeHelper.IsOrderDesignator(designator);
            }
            isAddingMore = false; // Reset the flag

            // Add this placement as a new segment
            // For Build designators, track Things (blueprints)
            // For others, track cells
            if (isBuildDesignator)
            {
                var newSegment = new List<Thing>(result.PlacedBlueprints ?? new List<Thing>());
                segments.Add(newSegment);
            }
            else
            {
                var newCellSegment = new List<IntVec3>(result.PlacedCells ?? new List<IntVec3>());
                cellSegments.Add(newCellSegment);

                // For order designators, collect the targets (things/cells that were designated)
                if (isOrderDesignator && result.PlacedCells != null)
                {
                    CollectOrderTargets(result.PlacedCells, designator);
                }
            }

            // Add any new obstacles
            if (result.ObstacleCells != null)
            {
                foreach (var cell in result.ObstacleCells)
                {
                    if (!obstacleCells.Contains(cell))
                        obstacleCells.Add(cell);
                }
            }

            activeDesignator = designator;
            usedShapeType = shapeType;
            isActive = true;

            // Create temporary scanner category for obstacles or targets
            if (isOrderDesignator)
            {
                // For orders, create targets category instead of obstacles
                UpdateTargetsCategory();
            }
            else
            {
                // For builds, create obstacles category if any
                UpdateObstacleCategory();
            }

            // Build the announcement
            string announcement = BuildEntryAnnouncement(designator);
            TolkHelper.Speak(announcement, SpeechPriority.Normal);

            int totalPlaced = PlacedCount;
            int segCount = SegmentCount;
            string itemType = isBuildDesignator ? "blueprints" : "designations";
            Log.Message($"[ViewingModeState] Entered with {result.PlacedCount} new {itemType} (total: {totalPlaced}), {obstacleCells.Count} obstacles, {orderTargets.Count} order targets");
        }

        /// <summary>
        /// Collects the things/cells that were designated by an order designator.
        /// This queries the map to find what was actually designated at each cell.
        /// </summary>
        private static void CollectOrderTargets(List<IntVec3> placedCells, Designator designator)
        {
            Map map = Find.CurrentMap;
            if (map == null || designator == null)
                return;

            foreach (IntVec3 cell in placedCells)
            {
                // Check for thing-based designations (Hunt, Haul, Tame, etc.)
                List<Thing> things = cell.GetThingList(map);
                bool foundThingTarget = false;

                foreach (Thing thing in things)
                {
                    // Check if this thing has any designation on it
                    if (map.designationManager.DesignationOn(thing) != null)
                    {
                        if (!orderTargets.Contains(thing))
                        {
                            orderTargets.Add(thing);
                            foundThingTarget = true;
                        }
                    }
                }

                // If no thing target was found, this might be a cell-based designation (Mine, Cancel, etc.)
                if (!foundThingTarget)
                {
                    // Check if there's any designation at this cell
                    if (map.designationManager.AllDesignationsAt(cell).Any())
                    {
                        if (!orderTargetCells.Contains(cell))
                        {
                            orderTargetCells.Add(cell);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the announcement for entering viewing mode.
        /// For orders, includes a summary of what was designated.
        /// For builds, includes blueprint count and obstacles.
        /// </summary>
        private static string BuildEntryAnnouncement(Designator designator)
        {
            int totalPlaced = PlacedCount;
            int segCount = SegmentCount;
            string segmentInfo = segCount > 1 ? $" ({segCount} segments)" : "";

            if (isOrderDesignator)
            {
                // Build order-specific announcement with target summary
                string summary = BuildOrderTargetSummary(designator);
                int targetCount = orderTargets.Count + orderTargetCells.Count;

                if (!string.IsNullOrEmpty(summary))
                {
                    string navHint = targetCount > 0 ? " Use Page Up and Down to navigate targets." : "";
                    return $"Viewing mode. {summary}{segmentInfo}.{navHint}";
                }
                else
                {
                    return $"Viewing mode. {totalPlaced} designations{segmentInfo}.";
                }
            }
            else
            {
                // Standard announcement for builds
                string itemType = isBuildDesignator ? "blueprints" : "designations";

                if (obstacleCells.Count > 0)
                {
                    return $"Viewing mode. {totalPlaced} {itemType}{segmentInfo}. {obstacleCells.Count} obstacles. Use Page Up and Down to navigate obstacles.";
                }
                else
                {
                    return $"Viewing mode. {totalPlaced} {itemType}{segmentInfo}.";
                }
            }
        }

        /// <summary>
        /// Builds a summary of order targets grouped by type.
        /// Examples: "Now hunting: 1 tiger, 2 lions" or "Mining: 12 compacted steel, 5 jade"
        /// </summary>
        private static string BuildOrderTargetSummary(Designator designator)
        {
            string designatorLabel = designator?.Label ?? "Order";
            Map map = Find.CurrentMap;

            // Count things by kind/def
            var thingCounts = new Dictionary<string, int>();

            foreach (Thing thing in orderTargets)
            {
                string label = thing.LabelNoCount ?? thing.def?.label ?? "Unknown";
                if (thingCounts.ContainsKey(label))
                    thingCounts[label]++;
                else
                    thingCounts[label] = 1;
            }

            // Count cell-based targets by what's there (mineable, etc.)
            if (map != null)
            {
                foreach (IntVec3 cell in orderTargetCells)
                {
                    // Get what's at this cell
                    Thing edifice = cell.GetEdifice(map);
                    string label;

                    if (edifice != null)
                    {
                        label = edifice.LabelNoCount ?? edifice.def?.label ?? "Unknown";
                    }
                    else
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        label = terrain?.label ?? "tile";
                    }

                    if (thingCounts.ContainsKey(label))
                        thingCounts[label]++;
                    else
                        thingCounts[label] = 1;
                }
            }

            if (thingCounts.Count == 0)
                return string.Empty;

            int totalCount = thingCounts.Values.Sum();
            int threshold = totalCount / 100; // 1% threshold
            if (threshold < 1)
                threshold = 1; // minimum 1

            var significantParts = new List<string>();
            int smallGroupCount = 0;
            int smallGroupItems = 0;

            foreach (var kvp in thingCounts.OrderByDescending(x => x.Value))
            {
                if (kvp.Value >= threshold)
                {
                    string label = kvp.Value > 1
                        ? Find.ActiveLanguageWorker.Pluralize(kvp.Key, kvp.Value)
                        : kvp.Key;
                    significantParts.Add($"{kvp.Value} {label}");
                }
                else
                {
                    smallGroupCount++;
                    smallGroupItems += kvp.Value;
                }
            }

            string result = $"{designatorLabel}: {string.Join(", ", significantParts)}";

            if (smallGroupCount > 0)
            {
                string itemWord = smallGroupItems == 1 ? "item" : "items";
                string groupWord = smallGroupCount == 1 ? "group" : "groups";
                result += $", and {smallGroupItems} more {itemWord} in {smallGroupCount} smaller {groupWord}";
            }

            return result;
        }

        /// <summary>
        /// Creates or updates the temporary scanner category with current order targets.
        /// Similar to UpdateObstacleCategory but for order targets.
        /// </summary>
        private static void UpdateTargetsCategory()
        {
            int targetCount = orderTargets.Count + orderTargetCells.Count;
            if (targetCount == 0)
            {
                ScannerState.RemoveTemporaryCategory();
                return;
            }

            // Get category name based on designator
            string categoryName = GetTargetsCategoryName();

            // Create scanner items for each target
            var cursorPos = MapNavigationState.CurrentCursorPosition;
            var targetItems = new List<ScannerItem>();
            Map map = Find.CurrentMap;

            // Add thing-based targets
            foreach (Thing thing in orderTargets)
            {
                string label = thing.LabelShort ?? thing.def?.label ?? "Target";
                var item = new ScannerItem(thing, cursorPos);
                targetItems.Add(item);
            }

            // Add cell-based targets
            if (map != null)
            {
                foreach (IntVec3 cell in orderTargetCells)
                {
                    Thing edifice = cell.GetEdifice(map);
                    string label;

                    if (edifice != null)
                    {
                        // Use the edifice as a scanner item
                        var item = new ScannerItem(edifice, cursorPos);
                        targetItems.Add(item);
                    }
                    else
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        label = terrain?.label ?? "Target";
                        var item = new ScannerItem(cell, label, cursorPos);
                        targetItems.Add(item);
                    }
                }
            }

            // Sort by distance
            targetItems.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Create temporary category in ScannerState
            ScannerState.CreateTemporaryCategory(categoryName, targetItems);
        }

        /// <summary>
        /// Gets the category name for the targets scanner based on the active designator.
        /// </summary>
        private static string GetTargetsCategoryName()
        {
            if (activeDesignator == null)
                return "Targets";

            string label = activeDesignator.Label ?? "Order";

            // Common designator types get specific names
            string defName = activeDesignator.GetType().Name;

            if (defName.Contains("Hunt"))
                return "Hunt Targets";
            if (defName.Contains("Mine"))
                return "Mine Targets";
            if (defName.Contains("Cancel"))
                return "Canceled Orders";
            if (defName.Contains("Haul"))
                return "Haul Targets";
            if (defName.Contains("Cut") || defName.Contains("Chop"))
                return "Cut Targets";
            if (defName.Contains("Harvest"))
                return "Harvest Targets";
            if (defName.Contains("Tame"))
                return "Tame Targets";
            if (defName.Contains("Slaughter"))
                return "Slaughter Targets";
            if (defName.Contains("Deconstruct"))
                return "Deconstruct Targets";

            return $"{label} Targets";
        }

        /// <summary>
        /// Creates or updates the temporary scanner category with current obstacles.
        /// </summary>
        private static void UpdateObstacleCategory()
        {
            if (obstacleCells.Count == 0)
            {
                ScannerState.RemoveTemporaryCategory();
                return;
            }

            // Create scanner items for each obstacle cell
            var cursorPos = MapNavigationState.CurrentCursorPosition;
            var obstacleItems = new List<ScannerItem>();

            foreach (var cell in obstacleCells)
            {
                string obstacleDesc = GetObstacleDescription(cell);
                var item = new ScannerItem(cell, $"Obstacle: {obstacleDesc}", cursorPos);
                obstacleItems.Add(item);
            }

            // Sort by distance
            obstacleItems.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Create temporary category in ScannerState
            ScannerState.CreateTemporaryCategory("Obstacles", obstacleItems);
        }

        /// <summary>
        /// Gets a description of what is blocking placement at a cell.
        /// </summary>
        private static string GetObstacleDescription(IntVec3 cell)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "Unknown";

            // Check for things at the cell
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                // Skip plants, filth, and other minor things
                if (thing is Building || thing is Pawn)
                {
                    return thing.LabelShort ?? thing.def?.label ?? "Something";
                }

                // Check for blueprints or frames
                if (thing.def.IsBlueprint || thing.def.IsFrame)
                {
                    return thing.LabelShort ?? "Blueprint";
                }

                // Check for items
                if (thing.def.category == ThingCategory.Item)
                {
                    return thing.LabelShort ?? "Item";
                }
            }

            // Check terrain
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain != null && (terrain.passability == Traversability.Impassable || !terrain.affordances?.Contains(TerrainAffordanceDefOf.Light) == true))
            {
                return terrain.label ?? "Terrain";
            }

            // Check if out of bounds
            if (!cell.InBounds(map))
            {
                return "Out of bounds";
            }

            // Check for roofing issues
            if (map.roofGrid?.RoofAt(cell) != null)
            {
                // Some buildings can't be placed under certain roofs
                return "Roofed area";
            }

            return "Blocked";
        }

        /// <summary>
        /// Exits viewing mode and confirms all placements.
        /// Also exits architect/placement mode entirely.
        /// </summary>
        public static void Confirm()
        {
            if (!isActive)
                return;

            int totalPlaced = PlacedCount;
            string itemType = isBuildDesignator ? "blueprints" : "designations";
            TolkHelper.Speak($"Orders confirmed. {totalPlaced} {itemType} placed.", SpeechPriority.Normal);

            // Restore scanner focus
            RestoreFocus();

            Reset();

            // Also exit architect/placement mode entirely
            ShapePlacementState.Reset();
            ArchitectState.Reset();

            Log.Message("[ViewingModeState] Confirmed and exited placement mode");
        }

        /// <summary>
        /// Removes the last segment only and stays in viewing mode.
        /// For Build designators: Destroys blueprints
        /// For Orders/Zones/Cells: Removes designations from DesignationManager
        /// If no segments left, exits viewing mode and returns to placement.
        /// </summary>
        public static void RemoveLastSegment()
        {
            if (!isActive)
                return;

            int currentSegCount = SegmentCount;
            if (currentSegCount == 0)
                return;

            int removedCount = 0;
            string itemType = isBuildDesignator ? "blueprints" : "designations";

            if (isBuildDesignator)
            {
                // For Build designators, destroy the Things (blueprints)
                var lastSegment = segments[segments.Count - 1];
                foreach (Thing blueprint in lastSegment)
                {
                    if (blueprint != null && !blueprint.Destroyed)
                    {
                        blueprint.Destroy(DestroyMode.Cancel);
                        removedCount++;
                    }
                }
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                // For Orders/Zones/Cells, remove designations from DesignationManager
                var lastCellSegment = cellSegments[cellSegments.Count - 1];
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    foreach (IntVec3 cell in lastCellSegment)
                    {
                        // Try to remove any designation at this cell
                        bool removed = RemoveDesignationAtCell(cell, map);
                        if (removed)
                            removedCount++;
                    }
                }
                cellSegments.RemoveAt(cellSegments.Count - 1);
            }

            // Announce what happened
            int remainingCount = PlacedCount;
            int remainingSegments = SegmentCount;

            if (remainingSegments > 0)
            {
                TolkHelper.Speak($"Removed {removedCount} {itemType}. {remainingCount} remaining in {remainingSegments} segments.", SpeechPriority.Normal);
            }
            else
            {
                // No segments left - go back to placement mode
                TolkHelper.Speak($"Removed {removedCount} {itemType}. Returning to placement.", SpeechPriority.Normal);

                Designator savedDesignator = activeDesignator;
                ShapeType savedShape = usedShapeType;

                Reset();

                if (savedDesignator != null && savedShape != ShapeType.Manual)
                {
                    ShapePlacementState.Enter(savedDesignator, savedShape);
                }
            }

            Log.Message($"[ViewingModeState] Removed last segment ({removedCount} {itemType}), {remainingSegments} segments remaining");
        }

        /// <summary>
        /// Returns to viewing mode from shape placement when user presses Escape at SettingFirstCorner.
        /// Called when there are still segments on the stack.
        /// </summary>
        public static void ReturnFromShapePlacement()
        {
            // Only valid if we have segments preserved via isAddingMore flag
            int segCount = SegmentCount;
            if (!isAddingMore || segCount == 0)
            {
                Log.Warning("[ViewingModeState] ReturnFromShapePlacement called but no segments to return to");
                return;
            }

            // Re-activate viewing mode
            isActive = true;
            isAddingMore = false;

            // Announce return to viewing mode
            int totalPlaced = PlacedCount;
            string itemType = isBuildDesignator ? "blueprints" : "designations";
            string segmentInfo = segCount > 1 ? $" ({segCount} segments)" : "";
            TolkHelper.Speak($"Back to viewing mode. {totalPlaced} {itemType}{segmentInfo}.", SpeechPriority.Normal);

            Log.Message($"[ViewingModeState] Returned from shape placement, {segCount} segments");
        }

        /// <summary>
        /// Reactivates viewing mode without resetting state.
        /// Used when returning from shape placement via Escape.
        /// Keeps all segments intact and simply re-enables viewing mode.
        /// </summary>
        public static void Reactivate()
        {
            int segCount = SegmentCount;
            if (segCount == 0)
            {
                Log.Warning("[ViewingModeState] Reactivate called but no segments exist");
                return;
            }

            // Re-activate viewing mode without resetting anything
            isActive = true;
            isAddingMore = false;

            TolkHelper.Speak("Returned to preview mode", SpeechPriority.Normal);

            Log.Message($"[ViewingModeState] Reactivated with {segCount} segments");
        }

        /// <summary>
        /// Goes back to shape placement mode to add another segment.
        /// Keeps existing blueprints in place.
        /// </summary>
        public static void AddAnotherShape()
        {
            if (!isActive)
                return;

            Designator savedDesignator = activeDesignator;
            ShapeType savedShape = usedShapeType;

            // Mark that we're adding more - don't clear segments on re-entry
            isAddingMore = true;

            // Exit viewing mode but keep segments intact
            isActive = false;

            TolkHelper.Speak("Add another shape", SpeechPriority.Normal);

            // Enter shape placement mode with viewing mode on stack (so Escape can return here)
            if (savedDesignator != null && savedShape != ShapeType.Manual)
            {
                ShapePlacementState.Enter(savedDesignator, savedShape, fromViewingMode: true);
            }

            Log.Message("[ViewingModeState] Exited to add another shape");
        }

        /// <summary>
        /// Removes last segment AND returns to placement mode.
        /// This is the Escape key behavior.
        /// Works for all designator types: Build (blueprints), Orders, Zones, and Cells.
        /// </summary>
        public static void UndoLastAndReturn()
        {
            if (!isActive)
                return;

            int removedCount = 0;
            string itemType = isBuildDesignator ? "blueprints" : "designations";
            int currentSegCount = SegmentCount;

            // Remove the last segment if there is one
            if (currentSegCount > 0)
            {
                if (isBuildDesignator)
                {
                    var lastSegment = segments[segments.Count - 1];
                    foreach (Thing blueprint in lastSegment)
                    {
                        if (blueprint != null && !blueprint.Destroyed)
                        {
                            blueprint.Destroy(DestroyMode.Cancel);
                            removedCount++;
                        }
                    }
                    segments.RemoveAt(segments.Count - 1);
                }
                else
                {
                    var lastCellSegment = cellSegments[cellSegments.Count - 1];
                    Map map = Find.CurrentMap;
                    if (map != null)
                    {
                        foreach (IntVec3 cell in lastCellSegment)
                        {
                            if (RemoveDesignationAtCell(cell, map))
                                removedCount++;
                        }
                    }
                    cellSegments.RemoveAt(cellSegments.Count - 1);
                }

                TolkHelper.Speak($"Removed {removedCount} {itemType}. Returning to placement.", SpeechPriority.Normal);
            }
            else
            {
                TolkHelper.Speak("Returning to placement.", SpeechPriority.Normal);
            }

            // Save designator and shape before leaving
            Designator savedDesignator = activeDesignator;
            ShapeType savedShape = usedShapeType;

            // If there are still segments, just temporarily exit viewing mode
            // When they come back via Enter, we'll add to existing segments
            int remainingSegments = SegmentCount;
            if (remainingSegments > 0)
            {
                isAddingMore = true; // Keep segments on re-entry
                isActive = false;
            }
            else
            {
                // No segments left, fully reset
                RestoreFocus();
                Reset();
            }

            // Return to shape placement mode with viewing mode on stack if segments remain
            if (savedDesignator != null && savedShape != ShapeType.Manual)
            {
                bool hasRemainingSegments = SegmentCount > 0;
                ShapePlacementState.Enter(savedDesignator, savedShape, fromViewingMode: hasRemainingSegments);
            }

            Log.Message($"[ViewingModeState] Undid last segment and returned to placement, {SegmentCount} segments remaining");
        }

        /// <summary>
        /// Removes ALL placed items and exits completely.
        /// For Build designators: Destroys blueprints
        /// For Orders/Zones/Cells: Removes designations
        /// </summary>
        public static void UndoAll()
        {
            if (!isActive)
                return;

            int removedCount = 0;
            string itemType = isBuildDesignator ? "blueprints" : "designations";

            if (isBuildDesignator)
            {
                // Remove all blueprints from all segments
                foreach (var segment in segments)
                {
                    foreach (Thing blueprint in segment)
                    {
                        if (blueprint != null && !blueprint.Destroyed)
                        {
                            blueprint.Destroy(DestroyMode.Cancel);
                            removedCount++;
                        }
                    }
                }
            }
            else
            {
                // Remove all designations from all cell segments
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    foreach (var cellSegment in cellSegments)
                    {
                        foreach (IntVec3 cell in cellSegment)
                        {
                            if (RemoveDesignationAtCell(cell, map))
                                removedCount++;
                        }
                    }
                }
            }

            TolkHelper.Speak($"Removed all {removedCount} {itemType}", SpeechPriority.Normal);

            // Restore scanner focus
            RestoreFocus();

            // Save the designator and shape before reset
            Designator savedDesignator = activeDesignator;
            ShapeType savedShape = usedShapeType;

            Reset();

            // Re-enter shape placement mode with the same shape
            if (savedDesignator != null && savedShape != ShapeType.Manual)
            {
                ShapePlacementState.Enter(savedDesignator, savedShape);
            }

            Log.Message($"[ViewingModeState] Undid all {removedCount} {itemType} and returned to {savedShape} placement");
        }

        /// <summary>
        /// Shows a confirmation dialog before exiting viewing mode.
        /// "Leave" removes all blueprints and exits to game map.
        /// "Stay" closes the dialog and stays in preview mode.
        /// </summary>
        private static void ShowExitConfirmation()
        {
            if (!isActive)
                return;

            Find.WindowStack.Add(new Dialog_MessageBox(
                "Leave preview? All blueprints will be lost.",
                "Leave",
                () =>
                {
                    // Remove all blueprints/designations
                    int removedCount = 0;
                    string itemType = isBuildDesignator ? "blueprints" : "designations";

                    if (isBuildDesignator)
                    {
                        foreach (var segment in segments)
                        {
                            foreach (Thing blueprint in segment)
                            {
                                if (blueprint != null && !blueprint.Destroyed)
                                {
                                    blueprint.Destroy(DestroyMode.Cancel);
                                    removedCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        Map map = Find.CurrentMap;
                        if (map != null)
                        {
                            foreach (var cellSegment in cellSegments)
                            {
                                foreach (IntVec3 cell in cellSegment)
                                {
                                    if (RemoveDesignationAtCell(cell, map))
                                        removedCount++;
                                }
                            }
                        }
                    }

                    TolkHelper.Speak($"Removed all {removedCount} {itemType}. Exited preview.", SpeechPriority.Normal);

                    // Restore focus and exit completely
                    RestoreFocus();
                    Reset();

                    Log.Message($"[ViewingModeState] Exited via confirmation, removed {removedCount} {itemType}");
                },
                "Stay",
                null,
                "Confirm Exit",
                false
            ));
        }

        /// <summary>
        /// Removes a designation at a specific cell.
        /// Used for undoing Orders/Zones/Cells designations.
        /// </summary>
        /// <param name="cell">The cell to remove designation from</param>
        /// <param name="map">The map</param>
        /// <returns>True if a designation was removed</returns>
        private static bool RemoveDesignationAtCell(IntVec3 cell, Map map)
        {
            if (map?.designationManager == null)
                return false;

            // Try to get the designation def from the active designator
            DesignationDef designationDef = null;

            // Use reflection to get the protected Designation property from most designator types
            var designationProperty = activeDesignator?.GetType().GetProperty("Designation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (designationProperty != null)
            {
                designationDef = designationProperty.GetValue(activeDesignator) as DesignationDef;
            }

            if (designationDef != null)
            {
                // Remove designation of the specific type
                Designation existing = map.designationManager.DesignationAt(cell, designationDef);
                if (existing != null)
                {
                    map.designationManager.RemoveDesignation(existing);
                    return true;
                }
            }
            else
            {
                // Fallback: try to remove any designation at the cell
                // This is less precise but handles edge cases
                List<Designation> designations = new List<Designation>(map.designationManager.AllDesignationsAt(cell));
                foreach (var designation in designations)
                {
                    map.designationManager.RemoveDesignation(designation);
                }
                return designations.Count > 0;
            }

            return false;
        }

        /// <summary>
        /// Resets all state to inactive.
        /// </summary>
        private static void Reset()
        {
            isActive = false;
            isAddingMore = false;
            isBuildDesignator = false;
            isOrderDesignator = false;
            segments.Clear();
            cellSegments.Clear();
            obstacleCells.Clear();
            orderTargets.Clear();
            orderTargetCells.Clear();
            activeDesignator = null;
            usedShapeType = ShapeType.Manual;
            savedCursorPosition = IntVec3.Invalid;

            // Clean up scanner temporary category
            ScannerState.RemoveTemporaryCategory();
            ScannerState.RestoreFocus();
        }

        #endregion

        #region Blueprint Management

        /// <summary>
        /// Adds a blueprint at the current map cursor position.
        /// </summary>
        public static void AddBlueprintAtCursor()
        {
            if (!isActive)
                return;

            if (activeDesignator == null)
            {
                TolkHelper.Speak("No designator available", SpeechPriority.High);
                return;
            }

            Map map = Find.CurrentMap;
            if (map == null)
            {
                TolkHelper.Speak("No map available", SpeechPriority.High);
                return;
            }

            IntVec3 cursorPos = MapNavigationState.CurrentCursorPosition;

            // Check if we can place here
            AcceptanceReport report = activeDesignator.CanDesignateCell(cursorPos);
            if (!report.Accepted)
            {
                string reason = !string.IsNullOrEmpty(report.Reason) ? report.Reason : "Cannot place here";
                TolkHelper.Speak(reason, SpeechPriority.Normal);
                return;
            }

            try
            {
                // Track things before placement
                List<Thing> thingsBefore = new List<Thing>(cursorPos.GetThingList(map));

                // Place the blueprint
                activeDesignator.DesignateSingleCell(cursorPos);

                // Call Finalize to play the placement sound (like manual placement does)
                activeDesignator.Finalize(true);

                // Find the newly placed blueprint
                List<Thing> thingsAfter = cursorPos.GetThingList(map);
                foreach (Thing thing in thingsAfter)
                {
                    if (!thingsBefore.Contains(thing) &&
                        (thing.def.IsBlueprint || thing.def.IsFrame))
                    {
                        // Add to the last segment (or create one if needed)
                        if (segments.Count == 0)
                        {
                            segments.Add(new List<Thing>());
                        }
                        segments[segments.Count - 1].Add(thing);
                        break;
                    }
                }

                // Remove from obstacle list if it was there and update scanner category
                if (obstacleCells.Contains(cursorPos))
                {
                    obstacleCells.Remove(cursorPos);
                    UpdateObstacleCategory();
                }

                // Announce like manual placement: "{label} placed at x, z"
                string label = activeDesignator.Label ?? "Blueprint";
                TolkHelper.Speak($"{label} placed at {cursorPos.x}, {cursorPos.z}", SpeechPriority.Normal);
                Log.Message($"[ViewingModeState] Added blueprint at {cursorPos}");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ViewingModeState] Error adding blueprint: {ex.Message}");
                TolkHelper.Speak("Failed to add blueprint", SpeechPriority.High);
            }
        }

        /// <summary>
        /// Removes a blueprint at the current map cursor position.
        /// </summary>
        public static void RemoveBlueprintAtCursor()
        {
            if (!isActive)
                return;

            Map map = Find.CurrentMap;
            if (map == null)
            {
                TolkHelper.Speak("No map available", SpeechPriority.High);
                return;
            }

            IntVec3 cursorPos = MapNavigationState.CurrentCursorPosition;

            // Find blueprints at cursor position that we placed
            List<Thing> things = cursorPos.GetThingList(map);
            Thing blueprintToRemove = null;
            var allPlaced = PlacedBlueprints;

            foreach (Thing thing in things)
            {
                if ((thing.def.IsBlueprint || thing.def.IsFrame) && allPlaced.Contains(thing))
                {
                    blueprintToRemove = thing;
                    break;
                }
            }

            if (blueprintToRemove == null)
            {
                TolkHelper.Speak("No blueprint to cancel here", SpeechPriority.Normal);
                return;
            }

            // Get the label before destroying
            string thingLabel = blueprintToRemove.Label;

            // Remove the blueprint from whichever segment contains it
            foreach (var segment in segments)
            {
                if (segment.Remove(blueprintToRemove))
                    break;
            }
            blueprintToRemove.Destroy(DestroyMode.Cancel);

            // Play cancel sound and announce like manual placement
            SoundDefOf.Designate_Cancel.PlayOneShotOnCamera();
            TolkHelper.Speak($"Cancelled {thingLabel}", SpeechPriority.Normal);
            Log.Message($"[ViewingModeState] Removed blueprint at {cursorPos}");
        }

        #endregion

        #region Focus Management

        /// <summary>
        /// Saves the current cursor position for later restoration.
        /// </summary>
        private static void SaveFocus()
        {
            savedCursorPosition = MapNavigationState.CurrentCursorPosition;
            Log.Message($"[ViewingModeState] Saved cursor position: {savedCursorPosition}");
        }

        /// <summary>
        /// Restores the cursor position saved when entering viewing mode.
        /// </summary>
        private static void RestoreFocus()
        {
            if (savedCursorPosition.IsValid)
            {
                MapNavigationState.CurrentCursorPosition = savedCursorPosition;
                Find.CameraDriver?.JumpToCurrentMapLoc(savedCursorPosition);
                Log.Message($"[ViewingModeState] Restored cursor position: {savedCursorPosition}");
            }
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Processes keyboard input for viewing mode.
        /// </summary>
        /// <param name="key">The key code pressed</param>
        /// <param name="shift">Whether shift is held</param>
        /// <returns>True if the input was handled</returns>
        public static bool HandleInput(KeyCode key, bool shift)
        {
            if (!isActive)
                return false;

            // Note: PageUp/PageDown/Home for obstacle navigation are handled by ScannerState
            // via the temporary "Obstacles" category

            switch (key)
            {
                case KeyCode.Space:
                    if (shift)
                    {
                        RemoveBlueprintAtCursor();
                    }
                    else
                    {
                        AddBlueprintAtCursor();
                    }
                    return true;

                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                    // = key - add another shape, keep existing blueprints
                    AddAnotherShape();
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    // - key - remove last segment only, stay in viewing mode
                    RemoveLastSegment();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Enter - finalize all placements
                    Confirm();
                    return true;

                case KeyCode.Escape:
                    // Escape - show confirmation dialog before leaving preview
                    ShowExitConfirmation();
                    return true;
            }

            return false;
        }

        #endregion
    }
}
