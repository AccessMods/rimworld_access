using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;
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
    /// Defines the selection mode for architect placement.
    /// </summary>
    public enum ArchitectSelectionMode
    {
        BoxSelection,    // Space sets corners for rectangle selection
        SingleTile       // Space toggles individual tiles
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
        private static Rot4 currentRotation = Rot4.North;
        private static ArchitectSelectionMode selectionMode = ArchitectSelectionMode.BoxSelection; // Default to box selection

        // Reflection field info for accessing protected placingRot field
        private static FieldInfo placingRotField = AccessTools.Field(typeof(Designator_Place), "placingRot");

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
        /// Gets or sets the current rotation for building placement.
        /// </summary>
        public static Rot4 CurrentRotation
        {
            get => currentRotation;
            set => currentRotation = value;
        }

        /// <summary>
        /// Whether architect mode is currently active (any mode except Inactive).
        /// </summary>
        public static bool IsActive => currentMode != ArchitectMode.Inactive;

        /// <summary>
        /// Whether we're currently in placement mode on the map.
        /// </summary>
        public static bool IsInPlacementMode => currentMode == ArchitectMode.PlacementMode;

        /// <summary>
        /// Gets the current selection mode (BoxSelection or SingleTile).
        /// </summary>
        public static ArchitectSelectionMode SelectionMode => selectionMode;

        /// <summary>
        /// Toggles between box selection and single tile selection modes.
        /// Only applies to zone designators.
        /// </summary>
        public static void ToggleSelectionMode()
        {
            // Only allow toggling for zone designators
            if (!IsZoneDesignator())
            {
                TolkHelper.Speak("Selection mode toggle only works for zone designators");
                return;
            }

            selectionMode = (selectionMode == ArchitectSelectionMode.BoxSelection)
                ? ArchitectSelectionMode.SingleTile
                : ArchitectSelectionMode.BoxSelection;

            string modeName = (selectionMode == ArchitectSelectionMode.BoxSelection)
                ? "Box selection mode"
                : "Single tile selection mode";
            TolkHelper.Speak(modeName);
            Log.Message($"Architect placement: Switched to {modeName}");
        }

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

            Log.Message("Entered architect category selection");
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

            TolkHelper.Speak($"{category.LabelCap} category selected. Choose a tool");
            Log.Message($"Entered tool selection for category: {category.defName}");
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

            TolkHelper.Speak($"Select material for {buildable.label}");
            Log.Message($"Entered material selection for: {buildable.defName}");
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

            // Reset rotation to North when entering placement mode
            currentRotation = Rot4.North;

            // Reset selection mode to box selection
            selectionMode = ArchitectSelectionMode.BoxSelection;

            // Set the designator as selected in the game's DesignatorManager
            if (Find.DesignatorManager != null)
            {
                Find.DesignatorManager.Select(designator);
            }

            // If this is a place designator (build or install), set its rotation via reflection
            if (designator is Designator_Place placeDesignator)
            {
                if (placingRotField != null)
                {
                    placingRotField.SetValue(placeDesignator, currentRotation);
                }
            }

            string toolName = designator.Label;

            // Check if this designator supports shapes - if so, we'll skip manual mode announcement
            // and let ShapePlacementState.Enter() handle the announcement
            bool supportsShapes = false;
            if (designator is Designator_Build)
            {
                var availableShapes = ShapeHelper.GetAvailableShapes(designator);
                supportsShapes = availableShapes.Count > 0;
            }

            // Only announce manual mode if shapes are NOT available
            // This prevents double-announcement (manual mode + shape mode)
            if (!supportsShapes)
            {
                string announcement = GetPlacementAnnouncement(designator);
                TolkHelper.Speak(announcement);
            }
            Log.Message($"Entered placement mode with designator: {toolName}");

            // Auto-enter shape placement mode if the designator supports shapes
            if (supportsShapes)
            {
                var availableShapes = ShapeHelper.GetAvailableShapes(designator);
                // Use the game's default shape (first in the list)
                ShapeType defaultShape = availableShapes[0];
                ShapePlacementState.Enter(designator, defaultShape);
                Log.Message($"Auto-entered shape placement with default shape: {defaultShape}");
            }
        }

        /// <summary>
        /// Rotates the current building clockwise.
        /// Works with both Designator_Build (new construction) and Designator_Install (reinstall/install).
        /// </summary>
        public static void RotateBuilding()
        {
            if (!IsInPlacementMode || !(selectedDesignator is Designator_Place placeDesignator))
                return;

            // Rotate clockwise
            currentRotation.Rotate(RotationDirection.Clockwise);

            // Set rotation on the designator via reflection
            if (placingRotField != null)
            {
                placingRotField.SetValue(placeDesignator, currentRotation);
            }

            // Announce new rotation and spatial info
            string announcement = GetRotationAnnouncementForDef(placeDesignator.PlacingDef, currentRotation);
            TolkHelper.Speak(announcement);
            Log.Message($"Rotated building to: {currentRotation}");
        }

        /// <summary>
        /// Gets the initial placement announcement including size and rotation info.
        /// Works with all designator types: Build, Orders, Zones, etc.
        /// </summary>
        private static string GetPlacementAnnouncement(Designator designator)
        {
            // Handle Designator_Place (parent of both Designator_Build and Designator_Install)
            if (!(designator is Designator_Place placeDesignator))
            {
                // For non-Place designators (Orders, Zones, Cells), check if shapes are available
                var availableShapes = ShapeHelper.GetAvailableShapes(designator);
                bool hasShapes = availableShapes.Count > 0;

                if (hasShapes)
                {
                    return $"{designator.Label} selected. Press Space to designate, Tab for shapes, Enter to confirm, Escape to cancel";
                }
                return $"{designator.Label} selected. Press Space to designate tiles, Enter to confirm, Escape to cancel";
            }

            BuildableDef entDef = placeDesignator.PlacingDef;
            if (entDef == null)
            {
                return $"{designator.Label} selected. Press Space to place, R to rotate, Escape to cancel";
            }

            IntVec2 size = entDef.Size;

            string sizeInfo = GetSizeDescription(size, currentRotation);
            string specialRequirements = GetSpecialSpatialRequirements(entDef, currentRotation);
            string controlInfo = "Press Space to place, R to rotate, Escape to cancel";

            if (!string.IsNullOrEmpty(specialRequirements))
            {
                return $"{designator.Label} selected. {sizeInfo}. {specialRequirements}. {controlInfo}";
            }

            return $"{designator.Label} selected. {sizeInfo}. {controlInfo}";
        }

        /// <summary>
        /// Gets rotation announcement including size and spatial requirements.
        /// </summary>
        private static string GetRotationAnnouncement(Designator_Build buildDesignator)
        {
            return GetRotationAnnouncementForDef(buildDesignator.PlacingDef, currentRotation);
        }

        /// <summary>
        /// Gets rotation announcement for any BuildableDef at a given rotation.
        /// Shared by architect menu placement and gizmo-based placement (reinstall, etc.)
        /// </summary>
        internal static string GetRotationAnnouncementForDef(BuildableDef def, Rot4 rotation)
        {
            if (def == null)
                return $"Facing {GetRotationName(rotation)}";

            IntVec2 size = def.Size;
            string sizeInfo = GetSizeDescription(size, rotation);
            string rotationName = GetRotationName(rotation);
            string specialRequirements = GetSpecialSpatialRequirements(def, rotation);

            if (!string.IsNullOrEmpty(specialRequirements))
            {
                return $"Facing {rotationName}. {sizeInfo}. {specialRequirements}";
            }

            return $"Facing {rotationName}. {sizeInfo}";
        }

        /// <summary>
        /// Gets special spatial requirements for buildings like wind turbines, coolers,
        /// beds (head position), fuel ports, TVs, and interaction cell buildings.
        /// </summary>
        private static string GetSpecialSpatialRequirements(BuildableDef def, Rot4 rotation)
        {
            if (def == null || !(def is ThingDef thingDef))
                return null;

            // Check for wind turbine (highest priority - needs clear space)
            // Uses PlaceWorker_WindTurbine check for proper game integration
            if (IsWindTurbine(thingDef))
            {
                return GetWindTurbineRequirements(rotation);
            }

            // Use BuildingCellHelper for all other special position info
            // (coolers, beds, fuel ports, TVs, vents, thrones, turrets, etc.)
            return BuildingCellHelper.GetPlacementPositionInfo(thingDef, rotation);
        }

        /// <summary>
        /// Checks if a ThingDef is a wind turbine by looking for PlaceWorker_WindTurbine.
        /// This is the proper game API for identifying wind turbines.
        /// </summary>
        private static bool IsWindTurbine(ThingDef def)
        {
            if (def?.placeWorkers == null)
                return false;

            foreach (var workerType in def.placeWorkers)
            {
                if (workerType == typeof(PlaceWorker_WindTurbine))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets spatial requirements for wind turbines.
        /// Verified against WindTurbineUtility.CalculateWindCells() in game code:
        /// - North/East facing: 9 tiles front, 5 tiles back
        /// - South/West facing: 5 tiles front, 9 tiles back
        /// </summary>
        private static string GetWindTurbineRequirements(Rot4 rotation)
        {
            // Wind turbines need clear space in front and behind
            // The exact distances vary based on rotation
            if (rotation == Rot4.North)
            {
                return "Requires clear space: 9 tiles north, 5 tiles south";
            }
            else if (rotation == Rot4.East)
            {
                return "Requires clear space: 9 tiles east, 5 tiles west";
            }
            else if (rotation == Rot4.South)
            {
                return "Requires clear space: 5 tiles north, 9 tiles south";
            }
            else // West
            {
                return "Requires clear space: 5 tiles east, 9 tiles west";
            }
        }

        // NOTE: Cooler handling moved to BuildingCellHelper.GetCoolerDirectionInfo()
        // for consistent handling in both placement and cursor inspection.

        /// <summary>
        /// Gets a direction name from an IntVec3 offset.
        /// Delegates to BuildingCellHelper.GetCardinalDirection for consistency.
        /// </summary>
        private static string GetDirectionName(IntVec3 offset)
        {
            return BuildingCellHelper.GetCardinalDirection(offset) ?? "unknown";
        }

        /// <summary>
        /// Gets a human-readable description of the building size and occupied tiles.
        /// Uses RimWorld's GenAdj.OccupiedRect to accurately calculate where the building extends.
        /// </summary>
        internal static string GetSizeDescription(IntVec2 size, Rot4 rotation)
        {
            // Get cursor position for calculating actual occupied rect
            IntVec3 cursorPosition = MapNavigationState.CurrentCursorPosition;

            // Use RimWorld's OccupiedRect to get the actual cells (accounts for rotation adjustments)
            CellRect occupiedRect = GenAdj.OccupiedRect(cursorPosition, rotation, size);

            int width = occupiedRect.Width;
            int depth = occupiedRect.Height;

            if (width == 1 && depth == 1)
            {
                return "Size: 1 tile";
            }

            // Build relative description
            List<string> parts = new List<string>();
            parts.Add($"Size: {width} by {depth}");

            // Calculate extends from cursor position to rect bounds
            if (width > 1 || depth > 1)
            {
                List<string> directions = new List<string>();

                // Calculate how many tiles extend in each direction from cursor
                int northTiles = occupiedRect.maxZ - cursorPosition.z;
                int southTiles = cursorPosition.z - occupiedRect.minZ;
                int eastTiles = occupiedRect.maxX - cursorPosition.x;
                int westTiles = cursorPosition.x - occupiedRect.minX;

                if (northTiles > 0)
                    directions.Add($"{northTiles} north");
                if (southTiles > 0)
                    directions.Add($"{southTiles} south");
                if (eastTiles > 0)
                    directions.Add($"{eastTiles} east");
                if (westTiles > 0)
                    directions.Add($"{westTiles} west");

                if (directions.Count > 0)
                    parts.Add("Extends " + string.Join(", ", directions));
            }

            return string.Join(". ", parts);
        }

        /// <summary>
        /// Gets a human-readable rotation name.
        /// </summary>
        internal static string GetRotationName(Rot4 rotation)
        {
            if (rotation == Rot4.North) return "North";
            if (rotation == Rot4.East) return "East";
            if (rotation == Rot4.South) return "South";
            if (rotation == Rot4.West) return "West";
            return rotation.ToString();
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
                TolkHelper.Speak($"Deselected, {cell.x}, {cell.z}");
            }
            else if (report.Accepted)
            {
                // Add cell
                selectedCells.Add(cell);
                TolkHelper.Speak($"Selected, {cell.x}, {cell.z}");
            }
            else
            {
                // Cannot designate this cell
                string reason = report.Reason ?? "Cannot designate here";
                TolkHelper.Speak($"Invalid: {reason}");
            }
        }

        /// <summary>
        /// Executes the placement (designates all selected cells).
        /// </summary>
        public static void ExecutePlacement(Map map)
        {
            if (selectedDesignator == null || selectedCells.Count == 0)
            {
                TolkHelper.Speak("No tiles selected");
                Cancel();
                return;
            }

            try
            {
                // Use the designator's DesignateMultiCell method
                selectedDesignator.DesignateMultiCell(selectedCells);

                string toolName = selectedDesignator.Label;
                TolkHelper.Speak($"{toolName} placed on {selectedCells.Count} tiles");
                Log.Message($"Executed placement: {toolName} on {selectedCells.Count} tiles");
            }
            catch (System.Exception ex)
            {
                TolkHelper.Speak($"Error placing designation: {ex.Message}", SpeechPriority.High);
                Log.Error($"Error in ExecutePlacement: {ex}");
            }
            finally
            {
                Reset();
            }
        }

        /// <summary>
        /// Cancels the current operation and fully exits architect mode.
        /// </summary>
        public static void Cancel()
        {
            TolkHelper.Speak("Architect menu closed");

            // Always fully close architect mode
            Reset();
        }

        /// <summary>
        /// Checks if the current designator is a zone/area/cell-based designator.
        /// This includes zones (stockpiles, growing zones), areas (home, roof), and other multi-cell designators.
        /// </summary>
        public static bool IsZoneDesignator()
        {
            if (selectedDesignator == null)
                return false;

            // Check if this designator's type hierarchy includes "Designator_Cells"
            // This covers all multi-cell designators: zones, areas, roofs, etc.
            System.Type type = selectedDesignator.GetType();
            while (type != null)
            {
                if (type.Name == "Designator_Cells")
                    return true;
                type = type.BaseType;
            }

            return false;
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
            currentRotation = Rot4.North;
            selectionMode = ArchitectSelectionMode.BoxSelection; // Reset to default mode

            // Deselect any active designator in the game
            if (Find.DesignatorManager != null)
            {
                Find.DesignatorManager.Deselect();
            }

            // Clear selection if it's a MinifiedThing (from inventory install)
            // This prevents stale MinifiedThing selection from affecting gizmo visibility
            if (Find.Selector?.SingleSelectedThing is MinifiedThing)
            {
                Find.Selector.ClearSelection();
            }

            Log.Message("Architect state reset");
        }
    }
}
