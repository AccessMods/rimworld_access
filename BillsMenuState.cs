using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a windowless bills menu for crafting stations.
    /// Provides keyboard navigation to add, configure, and delete bills.
    /// </summary>
    public static class BillsMenuState
    {
        private static IBillGiver billGiver = null;
        private static IntVec3 billGiverPos;
        private static List<MenuItem> menuItems = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;

        private enum MenuItemType
        {
            AddBill,
            ExistingBill,
            PasteBill
        }

        private class MenuItem
        {
            public MenuItemType type;
            public string label;
            public object data; // Bill for ExistingBill, RecipeDef for AddBill
            public bool isEnabled;

            public MenuItem(MenuItemType type, string label, object data, bool enabled = true)
            {
                this.type = type;
                this.label = label;
                this.data = data;
                this.isEnabled = enabled;
            }
        }

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the bills menu for the given bill giver.
        /// </summary>
        public static void Open(IBillGiver giver, IntVec3 position)
        {
            if (giver == null)
            {
                Log.Error("Cannot open bills menu: giver is null");
                return;
            }

            billGiver = giver;
            billGiverPos = position;
            menuItems = new List<MenuItem>();
            selectedIndex = 0;
            isActive = true;

            BuildMenuItems();
            AnnounceCurrentSelection();

            Log.Message($"Opened bills menu with {menuItems.Count} items");
        }

        /// <summary>
        /// Closes the bills menu.
        /// </summary>
        public static void Close()
        {
            billGiver = null;
            menuItems = null;
            selectedIndex = 0;
            isActive = false;
        }

        /// <summary>
        /// Builds the menu item list.
        /// </summary>
        private static void BuildMenuItems()
        {
            menuItems.Clear();

            // Add "Add new bill" option
            menuItems.Add(new MenuItem(MenuItemType.AddBill, "Add new bill...", null));

            // Add paste bill option if clipboard has a bill
            if (BillUtility.Clipboard != null)
            {
                Building_WorkTable workTable = billGiver as Building_WorkTable;
                bool canPaste = false;
                string pasteLabel = "Paste bill";

                if (workTable != null)
                {
                    if (!workTable.def.AllRecipes.Contains(BillUtility.Clipboard.recipe) ||
                        !BillUtility.Clipboard.recipe.AvailableNow ||
                        !BillUtility.Clipboard.recipe.AvailableOnNow(workTable))
                    {
                        pasteLabel = $"Paste bill (not available here): {BillUtility.Clipboard.LabelCap}";
                        canPaste = false;
                    }
                    else if (billGiver.BillStack.Count >= 15)
                    {
                        pasteLabel = $"Paste bill (limit reached): {BillUtility.Clipboard.LabelCap}";
                        canPaste = false;
                    }
                    else
                    {
                        pasteLabel = $"Paste bill: {BillUtility.Clipboard.LabelCap}";
                        canPaste = true;
                    }
                }

                menuItems.Add(new MenuItem(MenuItemType.PasteBill, pasteLabel, BillUtility.Clipboard, canPaste));
            }

            // Add existing bills
            if (billGiver.BillStack != null)
            {
                for (int i = 0; i < billGiver.BillStack.Count; i++)
                {
                    Bill bill = billGiver.BillStack[i];
                    string billLabel = $"{i + 1}. {bill.LabelCap}";

                    // Add cost information
                    string costInfo = GetBillCostInfo(bill);
                    if (!string.IsNullOrEmpty(costInfo))
                    {
                        billLabel += $" - {costInfo}";
                    }

                    // Add description
                    string description = GetBillDescription(bill);
                    if (!string.IsNullOrEmpty(description))
                    {
                        billLabel += $" - {description}";
                    }

                    if (bill.suspended)
                    {
                        billLabel += " (paused)";
                    }

                    menuItems.Add(new MenuItem(MenuItemType.ExistingBill, billLabel, bill));
                }
            }

            // If no bills, add a note
            if (billGiver.BillStack == null || billGiver.BillStack.Count == 0)
            {
                menuItems.Add(new MenuItem(MenuItemType.ExistingBill, "(No bills)", null, false));
            }
        }

        public static void SelectNext()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % menuItems.Count;
            AnnounceCurrentSelection();
        }

        public static void SelectPrevious()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + menuItems.Count) % menuItems.Count;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Executes the currently selected menu item.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (!item.isEnabled)
            {
                TolkHelper.Speak("Option not available", SpeechPriority.High);
                return;
            }

            switch (item.type)
            {
                case MenuItemType.AddBill:
                    OpenAddBillMenu();
                    break;

                case MenuItemType.PasteBill:
                    PasteBill();
                    break;

                case MenuItemType.ExistingBill:
                    OpenBillConfig(item.data as Bill);
                    break;
            }
        }

        /// <summary>
        /// Deletes the currently selected bill.
        /// </summary>
        public static void DeleteSelected()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.ExistingBill && item.data is Bill bill)
            {
                billGiver.BillStack.Delete(bill);
                TolkHelper.Speak($"Deleted: {bill.LabelCap}");

                // Rebuild menu
                BuildMenuItems();

                // Adjust selection
                if (selectedIndex >= menuItems.Count)
                {
                    selectedIndex = menuItems.Count - 1;
                }

                AnnounceCurrentSelection();
            }
            else
            {
                TolkHelper.Speak("Cannot delete this item", SpeechPriority.High);
            }
        }

        /// <summary>
        /// Copies the currently selected bill to clipboard.
        /// </summary>
        public static void CopySelected()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.ExistingBill && item.data is Bill bill)
            {
                BillUtility.Clipboard = bill;
                TolkHelper.Speak($"Copied to clipboard: {bill.LabelCap}");

                // Rebuild to show paste option
                BuildMenuItems();
                AnnounceCurrentSelection();
            }
            else
            {
                TolkHelper.Speak("Cannot copy this item", SpeechPriority.High);
            }
        }

        /// <summary>
        /// Opens the add bill submenu (list of available recipes).
        /// </summary>
        private static void OpenAddBillMenu()
        {
            Building_WorkTable workTable = billGiver as Building_WorkTable;
            if (workTable == null)
            {
                TolkHelper.Speak("Cannot add bills to this object", SpeechPriority.High);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Build list of available recipes
            foreach (RecipeDef recipe in workTable.def.AllRecipes)
            {
                if (recipe.AvailableNow && recipe.AvailableOnNow(workTable))
                {
                    // Add main recipe option
                    AddRecipeOption(recipe, workTable, options, null);

                    // Add precept-specific variants if applicable
                    if (recipe.ProducedThingDef != null)
                    {
                        foreach (Ideo ideo in Faction.OfPlayer.ideos.AllIdeos)
                        {
                            foreach (Precept_Building precept in ideo.cachedPossibleBuildings)
                            {
                                if (precept.ThingDef == recipe.ProducedThingDef)
                                {
                                    AddRecipeOption(recipe, workTable, options, precept);
                                }
                            }
                        }
                    }
                }
            }

            if (options.Count == 0)
            {
                TolkHelper.Speak("No recipes available");
                return;
            }

            // Open windowless float menu
            WindowlessFloatMenuState.Open(options, false);
        }

        private static void AddRecipeOption(RecipeDef recipe, Building_WorkTable workTable, List<FloatMenuOption> options, Precept_ThingStyle precept)
        {
            string label = (precept != null) ? "RecipeMake".Translate(precept.LabelCap).CapitalizeFirst() : recipe.LabelCap;

            // Add cost information
            string costInfo = GetRecipeCostInfo(recipe);
            if (!string.IsNullOrEmpty(costInfo))
            {
                label += $" - {costInfo}";
            }

            // Add description
            string description = GetRecipeDescription(recipe);
            if (!string.IsNullOrEmpty(description))
            {
                label += $" - {description}";
            }

            FloatMenuOption option = new FloatMenuOption(label, delegate
            {
                // Check requirements
                if (ModsConfig.BiotechActive && recipe.mechanitorOnlyRecipe &&
                    !workTable.Map.mapPawns.FreeColonists.Any(MechanitorUtility.IsMechanitor))
                {
                    TolkHelper.Speak($"Recipe requires mechanitor: {recipe.LabelCap}");
                    return;
                }

                if (!workTable.Map.mapPawns.FreeColonists.Any((Pawn col) => recipe.PawnSatisfiesSkillRequirements(col)))
                {
                    TolkHelper.Speak($"No pawns have required skills for: {recipe.LabelCap}");
                    return;
                }

                // Create the bill
                Bill bill = recipe.MakeNewBill(precept);
                billGiver.BillStack.AddBill(bill);

                TolkHelper.Speak($"Added bill: {bill.LabelCap}");

                // Rebuild menu
                BuildMenuItems();

                // Select the newly added bill
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i].type == MenuItemType.ExistingBill && menuItems[i].data == bill)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                AnnounceCurrentSelection();

                // Demonstrate knowledge
                if (recipe.conceptLearned != null)
                {
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(recipe.conceptLearned, KnowledgeAmount.Total);
                }
            });

            options.Add(option);
        }

        private static void PasteBill()
        {
            if (BillUtility.Clipboard == null)
            {
                TolkHelper.Speak("Clipboard is empty");
                return;
            }

            Bill bill = BillUtility.Clipboard.Clone();
            bill.InitializeAfterClone();
            billGiver.BillStack.AddBill(bill);

            TolkHelper.Speak($"Pasted bill: {bill.LabelCap}");

            // Rebuild menu and select the new bill
            BuildMenuItems();
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuItems[i].type == MenuItemType.ExistingBill && menuItems[i].data == bill)
                {
                    selectedIndex = i;
                    break;
                }
            }
            AnnounceCurrentSelection();
        }

        private static void OpenBillConfig(Bill bill)
        {
            if (bill == null)
            {
                TolkHelper.Speak("No bill selected");
                return;
            }

            if (bill is Bill_Production productionBill)
            {
                BillConfigState.Open(productionBill, billGiverPos);
            }
            else
            {
                TolkHelper.Speak($"Bill type {bill.GetType().Name} not yet supported");
            }
        }

        private static void AnnounceCurrentSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < menuItems.Count)
            {
                MenuItem item = menuItems[selectedIndex];
                string announcement = item.label;

                if (!item.isEnabled)
                {
                    announcement += " (unavailable)";
                }

                TolkHelper.Speak(announcement);
            }
        }

        /// <summary>
        /// Gets cost information for a recipe (ingredients required).
        /// </summary>
        private static string GetRecipeCostInfo(RecipeDef recipe)
        {
            if (recipe == null)
                return "";

            List<string> costs = new List<string>();

            // Get ingredient costs
            if (recipe.ingredients != null && recipe.ingredients.Count > 0)
            {
                foreach (IngredientCount ingredient in recipe.ingredients)
                {
                    string ingredientName = ingredient.filter.Summary;
                    float amount = ingredient.GetBaseCount();
                    costs.Add($"{amount} {ingredientName}");
                }
            }

            // Get fixed ingredient costs
            if (recipe.fixedIngredientFilter != null && recipe.fixedIngredientFilter.AllowedThingDefs.Any())
            {
                // Only show if not already covered by ingredients list
                if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                {
                    costs.Add(recipe.fixedIngredientFilter.Summary);
                }
            }

            if (costs.Count == 0)
                return "";

            return string.Join(", ", costs);
        }

        /// <summary>
        /// Gets description for a recipe (what it produces).
        /// </summary>
        private static string GetRecipeDescription(RecipeDef recipe)
        {
            if (recipe == null)
                return "";

            List<string> descriptions = new List<string>();

            // Add what it produces
            if (recipe.products != null && recipe.products.Count > 0)
            {
                foreach (ThingDefCountClass product in recipe.products)
                {
                    string productDesc = $"Makes {product.count} {product.thingDef.LabelCap}";
                    descriptions.Add(productDesc);
                }
            }
            else if (recipe.ProducedThingDef != null)
            {
                // Check recipe's ProducedThingDef if no products list
                descriptions.Add($"Makes {recipe.ProducedThingDef.LabelCap}");
            }

            // Add work amount if available
            if (recipe.workAmount > 0)
            {
                descriptions.Add($"Work: {recipe.workAmount}");
            }

            // Add skill requirement if available
            if (recipe.workSkill != null)
            {
                string skillInfo = recipe.workSkill.LabelCap.ToString();
                if (recipe.workSkillLearnFactor > 0)
                {
                    skillInfo += $" (Learn factor: {recipe.workSkillLearnFactor:F1})";
                }
                descriptions.Add(skillInfo);
            }

            if (descriptions.Count == 0)
                return "";

            return string.Join(", ", descriptions);
        }

        /// <summary>
        /// Gets cost information for a bill (ingredients required).
        /// </summary>
        private static string GetBillCostInfo(Bill bill)
        {
            if (bill?.recipe == null)
                return "";

            List<string> costs = new List<string>();

            // Get ingredient costs
            if (bill.recipe.ingredients != null && bill.recipe.ingredients.Count > 0)
            {
                foreach (IngredientCount ingredient in bill.recipe.ingredients)
                {
                    string ingredientName = ingredient.filter.Summary;
                    float amount = ingredient.GetBaseCount();
                    costs.Add($"{amount} {ingredientName}");
                }
            }

            // Get fixed ingredient costs
            if (bill.recipe.fixedIngredientFilter != null && bill.recipe.fixedIngredientFilter.AllowedThingDefs.Any())
            {
                // Only show if not already covered by ingredients list
                if (bill.recipe.ingredients == null || bill.recipe.ingredients.Count == 0)
                {
                    costs.Add(bill.recipe.fixedIngredientFilter.Summary);
                }
            }

            if (costs.Count == 0)
                return "";

            return string.Join(", ", costs);
        }

        /// <summary>
        /// Gets description for a bill (what it produces).
        /// </summary>
        private static string GetBillDescription(Bill bill)
        {
            if (bill?.recipe == null)
                return "";

            List<string> descriptions = new List<string>();

            // Add what it produces
            if (bill.recipe.products != null && bill.recipe.products.Count > 0)
            {
                foreach (ThingDefCountClass product in bill.recipe.products)
                {
                    string productDesc = $"Makes {product.count} {product.thingDef.LabelCap}";
                    descriptions.Add(productDesc);
                }
            }
            else if (bill.recipe.ProducedThingDef != null)
            {
                // Check recipe's ProducedThingDef if no products list
                descriptions.Add($"Makes {bill.recipe.ProducedThingDef.LabelCap}");
            }

            // Add work amount if available
            if (bill.recipe.workAmount > 0)
            {
                descriptions.Add($"Work: {bill.recipe.workAmount}");
            }

            // Add skill requirement if available
            if (bill.recipe.workSkill != null)
            {
                string skillInfo = bill.recipe.workSkill.LabelCap.ToString();
                if (bill.recipe.workSkillLearnFactor > 0)
                {
                    skillInfo += $" (Learn factor: {bill.recipe.workSkillLearnFactor:F1})";
                }
                descriptions.Add(skillInfo);
            }

            if (descriptions.Count == 0)
                return "";

            return string.Join(", ", descriptions);
        }
    }
}
