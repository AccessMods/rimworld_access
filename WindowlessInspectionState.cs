using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the windowless inspection panel state.
    /// Multi-level hierarchical menu with interactive gear management.
    /// </summary>
    public static class WindowlessInspectionState
    {
        private enum MenuLevel
        {
            ObjectList,        // Level 1: List of objects at cursor position
            CategoryMenu,      // Level 2: List of categories for selected object
            GearCategory,      // Level 3: Gear categories (Equipment/Apparel/Inventory)
            GearItemList,      // Level 4: List of items in selected gear category
            GearItemActions,   // Level 5: Actions for selected item (Drop/Consume/Info)
            SkillList,         // Level 3: List of skills (sorted highest to lowest)
            SkillDetail,       // Level 4: Detailed description of selected skill
            DetailedInfo       // Level 3: Detailed text information (for non-gear/non-skills categories)
        }

        public static bool IsActive { get; private set; } = false;

        private static MenuLevel currentLevel = MenuLevel.ObjectList;
        private static int objectListIndex = 0;
        private static int categoryMenuIndex = 0;
        private static int gearCategoryIndex = 0;
        private static int gearItemIndex = 0;
        private static int gearActionIndex = 0;
        private static int skillIndex = 0;

        private static List<object> availableObjects = new List<object>();
        private static List<string> availableCategories = new List<string>();
        private static List<string> gearCategories = new List<string> { "Equipment", "Apparel", "Inventory" };
        private static List<InteractiveGearHelper.GearItem> gearItemList = new List<InteractiveGearHelper.GearItem>();
        private static List<string> gearActionList = new List<string>();
        private static List<SkillRecord> skillList = new List<SkillRecord>();
        private static string currentDetailedInfo = "";
        private static object currentSelectedObject = null;
        private static string currentSelectedCategory = "";
        private static InteractiveGearHelper.GearItem currentSelectedGearItem = null;
        private static SkillRecord currentSelectedSkill = null;

        private static IntVec3 inspectionPosition;
        private static bool includeSelectedPawn = false;

        /// <summary>
        /// Opens the inspection menu for the specified position.
        /// </summary>
        public static void Open(IntVec3 position)
        {
            try
            {
                inspectionPosition = position;
                currentLevel = MenuLevel.ObjectList;
                objectListIndex = 0;
                categoryMenuIndex = 0;

                // Build the object list
                BuildObjectList();

                if (availableObjects.Count == 0)
                {
                    // No objects to inspect
                    ClipboardHelper.CopyToClipboard("No items here to inspect.");
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return;
                }

                IsActive = true;
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error opening inspection menu: {ex}");
                Close();
            }
        }

        /// <summary>
        /// Closes the inspection menu.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            currentLevel = MenuLevel.ObjectList;
            objectListIndex = 0;
            categoryMenuIndex = 0;
            gearCategoryIndex = 0;
            gearItemIndex = 0;
            gearActionIndex = 0;
            skillIndex = 0;
            availableObjects.Clear();
            availableCategories.Clear();
            gearItemList.Clear();
            gearActionList.Clear();
            skillList.Clear();
            currentDetailedInfo = "";
            currentSelectedObject = null;
            currentSelectedCategory = "";
            currentSelectedGearItem = null;
            currentSelectedSkill = null;
        }

        /// <summary>
        /// Builds the list of inspectable objects at the cursor position.
        /// </summary>
        private static void BuildObjectList()
        {
            availableObjects.Clear();
            includeSelectedPawn = false;

            if (Find.CurrentMap == null)
                return;

            // Check if there's a selected pawn (from Tab cycling)
            Pawn selectedPawn = Find.Selector.SingleSelectedThing as Pawn;
            if (selectedPawn == null && Find.Selector.SelectedPawns != null && Find.Selector.SelectedPawns.Any())
            {
                selectedPawn = Find.Selector.SelectedPawns.FirstOrDefault();
            }

            // If there's a selected pawn, add it as the first option
            if (selectedPawn != null)
            {
                availableObjects.Add(selectedPawn);
                includeSelectedPawn = true;
            }

            // Get all selectable objects at the cursor position
            var objectsAtPosition = Selector.SelectableObjectsAt(inspectionPosition, Find.CurrentMap);

            // Filter and add objects (excluding the selected pawn if already added)
            foreach (var obj in objectsAtPosition)
            {
                // Skip if this is the selected pawn we already added
                if (includeSelectedPawn && obj == selectedPawn)
                    continue;

                // Only include things we want to inspect
                if (obj is Pawn || obj is Building || obj is Plant || obj is Thing)
                {
                    availableObjects.Add(obj);
                }
            }
        }

        /// <summary>
        /// Builds the gear item list for the selected category.
        /// </summary>
        private static void BuildGearItemList()
        {
            gearItemList.Clear();

            if (!(currentSelectedObject is Pawn pawn))
                return;

            string category = gearCategories[gearCategoryIndex];

            switch (category)
            {
                case "Equipment":
                    gearItemList = InteractiveGearHelper.GetEquipmentItems(pawn);
                    break;
                case "Apparel":
                    gearItemList = InteractiveGearHelper.GetApparelItems(pawn);
                    break;
                case "Inventory":
                    gearItemList = InteractiveGearHelper.GetInventoryItems(pawn);
                    break;
            }
        }

        /// <summary>
        /// Builds the skill list sorted by level (highest to lowest).
        /// </summary>
        private static void BuildSkillList()
        {
            skillList.Clear();

            if (!(currentSelectedObject is Pawn pawn))
                return;

            if (pawn.skills?.skills == null)
                return;

            // Get all skills and sort by level (highest to lowest)
            skillList = pawn.skills.skills
                .OrderByDescending(s => s.Level)
                .ToList();
        }

        /// <summary>
        /// Selects the next item in the current menu level.
        /// </summary>
        public static void SelectNext()
        {
            if (!IsActive) return;

            try
            {
                switch (currentLevel)
                {
                    case MenuLevel.ObjectList:
                        if (availableObjects.Count > 0)
                        {
                            objectListIndex = (objectListIndex + 1) % availableObjects.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.CategoryMenu:
                        if (availableCategories.Count > 0)
                        {
                            categoryMenuIndex = (categoryMenuIndex + 1) % availableCategories.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearCategory:
                        if (gearCategories.Count > 0)
                        {
                            gearCategoryIndex = (gearCategoryIndex + 1) % gearCategories.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearItemList:
                        if (gearItemList.Count > 0)
                        {
                            gearItemIndex = (gearItemIndex + 1) % gearItemList.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearItemActions:
                        if (gearActionList.Count > 0)
                        {
                            gearActionIndex = (gearActionIndex + 1) % gearActionList.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.SkillList:
                        if (skillList.Count > 0)
                        {
                            skillIndex = (skillIndex + 1) % skillList.Count;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.SkillDetail:
                    case MenuLevel.DetailedInfo:
                        // No navigation in detailed info view
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error in SelectNext: {ex}");
            }
        }

        /// <summary>
        /// Selects the previous item in the current menu level.
        /// </summary>
        public static void SelectPrevious()
        {
            if (!IsActive) return;

            try
            {
                switch (currentLevel)
                {
                    case MenuLevel.ObjectList:
                        if (availableObjects.Count > 0)
                        {
                            objectListIndex--;
                            if (objectListIndex < 0)
                                objectListIndex = availableObjects.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.CategoryMenu:
                        if (availableCategories.Count > 0)
                        {
                            categoryMenuIndex--;
                            if (categoryMenuIndex < 0)
                                categoryMenuIndex = availableCategories.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearCategory:
                        if (gearCategories.Count > 0)
                        {
                            gearCategoryIndex--;
                            if (gearCategoryIndex < 0)
                                gearCategoryIndex = gearCategories.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearItemList:
                        if (gearItemList.Count > 0)
                        {
                            gearItemIndex--;
                            if (gearItemIndex < 0)
                                gearItemIndex = gearItemList.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearItemActions:
                        if (gearActionList.Count > 0)
                        {
                            gearActionIndex--;
                            if (gearActionIndex < 0)
                                gearActionIndex = gearActionList.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.SkillList:
                        if (skillList.Count > 0)
                        {
                            skillIndex--;
                            if (skillIndex < 0)
                                skillIndex = skillList.Count - 1;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.SkillDetail:
                    case MenuLevel.DetailedInfo:
                        // No navigation in detailed info view
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error in SelectPrevious: {ex}");
            }
        }

        /// <summary>
        /// Drills down into the selected item (Enter key).
        /// </summary>
        public static void DrillDown()
        {
            if (!IsActive) return;

            try
            {
                switch (currentLevel)
                {
                    case MenuLevel.ObjectList:
                        // Move to category menu
                        if (objectListIndex >= 0 && objectListIndex < availableObjects.Count)
                        {
                            currentSelectedObject = availableObjects[objectListIndex];
                            availableCategories = InspectionInfoHelper.GetAvailableCategories(currentSelectedObject);

                            if (availableCategories.Count == 0)
                            {
                                ClipboardHelper.CopyToClipboard("No information available for this object.");
                                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                                return;
                            }

                            currentLevel = MenuLevel.CategoryMenu;
                            categoryMenuIndex = 0;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.CategoryMenu:
                        // Check if Gear or Skills category is selected
                        if (categoryMenuIndex >= 0 && categoryMenuIndex < availableCategories.Count)
                        {
                            currentSelectedCategory = availableCategories[categoryMenuIndex];

                            // Delegate to appropriate tab state based on category
                            if (currentSelectedObject is Pawn pawn)
                            {
                                // Special handling for Gear category
                                if (currentSelectedCategory == "Gear")
                                {
                                    currentLevel = MenuLevel.GearCategory;
                                    gearCategoryIndex = 0;
                                    SoundDefOf.Click.PlayOneShotOnCamera();
                                    AnnounceCurrentSelection();
                                    return;
                                }
                                // Special handling for Skills category
                                else if (currentSelectedCategory == "Skills")
                                {
                                    BuildSkillList();

                                    if (skillList.Count == 0)
                                    {
                                        ClipboardHelper.CopyToClipboard("No skills available.");
                                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                                        return;
                                    }

                                    currentLevel = MenuLevel.SkillList;
                                    skillIndex = 0;
                                    SoundDefOf.Click.PlayOneShotOnCamera();
                                    AnnounceCurrentSelection();
                                    return;
                                }
                                // Delegate to Health tab
                                else if (currentSelectedCategory == "Health")
                                {
                                    HealthTabState.Open(pawn);
                                    return;
                                }
                                // Delegate to Needs tab
                                else if (currentSelectedCategory == "Needs")
                                {
                                    NeedsTabState.Open(pawn);
                                    return;
                                }
                                // Delegate to Social tab
                                else if (currentSelectedCategory == "Social")
                                {
                                    SocialTabState.Open(pawn);
                                    return;
                                }
                                // Delegate to Training tab
                                else if (currentSelectedCategory == "Training")
                                {
                                    TrainingTabState.Open(pawn);
                                    return;
                                }
                                // Delegate to Character tab
                                else if (currentSelectedCategory == "Character")
                                {
                                    CharacterTabState.Open(pawn);
                                    return;
                                }
                            }

                            // Fallback to detailed info for other categories
                            currentDetailedInfo = InspectionInfoHelper.GetCategoryInfo(
                                currentSelectedObject,
                                currentSelectedCategory
                            );

                            currentLevel = MenuLevel.DetailedInfo;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearCategory:
                        // Move to gear item list
                        BuildGearItemList();

                        if (gearItemList.Count == 0)
                        {
                            string category = gearCategories[gearCategoryIndex];
                            ClipboardHelper.CopyToClipboard($"No {category.ToLower()} items.");
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            return;
                        }

                        currentLevel = MenuLevel.GearItemList;
                        gearItemIndex = 0;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.GearItemList:
                        // Move to gear item actions
                        if (gearItemIndex >= 0 && gearItemIndex < gearItemList.Count)
                        {
                            currentSelectedGearItem = gearItemList[gearItemIndex];
                            Pawn pawn = currentSelectedObject as Pawn;

                            gearActionList = InteractiveGearHelper.GetAvailableActions(currentSelectedGearItem, pawn);

                            if (gearActionList.Count == 0)
                            {
                                ClipboardHelper.CopyToClipboard("No actions available for this item.");
                                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                                return;
                            }

                            currentLevel = MenuLevel.GearItemActions;
                            gearActionIndex = 0;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.GearItemActions:
                        // Execute the selected action
                        if (gearActionIndex >= 0 && gearActionIndex < gearActionList.Count)
                        {
                            string action = gearActionList[gearActionIndex];
                            Pawn pawn = currentSelectedObject as Pawn;

                            bool success = false;

                            switch (action)
                            {
                                case "Drop":
                                    success = InteractiveGearHelper.ExecuteDropAction(currentSelectedGearItem, pawn);
                                    break;

                                case "Consume":
                                    success = InteractiveGearHelper.ExecuteConsumeAction(currentSelectedGearItem, pawn);
                                    break;

                                case "View Info":
                                    InteractiveGearHelper.ExecuteInfoAction(currentSelectedGearItem);
                                    success = true;
                                    break;
                            }

                            if (success && action != "View Info")
                            {
                                // Go back to item list after successful action
                                currentLevel = MenuLevel.GearItemList;
                                BuildGearItemList(); // Rebuild list since items may have changed

                                // Adjust index if needed
                                if (gearItemIndex >= gearItemList.Count)
                                    gearItemIndex = Math.Max(0, gearItemList.Count - 1);

                                AnnounceCurrentSelection();
                            }
                        }
                        break;

                    case MenuLevel.SkillList:
                        // Show skill detail
                        if (skillIndex >= 0 && skillIndex < skillList.Count)
                        {
                            currentSelectedSkill = skillList[skillIndex];

                            currentLevel = MenuLevel.SkillDetail;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            AnnounceCurrentSelection();
                        }
                        break;

                    case MenuLevel.SkillDetail:
                    case MenuLevel.DetailedInfo:
                        // Already at deepest level
                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error in DrillDown: {ex}");
            }
        }

        /// <summary>
        /// Goes back up one level (Escape key).
        /// </summary>
        public static void GoBack()
        {
            if (!IsActive) return;

            try
            {
                switch (currentLevel)
                {
                    case MenuLevel.ObjectList:
                        // Close the menu
                        Close();
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        ClipboardHelper.CopyToClipboard("Inspection menu closed.");
                        break;

                    case MenuLevel.CategoryMenu:
                        // Go back to object list
                        currentLevel = MenuLevel.ObjectList;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.GearCategory:
                        // Go back to category menu
                        currentLevel = MenuLevel.CategoryMenu;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.GearItemList:
                        // Go back to gear category
                        currentLevel = MenuLevel.GearCategory;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.GearItemActions:
                        // Go back to gear item list
                        currentLevel = MenuLevel.GearItemList;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.SkillList:
                        // Go back to category menu
                        currentLevel = MenuLevel.CategoryMenu;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.SkillDetail:
                        // Go back to skill list
                        currentLevel = MenuLevel.SkillList;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;

                    case MenuLevel.DetailedInfo:
                        // Go back to category menu
                        currentLevel = MenuLevel.CategoryMenu;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error in GoBack: {ex}");
            }
        }

        /// <summary>
        /// Announces the current selection to the screen reader via clipboard.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            try
            {
                string announcement = "";

                switch (currentLevel)
                {
                    case MenuLevel.ObjectList:
                        if (availableObjects.Count == 0)
                        {
                            announcement = "No items to inspect.";
                        }
                        else if (objectListIndex >= 0 && objectListIndex < availableObjects.Count)
                        {
                            var obj = availableObjects[objectListIndex];
                            string prefix = "";

                            // Add prefix for selected pawn
                            if (includeSelectedPawn && objectListIndex == 0)
                            {
                                prefix = "Selected: ";
                            }

                            announcement = $"{prefix}{InspectionInfoHelper.GetObjectSummary(obj)}\n" +
                                         $"Item {objectListIndex + 1} of {availableObjects.Count}\n" +
                                         $"Press Enter to inspect, Escape to close";
                        }
                        break;

                    case MenuLevel.CategoryMenu:
                        if (availableCategories.Count == 0)
                        {
                            announcement = "No categories available.";
                        }
                        else if (categoryMenuIndex >= 0 && categoryMenuIndex < availableCategories.Count)
                        {
                            string category = availableCategories[categoryMenuIndex];
                            announcement = $"{category}\n" +
                                         $"Category {categoryMenuIndex + 1} of {availableCategories.Count}\n" +
                                         $"Press Enter to view, Escape to go back";
                        }
                        break;

                    case MenuLevel.GearCategory:
                        if (gearCategoryIndex >= 0 && gearCategoryIndex < gearCategories.Count)
                        {
                            string category = gearCategories[gearCategoryIndex];
                            announcement = $"{category}\n" +
                                         $"Category {gearCategoryIndex + 1} of {gearCategories.Count}\n" +
                                         $"Press Enter to view items, Escape to go back";
                        }
                        break;

                    case MenuLevel.GearItemList:
                        if (gearItemList.Count == 0)
                        {
                            announcement = "No items in this category.";
                        }
                        else if (gearItemIndex >= 0 && gearItemIndex < gearItemList.Count)
                        {
                            var item = gearItemList[gearItemIndex];
                            announcement = $"{item.Label}\n" +
                                         $"Item {gearItemIndex + 1} of {gearItemList.Count}\n" +
                                         $"Press Enter for actions, Escape to go back";
                        }
                        break;

                    case MenuLevel.GearItemActions:
                        if (gearActionList.Count == 0)
                        {
                            announcement = "No actions available.";
                        }
                        else if (gearActionIndex >= 0 && gearActionIndex < gearActionList.Count)
                        {
                            string action = gearActionList[gearActionIndex];
                            announcement = $"{action}\n" +
                                         $"Action {gearActionIndex + 1} of {gearActionList.Count}\n" +
                                         $"Press Enter to execute, Escape to go back";
                        }
                        break;

                    case MenuLevel.SkillList:
                        if (skillList.Count == 0)
                        {
                            announcement = "No skills available.";
                        }
                        else if (skillIndex >= 0 && skillIndex < skillList.Count)
                        {
                            SkillRecord skill = skillList[skillIndex];
                            string passionText = skill.passion == Passion.None ? "" : $" ({skill.passion})";
                            string disabledText = skill.TotallyDisabled ? " [DISABLED]" : "";

                            announcement = $"{skill.def.skillLabel}: Level {skill.Level}{passionText}{disabledText}\n" +
                                         $"Skill {skillIndex + 1} of {skillList.Count}\n" +
                                         $"Press Enter for details, Escape to go back";
                        }
                        break;

                    case MenuLevel.SkillDetail:
                        if (currentSelectedSkill != null)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine($"{currentSelectedSkill.def.skillLabel}:");
                            sb.AppendLine($"Level: {currentSelectedSkill.Level}");
                            sb.AppendLine($"XP: {currentSelectedSkill.XpTotalEarned:F0}");

                            if (currentSelectedSkill.passion != Passion.None)
                            {
                                sb.AppendLine($"Passion: {currentSelectedSkill.passion}");
                            }

                            if (currentSelectedSkill.TotallyDisabled)
                            {
                                sb.AppendLine("Status: DISABLED");
                            }

                            sb.AppendLine();
                            sb.AppendLine(currentSelectedSkill.def.description);
                            sb.AppendLine();
                            sb.AppendLine("Press Escape to go back");

                            announcement = sb.ToString();
                        }
                        else
                        {
                            announcement = "No skill information available.";
                        }
                        break;

                    case MenuLevel.DetailedInfo:
                        if (!string.IsNullOrEmpty(currentDetailedInfo))
                        {
                            announcement = $"{currentSelectedCategory}:\n\n{currentDetailedInfo}\n\n" +
                                         $"Press Escape to go back";
                        }
                        else
                        {
                            announcement = "No information available.";
                        }
                        break;
                }

                ClipboardHelper.CopyToClipboard(announcement);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error in AnnounceCurrentSelection: {ex}");
            }
        }

        /// <summary>
        /// Handles keyboard input for the inspection menu.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(Event ev)
        {
            if (!IsActive) return false;

            if (ev.type != EventType.KeyDown) return false;

            try
            {
                // Check if any tab state is active and delegate input to it
                if (HealthTabState.IsActive)
                {
                    return HealthTabState.HandleInput(ev);
                }
                if (NeedsTabState.IsActive)
                {
                    return NeedsTabState.HandleInput(ev);
                }
                if (SocialTabState.IsActive)
                {
                    return SocialTabState.HandleInput(ev);
                }
                if (TrainingTabState.IsActive)
                {
                    return TrainingTabState.HandleInput(ev);
                }
                if (CharacterTabState.IsActive)
                {
                    return CharacterTabState.HandleInput(ev);
                }

                // Handle regular inspection menu input
                switch (ev.keyCode)
                {
                    case KeyCode.UpArrow:
                        SelectPrevious();
                        ev.Use();
                        return true;

                    case KeyCode.DownArrow:
                        SelectNext();
                        ev.Use();
                        return true;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        DrillDown();
                        ev.Use();
                        return true;

                    case KeyCode.Escape:
                        GoBack();
                        ev.Use();
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error handling input in inspection menu: {ex}");
            }

            return false;
        }
    }
}
