using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Column types for the schedule menu.
    /// </summary>
    public enum ScheduleColumnMode
    {
        Schedule,
        Areas
    }

    /// <summary>
    /// Manages the state and navigation for the windowless schedule/timetable interface.
    /// Provides 2D grid navigation (pawns x hours) with keyboard controls.
    /// Similar to WorkMenuState but for managing colonist schedules without opening the native tab.
    /// Now includes dual-column interface with Schedule and Allowed Areas columns.
    /// </summary>
    public static class WindowlessScheduleState
    {
        private static bool isActive = false;
        private static int selectedPawnIndex = 0;
        private static int selectedHourIndex = 0;
        private static TimeAssignmentDef selectedAssignment = null;
        private static List<Pawn> pawns = new List<Pawn>();
        private static List<TimeAssignmentDef> copiedSchedule = null;

        // Track pending changes: Dictionary<Pawn, Dictionary<Hour, NewAssignment>>
        private static Dictionary<Pawn, Dictionary<int, TimeAssignmentDef>> pendingChanges = new Dictionary<Pawn, Dictionary<int, TimeAssignmentDef>>();

        // Track original schedules for revert: Dictionary<Pawn, List<TimeAssignmentDef>>
        private static Dictionary<Pawn, List<TimeAssignmentDef>> originalSchedules = new Dictionary<Pawn, List<TimeAssignmentDef>>();

        // Column mode state (Schedule vs Areas)
        private static ScheduleColumnMode currentColumn = ScheduleColumnMode.Schedule;
        private static List<Area> availableAreas = new List<Area>();  // null = Unrestricted, then game areas
        private static int selectedAreaIndex = 0;

        public static bool IsActive => isActive;
        public static int SelectedPawnIndex => selectedPawnIndex;
        public static int SelectedHourIndex => selectedHourIndex;
        public static List<Pawn> Pawns => pawns;
        public static TimeAssignmentDef SelectedAssignment => selectedAssignment;
        public static ScheduleColumnMode CurrentColumn => currentColumn;
        public static bool IsInAreasColumn => currentColumn == ScheduleColumnMode.Areas;

        /// <summary>
        /// Opens the schedule menu. Initializes pawn list and sets focus to current game hour.
        /// </summary>
        public static void Open()
        {
            isActive = true;
            selectedPawnIndex = 0;
            selectedHourIndex = GenLocalDate.HourOfDay(Find.CurrentMap);
            copiedSchedule = null;
            pendingChanges.Clear();
            originalSchedules.Clear();
            currentColumn = ScheduleColumnMode.Schedule;
            availableAreas.Clear();
            selectedAreaIndex = 0;

            // Get list of pawns (colonists + controllable subhumans, excluding babies)
            pawns.Clear();
            if (Find.CurrentMap?.mapPawns?.FreeColonists != null)
            {
                pawns.AddRange(Find.CurrentMap.mapPawns.FreeColonists
                    .Where(p => !p.DevelopmentalStage.Baby()));
            }

            // Add colony subhumans (controllable animals, etc.)
            if (Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals != null)
            {
                pawns.AddRange(Find.CurrentMap.mapPawns.SpawnedColonyAnimals
                    .Where(p => p.RaceProps.intelligence >= Intelligence.ToolUser));
            }

            // Sort by label for consistency
            pawns = pawns.OrderBy(p => p.LabelShort).ToList();

            // Store original schedules for each pawn (for revert on cancel)
            foreach (var pawn in pawns)
            {
                if (pawn.timetable != null)
                {
                    var originalSchedule = new List<TimeAssignmentDef>();
                    for (int hour = 0; hour < 24; hour++)
                    {
                        originalSchedule.Add(pawn.timetable.GetAssignment(hour));
                    }
                    originalSchedules[pawn] = originalSchedule;
                }
            }

            // Default to "Anything" assignment
            selectedAssignment = TimeAssignmentDefOf.Anything;

            // If we have a selected pawn, try to focus on them
            if (Find.Selector.SingleSelectedThing is Pawn selectedPawn)
            {
                int index = pawns.IndexOf(selectedPawn);
                if (index >= 0)
                {
                    selectedPawnIndex = index;
                }
            }

            TolkHelper.Speak("Schedule");
            UpdateClipboard();
        }

        /// <summary>
        /// Confirms all pending changes, applies them to pawns, and closes the schedule menu.
        /// </summary>
        public static void Confirm()
        {
            int changesApplied = 0;

            // Apply all pending changes
            foreach (var pawnChanges in pendingChanges)
            {
                Pawn pawn = pawnChanges.Key;
                if (pawn.timetable == null)
                    continue;

                foreach (var hourChange in pawnChanges.Value)
                {
                    int hour = hourChange.Key;
                    TimeAssignmentDef newAssignment = hourChange.Value;
                    pawn.timetable.SetAssignment(hour, newAssignment);
                    changesApplied++;
                }
            }

            // Note: Area changes are applied immediately, so we only report schedule changes here
            string message = changesApplied > 0
                ? $"Applied {changesApplied} schedule changes"
                : "Schedule closed";

            TolkHelper.Speak(message);

            // Close and cleanup
            isActive = false;
            pawns.Clear();
            selectedPawnIndex = 0;
            selectedHourIndex = 0;
            selectedAssignment = null;
            copiedSchedule = null;
            pendingChanges.Clear();
            originalSchedules.Clear();
            currentColumn = ScheduleColumnMode.Schedule;
            availableAreas.Clear();
            selectedAreaIndex = 0;
        }

        /// <summary>
        /// Cancels all pending changes, reverts to original schedules, and closes the schedule menu.
        /// </summary>
        public static void Cancel()
        {
            // Revert all changes back to original
            foreach (var pawnOriginal in originalSchedules)
            {
                Pawn pawn = pawnOriginal.Key;
                if (pawn.timetable == null)
                    continue;

                List<TimeAssignmentDef> originalSchedule = pawnOriginal.Value;
                for (int hour = 0; hour < 24; hour++)
                {
                    pawn.timetable.SetAssignment(hour, originalSchedule[hour]);
                }
            }

            TolkHelper.Speak("Schedule changes cancelled");

            // Close and cleanup
            isActive = false;
            pawns.Clear();
            selectedPawnIndex = 0;
            selectedHourIndex = 0;
            selectedAssignment = null;
            copiedSchedule = null;
            pendingChanges.Clear();
            originalSchedules.Clear();
            currentColumn = ScheduleColumnMode.Schedule;
            availableAreas.Clear();
            selectedAreaIndex = 0;
        }

        // ===== Column switching methods =====

        /// <summary>
        /// Switches to the next column (Schedule -> Areas -> Schedule).
        /// </summary>
        public static void SwitchToNextColumn()
        {
            currentColumn = currentColumn == ScheduleColumnMode.Schedule
                ? ScheduleColumnMode.Areas
                : ScheduleColumnMode.Schedule;

            if (currentColumn == ScheduleColumnMode.Areas)
            {
                LoadAvailableAreas();
                SyncAreaIndexToCurrentPawn();
            }

            AnnounceColumnSwitch();
        }

        /// <summary>
        /// Switches to the previous column (Areas -> Schedule -> Areas).
        /// </summary>
        public static void SwitchToPreviousColumn()
        {
            SwitchToNextColumn();  // Only 2 columns, so same as next
        }

        /// <summary>
        /// Loads available areas from the current map's area manager.
        /// </summary>
        private static void LoadAvailableAreas()
        {
            availableAreas.Clear();
            availableAreas.Add(null);  // "Unrestricted" is null

            if (Find.CurrentMap?.areaManager != null)
            {
                var areas = Find.CurrentMap.areaManager.AllAreas
                    .Where(a => a.AssignableAsAllowed());
                availableAreas.AddRange(areas);
            }
        }

        /// <summary>
        /// Syncs selectedAreaIndex to match current pawn's assigned area.
        /// </summary>
        private static void SyncAreaIndexToCurrentPawn()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            Area currentArea = pawn.playerSettings?.AreaRestrictionInPawnCurrentMap;

            int index = availableAreas.IndexOf(currentArea);
            selectedAreaIndex = index >= 0 ? index : 0;
        }

        /// <summary>
        /// Announces column switch.
        /// </summary>
        private static void AnnounceColumnSwitch()
        {
            if (currentColumn == ScheduleColumnMode.Schedule)
            {
                TolkHelper.Speak("Schedule column");
                UpdateClipboard();
            }
            else
            {
                SyncAreaIndexToCurrentPawn();
                if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                {
                    TolkHelper.Speak("Allowed Areas column. No pawns available");
                    return;
                }

                Pawn pawn = pawns[selectedPawnIndex];
                string areaName = selectedAreaIndex >= 0 && selectedAreaIndex < availableAreas.Count
                    ? (availableAreas[selectedAreaIndex]?.Label ?? "Unrestricted")
                    : "Unrestricted";
                string pawnPos = MenuHelper.FormatPosition(selectedPawnIndex, pawns.Count);
                string areaPos = MenuHelper.FormatPosition(selectedAreaIndex, availableAreas.Count);
                TolkHelper.Speak($"Allowed Areas column. {pawn.LabelShort}: {areaName}. Pawn {pawnPos}, Area {areaPos}");
            }
        }

        // ===== Area column navigation methods =====

        /// <summary>
        /// Selects the next area for the current pawn (Right arrow in Areas column).
        /// Immediately applies the change.
        /// </summary>
        public static void SelectNextArea()
        {
            if (currentColumn != ScheduleColumnMode.Areas || availableAreas.Count == 0)
                return;

            selectedAreaIndex = MenuHelper.SelectNext(selectedAreaIndex, availableAreas.Count);
            ApplyAreaToCurrentPawn();
        }

        /// <summary>
        /// Selects the previous area for the current pawn (Left arrow in Areas column).
        /// Immediately applies the change.
        /// </summary>
        public static void SelectPreviousArea()
        {
            if (currentColumn != ScheduleColumnMode.Areas || availableAreas.Count == 0)
                return;

            selectedAreaIndex = MenuHelper.SelectPrevious(selectedAreaIndex, availableAreas.Count);
            ApplyAreaToCurrentPawn();
        }

        /// <summary>
        /// Applies the currently selected area to the current pawn.
        /// </summary>
        private static void ApplyAreaToCurrentPawn()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.playerSettings == null)
                return;

            Area area = selectedAreaIndex >= 0 && selectedAreaIndex < availableAreas.Count
                ? availableAreas[selectedAreaIndex]
                : null;

            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;

            string areaName = area?.Label ?? "Unrestricted";
            string position = MenuHelper.FormatPosition(selectedAreaIndex, availableAreas.Count);
            TolkHelper.Speak($"{pawn.LabelShort}: {areaName}. {position}");
        }

        /// <summary>
        /// Applies current area to the pawn below and moves down.
        /// </summary>
        public static void ApplyAreaToPawnBelow()
        {
            if (currentColumn != ScheduleColumnMode.Areas || pawns.Count <= 1)
                return;

            Area currentArea = selectedAreaIndex >= 0 && selectedAreaIndex < availableAreas.Count
                ? availableAreas[selectedAreaIndex]
                : null;

            // Move to next pawn
            selectedPawnIndex = MenuHelper.SelectNext(selectedPawnIndex, pawns.Count);

            // Apply same area
            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.playerSettings != null)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = currentArea;
            }

            // Keep same area selected
            selectedAreaIndex = availableAreas.IndexOf(currentArea);
            if (selectedAreaIndex < 0) selectedAreaIndex = 0;

            string areaName = currentArea?.Label ?? "Unrestricted";
            string pawnPosition = MenuHelper.FormatPosition(selectedPawnIndex, pawns.Count);
            TolkHelper.Speak($"{pawn.LabelShort}: {areaName} applied. Pawn {pawnPosition}");
        }

        /// <summary>
        /// Applies current area to the pawn above and moves up.
        /// </summary>
        public static void ApplyAreaToPawnAbove()
        {
            if (currentColumn != ScheduleColumnMode.Areas || pawns.Count <= 1)
                return;

            Area currentArea = selectedAreaIndex >= 0 && selectedAreaIndex < availableAreas.Count
                ? availableAreas[selectedAreaIndex]
                : null;

            // Move to previous pawn
            selectedPawnIndex = MenuHelper.SelectPrevious(selectedPawnIndex, pawns.Count);

            // Apply same area
            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.playerSettings != null)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = currentArea;
            }

            // Keep same area selected
            selectedAreaIndex = availableAreas.IndexOf(currentArea);
            if (selectedAreaIndex < 0) selectedAreaIndex = 0;

            string areaName = currentArea?.Label ?? "Unrestricted";
            string pawnPosition = MenuHelper.FormatPosition(selectedPawnIndex, pawns.Count);
            TolkHelper.Speak($"{pawn.LabelShort}: {areaName} applied. Pawn {pawnPosition}");
        }

        /// <summary>
        /// Opens context menu with bulk area operations.
        /// </summary>
        public static void OpenAreaContextMenu()
        {
            if (currentColumn != ScheduleColumnMode.Areas)
            {
                TolkHelper.Speak("Context menu only available in Areas column");
                return;
            }

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Set All to Home Area", () => SetAllToHomeArea()),
                new FloatMenuOption("Clear All Allowed Areas", () => ClearAllAreas())
            };

            WindowlessFloatMenuState.Open(options, colonistOrders: false);
        }

        /// <summary>
        /// Sets all pawns' allowed areas to Home.
        /// </summary>
        private static void SetAllToHomeArea()
        {
            Area homeArea = Find.CurrentMap?.areaManager?.Home;
            if (homeArea == null)
            {
                TolkHelper.Speak("No home area defined");
                return;
            }

            int count = 0;
            foreach (var pawn in pawns)
            {
                if (pawn.playerSettings != null && pawn.playerSettings.SupportsAllowedAreas)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = homeArea;
                    count++;
                }
            }

            // Update selection to match
            selectedAreaIndex = availableAreas.IndexOf(homeArea);
            if (selectedAreaIndex < 0) selectedAreaIndex = 0;

            TolkHelper.Speak($"Set {count} pawns to Home area");
        }

        /// <summary>
        /// Clears all pawns' allowed areas (sets to Unrestricted).
        /// </summary>
        private static void ClearAllAreas()
        {
            int count = 0;
            foreach (var pawn in pawns)
            {
                if (pawn.playerSettings != null && pawn.playerSettings.SupportsAllowedAreas)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                    count++;
                }
            }

            selectedAreaIndex = 0;  // Unrestricted is index 0

            TolkHelper.Speak($"Cleared allowed areas for {count} pawns");
        }

        /// <summary>
        /// Jumps to first pawn (for Areas column).
        /// </summary>
        public static void JumpToFirstPawn()
        {
            if (pawns.Count == 0) return;
            selectedPawnIndex = 0;
            if (currentColumn == ScheduleColumnMode.Areas)
            {
                SyncAreaIndexToCurrentPawn();
                AnnounceAreaSelection();
            }
            else
            {
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Jumps to last pawn (for Areas column).
        /// </summary>
        public static void JumpToLastPawn()
        {
            if (pawns.Count == 0) return;
            selectedPawnIndex = pawns.Count - 1;
            if (currentColumn == ScheduleColumnMode.Areas)
            {
                SyncAreaIndexToCurrentPawn();
                AnnounceAreaSelection();
            }
            else
            {
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Announces current area selection.
        /// </summary>
        private static void AnnounceAreaSelection()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
            {
                TolkHelper.Speak("No pawns available");
                return;
            }

            Pawn pawn = pawns[selectedPawnIndex];
            string areaName = selectedAreaIndex >= 0 && selectedAreaIndex < availableAreas.Count
                ? (availableAreas[selectedAreaIndex]?.Label ?? "Unrestricted")
                : "Unrestricted";

            string pawnPos = MenuHelper.FormatPosition(selectedPawnIndex, pawns.Count);
            string areaPos = MenuHelper.FormatPosition(selectedAreaIndex, availableAreas.Count);

            TolkHelper.Speak($"{pawn.LabelShort}: {areaName}. Pawn {pawnPos}, Area {areaPos}");
        }

        // ===== Original schedule navigation methods =====

        /// <summary>
        /// Moves selection up to previous pawn (wraps around).
        /// </summary>
        public static void MoveUp()
        {
            if (pawns.Count == 0)
                return;

            selectedPawnIndex = MenuHelper.SelectPrevious(selectedPawnIndex, pawns.Count);

            // After moving pawn, sync area index if in Areas column
            if (currentColumn == ScheduleColumnMode.Areas)
            {
                SyncAreaIndexToCurrentPawn();
                AnnounceAreaSelection();
            }
            else
            {
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Moves selection down to next pawn (wraps around).
        /// </summary>
        public static void MoveDown()
        {
            if (pawns.Count == 0)
                return;

            selectedPawnIndex = MenuHelper.SelectNext(selectedPawnIndex, pawns.Count);

            // After moving pawn, sync area index if in Areas column
            if (currentColumn == ScheduleColumnMode.Areas)
            {
                SyncAreaIndexToCurrentPawn();
                AnnounceAreaSelection();
            }
            else
            {
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Moves selection left to previous hour (wraps around).
        /// </summary>
        public static void MoveLeft()
        {
            selectedHourIndex = MenuHelper.SelectPrevious(selectedHourIndex, 24);
            UpdateClipboard();
        }

        /// <summary>
        /// Moves selection right to next hour (wraps around).
        /// </summary>
        public static void MoveRight()
        {
            selectedHourIndex = MenuHelper.SelectNext(selectedHourIndex, 24);
            UpdateClipboard();
        }

        /// <summary>
        /// Jumps to hour 0.
        /// </summary>
        public static void JumpToFirstHour()
        {
            selectedHourIndex = MenuHelper.JumpToFirst();
            UpdateClipboard();
        }

        /// <summary>
        /// Jumps to hour 23.
        /// </summary>
        public static void JumpToLastHour()
        {
            selectedHourIndex = MenuHelper.JumpToLast(24);
            UpdateClipboard();
        }

        /// <summary>
        /// Cycles forward through available time assignment types for current cell.
        /// Order: Anything -> Work -> Joy -> Sleep -> Meditate (if available)
        /// </summary>
        public static void CycleAssignment()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            var availableAssignments = GetAvailableAssignments();
            if (availableAssignments.Count == 0)
                return;

            // Get the CURRENT cell's assignment, not the tracking variable
            TimeAssignmentDef currentCellAssignment = pawn.timetable.GetAssignment(selectedHourIndex);
            int currentIndex = availableAssignments.IndexOf(currentCellAssignment);
            if (currentIndex < 0) currentIndex = 0;

            // Wrap around: at end, go to start
            currentIndex = (currentIndex + 1) % availableAssignments.Count;
            selectedAssignment = availableAssignments[currentIndex];

            // Apply to current cell immediately
            ApplyAssignment();
        }

        /// <summary>
        /// Cycles backward through available time assignment types for current cell.
        /// Order: Meditate (if available) -> Sleep -> Joy -> Work -> Anything
        /// </summary>
        public static void CycleAssignmentBackward()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            var availableAssignments = GetAvailableAssignments();
            if (availableAssignments.Count == 0)
                return;

            // Get the CURRENT cell's assignment, not the tracking variable
            TimeAssignmentDef currentCellAssignment = pawn.timetable.GetAssignment(selectedHourIndex);
            int currentIndex = availableAssignments.IndexOf(currentCellAssignment);
            if (currentIndex < 0) currentIndex = 0;

            // Wrap around: at start, go to end
            currentIndex = (currentIndex - 1 + availableAssignments.Count) % availableAssignments.Count;
            selectedAssignment = availableAssignments[currentIndex];

            // Apply to current cell immediately
            ApplyAssignment();
        }

        /// <summary>
        /// Gets the list of available time assignments (includes Meditate if Royalty active).
        /// </summary>
        private static List<TimeAssignmentDef> GetAvailableAssignments()
        {
            var assignments = new List<TimeAssignmentDef>
            {
                TimeAssignmentDefOf.Anything,
                TimeAssignmentDefOf.Work,
                TimeAssignmentDefOf.Joy,
                TimeAssignmentDefOf.Sleep
            };

            // Add Meditate if it exists (Royalty DLC)
            if (TimeAssignmentDefOf.Meditate != null)
            {
                assignments.Add(TimeAssignmentDefOf.Meditate);
            }

            return assignments;
        }

        /// <summary>
        /// Applies the currently selected assignment to the current cell.
        /// Changes are applied immediately (for visual feedback) but tracked as pending.
        /// </summary>
        public static void ApplyAssignment()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            // Apply the change immediately (for visual feedback)
            pawn.timetable.SetAssignment(selectedHourIndex, selectedAssignment);

            // Track as pending change
            if (!pendingChanges.ContainsKey(pawn))
            {
                pendingChanges[pawn] = new Dictionary<int, TimeAssignmentDef>();
            }
            pendingChanges[pawn][selectedHourIndex] = selectedAssignment;

            string message = $"{pawn.LabelShort}, Hour {selectedHourIndex}: {selectedAssignment.label} (pending)";
            TolkHelper.Speak(message);
        }

        /// <summary>
        /// Fills the rest of the current row (from current hour to end) with the selected assignment.
        /// </summary>
        public static void FillRow()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            // Track pending changes
            if (!pendingChanges.ContainsKey(pawn))
            {
                pendingChanges[pawn] = new Dictionary<int, TimeAssignmentDef>();
            }

            int cellsFilled = 0;
            for (int hour = selectedHourIndex; hour <= 23; hour++)
            {
                pawn.timetable.SetAssignment(hour, selectedAssignment);
                pendingChanges[pawn][hour] = selectedAssignment;
                cellsFilled++;
            }

            string message = $"{pawn.LabelShort}: Filled {cellsFilled} hours with {selectedAssignment.label} (pending)";
            TolkHelper.Speak(message);
        }

        /// <summary>
        /// Copies the current pawn's entire schedule to clipboard storage.
        /// </summary>
        public static void CopySchedule()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            copiedSchedule = new List<TimeAssignmentDef>();
            for (int hour = 0; hour < 24; hour++)
            {
                copiedSchedule.Add(pawn.timetable.GetAssignment(hour));
            }

            TolkHelper.Speak($"Copied schedule from {pawn.LabelShort}");
        }

        /// <summary>
        /// Pastes the copied schedule to the current pawn.
        /// </summary>
        public static void PasteSchedule()
        {
            if (copiedSchedule == null)
            {
                TolkHelper.Speak("No schedule copied");
                return;
            }

            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
                return;

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
                return;

            // Track pending changes
            if (!pendingChanges.ContainsKey(pawn))
            {
                pendingChanges[pawn] = new Dictionary<int, TimeAssignmentDef>();
            }

            for (int hour = 0; hour < 24; hour++)
            {
                pawn.timetable.SetAssignment(hour, copiedSchedule[hour]);
                pendingChanges[pawn][hour] = copiedSchedule[hour];
            }

            TolkHelper.Speak($"Pasted schedule to {pawn.LabelShort} (pending)");
        }

        /// <summary>
        /// Updates clipboard with current cell information for screen reader.
        /// </summary>
        private static void UpdateClipboard()
        {
            if (pawns.Count == 0 || selectedPawnIndex < 0 || selectedPawnIndex >= pawns.Count)
            {
                TolkHelper.Speak("No pawns available");
                return;
            }

            Pawn pawn = pawns[selectedPawnIndex];
            if (pawn.timetable == null)
            {
                TolkHelper.Speak($"{pawn.LabelShort}: No schedule");
                return;
            }

            TimeAssignmentDef currentAssignment = pawn.timetable.GetAssignment(selectedHourIndex);

            // Check if this cell has pending changes
            bool hasPendingChange = pendingChanges.ContainsKey(pawn) &&
                                   pendingChanges[pawn].ContainsKey(selectedHourIndex);

            string pendingIndicator = hasPendingChange ? " (pending)" : "";
            string pawnPosition = MenuHelper.FormatPosition(selectedPawnIndex, pawns.Count);
            string hourPosition = MenuHelper.FormatPosition(selectedHourIndex, 24);
            string positionSuffix = "";
            if (!string.IsNullOrEmpty(pawnPosition) || !string.IsNullOrEmpty(hourPosition))
            {
                positionSuffix = $". Pawn {pawnPosition}, Hour {hourPosition}";
            }
            string message = $"{pawn.LabelShort}, Hour {selectedHourIndex}: {currentAssignment.label}{pendingIndicator}{positionSuffix}";
            TolkHelper.Speak(message);
        }
    }
}
