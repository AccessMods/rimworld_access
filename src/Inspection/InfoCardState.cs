using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages keyboard navigation state for Dialog_InfoCard.
    /// Provides tree-based navigation through Stats, Character, Health, Records, and Permits tabs.
    /// </summary>
    public static class InfoCardState
    {
        public static bool IsActive { get; private set; } = false;

        private static Dialog_InfoCard currentDialog = null;
        private static InspectionTreeItem rootItem = null;
        private static List<InspectionTreeItem> visibleItems = null;
        private static int selectedIndex = 0;

        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();
        public static TypeaheadSearchHelper Typeahead => typeahead;

        /// <summary>
        /// Opens the Info Card accessibility state for a dialog.
        /// </summary>
        /// <param name="dialog">The dialog to attach to</param>
        /// <param name="announceOpening">Whether to announce immediately (false to delay for stats to load)</param>
        public static void Open(Dialog_InfoCard dialog, bool announceOpening = true)
        {
            try
            {
                if (dialog == null)
                    return;

                currentDialog = dialog;
                IsActive = true;

                // Disable Enter-to-close since we handle Enter for navigation
                dialog.closeOnAccept = false;

                // Build the tree (stats may not be populated yet on first frame)
                rootItem = InfoCardTreeBuilder.BuildTree(dialog);
                RebuildVisibleList();
                selectedIndex = 0;

                MenuHelper.ResetLevel("InfoCard");
                typeahead.ClearSearch();

                if (announceOpening)
                {
                    SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    AnnounceOpening();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[InfoCardState] Error opening: {ex.Message}");
                Close();
            }
        }

        /// <summary>
        /// Rebuilds the tree and announces opening. Called after stats have loaded.
        /// </summary>
        public static void RebuildAndAnnounce()
        {
            if (!IsActive || currentDialog == null)
                return;

            try
            {
                // Rebuild tree now that stats are populated
                rootItem = InfoCardTreeBuilder.BuildTree(currentDialog);
                RebuildVisibleList();
                selectedIndex = 0;

                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                AnnounceOpening();
            }
            catch (Exception ex)
            {
                Log.Error($"[InfoCardState] Error rebuilding: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the Info Card accessibility state.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            currentDialog = null;
            rootItem = null;
            visibleItems = null;
            selectedIndex = 0;
            typeahead.ClearSearch();
            MenuHelper.ResetLevel("InfoCard");
        }

        /// <summary>
        /// Selects the next item (Down arrow).
        /// </summary>
        public static void SelectNext()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectNext(selectedIndex, visibleItems.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Selects the previous item (Up arrow).
        /// </summary>
        public static void SelectPrevious()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, visibleItems.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Jumps to the next category or subcategory header (Page Down).
        /// </summary>
        public static void JumpToNextCategory()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            typeahead.ClearSearch();

            // Search forward from current position
            for (int i = selectedIndex + 1; i < visibleItems.Count; i++)
            {
                var item = visibleItems[i];
                if (item.Type == InspectionTreeItem.ItemType.Category ||
                    item.Type == InspectionTreeItem.ItemType.SubCategory)
                {
                    selectedIndex = i;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    return;
                }
            }

            // Wrap to beginning (if enabled)
            if (RimWorldAccessMod_Settings.Settings?.WrapNavigation == true)
            {
                for (int i = 0; i <= selectedIndex; i++)
                {
                    var item = visibleItems[i];
                    if (item.Type == InspectionTreeItem.ItemType.Category ||
                        item.Type == InspectionTreeItem.ItemType.SubCategory)
                    {
                        selectedIndex = i;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        return;
                    }
                }
            }

            // No categories found or wrap disabled
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Jumps to the previous category or subcategory header (Page Up).
        /// </summary>
        public static void JumpToPreviousCategory()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            typeahead.ClearSearch();

            // Search backward from current position
            for (int i = selectedIndex - 1; i >= 0; i--)
            {
                var item = visibleItems[i];
                if (item.Type == InspectionTreeItem.ItemType.Category ||
                    item.Type == InspectionTreeItem.ItemType.SubCategory)
                {
                    selectedIndex = i;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                    return;
                }
            }

            // Wrap to end (if enabled)
            if (RimWorldAccessMod_Settings.Settings?.WrapNavigation == true)
            {
                for (int i = visibleItems.Count - 1; i >= selectedIndex; i--)
                {
                    var item = visibleItems[i];
                    if (item.Type == InspectionTreeItem.ItemType.Category ||
                        item.Type == InspectionTreeItem.ItemType.SubCategory)
                    {
                        selectedIndex = i;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                        return;
                    }
                }
            }

            // No categories found or wrap disabled
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Jumps to the first sibling at the same indent level (Home key).
        /// </summary>
        public static void JumpToFirst()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = MenuHelper.JumpToFirstSibling(visibleItems, selectedIndex, item => item.IndentLevel);
            typeahead.ClearSearch();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Jumps to the last item in the current scope (End key).
        /// If on an expanded node, jumps to its last visible descendant.
        /// Otherwise, jumps to last sibling at same level.
        /// </summary>
        public static void JumpToLast()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            var currentItem = visibleItems[selectedIndex];

            // If current item is expanded and has children, jump to last descendant
            if (currentItem.IsExpanded && currentItem.Children.Count > 0)
            {
                // Find last visible descendant
                int lastDescendantIndex = selectedIndex;
                for (int i = selectedIndex + 1; i < visibleItems.Count; i++)
                {
                    if (visibleItems[i].IndentLevel <= currentItem.IndentLevel)
                        break;
                    lastDescendantIndex = i;
                }
                selectedIndex = lastDescendantIndex;
            }
            else
            {
                // Jump to last sibling
                selectedIndex = MenuHelper.JumpToLastSibling(visibleItems, selectedIndex, item => item.IndentLevel);
            }

            typeahead.ClearSearch();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Jumps to the absolute first item in the entire tree (Ctrl+Home).
        /// </summary>
        public static void JumpToAbsoluteFirst()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = 0;
            typeahead.ClearSearch();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Jumps to the absolute last item in the entire tree (Ctrl+End).
        /// </summary>
        public static void JumpToAbsoluteLast()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = visibleItems.Count - 1;
            typeahead.ClearSearch();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the selected item (Right arrow).
        /// WCAG behavior:
        /// - On closed node: Open node, focus stays on current item
        /// - On open node: Move to first child
        /// - On end node: Reject sound
        /// </summary>
        public static void Expand()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            typeahead.ClearSearch();
            var item = visibleItems[selectedIndex];

            // End node (not expandable) - reject
            if (!item.IsExpandable)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Cannot expand this item.", SpeechPriority.High);
                return;
            }

            // Already expanded - move to first child
            if (item.IsExpanded)
            {
                MoveToFirstChild();
                return;
            }

            // Collapsed node - expand it
            if (item.OnActivate != null && item.Children.Count == 0)
            {
                item.OnActivate();
            }

            if (item.Children.Count == 0)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("No items to show.");
                return;
            }

            item.IsExpanded = true;
            RebuildVisibleList();
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands all sibling categories at the same level as the current item.
        /// WCAG tree view pattern: * key expands all siblings.
        /// </summary>
        public static void ExpandAllSiblings()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            typeahead.ClearSearch();

            var currentItem = visibleItems[selectedIndex];

            // Get siblings - items with the same parent
            List<InspectionTreeItem> siblings;
            if (currentItem.Parent == null || currentItem.Parent == rootItem)
            {
                siblings = rootItem.Children;
            }
            else
            {
                siblings = currentItem.Parent.Children;
            }

            // Find all collapsed sibling nodes that can be expanded
            var collapsedSiblings = new List<InspectionTreeItem>();
            foreach (var sibling in siblings)
            {
                if (sibling.IsExpandable && !sibling.IsExpanded)
                {
                    collapsedSiblings.Add(sibling);
                }
            }

            // Check if there are any expandable items at this level at all
            bool hasExpandableItems = false;
            foreach (var sibling in siblings)
            {
                if (sibling.IsExpandable)
                {
                    hasExpandableItems = true;
                    break;
                }
            }

            if (!hasExpandableItems)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("No categories to expand at this level.");
                return;
            }

            if (collapsedSiblings.Count == 0)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("All categories already expanded at this level.");
                return;
            }

            // Expand all collapsed siblings
            int expandedCount = 0;
            foreach (var sibling in collapsedSiblings)
            {
                // Trigger lazy loading if needed
                if (sibling.OnActivate != null && sibling.Children.Count == 0)
                {
                    sibling.OnActivate();
                }

                // Only count as expanded if there are children to show
                if (sibling.Children.Count > 0)
                {
                    sibling.IsExpanded = true;
                    expandedCount++;
                }
            }

            // Rebuild the visible items list
            RebuildVisibleList();

            // Announce result
            if (expandedCount == 0)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("No categories to expand at this level.");
            }
            else
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                string categoryWord = expandedCount == 1 ? "category" : "categories";
                TolkHelper.Speak($"Expanded {expandedCount} {categoryWord}.");
            }
        }

        /// <summary>
        /// Collapses the selected item (Left arrow).
        /// WCAG behavior:
        /// - On open node: Close node, focus stays on current item
        /// - On closed node: Move to parent
        /// - On end node: Move to parent
        /// </summary>
        public static void Collapse()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            typeahead.ClearSearch();
            var item = visibleItems[selectedIndex];

            // Case 1: Item is expandable and expanded - collapse it
            if (item.IsExpandable && item.IsExpanded)
            {
                item.IsExpanded = false;
                RebuildVisibleList();

                if (selectedIndex >= visibleItems.Count)
                    selectedIndex = Math.Max(0, visibleItems.Count - 1);

                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            // Case 2: Move to parent
            MoveToParent();
        }

        /// <summary>
        /// Activates the selected item (Enter key).
        /// For expandable items: expands them.
        /// For action items: executes the action.
        /// </summary>
        public static void ActivateItem()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            var item = visibleItems[selectedIndex];

            // Handle expandable items
            if (item.IsExpandable)
            {
                if (!item.IsExpanded)
                {
                    Expand();
                }
                else
                {
                    // Already expanded - don't re-trigger OnActivate
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    TolkHelper.Speak("Already expanded.");
                }
                return;
            }

            // For non-expandable items with actions, execute the action
            if (item.OnActivate != null)
            {
                item.OnActivate();
                SoundDefOf.Click.PlayOneShotOnCamera();
                return;
            }

            // Otherwise, nothing to do
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            TolkHelper.Speak("No action available for this item.");
        }

        /// <summary>
        /// Closes the Info Card (Escape key).
        /// </summary>
        public static void CloseInfoCard()
        {
            if (!IsActive)
                return;

            // Close the visual dialog as well
            if (currentDialog != null)
            {
                currentDialog.Close();
            }

            Close();
            SoundDefOf.Click.PlayOneShotOnCamera();
            TolkHelper.Speak("Info card closed.");
        }

        /// <summary>
        /// Handles keyboard input for the Info Card.
        /// Returns true if input was handled.
        /// Called from UnifiedKeyboardPatch which handles Event.current.Use().
        /// </summary>
        public static bool HandleInput(Event ev)
        {
            if (!IsActive || ev.type != EventType.KeyDown)
                return false;

            KeyCode key = ev.keyCode;

            // Escape - close
            if (key == KeyCode.Escape)
            {
                CloseInfoCard();
                return true;
            }

            // Up arrow - previous item
            if (key == KeyCode.UpArrow)
            {
                SelectPrevious();
                return true;
            }

            // Down arrow - next item
            if (key == KeyCode.DownArrow)
            {
                SelectNext();
                return true;
            }

            // Right arrow - expand
            if (key == KeyCode.RightArrow)
            {
                Expand();
                return true;
            }

            // Left arrow - collapse
            if (key == KeyCode.LeftArrow)
            {
                Collapse();
                return true;
            }

            // Page Down - jump to next category
            if (key == KeyCode.PageDown)
            {
                JumpToNextCategory();
                return true;
            }

            // Page Up - jump to previous category
            if (key == KeyCode.PageUp)
            {
                JumpToPreviousCategory();
                return true;
            }

            // Home - jump to first (Ctrl+Home for absolute first)
            if (key == KeyCode.Home)
            {
                if (ev.control)
                    JumpToAbsoluteFirst();
                else
                    JumpToFirst();
                return true;
            }

            // End - jump to last (Ctrl+End for absolute last)
            if (key == KeyCode.End)
            {
                if (ev.control)
                    JumpToAbsoluteLast();
                else
                    JumpToLast();
                return true;
            }

            // Asterisk (*) - expand all sibling categories (WCAG tree view pattern)
            bool isStar = key == KeyCode.KeypadMultiply || (ev.shift && key == KeyCode.Alpha8);
            if (isStar)
            {
                ExpandAllSiblings();
                return true;
            }

            // Enter - activate
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                ActivateItem();
                return true;
            }

            // Typeahead search - alphanumeric keys
            if (ev.character != '\0' && char.IsLetterOrDigit(ev.character))
            {
                HandleTypeahead(ev.character);
                return true;
            }

            // Backspace - clear search
            if (key == KeyCode.Backspace && typeahead.HasActiveSearch)
            {
                typeahead.ClearSearch();
                SoundDefOf.Click.PlayOneShotOnCamera();
                TolkHelper.Speak("Search cleared.");
                return true;
            }

            return false;
        }

        #region Private Methods

        /// <summary>
        /// Rebuilds the visible items list from the tree.
        /// </summary>
        private static void RebuildVisibleList()
        {
            visibleItems = new List<InspectionTreeItem>();
            if (rootItem != null)
            {
                CollectVisibleItems(rootItem, visibleItems);
            }
        }

        /// <summary>
        /// Recursively collects visible items from the tree.
        /// </summary>
        private static void CollectVisibleItems(InspectionTreeItem item, List<InspectionTreeItem> list)
        {
            // Skip root item (it's just a container)
            if (item.IndentLevel >= 0)
            {
                list.Add(item);
            }

            // Add children if expanded (or if it's the root)
            if (item.IsExpanded || item.IndentLevel < 0)
            {
                foreach (var child in item.Children)
                {
                    CollectVisibleItems(child, list);
                }
            }
        }

        /// <summary>
        /// Moves selection to the first child of the current item.
        /// </summary>
        private static void MoveToFirstChild()
        {
            var item = visibleItems[selectedIndex];
            if (item.Children.Count > 0)
            {
                int itemIndex = visibleItems.IndexOf(item);
                if (itemIndex < 0)
                    return; // Item not in list, shouldn't happen but guard against it

                int firstChildIndex = itemIndex + 1;
                if (firstChildIndex < visibleItems.Count)
                {
                    selectedIndex = firstChildIndex;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                }
            }
        }

        /// <summary>
        /// Moves selection to the parent of the current item.
        /// </summary>
        private static void MoveToParent()
        {
            var item = visibleItems[selectedIndex];
            var parent = item.Parent;

            // Don't move to root (hidden)
            if (parent == null || parent == rootItem)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("At top level.");
                return;
            }

            int parentIndex = visibleItems.IndexOf(parent);
            if (parentIndex >= 0)
            {
                selectedIndex = parentIndex;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
        }

        /// <summary>
        /// Handles typeahead search input.
        /// </summary>
        private static void HandleTypeahead(char c)
        {
            var labels = new List<string>();
            foreach (var item in visibleItems)
            {
                labels.Add(item.Label.StripTags());
            }

            if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
            {
                selectedIndex = newIndex;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceWithSearch();
            }
            else
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak($"No matches for '{typeahead.LastFailedSearch}'.");
            }
        }

        /// <summary>
        /// Announces the current selection with search context if applicable.
        /// </summary>
        private static void AnnounceWithSearch()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            if (selectedIndex < 0 || selectedIndex >= visibleItems.Count)
                return;

            var item = visibleItems[selectedIndex];
            string label = item.Label.StripTags();

            string stateIndicator = "";
            if (item.IsExpandable)
            {
                stateIndicator = item.IsExpanded ? " expanded" : " collapsed";
            }

            string announcement = $"{label}{stateIndicator}, {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} matches for '{typeahead.SearchBuffer}'";
            TolkHelper.Speak(announcement);
        }

        /// <summary>
        /// Gets the sibling position (X of Y) for the given item.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(InspectionTreeItem item)
        {
            List<InspectionTreeItem> siblings;
            if (item.Parent == null || item.Parent == rootItem)
            {
                siblings = rootItem.Children;
            }
            else
            {
                siblings = item.Parent.Children;
            }
            int position = siblings.IndexOf(item) + 1;
            return (position, siblings.Count);
        }

        /// <summary>
        /// Announces the opening of the Info Card.
        /// </summary>
        private static void AnnounceOpening()
        {
            if (rootItem == null)
                return;

            string rootLabel = rootItem.Label.StripTags();

            // Check if children are tabs (Category type) or direct content (single-tab case)
            bool hasTabs = rootItem.Children.Count > 0 &&
                           rootItem.Children[0].Type == InspectionTreeItem.ItemType.Category;

            string announcement;
            if (hasTabs)
            {
                int tabCount = rootItem.Children.Count;
                announcement = $"{rootLabel}. {tabCount} tabs available. Use Up and Down to navigate, Right to expand, Left to collapse.";
            }
            else
            {
                int itemCount = rootItem.Children.Count;
                announcement = $"{rootLabel}. {itemCount} items. Use Up and Down to navigate, Page Up and Page Down to jump between categories.";
            }

            TolkHelper.Speak(announcement);
        }

        /// <summary>
        /// Announces the current selection to the screen reader.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            try
            {
                if (visibleItems == null || visibleItems.Count == 0)
                {
                    TolkHelper.Speak("No items available.");
                    return;
                }

                if (selectedIndex < 0 || selectedIndex >= visibleItems.Count)
                    return;

                var item = visibleItems[selectedIndex];

                // Strip XML tags from label
                string label = item.Label.StripTags().TrimEnd('.', '!', '?');

                // Build state indicator (only for expandable items)
                string stateIndicator = "";
                if (item.IsExpandable)
                {
                    stateIndicator = item.IsExpanded ? " expanded" : " collapsed";
                }

                // Get sibling position
                var (position, total) = GetSiblingPosition(item);

                // Build level suffix if level changed
                string levelSuffix = MenuHelper.GetLevelSuffix("InfoCard", item.IndentLevel, skipLevelOne: false);

                // Build full announcement (respects AnnouncePosition setting)
                string positionPart = MenuHelper.FormatPosition(position - 1, total);
                string announcement = string.IsNullOrEmpty(positionPart)
                    ? $"{label}{stateIndicator}.{levelSuffix}"
                    : $"{label}{stateIndicator}.{levelSuffix} {positionPart}.";

                TolkHelper.Speak(announcement);
            }
            catch (Exception ex)
            {
                Log.Error($"[InfoCardState] Error announcing selection: {ex.Message}");
            }
        }

        #endregion
    }
}
