using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch for CameraDriver.Update() to add accessible map navigation.
    /// Intercepts arrow key input to move a cursor tile-by-tile instead of panning the camera.
    /// The camera follows the cursor, keeping it centered on screen.
    /// </summary>
    [HarmonyPatch(typeof(CameraDriver))]
    [HarmonyPatch("Update")]
    public static class MapNavigationPatch
    {
        private static bool hasAnnouncedThisFrame = false;
        private static int lastProcessedFrame = -1;

        /// <summary>
        /// Updates the map navigation suppression flag based on active menus.
        /// </summary>
        private static void UpdateSuppressionFlag()
        {
            // Suppress map navigation if ANY menu that uses arrow keys is active
            // Note: Scanner is NOT included here because it doesn't suppress map navigation
            MapNavigationState.SuppressMapNavigation =
                WorldNavigationState.IsActive ||
                WindowlessDialogState.IsActive ||
                WindowlessFloatMenuState.IsActive ||
                ShapeSelectionMenuState.IsActive ||
                // Note: ViewingModeState is NOT included - it allows arrow navigation for moving around
                ArchitectTreeState.IsActive ||
                CaravanFormationState.IsActive ||
                WindowlessPauseMenuState.IsActive ||
                NotificationMenuState.IsActive ||
                QuestMenuState.IsActive ||
                WindowlessSaveMenuState.IsActive ||
                WindowlessConfirmationState.IsActive ||
                WindowlessDeleteConfirmationState.IsActive ||
                WindowlessOptionsMenuState.IsActive ||
                ZoneRenameState.IsActive ||
                PlaySettingsMenuState.IsActive ||
                StorageSettingsMenuState.IsActive ||
                PlantSelectionMenuState.IsActive ||
                RangeEditMenuState.IsActive ||
                WorkMenuState.IsActive ||
                AssignMenuState.IsActive ||
                WindowlessOutfitPolicyState.IsActive ||
                WindowlessFoodPolicyState.IsActive ||
                WindowlessDrugPolicyState.IsActive ||
                WindowlessAreaState.IsActive ||
                WindowlessScheduleState.IsActive ||
                BillsMenuState.IsActive ||
                PrisonerTabState.IsActive ||
                BillConfigState.IsActive ||
                ThingFilterMenuState.IsActive ||
                TempControlMenuState.IsActive ||
                BedAssignmentState.IsActive ||
                WindowlessResearchMenuState.IsActive ||
                WindowlessResearchDetailState.IsActive ||
                WindowlessInspectionState.IsActive ||
                WindowlessInventoryState.IsActive ||
                HealthTabState.IsActive ||
                FlickableComponentState.IsActive ||
                RefuelableComponentState.IsActive ||
                BreakdownableComponentState.IsActive ||
                DoorControlState.IsActive ||
                ForbidControlState.IsActive ||
                AnimalsMenuState.IsActive ||
                WildlifeMenuState.IsActive ||
                TransportPodLoadingState.IsActive ||
                // History tab states
                HistoryState.IsActive ||
                HistoryStatisticsState.IsActive ||
                HistoryMessagesState.IsActive;
                // Note: TransportPodSelectionState is NOT included - it uses map navigation for cursor movement
        }

        /// <summary>
        /// Prefix patch that intercepts arrow key input before the camera's normal panning behavior.
        /// Returns false to skip original CameraDriver.Update() when menus are active (prevents camera panning in menus).
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(CameraDriver __instance)
        {
            // Reset per-frame flag
            hasAnnouncedThisFrame = false;

            // Update suppression flag based on active menus
            UpdateSuppressionFlag();

            // Only process input during normal gameplay with a valid map
            if (Find.CurrentMap == null)
            {
                MapNavigationState.Reset();
                return true; // Let original run
            }

            // Don't process arrow keys if any dialog or window that prevents camera motion is open
            if (Find.WindowStack != null && Find.WindowStack.WindowsPreventCameraMotion)
            {
                return true; // Let original run (it will also respect this flag)
            }

            // Prevent processing input multiple times in the same frame
            // (Update() can be called multiple times per frame)
            int currentFrame = Time.frameCount;
            if (lastProcessedFrame == currentFrame)
            {
                return true;
            }
            lastProcessedFrame = currentFrame;

            // Check for map additions/removals and announce to user
            MapNavigationState.CheckForMapChanges();

            // Initialize cursor position if needed - MUST happen before suppression check
            // so that new maps get initialized even if a menu is temporarily active
            if (!MapNavigationState.IsInitialized)
            {
                MapNavigationState.Initialize(Find.CurrentMap);

                // Announce starting position
                string initialInfo = TileInfoHelper.GetTileSummary(MapNavigationState.CurrentCursorPosition, Find.CurrentMap);
                TolkHelper.Speak(initialInfo);
                MapNavigationState.LastAnnouncedInfo = initialInfo;
                hasAnnouncedThisFrame = true;
                return true;
            }

            // When menus are open, skip the original CameraDriver.Update() entirely
            // This prevents arrow keys from panning the camera while in menus
            if (MapNavigationState.SuppressMapNavigation)
            {
                return false; // SKIP original - don't let camera pan in menus
            }

            // Check for map switching (Shift+comma/period)
            // Regular comma/period pawn cycling is handled by ThingSelectionUtilityPatch
            bool shiftHeldForMapSwitch = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (shiftHeldForMapSwitch && Input.GetKeyDown(KeyCode.Period))
            {
                HandleMapSwitching(forward: true);
                return true;
            }
            else if (shiftHeldForMapSwitch && Input.GetKeyDown(KeyCode.Comma))
            {
                HandleMapSwitching(forward: false);
                return true;
            }
            // Note: Regular comma/period without shift passes through to game's ShortcutKeys
            // which calls ThingSelectionUtility.SelectNext/PreviousColonist()
            // Our ThingSelectionUtilityPatch intercepts those to filter by current map

            // Check for arrow key input
            IntVec3 moveOffset = IntVec3.Zero;
            bool keyPressed = false;
            bool isJump = false;

            // Check if Ctrl or Shift is held down
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Handle Shift+Up/Down for jump mode cycling
            // (but not when zone creation is active - that uses Shift+Arrows for auto-select to wall)
            bool blockJumpModeCycling = ZoneCreationState.IsInCreationMode;
            if (shiftHeld && !blockJumpModeCycling)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    MapNavigationState.CycleJumpModeForward();
                    hasAnnouncedThisFrame = true;
                    return true;
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    MapNavigationState.CycleJumpModeBackward();
                    hasAnnouncedThisFrame = true;
                    return true;
                }
                // Shift+Left/Right adjusts preset distance (only in PresetDistance mode)
                else if (MapNavigationState.CurrentJumpMode == JumpMode.PresetDistance)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        MapNavigationState.DecreasePresetDistance();
                        hasAnnouncedThisFrame = true;
                        return true;
                    }
                    else if (Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        MapNavigationState.IncreasePresetDistance();
                        hasAnnouncedThisFrame = true;
                        return true;
                    }
                }
            }

            // Check each arrow key direction
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                moveOffset = IntVec3.North; // North is positive Z
                keyPressed = true;
                isJump = ctrlHeld;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                moveOffset = IntVec3.South; // South is negative Z
                keyPressed = true;
                isJump = ctrlHeld;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                moveOffset = IntVec3.West; // West is negative X
                keyPressed = true;
                isJump = ctrlHeld;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                moveOffset = IntVec3.East; // East is positive X
                keyPressed = true;
                isJump = ctrlHeld;
            }

            // If an arrow key was pressed, move the cursor and update camera
            if (keyPressed)
            {
                // Move the cursor position - either jump or normal movement
                bool positionChanged;
                if (isJump)
                {
                    // Use appropriate jump method based on current jump mode
                    switch (MapNavigationState.CurrentJumpMode)
                    {
                        case JumpMode.Terrain:
                            positionChanged = MapNavigationState.JumpToNextTerrainType(moveOffset, Find.CurrentMap);
                            break;
                        case JumpMode.Buildings:
                            positionChanged = MapNavigationState.JumpToNextBuilding(moveOffset, Find.CurrentMap);
                            break;
                        case JumpMode.Geysers:
                            positionChanged = MapNavigationState.JumpToNextGeyser(moveOffset, Find.CurrentMap);
                            break;
                        case JumpMode.HarvestableTrees:
                            positionChanged = MapNavigationState.JumpToNextHarvestableTrees(moveOffset, Find.CurrentMap);
                            break;
                        case JumpMode.MinableTiles:
                            positionChanged = MapNavigationState.JumpToNextMinableTiles(moveOffset, Find.CurrentMap);
                            break;
                        case JumpMode.PresetDistance:
                            positionChanged = MapNavigationState.JumpPresetDistance(moveOffset, Find.CurrentMap);
                            break;
                        default:
                            positionChanged = MapNavigationState.MoveCursor(moveOffset, Find.CurrentMap);
                            break;
                    }
                }
                else
                {
                    positionChanged = MapNavigationState.MoveCursor(moveOffset, Find.CurrentMap);
                }

                if (positionChanged)
                {
                    // Clear the "pawn just selected" flag since user is now navigating the map
                    GizmoNavigationState.PawnJustSelected = false;

                    // Get the new cursor position
                    IntVec3 newPosition = MapNavigationState.CurrentCursorPosition;

                    // Update rectangle preview if in zone creation mode with a start corner
                    if (ZoneCreationState.IsInCreationMode && ZoneCreationState.HasRectangleStart)
                    {
                        ZoneCreationState.UpdatePreview(newPosition);
                    }

                    // Update rectangle preview if in area painting mode with a start corner
                    if (AreaPaintingState.IsActive && AreaPaintingState.HasRectangleStart)
                    {
                        AreaPaintingState.UpdatePreview(newPosition);
                    }

                    // Note: Shape preview update for architect placement is now handled by
                    // ShapePlacementState.UpdatePreview() in ArchitectPlacementAnnouncementPatch

                    // Move camera to center on new cursor position
                    __instance.JumpToCurrentMapLoc(newPosition);

                    // Play terrain audio feedback
                    TerrainDef terrain = newPosition.GetTerrain(Find.CurrentMap);
                    TerrainAudioHelper.PlayTerrainAudio(terrain, 0.5f);

                    // Get tile information and announce it
                    string tileInfo = TileInfoHelper.GetTileSummary(newPosition, Find.CurrentMap);

                    // If in zone creation mode, prepend selection state
                    if (ZoneCreationState.IsInCreationMode)
                    {
                        if (ZoneCreationState.IsInPreviewMode && ZoneCreationState.PreviewCells.Contains(newPosition))
                        {
                            tileInfo = "Preview, " + tileInfo;
                        }
                        else if (ZoneCreationState.IsCellSelected(newPosition))
                        {
                            tileInfo = "Selected, " + tileInfo;
                        }
                    }
                    // If in area painting mode, prepend selection/preview state
                    else if (AreaPaintingState.IsActive)
                    {
                        if (AreaPaintingState.IsInPreviewMode && AreaPaintingState.PreviewCells.Contains(newPosition))
                        {
                            tileInfo = "Preview, " + tileInfo;
                        }
                        else if (AreaPaintingState.StagedCells.Contains(newPosition))
                        {
                            tileInfo = "Selected, " + tileInfo;
                        }
                    }
                    // If in architect mode, prepend selection/preview state
                    // Shape preview is handled via ShapePlacementState
                    else if (ArchitectState.IsInPlacementMode)
                    {
                        if (ShapePlacementState.IsActive && ShapePlacementState.PreviewCells.Contains(newPosition))
                        {
                            // Only label corners, not intermediate tiles
                            if (ShapePlacementState.FirstCorner.HasValue && newPosition == ShapePlacementState.FirstCorner.Value)
                            {
                                tileInfo = "First corner, " + tileInfo;
                            }
                            else if (ShapePlacementState.SecondCorner.HasValue && newPosition == ShapePlacementState.SecondCorner.Value)
                            {
                                tileInfo = "Second corner, " + tileInfo;
                            }
                            // No else - intermediate tiles in preview don't get a prefix
                        }
                        else if (ArchitectState.SelectedCells.Contains(newPosition))
                        {
                            tileInfo = "Selected, " + tileInfo;
                        }
                    }

                    // Only announce if different from last announcement (avoids spam when hitting map edge)
                    if (tileInfo != MapNavigationState.LastAnnouncedInfo)
                    {
                        TolkHelper.Speak(tileInfo);
                        MapNavigationState.LastAnnouncedInfo = tileInfo;
                        hasAnnouncedThisFrame = true;
                    }
                }
                else
                {
                    // Cursor at map boundary - optionally announce boundary
                    if (!hasAnnouncedThisFrame)
                    {
                        TolkHelper.Speak("Map boundary");
                        hasAnnouncedThisFrame = true;
                    }
                }

                // Consume the arrow key event to prevent default camera panning
                // This is done by preventing the KeyBindingDefOf checks from succeeding
                // Note: We're using Input.GetKeyDown instead of KeyBindingDefOf to intercept earlier
            }

            // Let original CameraDriver.Update() run for non-arrow-key functionality
            // (zoom, following, etc.) - we've already handled our arrow key navigation above
            return true;
        }

        /// <summary>
        /// Handles switching between maps when Shift+comma or Shift+period is pressed.
        /// Restores cursor to last known position on the target map.
        /// </summary>
        /// <param name="forward">True for Shift+period (next map), false for Shift+comma (previous map)</param>
        private static void HandleMapSwitching(bool forward)
        {
            int mapCount = PawnSelectionState.GetMapCount();

            if (mapCount <= 1)
            {
                TolkHelper.Speak("Only one map available");
                hasAnnouncedThisFrame = true;
                return;
            }

            // Switch to the next/previous map
            Pawn focusPawn = forward
                ? PawnSelectionState.SwitchToNextMap(out string mapName, out int pawnCount)
                : PawnSelectionState.SwitchToPreviousMap(out mapName, out pawnCount);

            // Check if map switch actually happened (mapName will be set if successful)
            if (string.IsNullOrEmpty(mapName))
            {
                TolkHelper.Speak("Could not switch maps");
                hasAnnouncedThisFrame = true;
                return;
            }

            // Restore cursor to last known position for this map
            MapNavigationState.RestoreCursorForCurrentMap();

            // Invalidate scanner cache so it refreshes for the new map
            ScannerState.Invalidate();

            // Clear any selection when switching maps
            if (Find.Selector != null)
            {
                Find.Selector.ClearSelection();
            }

            // Build announcement: "Now at [MapName] ([X] colonists)"
            string colonistWord = pawnCount == 1 ? "colonist" : "colonists";
            string fullAnnouncement;
            if (pawnCount == 0)
            {
                fullAnnouncement = $"Now at {mapName}. No colonists here.";
            }
            else
            {
                fullAnnouncement = $"Now at {mapName} ({pawnCount} {colonistWord})";
            }
            TolkHelper.Speak(fullAnnouncement);
            MapNavigationState.LastAnnouncedInfo = fullAnnouncement;
            hasAnnouncedThisFrame = true;
        }

        /// <summary>
        /// Postfix patch to prevent default camera dolly movement when we've handled arrow keys.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(CameraDriver __instance)
        {
            // If we announced something this frame, it means we handled arrow key input
            // The camera jump already happened in Prefix, but we need to ensure
            // no residual dolly movement occurs
            if (hasAnnouncedThisFrame)
            {
                // Reset velocity to prevent any accumulated movement
                Traverse.Create(__instance).Field("velocity").SetValue(Vector3.zero);
                Traverse.Create(__instance).Field("desiredDollyRaw").SetValue(Vector2.zero);
            }
        }
    }

    /// <summary>
    /// Harmony patches for ThingSelectionUtility to override the game's colonist cycling.
    /// By default, the game cycles through ALL colonists across all maps.
    /// We override this to only cycle through colonists on the CURRENT map.
    /// Shift+comma/period for map switching is handled separately in MapNavigationPatch.
    /// </summary>
    [HarmonyPatch(typeof(ThingSelectionUtility))]
    public static class ThingSelectionUtilityPatch
    {
        /// <summary>
        /// Prefix patch for SelectNextColonist to filter by current map.
        /// </summary>
        [HarmonyPatch("SelectNextColonist")]
        [HarmonyPrefix]
        public static bool SelectNextColonist_Prefix()
        {
            // If world view is selected, let the original method handle it (caravan cycling)
            if (WorldRendererUtility.WorldRendered)
                return true;

            // Check if shift is held - if so, this is a map switch request
            // Let our HandleMapSwitching in MapNavigationPatch handle it (it already ran)
            // Just block the original to prevent double-handling
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld)
                return false; // Block original - our map switching already handled it

            // Use our map-filtered pawn cycling
            Pawn selectedPawn = PawnSelectionState.SelectNextColonist();

            if (selectedPawn == null)
            {
                TolkHelper.Speak("No colonists on this map");
                return false;
            }

            // Select the pawn (but don't jump camera - user can use Alt+C)
            if (Find.Selector != null)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(selectedPawn);
            }

            // Set flag for gizmo navigation
            GizmoNavigationState.PawnJustSelected = true;

            // Announce selection
            string currentTask = selectedPawn.GetJobReport();
            if (string.IsNullOrEmpty(currentTask))
                currentTask = "Idle";

            TolkHelper.Speak($"{selectedPawn.LabelShort} selected - {currentTask}");

            return false; // Block original method
        }

        /// <summary>
        /// Prefix patch for SelectPreviousColonist to filter by current map.
        /// </summary>
        [HarmonyPatch("SelectPreviousColonist")]
        [HarmonyPrefix]
        public static bool SelectPreviousColonist_Prefix()
        {
            // If world view is selected, let the original method handle it (caravan cycling)
            if (WorldRendererUtility.WorldRendered)
                return true;

            // Check if shift is held - if so, this is a map switch request
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld)
                return false; // Block original - our map switching already handled it

            // Use our map-filtered pawn cycling
            Pawn selectedPawn = PawnSelectionState.SelectPreviousColonist();

            if (selectedPawn == null)
            {
                TolkHelper.Speak("No colonists on this map");
                return false;
            }

            // Select the pawn (but don't jump camera - user can use Alt+C)
            if (Find.Selector != null)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(selectedPawn);
            }

            // Set flag for gizmo navigation
            GizmoNavigationState.PawnJustSelected = true;

            // Announce selection
            string currentTask = selectedPawn.GetJobReport();
            if (string.IsNullOrEmpty(currentTask))
                currentTask = "Idle";

            TolkHelper.Speak($"{selectedPawn.LabelShort} selected - {currentTask}");

            return false; // Block original method
        }
    }
}
