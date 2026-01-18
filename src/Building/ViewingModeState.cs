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

        // Track detected enclosures formed by wall blueprints
        private static List<Enclosure> detectedEnclosures = new List<Enclosure>();

        // Track the number of disconnected regions for zone placements
        private static int detectedRegionCount = 0;

        // Track zones created by zone placement (for accurate confirmation message)
        private static HashSet<Zone> createdZones = new HashSet<Zone>();

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

        // Track whether this is a Zone designator (Stockpile, Growing, etc.)
        private static bool isZoneDesignator = false;

        // Track whether this is a delete/shrink designator (removes cells, no obstacles possible)
        private static bool isDeleteDesignator = false;

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

        /// <summary>
        /// Whether the current designator is a Zone type (Stockpile, Growing, etc.).
        /// </summary>
        public static bool IsZoneDesignator => isZoneDesignator;

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
                createdZones.Clear();
                orderTargets.Clear();
                orderTargetCells.Clear();
                isBuildDesignator = ShapeHelper.IsBuildDesignator(designator);
                isOrderDesignator = ShapeHelper.IsOrderDesignator(designator);
                isZoneDesignator = ShapeHelper.IsZoneDesignator(designator);
                isDeleteDesignator = ShapeHelper.IsDeleteDesignator(designator);
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

            // Add any new obstacles (skip for delete designators since removing cells can't have obstacles)
            if (!isDeleteDesignator && result.ObstacleCells != null)
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

            // Detect wall enclosures for build designators (do this before UpdateObstacleCategory
            // so interior obstacles can be added to the scanner)
            // Pass obstacleCells so we can detect corner gaps in the perimeter
            detectedEnclosures.Clear();
            if (isBuildDesignator)
            {
                detectedEnclosures = EnclosureDetector.DetectEnclosures(PlacedBlueprints, Find.CurrentMap, obstacleCells);
            }

            // For zone designators, count disconnected regions and collect created zones
            detectedRegionCount = 0;
            if (isZoneDesignator && result.PlacedCells != null && result.PlacedCells.Count > 0)
            {
                // Count how many separate regions the valid cells form
                detectedRegionCount = CountDisconnectedRegions(result.PlacedCells);

                // Collect the actual zones created (for confirm message)
                CollectCreatedZones(result.PlacedCells);

                // Store the zone undo record as a segment (for undo support)
                ZoneUndoTracker.AddSegment();
            }

            // Create temporary scanner category for obstacles or targets
            // Skip for delete designators since they can't have obstacles
            if (isOrderDesignator)
            {
                // For orders, create targets category instead of obstacles
                UpdateTargetsCategory();
            }
            else if (!isDeleteDesignator)
            {
                // For builds and zone-add, create obstacles category (includes interior obstacles from enclosures)
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

        #region Announcement Building
        // NOTE: The announcement methods (BuildEntryAnnouncement, BuildZoneDesignatorAnnouncement,
        // BuildBuildDesignatorAnnouncement, BuildOrderTargetSummary, FormatCountedList) are kept
        // in this class rather than extracted to a helper because they have deep dependencies on
        // the static state (isOrderDesignator, obstacleCells, detectedEnclosures, orderTargets, etc.).
        // Extracting them would require either unwieldy parameter lists or breaking encapsulation.
        // The ~400 lines they occupy are acceptable given they're cohesive with the state they access.

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
            else if (isZoneDesignator)
            {
                // Build zone-specific announcement with obstacle info and split warning
                return BuildZoneDesignatorAnnouncement(designator, totalPlaced, segmentInfo);
            }
            else
            {
                // Build announcement for build designators with smooth flowing sentences
                return BuildBuildDesignatorAnnouncement(designator, totalPlaced, segmentInfo);
            }
        }

        /// <summary>
        /// Builds the announcement specifically for zone designators.
        /// Produces smooth flowing sentences combining zone creation/expansion, obstacles, and split warnings.
        /// Uses dimensions instead of cell counts for clarity.
        /// Examples:
        /// - "50 by 12 stockpile zone created." (new zone)
        /// - "50 by 12 stockpile zone expanded." (adding to existing zone)
        /// - "50 by 12 stockpile zone created, 11 cells blocked by sandstones." (contiguous - no split warning)
        /// - "50 by 12 stockpile zone created, 11 cells blocked by 3 walls. Warning: This will create 3 separate zones. Press Escape to cancel."
        /// For delete/shrink: "Removed 50 cells from zone."
        /// </summary>
        private static string BuildZoneDesignatorAnnouncement(Designator designator, int totalPlaced, string segmentInfo)
        {
            var parts = new List<string>();

            // Handle delete/shrink designators differently
            if (isDeleteDesignator)
            {
                // For shrink, we're removing cells, not creating zones
                if (totalPlaced > 0)
                {
                    string cellWord = totalPlaced == 1 ? "cell" : "cells";
                    parts.Add($"Removed {totalPlaced} {cellWord} from zone{segmentInfo}");
                }
                else
                {
                    parts.Add($"No zone cells removed{segmentInfo}");
                }

                return $"Viewing mode. {string.Join(". ", parts)}.";
            }

            // Get zone type label from designator
            string zoneName = GetZoneTypeName(designator).ToLower();

            // Calculate dimensions from placed cells
            var placedCells = PlacedCells;
            var (width, height) = GetCellsDimensions(placedCells);

            // Get blocking obstacle summary using zone-specific detection
            string blockingObstacleSummary = GetZoneBlockingObstacleSummary();

            // Determine if this is an expansion of existing zone vs new zone creation
            // Use "expanded" for adding to existing zone, "created" for new zone
            string actionVerb = ZoneUndoTracker.WasZoneExpansion ? "expanded" : "created";

            // Build the main placement sentence
            if (obstacleCells.Count > 0 && totalPlaced > 0)
            {
                // "50 by 12 stockpile zone created, 11 cells blocked by [obstacle list]."
                string blockedPart;
                if (!string.IsNullOrEmpty(blockingObstacleSummary))
                {
                    blockedPart = $", {obstacleCells.Count} cells blocked by {blockingObstacleSummary}";
                }
                else
                {
                    blockedPart = $", {obstacleCells.Count} cells blocked";
                }

                parts.Add($"{width} by {height} {zoneName} zone {actionVerb}{blockedPart}{segmentInfo}");

                // Only add split warning if the valid cells form multiple disconnected regions
                // Obstacles on the edge don't cause splits - only obstacles that break contiguity do
                if (detectedRegionCount > 1)
                {
                    parts.Add($"Warning: This will create {detectedRegionCount} separate zones. Press Escape to cancel");
                }
            }
            else if (totalPlaced > 0)
            {
                // "50 by 12 stockpile zone created."
                parts.Add($"{width} by {height} {zoneName} zone {actionVerb}{segmentInfo}");
            }
            else
            {
                // No cells placed - all blocked
                parts.Add($"No zone cells placed{segmentInfo}");

                if (obstacleCells.Count > 0 && !string.IsNullOrEmpty(blockingObstacleSummary))
                {
                    parts.Add($"All {obstacleCells.Count} cells blocked by {blockingObstacleSummary}");
                }
            }

            // Add navigation hint if there are obstacles
            if (obstacleCells.Count > 0)
            {
                parts.Add("Page Up/Down to navigate obstacles");
            }

            return $"Viewing mode. {string.Join(". ", parts)}.";
        }

        /// <summary>
        /// Gets the zone type name from a zone designator.
        /// </summary>
        private static string GetZoneTypeName(Designator designator)
        {
            if (designator == null)
                return "Zone";

            // Try to get a clean label
            string label = designator.Label;
            if (!string.IsNullOrEmpty(label))
            {
                // Common patterns: "Create stockpile zone", "Create growing zone"
                // Extract just the zone type
                string lowerLabel = label.ToLower();
                if (lowerLabel.Contains("stockpile"))
                    return "Stockpile";
                if (lowerLabel.Contains("growing"))
                    return "Growing";
                if (lowerLabel.Contains("dumping"))
                    return "Dumping";
                if (lowerLabel.Contains("fishing"))
                    return "Fishing";

                // Fallback to the label with some cleanup
                label = label.Replace("Create ", "").Replace(" zone", "");
                if (!string.IsNullOrEmpty(label))
                    return char.ToUpper(label[0]) + label.Substring(1);
            }

            return "Zone";
        }

        /// <summary>
        /// Counts items by their label from a collection of cells.
        /// This is a shared helper for GetZoneBlockingObstacleSummary and GetBlockingObstacleSummary.
        /// </summary>
        /// <param name="cells">The cells to process</param>
        /// <param name="labelGetter">A function that gets the label for a cell</param>
        /// <param name="map">The map to use for label lookup</param>
        /// <returns>A dictionary of label to count</returns>
        private static Dictionary<string, int> CountItemsByLabel(IEnumerable<IntVec3> cells, System.Func<IntVec3, Map, string> labelGetter, Map map)
        {
            var counts = new Dictionary<string, int>();

            foreach (IntVec3 cell in cells)
            {
                string label = labelGetter(cell, map);
                if (counts.ContainsKey(label))
                    counts[label]++;
                else
                    counts[label] = 1;
            }

            return counts;
        }

        /// <summary>
        /// Gets a formatted summary of obstacles blocking zone placement.
        /// Specifically looks for things that can't overlap zones (buildings, walls, etc.).
        /// Groups obstacles by type and formats as "X thing1, Y thing2, and Z thing3".
        /// </summary>
        private static string GetZoneBlockingObstacleSummary()
        {
            if (obstacleCells.Count == 0)
                return string.Empty;

            Map map = Find.CurrentMap;
            if (map == null)
                return string.Empty;

            var obstacleCounts = CountItemsByLabel(obstacleCells, GetZoneObstacleLabel, map);
            return FormatCountedList(obstacleCounts, truncate: true);
        }

        /// <summary>
        /// Gets a simple label for the obstacle blocking zone placement at a cell.
        /// Checks for things that can't overlap zones according to RimWorld's CanOverlapZones property.
        /// </summary>
        private static string GetZoneObstacleLabel(IntVec3 cell, Map map)
        {
            // Check for things at the cell that can't overlap zones
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                // Check if this thing prevents zone overlap
                if (thing.def != null && !thing.def.CanOverlapZones)
                {
                    // Try multiple label sources to get a meaningful name
                    string label = thing.LabelNoCount;
                    if (string.IsNullOrEmpty(label))
                    {
                        label = thing.def?.label;
                    }
                    if (string.IsNullOrEmpty(label))
                    {
                        // For blueprints/frames, try to get the label of what's being built
                        if (thing.def?.entityDefToBuild is ThingDef builtDef)
                        {
                            label = builtDef.label + " (unbuilt)";
                        }
                    }
                    if (string.IsNullOrEmpty(label))
                    {
                        // Use the thing's category as a last resort
                        label = thing.def?.category.ToString().ToLower() ?? "structure";
                    }
                    return label;
                }
            }

            // Check for existing zone of different type
            Zone existingZone = map.zoneManager.ZoneAt(cell);
            if (existingZone != null)
            {
                return $"existing {existingZone.label}";
            }

            // Check if too close to map edge
            if (cell.InNoZoneEdgeArea(map))
            {
                return TerrainPrefix + "edge area";
            }

            // Check if fogged
            if (cell.Fogged(map))
            {
                return "fog of war";
            }

            // Check terrain for impassable areas
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain != null && terrain.passability == Traversability.Impassable)
            {
                return TerrainPrefix + (terrain.label ?? "terrain");
            }

            // If we still can't identify the obstacle, use a clear fallback
            // This shouldn't happen often, but if it does, "blocked cell" is clearer than "obstacle"
            return "blocked cell";
        }

        /// <summary>
        /// Builds the announcement specifically for build designators.
        /// Produces smooth flowing sentences combining placement, obstacles, and enclosure info.
        /// Examples:
        /// - "Placed 28 wooden walls (140 wood). 7 by 9 enclosure formed containing 2 poplar trees, 1 cougar (dead), and 1 limestone chunk, but has 1 gap."
        /// - "Placed 24 of 28 wooden walls (120 wood). 4 walls blocked by 2 compacted steel and 2 granite boulders."
        /// </summary>
        private static string BuildBuildDesignatorAnnouncement(Designator designator, int totalPlaced, string segmentInfo)
        {
            var parts = new List<string>();

            // Get designator label for the announcement
            // Use sanitized label to strip "..." suffix (prevents "wall...s" bug)
            string designatorLabel = ArchitectHelper.GetSanitizedLabel(designator, "blueprints");
            string itemType = isBuildDesignator ? "blueprints" : "designations";

            // Calculate total intended placements (placed + blocked)
            int totalIntended = totalPlaced + obstacleCells.Count;

            // Get blocking obstacle summary
            string blockingObstacleSummary = GetBlockingObstacleSummary();

            // Build the main placement sentence
            if (obstacleCells.Count > 0 && totalPlaced > 0)
            {
                // "Placed X of Y wooden walls (cost), Z blocked by [obstacle list]."
                // Combined into one sentence for conciseness
                string pluralLabel = totalPlaced > 1
                    ? Find.ActiveLanguageWorker.Pluralize(designatorLabel, totalPlaced)
                    : designatorLabel;

                string costInfo = GetCostInfo();
                string blockedPart;
                if (!string.IsNullOrEmpty(blockingObstacleSummary))
                {
                    blockedPart = $", {obstacleCells.Count} blocked by {blockingObstacleSummary}";
                }
                else
                {
                    blockedPart = $", {obstacleCells.Count} blocked";
                }
                parts.Add($"Placed {totalPlaced} of {totalIntended} {pluralLabel}{costInfo}{blockedPart}{segmentInfo}");
            }
            else if (totalPlaced > 0)
            {
                // "Placed X wooden walls (cost)."
                string pluralLabel = totalPlaced > 1
                    ? Find.ActiveLanguageWorker.Pluralize(designatorLabel, totalPlaced)
                    : designatorLabel;

                string costInfo = GetCostInfo();
                parts.Add($"Placed {totalPlaced} {pluralLabel}{costInfo}{segmentInfo}");
            }
            else
            {
                // No placements at all
                parts.Add($"No {itemType} placed{segmentInfo}");

                if (obstacleCells.Count > 0 && !string.IsNullOrEmpty(blockingObstacleSummary))
                {
                    parts.Add($"All blocked by {blockingObstacleSummary}");
                }
            }

            // Add enclosure info if any enclosures detected
            if (detectedEnclosures.Count > 0)
            {
                if (detectedEnclosures.Count == 1)
                {
                    var enc = detectedEnclosures[0];
                    var (width, height) = GetEnclosureDimensions(enc);
                    string enclosurePart = $"{width} by {height} enclosure formed";

                    // Add "containing X, Y, Z" if there are interior obstacles
                    if (enc.ObstacleCount > 0 && enc.Obstacles != null)
                    {
                        string interiorSummary = FormatObstacleList(enc.Obstacles);
                        if (!string.IsNullOrEmpty(interiorSummary))
                        {
                            enclosurePart += $" containing {interiorSummary}";
                        }
                    }

                    // Add gap warning at the end
                    if (enc.HasGaps)
                    {
                        string gapWord = enc.GapCount == 1 ? "gap" : "gaps";
                        enclosurePart += $", but has {enc.GapCount} {gapWord}";
                    }

                    parts.Add(enclosurePart);
                }
                else
                {
                    int totalInteriorObstacles = detectedEnclosures.Sum(e => e.ObstacleCount);
                    int totalGaps = detectedEnclosures.Sum(e => e.GapCount);

                    // List dimensions for each enclosure
                    var dimensionsList = new List<string>();
                    foreach (var enc in detectedEnclosures)
                    {
                        var (w, h) = GetEnclosureDimensions(enc);
                        dimensionsList.Add($"{w} by {h}");
                    }
                    string enclosurePart = $"{detectedEnclosures.Count} enclosures formed: {string.Join(", ", dimensionsList)}";

                    // Add "containing X, Y, Z" if there are interior obstacles
                    if (totalInteriorObstacles > 0)
                    {
                        // Gather all interior obstacles from all enclosures
                        var allInteriorObstacles = new List<ScannerItem>();
                        foreach (var enc in detectedEnclosures)
                        {
                            if (enc.Obstacles != null)
                                allInteriorObstacles.AddRange(enc.Obstacles);
                        }

                        string interiorSummary = FormatObstacleList(allInteriorObstacles);
                        if (!string.IsNullOrEmpty(interiorSummary))
                        {
                            enclosurePart += $" containing {interiorSummary}";
                        }
                    }

                    // Add gap warning at the end
                    if (totalGaps > 0)
                    {
                        string gapWord = totalGaps == 1 ? "gap" : "gaps";
                        enclosurePart += $", but {totalGaps} {gapWord} total";
                    }

                    parts.Add(enclosurePart);
                }
            }

            // Add navigation hint if there are obstacles or interior obstacles
            bool hasAnyObstacles = obstacleCells.Count > 0 || detectedEnclosures.Any(e => e.ObstacleCount > 0);
            if (hasAnyObstacles)
            {
                parts.Add("Page Up/Down to navigate");
            }

            return $"Viewing mode. {string.Join(". ", parts)}.";
        }

        /// <summary>
        /// Gets a formatted summary of blocking obstacles (obstacles that prevented placement).
        /// Groups obstacles by type and formats as "X thing1, Y thing2, and Z thing3".
        /// </summary>
        private static string GetBlockingObstacleSummary()
        {
            if (obstacleCells.Count == 0)
                return string.Empty;

            Map map = Find.CurrentMap;
            if (map == null)
                return string.Empty;

            var obstacleCounts = CountItemsByLabel(obstacleCells, GetObstacleLabel, map);
            return FormatCountedList(obstacleCounts, truncate: true);
        }

        // Prefix used to mark terrain-based obstacles for special formatting
        private const string TerrainPrefix = "TERRAIN:";

        /// <summary>
        /// Gets a simple label for the obstacle at a cell (without "Obstacle:" prefix).
        /// Terrain-based obstacles are prefixed with "TERRAIN:" for special formatting.
        /// </summary>
        private static string GetObstacleLabel(IntVec3 cell, Map map)
        {
            // Check for things at the cell
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                // Skip plants, filth, and other minor things
                if (thing is Building || thing is Pawn)
                {
                    return thing.LabelNoCount ?? thing.def?.label ?? "obstacle";
                }

                // Check for blueprints or frames
                if (thing.def.IsBlueprint || thing.def.IsFrame)
                {
                    return thing.LabelNoCount ?? "blueprint";
                }

                // Check for items
                if (thing.def.category == ThingCategory.Item)
                {
                    return thing.LabelNoCount ?? "item";
                }
            }

            // Check terrain - prefix with TERRAIN: for special formatting as "X tiles"
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain != null && (terrain.passability == Traversability.Impassable || !terrain.affordances?.Contains(TerrainAffordanceDefOf.Light) == true))
            {
                return TerrainPrefix + (terrain.label ?? "terrain");
            }

            return "obstacle";
        }

        /// <summary>
        /// Formats a list of ScannerItem obstacles as "X thing1, Y thing2, and Z thing3".
        /// Lists all types without truncation.
        /// </summary>
        private static string FormatObstacleList(List<ScannerItem> obstacles)
        {
            if (obstacles == null || obstacles.Count == 0)
                return string.Empty;

            // Count obstacles by their label
            var obstacleCounts = new Dictionary<string, int>();

            foreach (var obstacle in obstacles)
            {
                // Use the thing's LabelNoCount for proper grouping
                string label;
                if (obstacle.Thing != null)
                {
                    label = obstacle.Thing.LabelNoCount ?? obstacle.Thing.def?.label ?? "obstacle";
                }
                else
                {
                    // For terrain-based obstacles, use the stored label (strip any prefix)
                    label = obstacle.Label;
                    if (label.StartsWith("Interior: "))
                        label = label.Substring(10);
                }

                if (obstacleCounts.ContainsKey(label))
                    obstacleCounts[label]++;
                else
                    obstacleCounts[label] = 1;
            }

            return FormatCountedList(obstacleCounts, truncate: true);
        }

        /// <summary>
        /// Calculates the bounding box dimensions of an enclosure's interior cells.
        /// Returns width and height as interior dimensions (not including walls).
        /// </summary>
        /// <param name="enclosure">The enclosure to measure</param>
        /// <returns>A tuple of (width, height)</returns>
        private static (int width, int height) GetEnclosureDimensions(Enclosure enclosure)
        {
            if (enclosure?.InteriorCells == null || enclosure.InteriorCells.Count == 0)
                return (0, 0);

            return GetCellsDimensions(enclosure.InteriorCells);
        }

        /// <summary>
        /// Calculates the bounding box dimensions of a set of cells.
        /// Returns width and height of the bounding box.
        /// </summary>
        /// <param name="cells">The cells to measure</param>
        /// <returns>A tuple of (width, height)</returns>
        private static (int width, int height) GetCellsDimensions(IEnumerable<IntVec3> cells)
        {
            if (cells == null || !cells.Any())
                return (0, 0);

            int minX = cells.Min(c => c.x);
            int maxX = cells.Max(c => c.x);
            int minZ = cells.Min(c => c.z);
            int maxZ = cells.Max(c => c.z);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            return (width, height);
        }

        /// <summary>
        /// Gets the cells belonging to a zone.
        /// </summary>
        /// <param name="zone">The zone to get cells from</param>
        /// <returns>A list of cells in the zone</returns>
        private static List<IntVec3> GetZoneCells(Zone zone)
        {
            if (zone == null)
                return new List<IntVec3>();

            return zone.Cells.ToList();
        }

        /// <summary>
        /// Formats a dictionary of label counts as "X thing1, Y thing2, and Z thing3".
        /// Pluralizes labels when count > 1.
        /// Terrain-based obstacles (prefixed with TERRAIN:) are formatted as "X terrain tiles" instead of pluralizing.
        /// When truncate is true, groups with less than 1% of the total are summarized as
        /// "and X more in Y smaller groups".
        /// </summary>
        /// <param name="counts">Dictionary of label to count</param>
        /// <param name="truncate">If true, truncate groups under 1% threshold</param>
        private static string FormatCountedList(Dictionary<string, int> counts, bool truncate = false)
        {
            if (counts == null || counts.Count == 0)
                return string.Empty;

            var formattedParts = new List<string>();
            int smallGroupCount = 0;
            int smallGroupItems = 0;

            // Calculate 1% threshold for truncation
            int totalCount = counts.Values.Sum();
            int threshold = truncate ? totalCount / 100 : 0;
            if (truncate && threshold < 1)
                threshold = 1; // minimum 1

            // Sort by count descending for consistency
            foreach (var kvp in counts.OrderByDescending(x => x.Value))
            {
                // If truncating and this group is below threshold, add to summary
                if (truncate && kvp.Value < threshold)
                {
                    smallGroupCount++;
                    smallGroupItems += kvp.Value;
                    continue;
                }

                string key = kvp.Key;
                string label;

                // Check for terrain prefix - terrain gets "X terrain tiles" format
                if (key.StartsWith(TerrainPrefix))
                {
                    string terrainName = key.Substring(TerrainPrefix.Length);
                    label = kvp.Value == 1 ? $"{terrainName} tile" : $"{terrainName} tiles";
                }
                else
                {
                    // Regular items get pluralized
                    label = kvp.Value > 1
                        ? Find.ActiveLanguageWorker.Pluralize(key, kvp.Value)
                        : key;
                }
                formattedParts.Add($"{kvp.Value} {label}");
            }

            // Add truncated summary if any small groups were skipped
            if (smallGroupCount > 0)
            {
                string groupWord = smallGroupCount == 1 ? "group" : "groups";
                formattedParts.Add($"{smallGroupItems} more in {smallGroupCount} smaller {groupWord}");
            }

            // Join with commas and "and" before the last item
            if (formattedParts.Count == 1)
            {
                return formattedParts[0];
            }
            else if (formattedParts.Count == 2)
            {
                return $"{formattedParts[0]} and {formattedParts[1]}";
            }
            else
            {
                // "X thing1, Y thing2, and Z thing3"
                string allButLast = string.Join(", ", formattedParts.Take(formattedParts.Count - 1));
                return $"{allButLast}, and {formattedParts.Last()}";
            }
        }

        /// <summary>
        /// Gets the cost info string for the current placement (e.g., "(140 wood)").
        /// </summary>
        private static string GetCostInfo()
        {
            // Try to calculate total cost from placed blueprints
            int totalCost = 0;
            string resourceName = string.Empty;

            var allBlueprints = PlacedBlueprints;
            if (allBlueprints.Count > 0 && allBlueprints[0]?.def?.entityDefToBuild is ThingDef thingDef)
            {
                // Get the stuff used (material)
                ThingDef stuffDef = ArchitectState.SelectedMaterial;

                if (thingDef.MadeFromStuff && stuffDef != null)
                {
                    totalCost = thingDef.CostStuffCount * allBlueprints.Count;
                    resourceName = stuffDef.label;
                }
                else if (thingDef.CostList != null && thingDef.CostList.Count > 0)
                {
                    totalCost = thingDef.CostList[0].count * allBlueprints.Count;
                    resourceName = thingDef.CostList[0].thingDef.label;
                }
            }

            if (totalCost > 0 && !string.IsNullOrEmpty(resourceName))
            {
                return $" ({totalCost} {resourceName})";
            }

            return string.Empty;
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

        #endregion

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
        /// Includes both placement obstacles and interior obstacles from enclosures.
        /// </summary>
        private static void UpdateObstacleCategory()
        {
            var cursorPos = MapNavigationState.CurrentCursorPosition;
            var allObstacles = new List<ScannerItem>();

            // Add placement obstacles (cells where placement failed)
            foreach (var cell in obstacleCells)
            {
                string obstacleDesc = GetObstacleDescription(cell);
                var item = new ScannerItem(cell, $"Obstacle: {obstacleDesc}", cursorPos);
                allObstacles.Add(item);
            }

            // Add interior obstacles from detected enclosures
            foreach (var enclosure in detectedEnclosures)
            {
                if (enclosure.Obstacles != null)
                {
                    foreach (var obstacle in enclosure.Obstacles)
                    {
                        // Create a new item with "Interior:" prefix
                        var item = new ScannerItem(obstacle.Thing, cursorPos);
                        item.Label = $"Interior: {obstacle.Label}";
                        allObstacles.Add(item);
                    }
                }
            }

            if (allObstacles.Count == 0)
            {
                ScannerState.RemoveTemporaryCategory();
                return;
            }

            // Sort by distance
            allObstacles.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // Create temporary category in ScannerState
            ScannerState.CreateTemporaryCategory("Obstacles", allObstacles);
        }

        /// <summary>
        /// Gets a description of what is blocking placement at a cell.
        /// For zone designators, uses zone-specific obstacle detection.
        /// </summary>
        private static string GetObstacleDescription(IntVec3 cell)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return "Unknown";

            // For zone designators, use zone-specific detection
            if (isZoneDesignator)
            {
                return GetZoneObstacleDescriptionForScanner(cell, map);
            }

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
        /// Gets a description for zone obstacles for scanner navigation.
        /// Returns a short, friendly description for screen reader announcement.
        /// </summary>
        private static string GetZoneObstacleDescriptionForScanner(IntVec3 cell, Map map)
        {
            // Check for things that can't overlap zones
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing.def != null && !thing.def.CanOverlapZones)
                {
                    return thing.LabelShort ?? thing.def?.label ?? "Obstacle";
                }
            }

            // Check for existing zone of different type
            Zone existingZone = map.zoneManager.ZoneAt(cell);
            if (existingZone != null)
            {
                return $"Existing {existingZone.label}";
            }

            // Check if too close to map edge
            if (cell.InNoZoneEdgeArea(map))
            {
                return "Edge area";
            }

            // Check if fogged
            if (cell.Fogged(map))
            {
                return "Fog of war";
            }

            // Check terrain
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain != null && terrain.passability == Traversability.Impassable)
            {
                return terrain.label ?? "Terrain";
            }

            return "Blocked";
        }

        /// <summary>
        /// Exits viewing mode and confirms all placements.
        /// Also exits architect/placement mode entirely.
        /// Note: We do NOT call RestoreFocus() here because users expect to stay
        /// at their current position after confirming (near their placed buildings).
        /// RestoreFocus() is only appropriate for cancel/undo operations.
        /// </summary>
        public static void Confirm()
        {
            if (!isActive)
                return;

            int totalPlaced = PlacedCount;
            string announcement;

            if (isZoneDesignator)
            {
                // Handle delete/shrink designators differently
                if (isDeleteDesignator)
                {
                    string cellWord = totalPlaced == 1 ? "cell" : "cells";
                    announcement = $"Confirmed. {totalPlaced} zone {cellWord} removed.";
                }
                else
                {
                    // For zone creation, report with dimensions
                    int zoneCount = createdZones.Count;
                    string zoneName = GetZoneTypeName(activeDesignator).ToLower();
                    string zoneWord = zoneCount == 1 ? "zone" : "zones";

                    if (zoneCount == 1)
                    {
                        // Single zone - just use overall dimensions
                        var (width, height) = GetCellsDimensions(PlacedCells);
                        announcement = $"Confirmed. {width} by {height} {zoneName} zone created.";
                    }
                    else
                    {
                        // Multiple zones - list dimensions for each, sorted by size
                        // Truncate smaller zones (under 1% of total cells)
                        var zoneSizes = new List<(Zone zone, int cellCount, string dims)>();
                        int totalCells = 0;

                        foreach (Zone zone in createdZones)
                        {
                            var zoneCells = GetZoneCells(zone);
                            var (w, h) = GetCellsDimensions(zoneCells);
                            int cellCount = zoneCells.Count;
                            totalCells += cellCount;
                            zoneSizes.Add((zone, cellCount, $"{w} by {h}"));
                        }

                        // Sort by cell count descending (largest first)
                        zoneSizes.Sort((a, b) => b.cellCount.CompareTo(a.cellCount));

                        // 1% threshold for truncation
                        int threshold = totalCells / 100;
                        if (threshold < 1)
                            threshold = 1;

                        var significantDimensions = new List<string>();
                        int smallZoneCount = 0;

                        foreach (var (zone, cellCount, dims) in zoneSizes)
                        {
                            if (cellCount >= threshold)
                            {
                                significantDimensions.Add(dims);
                            }
                            else
                            {
                                smallZoneCount++;
                            }
                        }

                        string dimensionsPart = string.Join(", ", significantDimensions);
                        if (smallZoneCount > 0)
                        {
                            string smallWord = smallZoneCount == 1 ? "zone" : "zones";
                            dimensionsPart += $", and {smallZoneCount} smaller {smallWord}";
                        }

                        announcement = $"Confirmed. {zoneCount} {zoneName} {zoneWord} created: {dimensionsPart}.";
                    }
                }
            }
            else if (isBuildDesignator)
            {
                announcement = $"Orders confirmed. {totalPlaced} blueprints placed.";
            }
            else
            {
                announcement = $"Orders confirmed. {totalPlaced} designations placed.";
            }

            TolkHelper.Speak(announcement, SpeechPriority.Normal);

            // Clear zone undo tracker data since changes are confirmed
            if (isZoneDesignator)
            {
                ZoneUndoTracker.Clear();
            }

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

            // Remove the last segment using centralized helper
            int lastIndex = isBuildDesignator ? segments.Count - 1 : cellSegments.Count - 1;
            int removedCount = RemoveSegmentItems(lastIndex);

            // Remove the segment from the list
            if (isBuildDesignator)
                segments.RemoveAt(segments.Count - 1);
            else
                cellSegments.RemoveAt(cellSegments.Count - 1);

            // Determine item type with proper pluralization
            string itemType = GetItemTypeForCount(removedCount);

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

            Log.Message($"[ViewingModeState] Removed last segment ({removedCount} items), {remainingSegments} segments remaining");
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
            int currentSegCount = SegmentCount;

            // Remove the last segment if there is one
            if (currentSegCount > 0)
            {
                // Remove the last segment using centralized helper
                int lastIndex = isBuildDesignator ? segments.Count - 1 : cellSegments.Count - 1;
                removedCount = RemoveSegmentItems(lastIndex);

                // Remove the segment from the list
                if (isBuildDesignator)
                    segments.RemoveAt(segments.Count - 1);
                else
                    cellSegments.RemoveAt(cellSegments.Count - 1);

                string itemType = GetItemTypeForCount(removedCount);
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

            // Remove all segments using centralized helper (-1 = all segments)
            int removedCount = RemoveSegmentItems(-1);

            string itemType = GetItemTypeForCount(removedCount);
            string allWord = removedCount == 1 ? "" : "all ";
            TolkHelper.Speak($"Removed {allWord}{removedCount} {itemType}", SpeechPriority.Normal);

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

            Log.Message($"[ViewingModeState] Undid all {removedCount} items and returned to {savedShape} placement");
        }

        /// <summary>
        /// Shows a confirmation dialog before exiting viewing mode.
        /// "Leave" removes all blueprints/designations/zone changes and exits to game map.
        /// "Stay" closes the dialog and stays in preview mode.
        /// </summary>
        private static void ShowExitConfirmation()
        {
            if (!isActive)
                return;

            // Determine dialog message based on designator type
            string dialogMessage;
            if (isBuildDesignator)
            {
                dialogMessage = "Leave preview? All blueprints will be lost.";
            }
            else if (isZoneDesignator)
            {
                dialogMessage = "Leave preview? All zone changes will be undone.";
            }
            else
            {
                dialogMessage = "Leave preview? All designations will be removed.";
            }

            Find.WindowStack.Add(new Dialog_MessageBox(
                dialogMessage,
                "Leave",
                () =>
                {
                    // Remove all segments using centralized helper (-1 = all segments)
                    int removedCount = RemoveSegmentItems(-1);

                    string itemType = GetItemTypeForCount(removedCount);
                    string allWord = removedCount == 1 ? "" : "all ";
                    TolkHelper.Speak($"Removed {allWord}{removedCount} {itemType}. Exited preview.", SpeechPriority.Normal);

                    // Restore focus and exit completely
                    RestoreFocus();
                    Reset();

                    Log.Message($"[ViewingModeState] Exited via confirmation, removed {removedCount} items");
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
        /// Gets the item type string with proper pluralization based on count.
        /// Returns singular form for count of 1, plural form otherwise.
        /// </summary>
        private static string GetItemTypeForCount(int count)
        {
            if (isBuildDesignator)
                return count == 1 ? "blueprint" : "blueprints";
            else if (isZoneDesignator)
                return count == 1 ? "zone cell" : "zone cells";
            else
                return count == 1 ? "designation" : "designations";
        }

        /// <summary>
        /// Removes items from a single segment (blueprints, zone cells, or designations).
        /// This is the core removal logic extracted from RemoveLastSegment, UndoLastAndReturn,
        /// UndoAll, and ShowExitConfirmation to eliminate code duplication.
        /// </summary>
        /// <param name="segmentIndex">Index of the segment to remove, or -1 to process all segments</param>
        /// <returns>The count of items that were removed</returns>
        private static int RemoveSegmentItems(int segmentIndex = -1)
        {
            int removedCount = 0;
            Map map = Find.CurrentMap;

            if (isBuildDesignator)
            {
                // For Build designators, destroy the Things (blueprints)
                if (segmentIndex >= 0 && segmentIndex < segments.Count)
                {
                    // Remove single segment
                    var segment = segments[segmentIndex];
                    foreach (Thing blueprint in segment)
                    {
                        if (blueprint != null && !blueprint.Destroyed)
                        {
                            blueprint.Destroy(DestroyMode.Cancel);
                            removedCount++;
                        }
                    }
                }
                else if (segmentIndex == -1)
                {
                    // Remove all segments
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
            }
            else if (isZoneDesignator)
            {
                // For Zone designators, use ZoneUndoTracker to restore previous state
                if (map != null)
                {
                    if (segmentIndex >= 0 && segmentIndex < cellSegments.Count)
                    {
                        // Count cells in segment for feedback
                        removedCount = cellSegments[segmentIndex].Count;
                        ZoneUndoTracker.UndoLastSegment(map);
                    }
                    else if (segmentIndex == -1)
                    {
                        // Count total cells for feedback
                        foreach (var cellSegment in cellSegments)
                        {
                            removedCount += cellSegment.Count;
                        }
                        ZoneUndoTracker.UndoAll(map);
                    }
                }
            }
            else
            {
                // For Orders/Cells, remove designations from DesignationManager
                if (map != null)
                {
                    if (segmentIndex >= 0 && segmentIndex < cellSegments.Count)
                    {
                        // Remove single segment
                        foreach (IntVec3 cell in cellSegments[segmentIndex])
                        {
                            if (RemoveDesignationAtCell(cell, map))
                                removedCount++;
                        }
                    }
                    else if (segmentIndex == -1)
                    {
                        // Remove all segments
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
            }

            return removedCount;
        }

        /// <summary>
        /// Resets all state to inactive.
        /// </summary>
        private static void Reset()
        {
            // Clear zone undo tracker data
            ZoneUndoTracker.Clear();

            isActive = false;
            isAddingMore = false;
            isBuildDesignator = false;
            isOrderDesignator = false;
            isZoneDesignator = false;
            isDeleteDesignator = false;
            segments.Clear();
            cellSegments.Clear();
            obstacleCells.Clear();
            detectedEnclosures.Clear();
            detectedRegionCount = 0;
            createdZones.Clear();
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

        #region Zone Region Detection

        /// <summary>
        /// Counts the number of disconnected regions among a set of cells using flood fill.
        /// This helps determine if zone placement will result in multiple separate zones.
        /// </summary>
        /// <param name="cells">The cells to analyze for contiguity</param>
        /// <returns>The number of disconnected regions (1 = fully contiguous, 2+ = will be split)</returns>
        private static int CountDisconnectedRegions(List<IntVec3> cells)
        {
            if (cells == null || cells.Count == 0)
                return 0;

            // Build a set for efficient lookup
            var remainingCells = new HashSet<IntVec3>(cells);
            int regionCount = 0;

            // Keep flood filling until all cells are processed
            while (remainingCells.Count > 0)
            {
                regionCount++;

                // Start a new region from any remaining cell
                IntVec3 startCell = default;
                foreach (var cell in remainingCells)
                {
                    startCell = cell;
                    break;
                }

                // Flood fill to find all connected cells in this region
                var queue = new Queue<IntVec3>();
                queue.Enqueue(startCell);
                remainingCells.Remove(startCell);

                while (queue.Count > 0)
                {
                    IntVec3 current = queue.Dequeue();

                    // Check all 4 cardinal neighbors
                    foreach (IntVec3 offset in GenAdj.CardinalDirections)
                    {
                        IntVec3 neighbor = current + offset;

                        // If this neighbor is in our remaining cells, it's connected
                        if (remainingCells.Contains(neighbor))
                        {
                            remainingCells.Remove(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return regionCount;
        }

        /// <summary>
        /// Collects the unique zones that were created/expanded by zone placement.
        /// Call this after DesignateMultiCell to find what zones contain our cells.
        /// </summary>
        /// <param name="placedCells">The cells that were designated for zoning</param>
        private static void CollectCreatedZones(List<IntVec3> placedCells)
        {
            Map map = Find.CurrentMap;
            if (map?.zoneManager == null || placedCells == null)
                return;

            foreach (IntVec3 cell in placedCells)
            {
                Zone zone = map.zoneManager.ZoneAt(cell);
                if (zone != null)
                {
                    createdZones.Add(zone);
                }
            }
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
