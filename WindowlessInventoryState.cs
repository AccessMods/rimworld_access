using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the windowless colony-wide inventory menu (activated with 'I' key)
    /// Displays all stored items organized by category with hierarchical navigation
    /// </summary>
    public static class WindowlessInventoryState
    {
        private static bool isActive = false;
        private static List<TreeNode> rootNodes = new List<TreeNode>();
        private static List<TreeNode> flattenedVisibleNodes = new List<TreeNode>();
        private static int selectedIndex = 0;
        private static Dictionary<TreeNode, TreeNode> lastChildPerParent = new Dictionary<TreeNode, TreeNode>();
        private static int lastAnnouncedLevel = -1;

        public static bool IsActive => isActive;

        /// <summary>
        /// Represents a node in the inventory tree (category, item, or action)
        /// </summary>
        public class TreeNode
        {
            public enum NodeType
            {
                Category,      // A ThingCategoryDef
                Item,          // An aggregated inventory item
                Action         // An action (Jump to location, View details)
            }

            public NodeType Type { get; set; }
            public string Label { get; set; }
            public int Depth { get; set; }
            public bool IsExpanded { get; set; }
            public bool CanExpand { get; set; }

            // For Category nodes
            public InventoryHelper.CategoryNode CategoryData { get; set; }

            // For Item nodes
            public InventoryHelper.InventoryItem ItemData { get; set; }

            // For Action nodes
            public Action OnActivate { get; set; }

            // Tree structure
            public TreeNode Parent { get; set; }
            public List<TreeNode> Children { get; set; }

            public TreeNode()
            {
                Children = new List<TreeNode>();
                IsExpanded = false;
                CanExpand = false;
            }

            public string GetDisplayString()
            {
                string indent = new string(' ', Depth * 2);
                string expandIndicator = "";

                if (CanExpand)
                {
                    expandIndicator = IsExpanded ? "▼ " : "► ";
                }

                return $"{indent}{expandIndicator}{Label}";
            }
        }

        /// <summary>
        /// Opens the inventory menu and collects all colony storage data
        /// </summary>
        public static void Open()
        {
            if (Find.CurrentMap == null)
            {
                Log.Warning("WindowlessInventoryState: Cannot open - no current map");
                return;
            }

            isActive = true;
            selectedIndex = 0;
            lastChildPerParent.Clear();
            lastAnnouncedLevel = -1;

            // Collect all stored items
            List<Thing> allItems = InventoryHelper.GetAllStoredItems();

            if (allItems.Count == 0)
            {
                TolkHelper.Speak("Inventory menu opened. No items in colony storage.");
                rootNodes = new List<TreeNode>();
                flattenedVisibleNodes = new List<TreeNode>();
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                return;
            }

            // Aggregate items by type
            Dictionary<ThingDef, InventoryHelper.InventoryItem> aggregatedItems = InventoryHelper.AggregateStacks(allItems);

            // Build category tree
            List<InventoryHelper.CategoryNode> categoryTree = InventoryHelper.BuildCategoryTree(aggregatedItems);

            // Convert to TreeNode structure
            rootNodes = BuildTreeNodes(categoryTree);

            // Flatten to get initially visible nodes
            RebuildFlattenedList();

            // Announce opening
            string announcement = $"Colony inventory opened. {aggregatedItems.Count} item types in storage. {rootNodes.Count} categories.";
            TolkHelper.Speak(announcement);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();

            // Announce first selection
            if (flattenedVisibleNodes.Count > 0)
            {
                AnnounceCurrentSelection();
            }
        }

        /// <summary>
        /// Converts InventoryHelper.CategoryNode tree to TreeNode tree
        /// </summary>
        private static List<TreeNode> BuildTreeNodes(List<InventoryHelper.CategoryNode> categoryNodes, TreeNode parent = null, int depth = 0)
        {
            List<TreeNode> nodes = new List<TreeNode>();

            foreach (InventoryHelper.CategoryNode categoryNode in categoryNodes)
            {
                // Create category node
                TreeNode catNode = new TreeNode
                {
                    Type = TreeNode.NodeType.Category,
                    Label = categoryNode.GetDisplayLabel(),
                    Depth = depth,
                    CategoryData = categoryNode,
                    Parent = parent,
                    CanExpand = (categoryNode.SubCategories.Count > 0 || categoryNode.Items.Count > 0)
                };

                // Build children (subcategories)
                if (categoryNode.SubCategories.Count > 0)
                {
                    catNode.Children.AddRange(BuildTreeNodes(categoryNode.SubCategories, catNode, depth + 1));
                }

                // Add items as children
                foreach (InventoryHelper.InventoryItem item in categoryNode.Items)
                {
                    TreeNode itemNode = new TreeNode
                    {
                        Type = TreeNode.NodeType.Item,
                        Label = item.GetDisplayLabel(),
                        Depth = depth + 1,
                        ItemData = item,
                        Parent = catNode,
                        CanExpand = true // Items can expand to show actions
                    };

                    // Build action children
                    itemNode.Children.AddRange(BuildItemActionNodes(item, itemNode, depth + 2));

                    catNode.Children.Add(itemNode);
                }

                nodes.Add(catNode);
            }

            return nodes;
        }

        /// <summary>
        /// Builds action nodes for an inventory item
        /// </summary>
        private static List<TreeNode> BuildItemActionNodes(InventoryHelper.InventoryItem item, TreeNode parent, int depth)
        {
            List<TreeNode> actions = new List<TreeNode>();

            // Action: Jump to location
            if (item.StorageLocations.Count > 0)
            {
                TreeNode jumpAction = new TreeNode
                {
                    Type = TreeNode.NodeType.Action,
                    Label = "Jump to location",
                    Depth = depth,
                    Parent = parent,
                    CanExpand = false,
                    OnActivate = () => JumpToItem(item)
                };
                actions.Add(jumpAction);
            }

            // Action: View details
            TreeNode detailsAction = new TreeNode
            {
                Type = TreeNode.NodeType.Action,
                Label = "View details",
                Depth = depth,
                Parent = parent,
                CanExpand = false,
                OnActivate = () => ViewItemDetails(item)
            };
            actions.Add(detailsAction);

            return actions;
        }

        /// <summary>
        /// Rebuilds the flattened list of visible nodes based on expansion states
        /// </summary>
        private static void RebuildFlattenedList()
        {
            flattenedVisibleNodes.Clear();
            AddVisibleNodes(rootNodes);

            // Clamp selection index
            if (selectedIndex >= flattenedVisibleNodes.Count)
            {
                selectedIndex = Math.Max(0, flattenedVisibleNodes.Count - 1);
            }
        }

        /// <summary>
        /// Recursively adds visible nodes to the flattened list
        /// </summary>
        private static void AddVisibleNodes(List<TreeNode> nodes)
        {
            foreach (TreeNode node in nodes)
            {
                flattenedVisibleNodes.Add(node);

                if (node.IsExpanded && node.Children.Count > 0)
                {
                    AddVisibleNodes(node.Children);
                }
            }
        }

        /// <summary>
        /// Closes the inventory menu
        /// </summary>
        public static void Close()
        {
            isActive = false;
            rootNodes.Clear();
            flattenedVisibleNodes.Clear();
            selectedIndex = 0;
            lastChildPerParent.Clear();
            lastAnnouncedLevel = -1;

            TolkHelper.Speak("Inventory menu closed.");
            SoundDefOf.TabClose.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Handles keyboard input for the inventory menu
        /// Returns true if the input was handled
        /// </summary>
        public static bool HandleInput(Event ev)
        {
            if (!isActive) return false;

            KeyCode key = ev.keyCode;

            // Up arrow - previous item
            if (key == KeyCode.UpArrow)
            {
                ev.Use();
                SelectPrevious();
                return true;
            }

            // Down arrow - next item
            if (key == KeyCode.DownArrow)
            {
                ev.Use();
                SelectNext();
                return true;
            }

            // Right arrow - expand current node
            if (key == KeyCode.RightArrow)
            {
                ev.Use();
                ExpandCurrent();
                return true;
            }

            // Left arrow - collapse current node
            if (key == KeyCode.LeftArrow)
            {
                ev.Use();
                CollapseCurrent();
                return true;
            }

            // Enter - activate current node
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                ev.Use();
                ActivateCurrent();
                return true;
            }

            // Escape - close menu
            if (key == KeyCode.Escape)
            {
                ev.Use();
                Close();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Selects the previous item in the list
        /// </summary>
        private static void SelectPrevious()
        {
            if (flattenedVisibleNodes.Count == 0) return;

            selectedIndex--;
            if (selectedIndex < 0)
            {
                selectedIndex = flattenedVisibleNodes.Count - 1; // Wrap to bottom
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Selects the next item in the list
        /// </summary>
        private static void SelectNext()
        {
            if (flattenedVisibleNodes.Count == 0) return;

            selectedIndex++;
            if (selectedIndex >= flattenedVisibleNodes.Count)
            {
                selectedIndex = 0; // Wrap to top
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the currently selected node (WCAG-compliant)
        /// - If collapsed and expandable: expand, focus stays on current item
        /// - If already expanded with children: move to first child
        /// - If end node (not expandable): reject feedback
        /// </summary>
        private static void ExpandCurrent()
        {
            if (flattenedVisibleNodes.Count == 0) return;

            TreeNode current = flattenedVisibleNodes[selectedIndex];

            if (!current.CanExpand)
            {
                // End node - provide reject feedback
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Cannot expand this item.", SpeechPriority.High);
                return;
            }

            if (!current.IsExpanded)
            {
                // Collapsed node - expand it, focus stays on current item
                current.IsExpanded = true;
                RebuildFlattenedList();
                SoundDefOf.Click.PlayOneShotOnCamera();
                // Focus stays on current item, just announce the state change
                AnnounceCurrentSelection();
            }
            else
            {
                // Already expanded - move to first child
                MoveToFirstChild();
            }
        }

        /// <summary>
        /// Moves focus to the first child of the current expanded item
        /// </summary>
        private static void MoveToFirstChild()
        {
            TreeNode current = flattenedVisibleNodes[selectedIndex];

            if (current.Children.Count > 0)
            {
                int firstChildIndex = flattenedVisibleNodes.IndexOf(current) + 1;
                if (firstChildIndex < flattenedVisibleNodes.Count)
                {
                    selectedIndex = firstChildIndex;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentSelection();
                }
            }
        }

        /// <summary>
        /// Collapses the currently selected node (WCAG-compliant)
        /// - If expanded: collapse, focus stays on current item
        /// - If collapsed or end node: move to parent WITHOUT collapsing it
        /// </summary>
        private static void CollapseCurrent()
        {
            if (flattenedVisibleNodes.Count == 0) return;

            TreeNode current = flattenedVisibleNodes[selectedIndex];

            if (current.CanExpand && current.IsExpanded)
            {
                // Currently expanded - collapse it, focus stays on current item
                current.IsExpanded = false;
                RebuildFlattenedList();
                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
            else if (current.Parent != null)
            {
                // Collapsed or end node - move to parent WITHOUT collapsing it
                // Save current child position for potential future restoration
                lastChildPerParent[current.Parent] = current;

                // Move focus to parent (do NOT collapse it)
                selectedIndex = flattenedVisibleNodes.IndexOf(current.Parent);
                if (selectedIndex < 0) selectedIndex = 0;

                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
            else
            {
                // Already at top level with no parent
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Already at top level.", SpeechPriority.High);
            }
        }

        /// <summary>
        /// Activates the currently selected node
        /// </summary>
        private static void ActivateCurrent()
        {
            if (flattenedVisibleNodes.Count == 0) return;

            TreeNode current = flattenedVisibleNodes[selectedIndex];

            // If it's an action, execute it
            if (current.Type == TreeNode.NodeType.Action && current.OnActivate != null)
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                current.OnActivate();
                return;
            }

            // If it's a category or item, toggle expansion
            if (current.CanExpand)
            {
                if (current.IsExpanded)
                {
                    CollapseCurrent();
                }
                else
                {
                    ExpandCurrent();
                }
            }
            else
            {
                TolkHelper.Speak("This item has no actions.");
            }
        }

        /// <summary>
        /// Gets the sibling position (1-based) and total siblings for a node
        /// </summary>
        private static (int position, int total) GetSiblingPosition(TreeNode item)
        {
            List<TreeNode> siblings = item.Parent != null ? item.Parent.Children : rootNodes;
            int position = siblings.IndexOf(item) + 1;
            return (position, siblings.Count);
        }

        /// <summary>
        /// Gets the level suffix for announcements (only when level changes)
        /// </summary>
        private static string GetLevelSuffix(int currentLevel)
        {
            int displayLevel = currentLevel + 1; // 1-based for users
            if (displayLevel != lastAnnouncedLevel)
            {
                lastAnnouncedLevel = displayLevel;
                return $". level {displayLevel}.";
            }
            return ".";  // Always end with period
        }

        /// <summary>
        /// Announces the currently selected item via screen reader
        /// Format: "{name} {state}. {X of Y}. level N." or "{name}. {X of Y}. level N."
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            if (flattenedVisibleNodes.Count == 0)
            {
                TolkHelper.Speak("Inventory is empty.");
                return;
            }

            TreeNode current = flattenedVisibleNodes[selectedIndex];

            // Get state info (only for expandable nodes)
            string stateInfo = "";
            if (current.CanExpand)
            {
                stateInfo = current.IsExpanded ? "expanded" : "collapsed";
            }

            // Get sibling position
            var (position, total) = GetSiblingPosition(current);
            string positionInfo = $"{position} of {total}";

            // Get level suffix (only announced when level changes)
            string levelSuffix = GetLevelSuffix(current.Depth);

            // Build announcement: "{name} {state}. {X of Y}{levelSuffix}" or "{name}. {X of Y}{levelSuffix}"
            string announcement;
            if (string.IsNullOrEmpty(stateInfo))
            {
                // End node (action) - no state
                announcement = $"{current.Label}. {positionInfo}{levelSuffix}";
            }
            else
            {
                announcement = $"{current.Label} {stateInfo}. {positionInfo}{levelSuffix}";
            }

            TolkHelper.Speak(announcement.Trim());
        }

        /// <summary>
        /// Jumps the camera and cursor to the location of an item
        /// </summary>
        private static void JumpToItem(InventoryHelper.InventoryItem item)
        {
            if (item.StorageLocations.Count == 0)
            {
                TolkHelper.Speak("No storage location found for this item.");
                return;
            }

            IntVec3 location = item.StorageLocations[0];

            // Jump camera
            if (Find.CameraDriver != null)
            {
                Find.CameraDriver.JumpToCurrentMapLoc(location);
            }

            // Update map cursor if initialized
            if (MapNavigationState.IsInitialized)
            {
                MapNavigationState.CurrentCursorPosition = location;
            }

            TolkHelper.Speak($"Jumped to {item.Def.LabelCap} storage location at {location}.");
            SoundDefOf.Click.PlayOneShotOnCamera();

            // Close the inventory menu after jumping
            Close();
        }

        /// <summary>
        /// Views detailed information about an item
        /// </summary>
        private static void ViewItemDetails(InventoryHelper.InventoryItem item)
        {
            ThingDef def = item.Def;

            // Build detailed description
            List<string> details = new List<string>();
            details.Add($"Item: {def.LabelCap}");
            details.Add($"Total quantity: {item.TotalQuantity}");
            details.Add($"Description: {def.description}");

            if (def.stackLimit > 1)
            {
                details.Add($"Stack limit: {def.stackLimit}");
            }

            if (def.BaseMarketValue > 0)
            {
                details.Add($"Market value: ${def.BaseMarketValue:F2} each, ${def.BaseMarketValue * item.TotalQuantity:F2} total");
            }

            if (def.statBases != null && def.statBases.Count > 0)
            {
                details.Add($"Mass: {def.statBases.GetStatValueFromList(StatDefOf.Mass, 0):F2} kg");
            }

            details.Add($"Storage locations: {item.StorageLocations.Count}");

            string announcement = string.Join(". ", details);
            TolkHelper.Speak(announcement);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Draws visual highlights for the selected item (for sighted users)
        /// </summary>
        public static void DrawHighlight()
        {
            if (!isActive || flattenedVisibleNodes.Count == 0) return;

            TreeNode current = flattenedVisibleNodes[selectedIndex];

            // Calculate position for highlight
            float lineHeight = 24f;
            float yOffset = selectedIndex * lineHeight;
            float xOffset = 20f;
            float width = 600f;
            float height = lineHeight;

            Rect highlightRect = new Rect(xOffset, yOffset + 100f, width, height);

            // Draw semi-transparent highlight
            Color highlightColor = new Color(1f, 1f, 0f, 0.3f); // Yellow with alpha
            Widgets.DrawBoxSolid(highlightRect, highlightColor);

            // Draw label
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(highlightRect, current.GetDisplayString());
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
