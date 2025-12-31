using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    public static class AnimalsMenuState
    {
        public static bool IsActive { get; private set; } = false;

        private static List<Pawn> animalsList = new List<Pawn>();
        private static int currentAnimalIndex = 0;
        private static int currentColumnIndex = 0;
        private static int sortColumnIndex = 0; // Column used for sorting (default: Name)
        private static bool sortDescending = false;

        // Submenu state
        private enum SubmenuType { None, Master, AllowedArea, MedicalCare, FoodRestriction }
        private static SubmenuType activeSubmenu = SubmenuType.None;
        private static int submenuSelectedIndex = 0;
        private static List<object> submenuOptions = new List<object>();

        public static void Open()
        {
            if (Find.CurrentMap == null)
            {
                TolkHelper.Speak("No map loaded");
                return;
            }

            // Get all colony animals
            animalsList = Find.CurrentMap.mapPawns.ColonyAnimals.ToList();

            if (animalsList.Count == 0)
            {
                TolkHelper.Speak("No colony animals found");
                return;
            }

            // Apply default sort (by name)
            animalsList = AnimalsMenuHelper.SortAnimalsByColumn(animalsList, sortColumnIndex, sortDescending);

            currentAnimalIndex = 0;
            currentColumnIndex = 0;
            activeSubmenu = SubmenuType.None;
            IsActive = true;

            SoundDefOf.TabOpen.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        public static void Close()
        {
            IsActive = false;
            activeSubmenu = SubmenuType.None;
            animalsList.Clear();
            SoundDefOf.TabClose.PlayOneShotOnCamera();
            TolkHelper.Speak("Animals menu closed");
        }

        public static void HandleInput()
        {
            if (!IsActive) return;

            // Handle submenu input if active
            if (activeSubmenu != SubmenuType.None)
            {
                HandleSubmenuInput();
                return;
            }

            // Main menu input handling
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
            if (animalsList.Count == 0) return;

            currentAnimalIndex = MenuHelper.SelectNext(currentAnimalIndex, animalsList.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        private static void SelectPreviousAnimal()
        {
            if (animalsList.Count == 0) return;

            currentAnimalIndex = MenuHelper.SelectPrevious(currentAnimalIndex, animalsList.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: true);
        }

        private static void SelectNextColumn()
        {
            int totalColumns = AnimalsMenuHelper.GetTotalColumnCount();
            currentColumnIndex = MenuHelper.SelectNext(currentColumnIndex, totalColumns);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void SelectPreviousColumn()
        {
            int totalColumns = AnimalsMenuHelper.GetTotalColumnCount();
            currentColumnIndex = MenuHelper.SelectPrevious(currentColumnIndex, totalColumns);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void AnnounceCurrentCell(bool includeAnimalName = true)
        {
            if (animalsList.Count == 0) return;

            Pawn currentAnimal = animalsList[currentAnimalIndex];
            string columnName = AnimalsMenuHelper.GetColumnName(currentColumnIndex);
            string columnValue = AnimalsMenuHelper.GetColumnValue(currentAnimal, currentColumnIndex);

            string announcement;
            if (includeAnimalName)
            {
                string animalName = AnimalsMenuHelper.GetAnimalName(currentAnimal);
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
            if (animalsList.Count == 0) return;

            Pawn currentAnimal = animalsList[currentAnimalIndex];

            if (!AnimalsMenuHelper.IsColumnInteractive(currentColumnIndex))
            {
                // Just re-announce for non-interactive columns
                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentCell(includeAnimalName: false);
                return;
            }

            // Handle interaction based on column type
            if (currentColumnIndex < 8) // Fixed columns before training
            {
                switch ((AnimalsMenuHelper.ColumnType)currentColumnIndex)
                {
                    case AnimalsMenuHelper.ColumnType.Master:
                        OpenMasterSubmenu(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.Slaughter:
                        ToggleSlaughter(currentAnimal);
                        break;
                }
            }
            else if (currentColumnIndex < 8 + AnimalsMenuHelper.GetAllTrainables().Count)
            {
                // Training column
                ToggleTraining(currentAnimal, currentColumnIndex);
            }
            else
            {
                // Fixed columns after training
                int fixedIndex = currentColumnIndex - 8 - AnimalsMenuHelper.GetAllTrainables().Count;
                AnimalsMenuHelper.ColumnType type = (AnimalsMenuHelper.ColumnType)(8 + fixedIndex);

                switch (type)
                {
                    case AnimalsMenuHelper.ColumnType.FollowDrafted:
                        ToggleFollowDrafted(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.FollowFieldwork:
                        ToggleFollowFieldwork(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.AllowedArea:
                        OpenAllowedAreaSubmenu(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.MedicalCare:
                        OpenMedicalCareSubmenu(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.FoodRestriction:
                        OpenFoodRestrictionSubmenu(currentAnimal);
                        break;
                    case AnimalsMenuHelper.ColumnType.ReleaseToWild:
                        ToggleReleaseToWild(currentAnimal);
                        break;
                }
            }
        }

        // === Cell Interaction Methods ===

        private static void ToggleSlaughter(Pawn pawn)
        {
            if (pawn.Map == null) return;

            Designation existing = pawn.Map.designationManager.DesignationOn(pawn, DesignationDefOf.Slaughter);

            if (existing != null)
            {
                // Remove slaughter designation
                pawn.Map.designationManager.RemoveDesignation(existing);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            else
            {
                // Check if bonded
                bool isBonded = pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) != null;

                if (isBonded)
                {
                    TolkHelper.Speak($"{pawn.Name.ToStringShort} is bonded. Marking for slaughter anyway.");
                }

                // Add slaughter designation
                pawn.Map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.Slaughter));
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleTraining(Pawn pawn, int columnIndex)
        {
            TrainableDef trainable = AnimalsMenuHelper.GetTrainableAtColumn(columnIndex);
            if (trainable == null || pawn.training == null) return;

            AcceptanceReport canTrain = pawn.training.CanAssignToTrain(trainable);
            if (!canTrain.Accepted)
            {
                TolkHelper.Speak($"{pawn.Name.ToStringShort} cannot be trained in {trainable.LabelCap}", SpeechPriority.High);
                return;
            }

            bool currentlyWanted = pawn.training.GetWanted(trainable);
            pawn.training.SetWantedRecursive(trainable, !currentlyWanted);

            if (!currentlyWanted)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleFollowDrafted(Pawn pawn)
        {
            if (pawn.playerSettings == null) return;

            pawn.playerSettings.followDrafted = !pawn.playerSettings.followDrafted;

            if (pawn.playerSettings.followDrafted)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleFollowFieldwork(Pawn pawn)
        {
            if (pawn.playerSettings == null) return;

            pawn.playerSettings.followFieldwork = !pawn.playerSettings.followFieldwork;

            if (pawn.playerSettings.followFieldwork)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void ToggleReleaseToWild(Pawn pawn)
        {
            if (pawn.Map == null) return;

            Designation existing = pawn.Map.designationManager.DesignationOn(pawn, DesignationDefOf.ReleaseAnimalToWild);

            if (existing != null)
            {
                pawn.Map.designationManager.RemoveDesignation(existing);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            else
            {
                pawn.Map.designationManager.AddDesignation(new Designation(pawn, DesignationDefOf.ReleaseAnimalToWild));
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }

            AnnounceCurrentCell(includeAnimalName: false);
        }

        // === Submenu System ===

        private static void OpenMasterSubmenu(Pawn pawn)
        {
            List<Pawn> colonists = AnimalsMenuHelper.GetAvailableColonists();

            // Add "None" option at the beginning
            submenuOptions.Clear();
            submenuOptions.Add(null); // null = no master
            submenuOptions.AddRange(colonists.Cast<object>());

            submenuSelectedIndex = 0;

            // Find current master in list
            if (pawn.playerSettings?.Master != null)
            {
                for (int i = 0; i < colonists.Count; i++)
                {
                    if (colonists[i] == pawn.playerSettings.Master)
                    {
                        submenuSelectedIndex = i + 1; // +1 because of "None" option
                        break;
                    }
                }
            }

            activeSubmenu = SubmenuType.Master;
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceSubmenuOption();
        }

        private static void OpenAllowedAreaSubmenu(Pawn pawn)
        {
            List<Area> areas = AnimalsMenuHelper.GetAvailableAreas();

            submenuOptions.Clear();
            submenuOptions.Add(null); // null = unrestricted
            submenuOptions.AddRange(areas.Cast<object>());

            submenuSelectedIndex = 0;

            // Find current area in list
            if (pawn.playerSettings?.AreaRestrictionInPawnCurrentMap != null)
            {
                for (int i = 0; i < areas.Count; i++)
                {
                    if (areas[i] == pawn.playerSettings.AreaRestrictionInPawnCurrentMap)
                    {
                        submenuSelectedIndex = i + 1;
                        break;
                    }
                }
            }

            activeSubmenu = SubmenuType.AllowedArea;
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceSubmenuOption();
        }

        private static void OpenMedicalCareSubmenu(Pawn pawn)
        {
            List<MedicalCareCategory> levels = AnimalsMenuHelper.GetMedicalCareLevels();

            submenuOptions.Clear();
            submenuOptions.AddRange(levels.Cast<object>());

            // Find current medical care level
            submenuSelectedIndex = 0;
            if (pawn.playerSettings != null)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    if (levels[i] == pawn.playerSettings.medCare)
                    {
                        submenuSelectedIndex = i;
                        break;
                    }
                }
            }

            activeSubmenu = SubmenuType.MedicalCare;
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceSubmenuOption();
        }

        private static void OpenFoodRestrictionSubmenu(Pawn pawn)
        {
            List<FoodPolicy> policies = AnimalsMenuHelper.GetFoodPolicies();

            submenuOptions.Clear();
            submenuOptions.AddRange(policies.Cast<object>());

            // Find current food restriction
            submenuSelectedIndex = 0;
            if (pawn.foodRestriction?.CurrentFoodPolicy != null)
            {
                for (int i = 0; i < policies.Count; i++)
                {
                    if (policies[i] == pawn.foodRestriction.CurrentFoodPolicy)
                    {
                        submenuSelectedIndex = i;
                        break;
                    }
                }
            }

            activeSubmenu = SubmenuType.FoodRestriction;
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceSubmenuOption();
        }

        private static void HandleSubmenuInput()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow))
            {
                submenuSelectedIndex = MenuHelper.SelectPrevious(submenuSelectedIndex, submenuOptions.Count);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceSubmenuOption();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow))
            {
                submenuSelectedIndex = MenuHelper.SelectNext(submenuSelectedIndex, submenuOptions.Count);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceSubmenuOption();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) ||
                     UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter))
            {
                ApplySubmenuSelection();
            }
            else if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
            {
                CloseSubmenu();
            }
        }

        private static void AnnounceSubmenuOption()
        {
            if (submenuOptions.Count == 0) return;

            string optionText = "Unknown";

            object selectedOption = submenuOptions[submenuSelectedIndex];

            if (selectedOption == null)
            {
                optionText = activeSubmenu == SubmenuType.Master ? "None" : "Unrestricted";
            }
            else if (selectedOption is Pawn colonist)
            {
                optionText = colonist.Name.ToStringShort;
            }
            else if (selectedOption is Area area)
            {
                optionText = area.Label;
            }
            else if (selectedOption is MedicalCareCategory medCare)
            {
                optionText = medCare.GetLabel();
            }
            else if (selectedOption is FoodPolicy foodPolicy)
            {
                optionText = foodPolicy.label;
            }

            string announcement = $"{optionText} ({MenuHelper.FormatPosition(submenuSelectedIndex, submenuOptions.Count)})";
            TolkHelper.Speak(announcement);
        }

        private static void ApplySubmenuSelection()
        {
            if (animalsList.Count == 0 || submenuOptions.Count == 0) return;

            Pawn currentAnimal = animalsList[currentAnimalIndex];
            object selectedOption = submenuOptions[submenuSelectedIndex];

            switch (activeSubmenu)
            {
                case SubmenuType.Master:
                    if (currentAnimal.playerSettings != null)
                    {
                        currentAnimal.playerSettings.Master = selectedOption as Pawn;
                    }
                    break;

                case SubmenuType.AllowedArea:
                    if (currentAnimal.playerSettings != null)
                    {
                        currentAnimal.playerSettings.AreaRestrictionInPawnCurrentMap = selectedOption as Area;
                    }
                    break;

                case SubmenuType.MedicalCare:
                    if (currentAnimal.playerSettings != null && selectedOption is MedicalCareCategory medCare)
                    {
                        currentAnimal.playerSettings.medCare = medCare;
                    }
                    break;

                case SubmenuType.FoodRestriction:
                    if (currentAnimal.foodRestriction != null && selectedOption is FoodPolicy foodPolicy)
                    {
                        currentAnimal.foodRestriction.CurrentFoodPolicy = foodPolicy;
                    }
                    break;
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
            CloseSubmenu();
            AnnounceCurrentCell(includeAnimalName: false);
        }

        private static void CloseSubmenu()
        {
            activeSubmenu = SubmenuType.None;
            submenuOptions.Clear();
            submenuSelectedIndex = 0;
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
            animalsList = AnimalsMenuHelper.SortAnimalsByColumn(animalsList, sortColumnIndex, sortDescending);

            // Try to keep the same animal selected
            Pawn currentAnimal = null;
            if (currentAnimalIndex < animalsList.Count)
            {
                currentAnimal = animalsList[currentAnimalIndex];
            }

            if (currentAnimal != null)
            {
                currentAnimalIndex = animalsList.IndexOf(currentAnimal);
                if (currentAnimalIndex < 0) currentAnimalIndex = 0;
            }
            else
            {
                currentAnimalIndex = 0;
            }

            string direction = sortDescending ? "descending" : "ascending";
            string columnName = AnimalsMenuHelper.GetColumnName(sortColumnIndex);

            SoundDefOf.Click.PlayOneShotOnCamera();
            TolkHelper.Speak($"Sorted by {columnName} ({direction})");

            // Announce current cell after sorting (include animal name since position may have changed)
            AnnounceCurrentCell(includeAnimalName: true);
        }
    }
}
