using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the state and navigation for the interactive work assignment menu.
    /// Tracks work types, their priority values (0-4), and pending changes.
    /// Supports both simple mode (toggle on/off) and manual priority mode (1-4).
    /// </summary>
    public static class WorkMenuState
    {
        private static bool isActive = false;
        private static Pawn currentPawn = null;
        private static int currentPawnIndex = 0;
        private static List<Pawn> allPawns = new List<Pawn>();
        private static int selectedIndex = 0;
        private static List<WorkTypeEntry> workEntries = new List<WorkTypeEntry>();
        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();

        public static bool IsActive => isActive;
        public static int SelectedIndex => selectedIndex;
        public static List<WorkTypeEntry> WorkEntries => workEntries;
        public static Pawn CurrentPawn => currentPawn;
        public static int CurrentPawnIndex => currentPawnIndex;
        public static int TotalPawns => allPawns.Count;
        public static TypeaheadSearchHelper Typeahead => typeahead;

        /// <summary>
        /// Opens the work menu for the specified pawn.
        /// </summary>
        public static void Open(Pawn pawn)
        {
            if (pawn == null || pawn.workSettings == null)
                return;

            isActive = true;
            currentPawn = pawn;
            selectedIndex = 0;
            typeahead.ClearSearch();

            // Build list of all colonists
            allPawns.Clear();
            if (Find.CurrentMap != null)
            {
                allPawns = Find.CurrentMap.mapPawns.FreeColonists.ToList();
                currentPawnIndex = allPawns.IndexOf(pawn);
                if (currentPawnIndex < 0)
                    currentPawnIndex = 0;
            }

            LoadWorkTypesForCurrentPawn();
        }

        /// <summary>
        /// Loads work types for the current pawn.
        /// </summary>
        private static void LoadWorkTypesForCurrentPawn()
        {
            if (currentPawn == null || currentPawn.workSettings == null)
                return;

            // Build the list of work types
            workEntries.Clear();
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;

            foreach (var workType in allWorkTypes)
            {
                if (workType.visible)
                {
                    bool isDisabled = currentPawn.WorkTypeIsDisabled(workType);
                    int priority = isDisabled ? 0 : currentPawn.workSettings.GetPriority(workType);

                    workEntries.Add(new WorkTypeEntry
                    {
                        WorkType = workType,
                        IsDisabled = isDisabled,
                        CurrentPriority = priority,
                        OriginalPriority = priority
                    });
                }
            }

            // Sort by naturalPriority (descending = higher priority first)
            // This matches the execution order when priority numbers are equal
            workEntries = workEntries.OrderByDescending(e => e.WorkType.naturalPriority).ToList();

            // Announce menu opened
            UpdateClipboard();
        }

        /// <summary>
        /// Closes the menu without applying changes.
        /// </summary>
        public static void Cancel()
        {
            isActive = false;
            currentPawn = null;
            currentPawnIndex = 0;
            allPawns.Clear();
            selectedIndex = 0;
            workEntries.Clear();
            typeahead.ClearSearch();

            TolkHelper.Speak("Work menu cancelled");
        }

        /// <summary>
        /// Closes the menu and applies all pending changes to the pawn's work settings.
        /// </summary>
        public static void Confirm()
        {
            if (currentPawn == null || currentPawn.workSettings == null)
            {
                Cancel();
                return;
            }

            // Apply final changes
            int changesApplied = ApplyPendingChanges();

            string message = changesApplied > 0
                ? $"Applied {changesApplied} work assignment changes for {currentPawn.LabelShort}"
                : $"No changes made for {currentPawn.LabelShort}";

            TolkHelper.Speak(message);

            isActive = false;
            currentPawn = null;
            currentPawnIndex = 0;
            allPawns.Clear();
            selectedIndex = 0;
            workEntries.Clear();
            typeahead.ClearSearch();
        }

        /// <summary>
        /// Applies all pending changes to the current pawn's work settings.
        /// Returns the number of changes applied.
        /// </summary>
        private static int ApplyPendingChanges()
        {
            if (currentPawn == null || currentPawn.workSettings == null)
                return 0;

            int changesApplied = 0;

            foreach (var entry in workEntries)
            {
                if (!entry.IsDisabled && entry.CurrentPriority != entry.OriginalPriority)
                {
                    currentPawn.workSettings.SetPriority(entry.WorkType, entry.CurrentPriority);
                    changesApplied++;
                }
            }

            return changesApplied;
        }

        /// <summary>
        /// Moves selection up in the list (stops at top).
        /// </summary>
        public static void MoveUp()
        {
            if (workEntries.Count == 0)
                return;

            if (selectedIndex > 0)
            {
                selectedIndex--;
                UpdateClipboard();
            }
            else
            {
                TolkHelper.Speak("At top of list");
            }
        }

        /// <summary>
        /// Moves selection down in the list (stops at bottom).
        /// </summary>
        public static void MoveDown()
        {
            if (workEntries.Count == 0)
                return;

            if (selectedIndex < workEntries.Count - 1)
            {
                selectedIndex++;
                UpdateClipboard();
            }
            else
            {
                TolkHelper.Speak("At bottom of list");
            }
        }

        /// <summary>
        /// Toggles the enabled state of the currently selected work type.
        /// In simple mode: toggles between 0 (disabled) and 3 (default).
        /// In manual priority mode: toggles between 0 (disabled) and 3 (medium).
        /// Only works if the work type is not permanently disabled.
        /// </summary>
        public static void ToggleSelected()
        {
            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
                return;

            var entry = workEntries[selectedIndex];

            if (entry.IsDisabled)
            {
                TolkHelper.Speak($"{entry.WorkType.labelShort}: Disabled - cannot toggle", SpeechPriority.High);
                return;
            }

            // Toggle between 0 (disabled) and 3 (default enabled)
            entry.CurrentPriority = (entry.CurrentPriority == 0) ? 3 : 0;
            UpdateClipboard();
        }

        /// <summary>
        /// Sets the priority of the currently selected work type to a specific value (0-4).
        /// Only works in manual priority mode and if the work type is not permanently disabled.
        /// </summary>
        public static void SetPriority(int priority)
        {
            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
                return;

            var entry = workEntries[selectedIndex];

            if (entry.IsDisabled)
            {
                TolkHelper.Speak($"{entry.WorkType.labelShort}: Disabled - cannot change priority", SpeechPriority.High);
                return;
            }

            // Validate priority range
            if (priority < 0 || priority > 4)
                return;

            entry.CurrentPriority = priority;
            UpdateClipboard();
        }

        /// <summary>
        /// Switches to the next pawn in the list (does not wrap).
        /// Automatically applies any pending changes before switching.
        /// </summary>
        public static void SwitchToNextPawn()
        {
            if (allPawns.Count == 0)
                return;

            // Apply pending changes for current pawn before switching
            ApplyPendingChanges();

            currentPawnIndex = MenuHelper.SelectNext(currentPawnIndex, allPawns.Count);
            currentPawn = allPawns[currentPawnIndex];
            selectedIndex = 0;
            typeahead.ClearSearch();
            LoadWorkTypesForCurrentPawn();
            TolkHelper.Speak($"Now editing: {currentPawn.LabelShort}. {MenuHelper.FormatPosition(currentPawnIndex, allPawns.Count)}");
        }

        /// <summary>
        /// Switches to the previous pawn in the list (does not wrap).
        /// Automatically applies any pending changes before switching.
        /// </summary>
        public static void SwitchToPreviousPawn()
        {
            if (allPawns.Count == 0)
                return;

            // Apply pending changes for current pawn before switching
            ApplyPendingChanges();

            currentPawnIndex = MenuHelper.SelectPrevious(currentPawnIndex, allPawns.Count);

            currentPawn = allPawns[currentPawnIndex];
            selectedIndex = 0;
            typeahead.ClearSearch();
            LoadWorkTypesForCurrentPawn();
            TolkHelper.Speak($"Now editing: {currentPawn.LabelShort}. {MenuHelper.FormatPosition(currentPawnIndex, allPawns.Count)}");
        }

        /// <summary>
        /// Moves the selected work type up in the column order (left in UI).
        /// This affects execution order when priority numbers are equal by swapping naturalPriority values.
        /// Only works in manual priority mode.
        /// </summary>
        public static void ReorderWorkTypeUp()
        {
            if (!Find.PlaySettings.useWorkPriorities)
            {
                TolkHelper.Speak("Column reordering only available in manual priorities mode");
                return;
            }

            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
                return;

            var entry = workEntries[selectedIndex];

            // Get all work types sorted by naturalPriority (descending = higher priority first)
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(w => w.visible)
                .OrderByDescending(w => w.naturalPriority)
                .ToList();

            int currentIndex = allWorkTypes.IndexOf(entry.WorkType);
            if (currentIndex <= 0)
            {
                TolkHelper.Speak($"{entry.WorkType.labelShort}: Already at highest priority");
                return;
            }

            // Swap naturalPriority values with the work type that has higher priority
            var higherWorkType = allWorkTypes[currentIndex - 1];
            int temp = entry.WorkType.naturalPriority;
            entry.WorkType.naturalPriority = higherWorkType.naturalPriority;
            higherWorkType.naturalPriority = temp;

            // Also update column order for visual consistency
            var workTableDef = PawnTableDefOf.Work;
            var columns = workTableDef.columns;
            var column = columns.FirstOrDefault(c => c.workType == entry.WorkType);
            if (column != null)
            {
                int columnIndex = columns.IndexOf(column);
                if (columnIndex > 0)
                {
                    columns.RemoveAt(columnIndex);
                    columns.Insert(columnIndex - 1, column);
                }
            }

            // Force table refresh if work tab is open
            var workTab = Find.WindowStack.WindowOfType<MainTabWindow_Work>();
            if (workTab != null)
            {
                workTab.Notify_ResolutionChanged();
            }

            // Force all pawns to recache their work givers so execution order updates
            if (Find.CurrentMap != null)
            {
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    if (pawn.workSettings != null)
                    {
                        pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                    }
                }
            }

            // Re-sort the workEntries list to reflect the new order
            var selectedWorkType = entry.WorkType;
            workEntries = workEntries.OrderByDescending(e => e.WorkType.naturalPriority).ToList();

            // Update selectedIndex to follow the moved item
            selectedIndex = workEntries.FindIndex(e => e.WorkType == selectedWorkType);
            if (selectedIndex < 0)
                selectedIndex = 0;

            TolkHelper.Speak($"{entry.WorkType.labelShort}: Moved up in priority order (will execute earlier when priorities are equal)");
        }

        /// <summary>
        /// Moves the selected work type down in the column order (right in UI).
        /// This affects execution order when priority numbers are equal by swapping naturalPriority values.
        /// Only works in manual priority mode.
        /// </summary>
        public static void ReorderWorkTypeDown()
        {
            if (!Find.PlaySettings.useWorkPriorities)
            {
                TolkHelper.Speak("Column reordering only available in manual priorities mode");
                return;
            }

            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
                return;

            var entry = workEntries[selectedIndex];

            // Get all work types sorted by naturalPriority (descending = higher priority first)
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(w => w.visible)
                .OrderByDescending(w => w.naturalPriority)
                .ToList();

            int currentIndex = allWorkTypes.IndexOf(entry.WorkType);
            if (currentIndex >= allWorkTypes.Count - 1)
            {
                TolkHelper.Speak($"{entry.WorkType.labelShort}: Already at lowest priority");
                return;
            }

            // Swap naturalPriority values with the work type that has lower priority
            var lowerWorkType = allWorkTypes[currentIndex + 1];
            int temp = entry.WorkType.naturalPriority;
            entry.WorkType.naturalPriority = lowerWorkType.naturalPriority;
            lowerWorkType.naturalPriority = temp;

            // Also update column order for visual consistency
            var workTableDef = PawnTableDefOf.Work;
            var columns = workTableDef.columns;
            var column = columns.FirstOrDefault(c => c.workType == entry.WorkType);
            if (column != null)
            {
                int columnIndex = columns.IndexOf(column);
                if (columnIndex < columns.Count - 1)
                {
                    columns.RemoveAt(columnIndex);
                    columns.Insert(columnIndex + 1, column);
                }
            }

            // Force table refresh if work tab is open
            var workTab = Find.WindowStack.WindowOfType<MainTabWindow_Work>();
            if (workTab != null)
            {
                workTab.Notify_ResolutionChanged();
            }

            // Force all pawns to recache their work givers so execution order updates
            if (Find.CurrentMap != null)
            {
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    if (pawn.workSettings != null)
                    {
                        pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                    }
                }
            }

            // Re-sort the workEntries list to reflect the new order
            var selectedWorkType = entry.WorkType;
            workEntries = workEntries.OrderByDescending(e => e.WorkType.naturalPriority).ToList();

            // Update selectedIndex to follow the moved item
            selectedIndex = workEntries.FindIndex(e => e.WorkType == selectedWorkType);
            if (selectedIndex < 0)
                selectedIndex = 0;

            TolkHelper.Speak($"{entry.WorkType.labelShort}: Moved down in priority order (will execute later when priorities are equal)");
        }

        /// <summary>
        /// Toggles between simple mode and manual priority mode.
        /// </summary>
        public static void ToggleMode()
        {
            bool wasUsingPriorities = Find.PlaySettings.useWorkPriorities;
            Find.PlaySettings.useWorkPriorities = !wasUsingPriorities;

            // Convert priorities when switching modes
            foreach (var entry in workEntries)
            {
                if (!entry.IsDisabled)
                {
                    if (wasUsingPriorities)
                    {
                        // Switching from manual to simple: convert any non-zero priority to "enabled" (3)
                        entry.CurrentPriority = (entry.CurrentPriority > 0) ? 3 : 0;
                    }
                    // When switching from simple to manual, priorities stay as they are (0 or 3)
                }
            }

            string mode = Find.PlaySettings.useWorkPriorities ? "manual priorities" : "simple";
            TolkHelper.Speak($"Switched to {mode} mode");
            UpdateClipboard();
        }

        /// <summary>
        /// Gets the current selection as a formatted string for screen reader.
        /// Announces differently based on whether manual priorities mode is active.
        /// </summary>
        private static void UpdateClipboard()
        {
            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
            {
                TolkHelper.Speak("No work types available");
                return;
            }

            var entry = workEntries[selectedIndex];
            string message;

            if (entry.IsDisabled)
            {
                message = $"{entry.WorkType.labelShort}: Permanently disabled";
            }
            else if (Find.PlaySettings.useWorkPriorities)
            {
                // Manual priority mode: announce priority number
                string priorityDesc = GetPriorityDescription(entry.CurrentPriority);
                string changed = (entry.CurrentPriority != entry.OriginalPriority) ? " (pending)" : "";
                message = $"{entry.WorkType.labelShort}: {priorityDesc}{changed}";
            }
            else
            {
                // Simple mode: announce enabled/disabled
                bool isEnabled = (entry.CurrentPriority > 0);
                bool wasEnabled = (entry.OriginalPriority > 0);
                string status = isEnabled ? "Enabled" : "Disabled";
                string changed = (isEnabled != wasEnabled) ? " (pending)" : "";
                message = $"{entry.WorkType.labelShort}: {status}{changed}";
            }

            TolkHelper.Speak(message);
        }

        /// <summary>
        /// Gets a human-readable description of a priority value.
        /// </summary>
        private static string GetPriorityDescription(int priority)
        {
            switch (priority)
            {
                case 0: return "Disabled";
                case 1: return "Priority 1 (highest)";
                case 2: return "Priority 2 (high)";
                case 3: return "Priority 3 (medium)";
                case 4: return "Priority 4 (low)";
                default: return $"Priority {priority}";
            }
        }

        /// <summary>
        /// Jumps to the first item in the list.
        /// </summary>
        public static void JumpToFirst()
        {
            if (workEntries.Count == 0)
                return;

            selectedIndex = MenuHelper.JumpToFirst();
            typeahead.ClearSearch();
            UpdateClipboard();
        }

        /// <summary>
        /// Jumps to the last item in the list.
        /// </summary>
        public static void JumpToLast()
        {
            if (workEntries.Count == 0)
                return;

            selectedIndex = MenuHelper.JumpToLast(workEntries.Count);
            typeahead.ClearSearch();
            UpdateClipboard();
        }

        /// <summary>
        /// Gets a list of labels for all work entries for typeahead search.
        /// </summary>
        public static List<string> GetItemLabels()
        {
            List<string> labels = new List<string>();
            foreach (var entry in workEntries)
            {
                labels.Add(entry.WorkType.labelShort);
            }
            return labels;
        }

        /// <summary>
        /// Sets the selected index directly.
        /// </summary>
        public static void SetSelectedIndex(int index)
        {
            if (index >= 0 && index < workEntries.Count)
            {
                selectedIndex = index;
            }
        }

        /// <summary>
        /// Announces the current selection with search context if active.
        /// </summary>
        public static void AnnounceWithSearch()
        {
            if (workEntries.Count == 0 || selectedIndex < 0 || selectedIndex >= workEntries.Count)
            {
                TolkHelper.Speak("No work types available");
                return;
            }

            var entry = workEntries[selectedIndex];
            string message;

            if (entry.IsDisabled)
            {
                message = $"{entry.WorkType.labelShort}: Permanently disabled";
            }
            else if (Find.PlaySettings.useWorkPriorities)
            {
                string priorityDesc = GetPriorityDescription(entry.CurrentPriority);
                string changed = (entry.CurrentPriority != entry.OriginalPriority) ? " (pending)" : "";
                message = $"{entry.WorkType.labelShort}: {priorityDesc}{changed}";
            }
            else
            {
                bool isEnabled = (entry.CurrentPriority > 0);
                bool wasEnabled = (entry.OriginalPriority > 0);
                string status = isEnabled ? "Enabled" : "Disabled";
                string changed = (isEnabled != wasEnabled) ? " (pending)" : "";
                message = $"{entry.WorkType.labelShort}: {status}{changed}";
            }

            // Add search context if active
            if (typeahead.HasActiveSearch)
            {
                message += $", match {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} for '{typeahead.SearchBuffer}'";
            }

            TolkHelper.Speak(message);
        }

        /// <summary>
        /// Announces the current selection (same as UpdateClipboard but public).
        /// </summary>
        public static void AnnounceCurrentSelection()
        {
            UpdateClipboard();
        }

        /// <summary>
        /// Represents a work type entry in the menu.
        /// Tracks priority values (0-4) where:
        /// 0 = Disabled, 1 = Highest, 2 = High, 3 = Medium, 4 = Low
        /// </summary>
        public class WorkTypeEntry
        {
            public WorkTypeDef WorkType { get; set; }
            public bool IsDisabled { get; set; }      // Permanently disabled (cannot be changed)
            public int CurrentPriority { get; set; }  // Current priority in the menu (0-4)
            public int OriginalPriority { get; set; } // Original priority when menu opened (0-4)
        }
    }
}
