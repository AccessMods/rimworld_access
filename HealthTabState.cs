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
    /// State handler for the Health tab in the inspection menu.
    /// Manages hierarchical navigation through medical settings, capacities, operations, and body parts.
    /// </summary>
    public static class HealthTabState
    {
        private enum MenuLevel
        {
            SectionMenu,           // Level 1: Choose section (Settings/Capacities/Operations/BodyParts)
            MedicalSettingsList,   // Level 2a: List medical settings
            MedicalSettingChange,  // Level 3a: Change a medical setting
            CapacitiesList,        // Level 2b: List capacities
            CapacityDetail,        // Level 3b: View capacity details
            OperationsList,        // Level 2c: List operations
            OperationActions,      // Level 3c: Actions for operation
            AddOperationList,      // Level 3d: List available operations to add
            BodyPartsList,         // Level 2d: List body parts
            HediffsList,           // Level 3e: List hediffs on a part
            HediffDetail           // Level 4e: View hediff details
        }

        private static bool isActive = false;
        private static Pawn currentPawn = null;

        private static MenuLevel currentLevel = MenuLevel.SectionMenu;
        private static int sectionIndex = 0;
        private static readonly List<string> sections = new List<string> { "Medical Settings", "Capacities", "Operations", "Body Parts" };

        // Medical Settings
        private static int medicalSettingIndex = 0;
        private static readonly List<string> medicalSettings = new List<string> { "Food Restriction", "Medical Care", "Self-Tend" };
        private static string currentSettingName = "";
        private static List<FoodPolicy> availableFoodRestrictions = new List<FoodPolicy>();
        private static List<MedicalCareCategory> availableMedicalCare = new List<MedicalCareCategory>();
        private static int settingChoiceIndex = 0;

        // Capacities
        private static List<HealthTabHelper.CapacityInfo> capacities = new List<HealthTabHelper.CapacityInfo>();
        private static int capacityIndex = 0;

        // Operations
        private static List<Bill> queuedOperations = new List<Bill>();
        private static List<HealthTabHelper.OperationInfo> availableOperations = new List<HealthTabHelper.OperationInfo>();
        private static int operationIndex = 0;
        private static readonly List<string> operationActions = new List<string> { "View Details", "Remove Operation", "Go Back" };
        private static int operationActionIndex = 0;

        // Body Parts
        private static List<HealthTabHelper.BodyPartInfo> bodyParts = new List<HealthTabHelper.BodyPartInfo>();
        private static int bodyPartIndex = 0;
        private static int hediffIndex = 0;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the health tab for a pawn.
        /// </summary>
        public static void Open(Pawn pawn)
        {
            if (pawn == null)
                return;

            currentPawn = pawn;
            isActive = true;
            currentLevel = MenuLevel.SectionMenu;
            sectionIndex = 0;

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
                case MenuLevel.SectionMenu:
                    sectionIndex = (sectionIndex + 1) % sections.Count;
                    break;

                case MenuLevel.MedicalSettingsList:
                    medicalSettingIndex = (medicalSettingIndex + 1) % medicalSettings.Count;
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                        settingChoiceIndex = (settingChoiceIndex + 1) % availableFoodRestrictions.Count;
                    else if (currentSettingName == "Medical Care")
                        settingChoiceIndex = (settingChoiceIndex + 1) % availableMedicalCare.Count;
                    break;

                case MenuLevel.CapacitiesList:
                    if (capacities.Count > 0)
                        capacityIndex = (capacityIndex + 1) % capacities.Count;
                    break;

                case MenuLevel.OperationsList:
                    int totalOps = queuedOperations.Count + 1; // +1 for "Add Operation"
                    operationIndex = (operationIndex + 1) % totalOps;
                    break;

                case MenuLevel.OperationActions:
                    operationActionIndex = (operationActionIndex + 1) % operationActions.Count;
                    break;

                case MenuLevel.AddOperationList:
                    if (availableOperations.Count > 0)
                        operationIndex = (operationIndex + 1) % availableOperations.Count;
                    break;

                case MenuLevel.BodyPartsList:
                    if (bodyParts.Count > 0)
                        bodyPartIndex = (bodyPartIndex + 1) % bodyParts.Count;
                    break;

                case MenuLevel.HediffsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        int hediffCount = bodyParts[bodyPartIndex].Hediffs.Count;
                        if (hediffCount > 0)
                            hediffIndex = (hediffIndex + 1) % hediffCount;
                    }
                    break;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        private static void SelectPrevious()
        {
            switch (currentLevel)
            {
                case MenuLevel.SectionMenu:
                    sectionIndex = (sectionIndex - 1 + sections.Count) % sections.Count;
                    break;

                case MenuLevel.MedicalSettingsList:
                    medicalSettingIndex = (medicalSettingIndex - 1 + medicalSettings.Count) % medicalSettings.Count;
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                        settingChoiceIndex = (settingChoiceIndex - 1 + availableFoodRestrictions.Count) % availableFoodRestrictions.Count;
                    else if (currentSettingName == "Medical Care")
                        settingChoiceIndex = (settingChoiceIndex - 1 + availableMedicalCare.Count) % availableMedicalCare.Count;
                    break;

                case MenuLevel.CapacitiesList:
                    if (capacities.Count > 0)
                        capacityIndex = (capacityIndex - 1 + capacities.Count) % capacities.Count;
                    break;

                case MenuLevel.OperationsList:
                    int totalOps = queuedOperations.Count + 1;
                    operationIndex = (operationIndex - 1 + totalOps) % totalOps;
                    break;

                case MenuLevel.OperationActions:
                    operationActionIndex = (operationActionIndex - 1 + operationActions.Count) % operationActions.Count;
                    break;

                case MenuLevel.AddOperationList:
                    if (availableOperations.Count > 0)
                        operationIndex = (operationIndex - 1 + availableOperations.Count) % availableOperations.Count;
                    break;

                case MenuLevel.BodyPartsList:
                    if (bodyParts.Count > 0)
                        bodyPartIndex = (bodyPartIndex - 1 + bodyParts.Count) % bodyParts.Count;
                    break;

                case MenuLevel.HediffsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        int hediffCount = bodyParts[bodyPartIndex].Hediffs.Count;
                        if (hediffCount > 0)
                            hediffIndex = (hediffIndex - 1 + hediffCount) % hediffCount;
                    }
                    break;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        private static void DrillDown()
        {
            switch (currentLevel)
            {
                case MenuLevel.SectionMenu:
                    string section = sections[sectionIndex];
                    if (section == "Medical Settings")
                    {
                        currentLevel = MenuLevel.MedicalSettingsList;
                        medicalSettingIndex = 0;
                    }
                    else if (section == "Capacities")
                    {
                        capacities = HealthTabHelper.GetCapacities(currentPawn);
                        if (capacities.Count == 0)
                        {
                            ClipboardHelper.CopyToClipboard("No capacity information available");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }
                        currentLevel = MenuLevel.CapacitiesList;
                        capacityIndex = 0;
                    }
                    else if (section == "Operations")
                    {
                        queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                        currentLevel = MenuLevel.OperationsList;
                        operationIndex = 0;
                    }
                    else if (section == "Body Parts")
                    {
                        bodyParts = HealthTabHelper.GetBodyPartsWithHediffs(currentPawn);
                        if (bodyParts.Count == 0)
                        {
                            ClipboardHelper.CopyToClipboard("No health conditions");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }
                        currentLevel = MenuLevel.BodyPartsList;
                        bodyPartIndex = 0;
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.MedicalSettingsList:
                    currentSettingName = medicalSettings[medicalSettingIndex];
                    if (currentSettingName == "Food Restriction")
                    {
                        availableFoodRestrictions = HealthTabHelper.GetAvailableFoodRestrictions();
                        if (availableFoodRestrictions.Count == 0)
                        {
                            ClipboardHelper.CopyToClipboard("No food restrictions available");
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

                case MenuLevel.CapacitiesList:
                    if (capacityIndex >= 0 && capacityIndex < capacities.Count)
                    {
                        currentLevel = MenuLevel.CapacityDetail;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
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
                        availableOperations = HealthTabHelper.GetAvailableOperations(currentPawn);
                        if (availableOperations.Count == 0)
                        {
                            ClipboardHelper.CopyToClipboard("No operations available");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }
                        currentLevel = MenuLevel.AddOperationList;
                        operationIndex = 0;
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
                            ClipboardHelper.CopyToClipboard($"{bill.LabelCap.StripTags()}\n\nPress Escape to go back");
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

                case MenuLevel.AddOperationList:
                    if (operationIndex >= 0 && operationIndex < availableOperations.Count)
                    {
                        var op = availableOperations[operationIndex];
                        if (op.IsAvailable)
                        {
                            HealthTabHelper.AddOperation(currentPawn, op.Recipe, op.BodyPart);
                            queuedOperations = HealthTabHelper.GetQueuedOperations(currentPawn);
                            currentLevel = MenuLevel.OperationsList;
                            operationIndex = 0;
                            AnnounceCurrentSelection();
                        }
                        else
                        {
                            ClipboardHelper.CopyToClipboard($"Cannot add: {op.UnavailableReason}");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                        }
                    }
                    break;

                case MenuLevel.BodyPartsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        var part = bodyParts[bodyPartIndex];
                        if (part.Hediffs.Count > 0)
                        {
                            currentLevel = MenuLevel.HediffsList;
                            hediffIndex = 0;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        else
                        {
                            ClipboardHelper.CopyToClipboard("No conditions on this body part");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                        }
                    }
                    break;

                case MenuLevel.HediffsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        var part = bodyParts[bodyPartIndex];
                        if (hediffIndex >= 0 && hediffIndex < part.Hediffs.Count)
                        {
                            currentLevel = MenuLevel.HediffDetail;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                    }
                    break;
            }
        }

        private static void GoBack()
        {
            switch (currentLevel)
            {
                case MenuLevel.SectionMenu:
                    Close();
                    ClipboardHelper.CopyToClipboard("Closed Health tab");
                    break;

                case MenuLevel.MedicalSettingsList:
                case MenuLevel.CapacitiesList:
                case MenuLevel.OperationsList:
                case MenuLevel.BodyPartsList:
                    currentLevel = MenuLevel.SectionMenu;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.MedicalSettingChange:
                    currentLevel = MenuLevel.MedicalSettingsList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.CapacityDetail:
                    currentLevel = MenuLevel.CapacitiesList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.OperationActions:
                case MenuLevel.AddOperationList:
                    currentLevel = MenuLevel.OperationsList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.HediffsList:
                    currentLevel = MenuLevel.BodyPartsList;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    break;

                case MenuLevel.HediffDetail:
                    currentLevel = MenuLevel.HediffsList;
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
                case MenuLevel.SectionMenu:
                    sb.AppendLine($"Health - {sections[sectionIndex]}");
                    sb.AppendLine($"Section {sectionIndex + 1} of {sections.Count}");
                    sb.AppendLine("Press Enter to open, Escape to close");
                    break;

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

                    sb.AppendLine($"Setting {medicalSettingIndex + 1} of {medicalSettings.Count}");
                    sb.AppendLine("Press Enter to change, Escape to go back");
                    break;

                case MenuLevel.MedicalSettingChange:
                    if (currentSettingName == "Food Restriction")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableFoodRestrictions.Count)
                        {
                            var restriction = availableFoodRestrictions[settingChoiceIndex];
                            sb.AppendLine($"{restriction.label}");
                            sb.AppendLine($"Option {settingChoiceIndex + 1} of {availableFoodRestrictions.Count}");
                        }
                    }
                    else if (currentSettingName == "Medical Care")
                    {
                        if (settingChoiceIndex >= 0 && settingChoiceIndex < availableMedicalCare.Count)
                        {
                            var care = availableMedicalCare[settingChoiceIndex];
                            sb.AppendLine($"{care.GetLabel()}");
                            sb.AppendLine($"Option {settingChoiceIndex + 1} of {availableMedicalCare.Count}");
                        }
                    }
                    sb.AppendLine("Press Enter to confirm, Escape to cancel");
                    break;

                case MenuLevel.CapacitiesList:
                    if (capacityIndex >= 0 && capacityIndex < capacities.Count)
                    {
                        var capacity = capacities[capacityIndex];
                        sb.AppendLine($"{capacity.Label}: {capacity.LevelLabel}");
                        sb.AppendLine($"Capacity {capacityIndex + 1} of {capacities.Count}");
                        sb.AppendLine("Press Enter for details, Escape to go back");
                    }
                    break;

                case MenuLevel.CapacityDetail:
                    if (capacityIndex >= 0 && capacityIndex < capacities.Count)
                    {
                        var capacity = capacities[capacityIndex];
                        sb.AppendLine(capacity.DetailedBreakdown);
                        sb.AppendLine();
                        sb.AppendLine("Press Escape to go back");
                    }
                    break;

                case MenuLevel.OperationsList:
                    if (operationIndex < queuedOperations.Count)
                    {
                        var bill = queuedOperations[operationIndex];
                        sb.AppendLine($"Queued: {bill.LabelCap.StripTags()}");
                        sb.AppendLine($"Operation {operationIndex + 1} of {queuedOperations.Count + 1}");
                        sb.AppendLine("Press Enter for actions, Escape to go back");
                    }
                    else
                    {
                        sb.AppendLine("Add Operation");
                        sb.AppendLine($"Operation {operationIndex + 1} of {queuedOperations.Count + 1}");
                        sb.AppendLine("Press Enter to add, Escape to go back");
                    }
                    break;

                case MenuLevel.OperationActions:
                    sb.AppendLine($"{operationActions[operationActionIndex]}");
                    sb.AppendLine($"Action {operationActionIndex + 1} of {operationActions.Count}");
                    sb.AppendLine("Press Enter to execute, Escape to go back");
                    break;

                case MenuLevel.AddOperationList:
                    if (operationIndex >= 0 && operationIndex < availableOperations.Count)
                    {
                        var op = availableOperations[operationIndex];
                        sb.AppendLine($"{op.Label}");
                        if (!string.IsNullOrEmpty(op.Requirements))
                        {
                            sb.AppendLine(op.Requirements);
                        }
                        if (!op.IsAvailable)
                        {
                            sb.AppendLine($"Unavailable: {op.UnavailableReason}");
                        }
                        sb.AppendLine($"Operation {operationIndex + 1} of {availableOperations.Count}");
                        sb.AppendLine("Press Enter to add, Escape to go back");
                    }
                    break;

                case MenuLevel.BodyPartsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        var part = bodyParts[bodyPartIndex];
                        sb.AppendLine($"{part.Label}");
                        if (part.MaxHealth > 0)
                        {
                            sb.AppendLine($"Health: {part.Health:F0} / {part.MaxHealth:F0} ({part.Efficiency:P0})");
                        }
                        sb.AppendLine($"Conditions: {part.Hediffs.Count}");
                        sb.AppendLine($"Part {bodyPartIndex + 1} of {bodyParts.Count}");
                        sb.AppendLine("Press Enter to view conditions, Escape to go back");
                    }
                    break;

                case MenuLevel.HediffsList:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        var part = bodyParts[bodyPartIndex];
                        if (hediffIndex >= 0 && hediffIndex < part.Hediffs.Count)
                        {
                            var hediff = part.Hediffs[hediffIndex];
                            sb.AppendLine($"{hediff.Label}");
                            sb.AppendLine($"Condition {hediffIndex + 1} of {part.Hediffs.Count} on {part.Label}");
                            sb.AppendLine("Press Enter for details, Escape to go back");
                        }
                    }
                    break;

                case MenuLevel.HediffDetail:
                    if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Count)
                    {
                        var part = bodyParts[bodyPartIndex];
                        if (hediffIndex >= 0 && hediffIndex < part.Hediffs.Count)
                        {
                            var hediff = part.Hediffs[hediffIndex];
                            sb.AppendLine(hediff.DetailedInfo);
                            sb.AppendLine();
                            sb.AppendLine("Press Escape to go back");
                        }
                    }
                    break;
            }

            ClipboardHelper.CopyToClipboard(sb.ToString());
        }
    }
}
