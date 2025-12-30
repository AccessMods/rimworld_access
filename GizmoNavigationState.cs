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
        private static Dictionary<Gizmo, ISelectable> gizmoOwners = new Dictionary<Gizmo, ISelectable>();
        private static bool pawnJustSelected = false;
        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();

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
                TolkHelper.Speak("No commands available");
                return;
            }

            // Start at the first gizmo
            selectedGizmoIndex = 0;
            isActive = true;
            typeahead.ClearSearch();

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
                TolkHelper.Speak("Invalid cursor position");
                return;
            }

            // Get all things at the cursor position
            availableGizmos.Clear();
            List<Thing> thingsAtPosition = cursorPosition.GetThingList(map);

            if (thingsAtPosition == null || thingsAtPosition.Count == 0)
            {
                TolkHelper.Speak("No objects at cursor position");
                return;
            }

            // Collect gizmos from all things at this position
            // Important: Temporarily select each thing before getting its gizmos,
            // because some gizmos (like Designator_Install) check if the thing is selected
            // to determine their Visible property
            gizmoOwners.Clear();

            // Store the current selection to restore it later
            var previousSelection = Find.Selector.SelectedObjects.ToList();

            foreach (Thing thing in thingsAtPosition)
            {
                if (thing is ISelectable selectable)
                {
                    // Temporarily select this thing so its gizmos' Visible property works correctly
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(thing, playSound: false, forceDesignatorDeselect: false);

                    var gizmos = selectable.GetGizmos().ToList();
                    foreach (Gizmo gizmo in gizmos)
                    {
                        availableGizmos.Add(gizmo);
                        gizmoOwners[gizmo] = selectable;
                    }
                }
            }

            // Restore previous selection (or clear if nothing was selected)
            Find.Selector.ClearSelection();
            foreach (var obj in previousSelection)
            {
                if (obj is ISelectable selectableObj)
                {
                    Find.Selector.Select(selectableObj, playSound: false, forceDesignatorDeselect: false);
                }
            }

            // Sort by Order property (lower values appear first)
            availableGizmos = availableGizmos
                .Where(g => g != null && g.Visible)
                .OrderBy(g => g.Order)
                .ToList();

            if (availableGizmos.Count == 0)
            {
                TolkHelper.Speak("No commands available for objects at cursor");
                return;
            }

            // Start at the first gizmo
            selectedGizmoIndex = 0;
            isActive = true;
            typeahead.ClearSearch();

            // Announce what we're looking at and the first gizmo
            string objectNames = string.Join(", ", thingsAtPosition.Select(t => t.LabelShort).Take(3));
            if (thingsAtPosition.Count > 3)
                objectNames += $" and {thingsAtPosition.Count - 3} more";

            TolkHelper.Speak($"Gizmos for: {objectNames}");

            // Announce the first gizmo
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Closes the gizmo navigation menu.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            selectedGizmoIndex = 0;
            availableGizmos.Clear();
            gizmoOwners.Clear();
            typeahead.ClearSearch();
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
            string gizmoLabel = GetGizmoLabel(selectedGizmo);

            // Check if disabled
            if (selectedGizmo.Disabled)
            {
                string reason = selectedGizmo.disabledReason;
                if (string.IsNullOrEmpty(reason))
                    reason = "Command not available";
                TolkHelper.Speak($"Disabled: {reason}");
                return;
            }

            // Create a fake event to trigger the gizmo
            Event fakeEvent = new Event();
            fakeEvent.type = EventType.Used;

            // Special handling for different gizmo types

            // 1. Designator (like Reinstall, Copy) - enters placement mode
            if (selectedGizmo is Designator designator)
            {
                // For Designators opened via cursor objects (not selected pawns),
                // we need to ensure the correct object is selected
                // so the Designator has proper context (e.g., Designator_Install needs to know what to reinstall)
                if (!PawnJustSelected && gizmoOwners.ContainsKey(selectedGizmo) && Find.Selector != null)
                {
                    // Select ONLY the specific thing that owns this gizmo
                    ISelectable owner = gizmoOwners[selectedGizmo];
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(owner, playSound: false, forceDesignatorDeselect: false);
                }

                try
                {
                    // Call ProcessInput to let the Designator do its preparation work
                    // (Designator_Install does setup like canceling existing blueprints)
                    selectedGizmo.ProcessInput(fakeEvent);

                    // Validate that the designator was actually selected
                    if (Find.DesignatorManager != null && Find.DesignatorManager.SelectedDesignator != null)
                    {
                        // Announce placement mode
                        TolkHelper.Speak($"{gizmoLabel} - Use arrow keys to position, R to rotate, Space to place, Escape to cancel");
                    }
                    else
                    {
                        TolkHelper.Speak($"Error: {gizmoLabel} could not be activated. Check if the item can be placed.", SpeechPriority.High);
                    }
                }
                catch (System.Exception ex)
                {
                    ModLogger.Error($"Exception in Designator execution: {ex.Message}");
                    TolkHelper.Speak($"Error executing {gizmoLabel}: {ex.Message}", SpeechPriority.High);
                }

                // Close the gizmo menu AFTER announcing
                Close();
                return;
            }

            // 2. Command_Toggle - toggle and announce state
            if (selectedGizmo is Command_Toggle toggle)
            {
                try
                {
                    // Execute the toggle
                    selectedGizmo.ProcessInput(fakeEvent);

                    // Announce the new state
                    bool toggleActive = toggle.isActive?.Invoke() ?? false;
                    string state = toggleActive ? "ON" : "OFF";
                    string label = GetGizmoLabel(toggle);
                    TolkHelper.Speak($"{label}: {state}");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Error($"Exception in Command_Toggle execution: {ex.Message}");
                    TolkHelper.Speak($"Error executing {gizmoLabel}: {ex.Message}", SpeechPriority.High);
                }
            }
            else
            {
                // 3. Command_VerbTarget (weapon attacks) - announce targeting mode
                if (selectedGizmo is Command_VerbTarget verbTarget)
                {
                    try
                    {
                        // Execute the command
                        selectedGizmo.ProcessInput(fakeEvent);

                        string weaponName = verbTarget.ownerThing?.LabelCap ?? "weapon";
                        string verbLabel = verbTarget.verb?.ReportLabel ?? "attack";
                        TolkHelper.Speak($"{weaponName} {verbLabel} - Use map navigation to select target, then press Enter");
                    }
                    catch (System.Exception ex)
                    {
                        ModLogger.Error($"Exception in Command_VerbTarget execution: {ex.Message}");
                        TolkHelper.Speak($"Error executing {gizmoLabel}: {ex.Message}", SpeechPriority.High);
                    }
                }
                // 4. Command_Target - announce targeting mode
                else if (selectedGizmo is Command_Target)
                {
                    try
                    {
                        // Execute the command
                        selectedGizmo.ProcessInput(fakeEvent);

                        TolkHelper.Speak($"{gizmoLabel} - Use map navigation to select target, then press Enter");
                    }
                    catch (System.Exception ex)
                    {
                        ModLogger.Error($"Exception in Command_Target execution: {ex.Message}");
                        TolkHelper.Speak($"Error executing {gizmoLabel}: {ex.Message}", SpeechPriority.High);
                    }
                }
                // 5. Generic Command
                else
                {
                    try
                    {
                        // Execute the command
                        selectedGizmo.ProcessInput(fakeEvent);
                    }
                    catch (System.Exception ex)
                    {
                        ModLogger.Error($"Exception in generic Command execution: {ex.Message}");
                        TolkHelper.Speak($"Error executing {gizmoLabel}: {ex.Message}", SpeechPriority.High);
                    }
                }
            }

            // Always close after executing (per user requirement)
            Close();
        }

        /// <summary>
        /// Jumps to the first gizmo in the list.
        /// </summary>
        public static void JumpToFirst()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            selectedGizmoIndex = 0;
            typeahead.ClearSearch();
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Jumps to the last gizmo in the list.
        /// </summary>
        public static void JumpToLast()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            selectedGizmoIndex = availableGizmos.Count - 1;
            typeahead.ClearSearch();
            AnnounceCurrentGizmo();
        }

        /// <summary>
        /// Handles typeahead character input for the gizmo menu.
        /// Called from UnifiedKeyboardPatch to process alphanumeric characters.
        /// </summary>
        public static void HandleTypeahead(char c)
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            var labels = GetGizmoLabels();
            if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedGizmoIndex = newIndex;
                    AnnounceWithSearch();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{typeahead.SearchBuffer}'");
            }
        }

        /// <summary>
        /// Handles backspace key for typeahead search.
        /// Called from UnifiedKeyboardPatch.
        /// </summary>
        public static void HandleBackspace()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            if (!typeahead.HasActiveSearch)
                return;

            var labels = GetGizmoLabels();
            if (typeahead.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedGizmoIndex = newIndex;
                }
                AnnounceWithSearch();
            }
        }

        /// <summary>
        /// Gets whether typeahead search is active.
        /// </summary>
        public static bool HasActiveSearch => typeahead.HasActiveSearch;

        /// <summary>
        /// Handles keyboard input for the gizmo menu, including typeahead search.
        /// </summary>
        /// <returns>True if input was handled, false otherwise.</returns>
        public static bool HandleInput()
        {
            if (!isActive || availableGizmos.Count == 0)
                return false;

            if (Event.current.type != EventType.KeyDown)
                return false;

            KeyCode key = Event.current.keyCode;

            // Handle Home - jump to first
            if (key == KeyCode.Home)
            {
                JumpToFirst();
                Event.current.Use();
                return true;
            }

            // Handle End - jump to last
            if (key == KeyCode.End)
            {
                JumpToLast();
                Event.current.Use();
                return true;
            }

            // Handle Escape - clear search FIRST, then close
            if (key == KeyCode.Escape)
            {
                if (typeahead.HasActiveSearch)
                {
                    typeahead.ClearSearchAndAnnounce();
                    AnnounceCurrentGizmo();
                    Event.current.Use();
                    return true;
                }
                // Let the caller handle normal escape (close menu)
                return false;
            }

            // Handle Backspace for search
            if (key == KeyCode.Backspace && typeahead.HasActiveSearch)
            {
                var labels = GetGizmoLabels();
                if (typeahead.ProcessBackspace(labels, out int newIndex))
                {
                    if (newIndex >= 0)
                        selectedGizmoIndex = newIndex;
                    AnnounceWithSearch();
                }
                Event.current.Use();
                return true;
            }

            // Handle Up arrow - navigate with search awareness
            if (key == KeyCode.UpArrow)
            {
                if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
                {
                    // Navigate through matches only when there ARE matches
                    int prevIndex = typeahead.GetPreviousMatch(selectedGizmoIndex);
                    if (prevIndex >= 0)
                    {
                        selectedGizmoIndex = prevIndex;
                        AnnounceWithSearch();
                    }
                }
                else
                {
                    // Navigate normally (either no search active, OR search with no matches)
                    SelectPrevious();
                }
                Event.current.Use();
                return true;
            }

            // Handle Down arrow - navigate with search awareness
            if (key == KeyCode.DownArrow)
            {
                if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
                {
                    // Navigate through matches only when there ARE matches
                    int nextIndex = typeahead.GetNextMatch(selectedGizmoIndex);
                    if (nextIndex >= 0)
                    {
                        selectedGizmoIndex = nextIndex;
                        AnnounceWithSearch();
                    }
                }
                else
                {
                    // Navigate normally (either no search active, OR search with no matches)
                    SelectNext();
                }
                Event.current.Use();
                return true;
            }

            // Handle Enter - execute selected
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                ExecuteSelected();
                Event.current.Use();
                return true;
            }

            // Handle typeahead characters
            // Use KeyCode instead of Event.current.character (which is empty in Unity IMGUI)
            bool isLetter = key >= KeyCode.A && key <= KeyCode.Z;
            bool isNumber = key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9;

            if (isLetter || isNumber)
            {
                char c = isLetter ? (char)('a' + (key - KeyCode.A)) : (char)('0' + (key - KeyCode.Alpha0));
                var labels = GetGizmoLabels();
                if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
                {
                    if (newIndex >= 0)
                    {
                        selectedGizmoIndex = newIndex;
                        AnnounceWithSearch();
                    }
                }
                else
                {
                    TolkHelper.Speak($"No matches for '{typeahead.SearchBuffer}'");
                }
                Event.current.Use();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the list of labels for all gizmos.
        /// </summary>
        private static List<string> GetGizmoLabels()
        {
            var labels = new List<string>();
            foreach (var gizmo in availableGizmos)
            {
                labels.Add(GetGizmoLabel(gizmo));
            }
            return labels;
        }

        /// <summary>
        /// Announces the current selection with search context if applicable.
        /// </summary>
        private static void AnnounceWithSearch()
        {
            if (!isActive || availableGizmos.Count == 0)
                return;

            if (selectedGizmoIndex < 0 || selectedGizmoIndex >= availableGizmos.Count)
                return;

            Gizmo gizmo = availableGizmos[selectedGizmoIndex];
            string label = GetGizmoLabel(gizmo);

            if (typeahead.HasActiveSearch)
            {
                string announcement = $"{label}, {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} matches for '{typeahead.SearchBuffer}'";

                // Add disabled status if applicable
                if (gizmo.Disabled)
                {
                    string reason = gizmo.disabledReason;
                    if (string.IsNullOrEmpty(reason))
                        reason = "Not available";
                    announcement += $" [DISABLED: {reason}]";
                }

                TolkHelper.Speak(announcement);
            }
            else
            {
                AnnounceCurrentGizmo();
            }
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

            TolkHelper.Speak(announcement);
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
