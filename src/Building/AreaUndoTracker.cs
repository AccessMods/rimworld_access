using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Tracks area cell changes for undo support.
    /// Captures state before painting, then allows reverting to that state.
    /// </summary>
    public static class AreaUndoTracker
    {
        private static Area targetArea = null;
        private static HashSet<IntVec3> cellsBeforePaint = new HashSet<IntVec3>();
        private static HashSet<IntVec3> cellsAfterPaint = new HashSet<IntVec3>();
        private static bool hasUndoData = false;
        private static bool wasExpanding = true;  // true = expand, false = clear/shrink

        public static bool HasUndoData => hasUndoData;

        /// <summary>
        /// Captures the area state before painting begins.
        /// Call this BEFORE applying any cell changes.
        /// </summary>
        public static void CaptureBeforeState(Area area, bool expanding)
        {
            if (area == null)
                return;

            targetArea = area;
            wasExpanding = expanding;
            cellsBeforePaint.Clear();
            cellsAfterPaint.Clear();

            // Capture all cells currently in the area
            Map map = area.Map;
            if (map != null)
            {
                foreach (IntVec3 cell in map.AllCells)
                {
                    if (area[cell])
                        cellsBeforePaint.Add(cell);
                }
            }

            hasUndoData = false;
        }

        /// <summary>
        /// Captures the area state after painting is complete.
        /// Call this AFTER applying cell changes.
        /// </summary>
        public static void CaptureAfterState()
        {
            if (targetArea == null)
                return;

            cellsAfterPaint.Clear();

            Map map = targetArea.Map;
            if (map != null)
            {
                foreach (IntVec3 cell in map.AllCells)
                {
                    if (targetArea[cell])
                        cellsAfterPaint.Add(cell);
                }
            }

            // Only mark as having undo data if something actually changed
            hasUndoData = !cellsBeforePaint.SetEquals(cellsAfterPaint);
        }

        /// <summary>
        /// Undoes the last area change by restoring the before state.
        /// </summary>
        /// <returns>Number of cells restored</returns>
        public static int Undo()
        {
            if (!hasUndoData || targetArea == null)
                return 0;

            int changedCount = 0;

            if (wasExpanding)
            {
                // We were expanding - find cells that were added and remove them
                var addedCells = cellsAfterPaint.Except(cellsBeforePaint).ToList();
                foreach (IntVec3 cell in addedCells)
                {
                    targetArea[cell] = false;
                    changedCount++;
                }
            }
            else
            {
                // We were clearing/shrinking - find cells that were removed and add them back
                var removedCells = cellsBeforePaint.Except(cellsAfterPaint).ToList();
                foreach (IntVec3 cell in removedCells)
                {
                    targetArea[cell] = true;
                    changedCount++;
                }
            }

            hasUndoData = false;
            return changedCount;
        }

        /// <summary>
        /// Gets a description of what was changed for announcements.
        /// </summary>
        public static string GetChangeDescription()
        {
            if (targetArea == null)
                return "area";

            return targetArea.Label;
        }

        /// <summary>
        /// Clears all undo data.
        /// </summary>
        public static void Clear()
        {
            targetArea = null;
            cellsBeforePaint.Clear();
            cellsAfterPaint.Clear();
            hasUndoData = false;
        }
    }
}
