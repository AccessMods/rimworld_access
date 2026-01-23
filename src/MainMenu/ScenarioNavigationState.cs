using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    public static class ScenarioNavigationState
    {
        private static bool initialized = false;
        private static int selectedIndex = 0;
        private static List<Scenario> flatScenarioList = new List<Scenario>();
        public static bool DetailPanelActive { get; private set; } = false;

        // Track if "Scenario Builder" virtual entry is at the end of the list
        private static bool hasScenarioBuilderEntry = true;

        /// <summary>
        /// Gets whether the currently selected item is the Scenario Builder placeholder.
        /// The builder is always the last entry (index == actual scenario count).
        /// </summary>
        public static bool IsScenarioBuilderSelected =>
            hasScenarioBuilderEntry && selectedIndex == flatScenarioList.Count;

        /// <summary>
        /// Gets the total navigation count including the Scenario Builder entry.
        /// </summary>
        private static int TotalNavigationCount =>
            flatScenarioList.Count + (hasScenarioBuilderEntry ? 1 : 0);

        // Detail panel navigation with treeview structure
        private static List<DetailItem> detailItemsHierarchy = new List<DetailItem>();
        private static List<DetailItem> flattenedDetailItems = new List<DetailItem>();
        private static int detailIndex = 0;
        private const string LevelTrackingKey = "ScenarioDetails";

        // Typeahead search for detail panel
        private static TypeaheadSearchHelper detailTypeaheadHelper = new TypeaheadSearchHelper();

        // Typeahead search for scenario list
        private static TypeaheadSearchHelper listTypeaheadHelper = new TypeaheadSearchHelper();

        /// <summary>
        /// Represents an item in the scenario detail treeview.
        /// Items can be expandable (like "Start with:") or leaf nodes.
        /// </summary>
        private class DetailItem
        {
            public string Label { get; set; }
            public int IndentLevel { get; set; }
            public bool IsExpandable { get; set; }
            public bool IsExpanded { get; set; }
            public List<DetailItem> Children { get; set; } = new List<DetailItem>();
            public DetailItem Parent { get; set; }

            public DetailItem(string label, int indentLevel = 0, bool isExpandable = false)
            {
                Label = label;
                IndentLevel = indentLevel;
                IsExpandable = isExpandable;
                IsExpanded = false;
                Parent = null;
            }
        }

        public static void Initialize(List<Scenario> scenarios)
        {
            if (!initialized || flatScenarioList.Count != scenarios.Count)
            {
                flatScenarioList = new List<Scenario>(scenarios);
                selectedIndex = 0;
                initialized = true;
            }
        }

        public static void Reset()
        {
            initialized = false;
            selectedIndex = 0;
            flatScenarioList.Clear();
            DetailPanelActive = false;
            detailItemsHierarchy.Clear();
            flattenedDetailItems.Clear();
            detailIndex = 0;
            MenuHelper.ResetLevel(LevelTrackingKey);
            listTypeaheadHelper.ClearSearch();
            detailTypeaheadHelper.ClearSearch();
        }

        public static int SelectedIndex
        {
            get { return selectedIndex; }
        }

        public static Scenario SelectedScenario
        {
            get
            {
                if (flatScenarioList.Count == 0 || selectedIndex < 0 || selectedIndex >= flatScenarioList.Count)
                    return null;
                return flatScenarioList[selectedIndex];
            }
        }

        public static int ScenarioCount
        {
            get { return flatScenarioList.Count; }
        }

        public static void NavigateUp()
        {
            if (TotalNavigationCount == 0) return;

            listTypeaheadHelper.ClearSearch();
            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, TotalNavigationCount);
            AnnounceCurrentSelection();
        }

        public static void NavigateDown()
        {
            if (TotalNavigationCount == 0) return;

            listTypeaheadHelper.ClearSearch();
            selectedIndex = MenuHelper.SelectNext(selectedIndex, TotalNavigationCount);
            AnnounceCurrentSelection();
        }

        public static void NavigateHome()
        {
            if (TotalNavigationCount == 0) return;

            listTypeaheadHelper.ClearSearch();
            selectedIndex = 0;
            AnnounceCurrentSelection();
        }

        public static void NavigateEnd()
        {
            if (TotalNavigationCount == 0) return;

            listTypeaheadHelper.ClearSearch();
            selectedIndex = TotalNavigationCount - 1;
            AnnounceCurrentSelection();
        }

        // Scenario list typeahead support
        public static bool ListHasActiveSearch => listTypeaheadHelper.HasActiveSearch;

        public static bool HandleListTypeahead(char character)
        {
            if (flatScenarioList.Count == 0)
                return false;

            // Create labels list including "Scenario Builder" for search
            var labels = flatScenarioList.Select(s => s.name).ToList();
            if (hasScenarioBuilderEntry)
            {
                labels.Add("Scenario Builder");
            }

            if (listTypeaheadHelper.ProcessCharacterInput(character, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedIndex = newIndex;
                    AnnounceWithListSearch();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{listTypeaheadHelper.LastFailedSearch}'");
            }

            return true;
        }

        public static bool HandleListTypeaheadBackspace()
        {
            if (!listTypeaheadHelper.HasActiveSearch)
                return false;

            // Create labels list including "Scenario Builder" for search
            var labels = flatScenarioList.Select(s => s.name).ToList();
            if (hasScenarioBuilderEntry)
            {
                labels.Add("Scenario Builder");
            }

            if (listTypeaheadHelper.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedIndex = newIndex;
                    AnnounceWithListSearch();
                }
            }

            return true;
        }

        public static bool ClearListTypeaheadSearch()
        {
            if (listTypeaheadHelper.ClearSearchAndAnnounce())
            {
                AnnounceCurrentSelection();
                return true;
            }
            return false;
        }

        public static bool SelectNextListMatch()
        {
            if (!listTypeaheadHelper.HasActiveSearch)
                return false;

            int next = listTypeaheadHelper.GetNextMatch(selectedIndex);
            if (next >= 0)
            {
                selectedIndex = next;
                AnnounceWithListSearch();
            }
            return true;
        }

        public static bool SelectPreviousListMatch()
        {
            if (!listTypeaheadHelper.HasActiveSearch)
                return false;

            int prev = listTypeaheadHelper.GetPreviousMatch(selectedIndex);
            if (prev >= 0)
            {
                selectedIndex = prev;
                AnnounceWithListSearch();
            }
            return true;
        }

        private static void AnnounceWithListSearch()
        {
            // Scenario Builder doesn't participate in search
            if (IsScenarioBuilderSelected)
            {
                AnnounceScenarioBuilder();
                return;
            }

            Scenario selected = SelectedScenario;
            if (selected == null) return;

            string categorySuffix = GetCategorySuffix(selected);

            if (listTypeaheadHelper.HasActiveSearch)
            {
                TolkHelper.Speak($"{selected.name}{categorySuffix}, {listTypeaheadHelper.CurrentMatchPosition} of {listTypeaheadHelper.MatchCount} matches for '{listTypeaheadHelper.SearchBuffer}'");
            }
            else
            {
                AnnounceCurrentScenario();
            }
        }

        /// <summary>
        /// Announces the current selection (scenario or builder entry).
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            if (IsScenarioBuilderSelected)
            {
                AnnounceScenarioBuilder();
            }
            else
            {
                AnnounceCurrentScenario();
            }
        }

        /// <summary>
        /// Announces the Scenario Builder entry.
        /// </summary>
        private static void AnnounceScenarioBuilder()
        {
            string positionPart = MenuHelper.FormatPosition(selectedIndex, TotalNavigationCount);
            string text = "Scenario Builder - Create a custom scenario from scratch";

            if (!string.IsNullOrEmpty(positionPart))
            {
                text += $" ({positionPart})";
            }

            TolkHelper.Speak(text);
        }

        private static void AnnounceCurrentScenario()
        {
            Scenario selected = SelectedScenario;
            if (selected == null) return;

            string categorySuffix = GetCategorySuffix(selected);
            string positionPart = MenuHelper.FormatPosition(selectedIndex, TotalNavigationCount);

            string text = $"{selected.name} - {selected.summary}{categorySuffix}";

            // Append warning for invalid scenarios
            if (!selected.valid)
            {
                text += $". Warning: {"ScenPart_Error".Translate()}";
            }

            // Add position if enabled
            if (!string.IsNullOrEmpty(positionPart))
            {
                text += $" ({positionPart})";
            }

            TolkHelper.Speak(text);
        }

        // Legacy method name for compatibility
        private static void CopySelectedToClipboard()
        {
            AnnounceCurrentSelection();
        }

        private static string GetCategorySuffix(Scenario scenario)
        {
            switch (scenario.Category)
            {
                case ScenarioCategory.FromDef:
                    return " (Built-in)";
                case ScenarioCategory.CustomLocal:
                    return " (Custom)";
                case ScenarioCategory.SteamWorkshop:
                    return " (Workshop)";
                default:
                    return "";
            }
        }

        public static Scenario GetScenarioAtIndex(int index)
        {
            if (index < 0 || index >= flatScenarioList.Count)
                return null;
            return flatScenarioList[index];
        }

        public static void ToggleDetailPanel()
        {
            // Don't open detail panel for Scenario Builder entry
            if (IsScenarioBuilderSelected && !DetailPanelActive)
            {
                TolkHelper.Speak("No details for Scenario Builder. Press Enter to open.");
                return;
            }

            DetailPanelActive = !DetailPanelActive;
            if (DetailPanelActive)
            {
                MenuHelper.ResetLevel(LevelTrackingKey);
                detailTypeaheadHelper.ClearSearch();
                BuildDetailItems();
                FlattenDetailItems();
                detailIndex = 0;
                TolkHelper.Speak("Details");
                AnnounceCurrentDetailItem();
            }
            else
            {
                detailItemsHierarchy.Clear();
                flattenedDetailItems.Clear();
                detailIndex = 0;
                detailTypeaheadHelper.ClearSearch();
                MenuHelper.ResetLevel(LevelTrackingKey);
                TolkHelper.Speak("Scenario list");
                CopySelectedToClipboard();
            }
        }

        /// <summary>
        /// Builds the detail items from the scenario's parts.
        /// Processes each ScenPart directly rather than parsing concatenated text.
        /// </summary>
        private static void BuildDetailItems()
        {
            detailItemsHierarchy.Clear();
            Scenario selected = SelectedScenario;
            if (selected == null) return;

            var addedContent = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            // 1. Add description, extracting any "Note:" portion to add separately
            string noteFromDescription = null;
            if (!string.IsNullOrWhiteSpace(selected.description))
            {
                string desc = selected.description.Trim();

                // Check for "Note:" in description (often on its own line)
                int noteIndex = desc.IndexOf("\nNote:", System.StringComparison.OrdinalIgnoreCase);
                if (noteIndex < 0)
                    noteIndex = desc.IndexOf("\n\nNote:", System.StringComparison.OrdinalIgnoreCase);

                if (noteIndex >= 0)
                {
                    string mainDesc = desc.Substring(0, noteIndex).Trim();
                    noteFromDescription = desc.Substring(noteIndex).Trim();

                    detailItemsHierarchy.Add(new DetailItem($"Description: {mainDesc}", 0, false));
                    addedContent.Add(mainDesc);

                    // Add note as separate item
                    detailItemsHierarchy.Add(new DetailItem(noteFromDescription, 0, false));
                    addedContent.Add(noteFromDescription);
                }
                else
                {
                    detailItemsHierarchy.Add(new DetailItem($"Description: {desc}", 0, false));
                    addedContent.Add(desc);
                }
            }

            // 2. Collect items by processing each ScenPart directly
            var startWithItems = new List<string>();      // From GetSummaryListEntries("PlayerStartsWith")
            var mapScatteredItems = new List<string>();   // From GetSummaryListEntries("MapScatteredWith")
            string pawnCountLine = null;                   // From ConfigureStartingPawns.Summary()
            var researchLines = new List<string>();        // From StartingResearch.Summary()
            var otherSummaries = new List<string>();       // Everything else

            foreach (ScenPart part in selected.AllParts)
            {
                if (!part.visible)
                    continue;

                // Collect list entries directly (not from parsed text)
                foreach (string entry in part.GetSummaryListEntries("PlayerStartsWith"))
                {
                    if (!string.IsNullOrWhiteSpace(entry))
                        startWithItems.Add(entry);
                }

                foreach (string entry in part.GetSummaryListEntries("MapScatteredWith"))
                {
                    if (!string.IsNullOrWhiteSpace(entry))
                        mapScatteredItems.Add(entry);
                }

                // Categorize part by type name
                string partTypeName = part.GetType().Name;

                // Skip parts that contribute to lists - we use GetSummaryListEntries instead
                if (partTypeName.Contains("StartingThing") ||
                    partTypeName.Contains("StartingAnimal") ||
                    partTypeName.Contains("StartingMech") ||
                    partTypeName.Contains("ScatterThings"))
                {
                    continue;
                }

                // Get the part's summary
                string summary = part.Summary(selected);
                if (string.IsNullOrWhiteSpace(summary))
                    continue;

                // Categorize by part type
                if (partTypeName.Contains("ConfigureStartingPawns"))
                {
                    // "Start with X people" - goes in treeview
                    pawnCountLine = summary.Trim();
                }
                else if (partTypeName.Contains("StartingResearch"))
                {
                    // "Start with research: X" - goes in treeview
                    researchLines.Add(summary.Trim());
                }
                else
                {
                    // Other parts (faction, notes, conditions, etc.)
                    // Split by newlines and add each non-duplicate line
                    foreach (string line in summary.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed))
                            continue;

                        // Skip if already added (handles duplicate notes)
                        if (addedContent.Contains(trimmed))
                            continue;

                        // Skip list items (they start with "   -")
                        if (trimmed.StartsWith("-") || line.StartsWith("   -"))
                            continue;

                        // Skip section headers (we build our own treeviews)
                        if (IsListHeader(trimmed))
                            continue;

                        otherSummaries.Add(trimmed);
                        addedContent.Add(trimmed);
                    }
                }
            }

            // 3. Add other summaries (faction, notes, etc.) as root items
            foreach (string line in otherSummaries)
            {
                detailItemsHierarchy.Add(new DetailItem(line, 0, false));
            }

            // 4. Build "Start with" treeview if we have any content
            bool hasStartWithContent = pawnCountLine != null ||
                                       startWithItems.Count > 0 ||
                                       researchLines.Count > 0;
            if (hasStartWithContent)
            {
                var startWithSection = new DetailItem("Start with", 0, true);

                // Add pawn count first (e.g., "Start with 3 people")
                if (pawnCountLine != null)
                {
                    var item = new DetailItem(pawnCountLine, 1, false);
                    item.Parent = startWithSection;
                    startWithSection.Children.Add(item);
                }

                // Add starting items
                foreach (string label in startWithItems)
                {
                    var item = new DetailItem(label, 1, false);
                    item.Parent = startWithSection;
                    startWithSection.Children.Add(item);
                }

                // Add starting research
                foreach (string research in researchLines)
                {
                    var item = new DetailItem(research, 1, false);
                    item.Parent = startWithSection;
                    startWithSection.Children.Add(item);
                }

                detailItemsHierarchy.Add(startWithSection);
            }

            // 5. Build "Map is scattered with" treeview
            if (mapScatteredItems.Count > 0)
            {
                var mapScatteredSection = new DetailItem("Map is scattered with", 0, true);
                foreach (string label in mapScatteredItems)
                {
                    var item = new DetailItem(label, 1, false);
                    item.Parent = mapScatteredSection;
                    mapScatteredSection.Children.Add(item);
                }
                detailItemsHierarchy.Add(mapScatteredSection);
            }

            // Fallback if nothing was added
            if (detailItemsHierarchy.Count == 0)
            {
                detailItemsHierarchy.Add(new DetailItem("No additional details available", 0, false));
            }
        }

        /// <summary>
        /// Checks if a line is a list header like "Start with:" or "Map is scattered with:".
        /// These headers are skipped because we build our own treeviews.
        /// </summary>
        private static bool IsListHeader(string line)
        {
            if (!line.EndsWith(":"))
                return false;

            string lineLower = line.ToLowerInvariant();
            return lineLower.StartsWith("start with") ||
                   lineLower.StartsWith("map is scattered");
        }

        /// <summary>
        /// Flattens the hierarchical detail items into a list for navigation.
        /// Only includes children of expanded nodes.
        /// </summary>
        private static void FlattenDetailItems()
        {
            flattenedDetailItems.Clear();

            foreach (var item in detailItemsHierarchy)
            {
                flattenedDetailItems.Add(item);

                // If this item is expanded, add its children
                if (item.IsExpandable && item.IsExpanded)
                {
                    foreach (var child in item.Children)
                    {
                        flattenedDetailItems.Add(child);
                    }
                }
            }
        }

        public static void NavigateDetailUp()
        {
            if (flattenedDetailItems.Count == 0) return;

            detailIndex = MenuHelper.SelectPrevious(detailIndex, flattenedDetailItems.Count);
            AnnounceCurrentDetailItem();
        }

        public static void NavigateDetailDown()
        {
            if (flattenedDetailItems.Count == 0) return;

            detailIndex = MenuHelper.SelectNext(detailIndex, flattenedDetailItems.Count);
            AnnounceCurrentDetailItem();
        }

        /// <summary>
        /// Expands the current item if it's expandable and collapsed,
        /// or moves to the first child if already expanded.
        /// </summary>
        public static void ExpandOrDrillDown()
        {
            if (flattenedDetailItems.Count == 0 || detailIndex >= flattenedDetailItems.Count)
                return;

            detailTypeaheadHelper.ClearSearch();
            DetailItem item = flattenedDetailItems[detailIndex];

            if (item.IsExpandable)
            {
                if (!item.IsExpanded)
                {
                    // Expand the item
                    item.IsExpanded = true;
                    FlattenDetailItems();
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    AnnounceCurrentDetailItem();
                }
                else
                {
                    // Already expanded - move to first child
                    if (item.Children.Count > 0)
                    {
                        // Find the first child in the flattened list
                        for (int i = detailIndex + 1; i < flattenedDetailItems.Count; i++)
                        {
                            if (flattenedDetailItems[i].Parent == item)
                            {
                                detailIndex = i;
                                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                                AnnounceCurrentDetailItem();
                                return;
                            }
                        }
                    }
                    // No children found
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
            }
            else
            {
                // Not expandable - end node
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// Collapses the current item if it's expanded,
        /// or moves to the parent if on a child item.
        /// </summary>
        public static void CollapseOrDrillUp()
        {
            if (flattenedDetailItems.Count == 0 || detailIndex >= flattenedDetailItems.Count)
                return;

            detailTypeaheadHelper.ClearSearch();
            DetailItem item = flattenedDetailItems[detailIndex];

            if (item.IsExpandable && item.IsExpanded)
            {
                // Collapse the item
                item.IsExpanded = false;
                FlattenDetailItems();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceCurrentDetailItem();
            }
            else if (item.Parent != null)
            {
                // Move to parent
                for (int i = 0; i < flattenedDetailItems.Count; i++)
                {
                    if (flattenedDetailItems[i] == item.Parent)
                    {
                        detailIndex = i;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        AnnounceCurrentDetailItem();
                        return;
                    }
                }
                // Parent not found - shouldn't happen
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
            else
            {
                // At root level and not expanded - reject
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// Handles Home key navigation.
        /// Jumps to first sibling at current level, or absolute first with Ctrl.
        /// Uses MenuHelper.HandleTreeHomeKey for consistency with other treeviews.
        /// </summary>
        public static void HandleHomeKey(bool ctrlPressed)
        {
            if (flattenedDetailItems.Count == 0) return;

            MenuHelper.HandleTreeHomeKey(
                flattenedDetailItems,
                ref detailIndex,
                item => item.IndentLevel,
                ctrlPressed,
                AnnounceCurrentDetailItem);
        }

        /// <summary>
        /// Handles End key navigation.
        /// For expanded nodes with children: jumps to last visible descendant.
        /// For collapsed/leaf nodes: jumps to last sibling at current level.
        /// Ctrl+End jumps to absolute last.
        /// Uses MenuHelper.HandleTreeEndKey for consistency with other treeviews.
        /// </summary>
        public static void HandleEndKey(bool ctrlPressed)
        {
            if (flattenedDetailItems.Count == 0) return;

            MenuHelper.HandleTreeEndKey(
                flattenedDetailItems,
                ref detailIndex,
                item => item.IndentLevel,
                item => item.IsExpanded,
                item => item.IsExpandable && item.Children.Count > 0,
                ctrlPressed,
                AnnounceCurrentDetailItem);
        }

        // Typeahead search support
        public static bool HasActiveSearch => detailTypeaheadHelper.HasActiveSearch;
        public static bool HasNoMatches => detailTypeaheadHelper.HasNoMatches;

        /// <summary>
        /// Processes a character input for typeahead search in the detail panel.
        /// </summary>
        /// <param name="character">The character typed</param>
        /// <returns>True if input was processed</returns>
        public static bool HandleTypeahead(char character)
        {
            if (flattenedDetailItems.Count == 0)
                return false;

            // Build list of labels from flattened items
            var labels = flattenedDetailItems.Select(item => item.Label).ToList();

            if (detailTypeaheadHelper.ProcessCharacterInput(character, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    detailIndex = newIndex;
                    AnnounceWithSearch();
                }
            }
            else
            {
                // No matches - announce using LastFailedSearch (search was auto-cleared)
                TolkHelper.Speak($"No matches for '{detailTypeaheadHelper.LastFailedSearch}'");
            }

            return true;
        }

        /// <summary>
        /// Handles backspace for typeahead search.
        /// </summary>
        /// <returns>True if backspace was handled (active search existed)</returns>
        public static bool HandleTypeaheadBackspace()
        {
            if (!detailTypeaheadHelper.HasActiveSearch)
                return false;

            var labels = flattenedDetailItems.Select(item => item.Label).ToList();

            if (detailTypeaheadHelper.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    detailIndex = newIndex;
                    AnnounceWithSearch();
                }
                return true;
            }

            return true;
        }

        /// <summary>
        /// Clears the typeahead search and announces.
        /// </summary>
        /// <returns>True if there was an active search to clear</returns>
        public static bool ClearTypeaheadSearch()
        {
            if (detailTypeaheadHelper.ClearSearchAndAnnounce())
            {
                AnnounceCurrentDetailItem();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Moves to the next match in the current search.
        /// </summary>
        /// <returns>True if there was an active search</returns>
        public static bool SelectNextMatch()
        {
            if (!detailTypeaheadHelper.HasActiveSearch)
                return false;

            int next = detailTypeaheadHelper.GetNextMatch(detailIndex);
            if (next >= 0)
            {
                detailIndex = next;
                AnnounceWithSearch();
            }
            return true;
        }

        /// <summary>
        /// Moves to the previous match in the current search.
        /// </summary>
        /// <returns>True if there was an active search</returns>
        public static bool SelectPreviousMatch()
        {
            if (!detailTypeaheadHelper.HasActiveSearch)
                return false;

            int prev = detailTypeaheadHelper.GetPreviousMatch(detailIndex);
            if (prev >= 0)
            {
                detailIndex = prev;
                AnnounceWithSearch();
            }
            return true;
        }

        /// <summary>
        /// Announces the current item with search match information.
        /// </summary>
        private static void AnnounceWithSearch()
        {
            if (detailIndex < 0 || detailIndex >= flattenedDetailItems.Count)
                return;

            string label = flattenedDetailItems[detailIndex].Label;

            if (detailTypeaheadHelper.HasActiveSearch)
            {
                TolkHelper.Speak($"{label}, {detailTypeaheadHelper.CurrentMatchPosition} of {detailTypeaheadHelper.MatchCount} matches for '{detailTypeaheadHelper.SearchBuffer}'");
            }
            else
            {
                AnnounceCurrentDetailItem();
            }
        }

        /// <summary>
        /// Gets the sibling position (1-indexed) within the same level.
        /// For children, counts siblings under the same parent.
        /// For root items, counts all root items.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(DetailItem item)
        {
            int position = 0;
            int total = 0;

            if (item.Parent != null)
            {
                // Count siblings among parent's children
                foreach (var sibling in item.Parent.Children)
                {
                    total++;
                    if (sibling == item)
                        position = total;
                }
            }
            else
            {
                // Count root-level siblings in hierarchy
                foreach (var rootItem in detailItemsHierarchy)
                {
                    total++;
                    if (rootItem == item)
                        position = total;
                }
            }

            return (position, total);
        }

        private static void AnnounceCurrentDetailItem()
        {
            if (detailIndex < 0 || detailIndex >= flattenedDetailItems.Count) return;

            DetailItem item = flattenedDetailItems[detailIndex];
            var (position, total) = GetSiblingPosition(item);
            string positionPart = MenuHelper.FormatPosition(position - 1, total);

            string announcement = "";

            if (item.IsExpandable)
            {
                // Expandable items: "Label, collapsed/expanded, N items. position."
                string expandState = item.IsExpanded ? "expanded" : "collapsed";
                int childCount = item.Children.Count;
                string itemCountStr = childCount == 1 ? "1 item" : $"{childCount} items";
                string positionSection = string.IsNullOrEmpty(positionPart) ? "" : $" ({positionPart})";

                announcement = $"{item.Label}, {expandState}, {itemCountStr}.{positionSection}";
            }
            else
            {
                // Regular items: "Label. position."
                string labelText = item.Label;
                bool labelEndsWithPunctuation = !string.IsNullOrEmpty(labelText) &&
                    (labelText.EndsWith(".") || labelText.EndsWith("!") || labelText.EndsWith("?"));

                string positionSection;
                if (string.IsNullOrEmpty(positionPart))
                {
                    positionSection = labelEndsWithPunctuation ? "" : ".";
                }
                else
                {
                    positionSection = labelEndsWithPunctuation ? $" ({positionPart})" : $". ({positionPart})";
                }

                announcement = $"{item.Label}{positionSection}";
            }

            // Add level suffix at the end (only announced when level changes)
            announcement += MenuHelper.GetLevelSuffix(LevelTrackingKey, item.IndentLevel);

            TolkHelper.Speak(announcement);
        }
    }
}
