using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Maintains state for area painting mode (expanding/shrinking areas with keyboard).
    /// Allows keyboard navigation and cell-by-cell painting of areas.
    /// </summary>
    public static class AreaPaintingState
    {
        private static bool isActive = false;
        private static Area targetArea = null;
        private static bool isExpanding = true; // true = expand, false = shrink
        private static List<IntVec3> stagedCells = new List<IntVec3>(); // Cells staged for addition/removal

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
        /// Enters area painting mode for expanding an area.
        /// </summary>
        public static void EnterExpandMode(Area area)
        {
            MelonLoader.MelonLogger.Msg($"RimWorld Access: EnterExpandMode called for area: {area?.Label ?? "null"}");

            isActive = true;
            targetArea = area;
            isExpanding = true;
            stagedCells.Clear();

            MelonLoader.MelonLogger.Msg($"RimWorld Access: isActive set to {isActive}, targetArea set to {targetArea?.Label}");

            // Ensure map navigation is initialized
            if (!MapNavigationState.IsInitialized && area.Map != null)
            {
                MapNavigationState.Initialize(area.Map);
                MelonLoader.MelonLogger.Msg("RimWorld Access: Initialized map navigation");
            }

            ClipboardHelper.CopyToClipboard($"Expanding area: {area.Label}. Press Space to select cells, Enter to confirm, Escape to cancel.");
            MelonLoader.MelonLogger.Msg("RimWorld Access: Area painting mode entered");
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

            // Ensure map navigation is initialized
            if (!MapNavigationState.IsInitialized && area.Map != null)
            {
                MapNavigationState.Initialize(area.Map);
            }

            ClipboardHelper.CopyToClipboard($"Shrinking area: {area.Label}. Press Space to select cells, Enter to confirm, Escape to cancel.");
        }

        /// <summary>
        /// Toggles staging of the cell at the current cursor position.
        /// </summary>
        public static void ToggleStageCell()
        {
            MelonLoader.MelonLogger.Msg($"RimWorld Access: ToggleStageCell called, isActive={isActive}, targetArea={targetArea?.Label ?? "null"}");

            if (!isActive || targetArea == null)
            {
                MelonLoader.MelonLogger.Msg("RimWorld Access: Not active or no target area");
                return;
            }

            IntVec3 currentPos = MapNavigationState.CurrentCursorPosition;
            MelonLoader.MelonLogger.Msg($"RimWorld Access: Current position: {currentPos}");

            if (!currentPos.InBounds(targetArea.Map))
            {
                ClipboardHelper.CopyToClipboard("Position out of bounds");
                MelonLoader.MelonLogger.Msg("RimWorld Access: Position out of bounds");
                return;
            }

            // Toggle staging
            if (stagedCells.Contains(currentPos))
            {
                stagedCells.Remove(currentPos);
                ClipboardHelper.CopyToClipboard($"Deselected, {currentPos.x}, {currentPos.z}");
                MelonLoader.MelonLogger.Msg($"RimWorld Access: Deselected cell at {currentPos}");
            }
            else
            {
                stagedCells.Add(currentPos);
                ClipboardHelper.CopyToClipboard($"Selected, {currentPos.x}, {currentPos.z}");
                MelonLoader.MelonLogger.Msg($"RimWorld Access: Selected cell at {currentPos}");
            }
        }

        /// <summary>
        /// Confirms all staged changes and exits.
        /// </summary>
        public static void Confirm()
        {
            MelonLoader.MelonLogger.Msg("RimWorld Access: Confirm() called");

            if (!isActive || targetArea == null)
            {
                MelonLoader.MelonLogger.Msg("RimWorld Access: Not active or no target area");
                return;
            }

            if (stagedCells.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No cells selected. Area unchanged.");
                MelonLoader.MelonLogger.Msg("RimWorld Access: No selected cells");
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
            ClipboardHelper.CopyToClipboard($"{stagedCells.Count} cells {action} {targetArea.Label}. Total cells: {targetArea.TrueCount}");
            MelonLoader.MelonLogger.Msg($"RimWorld Access: Applied {stagedCells.Count} changes");

            isActive = false;
            targetArea = null;
            stagedCells.Clear();
        }

        /// <summary>
        /// Cancels all staged changes and exits.
        /// </summary>
        public static void Cancel()
        {
            MelonLoader.MelonLogger.Msg("RimWorld Access: Cancel() called");

            if (targetArea != null)
            {
                ClipboardHelper.CopyToClipboard("Area editing cancelled");
            }

            isActive = false;
            targetArea = null;
            stagedCells.Clear();

            MelonLoader.MelonLogger.Msg("RimWorld Access: Area painting cancelled");
        }

        /// <summary>
        /// Exits area painting mode without applying changes.
        /// </summary>
        private static void Exit()
        {
            MelonLoader.MelonLogger.Msg("RimWorld Access: AreaPaintingState.Exit() called");

            isActive = false;
            targetArea = null;
            stagedCells.Clear();

            MelonLoader.MelonLogger.Msg("RimWorld Access: Area painting mode exited");
        }

    }
}
