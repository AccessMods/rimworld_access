using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// State management for viewing stat breakdown tree views.
    /// Parses game explanation strings into navigable tree structures.
    /// Used when pressing Alt+I on caravan stats in summary view.
    /// </summary>
    public static class StatBreakdownState
    {
        private static bool isActive = false;
        private static string statName = "";
        private static List<TreeItem> rootItems = new List<TreeItem>();
        private static List<TreeItem> flattenedItems = new List<TreeItem>();
        private static int selectedIndex = 0;

        /// <summary>
        /// Represents an item in the breakdown tree.
        /// </summary>
        private class TreeItem
        {
            public string Text { get; set; }
            public string ResultValue { get; set; } // The calculated result for this section
            public int IndentLevel { get; set; }
            public bool IsExpandable { get; set; }
            public bool IsExpanded { get; set; }
            public List<TreeItem> Children { get; set; } = new List<TreeItem>();
            public TreeItem Parent { get; set; }
        }

        /// <summary>
        /// Gets whether the stat breakdown viewer is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the stat breakdown viewer with the given explanation text.
        /// </summary>
        /// <param name="name">The name of the stat (e.g., "Visibility", "Speed")</param>
        /// <param name="explanation">The explanation text from the game</param>
        public static void Open(string name, string explanation)
        {
            if (string.IsNullOrEmpty(explanation))
            {
                TolkHelper.Speak($"No breakdown available for {name}");
                return;
            }

            isActive = true;
            statName = name;
            selectedIndex = 0;

            ParseExplanation(explanation);

            if (rootItems.Count == 0)
            {
                TolkHelper.Speak($"No breakdown factors for {name}");
                Close();
                return;
            }

            // Count top-level items for announcement
            FlattenItems();

            // Only say "treeview" if there are expandable nodes
            bool hasExpandableNodes = rootItems.Exists(item => item.IsExpandable);
            string suffix = hasExpandableNodes ? " treeview" : "";
            TolkHelper.Speak($"{name} breakdown ({rootItems.Count} factors){suffix}.");
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Closes the stat breakdown viewer.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            statName = "";
            rootItems.Clear();
            flattenedItems.Clear();
            selectedIndex = 0;
            TolkHelper.Speak("Breakdown closed");
        }

        /// <summary>
        /// Parses the explanation string into tree items.
        /// Detects indentation to create hierarchy.
        /// Result lines (starting with "= ") are attached to their parent section.
        /// </summary>
        private static void ParseExplanation(string explanation)
        {
            rootItems.Clear();

            if (string.IsNullOrEmpty(explanation))
                return;

            string[] lines = explanation.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // First pass: build the tree structure
            TreeItem currentParent = null;
            int lastIndentLevel = 0;
            List<TreeItem> allItems = new List<TreeItem>();

            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                // Count leading spaces/tabs
                int leadingSpaces = 0;
                foreach (char c in rawLine)
                {
                    if (c == ' ') leadingSpaces++;
                    else if (c == '\t') leadingSpaces += 4;
                    else break;
                }

                // Determine indent level (roughly 2 spaces = 1 level)
                int indentLevel = leadingSpaces / 2;

                // Trim the line
                string line = rawLine.Trim();

                // Skip empty lines after trim
                if (string.IsNullOrEmpty(line))
                    continue;

                // Check if this is a result line (starts with "= ")
                bool isResultLine = line.StartsWith("= ");
                if (isResultLine)
                {
                    // This is the result value for the current parent section
                    string resultValue = line.Substring(2).Trim();

                    // Find the appropriate parent to attach this result to
                    TreeItem targetParent = currentParent;
                    while (targetParent != null && targetParent.IndentLevel >= indentLevel)
                    {
                        targetParent = targetParent.Parent;
                    }

                    if (targetParent != null)
                    {
                        targetParent.ResultValue = resultValue;
                    }
                    else if (rootItems.Count > 0)
                    {
                        // Attach to the last top-level item
                        rootItems[rootItems.Count - 1].ResultValue = resultValue;
                    }
                    continue; // Don't add result lines as separate items
                }

                // Handle dash prefix for sub-items
                if (line.StartsWith("- "))
                {
                    line = line.Substring(2);
                    indentLevel = Math.Max(1, indentLevel);
                }

                // Remove trailing colon for cleaner display (section headers often end with ":")
                bool isSection = line.EndsWith(":");
                if (isSection)
                {
                    line = line.Substring(0, line.Length - 1);
                }

                var item = new TreeItem
                {
                    Text = line,
                    IndentLevel = indentLevel,
                    IsExpandable = false,
                    IsExpanded = false // Default to collapsed
                };

                allItems.Add(item);

                // Build hierarchy based on indentation
                if (indentLevel == 0)
                {
                    // Top-level item
                    rootItems.Add(item);
                    currentParent = item;
                }
                else if (currentParent == null)
                {
                    // First item starts at non-zero indent (e.g., "  - Abby: +35 kg")
                    // Treat as root item since there's no parent to attach to
                    rootItems.Add(item);
                    currentParent = item;
                }
                else if (indentLevel > lastIndentLevel)
                {
                    // Child of current parent
                    item.Parent = currentParent;
                    currentParent.Children.Add(item);
                    currentParent.IsExpandable = true;
                    currentParent = item;
                }
                else if (indentLevel <= lastIndentLevel)
                {
                    // Find appropriate parent
                    TreeItem parent = currentParent;
                    while (parent != null && parent.IndentLevel >= indentLevel)
                    {
                        parent = parent.Parent;
                    }

                    if (parent != null)
                    {
                        item.Parent = parent;
                        parent.Children.Add(item);
                        parent.IsExpandable = true;
                    }
                    else
                    {
                        // Top-level item
                        rootItems.Add(item);
                    }
                    currentParent = item;
                }

                lastIndentLevel = indentLevel;
            }

            // Second pass: inline single children into their parents
            // When a node has exactly one child, merge the child's text into the parent
            InlineSingleChildren(rootItems);

            // Third pass: extract result values from children that look like results
            foreach (var item in allItems)
            {
                if (item.IsExpandable && string.IsNullOrEmpty(item.ResultValue) && item.Children.Count > 0)
                {
                    // Look for the last child that contains a result-like pattern
                    for (int i = item.Children.Count - 1; i >= 0; i--)
                    {
                        var child = item.Children[i];
                        string childText = child.Text;

                        // Check for patterns like "= X.X tiles/day" in the text
                        var equalsMatch = Regex.Match(childText, @"=\s*([\d.]+\s*\S+.*?)$");
                        if (equalsMatch.Success)
                        {
                            item.ResultValue = equalsMatch.Groups[1].Value.Trim();
                            break;
                        }

                        // Check for "Final" or "Total" or result-like lines with colon
                        // e.g., "Caravan members speed: 25.2 tiles/day" or "Final speed: X"
                        if (childText.StartsWith("Final", StringComparison.OrdinalIgnoreCase) ||
                            childText.StartsWith("Total", StringComparison.OrdinalIgnoreCase) ||
                            childText.Contains(" speed:") ||
                            childText.Contains(" Speed:"))
                        {
                            // Extract the value after the colon
                            var colonMatch = Regex.Match(childText, @":\s*(.+)$");
                            if (colonMatch.Success)
                            {
                                item.ResultValue = colonMatch.Groups[1].Value.Trim();
                                break;
                            }
                        }
                    }
                }
            }

            // Flatten for navigation
            FlattenItems();
        }

        /// <summary>
        /// Recursively inlines single children into their parent nodes.
        /// When a node has exactly one child, the child's text is appended to the parent
        /// and any grandchildren become the parent's children.
        /// </summary>
        private static void InlineSingleChildren(List<TreeItem> items)
        {
            foreach (var item in items)
            {
                // Keep inlining as long as there's exactly one child
                while (item.Children.Count == 1)
                {
                    var singleChild = item.Children[0];

                    // Append the child's text to the parent
                    item.Text += ", " + singleChild.Text;

                    // Merge result value if the child has one and parent doesn't
                    if (!string.IsNullOrEmpty(singleChild.ResultValue) && string.IsNullOrEmpty(item.ResultValue))
                    {
                        item.ResultValue = singleChild.ResultValue;
                    }

                    // Promote grandchildren to become children
                    item.Children = singleChild.Children;
                    foreach (var grandchild in item.Children)
                    {
                        grandchild.Parent = item;
                    }

                    // Update expandability based on new children count
                    item.IsExpandable = item.Children.Count > 0;
                }

                // Recursively process remaining children
                if (item.Children.Count > 0)
                {
                    InlineSingleChildren(item.Children);
                }
            }
        }

        /// <summary>
        /// Flattens the tree into a single list for navigation.
        /// Respects expanded/collapsed state.
        /// </summary>
        private static void FlattenItems()
        {
            flattenedItems.Clear();
            foreach (var item in rootItems)
            {
                FlattenItem(item);
            }
        }

        private static void FlattenItem(TreeItem item)
        {
            flattenedItems.Add(item);
            if (item.IsExpanded && item.Children.Count > 0)
            {
                foreach (var child in item.Children)
                {
                    FlattenItem(child);
                }
            }
        }

        /// <summary>
        /// Gets the position of an item among its siblings.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(TreeItem item)
        {
            List<TreeItem> siblings;
            if (item.Parent == null)
            {
                siblings = rootItems;
            }
            else
            {
                siblings = item.Parent.Children;
            }
            int position = siblings.IndexOf(item) + 1;
            return (position, siblings.Count);
        }

        /// <summary>
        /// Announces the currently selected item.
        /// </summary>
        private static void AnnounceCurrentItem()
        {
            if (flattenedItems.Count == 0)
            {
                TolkHelper.Speak("No items");
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= flattenedItems.Count)
            {
                selectedIndex = 0;
            }

            var item = flattenedItems[selectedIndex];

            string announcement = item.Text;

            // Add result value for expandable items (sections with a calculated result)
            if (!string.IsNullOrEmpty(item.ResultValue))
            {
                announcement += $" ({item.ResultValue})";
            }

            // Add expand/collapse indicator for items with children
            if (item.IsExpandable)
            {
                announcement += item.IsExpanded ? ", expanded" : ", collapsed";
            }

            // Add sibling position (position among items at same level)
            var (position, total) = GetSiblingPosition(item);
            string positionStr = MenuHelper.FormatPosition(position - 1, total); // GetSiblingPosition returns 1-indexed
            if (!string.IsNullOrEmpty(positionStr))
            {
                announcement += $". {positionStr}";
            }

            // Add level suffix if level changed
            string levelSuffix = MenuHelper.GetLevelSuffix("Breakdown", item.IndentLevel);
            if (!string.IsNullOrEmpty(levelSuffix))
            {
                announcement += levelSuffix;
            }

            TolkHelper.Speak(announcement);
        }

        /// <summary>
        /// Selects the next item.
        /// </summary>
        public static void SelectNext()
        {
            if (flattenedItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectNext(selectedIndex, flattenedItems.Count);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Selects the previous item.
        /// </summary>
        public static void SelectPrevious()
        {
            if (flattenedItems.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, flattenedItems.Count);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Expands the current item if it's expandable.
        /// </summary>
        public static void Expand()
        {
            if (flattenedItems.Count == 0 || selectedIndex < 0 || selectedIndex >= flattenedItems.Count)
                return;

            var item = flattenedItems[selectedIndex];
            if (!item.IsExpandable)
            {
                TolkHelper.Speak("Not expandable");
                return;
            }

            if (item.IsExpanded)
            {
                TolkHelper.Speak("Already expanded");
                return;
            }

            item.IsExpanded = true;
            FlattenItems();
            TolkHelper.Speak("Expanded");
        }

        /// <summary>
        /// Collapses the current item or goes to parent.
        /// </summary>
        public static void CollapseOrGoToParent()
        {
            if (flattenedItems.Count == 0 || selectedIndex < 0 || selectedIndex >= flattenedItems.Count)
                return;

            var item = flattenedItems[selectedIndex];

            if (item.IsExpanded && item.IsExpandable)
            {
                // Collapse this item
                item.IsExpanded = false;
                FlattenItems();
                TolkHelper.Speak("Collapsed");
            }
            else if (item.Parent != null)
            {
                // Go to parent
                int parentIndex = flattenedItems.IndexOf(item.Parent);
                if (parentIndex >= 0)
                {
                    selectedIndex = parentIndex;
                    AnnounceCurrentItem();
                }
            }
            else
            {
                TolkHelper.Speak("Already at top level");
            }
        }

        /// <summary>
        /// Handles keyboard input for the stat breakdown viewer.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key)
        {
            if (!isActive)
                return false;

            switch (key)
            {
                case KeyCode.UpArrow:
                    SelectPrevious();
                    return true;

                case KeyCode.DownArrow:
                    SelectNext();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    Expand();
                    return true;

                case KeyCode.LeftArrow:
                    CollapseOrGoToParent();
                    return true;

                case KeyCode.Escape:
                case KeyCode.Tab:
                    Close();
                    return true;

                default:
                    return false;
            }
        }
    }
}
