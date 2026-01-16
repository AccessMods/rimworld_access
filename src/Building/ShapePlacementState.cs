using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Defines the phases of the shape placement workflow.
    /// </summary>
    public enum PlacementPhase
    {
        /// <summary>Shape placement is not active</summary>
        Inactive,
        /// <summary>User is positioning the first corner of the shape</summary>
        SettingFirstCorner,
        /// <summary>User is positioning the second corner (shape preview updates live)</summary>
        SettingSecondCorner,
        /// <summary>Shape is defined, user is reviewing before placing</summary>
        Previewing
    }

    /// <summary>
    /// Contains the results of a shape placement operation.
    /// Tracks placed blueprints, obstacles, and resource costs for viewing mode.
    /// </summary>
    public class PlacementResult
    {
        /// <summary>Number of blueprints successfully placed</summary>
        public int PlacedCount { get; set; }

        /// <summary>Number of cells that could not be designated due to obstacles</summary>
        public int ObstacleCount { get; set; }

        /// <summary>Cells where blueprints were successfully placed</summary>
        public List<IntVec3> PlacedCells { get; set; }

        /// <summary>Cells that could not be designated (blocked by existing things)</summary>
        public List<IntVec3> ObstacleCells { get; set; }

        /// <summary>Total resource cost for all placed blueprints</summary>
        public int TotalResourceCost { get; set; }

        /// <summary>Name of the primary resource (e.g., "wood", "steel")</summary>
        public string ResourceName { get; set; }

        /// <summary>List of placed blueprint Things for undo functionality</summary>
        public List<Thing> PlacedBlueprints { get; set; }

        /// <summary>
        /// Creates a new empty PlacementResult.
        /// </summary>
        public PlacementResult()
        {
            PlacedCells = new List<IntVec3>();
            ObstacleCells = new List<IntVec3>();
            PlacedBlueprints = new List<Thing>();
            ResourceName = string.Empty;
        }
    }

    /// <summary>
    /// State machine for two-point shape-based building placement.
    /// Manages the workflow: Enter -> SetFirstCorner -> SetSecondCorner/UpdatePreview -> PlaceBlueprints.
    /// </summary>
    public static class ShapePlacementState
    {
        // Shared preview helper for shape calculations and sound feedback
        private static readonly ShapePreviewHelper previewHelper = new ShapePreviewHelper();

        // State tracking
        private static PlacementPhase currentPhase = PlacementPhase.Inactive;
        private static ShapeType currentShape = ShapeType.Manual;
        private static Designator activeDesignator = null;

        // Stack tracking - whether we can return to viewing mode on exit
        private static bool hasViewingModeOnStack = false;

        #region Properties

        /// <summary>
        /// Whether shape placement is currently active.
        /// </summary>
        public static bool IsActive => currentPhase != PlacementPhase.Inactive;

        /// <summary>
        /// The current phase of the placement workflow.
        /// </summary>
        public static PlacementPhase CurrentPhase => currentPhase;

        /// <summary>
        /// The currently selected shape type.
        /// </summary>
        public static ShapeType CurrentShape => currentShape;

        /// <summary>
        /// The first corner of the shape (origin point).
        /// </summary>
        public static IntVec3? FirstCorner => previewHelper.FirstCorner;

        /// <summary>
        /// The second corner of the shape (target point).
        /// </summary>
        public static IntVec3? SecondCorner => previewHelper.SecondCorner;

        /// <summary>
        /// The cells that make up the current shape preview.
        /// Updated as the cursor moves during SettingSecondCorner phase.
        /// </summary>
        public static IReadOnlyList<IntVec3> PreviewCells => previewHelper.PreviewCells;

        /// <summary>
        /// Whether the first corner has been set.
        /// </summary>
        public static bool HasFirstCorner => previewHelper.HasFirstCorner;

        /// <summary>
        /// Whether we're in preview mode (both corners set).
        /// </summary>
        public static bool IsInPreviewMode => previewHelper.IsInPreviewMode;

        /// <summary>
        /// The designator being used for placement.
        /// </summary>
        public static Designator ActiveDesignator => activeDesignator;

        /// <summary>
        /// Whether there's a viewing mode state on the stack to return to.
        /// </summary>
        public static bool HasViewingModeOnStack => hasViewingModeOnStack;

        #endregion

        #region State Management

        /// <summary>
        /// Enters shape placement mode with the specified designator and shape.
        /// </summary>
        /// <param name="designator">The designator to use for placement</param>
        /// <param name="shape">The shape type for the placement</param>
        /// <param name="fromViewingMode">Whether we're entering from viewing mode (to support returning on Escape)</param>
        public static void Enter(Designator designator, ShapeType shape, bool fromViewingMode = false)
        {
            activeDesignator = designator;
            currentShape = shape;
            currentPhase = PlacementPhase.SettingFirstCorner;
            previewHelper.Reset();
            previewHelper.SetCurrentShape(shape);
            hasViewingModeOnStack = fromViewingMode;

            string shapeName = ShapeHelper.GetShapeName(shape);
            string designatorLabel = designator?.Label ?? "Unknown";

            if (shape == ShapeType.Manual)
            {
                TolkHelper.Speak($"{designatorLabel} manual placement. Press Space to place.");
            }
            else
            {
                TolkHelper.Speak($"{designatorLabel} {shapeName} placement. Move to first corner and press Space.");
            }

            Log.Message($"[ShapePlacementState] Entered with shape {shape} for designator {designatorLabel}, viewingModeOnStack={fromViewingMode}");
        }

        /// <summary>
        /// Sets the first corner of the shape at the specified cell.
        /// </summary>
        /// <param name="cell">The cell position for the first corner</param>
        public static void SetFirstCorner(IntVec3 cell)
        {
            if (currentPhase != PlacementPhase.SettingFirstCorner)
            {
                Log.Warning($"[ShapePlacementState] SetFirstCorner called in wrong phase: {currentPhase}");
                return;
            }

            previewHelper.SetFirstCorner(cell, "[ShapePlacementState]");
            currentPhase = PlacementPhase.SettingSecondCorner;
        }

        /// <summary>
        /// Sets the second corner of the shape and transitions to previewing phase.
        /// </summary>
        /// <param name="cell">The cell position for the second corner</param>
        public static void SetSecondCorner(IntVec3 cell)
        {
            if (currentPhase != PlacementPhase.SettingSecondCorner)
            {
                Log.Warning($"[ShapePlacementState] SetSecondCorner called in wrong phase: {currentPhase}");
                return;
            }

            if (!previewHelper.HasFirstCorner)
            {
                Log.Error("[ShapePlacementState] SetSecondCorner called without first corner set");
                return;
            }

            previewHelper.SetSecondCorner(cell, "[ShapePlacementState]");
            currentPhase = PlacementPhase.Previewing;
        }

        /// <summary>
        /// Updates the shape preview as the cursor moves during SettingSecondCorner phase.
        /// Plays sound feedback when the cell count changes.
        /// </summary>
        /// <param name="cursor">The current cursor position</param>
        public static void UpdatePreview(IntVec3 cursor)
        {
            if (currentPhase != PlacementPhase.SettingSecondCorner)
                return;

            if (!previewHelper.HasFirstCorner)
                return;

            previewHelper.UpdatePreview(cursor);
        }

        /// <summary>
        /// Places designations for all cells in the current preview.
        /// Works for all designator types: Build (blueprints), Orders (Hunt, Haul), Zones, and Cells (Mine).
        /// </summary>
        /// <returns>A PlacementResult containing statistics and placed items</returns>
        public static PlacementResult PlaceDesignations()
        {
            PlacementResult result = new PlacementResult();

            if (activeDesignator == null)
            {
                TolkHelper.Speak("No designator active", SpeechPriority.High);
                return result;
            }

            if (previewHelper.PreviewCells.Count == 0)
            {
                TolkHelper.Speak("No cells selected", SpeechPriority.High);
                return result;
            }

            Map map = Find.CurrentMap;
            if (map == null)
            {
                TolkHelper.Speak("No map available", SpeechPriority.High);
                return result;
            }

            // Track items placed this operation for undo
            List<Thing> placedThisOperation = new List<Thing>();

            // Get designator info
            string designatorName = activeDesignator.Label ?? "Unknown";
            bool isBuildDesignator = ShapeHelper.IsBuildDesignator(activeDesignator);
            bool isZoneDesignator = ShapeHelper.IsZoneDesignator(activeDesignator);

            // For zones, use DesignateMultiCell with all valid cells at once
            if (isZoneDesignator)
            {
                // Filter to valid cells first
                List<IntVec3> validCells = new List<IntVec3>();
                foreach (IntVec3 cell in previewHelper.PreviewCells)
                {
                    AcceptanceReport report = activeDesignator.CanDesignateCell(cell);
                    if (report.Accepted)
                    {
                        validCells.Add(cell);
                    }
                    else
                    {
                        result.ObstacleCells.Add(cell);
                        result.ObstacleCount++;
                    }
                }

                if (validCells.Count > 0)
                {
                    try
                    {
                        activeDesignator.DesignateMultiCell(validCells);
                        result.PlacedCells.AddRange(validCells);
                        result.PlacedCount = validCells.Count;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"[ShapePlacementState] Error placing zone: {ex.Message}");
                    }
                }
            }
            // For all other designators (Build, Orders, Cells), use DesignateSingleCell per cell
            else
            {
                // Get building info for cost calculation (only applies to Build designators)
                BuildableDef buildableDef = isBuildDesignator ? GetBuildableDefFromDesignator(activeDesignator) : null;
                int costPerCell = GetCostPerCell(buildableDef);
                string resourceName = GetResourceName(buildableDef);

                // Place designation for each cell
                foreach (IntVec3 cell in previewHelper.PreviewCells)
                {
                    AcceptanceReport report = activeDesignator.CanDesignateCell(cell);

                    if (report.Accepted)
                    {
                        try
                        {
                            // For Build designators, track the blueprint for undo
                            if (isBuildDesignator)
                            {
                                List<Thing> thingsBefore = new List<Thing>(cell.GetThingList(map));
                                activeDesignator.DesignateSingleCell(cell);
                                List<Thing> thingsAfter = cell.GetThingList(map);
                                foreach (Thing thing in thingsAfter)
                                {
                                    if (!thingsBefore.Contains(thing) &&
                                        (thing.def.IsBlueprint || thing.def.IsFrame))
                                    {
                                        placedThisOperation.Add(thing);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // For Orders (Hunt, Haul) and Cells (Mine), just designate
                                activeDesignator.DesignateSingleCell(cell);
                            }

                            result.PlacedCells.Add(cell);
                            result.PlacedCount++;
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"[ShapePlacementState] Error placing at {cell}: {ex.Message}");
                            result.ObstacleCells.Add(cell);
                            result.ObstacleCount++;
                        }
                    }
                    else
                    {
                        result.ObstacleCells.Add(cell);
                        result.ObstacleCount++;
                    }
                }

                // Calculate total resource cost (only for Build designators)
                if (isBuildDesignator)
                {
                    result.TotalResourceCost = result.PlacedCount * costPerCell;
                    result.ResourceName = resourceName;
                }
            }

            // Finalize the designator if any placements succeeded
            if (result.PlacedCount > 0)
            {
                try
                {
                    activeDesignator.Finalize(true);
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[ShapePlacementState] Error finalizing designator: {ex.Message}");
                }
            }

            result.PlacedBlueprints = placedThisOperation;

            // Announce results
            string announcement = BuildPlacementAnnouncement(result, designatorName, activeDesignator);
            TolkHelper.Speak(announcement);

            Log.Message($"[ShapePlacementState] Placed {result.PlacedCount} designations, {result.ObstacleCount} obstacles");

            return result;
        }

        /// <summary>
        /// Places blueprints for all cells in the current preview.
        /// Kept for backwards compatibility - calls PlaceDesignations internally.
        /// </summary>
        /// <returns>A PlacementResult containing statistics and placed blueprints</returns>
        public static PlacementResult PlaceBlueprints()
        {
            return PlaceDesignations();
        }

        /// <summary>
        /// Cancels the current shape placement operation completely and exits shape mode.
        /// </summary>
        public static void Cancel()
        {
            PlacementPhase previousPhase = currentPhase;

            // Reset all state
            Reset();

            // Announce based on what phase we were in
            switch (previousPhase)
            {
                case PlacementPhase.SettingFirstCorner:
                    TolkHelper.Speak("Shape placement cancelled");
                    break;
                case PlacementPhase.SettingSecondCorner:
                    TolkHelper.Speak("Shape cancelled, back to first corner");
                    break;
                case PlacementPhase.Previewing:
                    TolkHelper.Speak("Preview cancelled");
                    break;
            }

            Log.Message($"[ShapePlacementState] Cancelled from phase {previousPhase}");
        }

        /// <summary>
        /// Clears the current selection but stays in shape placement mode with the same shape.
        /// Use this for Escape key behavior when user wants to restart selection, not exit.
        /// </summary>
        /// <param name="silent">If true, does not announce anything (caller will announce)</param>
        /// <returns>True if selection was cleared and we should stay in shape mode, false if nothing to clear</returns>
        public static bool ClearSelectionAndStay(bool silent = false)
        {
            PlacementPhase previousPhase = currentPhase;

            // If we're in SettingFirstCorner with no corner set, there's nothing to clear
            if (previousPhase == PlacementPhase.SettingFirstCorner && !previewHelper.HasFirstCorner)
            {
                return false;
            }

            // Save the shape for logging
            ShapeType savedShape = currentShape;

            // Announce if not silent
            if (!silent)
            {
                if (previousPhase == PlacementPhase.Previewing)
                {
                    // In Previewing phase, tell user how to proceed
                    TolkHelper.Speak("Selection cleared. Press Escape again to exit, or Enter then Equals to add another section.");
                }
                else if (previousPhase == PlacementPhase.SettingSecondCorner)
                {
                    TolkHelper.Speak("Selection cancelled, back to first corner");
                }
            }

            // Reset preview helper but keep the shape
            previewHelper.Reset();
            currentPhase = PlacementPhase.SettingFirstCorner;

            Log.Message($"[ShapePlacementState] Cleared selection from phase {previousPhase}, staying in {savedShape} mode");
            return true;
        }

        /// <summary>
        /// Resets all state variables to their initial values.
        /// </summary>
        public static void Reset()
        {
            currentPhase = PlacementPhase.Inactive;
            currentShape = ShapeType.Manual;
            previewHelper.FullReset();
            activeDesignator = null;
            hasViewingModeOnStack = false;

            Log.Message("[ShapePlacementState] State reset");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the BuildableDef from a designator for cost calculation.
        /// </summary>
        private static BuildableDef GetBuildableDefFromDesignator(Designator designator)
        {
            if (designator is Designator_Build buildDesignator)
            {
                return buildDesignator.PlacingDef;
            }

            if (designator is Designator_Place placeDesignator)
            {
                return placeDesignator.PlacingDef;
            }

            return null;
        }

        /// <summary>
        /// Gets the resource cost per cell for a buildable.
        /// </summary>
        private static int GetCostPerCell(BuildableDef buildable)
        {
            if (buildable == null)
                return 0;

            // Check for stuff cost (most common for walls, floors, etc.)
            if (buildable is ThingDef thingDef && thingDef.MadeFromStuff)
            {
                return buildable.CostStuffCount;
            }

            // Check for fixed costs
            if (buildable.CostList != null && buildable.CostList.Count > 0)
            {
                // Return the count of the first (primary) cost
                return buildable.CostList[0].count;
            }

            return 0;
        }

        /// <summary>
        /// Gets the name of the primary resource for a buildable.
        /// </summary>
        private static string GetResourceName(BuildableDef buildable)
        {
            if (buildable == null)
                return string.Empty;

            // For stuff-based buildings, return generic "material" (actual material depends on selection)
            if (buildable is ThingDef thingDef && thingDef.MadeFromStuff)
            {
                // Try to get the currently selected stuff from ArchitectState
                if (ArchitectState.SelectedMaterial != null)
                {
                    return ArchitectState.SelectedMaterial.label;
                }
                return "material";
            }

            // For fixed cost buildings, return the primary resource name
            if (buildable.CostList != null && buildable.CostList.Count > 0)
            {
                return buildable.CostList[0].thingDef.label;
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds the announcement string for placement results.
        /// </summary>
        private static string BuildPlacementAnnouncement(PlacementResult result, string designatorName, Designator designator)
        {
            List<string> parts = new List<string>();
            bool isBuild = ShapeHelper.IsBuildDesignator(designator);
            bool isOrder = ShapeHelper.IsOrderDesignator(designator);

            // Main placement info
            if (result.PlacedCount > 0)
            {
                if (isBuild)
                {
                    // Pluralize the designator name if multiple items placed
                    string name = result.PlacedCount > 1
                        ? Find.ActiveLanguageWorker.Pluralize(designatorName, result.PlacedCount)
                        : designatorName;

                    string costInfo = string.Empty;
                    if (result.TotalResourceCost > 0 && !string.IsNullOrEmpty(result.ResourceName))
                    {
                        costInfo = $" ({result.TotalResourceCost} {result.ResourceName})";
                    }
                    parts.Add($"Placed {result.PlacedCount} {name}{costInfo}");
                }
                else
                {
                    // For orders, use "Designated X for [action]" matching RimWorld's terminology
                    string action = GetActionFromDesignatorName(designatorName);
                    parts.Add($"Designated {result.PlacedCount} for {action}");
                }
            }
            else
            {
                if (isBuild)
                {
                    parts.Add("No blueprints placed");
                }
                else
                {
                    parts.Add("No designations placed");
                }
            }

            // Obstacle info - only for build designators, not for orders
            if (!isOrder && result.ObstacleCount > 0)
            {
                parts.Add($"{result.ObstacleCount} obstacles found");
            }

            return string.Join(". ", parts);
        }

        /// <summary>
        /// Converts a designator name to a gerund action phrase for announcements.
        /// </summary>
        /// <param name="designatorName">The designator label (e.g., "Haul things", "Hunt", "Mine")</param>
        /// <returns>A gerund action phrase (e.g., "hauling", "hunting", "mining")</returns>
        private static string GetActionFromDesignatorName(string designatorName)
        {
            // Handle common designator names and convert to gerund form
            string lowerName = designatorName.ToLower();

            // "Haul things" -> "hauling"
            if (lowerName.Contains("haul"))
                return "hauling";

            // "Hunt" -> "hunting"
            if (lowerName.Contains("hunt"))
                return "hunting";

            // "Mine" -> "mining"
            if (lowerName.Contains("mine"))
                return "mining";

            // "Deconstruct" -> "deconstruction"
            if (lowerName.Contains("deconstruct"))
                return "deconstruction";

            // "Cut plants" -> "cutting"
            if (lowerName.Contains("cut"))
                return "cutting";

            // "Smooth" -> "smoothing"
            if (lowerName.Contains("smooth"))
                return "smoothing";

            // "Tame" -> "taming"
            if (lowerName.Contains("tame"))
                return "taming";

            // "Cancel" -> "cancellation"
            if (lowerName.Contains("cancel"))
                return "cancellation";

            // For unknown designators, just use the name lowercase
            return lowerName;
        }

        /// <summary>
        /// Gets whether the current phase allows cursor movement to update preview.
        /// </summary>
        public static bool ShouldUpdatePreviewOnMove()
        {
            return currentPhase == PlacementPhase.SettingSecondCorner && previewHelper.HasFirstCorner;
        }

        /// <summary>
        /// Gets the dimensions of the current shape preview.
        /// </summary>
        /// <returns>Tuple of (width, height) or (0, 0) if no preview</returns>
        public static (int width, int height) GetCurrentDimensions()
        {
            if (!previewHelper.HasFirstCorner || !MapNavigationState.IsInitialized)
                return (0, 0);

            IntVec3 target = previewHelper.SecondCorner ?? MapNavigationState.CurrentCursorPosition;
            return ShapeHelper.GetDimensions(previewHelper.FirstCorner.Value, target);
        }

        #endregion
    }
}
