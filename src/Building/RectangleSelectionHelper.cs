using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Shared helper class for rectangle-based selection operations.
    /// Used by ZoneCreationState, AreaPaintingState, and ArchitectState to provide
    /// consistent rectangle selection behavior with native RimWorld sound feedback.
    /// </summary>
    public class RectangleSelectionHelper
    {
        /// <summary>
        /// Threshold for announcing dimensions (width or height must be >= this value).
        /// Matches native DesignationDragger behavior.
        /// </summary>
        public const int DIMENSION_ANNOUNCEMENT_THRESHOLD = 5;

        private IntVec3? rectangleStart = null;
        private IntVec3? rectangleEnd = null;
        private List<IntVec3> previewCells = new List<IntVec3>();
        private int lastCellCount = 0;
        private float lastDragRealTime = 0f;

        /// <summary>
        /// Whether a rectangle start corner has been set.
        /// </summary>
        public bool HasRectangleStart => rectangleStart.HasValue;

        /// <summary>
        /// Whether we are actively previewing a rectangle (start and end set).
        /// </summary>
        public bool IsInPreviewMode => rectangleStart.HasValue && rectangleEnd.HasValue;

        /// <summary>
        /// The start corner of the rectangle being selected.
        /// </summary>
        public IntVec3? RectangleStart => rectangleStart;

        /// <summary>
        /// The end corner of the rectangle being selected.
        /// </summary>
        public IntVec3? RectangleEnd => rectangleEnd;

        /// <summary>
        /// Cells in the current rectangle preview.
        /// </summary>
        public IReadOnlyList<IntVec3> PreviewCells => previewCells;

        /// <summary>
        /// The last cell count (for tracking changes).
        /// </summary>
        public int LastCellCount => lastCellCount;

        /// <summary>
        /// Resets the rectangle selection state.
        /// </summary>
        public void Reset()
        {
            rectangleStart = null;
            rectangleEnd = null;
            previewCells.Clear();
            lastCellCount = 0;
            lastDragRealTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Sets the start corner for rectangle selection.
        /// </summary>
        public void SetStart(IntVec3 cell)
        {
            rectangleStart = cell;
            rectangleEnd = null;
            previewCells.Clear();
            lastCellCount = 0;
            lastDragRealTime = Time.realtimeSinceStartup;
            TolkHelper.Speak($"Start at {cell.x}, {cell.z}");
        }

        /// <summary>
        /// Updates the rectangle preview as the cursor moves.
        /// Plays native sound feedback when cell count changes.
        /// </summary>
        /// <returns>True if the cell count changed, false otherwise.</returns>
        public bool UpdatePreview(IntVec3 endCell)
        {
            if (!rectangleStart.HasValue) return false;

            rectangleEnd = endCell;

            // Use native CellRect API for rectangle calculation
            CellRect rect = CellRect.FromLimits(rectangleStart.Value, endCell);
            previewCells = rect.Cells.ToList();

            int width = rect.Width;
            int height = rect.Height;
            int cellCount = previewCells.Count;

            // Play native sound when cell count changes (matches DesignationDragger behavior)
            if (cellCount != lastCellCount)
            {
                SoundInfo info = SoundInfo.OnCamera();
                info.SetParameter("TimeSinceDrag", Time.realtimeSinceStartup - lastDragRealTime);
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShot(info);
                lastDragRealTime = Time.realtimeSinceStartup;
                lastCellCount = cellCount;

                // Announce dimensions (matches native display behavior)
                if (width >= DIMENSION_ANNOUNCEMENT_THRESHOLD || height >= DIMENSION_ANNOUNCEMENT_THRESHOLD)
                {
                    TolkHelper.Speak($"{width} by {height}", SpeechPriority.Low);
                }
                else if (cellCount >= 4)
                {
                    TolkHelper.Speak($"{cellCount}", SpeechPriority.Low);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Confirms the current rectangle preview, returning cells to add to a target collection.
        /// </summary>
        /// <param name="existingCells">Existing cells to check for duplicates.</param>
        /// <returns>The number of new cells added, and the cells themselves via out parameter.</returns>
        public int ConfirmRectangle(ICollection<IntVec3> existingCells, out List<IntVec3> newCells)
        {
            newCells = new List<IntVec3>();

            if (!IsInPreviewMode)
            {
                TolkHelper.Speak("No rectangle to confirm");
                return 0;
            }

            // Find cells that aren't already in the collection
            int addedCount = 0;
            foreach (var cell in previewCells.Where(cell => !existingCells.Contains(cell)))
            {
                newCells.Add(cell);
                addedCount++;
            }

            CellRect rect = CellRect.FromLimits(rectangleStart.Value, rectangleEnd.Value);
            int totalAfterAdd = existingCells.Count + addedCount;
            TolkHelper.Speak($"{rect.Width} by {rect.Height}, {addedCount} cells added. Total: {totalAfterAdd}");

            // Clear rectangle state for next selection
            rectangleStart = null;
            rectangleEnd = null;
            previewCells.Clear();
            lastCellCount = 0;

            return addedCount;
        }

        /// <summary>
        /// Cancels the current rectangle selection without adding cells.
        /// </summary>
        /// <returns>True if there was a rectangle to cancel, false otherwise.</returns>
        public bool Cancel()
        {
            if (!HasRectangleStart)
            {
                return false;
            }

            rectangleStart = null;
            rectangleEnd = null;
            previewCells.Clear();
            lastCellCount = 0;
            TolkHelper.Speak("Rectangle cancelled");
            return true;
        }

        /// <summary>
        /// Gets the current rectangle dimensions.
        /// </summary>
        public (int width, int height) GetDimensions()
        {
            if (!IsInPreviewMode)
                return (0, 0);

            CellRect rect = CellRect.FromLimits(rectangleStart.Value, rectangleEnd.Value);
            return (rect.Width, rect.Height);
        }
    }
}
