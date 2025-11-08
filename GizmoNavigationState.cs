using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Maintains the state of gizmo (command button) navigation for accessibility features.
    /// Tracks the currently selected gizmo when browsing available commands with the G key.
    /// </summary>
    public static class GizmoNavigationState
    {
        private static bool isActive = false;
        private static int selectedGizmoIndex = 0;
        private static List<Gizmo> availableGizmos = new List<Gizmo>();
        private static bool pawnJustSelected = false;

        /// <summary>
        /// Gets whether gizmo navigation is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Gets or sets whether a pawn was just selected via , or . keys.
        /// This flag is cleared when the user navigates the map with arrow keys.
        /// </summary>
        public static bool PawnJustSelected
        {
            get => pawnJustSelected;
            set => pawnJustSelected = value;
        }

        /// <summary>
        /// Gets the currently selected gizmo index.
        /// </summary>
        public static int SelectedGizmoIndex => selectedGizmoIndex;

        /// <summary>
        /// Gets the list of available gizmos.
        /// </summary>
        public static List<Gizmo> AvailableGizmos => availableGizmos;

        /// <summary>
        /// Opens the gizmo navigation menu by collecting gizmos from selected objects.
        /// </summary>
        public static void Open()
        {
            if (Find.Selector == null || Find.CurrentMap == null)
                return;

            // Collect gizmos from all selected objects
            availableGizmos.Clear();
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                if (obj is ISelectable selectable)
                {
                    availableGizmos.AddRange(selectable.GetGizmos());
                }
            }

            // Sort by Order property (lower values appear first)
            availableGizmos = availableGizmos
                .Where(g => g != null && g.Visible)
                .OrderBy(g => g.Order)
                .ToList();

            if (availableGizmos.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No commands available");
                return;
            }

            // Start at the first gizmo
            selectedGizmoIndex = 0;
            isActive = true;

            // Announce the first gizmo
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Opens the gizmo navigation menu by collecting gizmos from objects at the cursor position.
        /// </summary>
        public static void OpenAtCursor(IntVec3 cursorPosition, Map map)
        {
            if (map == null)
                return;

            // Validate cursor position
            if (!cursorPosition.IsValid || !cursorPosition.InBounds(map))
            {
                ClipboardHelper.CopyToClipboard("Invalid cursor position");
                return;
            }

            // Get all things at the cursor position
            availableGizmos.Clear();
            List<Thing> thingsAtPosition = cursorPosition.GetThingList(map);

            if (thingsAtPosition == null || thingsAtPosition.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No objects at cursor position");
                return;
            }

            // Collect gizmos from all things at this position
            foreach (Thing thing in thingsAtPosition)
            {
                if (thing is ISelectable selectable)
                {
                    availableGizmos.AddRange(selectable.GetGizmos());
                }
            }

            // Sort by Order property (lower values appear first)
            availableGizmos = availableGizmos
                .Where(g => g != null && g.Visible)
                .OrderBy(g => g.Order)
                .ToList();

            if (availableGizmos.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No commands available for objects at cursor");
                return;
            }

            // Start at the first gizmo
            selectedGizmoIndex = 0;
            isActive = true;

            // Announce what we're looking at and the first gizmo
            string objectNames = string.Join(", ", thingsAtPosition.Select(t => t.LabelShort).Take(3));
            if (thingsAtPosition.Count > 3)
                objectNames += $" and {thingsAtPosition.Count - 3} more";

            ClipboardHelper.CopyToClipboard($"Gizmos for: {objectNames}");

            // Announce the first gizmo
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Closes the gizmo navigation menu.
        /// </summary>
        public static void Close()
        {
            ModLogger.Msg($"GizmoNavigationState.Close() called, was active: {isActive}");
            isActive = false;
            selectedGizmoIndex = 0;
            availableGizmos.Clear();
            ModLogger.Msg($"GizmoNavigationState.Close() finished, now active: {isActive}");
        }

        /// <summary>
        /// Selects the next gizmo in the list.
        /// </summary>
        public static void SelectNext()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            selectedGizmoIndex = (selectedGizmoIndex + 1) % availableGizmos.Count;
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Selects the previous gizmo in the list.
        /// </summary>
        public static void SelectPrevious()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            selectedGizmoIndex = (selectedGizmoIndex - 1 + availableGizmos.Count) % availableGizmos.Count;
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Executes the currently selected gizmo.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            if (selectedGizmoIndex < 0 || selectedGizmoIndex >= availableGizmos.Count)
                return;

            Gizmo selectedGizmo = availableGizmos[selectedGizmoIndex];

            // Check if disabled
            if (selectedGizmo.Disabled)
            {
                string reason = selectedGizmo.disabledReason;
                if (string.IsNullOrEmpty(reason))
                    reason = "Command not available";
                ClipboardHelper.CopyToClipboard($"Disabled: {reason}");
                return;
            }

            // Create a fake event to trigger the gizmo
            Event fakeEvent = new Event();
            fakeEvent.type = EventType.Used;

            // Special handling for different gizmo types

            // 1. Designator (like Reinstall, Copy) - enters placement mode
            if (selectedGizmo is Designator designator)
            {
                ModLogger.Msg($"Executing Designator: {GetGizmoLabel(selectedGizmo)}");

                // For Designators opened via cursor objects (not selected pawns),
                // we need to ensure the objects at cursor are actually selected
                // so the Designator has proper context (e.g., Designator_Install needs to know what to reinstall)
                if (!PawnJustSelected && MapNavigationState.IsInitialized)
                {
                    IntVec3 cursorPos = MapNavigationState.CurrentCursorPosition;
                    List<Thing> thingsAtCursor = cursorPos.GetThingList(Find.CurrentMap);

                    ModLogger.Msg($"Selecting {thingsAtCursor?.Count ?? 0} things at cursor before executing Designator");

                    if (thingsAtCursor != null && thingsAtCursor.Count > 0 && Find.Selector != null)
                    {
                        Find.Selector.ClearSelection();
                        foreach (Thing thing in thingsAtCursor)
                        {
                            if (thing is ISelectable)
                            {
                                Find.Selector.Select(thing, playSound: false, forceDesignatorDeselect: false);
                            }
                        }
                    }
                }

                try
                {
                    // Call ProcessInput to let the Designator do its preparation work
                    // (Designator_Install does setup like canceling existing blueprints)
                    ModLogger.Msg($"About to call ProcessInput on Designator");
                    selectedGizmo.ProcessInput(fakeEvent);
                    ModLogger.Msg($"ProcessInput completed successfully");

                    // Announce placement mode
                    ClipboardHelper.CopyToClipboard($"{GetGizmoLabel(selectedGizmo)} - Use arrow keys to position, R to rotate, Enter to place, Escape to cancel");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Msg($"Exception in Designator execution: {ex.Message}");
                    ModLogger.Msg($"Stack trace: {ex.StackTrace}");
                    ClipboardHelper.CopyToClipboard($"Error executing {GetGizmoLabel(selectedGizmo)}: {ex.Message}");
                }

                // Close the gizmo menu AFTER announcing
                Close();
                return;
            }

            // 2. Command_Toggle - toggle and announce state
            if (selectedGizmo is Command_Toggle toggle)
            {
                // Execute the toggle
                selectedGizmo.ProcessInput(fakeEvent);

                // Announce the new state
                bool toggleActive = toggle.isActive?.Invoke() ?? false;
                string state = toggleActive ? "ON" : "OFF";
                string label = GetGizmoLabel(toggle);
                ClipboardHelper.CopyToClipboard($"{label}: {state}");
            }
            else
            {
                // Execute the command
                selectedGizmo.ProcessInput(fakeEvent);

                // 3. Command_VerbTarget (weapon attacks) - announce targeting mode
                if (selectedGizmo is Command_VerbTarget verbTarget)
                {
                    string weaponName = verbTarget.ownerThing?.LabelCap ?? "weapon";
                    string verbLabel = verbTarget.verb?.ReportLabel ?? "attack";
                    ClipboardHelper.CopyToClipboard($"{weaponName} {verbLabel} - Use map navigation to select target, then press Enter");
                }
                // 4. Command_Target - announce targeting mode
                else if (selectedGizmo is Command_Target)
                {
                    ClipboardHelper.CopyToClipboard($"{GetGizmoLabel(selectedGizmo)} - Use map navigation to select target, then press Enter");
                }
            }

            // Always close after executing (per user requirement)
            Close();
        }

        /// <summary>
        /// Announces the currently selected gizmo to the user via clipboard.
        /// Format: "Name: Description (Hotkey)" or "Name: Description" if no hotkey.
        /// </summary>
        private static void AnnounceCurrentGizmo()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            if (selectedGizmoIndex < 0 || selectedGizmoIndex >= availableGizmos.Count)
                return;

            Gizmo gizmo = availableGizmos[selectedGizmoIndex];

            string label = GetGizmoLabel(gizmo);
            string description = GetGizmoDescription(gizmo);
            string hotkey = GetGizmoHotkey(gizmo);

            string announcement = $"{selectedGizmoIndex + 1}/{availableGizmos.Count}: {label}";

            if (!string.IsNullOrEmpty(description))
                announcement += $": {description}";

            if (!string.IsNullOrEmpty(hotkey))
                announcement += $" ({hotkey})";

            // Add disabled status if applicable
            if (gizmo.Disabled)
            {
                string reason = gizmo.disabledReason;
                if (string.IsNullOrEmpty(reason))
                    reason = "Not available";
                announcement += $" [DISABLED: {reason}]";
            }

            ClipboardHelper.CopyToClipboard(announcement);
        }

        /// <summary>
        /// Gets the label text for a gizmo.
        /// </summary>
        private static string GetGizmoLabel(Gizmo gizmo)
        {
            // Special handling for Command_VerbTarget (weapon attacks)
            if (gizmo is Command_VerbTarget verbTarget)
            {
                string weaponName = verbTarget.ownerThing?.LabelCap ?? "Unknown weapon";
                string verbLabel = verbTarget.verb?.ReportLabel ?? "attack";
                return $"{weaponName} - {verbLabel}";
            }

            if (gizmo is Command cmd)
            {
                string label = cmd.LabelCap;
                if (string.IsNullOrEmpty(label))
                    label = cmd.defaultLabel;
                if (string.IsNullOrEmpty(label))
                    label = "Unknown Command";
                return label;
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the description text for a gizmo.
        /// </summary>
        private static string GetGizmoDescription(Gizmo gizmo)
        {
            if (gizmo is Command cmd)
            {
                string desc = cmd.Desc;
                if (string.IsNullOrEmpty(desc))
                    desc = cmd.defaultDesc;
                return desc ?? "";
            }
            return "";
        }

        /// <summary>
        /// Gets the hotkey text for a gizmo.
        /// </summary>
        private static string GetGizmoHotkey(Gizmo gizmo)
        {
            if (gizmo is Command cmd && cmd.hotKey != null)
            {
                KeyCode key = cmd.hotKey.MainKey;
                if (key != KeyCode.None)
                {
                    return key.ToStringReadable();
                }
            }
            return "";
        }
    }
}
