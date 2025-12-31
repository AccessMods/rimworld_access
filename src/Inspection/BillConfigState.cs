using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a windowless bill configuration menu.
    /// Provides keyboard navigation through all bill settings.
    /// </summary>
    public static class BillConfigState
    {
        private static Bill_Production bill = null;
        private static IntVec3 billGiverPos;
        private static List<MenuItem> menuItems = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static bool isEditing = false;

        private enum MenuItemType
        {
            RecipeInfo,
            RepeatMode,
            RepeatCount,
            TargetCount,
            PauseWhenSatisfied,
            UnpauseAt,
            StoreMode,
            AllowedSkillRange,
            PawnRestriction,
            IngredientSearchRadius,
            IngredientFilter,
            SuspendToggle,
            DeleteBill
        }

        private class MenuItem
        {
            public MenuItemType type;
            public string label;
            public object data;
            public bool isEditable; // Can be edited with left/right or Enter

            public MenuItem(MenuItemType type, string label, object data = null, bool editable = false)
            {
                this.type = type;
                this.label = label;
                this.data = data;
                this.isEditable = editable;
            }
        }

        public static bool IsActive => isActive;
        public static bool IsEditing => isEditing;

        /// <summary>
        /// Opens the bill configuration menu.
        /// </summary>
        public static void Open(Bill_Production productionBill, IntVec3 position)
        {
            if (productionBill == null)
            {
                Log.Error("Cannot open bill config: bill is null");
                return;
            }

            bill = productionBill;
            billGiverPos = position;
            menuItems = new List<MenuItem>();
            selectedIndex = 0;
            isActive = true;
            isEditing = false;

            BuildMenuItems();
            AnnounceCurrentSelection();

            Log.Message($"Opened bill config for {bill.LabelCap}");
        }

        /// <summary>
        /// Closes the bill configuration menu.
        /// </summary>
        public static void Close()
        {
            bill = null;
            menuItems = null;
            selectedIndex = 0;
            isActive = false;
            isEditing = false;
        }

        private static void BuildMenuItems()
        {
            menuItems.Clear();

            // Recipe info (read-only)
            menuItems.Add(new MenuItem(MenuItemType.RecipeInfo, GetRecipeInfoLabel(), null, false));

            // Suspend/Resume toggle
            string suspendLabel = bill.suspended ? "Resume bill" : "Pause bill";
            menuItems.Add(new MenuItem(MenuItemType.SuspendToggle, suspendLabel, null, true));

            // Repeat mode
            menuItems.Add(new MenuItem(MenuItemType.RepeatMode, GetRepeatModeLabel(), null, true));

            // Repeat count (only if mode is RepeatCount)
            if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                menuItems.Add(new MenuItem(MenuItemType.RepeatCount, GetRepeatCountLabel(), null, true));
            }

            // Target count and unpause threshold (only if mode is TargetCount)
            if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                menuItems.Add(new MenuItem(MenuItemType.TargetCount, GetTargetCountLabel(), null, true));

                // Pause when satisfied checkbox
                menuItems.Add(new MenuItem(MenuItemType.PauseWhenSatisfied, GetPauseWhenSatisfiedLabel(), null, true));

                // Only show unpause threshold if pauseWhenSatisfied is enabled
                if (bill.pauseWhenSatisfied)
                {
                    menuItems.Add(new MenuItem(MenuItemType.UnpauseAt, GetUnpauseAtLabel(), null, true));
                }
            }

            // Store mode
            menuItems.Add(new MenuItem(MenuItemType.StoreMode, GetStoreModeLabel(), null, true));

            // Pawn restriction
            menuItems.Add(new MenuItem(MenuItemType.PawnRestriction, GetPawnRestrictionLabel(), null, true));

            // Allowed skill range
            menuItems.Add(new MenuItem(MenuItemType.AllowedSkillRange, GetSkillRangeLabel(), null, true));

            // Ingredient search radius
            menuItems.Add(new MenuItem(MenuItemType.IngredientSearchRadius, GetIngredientRadiusLabel(), null, true));

            // Ingredient filter
            menuItems.Add(new MenuItem(MenuItemType.IngredientFilter, "Configure ingredient filter...", null, true));

            // Delete bill
            menuItems.Add(new MenuItem(MenuItemType.DeleteBill, "Delete this bill", null, true));
        }

        #region Label Generators

        private static string GetRecipeInfoLabel()
        {
            string label = $"Recipe: {bill.recipe.LabelCap}";

            if (bill.recipe.workSkill != null)
            {
                label += $" (Skill: {bill.recipe.workSkill.LabelCap}";
                if (bill.recipe.workSkillLearnFactor > 0f)
                {
                    label += $", Learn: {bill.recipe.workSkillLearnFactor:F1}";
                }
                label += ")";
            }

            return label;
        }

        private static string GetRepeatModeLabel()
        {
            return $"Repeat mode: {bill.repeatMode.LabelCap}";
        }

        private static string GetRepeatCountLabel()
        {
            return $"Repeat count: {bill.repeatCount}";
        }

        private static string GetTargetCountLabel()
        {
            return $"Target count: {bill.targetCount}";
        }

        private static string GetPauseWhenSatisfiedLabel()
        {
            return $"Pause when satisfied: {(bill.pauseWhenSatisfied ? "Yes" : "No")}";
        }

        private static string GetUnpauseAtLabel()
        {
            return $"Unpause at: {bill.unpauseWhenYouHave}";
        }

        private static string GetStoreModeLabel()
        {
            string label = "Store in: ";

            if (bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
            {
                label += "Best stockpile";
            }
            else if (bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
            {
                label += "Drop on floor";
            }
            else if (bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
            {
                ISlotGroup slotGroup = bill.GetSlotGroup();
                if (slotGroup is Zone_Stockpile stockpile)
                {
                    label += stockpile.label;
                }
                else
                {
                    label += "(No stockpile)";
                }
            }

            return label;
        }

        private static string GetPawnRestrictionLabel()
        {
            if (bill.PawnRestriction == null)
            {
                return "Worker: Anyone";
            }
            else
            {
                return $"Worker: {bill.PawnRestriction.LabelShortCap}";
            }
        }

        private static string GetSkillRangeLabel()
        {
            IntRange range = bill.allowedSkillRange;
            return $"Allowed skill range: {range.min} - {range.max}";
        }

        private static string GetIngredientRadiusLabel()
        {
            if (bill.ingredientSearchRadius >= 999f)
            {
                return "Ingredient radius: Unlimited";
            }
            else
            {
                return $"Ingredient radius: {bill.ingredientSearchRadius:F0} tiles";
            }
        }

        #endregion

        public static void SelectNext()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            if (isEditing)
            {
                TolkHelper.Speak("Finish editing first (press Enter or Escape)");
                return;
            }

            selectedIndex = (selectedIndex + 1) % menuItems.Count;
            AnnounceCurrentSelection();
        }

        public static void SelectPrevious()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            if (isEditing)
            {
                TolkHelper.Speak("Finish editing first (press Enter or Escape)");
                return;
            }

            selectedIndex = (selectedIndex - 1 + menuItems.Count) % menuItems.Count;
            AnnounceCurrentSelection();
        }

        public static void AdjustValue(int direction)
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (!item.isEditable)
            {
                TolkHelper.Speak("This item cannot be adjusted", SpeechPriority.High);
                return;
            }

            switch (item.type)
            {
                case MenuItemType.RepeatMode:
                    CycleRepeatMode(direction);
                    break;

                case MenuItemType.RepeatCount:
                    AdjustRepeatCount(direction);
                    break;

                case MenuItemType.TargetCount:
                    AdjustTargetCount(direction);
                    break;

                case MenuItemType.UnpauseAt:
                    AdjustUnpauseAt(direction);
                    break;

                case MenuItemType.AllowedSkillRange:
                    AdjustSkillRange(direction);
                    break;

                case MenuItemType.IngredientSearchRadius:
                    AdjustIngredientRadius(direction);
                    break;

                default:
                    TolkHelper.Speak("Use Enter to open submenu");
                    break;
            }
        }

        public static void ExecuteSelected()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            switch (item.type)
            {
                case MenuItemType.SuspendToggle:
                    bill.suspended = !bill.suspended;
                    BuildMenuItems();
                    TolkHelper.Speak(bill.suspended ? "Bill paused" : "Bill resumed");
                    AnnounceCurrentSelection();
                    break;

                case MenuItemType.PauseWhenSatisfied:
                    bill.pauseWhenSatisfied = !bill.pauseWhenSatisfied;
                    // Ensure unpause threshold is valid
                    if (bill.pauseWhenSatisfied && bill.unpauseWhenYouHave >= bill.targetCount)
                    {
                        bill.unpauseWhenYouHave = bill.targetCount - 1;
                    }
                    BuildMenuItems();
                    TolkHelper.Speak(bill.pauseWhenSatisfied ? "Pause when satisfied enabled" : "Pause when satisfied disabled");
                    AnnounceCurrentSelection();
                    break;

                case MenuItemType.StoreMode:
                    OpenStoreModeMenu();
                    break;

                case MenuItemType.PawnRestriction:
                    OpenPawnRestrictionMenu();
                    break;

                case MenuItemType.IngredientFilter:
                    OpenIngredientFilterMenu();
                    break;

                case MenuItemType.DeleteBill:
                    DeleteBill();
                    break;

                default:
                    TolkHelper.Speak("Use left/right arrows to adjust");
                    break;
            }
        }

        #region Value Adjustment Methods

        private static void CycleRepeatMode(int direction)
        {
            List<BillRepeatModeDef> modes = DefDatabase<BillRepeatModeDef>.AllDefsListForReading;
            int currentIndex = modes.IndexOf(bill.repeatMode);

            if (direction > 0)
            {
                currentIndex = (currentIndex + 1) % modes.Count;
            }
            else
            {
                currentIndex = (currentIndex - 1 + modes.Count) % modes.Count;
            }

            bill.repeatMode = modes[currentIndex];
            BuildMenuItems(); // Rebuild to show/hide related options
            AnnounceCurrentSelection();
        }

        private static void AdjustRepeatCount(int direction)
        {
            int step = direction > 0 ? 1 : -1;
            bill.repeatCount = Mathf.Max(1, bill.repeatCount + step);

            menuItems[selectedIndex].label = GetRepeatCountLabel();
            TolkHelper.Speak(menuItems[selectedIndex].label);
        }

        private static void AdjustTargetCount(int direction)
        {
            int step = direction > 0 ? 1 : -1;
            bill.targetCount = Mathf.Max(1, bill.targetCount + step);

            // Ensure unpause threshold doesn't exceed target (must be at least 1 less if pauseWhenSatisfied)
            if (bill.pauseWhenSatisfied && bill.unpauseWhenYouHave >= bill.targetCount)
            {
                bill.unpauseWhenYouHave = bill.targetCount - 1;
            }

            menuItems[selectedIndex].label = GetTargetCountLabel();
            TolkHelper.Speak(menuItems[selectedIndex].label);
        }

        private static void AdjustUnpauseAt(int direction)
        {
            int step = direction > 0 ? 1 : -1;
            bill.unpauseWhenYouHave = Mathf.Clamp(bill.unpauseWhenYouHave + step, 0, bill.targetCount - 1);

            menuItems[selectedIndex].label = GetUnpauseAtLabel();
            TolkHelper.Speak(menuItems[selectedIndex].label);
        }

        private static void AdjustSkillRange(int direction)
        {
            // Cycle through presets: 0-3, 0-20, 6-20, 10-20
            IntRange current = bill.allowedSkillRange;

            if (direction > 0)
            {
                if (current.min == 0 && current.max == 3)
                {
                    bill.allowedSkillRange = new IntRange(0, 20);
                }
                else if (current.min == 0 && current.max == 20)
                {
                    bill.allowedSkillRange = new IntRange(6, 20);
                }
                else if (current.min == 6 && current.max == 20)
                {
                    bill.allowedSkillRange = new IntRange(10, 20);
                }
                else
                {
                    bill.allowedSkillRange = new IntRange(0, 3);
                }
            }
            else
            {
                if (current.min == 0 && current.max == 3)
                {
                    bill.allowedSkillRange = new IntRange(10, 20);
                }
                else if (current.min == 10 && current.max == 20)
                {
                    bill.allowedSkillRange = new IntRange(6, 20);
                }
                else if (current.min == 6 && current.max == 20)
                {
                    bill.allowedSkillRange = new IntRange(0, 20);
                }
                else
                {
                    bill.allowedSkillRange = new IntRange(0, 3);
                }
            }

            menuItems[selectedIndex].label = GetSkillRangeLabel();
            TolkHelper.Speak(menuItems[selectedIndex].label);
        }

        private static void AdjustIngredientRadius(int direction)
        {
            float step = direction > 0 ? 1f : -1f;

            if (bill.ingredientSearchRadius >= 999f)
            {
                if (direction < 0)
                {
                    bill.ingredientSearchRadius = 100f;
                }
            }
            else
            {
                bill.ingredientSearchRadius = Mathf.Clamp(bill.ingredientSearchRadius + step, 1f, 999f);

                if (bill.ingredientSearchRadius >= 999f)
                {
                    bill.ingredientSearchRadius = 999999f; // Unlimited
                }
            }

            menuItems[selectedIndex].label = GetIngredientRadiusLabel();
            TolkHelper.Speak(menuItems[selectedIndex].label);
        }

        #endregion

        #region Submenu Methods

        private static void OpenStoreModeMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Drop on floor
            options.Add(new FloatMenuOption("Drop on floor", delegate
            {
                bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
                BuildMenuItems();
                TolkHelper.Speak("Store mode: Drop on floor");
                AnnounceCurrentSelection();
            }));

            // Best stockpile
            options.Add(new FloatMenuOption("Best stockpile", delegate
            {
                bill.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                BuildMenuItems();
                TolkHelper.Speak("Store mode: Best stockpile");
                AnnounceCurrentSelection();
            }));

            // Specific stockpiles
            List<SlotGroup> allGroupsListForReading = bill.billStack.billGiver.Map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < allGroupsListForReading.Count; i++)
            {
                SlotGroup group = allGroupsListForReading[i];
                Zone_Stockpile stockpile = group.parent as Zone_Stockpile;

                if (stockpile != null)
                {
                    ISlotGroup localGroup = group; // Capture for lambda
                    options.Add(new FloatMenuOption($"Stockpile: {stockpile.label}", delegate
                    {
                        bill.SetStoreMode(BillStoreModeDefOf.SpecificStockpile, localGroup);
                        BuildMenuItems();
                        TolkHelper.Speak($"Store mode: {stockpile.label}");
                        AnnounceCurrentSelection();
                    }));
                }
            }

            WindowlessFloatMenuState.Open(options, false);
        }

        private static void OpenPawnRestrictionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Anyone
            options.Add(new FloatMenuOption("Anyone", delegate
            {
                bill.SetPawnRestriction(null);
                menuItems[selectedIndex].label = GetPawnRestrictionLabel();
                TolkHelper.Speak("Worker: Anyone");
            }));

            // Get all colonists and sort by skill
            Map map = bill.billStack.billGiver.Map;
            List<Pawn> colonists = map.mapPawns.FreeColonists.ToList();

            if (bill.recipe.workSkill != null)
            {
                colonists = colonists.OrderByDescending(p => p.skills.GetSkill(bill.recipe.workSkill).Level).ToList();
            }

            foreach (Pawn pawn in colonists)
            {
                string label = pawn.LabelShortCap;

                if (bill.recipe.workSkill != null)
                {
                    int skillLevel = pawn.skills.GetSkill(bill.recipe.workSkill).Level;
                    label += $" (Skill: {skillLevel})";
                }

                Pawn localPawn = pawn; // Capture for lambda
                options.Add(new FloatMenuOption(label, delegate
                {
                    bill.SetPawnRestriction(localPawn);
                    menuItems[selectedIndex].label = GetPawnRestrictionLabel();
                    TolkHelper.Speak($"Worker: {localPawn.LabelShortCap}");
                }));
            }

            WindowlessFloatMenuState.Open(options, false);
        }

        private static void OpenIngredientFilterMenu()
        {
            ThingFilterMenuState.Open(bill.ingredientFilter, "Ingredient Filter");
        }

        private static void DeleteBill()
        {
            string billLabel = bill.LabelCap;
            bill.billStack.Delete(bill);
            TolkHelper.Speak($"Deleted bill: {billLabel}");
            Close();

            // Go back to bills menu
            if (bill.billStack.billGiver is IBillGiver billGiver)
            {
                BillsMenuState.Open(billGiver, billGiverPos);
            }
        }

        #endregion

        private static void AnnounceCurrentSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < menuItems.Count)
            {
                MenuItem item = menuItems[selectedIndex];
                TolkHelper.Speak(item.label);
            }
        }
    }
}
