using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Reusable windowless menu for thing filtering.
    /// Used by both BillConfigState (ingredient filters) and StorageSettingsMenuState.
    /// </summary>
    public static class ThingFilterMenuState
    {
        private static List<MenuItem> menuItems = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static ThingFilter currentFilter = null;
        private static HashSet<string> expandedCategories = new HashSet<string>();
        private static string menuTitle = "";

        private enum MenuItemType
        {
            ClearAll,
            AllowAll,
            Category,
            ThingDef,
            SpecialFilter,
            HitPointsRange,
            QualityRange
        }

        private class MenuItem
        {
            public MenuItemType type;
            public string label;
            public object data; // Can be TreeNode_ThingCategory, ThingDef, SpecialThingFilterDef
            public int indentLevel;
            public bool isExpanded;
            public bool isAllowed; // Current state
            public MenuItem parent;

            public MenuItem(MenuItemType type, string label, object data, int indent = 0)
            {
                this.type = type;
                this.label = label;
                this.data = data;
                this.indentLevel = indent;
                this.isExpanded = false;
                this.isAllowed = false;
                this.parent = null;
            }
        }

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the thing filter menu.
        /// </summary>
        public static void Open(ThingFilter filter, string title = "Thing Filter")
        {
            if (filter == null)
            {
                Log.Error("Cannot open thing filter menu: filter is null");
                return;
            }

            currentFilter = filter;
            menuTitle = title;
            menuItems = new List<MenuItem>();
            selectedIndex = 0;
            isActive = true;

            BuildMenuItems();
            AnnounceCurrentSelection();

            Log.Message($"Opened thing filter menu: {title}");
        }

        /// <summary>
        /// Closes the thing filter menu.
        /// </summary>
        public static void Close()
        {
            menuItems = null;
            selectedIndex = 0;
            isActive = false;
            currentFilter = null;
            expandedCategories.Clear();
        }

        private static void BuildMenuItems()
        {
            menuItems.Clear();

            // Quick actions
            menuItems.Add(new MenuItem(MenuItemType.ClearAll, "Clear All", null));
            menuItems.Add(new MenuItem(MenuItemType.AllowAll, "Allow All", null));

            // Hit points range (if configurable)
            if (currentFilter.allowedHitPointsConfigurable)
            {
                FloatRange hpRange = currentFilter.AllowedHitPointsPercents;
                string hpLabel = $"Hit Points: {hpRange.min:P0} - {hpRange.max:P0}";
                menuItems.Add(new MenuItem(MenuItemType.HitPointsRange, hpLabel, hpRange));
            }

            // Quality range (if configurable)
            if (currentFilter.allowedQualitiesConfigurable)
            {
                QualityRange qualityRange = currentFilter.AllowedQualityLevels;
                string qualityLabel = $"Quality: {qualityRange.min} - {qualityRange.max}";
                menuItems.Add(new MenuItem(MenuItemType.QualityRange, qualityLabel, qualityRange));
            }

            // Thing filter tree
            TreeNode_ThingCategory rootNode = currentFilter.DisplayRootCategory;
            BuildCategoryItems(rootNode, 0, null, isRoot: true);
        }

        private static void BuildCategoryItems(TreeNode_ThingCategory node, int indent, MenuItem parentItem, bool isRoot = false)
        {
            if (node == null)
                return;

            // Add special filters for this category
            foreach (SpecialThingFilterDef specialFilter in node.catDef.childSpecialFilters)
            {
                if (specialFilter.configurable)
                {
                    MenuItem item = new MenuItem(MenuItemType.SpecialFilter, "*" + specialFilter.LabelCap, specialFilter, indent);
                    item.isAllowed = currentFilter.Allows(specialFilter);
                    item.parent = parentItem;
                    menuItems.Add(item);
                }
            }

            // Add child categories
            foreach (TreeNode_ThingCategory childNode in node.ChildCategoryNodes)
            {
                MenuItem catItem = new MenuItem(MenuItemType.Category, childNode.LabelCap, childNode, indent);
                catItem.isAllowed = IsCategoryAllowed(childNode);
                catItem.parent = parentItem;

                // Check if this category should be expanded
                string categoryKey = childNode.catDef.defName;
                catItem.isExpanded = expandedCategories.Contains(categoryKey);

                menuItems.Add(catItem);

                // If expanded, add children
                if (catItem.isExpanded)
                {
                    BuildCategoryItems(childNode, indent + 1, catItem, isRoot: false);
                }
            }

            // Add thing defs in this category
            foreach (ThingDef thingDef in node.catDef.childThingDefs)
            {
                if (!Find.HiddenItemsManager.Hidden(thingDef))
                {
                    MenuItem item = new MenuItem(MenuItemType.ThingDef, thingDef.LabelCap, thingDef, indent);
                    item.isAllowed = currentFilter.Allows(thingDef);
                    item.parent = parentItem;
                    menuItems.Add(item);
                }
            }
        }

        private static bool IsCategoryAllowed(TreeNode_ThingCategory node)
        {
            // Check if any descendant thing def is allowed
            foreach (ThingDef thingDef in node.catDef.DescendantThingDefs)
            {
                if (currentFilter.Allows(thingDef))
                {
                    return true;
                }
            }
            return false;
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

        public static void ExpandOrToggleOn()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            switch (item.type)
            {
                case MenuItemType.Category:
                    // If collapsed, expand; if expanded, toggle on
                    if (!item.isExpanded)
                    {
                        TreeNode_ThingCategory node = item.data as TreeNode_ThingCategory;
                        if (node != null)
                        {
                            expandedCategories.Add(node.catDef.defName);
                            RebuildMenu();
                            TolkHelper.Speak($"Expanded: {item.label}");
                        }
                    }
                    else
                    {
                        ToggleCategory(item, true);
                    }
                    break;

                case MenuItemType.ThingDef:
                case MenuItemType.SpecialFilter:
                    ToggleItem(item, true);
                    break;

                case MenuItemType.HitPointsRange:
                case MenuItemType.QualityRange:
                    TolkHelper.Speak("Press Enter to edit range");
                    break;

                case MenuItemType.ClearAll:
                    ClearAllItems();
                    break;

                case MenuItemType.AllowAll:
                    AllowAllItems();
                    break;
            }
        }

        public static void CollapseOrToggleOff()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            switch (item.type)
            {
                case MenuItemType.Category:
                    // If expanded, collapse; if collapsed, toggle off
                    if (item.isExpanded)
                    {
                        TreeNode_ThingCategory node = item.data as TreeNode_ThingCategory;
                        if (node != null)
                        {
                            expandedCategories.Remove(node.catDef.defName);
                            RebuildMenu();
                            TolkHelper.Speak($"Collapsed: {item.label}");
                        }
                    }
                    else
                    {
                        ToggleCategory(item, false);
                    }
                    break;

                case MenuItemType.ThingDef:
                case MenuItemType.SpecialFilter:
                    ToggleItem(item, false);
                    break;

                case MenuItemType.HitPointsRange:
                case MenuItemType.QualityRange:
                    TolkHelper.Speak("Press Enter to edit range");
                    break;
            }
        }

        public static void ToggleCurrent()
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            switch (item.type)
            {
                case MenuItemType.Category:
                    ToggleCategory(item, !item.isAllowed);
                    break;

                case MenuItemType.ThingDef:
                case MenuItemType.SpecialFilter:
                    ToggleItem(item, !item.isAllowed);
                    break;

                case MenuItemType.HitPointsRange:
                    // Open range edit submenu
                    RangeEditMenuState.OpenHitPointsRange(currentFilter.AllowedHitPointsPercents);
                    break;

                case MenuItemType.QualityRange:
                    // Open range edit submenu
                    RangeEditMenuState.OpenQualityRange(currentFilter.AllowedQualityLevels);
                    break;

                case MenuItemType.ClearAll:
                    ClearAllItems();
                    break;

                case MenuItemType.AllowAll:
                    AllowAllItems();
                    break;
            }
        }

        /// <summary>
        /// Applies range changes from the range edit submenu.
        /// </summary>
        public static void ApplyRangeChanges(FloatRange hitPoints, QualityRange quality)
        {
            if (menuItems == null || selectedIndex >= menuItems.Count)
                return;

            MenuItem item = menuItems[selectedIndex];

            if (item.type == MenuItemType.HitPointsRange)
            {
                currentFilter.AllowedHitPointsPercents = hitPoints;
                item.label = $"Hit Points: {hitPoints.min:P0} - {hitPoints.max:P0}";
                item.data = hitPoints;
                TolkHelper.Speak(item.label);
            }
            else if (item.type == MenuItemType.QualityRange)
            {
                currentFilter.AllowedQualityLevels = quality;
                item.label = $"Quality: {quality.min} - {quality.max}";
                item.data = quality;
                TolkHelper.Speak(item.label);
            }
        }

        private static void ToggleCategory(MenuItem item, bool allow)
        {
            TreeNode_ThingCategory node = item.data as TreeNode_ThingCategory;
            if (node == null) return;

            if (allow)
            {
                currentFilter.SetAllow(node.catDef, true);
            }
            else
            {
                currentFilter.SetAllow(node.catDef, false);
            }

            item.isAllowed = allow;
            string state = allow ? "Allowed" : "Disallowed";
            TolkHelper.Speak($"{state}: {item.label}");

            // Update child items if expanded
            if (item.isExpanded)
            {
                RebuildMenu();
            }
        }

        private static void ToggleItem(MenuItem item, bool allow)
        {
            if (item.type == MenuItemType.ThingDef)
            {
                ThingDef thingDef = item.data as ThingDef;
                if (thingDef != null)
                {
                    currentFilter.SetAllow(thingDef, allow);
                    item.isAllowed = allow;
                }
            }
            else if (item.type == MenuItemType.SpecialFilter)
            {
                SpecialThingFilterDef specialFilter = item.data as SpecialThingFilterDef;
                if (specialFilter != null)
                {
                    currentFilter.SetAllow(specialFilter, allow);
                    item.isAllowed = allow;
                }
            }

            string state = allow ? "Allowed" : "Disallowed";
            TolkHelper.Speak($"{state}: {item.label}");
        }

        private static void ClearAllItems()
        {
            currentFilter.SetDisallowAll();
            RebuildMenu();
            TolkHelper.Speak("Cleared all items");
        }

        private static void AllowAllItems()
        {
            currentFilter.SetAllowAll(null);
            RebuildMenu();
            TolkHelper.Speak("Allowed all items");
        }

        private static void RebuildMenu()
        {
            // Save current selection
            MenuItem currentItem = (selectedIndex >= 0 && selectedIndex < menuItems.Count) ? menuItems[selectedIndex] : null;

            // Rebuild
            BuildMenuItems();

            // Try to restore selection
            if (currentItem != null)
            {
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i].label == currentItem.label && menuItems[i].type == currentItem.type)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, menuItems.Count - 1);
        }

        private static void AnnounceCurrentSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < menuItems.Count)
            {
                MenuItem item = menuItems[selectedIndex];
                string announcement = item.label;

                // Add state information
                if (item.type == MenuItemType.Category || item.type == MenuItemType.ThingDef || item.type == MenuItemType.SpecialFilter)
                {
                    string state = item.isAllowed ? "Allowed" : "Disallowed";
                    announcement += $" ({state})";
                }

                if (item.type == MenuItemType.Category)
                {
                    string expandState = item.isExpanded ? "Expanded" : "Collapsed";
                    announcement += $" [{expandState}]";
                }

                TolkHelper.Speak(announcement);
            }
        }
    }
}
