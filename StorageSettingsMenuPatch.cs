using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to handle keyboard input for storage settings and plant selection menus.
    /// Intercepts keyboard events when these menus are active.
    /// </summary>
    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class StorageSettingsMenuPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)] // Run before other patches
        public static void Prefix()
        {
            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            // Handle storage settings menu
            if (StorageSettingsMenuState.IsActive)
            {
                HandleStorageSettingsInput();
                return;
            }

            // Handle plant selection menu
            if (PlantSelectionMenuState.IsActive)
            {
                HandlePlantSelectionInput();
                return;
            }
        }

        private static void HandleStorageSettingsInput()
        {
            // Check if range edit submenu is active
            if (RangeEditMenuState.IsActive)
            {
                HandleRangeEditInput();
                return;
            }

            KeyCode key = Event.current.keyCode;

            switch (key)
            {
                case KeyCode.UpArrow:
                    StorageSettingsMenuState.SelectPrevious();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    StorageSettingsMenuState.SelectNext();
                    Event.current.Use();
                    break;

                case KeyCode.RightArrow:
                    StorageSettingsMenuState.ExpandOrToggleOn();
                    Event.current.Use();
                    break;

                case KeyCode.LeftArrow:
                    StorageSettingsMenuState.CollapseOrToggleOff();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    StorageSettingsMenuState.ToggleCurrent();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    StorageSettingsMenuState.Close();
                    ClipboardHelper.CopyToClipboard("Closed storage settings menu");
                    Event.current.Use();
                    break;
            }
        }

        private static void HandleRangeEditInput()
        {
            KeyCode key = Event.current.keyCode;

            switch (key)
            {
                case KeyCode.UpArrow:
                    RangeEditMenuState.SelectPrevious();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    RangeEditMenuState.SelectNext();
                    Event.current.Use();
                    break;

                case KeyCode.LeftArrow:
                    RangeEditMenuState.DecreaseValue();
                    Event.current.Use();
                    break;

                case KeyCode.RightArrow:
                    RangeEditMenuState.IncreaseValue();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Apply changes and return to storage settings menu
                    if (RangeEditMenuState.ApplyAndClose(out var hitPoints, out var quality))
                    {
                        StorageSettingsMenuState.ApplyRangeChanges(hitPoints, quality);
                        ClipboardHelper.CopyToClipboard("Applied range changes");
                    }
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    RangeEditMenuState.Close();
                    ClipboardHelper.CopyToClipboard("Cancelled range editing");
                    Event.current.Use();
                    break;
            }
        }

        private static void HandlePlantSelectionInput()
        {
            KeyCode key = Event.current.keyCode;

            switch (key)
            {
                case KeyCode.UpArrow:
                    PlantSelectionMenuState.SelectPrevious();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    PlantSelectionMenuState.SelectNext();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    PlantSelectionMenuState.ConfirmSelection();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    PlantSelectionMenuState.Close();
                    ClipboardHelper.CopyToClipboard("Closed plant selection menu");
                    Event.current.Use();
                    break;
            }
        }
    }
}
