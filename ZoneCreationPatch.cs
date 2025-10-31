using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to handle input during zone creation mode.
    /// Handles Space (add cell), Enter (confirm), and Escape (cancel).
    /// Also modifies arrow key announcements to include "Selected" prefix for selected cells.
    /// </summary>
    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class ZoneCreationInputPatch
    {
        private static float lastSpaceTime = 0f;
        private const float SpaceCooldown = 0.2f;

        /// <summary>
        /// Prefix patch to handle zone creation input at GUI event level.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Normal)] // Run after ZoneMenuPatch, before OrderGivingPatch
        public static void Prefix()
        {
            // Only active when in zone creation mode
            if (!ZoneCreationState.IsInCreationMode)
                return;

            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            // Check we have a valid map
            if (Find.CurrentMap == null)
            {
                ZoneCreationState.Cancel();
                return;
            }

            KeyCode key = Event.current.keyCode;
            bool handled = false;

            // Space key - toggle selection of current cell
            if (key == KeyCode.Space)
            {
                // Cooldown to prevent rapid toggling
                if (Time.time - lastSpaceTime < SpaceCooldown)
                    return;

                lastSpaceTime = Time.time;

                IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;
                
                if (!ZoneCreationState.IsCellSelected(currentPosition))
                {
                    ZoneCreationState.AddCell(currentPosition);
                }
                else
                {
                    ZoneCreationState.RemoveCell(currentPosition);
                }
                
                handled = true;
            }
            // Enter key - confirm and create zone
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                ZoneCreationState.CreateZone(Find.CurrentMap);
                handled = true;
            }
            // Escape key - cancel zone creation
            else if (key == KeyCode.Escape)
            {
                ZoneCreationState.Cancel();
                handled = true;
            }

            if (handled)
            {
                Event.current.Use();
            }
        }
    }

    /// <summary>
    /// Harmony patch to modify map navigation announcements during zone creation.
    /// </summary>
    [HarmonyPatch(typeof(CameraDriver))]
    [HarmonyPatch("Update")]
    public static class ZoneCreationAnnouncementPatch
    {
        /// <summary>
        /// Postfix patch to modify tile announcements during zone creation.
        /// Prepends "Selected" to coordinates for cells that are in the selection.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)] // Run after MapNavigationPatch
        public static void Postfix(CameraDriver __instance)
        {
            // Only active when in zone creation or area painting mode
            if (!ZoneCreationState.IsInCreationMode && !AreaPaintingState.IsActive)
                return;

            // Check if an arrow key was just pressed
            if (Find.CurrentMap == null || !MapNavigationState.IsInitialized)
                return;

            // Check if any arrow key was pressed this frame
            bool arrowKeyPressed = Input.GetKeyDown(KeyCode.UpArrow) ||
                                   Input.GetKeyDown(KeyCode.DownArrow) ||
                                   Input.GetKeyDown(KeyCode.LeftArrow) ||
                                   Input.GetKeyDown(KeyCode.RightArrow);

            if (arrowKeyPressed)
            {
                IntVec3 currentPosition = MapNavigationState.CurrentCursorPosition;

                // Check if cell is selected in either zone creation or area painting
                bool isSelected = false;
                if (ZoneCreationState.IsInCreationMode)
                {
                    isSelected = ZoneCreationState.IsCellSelected(currentPosition);
                }
                else if (AreaPaintingState.IsActive)
                {
                    isSelected = AreaPaintingState.StagedCells.Contains(currentPosition);
                }

                // If this cell is selected, prepend "Selected" to the announcement
                if (isSelected)
                {
                    // Get the last announced info and modify it
                    string lastInfo = MapNavigationState.LastAnnouncedInfo;

                    // Only modify if it doesn't already start with "Selected"
                    if (!lastInfo.StartsWith("Selected"))
                    {
                        string modifiedInfo = "Selected, " + lastInfo;
                        ClipboardHelper.CopyToClipboard(modifiedInfo);
                        MapNavigationState.LastAnnouncedInfo = modifiedInfo;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Harmony patch to intercept pause key (Space) during zone creation mode.
    /// Prevents Space from pausing the game when in zone creation mode.
    /// </summary>
    [HarmonyPatch(typeof(TimeControls))]
    [HarmonyPatch("DoTimeControlsGUI")]
    public static class ZoneCreationTimeControlsPatch
    {
        /// <summary>
        /// Prefix patch that intercepts the pause key event during zone creation.
        /// Returns false to skip TimeControls processing when Space is pressed in zone creation mode.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            // Only intercept when in zone creation mode
            if (!ZoneCreationState.IsInCreationMode)
                return true; // Continue with normal processing

            // Check if this is a KeyDown event for the pause toggle key
            if (Event.current.type == EventType.KeyDown && 
                KeyBindingDefOf.TogglePause.KeyDownEvent)
            {
                // Consume the event so TimeControls doesn't process it
                Event.current.Use();
                
                // Log for debugging
                MelonLoader.MelonLogger.Msg("Space key intercepted during zone creation mode");
                
                // Don't let TimeControls process this event
                return false;
            }

            // Allow normal processing for other events
            return true;
        }
    }
}
