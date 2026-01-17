using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to handle input during architect placement mode.
    /// Handles Space (select/place cell), Shift+Space (cancel blueprint),
    /// Enter (confirm), and Escape (cancel).
    /// Also modifies arrow key announcements to include selected cell status.
    /// </summary>
    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class ArchitectPlacementInputPatch
    {
        private static float lastSpaceTime = 0f;
        private const float SpaceCooldown = 0.2f;

        /// <summary>
        /// Prefix patch to handle architect placement input at GUI event level.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal)]
        public static void Prefix()
        {
            // Only active during gameplay (not in main menu)
            if (Current.ProgramState != ProgramState.Playing)
                return;

            // Check if we're in placement mode (either via ArchitectState or directly via DesignatorManager)
            bool inArchitectMode = ArchitectState.IsInPlacementMode;
            bool hasActiveDesignator = Find.DesignatorManager != null &&
                                      Find.DesignatorManager.SelectedDesignator != null;

            // Check if local map targeting is active for transport pod landing specifically
            // We need to differentiate between transport pod landing and weapon/ability targeting
            bool inTransportPodTargeting = false;
            if (Find.Targeter != null && Find.Targeter.IsTargeting)
            {
                // Check if the mouseAttachment matches the transport pod cursor
                // This ensures we only handle transport pod landing, not weapon targeting
                var mouseAttachmentField = AccessTools.Field(typeof(Targeter), "mouseAttachment");
                if (mouseAttachmentField != null)
                {
                    Texture2D mouseAttachment = mouseAttachmentField.GetValue(Find.Targeter) as Texture2D;
                    if (mouseAttachment != null &&
                        CompLaunchable.TargeterMouseAttachment != null &&
                        mouseAttachment == CompLaunchable.TargeterMouseAttachment)
                    {
                        inTransportPodTargeting = true;
                    }
                }
            }

            // Only active when in architect placement mode OR when a designator is selected (e.g., from gizmos)
            // OR when transport pod landing targeting is active
            if (!inArchitectMode && !hasActiveDesignator && !inTransportPodTargeting)
                return;

            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            // Don't process input when shape selection menu or viewing mode is active
            // Those menus handle their own input and should receive all keys
            if (ShapeSelectionMenuState.IsActive || ViewingModeState.IsActive)
                return;

            // Check we have a valid map
            if (Find.CurrentMap == null)
            {
                if (inArchitectMode)
                    ArchitectState.Cancel();
                else if (hasActiveDesignator)
                    Find.DesignatorManager.Deselect();
                return;
            }

            KeyCode key = Event.current.keyCode;
            bool handled = false;
            bool shiftHeld = Event.current.shift;

            // Handle local map targeting mode (transport pod landing) first
            if (inTransportPodTargeting)
            {
                handled = HandleTargetingModeInput(key, shiftHeld);
                if (handled)
                {
                    Event.current.Use();
                }
                return;
            }

            // Get the active designator (from either source)
            Designator activeDesignator = inArchitectMode ?
                ArchitectState.SelectedDesignator :
                Find.DesignatorManager.SelectedDesignator;

            if (activeDesignator == null)
                return;

            // Check designator type - use ShapeHelper methods for consistent classification
            bool isZoneDesignator = ShapeHelper.IsZoneDesignator(activeDesignator);
            bool isBuildDesignator = ShapeHelper.IsBuildDesignator(activeDesignator);
            bool isPlaceDesignator = ShapeHelper.IsPlaceDesignator(activeDesignator);
            bool isCellsDesignator = ShapeHelper.IsCellsDesignator(activeDesignator);
            bool isOrderDesignator = ShapeHelper.IsOrderDesignator(activeDesignator);

            // Get available shapes for ANY designator that has DrawStyleCategory
            // This includes buildings, orders (Hunt, Haul), zones, and other multi-cell designators
            var availableShapes = ShapeHelper.GetAvailableShapes(activeDesignator);
            bool supportsShapes = availableShapes.Count >= 1;

            // Tab key - open shape selection menu (for any designator that supports shapes)
            if (key == KeyCode.Tab && !shiftHeld)
            {
                if (supportsShapes && inArchitectMode)
                {
                    // Cancel any active shape placement before opening menu
                    if (ShapePlacementState.IsActive)
                    {
                        ShapePlacementState.Reset();
                    }

                    if (availableShapes.Count > 1)
                    {
                        // Multiple shapes available - open shape selection menu
                        ShapeSelectionMenuState.Open(activeDesignator);
                    }
                    else
                    {
                        // Only one shape - activate it directly
                        ShapePlacementState.Enter(activeDesignator, availableShapes[0]);
                    }
                    handled = true;
                }
            }

            // Shift+Tab - quick switch to Manual mode (for any designator with shapes)
            if (key == KeyCode.Tab && shiftHeld && supportsShapes && inArchitectMode)
            {
                // Reset to manual mode and cancel any shape in progress
                if (ShapePlacementState.IsActive)
                {
                    ShapePlacementState.Cancel();
                }
                ShapePlacementState.Enter(activeDesignator, ShapeType.Manual);
                handled = true;
            }
            // Shift+Space - Cancel blueprint at cursor position (check before regular Space)
            if (shiftHeld && key == KeyCode.Space)
            {
                IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;
                CancelBlueprintAtPosition(currentPosition);
                handled = true;
            }
            // R key - rotate building
            else if (key == KeyCode.R)
            {
                if (inArchitectMode)
                {
                    ArchitectState.RotateBuilding();
                }
                else if (activeDesignator is Designator_Place designatorPlace)
                {
                    // Use reflection to access private placingRot field
                    var rotField = AccessTools.Field(typeof(Designator_Place), "placingRot");
                    if (rotField != null)
                    {
                        Rot4 currentRot = (Rot4)rotField.GetValue(designatorPlace);
                        currentRot.Rotate(RotationDirection.Clockwise);
                        rotField.SetValue(designatorPlace, currentRot);

                        // Build a proper announcement with direction and special info
                        string announcement = GetDesignatorRotationAnnouncement(designatorPlace, currentRot);
                        TolkHelper.Speak(announcement);
                    }
                }
                handled = true;
            }
            // Shift+Arrow keys - auto-select to wall (for zone designators)
            else if (shiftHeld && isZoneDesignator)
            {
                IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;
                Map map = Find.CurrentMap;
                Rot4 direction = Rot4.Invalid;

                if (key == KeyCode.UpArrow)
                    direction = Rot4.North;
                else if (key == KeyCode.DownArrow)
                    direction = Rot4.South;
                else if (key == KeyCode.LeftArrow)
                    direction = Rot4.West;
                else if (key == KeyCode.RightArrow)
                    direction = Rot4.East;

                if (direction != Rot4.Invalid)
                {
                    AutoSelectToWall(currentPosition, direction, map, activeDesignator);
                    handled = true;
                }
            }
            // Space key - unified handling for all designator types
            // Behavior depends on whether ShapePlacementState is active and what shape is selected
            else if (key == KeyCode.Space)
            {
                // Cooldown to prevent rapid toggling
                if (Time.time - lastSpaceTime < SpaceCooldown)
                    return;

                lastSpaceTime = Time.time;

                IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;

                // Check if we're in shape placement mode with a non-Manual shape
                // This applies to ALL designator types (build, orders, zones)
                if (ShapePlacementState.IsActive && ShapePlacementState.CurrentShape != ShapeType.Manual)
                {
                    // Two-point placement workflow - same for all designator types
                    if (ShapePlacementState.CurrentPhase == PlacementPhase.SettingFirstCorner)
                    {
                        ShapePlacementState.SetFirstCorner(currentPosition);
                    }
                    else if (ShapePlacementState.CurrentPhase == PlacementPhase.SettingSecondCorner)
                    {
                        ShapePlacementState.SetSecondCorner(currentPosition);
                    }
                }
                // Manual mode or ShapePlacementState inactive - single cell designation
                else
                {
                    // For build/place designators (buildings, install)
                    if (isPlaceDesignator)
                    {
                        AcceptanceReport report = activeDesignator.CanDesignateCell(currentPosition);

                        if (report.Accepted)
                        {
                            try
                            {
                                activeDesignator.DesignateSingleCell(currentPosition);
                                activeDesignator.Finalize(true);

                                string label = activeDesignator.Label;
                                TolkHelper.Speak($"{label} placed at {currentPosition.x}, {currentPosition.z}");

                                // If in ArchitectState mode, clear selected cells for next placement
                                if (inArchitectMode)
                                {
                                    ArchitectState.SelectedCells.Clear();
                                }
                                else
                                {
                                    // For gizmo-activated placement (like Reinstall), exit after placement
                                    Find.DesignatorManager.Deselect();
                                }
                            }
                            catch (System.Exception ex)
                            {
                                TolkHelper.Speak($"Error placing: {ex.Message}", SpeechPriority.High);
                                Log.Error($"Error in single cell designation: {ex}");
                            }
                        }
                        else
                        {
                            string reason = report.Reason ?? "Cannot place here";
                            TolkHelper.Speak($"Invalid: {reason}");
                        }
                    }
                    // For zone designators
                    else if (isZoneDesignator && inArchitectMode)
                    {
                        // Toggle cell in the selection list
                        ArchitectState.ToggleCell(currentPosition);
                    }
                    // For orders (Hunt, Haul, etc.) and cells designators (Mine)
                    else if ((isOrderDesignator || isCellsDesignator) && inArchitectMode)
                    {
                        // Toggle cell in the selection list
                        ArchitectState.ToggleCell(currentPosition);
                    }
                }

                handled = true;
            }
            // Enter key - confirm and execute designation
            // Unified handling for all designator types with shape support
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                // If in shape placement previewing phase, execute the designation and enter viewing mode
                // This works for ALL designator types (build, orders, zones)
                if (ShapePlacementState.IsActive && ShapePlacementState.CurrentPhase == PlacementPhase.Previewing)
                {
                    // Save the current shape before placing (for restore after undo)
                    ShapeType currentShape = ShapePlacementState.CurrentShape;
                    // Pass silent: true because ViewingModeState.Enter will announce the placement
                    var result = ShapePlacementState.PlaceDesignations(silent: true);
                    if (result.PlacedCount > 0)
                    {
                        ViewingModeState.Enter(result, activeDesignator, currentShape);
                    }
                    // Reset shape placement state after placing
                    ShapePlacementState.Reset();
                    handled = true;
                }
                // If in shape placement mode but corners not yet set, give guidance
                else if (ShapePlacementState.IsActive && ShapePlacementState.CurrentShape != ShapeType.Manual)
                {
                    if (ShapePlacementState.CurrentPhase == PlacementPhase.SettingFirstCorner)
                    {
                        TolkHelper.Speak("Place first point with Space");
                    }
                    else if (ShapePlacementState.CurrentPhase == PlacementPhase.SettingSecondCorner)
                    {
                        TolkHelper.Speak("Place second point with Space");
                    }
                    handled = true;
                }
                // For place designators (build, reinstall) not in shape mode
                else if (isPlaceDesignator)
                {
                    // Normal exit - placement completed
                    TolkHelper.Speak("Placement completed");
                    if (ShapePlacementState.IsActive)
                    {
                        ShapePlacementState.Reset();
                    }
                    if (inArchitectMode)
                        ArchitectState.Reset();
                    else
                        Find.DesignatorManager.Deselect();
                    handled = true;
                }
                // For zone designators (not in shape mode) - execute from selected cells
                else if (isZoneDesignator && inArchitectMode)
                {
                    Map map = Find.CurrentMap;
                    ExecuteZonePlacement(activeDesignator, map);
                    handled = true;
                }
                // For orders and cells designators in architect mode (not in shape mode)
                else if (inArchitectMode && (isOrderDesignator || isCellsDesignator))
                {
                    // Execute the placement from selected cells
                    ArchitectState.ExecutePlacement(Find.CurrentMap);
                    handled = true;
                }
            }
            // Escape key - cancel shape placement, rectangle, or cancel placement
            else if (key == KeyCode.Escape)
            {
                // If shape placement is active, check what to do based on corners and stack
                if (ShapePlacementState.IsActive)
                {
                    // Case 1: Corners are set - clear them and stay in placement mode
                    if (ShapePlacementState.HasFirstCorner)
                    {
                        // Clear corners via previewHelper, stay in placement mode
                        // This resets to SettingFirstCorner phase
                        // Use silent=true so we control the announcement here
                        ShapePlacementState.ClearSelectionAndStay(silent: true);
                        TolkHelper.Speak("Selection cleared");
                    }
                    // Case 2: No corners set, but we came from viewing mode - return to it
                    else if (ShapePlacementState.HasViewingModeOnStack)
                    {
                        ShapePlacementState.Reset();
                        // Re-activate viewing mode with existing segments intact
                        ViewingModeState.Reactivate();
                    }
                    // Case 3: No corners set, no viewing mode on stack - exit architect entirely
                    else
                    {
                        TolkHelper.Speak("Placement cancelled");
                        ShapePlacementState.Reset();
                        if (inArchitectMode)
                            ArchitectState.Reset();
                        else
                            Find.DesignatorManager.Deselect();
                    }
                }
                else
                {
                    TolkHelper.Speak("Placement cancelled");
                    if (inArchitectMode)
                        ArchitectState.Cancel();
                    else
                        Find.DesignatorManager.Deselect();
                }
                handled = true;
            }

            if (handled)
            {
                Event.current.Use();
            }
        }

        /// <summary>
        /// Cancels any blueprint or frame at the specified position.
        /// </summary>
        private static void CancelBlueprintAtPosition(IntVec3 position)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            // Get all things at this position
            List<Thing> thingList = position.GetThingList(map);

            // Look for blueprints or frames
            bool foundAndCanceled = false;
            for (int i = thingList.Count - 1; i >= 0; i--)
            {
                Thing thing = thingList[i];

                // Check if it's a player-owned blueprint or frame
                if (thing.Faction == Faction.OfPlayer && (thing is Frame || thing is Blueprint))
                {
                    string thingLabel = thing.Label;
                    thing.Destroy(DestroyMode.Cancel);
                    TolkHelper.Speak($"Cancelled {thingLabel}");
                    SoundDefOf.Designate_Cancel.PlayOneShotOnCamera();
                    foundAndCanceled = true;
                    break; // Only cancel one blueprint per keypress
                }
            }

            if (!foundAndCanceled)
            {
                TolkHelper.Speak("No blueprint to cancel here");
            }
        }

        /// <summary>
        /// Handles keyboard input during local map targeting mode (e.g., transport pod landing).
        /// Returns true if input was handled.
        /// </summary>
        private static bool HandleTargetingModeInput(KeyCode key, bool shiftHeld)
        {
            // Space or Enter - confirm target at cursor position
            if (key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                if (shiftHeld)
                    return false;

                IntVec3 targetCell = MapNavigationState.CurrentCursorPosition;
                Map map = Find.CurrentMap;

                if (map == null || !targetCell.InBounds(map))
                {
                    TolkHelper.Speak("Invalid target position", SpeechPriority.High);
                    return true;
                }

                // Validate landing spot using the game's validation
                if (!DropCellFinder.IsGoodDropSpot(targetCell, map, allowFogged: false, canRoofPunch: true))
                {
                    string reason = GetLandingInvalidReason(targetCell, map);
                    TolkHelper.Speak($"Cannot land here: {reason}", SpeechPriority.High);
                    return true;
                }

                // Create target info and let the targeter process it
                LocalTargetInfo target = new LocalTargetInfo(targetCell);

                // Get the action BEFORE stopping targeting (StopTargeting clears the action)
                var actionField = HarmonyLib.AccessTools.Field(typeof(Targeter), "action");
                System.Action<LocalTargetInfo> action = null;
                if (actionField != null)
                {
                    action = actionField.GetValue(Find.Targeter) as System.Action<LocalTargetInfo>;
                }

                // Check if we have an action to invoke
                if (action != null)
                {
                    // Stop targeting and invoke the action
                    Find.Targeter.StopTargeting();
                    action.Invoke(target);
                    TolkHelper.Speak($"Landing confirmed at {targetCell.x}, {targetCell.z}", SpeechPriority.Normal);
                }
                else
                {
                    // No action available - stop targeting but report the error
                    Find.Targeter.StopTargeting();
                    TolkHelper.Speak("Error: Could not confirm target. Please try using the mouse.", SpeechPriority.High);
                }

                return true;
            }

            // Escape - cancel targeting
            if (key == KeyCode.Escape)
            {
                Find.Targeter.StopTargeting();
                TolkHelper.Speak("Targeting cancelled", SpeechPriority.Normal);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a human-readable reason why a landing spot is invalid.
        /// Note: Thin roofs are VALID - pods punch through them.
        /// Only thick roofs (overhead mountain) block landing.
        /// </summary>
        private static string GetLandingInvalidReason(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
                return "Out of bounds";

            // Check terrain
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain != null)
            {
                if (terrain.IsWater)
                    return "Water";
            }

            // Check for thick roof (can't punch through mountain)
            // Note: Thin roofs are OK - pods punch through
            RoofDef roof = cell.GetRoof(map);
            if (roof != null && roof.isThickRoof)
                return "Overhead mountain";

            // Check if walkable (basic passability)
            if (!cell.Walkable(map))
                return "Impassable terrain";

            // Check for buildings/edifices
            Building building = cell.GetEdifice(map);
            if (building != null)
            {
                // IsClearableFreeBuilding buildings (like conduits) are OK
                if (!building.IsClearableFreeBuilding)
                    return building.LabelCap;
            }

            // Check for fog
            if (cell.Fogged(map))
                return "Fogged area";

            // Check for existing skyfallers or transporters
            List<Thing> things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing is IActiveTransporter)
                    return "Another transport pod";
                if (thing is Skyfaller)
                    return "Incoming skyfaller";
            }

            return "Invalid spot";
        }

        // Note: IsZoneDesignator moved to ShapeHelper.IsZoneDesignator()

        /// <summary>
        /// Auto-selects cells in a direction until hitting a wall or impassable terrain.
        /// </summary>
        private static void AutoSelectToWall(IntVec3 startPosition, Rot4 direction, Map map, Designator designator)
        {
            try
            {
                List<IntVec3> lineCells = new List<IntVec3>();
                IntVec3 currentCell = startPosition + direction.FacingCell;

                // Move in the direction until we hit a wall or go out of bounds
                while (currentCell.InBounds(map) && designator.CanDesignateCell(currentCell).Accepted)
                {
                    lineCells.Add(currentCell);
                    currentCell += direction.FacingCell;
                }

                // Add all cells to selection
                int addedCount = 0;
                foreach (IntVec3 cell in lineCells)
                {
                    if (!ArchitectState.SelectedCells.Contains(cell))
                    {
                        ArchitectState.SelectedCells.Add(cell);
                        addedCount++;
                    }
                }

                string directionName = direction.ToStringHuman();
                TolkHelper.Speak($"Selected {addedCount} cells to {directionName}. Total: {ArchitectState.SelectedCells.Count}");
                Log.Message($"Auto-select to wall: {addedCount} cells in direction {directionName}");
            }
            catch (System.Exception ex)
            {
                TolkHelper.Speak($"Error auto-selecting: {ex.Message}", SpeechPriority.High);
                Log.Error($"AutoSelectToWall error: {ex}");
            }
        }

        /// <summary>
        /// Executes zone placement with all selected cells.
        /// </summary>
        private static void ExecuteZonePlacement(Designator designator, Map map)
        {
            if (ArchitectState.SelectedCells.Count == 0)
            {
                TolkHelper.Speak("No cells selected");
                ArchitectState.Reset();
                return;
            }

            try
            {
                // Use the designator's DesignateMultiCell method
                designator.DesignateMultiCell(ArchitectState.SelectedCells);

                string label = designator.Label ?? "Zone";
                TolkHelper.Speak($"{label} created with {ArchitectState.SelectedCells.Count} cells");
                Log.Message($"Zone placement executed: {label} with {ArchitectState.SelectedCells.Count} cells");
            }
            catch (System.Exception ex)
            {
                TolkHelper.Speak($"Error creating zone: {ex.Message}", SpeechPriority.High);
                Log.Error($"ExecuteZonePlacement error: {ex}");
            }
            finally
            {
                ArchitectState.Reset();
            }
        }

        /// <summary>
        /// Gets a rotation announcement for a Designator_Place (used by reinstall gizmo, etc.)
        /// Delegates to shared ArchitectState method to avoid duplication.
        /// </summary>
        private static string GetDesignatorRotationAnnouncement(Designator_Place designatorPlace, Rot4 rotation)
        {
            return ArchitectState.GetRotationAnnouncementForDef(designatorPlace.PlacingDef, rotation);
        }
    }

    /// <summary>
    /// Harmony patch to modify map navigation announcements during architect placement.
    /// Adds information about whether a cell can be designated.
    /// </summary>
    [HarmonyPatch(typeof(CameraDriver))]
    [HarmonyPatch("Update")]
    public static class ArchitectPlacementAnnouncementPatch
    {
        /// <summary>
        /// Postfix patch to modify tile announcements during architect placement.
        /// Adds "Selected" prefix for multi-cell designators, or validity info for build designators.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(CameraDriver __instance)
        {
            // Check if an arrow key was just pressed
            if (Find.CurrentMap == null || !MapNavigationState.IsInitialized)
                return;

            // Check if any arrow key is being held (use GetKey for key repeat support)
            bool arrowKeyPressed = Input.GetKey(KeyCode.UpArrow) ||
                                   Input.GetKey(KeyCode.DownArrow) ||
                                   Input.GetKey(KeyCode.LeftArrow) ||
                                   Input.GetKey(KeyCode.RightArrow);

            if (!arrowKeyPressed)
                return;

            IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;
            Map map = Find.CurrentMap;

            // Skip if in local map targeting mode (transport pod landing)
            // We don't add extra announcements for targeting - the game handles this
            if (Find.Targeter != null && Find.Targeter.IsTargeting)
            {
                return;
            }

            // Update shape preview on cursor movement (if in SettingSecondCorner phase)
            if (ShapePlacementState.ShouldUpdatePreviewOnMove())
            {
                ShapePlacementState.UpdatePreview(currentPosition);
            }

            // Only continue for architect placement mode
            if (!ArchitectState.IsInPlacementMode)
                return;

            Designator designator = ArchitectState.SelectedDesignator;

            if (designator == null)
                return;

            // Get the last announced info
            string lastInfo = MapNavigationState.LastAnnouncedInfo;

            // For multi-cell designators (zones, etc.), show if cell is already selected
            // Don't check placement validity here - only check when user presses Space to place
            if (!(designator is Designator_Build))
            {
                if (ArchitectState.SelectedCells.Contains(currentPosition))
                {
                    if (!lastInfo.StartsWith("Selected"))
                    {
                        string modifiedInfo = "Selected, " + lastInfo;
                        TolkHelper.Speak(modifiedInfo);
                        MapNavigationState.LastAnnouncedInfo = modifiedInfo;
                    }
                }
            }
            // Note: Placement validity is checked when Space is pressed, not on cursor movement
        }
    }

    /// <summary>
    /// Harmony patch to intercept pause key (Space) during architect placement mode.
    /// Prevents Space from pausing the game when in placement mode.
    /// </summary>
    [HarmonyPatch(typeof(TimeControls))]
    [HarmonyPatch("DoTimeControlsGUI")]
    public static class ArchitectPlacementTimeControlsPatch
    {
        /// <summary>
        /// Prefix patch that intercepts the pause key event during architect placement.
        /// Returns false to skip TimeControls processing when Space is pressed in placement mode.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            // Only intercept when in architect placement mode
            if (!ArchitectState.IsInPlacementMode)
                return true; // Continue with normal processing

            // Check if this is a KeyDown event for the pause toggle key
            if (Event.current.type == EventType.KeyDown &&
                KeyBindingDefOf.TogglePause.KeyDownEvent)
            {
                // Consume the event so TimeControls doesn't process it
                Event.current.Use();

                // Log for debugging
                Log.Message("Space key intercepted during architect placement mode");

                // Don't let TimeControls process this event
                return false;
            }

            // Allow normal processing for other events
            return true;
        }
    }

    /// <summary>
    /// Harmony patch to render visual feedback during architect placement.
    /// Shows selected cells and current designation area.
    /// </summary>
    [HarmonyPatch(typeof(SelectionDrawer))]
    [HarmonyPatch("DrawSelectionOverlays")]
    public static class ArchitectPlacementVisualizationPatch
    {
        /// <summary>
        /// Postfix to draw visual indicators for selected cells during architect placement.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Only active when in architect placement mode
            if (!ArchitectState.IsInPlacementMode)
                return;

            Map map = Find.CurrentMap;
            if (map == null)
                return;

            // Draw highlights for selected cells (for multi-cell designators)
            foreach (IntVec3 cell in ArchitectState.SelectedCells)
            {
                if (cell.InBounds(map))
                {
                    // Draw a subtle highlight over selected cells
                    Graphics.DrawMesh(
                        MeshPool.plane10,
                        cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                        Quaternion.identity,
                        GenDraw.InteractionCellMaterial,
                        0
                    );
                }
            }

            // Draw highlight for current cursor position
            IntVec3 cursorPos = MapNavigationState.CurrentCursorPosition;
            if (cursorPos.InBounds(map))
            {
                // Use a different color for the current cursor
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    cursorPos.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                    Quaternion.identity,
                    GenDraw.InteractionCellMaterial,
                    0
                );
            }
        }
    }
}
