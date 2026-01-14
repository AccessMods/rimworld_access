using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// State handler for Health tab operations and medical settings.
    /// Accessed via inspection tree: Health → Operations or Health → Health Settings.
    /// </summary>
    public static class HealthTabState
    {
        private enum MenuLevel
        {
            MedicalSettingsList,   // List medical settings
            MedicalSettingChange,  // Change a medical setting
            OperationsList,        // List operations
            OperationActions,      // Actions for operation
            AddRecipeList,         // List available recipes to add
            SelectBodyPart,        // Select body part for recipe
        }

        private static bool isActive = false;
        private static Pawn currentPawn = null;

        private static MenuLevel currentLevel = MenuLevel.OperationsList;

        // Medical Settings
        private static int medicalSettingIndex = 0;
        private static readonly List<string> medicalSettings = new List<string> { "Food Restriction", "Medical Care", "Self-Tend" };
        private static string currentSettingName = "";
        private static List<FoodPolicy> availableFoodRestrictions = new List<FoodPolicy>();
        private static List<MedicalCareCategory> availableMedicalCare = new List<MedicalCareCategory>();
        private static int settingChoiceIndex = 0;

        // Operations
        private static List<Bill> queuedOperations = new List<Bill>();
        private static List<RecipeDef> availableRecipes = new List<RecipeDef>();
        private static RecipeDef selectedRecipe = null;
        private static List<BodyPartRecord> partsForRecipe = new List<BodyPartRecord>();
        private static int operationIndex = 0;
        private static int recipeIndex = 0;
        private static int partSelectionIndex = 0;
        private static readonly List<string> operationActions = new List<string> { "View Details", "Remove Operation", "Go Back" };
        private static int operationActionIndex = 0;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens directly to the Operations section.
        /// </summary>
        public static void OpenOperations(Pawn pawn)
        {
            if (pawn == null)
                return;

            currentPawn = pawn;
            isActive = true;
            currentLevel = MenuLevel.OperationsList;
            operationIndex = 0;

            // Build operations list
            queuedOperations.Clear();

            if (currentPawn.BillStack != null)
            {
                queuedOperations.AddRange(currentPawn.BillStack.Bills);
            }

            SoundDefOf.TabOpen.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Opens directly to the Medical Settings section.
        /// </summary>
        public static void OpenMedicalSettings(Pawn pawn)
        {
            if (pawn == null)
                return;

            currentPawn = pawn;
            isActive = true;
            currentLevel = MenuLevel.MedicalSettingsList;
            medicalSettingIndex = 0;

            SoundDefOf.TabOpen.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Closes the health tab.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentPawn = null;
            SoundDefOf.TabClose.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Handles keyboard input.
        /// </summary>
        public static bool HandleInput(Event evt)
        {
            if (!isActive || evt.type != EventType.KeyDown)
                return false;

            KeyCode key = evt.keyCode;

            // Handle Escape - go back or close
            if (key == KeyCode.Escape)
            {
                evt.Use();
                GoBack();
                return true;
            }

            // Handle arrow keys
            if (key == KeyCode.UpArrow)
            {
                evt.Use();
                SelectPrevious();
                return true;
            }

            if (key == KeyCode.DownArrow)
            {
                evt.Use();
                SelectNext();
                return true;
            }

            // Handle Enter - drill down or execute
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                evt.Use();
                DrillDown();
                return true;
            }

            return false;
        }

        private static void SelectNext()
        {
            switch (currentLevel)
            {
                case MenuLevel.MedicalSettingsList:
                    medicalSettingIndex = MenuHelper.SelectNext(medicalSettingIndex, medicalSettings.Count);
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                        settingChoiceIndex = MenuHelper.SelectNext(settingChoiceIndex, availableFoodRestrictions.Count);
                    else if (currentSettingName == "Medical Care")
                        settingChoiceIndex = MenuHelper.SelectNext(settingChoiceIndex, availableMedicalCare.Count);
                    break;

                case MenuLevel.OperationsList:
                    int totalOps = queuedOperations.Count + 1; // +1 for "Add Operation"
                    operationIndex = MenuHelper.SelectNext(operationIndex, totalOps);
                    break;

                case MenuLevel.OperationActions:
                    operationActionIndex = MenuHelper.SelectNext(operationActionIndex, operationActions.Count);
                    break;

                case MenuLevel.AddRecipeList:
                    if (availableRecipes.Count > 0)
                        recipeIndex = MenuHelper.SelectNext(recipeIndex, availableRecipes.Count);
                    break;

                case MenuLevel.SelectBodyPart:
                    if (partsForRecipe.Count > 0)
                        partSelectionIndex = MenuHelper.SelectNext(partSelectionIndex, partsForRecipe.Count);
                    break;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        private static void SelectPrevious()
        {
            switch (currentLevel)
            {
                case MenuLevel.MedicalSettingsList:
                    medicalSettingIndex = MenuHelper.SelectPrevious(medicalSettingIndex, medicalSettings.Count);
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                        settingChoiceIndex = MenuHelper.SelectPrevious(settingChoiceIndex, availableFoodRestrictions.Count);
                    else if (currentSettingName == "Medical Care")
                        settingChoiceIndex = MenuHelper.SelectPrevious(settingChoiceIndex, availableMedicalCare.Count);
                    break;

                case MenuLevel.OperationsList:
                    int totalOps = queuedOperations.Count + 1;
                    operationIndex = MenuHelper.SelectPrevious(operationIndex, totalOps);
                    break;

                case MenuLevel.OperationActions:
                    operationActionIndex = MenuHelper.SelectPrevious(operationActionIndex, operationActions.Count);
                    break;

                case MenuLevel.AddRecipeList:
                    if (availableRecipes.Count > 0)
                        recipeIndex = MenuHelper.SelectPrevious(recipeIndex, availableRecipes.Count);
                    break;

                case MenuLevel.SelectBodyPart:
                    if (partsForRecipe.Count > 0)
                        partSelectionIndex = MenuHelper.SelectPrevious(partSelectionIndex, partsForRecipe.Count);
                    break;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        private static void DrillDown()
        {
            switch (currentLevel)
            {
                case MenuLevel.MedicalSettingsList:
                    currentSettingName = medicalSettings[medicalSettingIndex];
                    if (currentSettingName == "Food Restriction")
                    {
                        availableFoodRestrictions = HealthTabHelper.GetAvailableFoodRestrictions();
                        if (availableFoodRestrictions.Count == 0)
                        {
                            TolkHelper.Speak("No food restrictions available");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }
                        currentLevel = MenuLevel.MedicalSettingChange;
                        settingChoiceIndex = 0;
                    }
                    else if (currentSettingName == "Medical Care")
                    {
                        availableMedicalCare = HealthTabHelper.GetAvailableMedicalCare();
                        currentLevel = MenuLevel.MedicalSettingChange;
                        settingChoiceIndex = 0;
                    }
                    else if (currentSettingName == "Self-Tend")
                    {
                        HealthTabHelper.ToggleSelfTend(currentPawn);
                        AnnounceCurrentSelection();
                        return;
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableFoodRestrictions.Count)
                        {
                            HealthTabHelper.SetFoodRestriction(currentPawn, availableFoodRestrictions[settingChoiceIndex]);
                            currentLevel = MenuLevel.MedicalSettingsList;
                            AnnounceCurrentSelection();
                        }
                    }
                    else if (currentSettingName == "Medical Care")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableMedicalCare.Count)
                        {
                            HealthTabHelper.SetMedicalCare(currentPawn, availableMedicalCare[settingChoiceIndex]);
                            currentLevel = MenuLevel.MedicalSettingsList;
                            AnnounceCurrentSelection();
                        }
                    }
                    break;

                case MenuLevel.OperationsList:
                    if (operationIndex < queuedOperations.Count)
                    {
                        currentLevel = MenuLevel.OperationActions;
                        operationActionIndex = 0;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                    }
                    else
                    {
                        // "Add Operation" selected
                        availableRecipes = HealthTabHelper.GetAvailableRecipes(currentPawn);
                        if (availableRecipes.Count == 0)
                        {
                            TolkHelper.Speak("No operations available");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }
                        currentLevel = MenuLevel.AddRecipeList;
                        recipeIndex = 0;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                    }
                    break;

                case MenuLevel.OperationActions:
                    string action = operationActions[operationActionIndex];
                    if (action == "View Details")
                    {
                        if (operationIndex >= 0 && operationIndex < queuedOperations.Count)
                        {
                            var bill = queuedOperations[operationIndex];
                            TolkHelper.Speak($"{bill.LabelCap.StripTags()}\n\nPress Escape to go back");
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        }
                    }
                    else if (action == "Remove Operation")
                    {
                        if (operationIndex >= 0 && operationIndex < queuedOperations.Count)
                        {
                            var bill = queuedOperations[operationIndex];
                            HealthTabHelper.RemoveOperation(currentPawn, bill);
                            queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                            currentLevel = MenuLevel.OperationsList;
                            operationIndex = 0;
                            AnnounceCurrentSelection();
                        }
                    }
                    else if (action == "Go Back")
                    {
                        currentLevel = MenuLevel.OperationsList;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                    }
                    break;

                case MenuLevel.AddRecipeList:
                    if (recipeIndex >= 0 && recipeIndex < availableRecipes.Count)
                    {
                        selectedRecipe = availableRecipes[recipeIndex];

                        // Get parts that this recipe can apply to
                        partsForRecipe = HealthTabHelper.GetPartsForRecipe(currentPawn, selectedRecipe);

                        if (partsForRecipe.Count == 0)
                        {
                            // Recipe doesn't require a specific part, add it directly
                            if (selectedRecipe.Worker.AvailableOnNow(currentPawn, null))
                            {
                                HealthTabHelper.AddOperation(currentPawn, selectedRecipe, null);
                                queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                                currentLevel = MenuLevel.OperationsList;
                                operationIndex = 0;
                                AnnounceCurrentSelection();
                            }
                            else
                            {
                                TolkHelper.Speak("This operation is not available", SpeechPriority.High);
                                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            }
                        }
                        else if (partsForRecipe.Count == 1)
                        {
                            // Only one valid part, add operation directly
                            HealthTabHelper.AddOperation(currentPawn, selectedRecipe, partsForRecipe[0]);
                            queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                            currentLevel = MenuLevel.OperationsList;
                            operationIndex = 0;
                            AnnounceCurrentSelection();
                        }
                        else
                        {
                            // Multiple parts available, let user choose
                            currentLevel = MenuLevel.SelectBodyPart;
                            partSelectionIndex = 0;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                    }
                    break;

                case MenuLevel.SelectBodyPart:
                    if (partSelectionIndex >= 0 && partSelectionIndex < partsForRecipe.Count)
                    {
                        var selectedPart = partsForRecipe[partSelectionIndex];
                        if (selectedRecipe.Worker.AvailableOnNow(currentPawn, selectedPart))
                        {
                            HealthTabHelper.AddOperation(currentPawn, selectedRecipe, selectedPart);
                            queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                            currentLevel = MenuLevel.OperationsList;
                            operationIndex = 0;
                            AnnounceCurrentSelection();
                        }
                        else
                        {
                            TolkHelper.Speak("This operation is not available on this body part", SpeechPriority.High);
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                        }
                    }
                    break;
            }
        }

        private static void GoBack()
        {
            switch (currentLevel)
            {
                case MenuLevel.MedicalSettingsList:
                case MenuLevel.OperationsList:
                    Close();
                    WindowlessInspectionState.ReannounceCurrentSelection();
                    break;

                case MenuLevel.MedicalSettingChange:
                    currentLevel = MenuLevel.MedicalSettingsList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.OperationActions:
                case MenuLevel.AddRecipeList:
                    currentLevel = MenuLevel.OperationsList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.SelectBodyPart:
                    currentLevel = MenuLevel.AddRecipeList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;
            }
        }

        private static void AnnounceCurrentSelection()
        {
            var sb = new StringBuilder();

            switch (currentLevel)
            {
                case MenuLevel.MedicalSettingsList:
                    string setting = medicalSettings[medicalSettingIndex];
                    sb.AppendLine($"{setting}");

                    if (setting == "Food Restriction")
                    {
                        string current = HealthTabHelper.GetCurrentFoodRestriction(currentPawn);
                        sb.AppendLine($"Current: {current}");
                    }
                    else if (setting == "Medical Care")
                    {
                        string current = HealthTabHelper.GetCurrentMedicalCare(currentPawn);
                        sb.AppendLine($"Current: {current}");
                    }
                    else if (setting == "Self-Tend")
                    {
                        bool enabled = HealthTabHelper.GetSelfTendEnabled(currentPawn);
                        sb.AppendLine($"Current: {(enabled ? "Enabled" : "Disabled")}");
                    }

                    sb.AppendLine($"Setting {MenuHelper.FormatPosition(medicalSettingIndex, medicalSettings.Count)}");
                    sb.AppendLine("Press Enter to change");
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableFoodRestrictions.Count)
                        {
                            var restriction = availableFoodRestrictions[settingChoiceIndex];
                            sb.AppendLine($"{restriction.label}");
                            sb.AppendLine($"Option {MenuHelper.FormatPosition(settingChoiceIndex, availableFoodRestrictions.Count)}");
                        }
                    }
                    else if (currentSettingName == "Medical Care")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableMedicalCare.Count)
                        {
                            var care = availableMedicalCare[settingChoiceIndex];
                            sb.AppendLine($"{care.GetLabel()}");
                            sb.AppendLine($"Option {MenuHelper.FormatPosition(settingChoiceIndex, availableMedicalCare.Count)}");
                        }
                    }
                    sb.AppendLine("Press Enter to confirm");
                    break;

                case MenuLevel.OperationsList:
                    if (operationIndex < queuedOperations.Count)
                    {
                        var bill = queuedOperations[operationIndex];
                        sb.AppendLine($"Queued: {bill.LabelCap.StripTags()}");
                        sb.AppendLine($"Operation {MenuHelper.FormatPosition(operationIndex, queuedOperations.Count + 1)}");
                        sb.AppendLine("Press Enter for actions");
                    }
                    else
                    {
                        sb.AppendLine("Add Operation");
                        sb.AppendLine($"Operation {MenuHelper.FormatPosition(operationIndex, queuedOperations.Count + 1)}");
                        sb.AppendLine("Press Enter to add");
                    }
                    break;

                case MenuLevel.OperationActions:
                    sb.AppendLine($"{operationActions[operationActionIndex]}");
                    sb.AppendLine($"Action {MenuHelper.FormatPosition(operationActionIndex, operationActions.Count)}");
                    sb.AppendLine("Press Enter to execute");
                    break;

                case MenuLevel.AddRecipeList:
                    if (recipeIndex >= 0 && recipeIndex < availableRecipes.Count)
                    {
                        var recipe = availableRecipes[recipeIndex];
                        sb.AppendLine($"{recipe.LabelCap.ToString().StripTags()}");

                        if (!string.IsNullOrEmpty(recipe.description))
                        {
                            sb.AppendLine(recipe.description);
                        }

                        // Show ingredient requirements
                        if (recipe.ingredients != null && recipe.ingredients.Count > 0)
                        {
                            sb.Append("Requires: ");
                            foreach (var ingredient in recipe.ingredients)
                            {
                                sb.Append($"{ingredient.Summary}, ");
                            }
                            sb.Length -= 2; // Remove trailing ", "
                            sb.AppendLine();
                        }

                        sb.AppendLine($"Recipe {MenuHelper.FormatPosition(recipeIndex, availableRecipes.Count)}");
                        sb.AppendLine("Press Enter to select");
                    }
                    break;

                case MenuLevel.SelectBodyPart:
                    if (partSelectionIndex >= 0 && partSelectionIndex < partsForRecipe.Count)
                    {
                        var part = partsForRecipe[partSelectionIndex];
                        sb.AppendLine($"{selectedRecipe.LabelCap.ToString().StripTags()}");
                        sb.AppendLine($"Body part: {part.Label}");

                        // Show health information about the part
                        float health = currentPawn.health.hediffSet.GetPartHealth(part);
                        float maxHealth = part.def.GetMaxHealth(currentPawn);
                        sb.AppendLine($"Health: {health:F0} / {maxHealth:F0}");

                        sb.AppendLine($"Part {MenuHelper.FormatPosition(partSelectionIndex, partsForRecipe.Count)}");
                        sb.AppendLine("Press Enter to add operation");
                    }
                    break;
            }

            TolkHelper.Speak(sb.ToString());
        }
    }
}
