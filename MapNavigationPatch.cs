using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

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
            MapNavigationState.SuppressMapNavigation =
                WindowlessFloatMenuState.IsActive ||
                WindowlessPauseMenuState.IsActive ||
                JumpMenuState.IsActive ||
                NotificationMenuState.IsActive ||
                QuestMenuState.IsActive ||
                WindowlessSaveMenuState.IsActive ||
                WindowlessConfirmationState.IsActive ||
                WindowlessDeleteConfirmationState.IsActive ||
                WindowlessOptionsMenuState.IsActive ||
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
                BuildingInspectState.IsActive ||
                BillsMenuState.IsActive ||
                PrisonerTabState.IsActive ||
                BillConfigState.IsActive ||
                ThingFilterMenuState.IsActive ||
                TempControlMenuState.IsActive ||
                BedAssignmentState.IsActive ||
                WindowlessResearchMenuState.IsActive ||
                WindowlessResearchDetailState.IsActive ||
                WindowlessInspectionState.IsActive;
        }

        /// <summary>
        /// Prefix patch that intercepts arrow key input before the camera's normal panning behavior.
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(CameraDriver __instance)
        {
            // Reset per-frame flag
            hasAnnouncedThisFrame = false;

            // Update suppression flag based on active menus
            UpdateSuppressionFlag();

            // Only process input during normal gameplay with a valid map
            if (Find.CurrentMap == null)
            {
                MapNavigationState.Reset();
                return;
            }

            // Don't process arrow keys if any dialog or window that prevents camera motion is open
            if (Find.WindowStack != null && Find.WindowStack.WindowsPreventCameraMotion)
            {
                return;
            }

            // Don't process arrow keys if map navigation is suppressed (e.g., when menus are open)
            if (MapNavigationState.SuppressMapNavigation)
            {
                return;
            }

            // Prevent processing input multiple times in the same frame
            // (Update() can be called multiple times per frame)
            int currentFrame = Time.frameCount;
            if (lastProcessedFrame == currentFrame)
            {
                return;
            }
            lastProcessedFrame = currentFrame;

            // Initialize cursor position if needed
            if (!MapNavigationState.IsInitialized)
            {
                MapNavigationState.Initialize(Find.CurrentMap);

                // Announce starting position
                string initialInfo = TileInfoHelper.GetTileSummary(MapNavigationState.CurrentCursorPosition, Find.CurrentMap);
                ClipboardHelper.CopyToClipboard(initialInfo);
                MapNavigationState.LastAnnouncedInfo = initialInfo;
                hasAnnouncedThisFrame = true;
                return;
            }

            // Check for pawn selection cycling (comma and period keys)
            if (Input.GetKeyDown(KeyCode.Period))
            {
                HandlePawnCycling(true, __instance);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.Comma))
            {
                HandlePawnCycling(false, __instance);
                return;
            }

            // Check for arrow key input
            IntVec3 moveOffset = IntVec3.Zero;
            bool keyPressed = false;

            // Check each arrow key direction
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                moveOffset = IntVec3.North; // North is positive Z
                keyPressed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                moveOffset = IntVec3.South; // South is negative Z
                keyPressed = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                moveOffset = IntVec3.West; // West is negative X
                keyPressed = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                moveOffset = IntVec3.East; // East is positive X
                keyPressed = true;
            }

            // If an arrow key was pressed, move the cursor and update camera
            if (keyPressed)
            {
                // Move the cursor position
                bool positionChanged = MapNavigationState.MoveCursor(moveOffset, Find.CurrentMap);

                if (positionChanged)
                {
                    // Get the new cursor position
                    IntVec3 newPosition = MapNavigationState.CurrentCursorPosition;

                    // Move camera to center on new cursor position
                    __instance.JumpToCurrentMapLoc(newPosition);

                    // Play terrain audio feedback
                    ModLogger.Msg($"Cursor moved to {newPosition}, attempting to play terrain audio");
                    TerrainDef terrain = newPosition.GetTerrain(Find.CurrentMap);
                    TerrainAudioHelper.PlayTerrainAudio(terrain, 0.5f);

                    // Get tile information and announce it
                    string tileInfo = TileInfoHelper.GetTileSummary(newPosition, Find.CurrentMap);

                    // Only announce if different from last announcement (avoids spam when hitting map edge)
                    if (tileInfo != MapNavigationState.LastAnnouncedInfo)
                    {
                        ClipboardHelper.CopyToClipboard(tileInfo);
                        MapNavigationState.LastAnnouncedInfo = tileInfo;
                        hasAnnouncedThisFrame = true;
                    }
                }
                else
                {
                    // Cursor at map boundary - optionally announce boundary
                    if (!hasAnnouncedThisFrame)
                    {
                        ClipboardHelper.CopyToClipboard("Map boundary");
                        hasAnnouncedThisFrame = true;
                    }
                }

                // Consume the arrow key event to prevent default camera panning
                // This is done by preventing the KeyBindingDefOf checks from succeeding
                // Note: We're using Input.GetKeyDown instead of KeyBindingDefOf to intercept earlier
            }
        }

        /// <summary>
        /// Handles pawn selection cycling when comma or period keys are pressed.
        /// </summary>
        /// <param name="cycleForward">True for period (next), false for comma (previous)</param>
        /// <param name="cameraDriver">The camera driver instance to move the camera</param>
        private static void HandlePawnCycling(bool cycleForward, CameraDriver cameraDriver)
        {
            // Select the next or previous pawn
            Pawn selectedPawn = cycleForward
                ? PawnSelectionState.SelectNextColonist()
                : PawnSelectionState.SelectPreviousColonist();

            if (selectedPawn == null)
            {
                // No colonists available to select
                ClipboardHelper.CopyToClipboard("No colonists available");
                hasAnnouncedThisFrame = true;
                return;
            }

            // Clear current selection and select the new pawn
            if (Find.Selector != null)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(selectedPawn);
            }

            // Get current task for the pawn
            string currentTask = selectedPawn.GetJobReport();
            if (string.IsNullOrEmpty(currentTask))
            {
                currentTask = "Idle";
            }

            // Announce the selected pawn's name and current task
            // Note: Camera does NOT jump - user can use Alt+C to manually jump to pawn
            string announcement = $"{selectedPawn.LabelShort} selected - {currentTask}";
            ClipboardHelper.CopyToClipboard(announcement);
            MapNavigationState.LastAnnouncedInfo = announcement;
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
}
