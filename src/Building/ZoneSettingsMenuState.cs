using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a unified zone settings menu accessible via Z key when cursor is on a zone.
    /// Provides options for storage settings, plant selection, zone expansion, and deletion.
    /// </summary>
    public static class ZoneSettingsMenuState
    {
        private static List<MenuOption> currentOptions = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static Zone currentZone = null;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the zone settings menu for the specified zone.
        /// </summary>
        public static void Open(Zone zone)
        {
            if (zone == null)
            {
                Log.Error("Cannot open zone settings menu: zone is null");
                return;
            }

            currentZone = zone;
            BuildMenuOptions();
            selectedIndex = 0;
            isActive = true;

            // Announce menu opened and first option
            TolkHelper.Speak($"Zone settings for {zone.label}");
            AnnounceCurrentOption();

            Log.Message($"Opened zone settings menu for: {zone.label}");
        }

        /// <summary>
        /// Closes the zone settings menu.
        /// </summary>
        public static void Close()
        {
            currentOptions = null;
            selectedIndex = 0;
            isActive = false;
            currentZone = null;
        }

        /// <summary>
        /// Moves selection to next option.
        /// </summary>
        public static void SelectNext()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % currentOptions.Count;
            AnnounceCurrentOption();
        }

        /// <summary>
        /// Moves selection to previous option.
        /// </summary>
        public static void SelectPrevious()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + currentOptions.Count) % currentOptions.Count;
            AnnounceCurrentOption();
        }

        /// <summary>
        /// Executes the currently selected option.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count)
                return;

            MenuOption selected = currentOptions[selectedIndex];

            // Execute the action
            selected.Action?.Invoke();
        }

        private static void AnnounceCurrentOption()
        {
            if (selectedIndex >= 0 && selectedIndex < currentOptions.Count)
            {
                TolkHelper.Speak(currentOptions[selectedIndex].Label);
            }
        }

        /// <summary>
        /// Builds the list of menu options based on the current zone type.
        /// Universal options (Expand, Delete) come first, then zone-specific settings.
        /// </summary>
        private static void BuildMenuOptions()
        {
            currentOptions = new List<MenuOption>();

            if (currentZone == null)
                return;

            // Universal options for all zone types (shown first)

            // Edit zone (expand/shrink)
            currentOptions.Add(new MenuOption(
                "Edit Zone",
                ExpandCurrentZone
            ));

            // Rename zone
            currentOptions.Add(new MenuOption(
                "Rename Zone",
                RenameCurrentZone
            ));

            // Delete zone
            currentOptions.Add(new MenuOption(
                "Delete Zone",
                DeleteCurrentZone
            ));

            // Zone-specific options (shown after universal options)
            if (currentZone is Zone_Stockpile stockpile)
            {
                // Configure storage settings
                currentOptions.Add(new MenuOption(
                    "Storage Settings",
                    () => {
                        StorageSettings settings = stockpile.GetStoreSettings();
                        if (settings != null)
                        {
                            Close(); // Close this menu before opening storage settings
                            StorageSettingsMenuState.Open(settings);
                            TolkHelper.Speak($"Storage settings for {currentZone.label}");
                        }
                        else
                        {
                            TolkHelper.Speak("Cannot access storage settings", SpeechPriority.High);
                        }
                    }
                ));
            }
            else if (currentZone is Zone_Growing growingZone)
            {
                // Plant selection menu
                currentOptions.Add(new MenuOption(
                    "Plant Settings",
                    () => {
                        Close(); // Close this menu before opening plant selection
                        PlantSelectionMenuState.Open(growingZone);
                        TolkHelper.Speak($"Plant selection for {currentZone.label}");
                    }
                ));

                // Auto-sow toggle
                currentOptions.Add(new MenuOption(
                    $"Auto-Sow: {(growingZone.allowSow ? "On" : "Off")}",
                    () => {
                        growingZone.allowSow = !growingZone.allowSow;
                        TolkHelper.Speak($"Auto-Sow {(growingZone.allowSow ? "enabled" : "disabled")}");
                        BuildMenuOptions(); // Refresh to update label
                        AnnounceCurrentOption();
                    }
                ));

                // Auto-harvest toggle
                currentOptions.Add(new MenuOption(
                    $"Auto-Harvest: {(growingZone.allowCut ? "On" : "Off")}",
                    () => {
                        growingZone.allowCut = !growingZone.allowCut;
                        TolkHelper.Speak($"Auto-Harvest {(growingZone.allowCut ? "enabled" : "disabled")}");
                        BuildMenuOptions(); // Refresh
                        AnnounceCurrentOption();
                    }
                ));
            }

            // Cancel (always last)
            currentOptions.Add(new MenuOption(
                "Cancel",
                Close
            ));
        }

        /// <summary>
        /// Expands the current zone by entering expansion mode.
        /// </summary>
        private static void ExpandCurrentZone()
        {
            if (currentZone == null)
            {
                TolkHelper.Speak("Cannot expand: no zone selected", SpeechPriority.High);
                return;
            }

            Zone zoneToExpand = currentZone; // Save reference before closing
            Close(); // Close menu before entering expansion mode
            ZoneCreationState.EnterExpansionMode(zoneToExpand);
        }

        /// <summary>
        /// Renames the current zone by opening the rename dialog.
        /// </summary>
        private static void RenameCurrentZone()
        {
            if (currentZone == null)
            {
                TolkHelper.Speak("Cannot rename: no zone selected", SpeechPriority.High);
                return;
            }

            Zone zoneToRename = currentZone; // Save reference before closing
            Close(); // Close menu before opening rename dialog
            ZoneRenameState.Open(zoneToRename);
        }

        /// <summary>
        /// Deletes the current zone after confirmation.
        /// </summary>
        private static void DeleteCurrentZone()
        {
            if (currentZone == null)
            {
                TolkHelper.Speak("Cannot delete: no zone selected", SpeechPriority.High);
                return;
            }

            string zoneName = currentZone.label;
            Zone zoneToDelete = currentZone;

            Close(); // Close menu before opening confirmation

            WindowlessConfirmationState.Open(
                $"Delete {zoneName}? This cannot be undone.",
                () => {
                    try
                    {
                        zoneToDelete.Delete(playSound: true); // RimWorld handles all cleanup
                        TolkHelper.Speak($"Deleted {zoneName}", SpeechPriority.High);
                        Log.Message($"Deleted zone: {zoneName}");
                    }
                    catch (Exception ex)
                    {
                        TolkHelper.Speak($"Error deleting zone: {ex.Message}", SpeechPriority.High);
                        Log.Error($"Error deleting zone: {ex}");
                    }
                }
            );
        }

        /// <summary>
        /// Simple data structure for menu options.
        /// </summary>
        private class MenuOption
        {
            public string Label { get; }
            public Action Action { get; }

            public MenuOption(string label, Action action)
            {
                Label = label;
                Action = action;
            }
        }
    }
}
