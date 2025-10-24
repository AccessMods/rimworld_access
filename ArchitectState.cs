using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Defines the modes of the architect system.
    /// </summary>
    public enum ArchitectMode
    {
        Inactive,           // Not in architect mode
        CategorySelection,  // Selecting a category (Orders, Structure, etc.)
        ToolSelection,      // Selecting a tool within a category
        MaterialSelection,  // Selecting material for construction
        PlacementMode       // Placing designations on the map
    }

    /// <summary>
    /// Maintains state for the accessible architect system.
    /// Tracks current mode, selected category, designator, and placement state.
    /// </summary>
    public static class ArchitectState
    {
        private static ArchitectMode currentMode = ArchitectMode.Inactive;
        private static DesignationCategoryDef selectedCategory = null;
        private static Designator selectedDesignator = null;
        private static BuildableDef selectedBuildable = null;
        private static ThingDef selectedMaterial = null;
        private static List<IntVec3> selectedCells = new List<IntVec3>();

        /// <summary>
        /// Gets the current architect mode.
        /// </summary>
        public static ArchitectMode CurrentMode => currentMode;

        /// <summary>
        /// Gets the currently selected category.
        /// </summary>
        public static DesignationCategoryDef SelectedCategory => selectedCategory;

        /// <summary>
        /// Gets the currently selected designator.
        /// </summary>
        public static Designator SelectedDesignator => selectedDesignator;

        /// <summary>
        /// Gets the currently selected buildable (for construction).
        /// </summary>
        public static BuildableDef SelectedBuildable => selectedBuildable;

        /// <summary>
        /// Gets the currently selected material (for construction).
        /// </summary>
        public static ThingDef SelectedMaterial => selectedMaterial;

        /// <summary>
        /// Gets the list of selected cells for placement.
        /// </summary>
        public static List<IntVec3> SelectedCells => selectedCells;

        /// <summary>
        /// Whether architect mode is currently active (any mode except Inactive).
        /// </summary>
        public static bool IsActive => currentMode != ArchitectMode.Inactive;

        /// <summary>
        /// Whether we're currently in placement mode on the map.
        /// </summary>
        public static bool IsInPlacementMode => currentMode == ArchitectMode.PlacementMode;

        /// <summary>
        /// Enters category selection mode.
        /// </summary>
        public static void EnterCategorySelection()
        {
            currentMode = ArchitectMode.CategorySelection;
            selectedCategory = null;
            selectedDesignator = null;
            selectedBuildable = null;
            selectedMaterial = null;
            selectedCells.Clear();

            ClipboardHelper.CopyToClipboard("Architect menu opened. Select a category");
            MelonLoader.MelonLogger.Msg("Entered architect category selection");
        }

        /// <summary>
        /// Enters tool selection mode for a specific category.
        /// </summary>
        public static void EnterToolSelection(DesignationCategoryDef category)
        {
            currentMode = ArchitectMode.ToolSelection;
            selectedCategory = category;
            selectedDesignator = null;
            selectedBuildable = null;
            selectedMaterial = null;
            selectedCells.Clear();

            ClipboardHelper.CopyToClipboard($"{category.LabelCap} category selected. Choose a tool");
            MelonLoader.MelonLogger.Msg($"Entered tool selection for category: {category.defName}");
        }

        /// <summary>
        /// Enters material selection mode for a buildable that requires stuff.
        /// </summary>
        public static void EnterMaterialSelection(BuildableDef buildable, Designator designator)
        {
            currentMode = ArchitectMode.MaterialSelection;
            selectedBuildable = buildable;
            selectedDesignator = designator;
            selectedMaterial = null;
            selectedCells.Clear();

            ClipboardHelper.CopyToClipboard($"Select material for {buildable.label}");
            MelonLoader.MelonLogger.Msg($"Entered material selection for: {buildable.defName}");
        }

        /// <summary>
        /// Enters placement mode with the selected designator.
        /// </summary>
        public static void EnterPlacementMode(Designator designator, ThingDef material = null)
        {
            currentMode = ArchitectMode.PlacementMode;
            selectedDesignator = designator;
            selectedMaterial = material;
            selectedCells.Clear();

            // Set the designator as selected in the game's DesignatorManager
            if (Find.DesignatorManager != null)
            {
                Find.DesignatorManager.Select(designator);
            }

            string toolName = designator.Label;
            ClipboardHelper.CopyToClipboard($"{toolName} selected. Press Space to designate tiles, Enter to confirm, Escape to cancel");
            MelonLoader.MelonLogger.Msg($"Entered placement mode with designator: {toolName}");
        }

        /// <summary>
        /// Adds a cell to the selection if valid for the current designator.
        /// </summary>
        public static void ToggleCell(IntVec3 cell)
        {
            if (selectedDesignator == null)
                return;

            // Check if this designator can designate this cell
            AcceptanceReport report = selectedDesignator.CanDesignateCell(cell);

            if (selectedCells.Contains(cell))
            {
                // Remove cell
                selectedCells.Remove(cell);
                ClipboardHelper.CopyToClipboard($"Deselected, {cell.x}, {cell.z}");
            }
            else if (report.Accepted)
            {
                // Add cell
                selectedCells.Add(cell);
                ClipboardHelper.CopyToClipboard($"Selected, {cell.x}, {cell.z}");
            }
            else
            {
                // Cannot designate this cell
                string reason = report.Reason ?? "Cannot designate here";
                ClipboardHelper.CopyToClipboard($"Invalid: {reason}");
            }
        }

        /// <summary>
        /// Executes the placement (designates all selected cells).
        /// </summary>
        public static void ExecutePlacement(Map map)
        {
            if (selectedDesignator == null || selectedCells.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No tiles selected");
                Cancel();
                return;
            }

            try
            {
                // Use the designator's DesignateMultiCell method
                selectedDesignator.DesignateMultiCell(selectedCells);

                string toolName = selectedDesignator.Label;
                ClipboardHelper.CopyToClipboard($"{toolName} placed on {selectedCells.Count} tiles");
                MelonLoader.MelonLogger.Msg($"Executed placement: {toolName} on {selectedCells.Count} tiles");
            }
            catch (System.Exception ex)
            {
                ClipboardHelper.CopyToClipboard($"Error placing designation: {ex.Message}");
                MelonLoader.MelonLogger.Error($"Error in ExecutePlacement: {ex}");
            }
            finally
            {
                Reset();
            }
        }

        /// <summary>
        /// Cancels the current operation and returns to the previous state or exits.
        /// </summary>
        public static void Cancel()
        {
            if (currentMode == ArchitectMode.PlacementMode)
            {
                // Return to category selection
                ClipboardHelper.CopyToClipboard("Placement cancelled");
                EnterCategorySelection();
            }
            else
            {
                // Exit architect mode entirely
                ClipboardHelper.CopyToClipboard("Architect menu closed");
                Reset();
            }
        }

        /// <summary>
        /// Resets the architect state completely.
        /// </summary>
        public static void Reset()
        {
            currentMode = ArchitectMode.Inactive;
            selectedCategory = null;
            selectedDesignator = null;
            selectedBuildable = null;
            selectedMaterial = null;
            selectedCells.Clear();

            // Deselect any active designator in the game
            if (Find.DesignatorManager != null)
            {
                Find.DesignatorManager.Deselect();
            }

            MelonLoader.MelonLogger.Msg("Architect state reset");
        }
    }
}
