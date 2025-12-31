using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    public static class WildlifeMenuState
    {
        public static bool IsActive { get; private set; } = false;

        private static List<Pawn> wildlifeList = new List<Pawn>();
        private static int currentAnimalIndex = 0;
        private static int currentColumnIndex = 0;
        private static int sortColumnIndex = 4; // Default: Body Size (matches PawnTable_Wildlife)
        private static bool sortDescending = true; // Default: descending (matches PawnTable_Wildlife)

        public static void Open()
        {
            if (Find.CurrentMap == null)
            {
                TolkHelper.Speak("No map loaded");
                return;
            }

            // Get all wild animals using same filter as MainTabWindow_Wildlife
            wildlifeList = Find.CurrentMap.mapPawns.AllPawns
                .Where(p => p.Spawned &&
                           (p.Faction == null || p.Faction == Faction.OfInsects) &&
                           p.AnimalOrWildMan() &&
                           !p.Position.Fogged(p.Map) &&
                           !p.IsPrisonerInPrisonCell())
                .ToList();

            if (wildlifeList.Count == 0)
            {
                TolkHelper.Speak("No wildlife found on map");
                return;
            }

            // Apply default sort (by body size descending, then by label)
            wildlifeList = WildlifeMenuHelper.DefaultSort(wildlifeList);

            currentAnimalIndex = 0;
            currentColumnIndex = 0;
            IsActive = true;

            SoundDefOf.TabOpen.PlayOneShotOnCamera();

            string announcement = $"Wildlife menu, {wildlifeList.Count} animals";
            TolkHelper.Speak(announcement);
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void Close()
        {
            IsActive = false;
            wildlifeList.Clear();
            SoundDefOf.TabClose.PlayOneShotOnCamera();
            TolkHelper.Speak("Wildlife menu closed");
        }

        public static void HandleInput()
        {
            if (!IsActive) return;

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow))
            {
                SelectPreviousAnimal();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow))
            {
                SelectNextAnimal();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow))
            {
                SelectPreviousColumn();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow))
            {
                SelectNextColumn();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) ||
                     UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter))
            {
                InteractWithCurrentCell();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.S))
            {
                ToggleSortByCurrentColumn();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
            {
                Close();
            }
        }

        private static void SelectNextAnimal()
        {
            if (wildlifeList.Count == 0) return;

            currentAnimalIndex = MenuHelper.SelectNext(currentAnimalIndex, wildlifeList.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        private static void SelectPreviousAnimal()
        {
            if (wildlifeList.Count == 0) return;

            currentAnimalIndex = MenuHelper.SelectPrevious(currentAnimalIndex, wildlifeList.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        private static void SelectNextColumn()
        {
            int totalColumns = WildlifeMenuHelper.GetTotalColumnCount();
            currentColumnIndex = MenuHelper.SelectNext(currentColumnIndex, totalColumns);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void SelectPreviousColumn()
        {
            int totalColumns = WildlifeMenuHelper.GetTotalColumnCount();
            currentColumnIndex = MenuHelper.SelectPrevious(currentColumnIndex, totalColumns);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void AnnounceCurrentCell(bool includeAnimalName = true)
        {
            if (wildlifeList.Count == 0) return;

            Pawn currentAnimal = wildlifeList[currentAnimalIndex];
            string columnName = WildlifeMenuHelper.GetColumnName(currentColumnIndex);
            string columnValue = WildlifeMenuHelper.GetColumnValue(currentAnimal, currentColumnIndex);

            string announcement;
            if (includeAnimalName)
            {
                string animalName = WildlifeMenuHelper.GetAnimalName(currentAnimal);
                announcement = $"{animalName} - {columnName}: {columnValue}";
            }
            else
            {
                announcement = $"{columnName}: {columnValue}";
            }

            TolkHelper.Speak(announcement);
        }

        private static void InteractWithCurrentCell()
        {
            if (wildlifeList.Count == 0) return;

            Pawn currentAnimal = wildlifeList[currentAnimalIndex];

            if (!WildlifeMenuHelper.IsColumnInteractive(currentColumnIndex))
            {
                // Just re-announce for non-interactive columns
                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentCell(includeAnimalName: false);
                return;
            }

            // Handle interaction based on column type
            WildlifeMenuHelper.ColumnType type = (WildlifeMenuHelper.ColumnType)currentColumnIndex;

            switch (type)
            {
                case WildlifeMenuHelper.ColumnType.Hunt:
                    ToggleHunt(currentAnimal);
                    break;
                case WildlifeMenuHelper.ColumnType.Tame:
                    ToggleTame(currentAnimal);
                    break;
            }
        }

        private static void ToggleHunt(Pawn pawn)
        {
            bool isNowMarked = WildlifeMenuHelper.ToggleHuntDesignation(pawn);

            if (isNowMarked)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleTame(Pawn pawn)
        {
            bool? result = WildlifeMenuHelper.ToggleTameDesignation(pawn);

            if (result == null)
            {
                // Cannot tame this animal
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Cannot tame this animal", SpeechPriority.High);
                return;
            }

            if (result.Value)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleSortByCurrentColumn()
        {
            if (sortColumnIndex == currentColumnIndex)
            {
                // Same column - toggle direction
                sortDescending = !sortDescending;
            }
            else
            {
                // New column - sort ascending
                sortColumnIndex = currentColumnIndex;
                sortDescending = false;
            }

            // Re-sort the list
            wildlifeList = WildlifeMenuHelper.SortWildlifeByColumn(wildlifeList, sortColumnIndex, sortDescending);

            // Try to keep the same animal selected
            Pawn currentAnimal = null;
            if (currentAnimalIndex < wildlifeList.Count)
            {
                currentAnimal = wildlifeList[currentAnimalIndex];
            }

            if (currentAnimal != null)
            {
                currentAnimalIndex = wildlifeList.IndexOf(currentAnimal);
                if (currentAnimalIndex < 0) currentAnimalIndex = 0;
            }
            else
            {
                currentAnimalIndex = 0;
            }

            string direction = sortDescending ? "descending" : "ascending";
            string columnName = WildlifeMenuHelper.GetColumnName(sortColumnIndex);

            SoundDefOf.Click.PlayOneShotOnCamera();
            TolkHelper.Speak($"Sorted by {columnName} ({direction})");

            // Announce current cell after sorting (include animal name since position may have changed)
            AnnounceCurrentCell(includeAnimalName: true);
        }
    }
}
