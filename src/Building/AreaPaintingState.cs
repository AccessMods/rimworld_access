using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Defines the selection mode for area painting.
    /// </summary>
    public enum AreaSelectionMode
    {
        BoxSelection,    // Space sets corners for rectangle selection
        SingleTile       // Space toggles individual tiles
    }

    /// <summary>
    /// Maintains state for area painting mode (expanding/shrinking areas with keyboard).
    /// Allows keyboard navigation and rectangle-based painting of areas.
    /// Uses RimWorld's native APIs for feedback.
    /// </summary>
    public static class AreaPaintingState
    {
        private static bool isActive = false;
        private static Area targetArea = null;
        private static bool isExpanding = true; // true = expand, false = shrink
        private static List<IntVec3> stagedCells = new List<IntVec3>(); // Cells staged for addition/removal
        private static AreaSelectionMode selectionMode = AreaSelectionMode.BoxSelection; // Default to box selection

        // Rectangle selection helper (shared logic for rectangle-based selection)
        private static readonly RectangleSelectionHelper rectangleHelper = new RectangleSelectionHelper();

        /// <summary>
        /// Whether area painting mode is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// The area being painted.
        /// </summary>
        public static Area TargetArea => targetArea;

        /// <summary>
        /// Whether we're expanding (true) or shrinking (false) the area.
        /// </summary>
        public static bool IsExpanding => isExpanding;

        /// <summary>
        /// List of cells staged for addition/removal.
        /// </summary>
        public static List<IntVec3> StagedCells => stagedCells;

        /// <summary>
        /// Whether a rectangle start corner has been set.
        /// </summary>
        public static bool HasRectangleStart => rectangleHelper.HasRectangleStart;

        /// <summary>
        /// Whether we are actively previewing a rectangle (start and end set).
        /// </summary>
        public static bool IsInPreviewMode => rectangleHelper.IsInPreviewMode;

        /// <summary>
        /// The start corner of the rectangle being selected.
        /// </summary>
        public static IntVec3? RectangleStart => rectangleHelper.RectangleStart;

        /// <summary>
        /// The end corner of the rectangle being selected.
        /// </summary>
        public static IntVec3? RectangleEnd => rectangleHelper.RectangleEnd;

        /// <summary>
        /// Cells in the current rectangle preview.
        /// </summary>
        public static IReadOnlyList<IntVec3> PreviewCells => rectangleHelper.PreviewCells;

        /// <summary>
        /// Gets the current selection mode (BoxSelection or SingleTile).
        /// </summary>
        public static AreaSelectionMode SelectionMode => selectionMode;

        /// <summary>
        /// Toggles between box selection and single tile selection modes.
        /// </summary>
        public static void ToggleSelectionMode()
        {
            selectionMode = (selectionMode == AreaSelectionMode.BoxSelection)
                ? AreaSelectionMode.SingleTile
                : AreaSelectionMode.BoxSelection;

            string modeName = (selectionMode == AreaSelectionMode.BoxSelection)
                ? "Box selection mode"
                : "Single tile selection mode";
            TolkHelper.Speak(modeName);
            Log.Message($"Area painting: Switched to {modeName}");
        }

        /// <summary>
        /// Enters area painting mode for expanding an area.
        /// </summary>
        public static void EnterExpandMode(Area area)
        {
            Log.Message($"RimWorld Access: EnterExpandMode called for area: {area?.Label ?? "null"}");

            isActive = true;
            targetArea = area;
            isExpanding = true;
            stagedCells.Clear();
            rectangleHelper.Reset();
            selectionMode = AreaSelectionMode.BoxSelection; // Default to box selection

            Log.Message($"RimWorld Access: isActive set to {isActive}, targetArea set to {targetArea?.Label}");

            // Ensure map navigation is initialized
            if (!MapNavigationState.IsInitialized && area.Map != null)
            {
                MapNavigationState.Initialize(area.Map);
                Log.Message("RimWorld Access: Initialized map navigation");
            }

            TolkHelper.Speak($"Expanding area: {area.Label}. Box selection mode. Tab to switch. Enter to confirm, Escape to cancel.");
            Log.Message("RimWorld Access: Area painting mode entered");
        }

        /// <summary>
        /// Enters area painting mode for shrinking an area.
        /// </summary>
        public static void EnterShrinkMode(Area area)
        {
            isActive = true;
            targetArea = area;
            isExpanding = false;
            stagedCells.Clear();
            rectangleHelper.Reset();
            selectionMode = AreaSelectionMode.BoxSelection; // Default to box selection

            // Ensure map navigation is initialized
            if (!MapNavigationState.IsInitialized && area.Map != null)
            {
                MapNavigationState.Initialize(area.Map);
            }

            TolkHelper.Speak($"Shrinking area: {area.Label}. Box selection mode. Tab to switch. Enter to confirm, Escape to cancel.");
        }

        /// <summary>
        /// Sets the start corner for rectangle selection.
        /// </summary>
        public static void SetRectangleStart(IntVec3 cell)
        {
            rectangleHelper.SetStart(cell);
        }

        /// <summary>
        /// Updates the rectangle preview as the cursor moves.
        /// Plays native sound feedback when cell count changes.
        /// </summary>
        public static void UpdatePreview(IntVec3 endCell)
        {
            rectangleHelper.UpdatePreview(endCell);
        }

        /// <summary>
        /// Confirms the current rectangle preview, adding all cells to staged list.
        /// </summary>
        public static void ConfirmRectangle()
        {
            rectangleHelper.ConfirmRectangle(stagedCells, out var newCells);
            foreach (var cell in newCells)
            {
                stagedCells.Add(cell);
            }
        }

        /// <summary>
        /// Cancels the current rectangle selection without adding cells.
        /// </summary>
        public static void CancelRectangle()
        {
            rectangleHelper.Cancel();
        }

        /// <summary>
        /// Toggles staging of the cell at the current cursor position.
        /// </summary>
        public static void ToggleStageCell()
        {
            Log.Message($"RimWorld Access: ToggleStageCell called, isActive={isActive}, targetArea={targetArea?.Label ?? "null"}");

            if (!isActive || targetArea == null)
            {
                Log.Message("RimWorld Access: Not active or no target area");
                return;
            }

            IntVec3 currentPos = MapNavigationState.CurrentCursorPosition;
            Log.Message($"RimWorld Access: Current position: {currentPos}");

            if (!currentPos.InBounds(targetArea.Map))
            {
                TolkHelper.Speak("Position out of bounds");
                Log.Message("RimWorld Access: Position out of bounds");
                return;
            }

            // Toggle staging
            if (stagedCells.Contains(currentPos))
            {
                stagedCells.Remove(currentPos);
                TolkHelper.Speak($"Deselected, {currentPos.x}, {currentPos.z}");
                Log.Message($"RimWorld Access: Deselected cell at {currentPos}");
            }
            else
            {
                stagedCells.Add(currentPos);
                TolkHelper.Speak($"Selected, {currentPos.x}, {currentPos.z}");
                Log.Message($"RimWorld Access: Selected cell at {currentPos}");
            }
        }

        /// <summary>
        /// Confirms all staged changes and exits.
        /// </summary>
        public static void Confirm()
        {
            Log.Message("RimWorld Access: Confirm() called");

            if (!isActive || targetArea == null)
            {
                Log.Message("RimWorld Access: Not active or no target area");
                return;
            }

            if (stagedCells.Count == 0)
            {
                TolkHelper.Speak("No cells selected. Area unchanged.");
                Log.Message("RimWorld Access: No selected cells");
                Exit();
                return;
            }

            // Apply all staged changes
            foreach (IntVec3 cell in stagedCells)
            {
                if (cell.InBounds(targetArea.Map))
                {
                    if (isExpanding)
                    {
                        targetArea[cell] = true;
                    }
                    else
                    {
                        targetArea[cell] = false;
                    }
                }
            }

            string action = isExpanding ? "added to" : "removed from";
            TolkHelper.Speak($"{stagedCells.Count} cells {action} {targetArea.Label}. Total cells: {targetArea.TrueCount}");
            Log.Message($"RimWorld Access: Applied {stagedCells.Count} changes");

            isActive = false;
            targetArea = null;
            stagedCells.Clear();
            rectangleHelper.Reset();
            selectionMode = AreaSelectionMode.BoxSelection; // Reset to default mode
        }

        /// <summary>
        /// Cancels all staged changes and exits.
        /// </summary>
        public static void Cancel()
        {
            Log.Message("RimWorld Access: Cancel() called");

            if (targetArea != null)
            {
                TolkHelper.Speak("Area editing cancelled");
            }

            isActive = false;
            targetArea = null;
            stagedCells.Clear();
            rectangleHelper.Reset();
            selectionMode = AreaSelectionMode.BoxSelection; // Reset to default mode

            Log.Message("RimWorld Access: Area painting cancelled");
        }

        /// <summary>
        /// Exits area painting mode without applying changes.
        /// </summary>
        private static void Exit()
        {
            Log.Message("RimWorld Access: AreaPaintingState.Exit() called");

            isActive = false;
            targetArea = null;
            stagedCells.Clear();
            rectangleHelper.Reset();
            selectionMode = AreaSelectionMode.BoxSelection; // Reset to default mode

            Log.Message("RimWorld Access: Area painting mode exited");
        }

    }
}
