using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to handle keyboard input for zone-related menus: zone settings, play settings, storage settings, and plant selection.
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
            // Handle zone rename text input (process both KeyDown and normal character input)
            if (ZoneRenameState.IsActive)
            {
                HandleZoneRenameInput();
                return;
            }

            // Only process keyboard events for other menus
            if (Event.current.type != EventType.KeyDown)
                return;

            // Handle zone settings menu
            if (ZoneSettingsMenuState.IsActive)
            {
                HandleZoneSettingsInput();
                return;
            }

            // Handle play settings menu
            if (PlaySettingsMenuState.IsActive)
            {
                HandlePlaySettingsInput();
                return;
            }

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
                    StorageSettingsMenuState.ExpandCurrent();
                    Event.current.Use();
                    break;

                case KeyCode.LeftArrow:
                    StorageSettingsMenuState.CollapseCurrent();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    StorageSettingsMenuState.ToggleCurrent();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    StorageSettingsMenuState.Close();
                    TolkHelper.Speak("Closed storage settings menu");
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
                        TolkHelper.Speak("Applied range changes");
                    }
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    RangeEditMenuState.Close();
                    TolkHelper.Speak("Cancelled range editing");
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
                    TolkHelper.Speak("Closed plant selection menu");
                    Event.current.Use();
                    break;
            }
        }

        private static void HandleZoneSettingsInput()
        {
            KeyCode key = Event.current.keyCode;

            switch (key)
            {
                case KeyCode.UpArrow:
                    ZoneSettingsMenuState.SelectPrevious();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    ZoneSettingsMenuState.SelectNext();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ZoneSettingsMenuState.ExecuteSelected();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    ZoneSettingsMenuState.Close();
                    TolkHelper.Speak("Closed zone settings menu");
                    Event.current.Use();
                    break;
            }
        }

        private static void HandlePlaySettingsInput()
        {
            KeyCode key = Event.current.keyCode;

            switch (key)
            {
                case KeyCode.UpArrow:
                    PlaySettingsMenuState.SelectPrevious();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    PlaySettingsMenuState.SelectNext();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    PlaySettingsMenuState.ExecuteSelected();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    PlaySettingsMenuState.Close();
                    TolkHelper.Speak("Closed play settings menu");
                    Event.current.Use();
                    break;
            }
        }

        private static void HandleZoneRenameInput()
        {
            Event currentEvent = Event.current;

            // Handle KeyDown events for special keys
            if (currentEvent.type == EventType.KeyDown)
            {
                KeyCode key = currentEvent.keyCode;

                // Check for special keys first
                if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    ZoneRenameState.Confirm();
                    currentEvent.Use();
                    return;
                }
                else if (key == KeyCode.Escape)
                {
                    ZoneRenameState.Cancel();
                    currentEvent.Use();
                    return;
                }
                else if (key == KeyCode.Backspace)
                {
                    ZoneRenameState.HandleBackspace();
                    currentEvent.Use();
                    return;
                }
                else if (key == KeyCode.Tab)
                {
                    ZoneRenameState.ReadCurrentText();
                    currentEvent.Use();
                    return;
                }

                // Handle character input from KeyDown event
                char character = currentEvent.character;

                // If there's a valid character, handle it
                if (character != '\0' && !char.IsControl(character))
                {
                    ZoneRenameState.HandleCharacter(character);
                    currentEvent.Use();
                    return;
                }
            }
        }
    }
}
