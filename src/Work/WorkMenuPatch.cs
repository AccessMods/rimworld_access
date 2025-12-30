using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch that intercepts keyboard input when the work menu is active.
    /// Handles navigation (Up/Down arrows), toggling (Space), and confirmation (Enter/Escape).
    /// </summary>
    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class WorkMenuPatch
    {
        /// <summary>
        /// Prefix patch that intercepts keyboard events when work menu is active.
        /// Returns false to prevent game from processing the event if we handle it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            // Only process if work menu is active
            if (!WorkMenuState.IsActive)
                return;

            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            bool handled = false;
            KeyCode key = Event.current.keyCode;
            bool shift = Event.current.shift;

            // Handle Shift+Up: Reorder work type up (manual priority mode only)
            if (key == KeyCode.UpArrow && shift)
            {
                WorkMenuState.ReorderWorkTypeUp();
                handled = true;
            }
            // Handle Shift+Down: Reorder work type down (manual priority mode only)
            else if (key == KeyCode.DownArrow && shift)
            {
                WorkMenuState.ReorderWorkTypeDown();
                handled = true;
            }
            // Handle Arrow Up: Navigate up
            else if (key == KeyCode.UpArrow)
            {
                WorkMenuState.MoveUp();
                handled = true;
            }
            // Handle Arrow Down: Navigate down
            else if (key == KeyCode.DownArrow)
            {
                WorkMenuState.MoveDown();
                handled = true;
            }
            // Handle Tab: Switch to next pawn
            else if (key == KeyCode.Tab && !shift)
            {
                WorkMenuState.SwitchToNextPawn();
                handled = true;
            }
            // Handle Shift+Tab: Switch to previous pawn
            else if (key == KeyCode.Tab && shift)
            {
                WorkMenuState.SwitchToPreviousPawn();
                handled = true;
            }
            // Handle M: Toggle manual priority mode
            else if (key == KeyCode.M)
            {
                WorkMenuState.ToggleMode();
                handled = true;
            }
            // Handle number keys 0-4: Set priority directly (manual priority mode)
            else if (key == KeyCode.Alpha0 || key == KeyCode.Keypad0)
            {
                WorkMenuState.SetPriority(0);
                handled = true;
            }
            else if (key == KeyCode.Alpha1 || key == KeyCode.Keypad1)
            {
                WorkMenuState.SetPriority(1);
                handled = true;
            }
            else if (key == KeyCode.Alpha2 || key == KeyCode.Keypad2)
            {
                WorkMenuState.SetPriority(2);
                handled = true;
            }
            else if (key == KeyCode.Alpha3 || key == KeyCode.Keypad3)
            {
                WorkMenuState.SetPriority(3);
                handled = true;
            }
            else if (key == KeyCode.Alpha4 || key == KeyCode.Keypad4)
            {
                WorkMenuState.SetPriority(4);
                handled = true;
            }
            // Handle Space: Toggle selected work type
            else if (key == KeyCode.Space)
            {
                WorkMenuState.ToggleSelected();
                handled = true;
            }
            // Handle Enter/Return: Confirm and apply changes
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                WorkMenuState.Confirm();
                handled = true;
            }
            // Handle Escape: Cancel without applying changes
            else if (key == KeyCode.Escape)
            {
                WorkMenuState.Cancel();
                handled = true;
            }

            // Consume the event if we handled it
            if (handled)
            {
                Event.current.Use();
            }
        }

        /// <summary>
        /// Postfix patch that draws visual feedback for the work menu.
        /// Shows a highlighted overlay for the currently selected work type.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Only draw if work menu is active
            if (!WorkMenuState.IsActive)
                return;

            // Draw a semi-transparent overlay to indicate menu is active
            DrawMenuOverlay();
        }

        /// <summary>
        /// Draws a visual overlay indicating the work menu is active.
        /// Shows instructions and current selection.
        /// </summary>
        private static void DrawMenuOverlay()
        {
            // Get screen dimensions
            float screenWidth = UI.screenWidth;
            float screenHeight = UI.screenHeight;

            // Create a rect for the overlay (top-center of screen)
            float overlayWidth = 700f;
            float overlayHeight = 140f;
            float overlayX = (screenWidth - overlayWidth) / 2f;
            float overlayY = 20f;

            Rect overlayRect = new Rect(overlayX, overlayY, overlayWidth, overlayHeight);

            // Draw semi-transparent background
            Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            Widgets.DrawBoxSolid(overlayRect, backgroundColor);

            // Draw border
            Color borderColor = new Color(0.5f, 0.7f, 1.0f, 1.0f);
            Widgets.DrawBox(overlayRect, 2);

            // Draw text
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            string pawnName = WorkMenuState.CurrentPawn != null ? WorkMenuState.CurrentPawn.LabelShort : "Unknown";
            int pawnIndex = WorkMenuState.CurrentPawnIndex + 1;
            int totalPawns = WorkMenuState.TotalPawns;
            string mode = Find.PlaySettings.useWorkPriorities ? "Manual Priorities" : "Simple Mode";

            string title = $"Work Assignment Menu - {pawnName} ({pawnIndex}/{totalPawns}) - {mode}";

            string instructions1 = "Arrows: Navigate | Tab/Shift+Tab: Switch Pawn | M: Toggle Mode";
            string instructions2 = Find.PlaySettings.useWorkPriorities
                ? "0-4: Set Priority | Shift+Arrows: Reorder | Space: Toggle | Enter: Confirm"
                : "Space: Toggle | Enter: Confirm | Escape: Cancel";

            Rect titleRect = new Rect(overlayX, overlayY + 10f, overlayWidth, 30f);
            Rect instructions1Rect = new Rect(overlayX, overlayY + 45f, overlayWidth, 25f);
            Rect instructions2Rect = new Rect(overlayX, overlayY + 75f, overlayWidth, 25f);

            Widgets.Label(titleRect, title);

            Text.Font = GameFont.Tiny;
            Widgets.Label(instructions1Rect, instructions1);
            Widgets.Label(instructions2Rect, instructions2);

            // Reset text anchor
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
