using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// State class for keyboard navigation of the Dialog_AutoSlaughter.
    /// Provides row/column navigation with +/- adjustment for limit values.
    /// </summary>
    public static class AutoSlaughterState
    {
        public static bool IsActive { get; private set; } = false;

        private static Dialog_AutoSlaughter currentDialog;
        private static List<AutoSlaughterConfig> configs = new List<AutoSlaughterConfig>();
        private static int currentRowIndex = 0;
        private static int currentColumnIndex = 0;
        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();

        // Column definitions: the 7 editable columns
        private enum Column
        {
            MaxTotal = 0,
            MaxMales = 1,
            MaxMalesYoung = 2,
            MaxFemales = 3,
            MaxFemalesYoung = 4,
            AllowPregnant = 5,
            AllowBonded = 6
        }

        private static readonly string[] ColumnNames = new[]
        {
            "Max Total",
            "Max Males",
            "Max Males Young",
            "Max Females",
            "Max Females Young",
            "Allow Pregnant",
            "Allow Bonded"
        };

        public static TypeaheadSearchHelper Typeahead => typeahead;
        public static int CurrentRowIndex => currentRowIndex;

        public static void Open(Dialog_AutoSlaughter dialog)
        {
            if (IsActive) return;

            currentDialog = dialog;
            RefreshConfigs();

            if (configs.Count == 0)
            {
                TolkHelper.Speak("No animals available for auto-slaughter settings");
                return;
            }

            currentRowIndex = 0;
            currentColumnIndex = 0;
            typeahead.ClearSearch();
            IsActive = true;

            SoundDefOf.TabOpen.PlayOneShotOnCamera();
            TolkHelper.Speak($"Auto-slaughter settings, {configs.Count} animal types");
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void Close()
        {
            IsActive = false;
            currentDialog = null;
            configs.Clear();
            typeahead.ClearSearch();
            TolkHelper.Speak("Auto-slaughter closed");
        }

        private static void RefreshConfigs()
        {
            configs.Clear();
            if (Find.CurrentMap?.autoSlaughterManager?.configs == null) return;

            // Get configs sorted by current count descending, then by label
            var manager = Find.CurrentMap.autoSlaughterManager;
            var animalCounts = new Dictionary<ThingDef, int>();

            foreach (var config in manager.configs)
            {
                int count = Find.CurrentMap.mapPawns.SpawnedColonyAnimals
                    .Count(p => p.def == config.animal);
                animalCounts[config.animal] = count;
            }

            configs = manager.configs
                .OrderByDescending(c => animalCounts.GetValueOrDefault(c.animal, 0))
                .ThenBy(c => c.animal.label)
                .ToList();
        }

        public static void SelectNextRow()
        {
            if (configs.Count == 0) return;

            currentRowIndex = (currentRowIndex + 1) % configs.Count;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void SelectPreviousRow()
        {
            if (configs.Count == 0) return;

            currentRowIndex = (currentRowIndex - 1 + configs.Count) % configs.Count;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void SelectNextColumn()
        {
            currentColumnIndex = (currentColumnIndex + 1) % ColumnNames.Length;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        public static void SelectPreviousColumn()
        {
            currentColumnIndex = (currentColumnIndex - 1 + ColumnNames.Length) % ColumnNames.Length;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        public static void JumpToFirst()
        {
            if (configs.Count == 0) return;

            currentRowIndex = 0;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void JumpToLast()
        {
            if (configs.Count == 0) return;

            currentRowIndex = configs.Count - 1;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void IncrementValue()
        {
            if (configs.Count == 0) return;

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;

            // Increment: unlimited (-1) -> 0 -> 1 -> 2 -> ...
            switch (column)
            {
                case Column.MaxTotal:
                    config.maxTotal = config.maxTotal == -1 ? 0 : config.maxTotal + 1;
                    break;
                case Column.MaxMales:
                    config.maxMales = config.maxMales == -1 ? 0 : config.maxMales + 1;
                    break;
                case Column.MaxMalesYoung:
                    config.maxMalesYoung = config.maxMalesYoung == -1 ? 0 : config.maxMalesYoung + 1;
                    break;
                case Column.MaxFemales:
                    config.maxFemales = config.maxFemales == -1 ? 0 : config.maxFemales + 1;
                    break;
                case Column.MaxFemalesYoung:
                    config.maxFemalesYoung = config.maxFemalesYoung == -1 ? 0 : config.maxFemalesYoung + 1;
                    break;
                case Column.AllowPregnant:
                case Column.AllowBonded:
                    ToggleBoolean();
                    return;
            }

            Find.CurrentMap?.autoSlaughterManager?.Notify_ConfigChanged();
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        public static void DecrementValue()
        {
            if (configs.Count == 0) return;

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;

            // Decrement: ... -> 2 -> 1 -> 0 -> unlimited (-1). At unlimited, do nothing.
            switch (column)
            {
                case Column.MaxTotal:
                    if (config.maxTotal >= 0) config.maxTotal = config.maxTotal == 0 ? -1 : config.maxTotal - 1;
                    break;
                case Column.MaxMales:
                    if (config.maxMales >= 0) config.maxMales = config.maxMales == 0 ? -1 : config.maxMales - 1;
                    break;
                case Column.MaxMalesYoung:
                    if (config.maxMalesYoung >= 0) config.maxMalesYoung = config.maxMalesYoung == 0 ? -1 : config.maxMalesYoung - 1;
                    break;
                case Column.MaxFemales:
                    if (config.maxFemales >= 0) config.maxFemales = config.maxFemales == 0 ? -1 : config.maxFemales - 1;
                    break;
                case Column.MaxFemalesYoung:
                    if (config.maxFemalesYoung >= 0) config.maxFemalesYoung = config.maxFemalesYoung == 0 ? -1 : config.maxFemalesYoung - 1;
                    break;
                case Column.AllowPregnant:
                case Column.AllowBonded:
                    ToggleBoolean();
                    return;
            }

            Find.CurrentMap?.autoSlaughterManager?.Notify_ConfigChanged();
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        public static void ClearLimit()
        {
            if (configs.Count == 0) return;

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;

            switch (column)
            {
                case Column.MaxTotal:
                    config.maxTotal = -1;
                    break;
                case Column.MaxMales:
                    config.maxMales = -1;
                    break;
                case Column.MaxMalesYoung:
                    config.maxMalesYoung = -1;
                    break;
                case Column.MaxFemales:
                    config.maxFemales = -1;
                    break;
                case Column.MaxFemalesYoung:
                    config.maxFemalesYoung = -1;
                    break;
                case Column.AllowPregnant:
                case Column.AllowBonded:
                    // Can't clear boolean, just toggle
                    return;
            }

            Find.CurrentMap?.autoSlaughterManager?.Notify_ConfigChanged();
            SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        public static void ToggleBoolean()
        {
            if (configs.Count == 0) return;

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;

            switch (column)
            {
                case Column.AllowPregnant:
                    config.allowSlaughterPregnant = !config.allowSlaughterPregnant;
                    if (config.allowSlaughterPregnant)
                        SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    else
                        SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    break;
                case Column.AllowBonded:
                    config.allowSlaughterBonded = !config.allowSlaughterBonded;
                    if (config.allowSlaughterBonded)
                        SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    else
                        SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    break;
                default:
                    return;
            }

            Find.CurrentMap?.autoSlaughterManager?.Notify_ConfigChanged();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void AnnounceCurrentCell(bool includeAnimalName)
        {
            if (configs.Count == 0) return;

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;
            string columnName = ColumnNames[currentColumnIndex];

            string value = GetColumnValueString(config, column);
            string position = MenuHelper.FormatPosition(currentRowIndex, configs.Count);

            string announcement;
            if (includeAnimalName)
            {
                announcement = $"{config.animal.LabelCap}, {columnName}: {value}. {position}";
            }
            else
            {
                announcement = $"{columnName}: {value}";
            }

            TolkHelper.Speak(announcement);
        }

        private static string GetColumnValueString(AutoSlaughterConfig config, Column column)
        {
            int current;
            int max;

            switch (column)
            {
                case Column.MaxTotal:
                    current = GetCurrentCount(config);
                    max = config.maxTotal;
                    return FormatCurrentOfMax(current, max);

                case Column.MaxMales:
                    current = GetCurrentMaleCount(config);
                    max = config.maxMales;
                    return FormatCurrentOfMax(current, max);

                case Column.MaxMalesYoung:
                    current = GetCurrentMaleYoungCount(config);
                    max = config.maxMalesYoung;
                    return FormatCurrentOfMax(current, max);

                case Column.MaxFemales:
                    current = GetCurrentFemaleCount(config);
                    max = config.maxFemales;
                    return FormatCurrentOfMax(current, max);

                case Column.MaxFemalesYoung:
                    current = GetCurrentFemaleYoungCount(config);
                    max = config.maxFemalesYoung;
                    return FormatCurrentOfMax(current, max);

                case Column.AllowPregnant:
                    int pregnant = GetPregnantCount(config);
                    return $"{pregnant} pregnant, slaughter {(config.allowSlaughterPregnant ? "allowed" : "not allowed")}";

                case Column.AllowBonded:
                    int bonded = GetBondedCount(config);
                    return $"{bonded} bonded, slaughter {(config.allowSlaughterBonded ? "allowed" : "not allowed")}";

                default:
                    return "Unknown";
            }
        }

        private static string FormatCurrentOfMax(int current, int max)
        {
            if (max == -1)
            {
                return $"{current} of unlimited";
            }
            else if (current > max)
            {
                int toSlaughter = current - max;
                return $"{current} of {max}, {toSlaughter} to slaughter";
            }
            else
            {
                return $"{current} of {max}";
            }
        }

        // Helper methods to get counts
        private static int GetCurrentCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal) ?? 0;
        }

        private static int GetCurrentMaleCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.gender == Gender.Male &&
                            p.ageTracker?.CurLifeStage?.reproductive == true) ?? 0;
        }

        private static int GetCurrentMaleYoungCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.gender == Gender.Male &&
                            p.ageTracker?.CurLifeStage?.reproductive != true) ?? 0;
        }

        private static int GetCurrentFemaleCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.gender == Gender.Female &&
                            p.ageTracker?.CurLifeStage?.reproductive == true) ?? 0;
        }

        private static int GetCurrentFemaleYoungCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.gender == Gender.Female &&
                            p.ageTracker?.CurLifeStage?.reproductive != true) ?? 0;
        }

        private static int GetPregnantCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.health?.hediffSet?.HasHediff(HediffDefOf.Pregnant) == true) ?? 0;
        }

        private static int GetBondedCount(AutoSlaughterConfig config)
        {
            return Find.CurrentMap?.mapPawns?.SpawnedColonyAnimals?
                .Count(p => p.def == config.animal &&
                            p.relations?.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0) ?? 0;
        }

        #region Typeahead Search

        public static List<string> GetItemLabels()
        {
            return configs.Select(c => c.animal.LabelCap.ToString()).ToList();
        }

        public static void SetCurrentRowIndex(int index)
        {
            if (index >= 0 && index < configs.Count)
            {
                currentRowIndex = index;
            }
        }

        public static void HandleTypeahead(char c)
        {
            var labels = GetItemLabels();
            if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    currentRowIndex = newIndex;
                    AnnounceWithSearch();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{typeahead.LastFailedSearch}'");
            }
        }

        public static void HandleBackspace()
        {
            if (!typeahead.HasActiveSearch) return;

            var labels = GetItemLabels();
            if (typeahead.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                    currentRowIndex = newIndex;
                AnnounceWithSearch();
            }
        }

        public static void AnnounceWithSearch()
        {
            if (configs.Count == 0)
            {
                TolkHelper.Speak("No animals");
                return;
            }

            var config = configs[currentRowIndex];
            var column = (Column)currentColumnIndex;
            string columnName = ColumnNames[currentColumnIndex];
            string value = GetColumnValueString(config, column);
            string position = MenuHelper.FormatPosition(currentRowIndex, configs.Count);

            string announcement = $"{config.animal.LabelCap}, {columnName}: {value}. {position}";

            if (typeahead.HasActiveSearch)
            {
                announcement += $", match {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} for '{typeahead.SearchBuffer}'";
            }

            TolkHelper.Speak(announcement);
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles keyboard input for the auto-slaughter dialog.
        /// Returns true if input was handled, false otherwise.
        /// </summary>
        public static bool HandleInput(Event evt)
        {
            if (!IsActive || evt.type != EventType.KeyDown) return false;

            KeyCode key = evt.keyCode;

            // Escape - clear search first, then close
            if (key == KeyCode.Escape)
            {
                if (typeahead.HasActiveSearch)
                {
                    typeahead.ClearSearchAndAnnounce();
                }
                else
                {
                    // Close dialog
                    if (currentDialog != null)
                    {
                        Find.WindowStack.TryRemove(currentDialog, doCloseSound: true);
                    }
                    Close();
                }
                return true;
            }

            // Backspace - search backspace
            if (key == KeyCode.Backspace)
            {
                HandleBackspace();
                return true;
            }

            // Home/End for first/last
            if (key == KeyCode.Home)
            {
                JumpToFirst();
                return true;
            }

            if (key == KeyCode.End)
            {
                JumpToLast();
                return true;
            }

            // Arrow keys for navigation
            if (key == KeyCode.DownArrow)
            {
                if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
                {
                    int newIndex = typeahead.GetNextMatch(currentRowIndex);
                    if (newIndex >= 0)
                    {
                        currentRowIndex = newIndex;
                        AnnounceWithSearch();
                    }
                }
                else
                {
                    SelectNextRow();
                }
                return true;
            }

            if (key == KeyCode.UpArrow)
            {
                if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
                {
                    int newIndex = typeahead.GetPreviousMatch(currentRowIndex);
                    if (newIndex >= 0)
                    {
                        currentRowIndex = newIndex;
                        AnnounceWithSearch();
                    }
                }
                else
                {
                    SelectPreviousRow();
                }
                return true;
            }

            if (key == KeyCode.RightArrow)
            {
                SelectNextColumn();
                return true;
            }

            if (key == KeyCode.LeftArrow)
            {
                SelectPreviousColumn();
                return true;
            }

            // +/- for value adjustment (also = and keypad)
            if (key == KeyCode.Plus || key == KeyCode.KeypadPlus || key == KeyCode.Equals)
            {
                IncrementValue();
                return true;
            }

            if (key == KeyCode.Minus || key == KeyCode.KeypadMinus)
            {
                DecrementValue();
                return true;
            }

            // Delete to clear limit
            if (key == KeyCode.Delete)
            {
                ClearLimit();
                return true;
            }

            // Space to toggle boolean columns
            if (key == KeyCode.Space)
            {
                var column = (Column)currentColumnIndex;
                if (column == Column.AllowPregnant || column == Column.AllowBonded)
                {
                    ToggleBoolean();
                    return true;
                }
            }

            // Tab - consume to prevent repeated announcements
            if (key == KeyCode.Tab)
            {
                return true;
            }

            // Typeahead - letter keys
            if (evt.character != '\0' && char.IsLetter(evt.character))
            {
                HandleTypeahead(evt.character);
                return true;
            }

            return false;
        }

        #endregion
    }
}
