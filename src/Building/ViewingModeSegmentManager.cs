using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Handles segment stack management and undo functionality for ViewingModeState.
    /// Contains methods for removing segments, tracking items, and managing undo operations
    /// for all designator types (Build, Zone, Orders, Cells).
    /// </summary>
    public static class ViewingModeSegmentManager
    {
        #region Segment Removal

        /// <summary>
        /// Removes items from a single segment (blueprints, zone cells, or designations).
        /// This is the core removal logic for all segment types.
        /// </summary>
        /// <param name="segmentIndex">Index of the segment to remove, or -1 to process all segments</param>
        /// <param name="segments">List of blueprint segments (for Build designators)</param>
        /// <param name="cellSegments">List of cell segments (for non-Build designators)</param>
        /// <param name="isBuildDesignator">Whether this is a Build designator</param>
        /// <param name="isZoneDesignator">Whether this is a Zone designator</param>
        /// <param name="activeDesignator">The active designator (for designation removal)</param>
        /// <param name="targetZone">The target zone (for zone undo cell count calculation)</param>
        /// <returns>The count of items that were removed</returns>
        public static int RemoveSegmentItems(
            int segmentIndex,
            List<List<Thing>> segments,
            List<List<IntVec3>> cellSegments,
            bool isBuildDesignator,
            bool isZoneDesignator,
            Designator activeDesignator,
            Zone targetZone)
        {
            int removedCount = 0;
            Map map = Find.CurrentMap;

            if (isBuildDesignator)
            {
                removedCount = RemoveBuildSegmentItems(segmentIndex, segments);
            }
            else if (isZoneDesignator)
            {
                removedCount = RemoveZoneSegmentItems(segmentIndex, cellSegments, map, targetZone);
            }
            else
            {
                removedCount = RemoveDesignationSegmentItems(segmentIndex, cellSegments, map, activeDesignator);
            }

            return removedCount;
        }

        /// <summary>
        /// Removes blueprint items from Build designator segments.
        /// </summary>
        private static int RemoveBuildSegmentItems(int segmentIndex, List<List<Thing>> segments)
        {
            int removedCount = 0;

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

            return removedCount;
        }

        /// <summary>
        /// Removes zone cells using ZoneUndoTracker to restore previous state.
        /// </summary>
        private static int RemoveZoneSegmentItems(
            int segmentIndex,
            List<List<IntVec3>> cellSegments,
            Map map,
            Zone targetZone)
        {
            int removedCount = 0;

            if (map != null)
            {
                if (segmentIndex >= 0 && segmentIndex < cellSegments.Count)
                {
                    // Get the count from the cell segment - this is what was actually placed
                    // This handles both new zone creation (where LastSegmentTargetZone is null)
                    // and zone expansion (where we want the actual cells placed, not zone diff)
                    removedCount = cellSegments[segmentIndex].Count;

                    // Perform the undo
                    ZoneUndoTracker.UndoLastSegment(map);
                }
                else if (segmentIndex == -1)
                {
                    // For undo all, sum up all cell segments
                    foreach (var segment in cellSegments)
                    {
                        removedCount += segment.Count;
                    }

                    // Perform the undo
                    ZoneUndoTracker.UndoAll(map);
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Removes designations from DesignationManager for Orders/Cells designators.
        /// </summary>
        private static int RemoveDesignationSegmentItems(
            int segmentIndex,
            List<List<IntVec3>> cellSegments,
            Map map,
            Designator activeDesignator)
        {
            int removedCount = 0;

            if (map != null)
            {
                if (segmentIndex >= 0 && segmentIndex < cellSegments.Count)
                {
                    // Remove single segment
                    foreach (IntVec3 cell in cellSegments[segmentIndex])
                    {
                        if (RemoveDesignationAtCell(cell, map, activeDesignator))
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
                            if (RemoveDesignationAtCell(cell, map, activeDesignator))
                                removedCount++;
                        }
                    }
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Removes a designation at a specific cell.
        /// Used for undoing Orders/Cells designations.
        /// </summary>
        /// <param name="cell">The cell to remove designation from</param>
        /// <param name="map">The map</param>
        /// <param name="activeDesignator">The active designator (to get the designation type)</param>
        /// <returns>True if a designation was removed</returns>
        public static bool RemoveDesignationAtCell(IntVec3 cell, Map map, Designator activeDesignator)
        {
            if (map?.designationManager == null)
                return false;

            // Try to get the designation def from the active designator
            DesignationDef designationDef = null;

            // Use reflection to get the protected Designation property from most designator types
            var designationProperty = activeDesignator?.GetType().GetProperty("Designation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

        #endregion

        #region Item Type Helpers

        /// <summary>
        /// Gets the item type string with proper pluralization based on count.
        /// Returns singular form for count of 1, plural form otherwise.
        /// </summary>
        /// <param name="count">The item count</param>
        /// <param name="isBuildDesignator">Whether this is a Build designator</param>
        /// <param name="isZoneDesignator">Whether this is a Zone designator</param>
        /// <returns>The item type string (e.g., "blueprint", "blueprints", "zone cell", "zone cells")</returns>
        public static string GetItemTypeForCount(int count, bool isBuildDesignator, bool isZoneDesignator)
        {
            if (isBuildDesignator)
                return count == 1 ? "blueprint" : "blueprints";
            else if (isZoneDesignator)
                return count == 1 ? "zone cell" : "zone cells";
            else
                return count == 1 ? "designation" : "designations";
        }

        #endregion

        #region Segment Stack Queries

        /// <summary>
        /// Gets the total count of placed items across all segments.
        /// </summary>
        /// <param name="segments">List of blueprint segments (for Build designators)</param>
        /// <param name="cellSegments">List of cell segments (for non-Build designators)</param>
        /// <param name="isBuildDesignator">Whether this is a Build designator</param>
        /// <returns>The total count of placed items</returns>
        public static int GetTotalPlacedCount(
            List<List<Thing>> segments,
            List<List<IntVec3>> cellSegments,
            bool isBuildDesignator)
        {
            if (isBuildDesignator)
            {
                int count = 0;
                foreach (var segment in segments)
                {
                    count += segment.Count;
                }
                return count;
            }
            else
            {
                int count = 0;
                foreach (var segment in cellSegments)
                {
                    count += segment.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// Gets all placed blueprints across all segments.
        /// Only meaningful for Build designators.
        /// </summary>
        /// <param name="segments">List of blueprint segments</param>
        /// <returns>A list of all placed blueprints</returns>
        public static List<Thing> GetAllPlacedBlueprints(List<List<Thing>> segments)
        {
            var all = new List<Thing>();
            foreach (var segment in segments)
            {
                all.AddRange(segment);
            }
            return all;
        }

        /// <summary>
        /// Gets all placed cells across all segments.
        /// For non-Build designators (Orders, Zones, Cells).
        /// </summary>
        /// <param name="cellSegments">List of cell segments</param>
        /// <returns>A list of all placed cells</returns>
        public static List<IntVec3> GetAllPlacedCells(List<List<IntVec3>> cellSegments)
        {
            var all = new List<IntVec3>();
            foreach (var segment in cellSegments)
            {
                all.AddRange(segment);
            }
            return all;
        }

        /// <summary>
        /// Gets the number of segments in the stack.
        /// </summary>
        /// <param name="segments">List of blueprint segments (for Build designators)</param>
        /// <param name="cellSegments">List of cell segments (for non-Build designators)</param>
        /// <param name="isBuildDesignator">Whether this is a Build designator</param>
        /// <returns>The segment count</returns>
        public static int GetSegmentCount(
            List<List<Thing>> segments,
            List<List<IntVec3>> cellSegments,
            bool isBuildDesignator)
        {
            return isBuildDesignator ? segments.Count : cellSegments.Count;
        }

        #endregion
    }
}
