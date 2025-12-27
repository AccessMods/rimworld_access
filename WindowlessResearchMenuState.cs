using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the windowless research menu state with hierarchical tree navigation.
    /// Organizes research projects by tab (Main/Anomaly) → status (Completed/Available/Locked/In Progress).
    /// </summary>
    public static class WindowlessResearchMenuState
    {
        private static bool isActive = false;
        private static List<ResearchMenuNode> rootNodes = new List<ResearchMenuNode>();
        private static List<ResearchMenuNode> flatNavigationList = new List<ResearchMenuNode>();
        private static int currentIndex = 0;
        private static HashSet<string> expandedNodes = new HashSet<string>();

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the research menu and builds the category tree.
        /// </summary>
        public static void Open()
        {
            isActive = true;
            expandedNodes.Clear();
            rootNodes = BuildCategoryTree();
            flatNavigationList = BuildFlatNavigationList();
            currentIndex = 0;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Closes the research menu.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            rootNodes.Clear();
            flatNavigationList.Clear();
            expandedNodes.Clear();
            TolkHelper.Speak("Research menu closed");
        }

        /// <summary>
        /// Navigates to the next item in the flat navigation list.
        /// </summary>
        public static void SelectNext()
        {
            if (flatNavigationList.Count == 0) return;

            currentIndex = (currentIndex + 1) % flatNavigationList.Count;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Navigates to the previous item in the flat navigation list.
        /// </summary>
        public static void SelectPrevious()
        {
            if (flatNavigationList.Count == 0) return;

            currentIndex--;
            if (currentIndex < 0)
                currentIndex = flatNavigationList.Count - 1;

            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the currently selected category (right arrow).
        /// </summary>
        public static void ExpandCategory()
        {
            if (flatNavigationList.Count == 0) return;

            var current = flatNavigationList[currentIndex];

            if (current.Type == ResearchMenuNodeType.Category && !current.IsExpanded)
            {
                current.IsExpanded = true;
                expandedNodes.Add(current.Id);
                flatNavigationList = BuildFlatNavigationList();
                AnnounceCurrentSelection();
            }
        }

        /// <summary>
        /// Collapses the currently selected category (left arrow).
        /// If on an expanded category, collapses it.
        /// If on a child item, collapses the parent and moves focus to it.
        /// </summary>
        public static void CollapseCategory()
        {
            if (flatNavigationList.Count == 0) return;

            var current = flatNavigationList[currentIndex];

            // Case 1: Current is an expanded category - collapse it directly
            if (current.Type == ResearchMenuNodeType.Category && current.IsExpanded)
            {
                current.IsExpanded = false;
                expandedNodes.Remove(current.Id);
                flatNavigationList = BuildFlatNavigationList();
                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            // Case 2: Find parent and collapse it
            var parent = current.Parent;

            // Skip to find an expandable parent (categories only)
            while (parent != null && parent.Type != ResearchMenuNodeType.Category)
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

            // Collapse the parent
            parent.IsExpanded = false;
            expandedNodes.Remove(parent.Id);
            flatNavigationList = BuildFlatNavigationList();

            // Move selection to the parent
            int parentIndex = flatNavigationList.IndexOf(parent);
            if (parentIndex >= 0)
            {
                currentIndex = parentIndex;
            }
            else if (currentIndex >= flatNavigationList.Count)
            {
                currentIndex = Math.Max(0, flatNavigationList.Count - 1);
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Executes the action for the currently selected item (Enter key).
        /// Opens detail view for projects.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (flatNavigationList.Count == 0) return;

            var current = flatNavigationList[currentIndex];

            if (current.Type == ResearchMenuNodeType.Project && current.Project != null)
            {
                // Open detail view for this project
                WindowlessResearchDetailState.Open(current.Project);
            }
            else if (current.Type == ResearchMenuNodeType.Category)
            {
                // Toggle expansion
                if (current.IsExpanded)
                    CollapseCategory();
                else
                    ExpandCategory();
            }
        }

        /// <summary>
        /// Builds the hierarchical category tree structure.
        /// Organization: Tab → Status Group → Individual Projects
        /// When only one tab exists, skips the tab wrapper and shows status groups directly.
        /// </summary>
        private static List<ResearchMenuNode> BuildCategoryTree()
        {
            var tree = new List<ResearchMenuNode>();

            // Get all research projects
            var allProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            // Group by research tab (Main, Anomaly, etc.)
            var projectsByTab = allProjects.GroupBy(p => p.tab ?? ResearchTabDefOf.Main).ToList();
            bool singleTab = projectsByTab.Count == 1;

            foreach (var tabGroup in projectsByTab.OrderBy(g => g.Key.defName))
            {
                var tab = tabGroup.Key;
                var tabProjects = tabGroup.ToList();

                // Group projects by status within this tab
                var inProgress = GetInProgressProjects(tabProjects);
                var completed = tabProjects.Where(p => p.IsFinished).ToList();
                var available = tabProjects.Where(p => !p.IsFinished && p.CanStartNow).ToList();
                var locked = tabProjects.Where(p => !p.IsFinished && !p.CanStartNow).ToList();

                // When only one tab exists, skip the tab wrapper and add status groups directly
                if (singleTab)
                {
                    // Add status groups directly to tree root (Level 0)
                    if (inProgress.Count > 0)
                    {
                        tree.Add(CreateStatusGroupNode($"Tab_{tab.defName}_InProgress", "In Progress", inProgress, 0));
                    }

                    if (available.Count > 0)
                    {
                        tree.Add(CreateStatusGroupNode($"Tab_{tab.defName}_Available", "Available", available, 0));
                    }

                    if (completed.Count > 0)
                    {
                        tree.Add(CreateStatusGroupNode($"Tab_{tab.defName}_Completed", "Completed", completed, 0));
                    }

                    if (locked.Count > 0)
                    {
                        tree.Add(CreateStatusGroupNode($"Tab_{tab.defName}_Locked", "Locked", locked, 0));
                    }
                }
                else
                {
                    // Multiple tabs - keep the tab wrapper structure
                    var tabNode = new ResearchMenuNode
                    {
                        Id = $"Tab_{tab.defName}",
                        Type = ResearchMenuNodeType.Category,
                        Label = tab.LabelCap.ToString(),
                        Level = 0,
                        Children = new List<ResearchMenuNode>()
                    };

                    // Add status group nodes (only if they have projects)
                    if (inProgress.Count > 0)
                    {
                        var node = CreateStatusGroupNode("InProgress", "In Progress", inProgress, 1);
                        node.Parent = tabNode;
                        tabNode.Children.Add(node);
                    }

                    if (available.Count > 0)
                    {
                        var node = CreateStatusGroupNode("Available", "Available", available, 1);
                        node.Parent = tabNode;
                        tabNode.Children.Add(node);
                    }

                    if (completed.Count > 0)
                    {
                        var node = CreateStatusGroupNode("Completed", "Completed", completed, 1);
                        node.Parent = tabNode;
                        tabNode.Children.Add(node);
                    }

                    if (locked.Count > 0)
                    {
                        var node = CreateStatusGroupNode("Locked", "Locked", locked, 1);
                        node.Parent = tabNode;
                        tabNode.Children.Add(node);
                    }

                    tree.Add(tabNode);
                }
            }

            return tree;
        }

        /// <summary>
        /// Gets the list of in-progress research projects.
        /// Handles both standard research and anomaly knowledge research.
        /// </summary>
        private static List<ResearchProjectDef> GetInProgressProjects(List<ResearchProjectDef> tabProjects)
        {
            var inProgress = new List<ResearchProjectDef>();

            // Check standard research
            var currentProject = Find.ResearchManager.GetProject();
            if (currentProject != null && tabProjects.Contains(currentProject))
            {
                inProgress.Add(currentProject);
            }

            // Check anomaly knowledge research (if Anomaly DLC active)
            if (ModsConfig.AnomalyActive)
            {
                var knowledgeCategories = DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading;
                foreach (var category in knowledgeCategories)
                {
                    var categoryProject = Find.ResearchManager.GetProject(category);
                    if (categoryProject != null && tabProjects.Contains(categoryProject))
                    {
                        inProgress.Add(categoryProject);
                    }
                }
            }

            return inProgress;
        }

        /// <summary>
        /// Creates a status group node (Completed, Available, Locked, In Progress).
        /// </summary>
        private static ResearchMenuNode CreateStatusGroupNode(string id, string label, List<ResearchProjectDef> projects, int level)
        {
            var statusNode = new ResearchMenuNode
            {
                Id = id,
                Type = ResearchMenuNodeType.Category,
                Label = $"{label} ({projects.Count})",
                Level = level,
                Children = new List<ResearchMenuNode>()
            };

            // Add individual project nodes
            foreach (var project in projects.OrderBy(p => p.LabelCap.ToString()))
            {
                var projectNode = new ResearchMenuNode
                {
                    Id = $"Project_{project.defName}",
                    Type = ResearchMenuNodeType.Project,
                    Label = FormatProjectLabel(project),
                    Level = level + 1,
                    Project = project,
                    Children = new List<ResearchMenuNode>(),
                    Parent = statusNode
                };
                statusNode.Children.Add(projectNode);
            }

            return statusNode;
        }

        /// <summary>
        /// Formats a research project label with cost and progress information.
        /// </summary>
        private static string FormatProjectLabel(ResearchProjectDef project)
        {
            string label = project.LabelCap.ToString();

            // Add cost information
            float cost = project.CostApparent;
            if (cost > 0)
            {
                label += $" - {cost:F0} cost";
            }
            else if (project.knowledgeCost > 0)
            {
                label += $" - {project.knowledgeCost:F0} knowledge";
            }

            // Add progress if in progress
            if (Find.ResearchManager.IsCurrentProject(project))
            {
                float progress = project.ProgressPercent * 100f;
                label += $" - {progress:F0}% complete";
            }

            // Add status indicator
            if (project.IsFinished)
            {
                label += " - Completed";
            }
            else if (project.CanStartNow)
            {
                label += " - Available";
            }
            else
            {
                label += " - Locked";
            }

            return label;
        }

        /// <summary>
        /// Flattens the hierarchical tree into a navigation list based on expanded categories.
        /// </summary>
        private static List<ResearchMenuNode> BuildFlatNavigationList()
        {
            var flatList = new List<ResearchMenuNode>();

            foreach (var node in rootNodes)
            {
                AddNodeToFlatList(node, flatList);
            }

            return flatList;
        }

        /// <summary>
        /// Recursively adds nodes to the flat navigation list.
        /// Only adds children of expanded nodes.
        /// </summary>
        private static void AddNodeToFlatList(ResearchMenuNode node, List<ResearchMenuNode> flatList)
        {
            flatList.Add(node);

            // Update expansion state based on expandedNodes set
            node.IsExpanded = expandedNodes.Contains(node.Id);

            if (node.IsExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    AddNodeToFlatList(child, flatList);
                }
            }
        }

        /// <summary>
        /// Announces the currently selected item to the clipboard for screen reader access.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            if (flatNavigationList.Count == 0)
            {
                TolkHelper.Speak("Research menu - No research projects available");
                return;
            }

            var current = flatNavigationList[currentIndex];
            string announcement = "";

            // Add indentation indicator based on level
            string indent = new string(' ', current.Level * 2);

            if (current.Type == ResearchMenuNodeType.Category)
            {
                string expandState = current.IsExpanded ? "expanded" : "collapsed";
                announcement = $"{indent}{current.Label} - {expandState}";
            }
            else if (current.Type == ResearchMenuNodeType.Project)
            {
                announcement = $"{indent}{current.Label}";
            }

            // Add navigation hint
            int position = currentIndex + 1;
            announcement += $" - Item {position} of {flatNavigationList.Count}";

            TolkHelper.Speak(announcement);
        }
    }

    /// <summary>
    /// Represents a node in the research menu tree (either a category or a project).
    /// </summary>
    public class ResearchMenuNode
    {
        public string Id { get; set; }
        public ResearchMenuNodeType Type { get; set; }
        public string Label { get; set; }
        public int Level { get; set; }
        public bool IsExpanded { get; set; }
        public ResearchProjectDef Project { get; set; }
        public List<ResearchMenuNode> Children { get; set; }
        public ResearchMenuNode Parent { get; set; }  // Reference to parent node for upward navigation
    }

    /// <summary>
    /// Type of node in the research menu tree.
    /// </summary>
    public enum ResearchMenuNodeType
    {
        Category,
        Project
    }
}
