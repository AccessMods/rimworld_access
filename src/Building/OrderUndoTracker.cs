using System.Collections.Generic;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Records designations created in a single placement operation for undo purposes.
    /// </summary>
    public class OrderUndoRecord
    {
        /// <summary>Designation objects created during this operation</summary>
        public List<Designation> CreatedDesignations;

        /// <summary>
        /// Creates a new empty record.
        /// </summary>
        public OrderUndoRecord()
        {
            CreatedDesignations = new List<Designation>();
        }
    }

    /// <summary>
    /// Static helper class that tracks order designations for undo operations.
    /// Used by ViewingModeState to allow undoing Hunt, Haul, Mine, Tame, etc. with minus key.
    ///
    /// This tracker captures actual Designation objects rather than cells, which is essential
    /// because thing-based designators (Hunt, Haul, Tame) store designations on the Thing itself,
    /// not indexed by cell. When an animal moves, the designation follows it.
    ///
    /// Usage pattern:
    /// 1. CaptureBeforeState() - Called BEFORE DesignateSingleCell/DesignateMultiCell
    /// 2. CaptureAfterState() - Called AFTER designation loop completes
    /// 3. AddSegment() - Store the current record as a segment
    /// 4. UndoLastSegment() or UndoAll() - Remove designations from DesignationManager
    /// 5. Clear() - Discard all undo data (on confirm)
    /// </summary>
    public static class OrderUndoTracker
    {
        // Snapshot of all designations before the operation
        private static HashSet<Designation> designationsBeforeOperation = new HashSet<Designation>();

        // Current working record (being built during placement)
        private static OrderUndoRecord currentRecord = null;

        // Stack of completed segments for multi-step undo
        private static List<OrderUndoRecord> segments = new List<OrderUndoRecord>();

        // Count of designations removed in last undo operation
        private static int lastRemovedCount = 0;

        /// <summary>
        /// Whether there's a pending record being built.
        /// </summary>
        public static bool HasPendingRecord => currentRecord != null && currentRecord.CreatedDesignations.Count > 0;

        /// <summary>
        /// Number of stored segments available for undo.
        /// </summary>
        public static int SegmentCount => segments.Count;

        /// <summary>
        /// Number of designations removed in the last UndoLastSegment call.
        /// </summary>
        public static int LastRemovedCount => lastRemovedCount;

        /// <summary>
        /// Captures all existing designations BEFORE placement.
        /// Creates a snapshot that will be compared against after placement.
        /// </summary>
        /// <param name="map">The current map</param>
        public static void CaptureBeforeState(Map map)
        {
            designationsBeforeOperation.Clear();
            currentRecord = null;

            if (map?.designationManager == null)
                return;

            // Snapshot all current designations
            foreach (Designation des in map.designationManager.AllDesignations)
            {
                designationsBeforeOperation.Add(des);
            }

            Log.Message($"[OrderUndoTracker] CaptureBeforeState: {designationsBeforeOperation.Count} existing designations");
        }

        /// <summary>
        /// Captures new designations AFTER placement by comparing to before state.
        /// Any designation that exists now but didn't before was created by this operation.
        /// </summary>
        /// <param name="map">The current map</param>
        public static void CaptureAfterState(Map map)
        {
            currentRecord = new OrderUndoRecord();

            if (map?.designationManager == null)
                return;

            // Find designations that were added (exist now but didn't before)
            foreach (Designation des in map.designationManager.AllDesignations)
            {
                if (!designationsBeforeOperation.Contains(des))
                {
                    currentRecord.CreatedDesignations.Add(des);
                }
            }

            Log.Message($"[OrderUndoTracker] CaptureAfterState: {currentRecord.CreatedDesignations.Count} new designations captured");
        }

        /// <summary>
        /// Stores the current record as a completed segment and starts fresh.
        /// Call this when entering ViewingModeState to commit the pending operation.
        /// </summary>
        public static void AddSegment()
        {
            if (currentRecord == null)
            {
                Log.Message("[OrderUndoTracker] AddSegment called but no pending record");
                return;
            }

            segments.Add(currentRecord);
            Log.Message($"[OrderUndoTracker] AddSegment: now have {segments.Count} segments");
            currentRecord = null;
            designationsBeforeOperation.Clear();
        }

        /// <summary>
        /// Undoes the most recent segment by removing its designations from the map.
        /// Handles cases where Things have been destroyed (designations auto-removed).
        /// </summary>
        /// <param name="map">The current map</param>
        /// <returns>Number of designations actually removed</returns>
        public static int UndoLastSegment(Map map)
        {
            lastRemovedCount = 0;

            if (segments.Count == 0)
            {
                Log.Message("[OrderUndoTracker] UndoLastSegment: no segments to undo");
                return 0;
            }

            if (map?.designationManager == null)
            {
                Log.Warning("[OrderUndoTracker] UndoLastSegment: no map or designation manager");
                return 0;
            }

            OrderUndoRecord record = segments[segments.Count - 1];
            segments.RemoveAt(segments.Count - 1);

            lastRemovedCount = RemoveDesignationsFromRecord(record, map);
            Log.Message($"[OrderUndoTracker] UndoLastSegment: removed {lastRemovedCount} designations, {segments.Count} segments remaining");

            return lastRemovedCount;
        }

        /// <summary>
        /// Undoes all segments in reverse order.
        /// </summary>
        /// <param name="map">The current map</param>
        /// <returns>Total number of designations removed</returns>
        public static int UndoAll(Map map)
        {
            if (map?.designationManager == null)
            {
                Log.Warning("[OrderUndoTracker] UndoAll: no map or designation manager");
                return 0;
            }

            int totalRemoved = 0;

            // Undo in reverse order (most recent first)
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                totalRemoved += RemoveDesignationsFromRecord(segments[i], map);
            }

            segments.Clear();
            lastRemovedCount = totalRemoved;
            Log.Message($"[OrderUndoTracker] UndoAll: removed {totalRemoved} designations total");

            return totalRemoved;
        }

        /// <summary>
        /// Removes designations from a record, handling destroyed Things gracefully.
        /// If a Thing was destroyed (e.g., animal died), its designation is automatically
        /// removed by RimWorld, so we just skip it.
        /// </summary>
        /// <param name="record">The undo record containing designations to remove</param>
        /// <param name="map">The current map</param>
        /// <returns>Number of designations actually removed</returns>
        private static int RemoveDesignationsFromRecord(OrderUndoRecord record, Map map)
        {
            int removedCount = 0;
            DesignationManager dm = map.designationManager;

            foreach (Designation des in record.CreatedDesignations)
            {
                // Check if designation still exists in the manager
                // It may have been auto-removed if the target Thing was destroyed
                if (dm.AllDesignations.Contains(des))
                {
                    dm.RemoveDesignation(des);
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Clears all undo data. Call this when confirming changes or exiting.
        /// </summary>
        public static void Clear()
        {
            currentRecord = null;
            segments.Clear();
            designationsBeforeOperation.Clear();
            lastRemovedCount = 0;
            Log.Message("[OrderUndoTracker] Cleared all undo data");
        }
    }
}
