using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Records the state of a zone before/after a modification for undo purposes.
    /// </summary>
    public class ZoneUndoRecord
    {
        /// <summary>The zone being modified</summary>
        public Zone TargetZone;

        /// <summary>Cells in the zone BEFORE the modification</summary>
        public HashSet<IntVec3> OriginalCells;

        /// <summary>All zones that existed BEFORE the operation (to detect splits)</summary>
        public HashSet<Zone> ZonesBeforeOperation;

        /// <summary>Zones created by CheckContiguous splits (populated after CaptureAfterState)</summary>
        public HashSet<Zone> SplitCreatedZones;

        /// <summary>Whether this is a shrink operation (true = shrink, false = expand)</summary>
        public bool IsShrinkOperation;

        /// <summary>
        /// Creates a new empty undo record.
        /// </summary>
        public ZoneUndoRecord()
        {
            OriginalCells = new HashSet<IntVec3>();
            ZonesBeforeOperation = new HashSet<Zone>();
            SplitCreatedZones = new HashSet<Zone>();
        }
    }

    /// <summary>
    /// Static helper class that tracks zone state for undo operations.
    /// Used by ViewingModeState to allow undoing zone expand/shrink operations with Escape.
    ///
    /// Usage pattern:
    /// 1. CaptureBeforeState() - Called BEFORE DesignateMultiCell
    /// 2. CaptureAfterState() - Called AFTER DesignateMultiCell
    /// 3. AddSegment() - Store the current record as a segment
    /// 4. UndoLastSegment() or UndoAll() - Restore zone to previous state
    /// 5. Clear() - Discard all undo data (on confirm)
    /// </summary>
    public static class ZoneUndoTracker
    {
        // Current working record (being built during placement)
        private static ZoneUndoRecord currentRecord = null;

        // Stack of completed segments for multi-step undo
        private static List<ZoneUndoRecord> segments = new List<ZoneUndoRecord>();

        /// <summary>
        /// Whether there's a pending record being built.
        /// </summary>
        public static bool HasPendingRecord => currentRecord != null;

        /// <summary>
        /// Number of stored segments available for undo.
        /// </summary>
        public static int SegmentCount => segments.Count;

        /// <summary>
        /// Whether the most recent segment was an expansion of an existing zone (vs creating a new one).
        /// Returns true if the last segment had a TargetZone, meaning we added cells to an existing zone.
        /// </summary>
        public static bool WasZoneExpansion
        {
            get
            {
                if (segments.Count == 0)
                    return false;
                return segments[segments.Count - 1].TargetZone != null;
            }
        }

        /// <summary>
        /// Captures the zone state BEFORE a modification operation.
        /// Call this immediately before calling DesignateMultiCell.
        /// </summary>
        /// <param name="targetZone">The zone being modified (can be null for new zone creation)</param>
        /// <param name="map">The current map</param>
        /// <param name="isShrink">True for shrink operations, false for expand</param>
        public static void CaptureBeforeState(Zone targetZone, Map map, bool isShrink)
        {
            currentRecord = new ZoneUndoRecord
            {
                TargetZone = targetZone,
                IsShrinkOperation = isShrink
            };

            // Capture original cells from the target zone
            if (targetZone != null)
            {
                foreach (IntVec3 cell in targetZone.Cells)
                {
                    currentRecord.OriginalCells.Add(cell);
                }
            }

            // Capture all zones that exist before the operation
            if (map?.zoneManager != null)
            {
                foreach (Zone zone in map.zoneManager.AllZones)
                {
                    currentRecord.ZonesBeforeOperation.Add(zone);
                }
            }

            Log.Message($"[ZoneUndoTracker] CaptureBeforeState: zone={targetZone?.label ?? "null"}, " +
                       $"originalCells={currentRecord.OriginalCells.Count}, " +
                       $"existingZones={currentRecord.ZonesBeforeOperation.Count}, " +
                       $"isShrink={isShrink}");
        }

        /// <summary>
        /// Captures the zone state AFTER a modification operation.
        /// Call this immediately after DesignateMultiCell completes.
        /// Detects any new zones created by CheckContiguous splits.
        /// </summary>
        /// <param name="map">The current map</param>
        public static void CaptureAfterState(Map map)
        {
            if (currentRecord == null)
            {
                Log.Warning("[ZoneUndoTracker] CaptureAfterState called without CaptureBeforeState");
                return;
            }

            // Detect split-created zones by comparing before/after zone lists
            if (map?.zoneManager != null)
            {
                foreach (Zone zone in map.zoneManager.AllZones)
                {
                    // If this zone wasn't in the before list, it was created by the operation
                    if (!currentRecord.ZonesBeforeOperation.Contains(zone))
                    {
                        currentRecord.SplitCreatedZones.Add(zone);
                    }
                }
            }

            Log.Message($"[ZoneUndoTracker] CaptureAfterState: splitCreatedZones={currentRecord.SplitCreatedZones.Count}");
        }

        /// <summary>
        /// Stores the current record as a completed segment and starts fresh.
        /// Call this when entering ViewingModeState to commit the pending operation.
        /// </summary>
        public static void AddSegment()
        {
            if (currentRecord == null)
            {
                Log.Message("[ZoneUndoTracker] AddSegment called but no pending record");
                return;
            }

            segments.Add(currentRecord);
            Log.Message($"[ZoneUndoTracker] AddSegment: now have {segments.Count} segments");
            currentRecord = null;
        }

        /// <summary>
        /// Undoes the most recent segment, restoring the zone to its previous state.
        /// </summary>
        /// <param name="map">The current map</param>
        /// <returns>True if a segment was undone, false if no segments available</returns>
        public static bool UndoLastSegment(Map map)
        {
            if (segments.Count == 0)
            {
                Log.Message("[ZoneUndoTracker] UndoLastSegment: no segments to undo");
                return false;
            }

            ZoneUndoRecord record = segments[segments.Count - 1];
            segments.RemoveAt(segments.Count - 1);

            RestoreFromRecord(record, map);
            return true;
        }

        /// <summary>
        /// Undoes all segments in reverse order, fully restoring original zone state.
        /// </summary>
        /// <param name="map">The current map</param>
        /// <returns>Number of segments undone</returns>
        public static int UndoAll(Map map)
        {
            int count = segments.Count;

            // Undo in reverse order (most recent first)
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                RestoreFromRecord(segments[i], map);
            }

            segments.Clear();
            Log.Message($"[ZoneUndoTracker] UndoAll: undid {count} segments");
            return count;
        }

        /// <summary>
        /// Clears all undo data. Call this when confirming changes.
        /// </summary>
        public static void Clear()
        {
            currentRecord = null;
            segments.Clear();
            Log.Message("[ZoneUndoTracker] Cleared all undo data");
        }

        /// <summary>
        /// Checks if the current shrink operation would delete ALL cells from the zone.
        /// Call this BEFORE applying a shrink operation to warn the user.
        /// </summary>
        /// <param name="targetZone">The zone being shrunk</param>
        /// <param name="cellsToRemove">The cells that will be removed</param>
        /// <returns>True if this operation would delete the entire zone</returns>
        public static bool WouldDeleteEntireZone(Zone targetZone, IEnumerable<IntVec3> cellsToRemove)
        {
            if (targetZone == null)
                return false;

            HashSet<IntVec3> zoneCells = new HashSet<IntVec3>(targetZone.Cells);
            HashSet<IntVec3> removeSet = new HashSet<IntVec3>(cellsToRemove);

            // Check if all zone cells would be removed
            foreach (IntVec3 cell in zoneCells)
            {
                if (!removeSet.Contains(cell))
                {
                    // At least one cell would remain
                    return false;
                }
            }

            // All cells would be removed
            return true;
        }

        /// <summary>
        /// Restores a zone to its state captured in the record.
        /// </summary>
        private static void RestoreFromRecord(ZoneUndoRecord record, Map map)
        {
            if (record == null || map == null)
                return;

            Log.Message($"[ZoneUndoTracker] RestoreFromRecord: zone={record.TargetZone?.label ?? "null"}, " +
                       $"originalCells={record.OriginalCells.Count}, " +
                       $"splitZones={record.SplitCreatedZones.Count}");

            // Step 1: Delete all zones created by splits
            foreach (Zone splitZone in record.SplitCreatedZones)
            {
                if (splitZone != null && map.zoneManager.AllZones.Contains(splitZone))
                {
                    Log.Message($"[ZoneUndoTracker] Deleting split zone: {splitZone.label}");
                    splitZone.Delete();
                }
            }

            // Step 2: Restore the target zone's cells
            if (record.TargetZone != null && map.zoneManager.AllZones.Contains(record.TargetZone))
            {
                Zone zone = record.TargetZone;
                HashSet<IntVec3> currentCells = new HashSet<IntVec3>(zone.Cells);

                // Remove cells that weren't in the original set
                foreach (IntVec3 cell in currentCells)
                {
                    if (!record.OriginalCells.Contains(cell))
                    {
                        zone.RemoveCell(cell);
                    }
                }

                // Add back cells that were in the original set but aren't now
                foreach (IntVec3 cell in record.OriginalCells)
                {
                    if (!zone.ContainsCell(cell) && cell.InBounds(map))
                    {
                        zone.AddCell(cell);
                    }
                }

                // Do NOT call CheckContiguous - we want exact restoration without splits
                Log.Message($"[ZoneUndoTracker] Restored zone {zone.label} to {zone.Cells.Count()} cells");
            }
            else if (record.TargetZone != null && !map.zoneManager.AllZones.Contains(record.TargetZone))
            {
                // The target zone was deleted - we can't restore it
                // This case is handled by the full deletion warning
                Log.Warning($"[ZoneUndoTracker] Target zone was deleted and cannot be restored");
            }
        }
    }
}
