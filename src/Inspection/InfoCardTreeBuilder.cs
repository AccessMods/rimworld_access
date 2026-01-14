using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Builds InspectionTreeItem trees from Dialog_InfoCard data.
    /// Supports all tabs: Stats, Character, Health, Records, Permits.
    /// </summary>
    public static class InfoCardTreeBuilder
    {
        /// <summary>
        /// Builds the complete tree for an info card, with all available tabs.
        /// </summary>
        public static InspectionTreeItem BuildTree(Dialog_InfoCard dialog)
        {
            var root = new InspectionTreeItem
            {
                Type = InspectionTreeItem.ItemType.Object,
                Label = GetRootLabel(dialog),
                IsExpandable = true,
                IsExpanded = true,
                IndentLevel = -1
            };

            var availableTabs = InfoCardDataExtractor.GetAvailableTabs(dialog);

            // If only one tab, skip the tab level and build contents directly under root
            if (availableTabs.Count == 1)
            {
                BuildTabChildren(root, dialog, availableTabs[0]);
                return root;
            }

            // Multiple tabs - create tab nodes
            foreach (var tab in availableTabs)
            {
                var tabNode = CreateTabNode(dialog, tab);
                AddChild(root, tabNode);
            }

            return root;
        }

        /// <summary>
        /// Gets the root label for the info card based on what's being displayed.
        /// </summary>
        private static string GetRootLabel(Dialog_InfoCard dialog)
        {
            string infoCardLabel = ConceptDefOf.InfoCard.label.CapitalizeFirst();

            var thing = InfoCardDataExtractor.GetThing(dialog);
            if (thing != null)
            {
                return $"{infoCardLabel}: {thing.LabelCapNoCount}";
            }

            var def = InfoCardDataExtractor.GetDef(dialog);
            if (def != null)
            {
                return $"{infoCardLabel}: {def.LabelCap}";
            }

            return infoCardLabel;
        }

        /// <summary>
        /// Creates a tab node with lazy-loaded children.
        /// </summary>
        private static InspectionTreeItem CreateTabNode(Dialog_InfoCard dialog, Dialog_InfoCard.InfoCardTab tab)
        {
            string tabLabel = GetTabLabel(tab);

            var tabNode = new InspectionTreeItem
            {
                Type = InspectionTreeItem.ItemType.Category,
                Label = tabLabel,
                Data = tab,
                IsExpandable = true,
                IsExpanded = false,
                IndentLevel = 0
            };

            // Lazy load children when expanded, and sync visual tab
            tabNode.OnActivate = () =>
            {
                dialog.SetTab(tab);
                BuildTabChildren(tabNode, dialog, tab);
            };

            return tabNode;
        }

        /// <summary>
        /// Gets the display label for a tab.
        /// </summary>
        private static string GetTabLabel(Dialog_InfoCard.InfoCardTab tab)
        {
            switch (tab)
            {
                case Dialog_InfoCard.InfoCardTab.Stats: return "TabStats".Translate();
                case Dialog_InfoCard.InfoCardTab.Character: return "TabCharacter".Translate();
                case Dialog_InfoCard.InfoCardTab.Health: return "TabHealth".Translate();
                case Dialog_InfoCard.InfoCardTab.Records: return "TabRecords".Translate();
                case Dialog_InfoCard.InfoCardTab.Permits: return "TabPermits".Translate();
                default: return tab.ToString();
            }
        }

        /// <summary>
        /// Builds children for a specific tab.
        /// </summary>
        private static void BuildTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog, Dialog_InfoCard.InfoCardTab tab)
        {
            if (tabNode.Children.Count > 0)
                return; // Already built

            switch (tab)
            {
                case Dialog_InfoCard.InfoCardTab.Stats:
                    BuildStatsTabChildren(tabNode, dialog);
                    break;
                case Dialog_InfoCard.InfoCardTab.Character:
                    BuildCharacterTabChildren(tabNode, dialog);
                    break;
                case Dialog_InfoCard.InfoCardTab.Health:
                    BuildHealthTabChildren(tabNode, dialog);
                    break;
                case Dialog_InfoCard.InfoCardTab.Records:
                    BuildRecordsTabChildren(tabNode, dialog);
                    break;
                case Dialog_InfoCard.InfoCardTab.Permits:
                    BuildPermitsTabChildren(tabNode, dialog);
                    break;
            }
        }

        #region Stats Tab

        private static void BuildStatsTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog)
        {
            var entries = InfoCardDataExtractor.GetStatEntries();
            if (entries.Count == 0)
            {
                AddChild(tabNode, CreateInfoItem("No stats available", tabNode.IndentLevel + 1));
                return;
            }

            // Group by category label (not object) to avoid duplicate headers for same-named categories
            var grouped = entries
                .GroupBy(e => e.category.LabelCap.ToString())
                .Select(g => new { Label = g.Key, Entries = g.ToList(), DisplayOrder = g.First().category.displayOrder })
                .OrderBy(g => g.DisplayOrder);

            foreach (var group in grouped)
            {
                // Add category header (non-expandable)
                AddChild(tabNode, CreateCategoryHeader(group.Label, tabNode.IndentLevel + 1));

                // Add stat items under this category
                var sortedEntries = group.Entries.OrderByDescending(e => e.DisplayPriorityWithinCategory);
                foreach (var entry in sortedEntries)
                {
                    string label = $"{entry.LabelCap}: {entry.ValueString}";

                    var statNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = label,
                        Data = entry,
                        IsExpandable = true,
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    statNode.OnActivate = () => BuildStatDetailChildren(statNode, entry);

                    AddChild(tabNode, statNode);
                }
            }
        }

        private static void BuildStatDetailChildren(InspectionTreeItem statNode, StatDrawEntry entry)
        {
            if (statNode.Children.Count > 0)
                return;

            try
            {
                string explanation = entry.GetExplanationText(StatRequest.ForEmpty());
                if (!string.IsNullOrEmpty(explanation))
                {
                    explanation = explanation.StripTags();
                    var lines = explanation.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            AddChild(statNode, CreateInfoItem(trimmedLine, statNode.IndentLevel + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[InfoCardTreeBuilder] Error building stat details: {ex.Message}");
                AddChild(statNode, CreateInfoItem("Unable to load details", statNode.IndentLevel + 1));
            }
        }

        #endregion

        #region Character Tab

        private static void BuildCharacterTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog)
        {
            var pawn = InfoCardDataExtractor.GetPawn(dialog);
            if (pawn == null)
            {
                AddChild(tabNode, CreateInfoItem("No character data available", tabNode.IndentLevel + 1));
                return;
            }

            // Backstory
            var backstoryInfo = InfoCardDataExtractor.GetBackstoryInfo(pawn);
            if (backstoryInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Backstory", tabNode.IndentLevel + 1));
                foreach (var (title, description) in backstoryInfo)
                {
                    var storyNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = title,
                        IsExpandable = !string.IsNullOrEmpty(description),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        storyNode.OnActivate = () =>
                        {
                            if (storyNode.Children.Count > 0) return;
                            var lines = description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                AddChild(storyNode, CreateInfoItem(line.Trim(), storyNode.IndentLevel + 1));
                            }
                        };
                    }
                    AddChild(tabNode, storyNode);
                }
            }

            // Traits
            var traitsInfo = InfoCardDataExtractor.GetTraitsInfo(pawn);
            if (traitsInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Traits", tabNode.IndentLevel + 1));
                foreach (var (label, description, suppressed) in traitsInfo)
                {
                    string displayLabel = suppressed ? $"{label} (Suppressed)" : label;

                    var traitNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = displayLabel,
                        IsExpandable = !string.IsNullOrEmpty(description),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        traitNode.OnActivate = () =>
                        {
                            if (traitNode.Children.Count > 0) return;
                            var lines = description.StripTags().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                AddChild(traitNode, CreateInfoItem(line.Trim(), traitNode.IndentLevel + 1));
                            }
                        };
                    }
                    AddChild(tabNode, traitNode);
                }
            }

            // Skills
            var skillsInfo = InfoCardDataExtractor.GetSkillsInfo(pawn);
            if (skillsInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Skills", tabNode.IndentLevel + 1));
                foreach (var (def, level, passion, disabled, levelDesc) in skillsInfo)
                {
                    string passionStr = "";
                    if (passion == Passion.Minor)
                        passionStr = " *";
                    else if (passion == Passion.Major)
                        passionStr = " **";

                    string label = disabled
                        ? $"{def.skillLabel.CapitalizeFirst()}: Disabled"
                        : $"{def.skillLabel.CapitalizeFirst()}: {level}{passionStr} ({levelDesc})";

                    AddChild(tabNode, CreateInfoItem(label, tabNode.IndentLevel + 1));
                }
            }

            // Incapable Of
            var incapableInfo = InfoCardDataExtractor.GetIncapableWorkTypes(pawn);
            if (incapableInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Incapable Of", tabNode.IndentLevel + 1));
                foreach (var workType in incapableInfo)
                {
                    AddChild(tabNode, CreateInfoItem(workType, tabNode.IndentLevel + 1));
                }
            }

            // Royal Titles
            var titlesInfo = InfoCardDataExtractor.GetRoyalTitlesInfo(pawn);
            if (titlesInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Royal Titles", tabNode.IndentLevel + 1));
                foreach (var (title, faction, description) in titlesInfo)
                {
                    var titleNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = $"{title} ({faction})",
                        IsExpandable = !string.IsNullOrEmpty(description),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        titleNode.OnActivate = () =>
                        {
                            if (titleNode.Children.Count > 0) return;
                            AddChild(titleNode, CreateInfoItem(description.StripTags(), titleNode.IndentLevel + 1));
                        };
                    }
                    AddChild(tabNode, titleNode);
                }
            }

            // Ideology Role
            var roleInfo = InfoCardDataExtractor.GetIdeologyRoleInfo(pawn);
            if (roleInfo.HasValue)
            {
                AddChild(tabNode, CreateCategoryHeader("Ideology Role", tabNode.IndentLevel + 1));
                AddChild(tabNode, CreateInfoItem($"{roleInfo.Value.roleName} ({roleInfo.Value.ideoName})", tabNode.IndentLevel + 1));
            }

            // Abilities
            var abilitiesInfo = InfoCardDataExtractor.GetAbilitiesInfo(pawn);
            if (abilitiesInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Abilities", tabNode.IndentLevel + 1));
                foreach (var (label, description) in abilitiesInfo)
                {
                    var abilityNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = label,
                        IsExpandable = !string.IsNullOrEmpty(description),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        abilityNode.OnActivate = () =>
                        {
                            if (abilityNode.Children.Count > 0) return;
                            AddChild(abilityNode, CreateInfoItem(description.StripTags(), abilityNode.IndentLevel + 1));
                        };
                    }
                    AddChild(tabNode, abilityNode);
                }
            }

            // Xenotype
            var xenotypeInfo = InfoCardDataExtractor.GetXenotypeInfo(pawn);
            if (xenotypeInfo.HasValue)
            {
                AddChild(tabNode, CreateCategoryHeader("Xenotype", tabNode.IndentLevel + 1));
                var xenoNode = new InspectionTreeItem
                {
                    Type = InspectionTreeItem.ItemType.Item,
                    Label = xenotypeInfo.Value.xenotypeName,
                    IsExpandable = xenotypeInfo.Value.genes.Count > 0 || !string.IsNullOrEmpty(xenotypeInfo.Value.description),
                    IsExpanded = false,
                    IndentLevel = tabNode.IndentLevel + 1
                };

                xenoNode.OnActivate = () =>
                {
                    if (xenoNode.Children.Count > 0) return;
                    if (!string.IsNullOrEmpty(xenotypeInfo.Value.description))
                    {
                        AddChild(xenoNode, CreateInfoItem(xenotypeInfo.Value.description.StripTags(), xenoNode.IndentLevel + 1));
                    }
                    foreach (var gene in xenotypeInfo.Value.genes)
                    {
                        AddChild(xenoNode, CreateInfoItem($"Gene: {gene}", xenoNode.IndentLevel + 1));
                    }
                };
                AddChild(tabNode, xenoNode);
            }
        }

        #endregion

        #region Health Tab

        private static void BuildHealthTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog)
        {
            var pawn = InfoCardDataExtractor.GetPawn(dialog);
            if (pawn == null)
            {
                AddChild(tabNode, CreateInfoItem("No health data available", tabNode.IndentLevel + 1));
                return;
            }

            // Capacities
            var capacitiesInfo = InfoCardDataExtractor.GetCapacitiesInfo(pawn);
            if (capacitiesInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Capacities", tabNode.IndentLevel + 1));
                foreach (var (label, efficiency, tip) in capacitiesInfo)
                {
                    string efficiencyStr = efficiency.ToStringPercent();
                    string displayLabel = $"{label}: {efficiencyStr}";

                    var capacityNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = displayLabel,
                        IsExpandable = !string.IsNullOrEmpty(tip),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(tip))
                    {
                        capacityNode.OnActivate = () =>
                        {
                            if (capacityNode.Children.Count > 0) return;
                            AddChild(capacityNode, CreateInfoItem(tip.StripTags(), capacityNode.IndentLevel + 1));
                        };
                    }
                    AddChild(tabNode, capacityNode);
                }
            }

            // Conditions (hediffs)
            var hediffsInfo = InfoCardDataExtractor.GetHediffsInfo(pawn);
            if (hediffsInfo.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Conditions", tabNode.IndentLevel + 1));
                foreach (var (label, partLabel, severity, tip) in hediffsInfo)
                {
                    string displayLabel = string.IsNullOrEmpty(severity)
                        ? $"{partLabel}: {label}"
                        : $"{partLabel}: {label}: {severity}";

                    var hediffNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = displayLabel,
                        IsExpandable = !string.IsNullOrEmpty(tip),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(tip))
                    {
                        hediffNode.OnActivate = () =>
                        {
                            if (hediffNode.Children.Count > 0) return;
                            var lines = tip.StripTags().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                AddChild(hediffNode, CreateInfoItem(line.Trim(), hediffNode.IndentLevel + 1));
                            }
                        };
                    }
                    AddChild(tabNode, hediffNode);
                }
            }
            else
            {
                AddChild(tabNode, CreateInfoItem("No health conditions", tabNode.IndentLevel + 1));
            }
        }

        #endregion

        #region Records Tab

        private static void BuildRecordsTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog)
        {
            var pawn = InfoCardDataExtractor.GetPawn(dialog);
            if (pawn == null)
            {
                AddChild(tabNode, CreateInfoItem("No records available", tabNode.IndentLevel + 1));
                return;
            }

            // Time records
            var timeRecords = InfoCardDataExtractor.GetTimeRecords(pawn);
            if (timeRecords.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Time Records", tabNode.IndentLevel + 1));
                foreach (var (label, value) in timeRecords)
                {
                    AddChild(tabNode, CreateInfoItem($"{label}: {value}", tabNode.IndentLevel + 1));
                }
            }

            // Misc records
            var miscRecords = InfoCardDataExtractor.GetMiscRecords(pawn);
            if (miscRecords.Count > 0)
            {
                AddChild(tabNode, CreateCategoryHeader("Miscellaneous", tabNode.IndentLevel + 1));
                foreach (var (label, value) in miscRecords)
                {
                    AddChild(tabNode, CreateInfoItem($"{label}: {value}", tabNode.IndentLevel + 1));
                }
            }

            if (timeRecords.Count == 0 && miscRecords.Count == 0)
            {
                AddChild(tabNode, CreateInfoItem("No records yet", tabNode.IndentLevel + 1));
            }
        }

        #endregion

        #region Permits Tab

        private static void BuildPermitsTabChildren(InspectionTreeItem tabNode, Dialog_InfoCard dialog)
        {
            var pawn = InfoCardDataExtractor.GetPawn(dialog);
            if (pawn == null)
            {
                AddChild(tabNode, CreateInfoItem("No permits available", tabNode.IndentLevel + 1));
                return;
            }

            var permitsInfo = InfoCardDataExtractor.GetPermitsInfo(pawn);
            if (permitsInfo.Count == 0)
            {
                AddChild(tabNode, CreateInfoItem("No permits available", tabNode.IndentLevel + 1));
                return;
            }

            // Group by faction
            var grouped = permitsInfo.GroupBy(p => p.factionName);
            foreach (var group in grouped)
            {
                // Add faction as category header
                AddChild(tabNode, CreateCategoryHeader(group.Key, tabNode.IndentLevel + 1));

                foreach (var (permitName, factionName, available, description) in group)
                {
                    string availStr = available ? "Available" : "Unavailable";
                    var permitNode = new InspectionTreeItem
                    {
                        Type = InspectionTreeItem.ItemType.Item,
                        Label = $"{permitName} ({availStr})",
                        IsExpandable = !string.IsNullOrEmpty(description),
                        IsExpanded = false,
                        IndentLevel = tabNode.IndentLevel + 1
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        permitNode.OnActivate = () =>
                        {
                            if (permitNode.Children.Count > 0) return;
                            AddChild(permitNode, CreateInfoItem(description.StripTags(), permitNode.IndentLevel + 1));
                        };
                    }
                    AddChild(tabNode, permitNode);
                }
            }
        }

        #endregion

        #region Helpers

        private static InspectionTreeItem CreateInfoItem(string label, int indent)
        {
            return new InspectionTreeItem
            {
                Type = InspectionTreeItem.ItemType.DetailText,
                Label = label,
                IsExpandable = false,
                IsExpanded = false,
                IndentLevel = indent
            };
        }

        private static InspectionTreeItem CreateCategoryHeader(string label, int indent)
        {
            return new InspectionTreeItem
            {
                Type = InspectionTreeItem.ItemType.SubCategory,
                Label = label,
                IsExpandable = false,
                IsExpanded = false,
                IndentLevel = indent
            };
        }

        private static void AddChild(InspectionTreeItem parent, InspectionTreeItem child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
        }

        #endregion
    }
}
