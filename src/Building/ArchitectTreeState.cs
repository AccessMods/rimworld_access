using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a treeview-style architect menu with expandable categories.
    /// Level 1: Categories (Orders, Structure, etc.) - expandable
    /// Level 2: Designators (Wall, Door, etc.) - activates tool
    /// </summary>
    public static class ArchitectTreeState
    {
        private static List<MenuItem> menuItems = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static HashSet<string> expandedCategories = new HashSet<string>();
        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();

        // Callback for when a designator is activated
        private static Action<Designator> onDesignatorActivated;

        private enum MenuItemType
        {
            Category,
            Designator
        }

        private class MenuItem
        {
            public MenuItemType type;
            public string label;
            public object data; // DesignationCategoryDef or Designator
            public int indentLevel;
            public bool isExpanded;
            public MenuItem parent;

            public MenuItem(MenuItemType type, string label, object data, int indent = 0)
            {
                this.type = type;
                this.label = label;
                this.data = data;
                this.indentLevel = indent;
                this.isExpanded = false;
                this.parent = null;
            }
        }

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the architect tree menu.
        /// </summary>
        /// <param name="onActivated">Callback when a designator is selected for activation.</param>
        public static void Open(Action<Designator> onActivated)
        {
            onDesignatorActivated = onActivated;
            menuItems = new List<MenuItem>();
            selectedIndex = 0;
            isActive = true;
            typeahead.ClearSearch();

            BuildMenuItems();

            if (menuItems.Count == 0)
            {
                TolkHelper.Speak("No architect categories available");
                Close();
                return;
            }

            TolkHelper.Speak("Architect menu");
            AnnounceCurrentSelection();

            Log.Message($"Opened architect tree menu with {menuItems.Count} items");
        }

        /// <summary>
        /// Closes the architect tree menu.
        /// </summary>
        public static void Close()
        {
            menuItems = null;
            selectedIndex = 0;
            isActive = false;
            onDesignatorActivated = null;
            typeahead.ClearSearch();
            expandedCategories.Clear(); // Reset expansion state on close
            MenuHelper.ResetLevel("Architect");
        }

        /// <summary>
        /// Builds the menu item list from categories and their designators.
        /// </summary>
        private static void BuildMenuItems()
        {
            menuItems.Clear();

            List<DesignationCategoryDef> categories = ArchitectHelper.GetAllCategories();

            foreach (DesignationCategoryDef category in categories)
            {
                MenuItem catItem = new MenuItem(MenuItemType.Category, category.LabelCap, category, 0);
                catItem.isExpanded = expandedCategories.Contains(category.defName);
                menuItems.Add(catItem);

                // If expanded, add designators as children
                if (catItem.isExpanded)
                {
                    List<Designator> designators = ArchitectHelper.GetDesignatorsForCategory(category);
                    foreach (Designator designator in designators)
                    {
                        string label = GetDesignatorLabel(designator);
                        MenuItem desItem = new MenuItem(MenuItemType.Designator, label, designator, 1);
                        desItem.parent = catItem;
                        menuItems.Add(desItem);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a label for a designator including cost and description.
        /// Format: "Name: cost. description" for build designators.
        /// Format: "Name. description" for order designators.
        /// If the designator has right-click options, adds "(right bracket for more options)" hint after the name.
        /// </summary>
        private static string GetDesignatorLabel(Designator designator)
        {
            string label = designator.LabelCap;

            // Check if designator has right-click options and add hint
            bool hasRightClickOptions = designator.RightClickFloatMenuOptions.Any();
            string rightClickHint = hasRightClickOptions ? " (right bracket for more options)" : "";

            // Add cost and description for build designators
            if (designator is Designator_Build buildDesignator)
            {
                BuildableDef buildable = buildDesignator.PlacingDef;
                if (buildable != null)
                {
                    string costInfo = ArchitectHelper.GetBriefCostInfo(buildable);
                    string description = ArchitectHelper.GetDescription(buildable);

                    // Format: "Name (right bracket hint): cost. description"
                    // Note: Descriptions from RimWorld typically include their own terminal punctuation
                    label += rightClickHint;
                    if (!string.IsNullOrEmpty(costInfo) && !string.IsNullOrEmpty(description))
                    {
                        label += $": {costInfo}. {description}";
                    }
                    else if (!string.IsNullOrEmpty(costInfo))
                    {
                        label += $": {costInfo}";
                    }
                    else if (!string.IsNullOrEmpty(description))
                    {
                        label += $". {description}";
                    }
                }
                else
                {
                    label += rightClickHint;
                }
            }
            else
            {
                // For non-build designators (orders), add hint and description if available
                label += rightClickHint;
                string description = ArchitectHelper.GetDesignatorDescriptionText(designator);
                if (!string.IsNullOrEmpty(description))
                {
                    label += $". {description}";
                }
            }

            return label;
        }

        public static void SelectNext()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectNext(selectedIndex, menuItems.Count);
            AnnounceCurrentSelection();
        }

        public static void SelectPrevious()
        {
            if (menuItems == null || menuItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, menuItems.Count);
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the current category or moves to first child if already expanded.
        /// For designators, plays reject sound (end node).
        /// </summary>
        public static void ExpandCurrent()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            typeahead.ClearSearch();
            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.Category)
            {
                DesignationCategoryDef category = item.data as DesignationCategoryDef;
                if (category == null) return;

                if (!item.isExpanded)
                {
                    // Expand the category
                    expandedCategories.Add(category.defName);
                    RebuildMenu();
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                }
                else
                {
                    // Already expanded - move to first child
                    if (!MoveToFirstChild())
                    {
                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                        TolkHelper.Speak("No items in this category");
                    }
                }
            }
            else
            {
                // Designator - end node
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// Collapses the current category or moves to parent.
        /// </summary>
        public static void CollapseCurrent()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            typeahead.ClearSearch();
            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.Category)
            {
                if (item.isExpanded)
                {
                    // Collapse the category
                    DesignationCategoryDef category = item.data as DesignationCategoryDef;
                    if (category != null)
                    {
                        expandedCategories.Remove(category.defName);
                        RebuildMenu();
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                    }
                }
                else
                {
                    // Already collapsed, at root level - reject
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
            }
            else
            {
                // Designator - move to parent category
                MoveToParent();
            }
        }

        /// <summary>
        /// Activates the current item (expand/collapse for categories, activate for designators).
        /// </summary>
        public static void ActivateCurrent()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.Category)
            {
                // Clear search when toggling expansion to avoid stale search state
                typeahead.ClearSearch();

                // Toggle expansion
                DesignationCategoryDef category = item.data as DesignationCategoryDef;
                if (category == null) return;

                if (item.isExpanded)
                {
                    expandedCategories.Remove(category.defName);
                }
                else
                {
                    expandedCategories.Add(category.defName);
                }
                RebuildMenu();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
            else if (item.type == MenuItemType.Designator)
            {
                // Activate the designator
                Designator designator = item.data as Designator;
                if (designator != null && onDesignatorActivated != null)
                {
                    isActive = false;
                    expandedCategories.Clear(); // Reset for next open
                    onDesignatorActivated(designator);
                }
            }
        }

        /// <summary>
        /// Expands all sibling categories at the same level as the current item.
        /// WCAG tree view pattern: * key expands all siblings.
        /// </summary>
        public static void ExpandAllSiblings()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem currentItem = menuItems[selectedIndex];
            MenuItem parent = currentItem.parent; // null means root level

            // Find all collapsed sibling categories
            int expandedCount = 0;
            foreach (var item in menuItems)
            {
                if (item.parent == parent && item.type == MenuItemType.Category && !item.isExpanded)
                {
                    var category = item.data as DesignationCategoryDef;
                    if (category != null)
                    {
                        expandedCategories.Add(category.defName);
                        expandedCount++;
                    }
                }
            }

            if (expandedCount > 0)
            {
                RebuildMenu();
                typeahead.ClearSearch();
                if (expandedCount == 1)
                    TolkHelper.Speak("Expanded 1 category");
                else
                    TolkHelper.Speak($"Expanded {expandedCount} categories");
            }
            else
            {
                bool hasAnySiblingCategories = menuItems.Any(m => m.parent == parent && m.type == MenuItemType.Category);
                if (hasAnySiblingCategories)
                    TolkHelper.Speak("All categories already expanded at this level");
                else
                    TolkHelper.Speak("No categories to expand at this level");
            }
        }

        /// <summary>
        /// Moves focus to the first child of the current item.
        /// </summary>
        private static bool MoveToFirstChild()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return false;

            MenuItem item = menuItems[selectedIndex];

            for (int i = selectedIndex + 1; i < menuItems.Count; i++)
            {
                if (menuItems[i].parent == item)
                {
                    selectedIndex = i;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Moves focus to the parent of the current item.
        /// </summary>
        private static void MoveToParent()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (item.parent == null)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }

            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuItems[i] == item.parent)
                {
                    selectedIndex = i;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    return;
                }
            }

            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }

        private static void RebuildMenu()
        {
            MenuItem currentItem = (selectedIndex >= 0 && selectedIndex < menuItems.Count) ? menuItems[selectedIndex] : null;
            string currentLabel = currentItem?.label;
            MenuItemType? currentType = currentItem?.type;

            BuildMenuItems();

            // Try to restore selection
            if (currentItem != null)
            {
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i].label == currentLabel && menuItems[i].type == currentType)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, menuItems.Count - 1));
        }

        /// <summary>
        /// Gets the sibling position (X of Y) for a menu item.
        /// Uses a counting loop instead of LINQ to avoid allocation.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(MenuItem item)
        {
            int position = 0, total = 0;
            foreach (var m in menuItems)
            {
                if (m.parent == item.parent)
                {
                    total++;
                    if (m == item) position = total;
                }
            }
            return (position, total);
        }

        private static void AnnounceCurrentSelection()
        {
            if (selectedIndex < 0 || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];
            var (position, total) = GetSiblingPosition(item);
            string positionPart = MenuHelper.FormatPosition(position - 1, total);

            string announcement = "";

            if (item.type == MenuItemType.Category)
            {
                // Categories: "Label, expanded/collapsed. position."
                // Category labels don't typically end with punctuation
                string expandState = item.isExpanded ? "expanded" : "collapsed";
                string positionSection = string.IsNullOrEmpty(positionPart) ? "." : $". {positionPart}.";
                announcement = $"{item.label}, {expandState}{positionSection}";
            }
            else
            {
                // Designator: "Label. position." or "Label position." if label ends with punctuation
                // Designator labels often include descriptions that end with periods
                string labelText = item.label;
                bool labelEndsWithPunctuation = !string.IsNullOrEmpty(labelText) &&
                    (labelText.EndsWith(".") || labelText.EndsWith("!") || labelText.EndsWith("?"));

                string positionSection;
                if (string.IsNullOrEmpty(positionPart))
                {
                    // No position info - just ensure we end with a period
                    positionSection = labelEndsWithPunctuation ? "" : ".";
                }
                else
                {
                    // Has position info - add separator only if label doesn't end with punctuation
                    positionSection = labelEndsWithPunctuation ? $" {positionPart}." : $". {positionPart}.";
                }

                announcement = $"{item.label}{positionSection}";
            }

            // Add level suffix at the end (only announced when level changes)
            announcement += MenuHelper.GetLevelSuffix("Architect", item.indentLevel);

            TolkHelper.Speak(announcement);
        }

        public static void JumpToFirst()
        {
            if (menuItems == null || menuItems.Count == 0) return;
            MenuHelper.HandleTreeHomeKey(menuItems, ref selectedIndex, m => m.indentLevel, false, ClearAndAnnounce);
        }

        public static void JumpToLast()
        {
            if (menuItems == null || menuItems.Count == 0) return;
            MenuHelper.HandleTreeEndKey(menuItems, ref selectedIndex, m => m.indentLevel,
                m => m.isExpanded, m => m.type == MenuItemType.Category, false, ClearAndAnnounce);
        }

        public static void JumpToAbsoluteFirst()
        {
            if (menuItems == null || menuItems.Count == 0) return;
            MenuHelper.HandleTreeHomeKey(menuItems, ref selectedIndex, m => m.indentLevel, true, ClearAndAnnounce);
        }

        public static void JumpToAbsoluteLast()
        {
            if (menuItems == null || menuItems.Count == 0) return;
            MenuHelper.HandleTreeEndKey(menuItems, ref selectedIndex, m => m.indentLevel,
                m => m.isExpanded, m => m.type == MenuItemType.Category, true, ClearAndAnnounce);
        }

        private static void ClearAndAnnounce()
        {
            typeahead.ClearSearch();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Gets the currently selected designator, or null if a category is selected.
        /// </summary>
        public static Designator GetSelectedDesignator()
        {
            if (menuItems == null || selectedIndex < 0 || selectedIndex >= menuItems.Count)
                return null;

            MenuItem item = menuItems[selectedIndex];
            if (item.type == MenuItemType.Designator)
            {
                return item.data as Designator;
            }
            return null;
        }

        // Typeahead search support
        public static bool HasActiveSearch => typeahead.HasActiveSearch;
        public static bool HasNoMatches => typeahead.HasNoMatches;

        public static void ClearTypeaheadSearch()
        {
            typeahead.ClearSearchAndAnnounce();
            AnnounceCurrentSelection();
        }

        public static bool ProcessBackspace()
        {
            if (!typeahead.HasActiveSearch) return false;

            var labels = GetVisibleItemLabels();
            if (typeahead.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0) selectedIndex = newIndex;
                AnnounceWithSearch();
            }
            return true;
        }

        public static bool ProcessTypeaheadCharacter(char c)
        {
            if (menuItems == null || menuItems.Count == 0)
                return false;

            var labels = GetVisibleItemLabels();
            if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
            {
                if (newIndex >= 0) { selectedIndex = newIndex; AnnounceWithSearch(); }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{typeahead.LastFailedSearch}'");
            }
            return true;
        }

        public static bool SelectNextMatch()
        {
            if (!typeahead.HasActiveSearch) return false;

            int next = typeahead.GetNextMatch(selectedIndex);
            if (next >= 0)
            {
                selectedIndex = next;
                AnnounceWithSearch();
            }
            return true;
        }

        public static bool SelectPreviousMatch()
        {
            if (!typeahead.HasActiveSearch) return false;

            int prev = typeahead.GetPreviousMatch(selectedIndex);
            if (prev >= 0)
            {
                selectedIndex = prev;
                AnnounceWithSearch();
            }
            return true;
        }

        private static List<string> GetVisibleItemLabels()
        {
            var labels = new List<string>();
            if (menuItems != null)
            {
                foreach (var item in menuItems)
                {
                    labels.Add(item.label);
                }
            }
            return labels;
        }

        private static void AnnounceWithSearch()
        {
            if (menuItems == null || selectedIndex < 0 || selectedIndex >= menuItems.Count) return;

            string label = menuItems[selectedIndex].label;

            if (typeahead.HasActiveSearch)
            {
                TolkHelper.Speak($"{label}, {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} matches for '{typeahead.SearchBuffer}'");
            }
            else
            {
                AnnounceCurrentSelection();
            }
        }
    }
}
