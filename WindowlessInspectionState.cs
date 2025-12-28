using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the windowless inspection panel state.
    /// Uses a tree structure with inline expansion/collapse.
    /// </summary>
    public static class WindowlessInspectionState
    {
        public static bool IsActive { get; private set; } = false;

        private static InspectionTreeItem rootItem = null;
        private static List<InspectionTreeItem> visibleItems = null;
        private static int selectedIndex = 0;
        private static IntVec3 inspectionPosition;
        private static object parentObject = null; // Track parent object for navigation back
        private static Dictionary<InspectionTreeItem, InspectionTreeItem> lastChildPerParent = new Dictionary<InspectionTreeItem, InspectionTreeItem>();

        /// <summary>
        /// Opens the inspection menu for the specified position.
        /// </summary>
        public static void Open(IntVec3 position)
        {
            try
            {
                inspectionPosition = position;

                // Build the object list
                var objects = BuildObjectList();

                if (objects.Count == 0)
                {
                    TolkHelper.Speak("No items here to inspect.");
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return;
                }

                // Build the tree
                rootItem = InspectionTreeBuilder.BuildTree(objects);
                RebuildVisibleList();
                selectedIndex = 0;
                lastChildPerParent.Clear();

                IsActive = true;
                SoundDefOf.TabOpen.PlayOneShotOnCamera();

                // Special handling for single object: auto-expand and position on first child
                if (objects.Count == 1 && visibleItems.Count > 0)
                {
                    var singleItem = visibleItems[0];
                    if (singleItem.IsExpandable)
                    {
                        // Announce just the item name (no expand/collapse status)
                        TolkHelper.Speak(singleItem.Label.StripTags());

                        // Trigger lazy loading if needed
                        if (singleItem.OnActivate != null && singleItem.Children.Count == 0)
                        {
                            singleItem.OnActivate();
                        }

                        if (singleItem.Children.Count > 0)
                        {
                            singleItem.IsExpanded = true;
                            RebuildVisibleList();
                            selectedIndex = 1; // First child
                            AnnounceCurrentSelection();
                        }
                        return; // Early return - skip normal announcement
                    }
                }

                // Normal case: multiple objects or non-expandable single object
                AnnounceCurrentSelection();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error opening inspection menu: {ex}");
                Close();
            }
        }

        /// <summary>
        /// Opens the inspection menu for a specific object (Thing, Pawn, Building, etc.).
        /// </summary>
        /// <param name="obj">The object to inspect</param>
        /// <param name="parent">Optional parent object to return to when pressing Escape</param>
        public static void OpenForObject(object obj, object parent = null)
        {
            try
            {
                if (obj == null)
                {
                    TolkHelper.Speak("No object to inspect.");
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return;
                }

                // Set position if it's a Thing
                if (obj is Thing thing)
                {
                    inspectionPosition = thing.Position;
                }
                else
                {
                    inspectionPosition = IntVec3.Invalid;
                }

                // Store parent for navigation back
                parentObject = parent;

                // Build tree with just this object
                var objects = new List<object> { obj };
                rootItem = InspectionTreeBuilder.BuildTree(objects);
                RebuildVisibleList();
                selectedIndex = 0;
                lastChildPerParent.Clear();

                IsActive = true;
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error opening inspection menu for object: {ex}");
                Close();
            }
        }

        /// <summary>
        /// Closes the inspection menu.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            rootItem = null;
            visibleItems = null;
            lastChildPerParent.Clear();
            selectedIndex = 0;
            parentObject = null;
        }

        /// <summary>
        /// Rebuilds the tree (used after actions that modify state).
        /// </summary>
        public static void RebuildTree()
        {
            if (!IsActive)
                return;

            var objects = BuildObjectList();
            rootItem = InspectionTreeBuilder.BuildTree(objects);
            RebuildVisibleList();
            lastChildPerParent.Clear(); // Clear saved positions since tree nodes are recreated

            // Try to keep selection valid
            if (selectedIndex >= visibleItems.Count)
                selectedIndex = Math.Max(0, visibleItems.Count - 1);

            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Builds the list of inspectable objects at the cursor position.
        /// </summary>
        private static List<object> BuildObjectList()
        {
            var objects = new List<object>();

            if (Find.CurrentMap == null)
                return objects;

            var objectsAtPosition = Selector.SelectableObjectsAt(inspectionPosition, Find.CurrentMap);

            foreach (var obj in objectsAtPosition)
            {
                if (obj is Pawn || obj is Building || obj is Plant || obj is Thing)
                {
                    objects.Add(obj);
                }
            }

            return objects;
        }

        /// <summary>
        /// Rebuilds the visible items list based on expansion state.
        /// </summary>
        private static void RebuildVisibleList()
        {
            visibleItems = new List<InspectionTreeItem>();

            if (rootItem == null)
                return;

            // Get all visible items from the tree
            foreach (var child in rootItem.Children)
            {
                visibleItems.AddRange(child.GetVisibleItems());
            }
        }

        /// <summary>
        /// Selects the next item.
        /// </summary>
        public static void SelectNext()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % visibleItems.Count;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Selects the previous item.
        /// </summary>
        public static void SelectPrevious()
        {
            if (!IsActive || visibleItems == null || visibleItems.Count == 0)
                return;

            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = visibleItems.Count - 1;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the selected item (Right arrow).
        /// </summary>
        public static void Expand()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            var item = visibleItems[selectedIndex];

            if (!item.IsExpandable)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("This item cannot be expanded.", SpeechPriority.High);
                return;
            }

            if (item.IsExpanded)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Already expanded.");
                return;
            }

            // Trigger lazy loading if needed
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

            // Restore last selected child position if available
            if (lastChildPerParent.TryGetValue(item, out InspectionTreeItem savedChild))
            {
                int savedIndex = visibleItems.IndexOf(savedChild);
                if (savedIndex >= 0)
                {
                    selectedIndex = savedIndex;
                }
            }
            else
            {
                // No saved position - move to first child
                int firstChildIndex = visibleItems.IndexOf(item) + 1;
                if (firstChildIndex < visibleItems.Count)
                {
                    selectedIndex = firstChildIndex;
                }
            }

            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Collapses the selected item (Left arrow).
        /// If on an expanded item, collapses it.
        /// If on a child item, collapses the parent and moves focus to it.
        /// </summary>
        public static void Collapse()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            var item = visibleItems[selectedIndex];

            // Case 1: Item is expandable and expanded - collapse it directly
            if (item.IsExpandable && item.IsExpanded)
            {
                item.IsExpanded = false;
                RebuildVisibleList();

                // Adjust selection if it's now out of range
                if (selectedIndex >= visibleItems.Count)
                    selectedIndex = Math.Max(0, visibleItems.Count - 1);

                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            // Case 2: Item is not expanded (or not expandable) - find parent and collapse it
            var parent = item.Parent;

            // Skip non-expandable parents (like root) to find an expandable ancestor
            while (parent != null && !parent.IsExpandable)
            {
                parent = parent.Parent;
            }

            if (parent == null || !parent.IsExpanded)
            {
                // No expandable parent to collapse - we're at the top level
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Already at top level.", SpeechPriority.High);
                return;
            }

            // Save current child position for this parent before collapsing
            lastChildPerParent[parent] = item;

            // Collapse the parent
            parent.IsExpanded = false;
            RebuildVisibleList();

            // Move selection to the parent
            int parentIndex = visibleItems.IndexOf(parent);
            if (parentIndex >= 0)
            {
                selectedIndex = parentIndex;
            }
            else if (selectedIndex >= visibleItems.Count)
            {
                selectedIndex = Math.Max(0, visibleItems.Count - 1);
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Activates the selected item (Enter key).
        /// </summary>
        public static void ActivateAction()
        {
            if (!IsActive || visibleItems == null || selectedIndex >= visibleItems.Count)
                return;

            var item = visibleItems[selectedIndex];

            // For expandable items, Enter acts like Right arrow
            if (item.IsExpandable && !item.IsExpanded)
            {
                Expand();
                return;
            }

            // For items with actions, execute the action
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
        /// Closes the entire panel (Escape key).
        /// If there's a parent object, returns to that parent's inspection instead of fully closing.
        /// </summary>
        public static void ClosePanel()
        {
            if (!IsActive)
                return;

            // Check if we have a parent to return to
            if (parentObject != null)
            {
                var parent = parentObject; // Save reference before Close() clears it
                Close();
                SoundDefOf.Click.PlayOneShotOnCamera();
                OpenForObject(parent); // Open parent without a parent (we don't go deeper than 2 levels)
            }
            else
            {
                Close();
                SoundDefOf.Click.PlayOneShotOnCamera();
                TolkHelper.Speak("Inspection panel closed.");
            }
        }

        /// <summary>
        /// Announces the current selection to the screen reader via clipboard.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            try
            {
                if (visibleItems == null || visibleItems.Count == 0)
                {
                    TolkHelper.Speak("No items to inspect.");
                    return;
                }

                if (selectedIndex < 0 || selectedIndex >= visibleItems.Count)
                    return;

                var item = visibleItems[selectedIndex];

                // Build indentation prefix
                string indent = new string(' ', item.IndentLevel * 2);

                // Build status indicators
                string expandIndicator = "";
                if (item.IsExpandable)
                {
                    expandIndicator = item.IsExpanded ? " [Expanded]" : " [Collapsed]";
                }

                // Build help text
                string helpText = "";
                if (item.Type == InspectionTreeItem.ItemType.Action)
                {
                    helpText = "Enter to execute";
                }

                // Strip XML tags from label
                string label = item.Label.StripTags();

                // Build full announcement
                string announcement = $"{indent}{label}{expandIndicator}";

                if (!string.IsNullOrEmpty(helpText))
                {
                    announcement += $"\n{helpText}";
                }

                TolkHelper.Speak(announcement);
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
            if (!IsActive)
                return false;

            if (ev.type != EventType.KeyDown)
                return false;

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

                    case KeyCode.RightArrow:
                        Expand();
                        ev.Use();
                        return true;

                    case KeyCode.LeftArrow:
                        Collapse();
                        ev.Use();
                        return true;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        ActivateAction();
                        ev.Use();
                        return true;

                    case KeyCode.Escape:
                        ClosePanel();
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
