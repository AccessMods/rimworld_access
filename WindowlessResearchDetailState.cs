using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the detail view for a specific research project.
    /// Uses a tree structure with expandable categories for prerequisites, unlocks, and dependents.
    /// </summary>
    public static class WindowlessResearchDetailState
    {
        private static bool isActive = false;
        private static ResearchProjectDef currentProject = null;
        private static List<DetailNode> rootNodes = new List<DetailNode>();
        private static List<DetailNode> flatNavigationList = new List<DetailNode>();
        private static int currentIndex = 0;
        private static HashSet<string> expandedNodes = new HashSet<string>();
        private static Dictionary<string, string> lastChildIdPerParent = new Dictionary<string, string>();
        private static Stack<ResearchProjectDef> navigationStack = new Stack<ResearchProjectDef>();

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the detail view for a specific research project.
        /// </summary>
        public static void Open(ResearchProjectDef project)
        {
            currentProject = project;
            isActive = true;
            expandedNodes.Clear();
            lastChildIdPerParent.Clear();
            rootNodes = BuildDetailTree(project);
            flatNavigationList = BuildFlatNavigationList();
            currentIndex = 0;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Opens the detail view for a project, pushing the current one onto the navigation stack.
        /// </summary>
        public static void OpenWithBackNavigation(ResearchProjectDef project)
        {
            if (currentProject != null)
            {
                navigationStack.Push(currentProject);
            }
            Open(project);
        }

        /// <summary>
        /// Closes the detail view. If there's a project in the stack, goes back to it.
        /// </summary>
        public static void Close()
        {
            if (navigationStack.Count > 0)
            {
                var previousProject = navigationStack.Pop();
                Open(previousProject);
                TolkHelper.Speak($"Back to {previousProject.LabelCap}");
            }
            else
            {
                isActive = false;
                currentProject = null;
                rootNodes.Clear();
                flatNavigationList.Clear();
                expandedNodes.Clear();
                lastChildIdPerParent.Clear();
                navigationStack.Clear();
                TolkHelper.Speak("Returned to research menu");
            }
        }

        /// <summary>
        /// Navigates to the next item.
        /// </summary>
        public static void SelectNext()
        {
            if (flatNavigationList.Count == 0) return;

            currentIndex = (currentIndex + 1) % flatNavigationList.Count;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Navigates to the previous item.
        /// </summary>
        public static void SelectPrevious()
        {
            if (flatNavigationList.Count == 0) return;

            currentIndex--;
            if (currentIndex < 0)
                currentIndex = flatNavigationList.Count - 1;

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the current category (Right arrow).
        /// </summary>
        public static void Expand()
        {
            if (flatNavigationList.Count == 0) return;

            var current = flatNavigationList[currentIndex];

            if (!current.IsExpandable)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }

            if (current.IsExpanded)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }

            if (current.Children == null || current.Children.Count == 0)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("No items");
                return;
            }

            current.IsExpanded = true;
            expandedNodes.Add(current.Id);
            flatNavigationList = BuildFlatNavigationList();
            SoundDefOf.Click.PlayOneShotOnCamera();

            // Restore last selected child position if available
            if (lastChildIdPerParent.TryGetValue(current.Id, out string savedChildId))
            {
                int savedIndex = flatNavigationList.FindIndex(n => n.Id == savedChildId);
                if (savedIndex >= 0)
                {
                    currentIndex = savedIndex;
                }
            }
            else
            {
                // Move to first child
                int firstChildIndex = flatNavigationList.IndexOf(current) + 1;
                if (firstChildIndex < flatNavigationList.Count)
                {
                    currentIndex = firstChildIndex;
                }
            }

            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Collapses the current category or navigates to parent (Left arrow).
        /// </summary>
        public static void Collapse()
        {
            if (flatNavigationList.Count == 0) return;

            var current = flatNavigationList[currentIndex];

            // Case 1: Current is expanded category - collapse it
            if (current.IsExpandable && current.IsExpanded)
            {
                current.IsExpanded = false;
                expandedNodes.Remove(current.Id);
                flatNavigationList = BuildFlatNavigationList();
                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            // Case 2: Navigate to parent and collapse it
            var parent = current.Parent;
            if (parent != null && parent.IsExpanded)
            {
                // Save current position
                lastChildIdPerParent[parent.Id] = current.Id;

                parent.IsExpanded = false;
                expandedNodes.Remove(parent.Id);
                flatNavigationList = BuildFlatNavigationList();

                int parentIndex = flatNavigationList.IndexOf(parent);
                if (parentIndex >= 0)
                {
                    currentIndex = parentIndex;
                }

                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            TolkHelper.Speak("Already at top level");
        }

        /// <summary>
        /// Executes the action for the current item (Enter key).
        /// </summary>
        public static void ExecuteCurrentItem()
        {
            if (flatNavigationList.Count == 0 || currentProject == null) return;

            var current = flatNavigationList[currentIndex];

            switch (current.Type)
            {
                case DetailNodeType.Category:
                    // Toggle expand/collapse
                    if (current.IsExpanded)
                        Collapse();
                    else
                        Expand();
                    break;

                case DetailNodeType.ResearchItem:
                    // Drill into this research project
                    if (current.LinkedProject != null)
                    {
                        OpenWithBackNavigation(current.LinkedProject);
                    }
                    break;

                case DetailNodeType.UnlockedItem:
                    // Re-read the label (which contains the description inline)
                    TolkHelper.Speak(current.Label);
                    break;

                case DetailNodeType.Action:
                    ExecuteResearchAction();
                    break;

                case DetailNodeType.Info:
                    // Read the content
                    if (!string.IsNullOrEmpty(current.Content))
                    {
                        TolkHelper.Speak(current.Content);
                    }
                    break;
            }
        }

        /// <summary>
        /// Starts or stops research on the current project.
        /// </summary>
        private static void ExecuteResearchAction()
        {
            if (currentProject == null) return;

            // Check if already researching this project
            if (Find.ResearchManager.IsCurrentProject(currentProject))
            {
                Find.ResearchManager.StopProject(currentProject);
                TolkHelper.Speak($"Stopped research on {currentProject.LabelCap}");
                RefreshTree();
                return;
            }

            // Check if already completed
            if (currentProject.IsFinished)
            {
                TolkHelper.Speak($"{currentProject.LabelCap} is already completed");
                return;
            }

            // Check prerequisites
            if (!currentProject.PrerequisitesCompleted)
            {
                var missingPrereqs = GetMissingPrerequisites();
                TolkHelper.Speak($"Cannot start research: Missing prerequisites - {missingPrereqs}", SpeechPriority.High);
                return;
            }

            // Check techprint requirements
            if (currentProject.TechprintCount > 0 && !currentProject.TechprintRequirementMet)
            {
                int applied = Find.ResearchManager.GetTechprints(currentProject);
                TolkHelper.Speak($"Cannot start research: Need {currentProject.TechprintCount} techprints, only {applied} applied", SpeechPriority.High);
                return;
            }

            // Check study requirements
            if (currentProject.requiredAnalyzed != null && currentProject.requiredAnalyzed.Count > 0)
            {
                if (!currentProject.AnalyzedThingsRequirementsMet)
                {
                    TolkHelper.Speak($"Cannot start research: Must study required items first", SpeechPriority.High);
                    return;
                }
            }

            // Capture previous project before starting new one
            var previousProject = Find.ResearchManager.GetProject();

            // Start research
            Find.ResearchManager.SetCurrentProject(currentProject);

            // Announce with replacement info if applicable
            if (previousProject != null && previousProject != currentProject)
            {
                float previousProgress = previousProject.ProgressPercent * 100f;
                TolkHelper.Speak($"Started research on {currentProject.LabelCap}. Stopped {previousProject.LabelCap} at {previousProgress:F0}% progress.");
            }
            else
            {
                TolkHelper.Speak($"Started research on {currentProject.LabelCap}");
            }
            RefreshTree();
        }

        /// <summary>
        /// Refreshes the tree after changes (like starting/stopping research).
        /// </summary>
        private static void RefreshTree()
        {
            rootNodes = BuildDetailTree(currentProject);
            flatNavigationList = BuildFlatNavigationList();
            if (currentIndex >= flatNavigationList.Count)
                currentIndex = Math.Max(0, flatNavigationList.Count - 1);
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Gets a formatted list of missing prerequisites.
        /// </summary>
        private static string GetMissingPrerequisites()
        {
            if (currentProject == null || currentProject.prerequisites == null)
                return "Unknown";

            var missing = currentProject.prerequisites
                .Where(p => !p.IsFinished)
                .Select(p => p.LabelCap.ToString());

            return string.Join(", ", missing);
        }

        /// <summary>
        /// Builds the tree structure for the detail view.
        /// </summary>
        private static List<DetailNode> BuildDetailTree(ResearchProjectDef project)
        {
            var nodes = new List<DetailNode>();

            // Node 1: Description (Info, non-expandable)
            nodes.Add(new DetailNode
            {
                Id = "description",
                Type = DetailNodeType.Info,
                Label = "Description",
                Content = BuildDescriptionContent(project),
                IsExpandable = false
            });

            // Node 2: Prerequisites (Category, expandable)
            var prereqNode = BuildPrerequisitesNode(project);
            if (prereqNode != null)
                nodes.Add(prereqNode);

            // Node 3: Unlocks (Category, expandable)
            var unlocksNode = BuildUnlocksNode(project);
            if (unlocksNode != null)
                nodes.Add(unlocksNode);

            // Node 4: Dependents (Category, expandable)
            var dependentsNode = BuildDependentsNode(project);
            if (dependentsNode != null)
                nodes.Add(dependentsNode);

            // Node 5: Start/Stop Research (Action)
            nodes.Add(new DetailNode
            {
                Id = "action_research",
                Type = DetailNodeType.Action,
                Label = Find.ResearchManager.IsCurrentProject(project) ? "Stop Research" : "Start Research",
                Content = Find.ResearchManager.IsCurrentProject(project)
                    ? "Press Enter to stop this research"
                    : "Press Enter to start this research",
                IsExpandable = false
            });

            return nodes;
        }

        /// <summary>
        /// Builds the prerequisites node with children.
        /// </summary>
        private static DetailNode BuildPrerequisitesNode(ResearchProjectDef project)
        {
            var children = new List<DetailNode>();

            // Research prerequisites
            if (project.prerequisites != null && project.prerequisites.Count > 0)
            {
                foreach (var prereq in project.prerequisites.OrderBy(p => p.LabelCap.ToString()))
                {
                    string status = prereq.IsFinished ? "Completed" : "Locked";
                    children.Add(new DetailNode
                    {
                        Id = $"prereq_{prereq.defName}",
                        Type = DetailNodeType.ResearchItem,
                        Label = $"{prereq.LabelCap} - cost: {prereq.CostApparent:F0} {status}",
                        LinkedProject = prereq,
                        IsExpandable = false
                    });
                }
            }

            // Even if no prerequisites, show the node with a message
            var node = new DetailNode
            {
                Id = "prerequisites",
                Type = DetailNodeType.Category,
                Label = children.Count > 0
                    ? $"Prerequisites ({children.Count} items)"
                    : "Prerequisites (none)",
                IsExpandable = children.Count > 0,
                Children = children
            };

            // Set parent references
            foreach (var child in children)
                child.Parent = node;

            // Restore expansion state
            node.IsExpanded = expandedNodes.Contains(node.Id);

            return node;
        }

        /// <summary>
        /// Builds the unlocks node with children.
        /// </summary>
        private static DetailNode BuildUnlocksNode(ResearchProjectDef project)
        {
            var children = new List<DetailNode>();

            // Get all unlocked things
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.researchPrerequisites != null && def.researchPrerequisites.Contains(project))
                {
                    string category = def.building != null ? "Building" :
                                     def.plant != null ? "Plant" : "Item";
                    var itemNode = CreateUnlockedItemNode($"unlock_thing_{def.defName}", def.LabelCap, category, def.description);
                    children.Add(itemNode);
                }
            }

            // Get unlocked recipes
            foreach (var def in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (def.researchPrerequisite == project ||
                    (def.researchPrerequisites != null && def.researchPrerequisites.Contains(project)))
                {
                    // Use recipe description, or product description if available
                    string description = def.description;
                    if (string.IsNullOrEmpty(description) && def.ProducedThingDef != null)
                    {
                        description = def.ProducedThingDef.description;
                    }
                    var itemNode = CreateUnlockedItemNode($"unlock_recipe_{def.defName}", def.LabelCap, "Recipe", description);
                    children.Add(itemNode);
                }
            }

            // Sort by label
            children = children.OrderBy(c => c.Label).ToList();

            var node = new DetailNode
            {
                Id = "unlocks",
                Type = DetailNodeType.Category,
                Label = children.Count > 0
                    ? $"Unlocks ({children.Count} items)"
                    : "Unlocks (none)",
                IsExpandable = children.Count > 0,
                Children = children
            };

            // Set parent references
            foreach (var child in children)
                child.Parent = node;

            // Restore expansion state
            node.IsExpanded = expandedNodes.Contains(node.Id);

            return node;
        }

        /// <summary>
        /// Creates an unlocked item node with description inline in the label.
        /// </summary>
        private static DetailNode CreateUnlockedItemNode(string id, string label, string category, string description)
        {
            // Clean up description
            string cleanDesc = "";
            if (!string.IsNullOrEmpty(description))
            {
                cleanDesc = description;
                if (cleanDesc.Contains("<"))
                {
                    cleanDesc = System.Text.RegularExpressions.Regex.Replace(cleanDesc, "<[^>]+>", "");
                }
                cleanDesc = cleanDesc.Trim();
                cleanDesc = System.Text.RegularExpressions.Regex.Replace(cleanDesc, @"\s+", " ");
            }

            // Build label with description inline
            string fullLabel = $"{label} ({category})";
            if (!string.IsNullOrEmpty(cleanDesc))
            {
                fullLabel += $" - {cleanDesc}";
            }

            return new DetailNode
            {
                Id = id,
                Type = DetailNodeType.UnlockedItem,
                Label = fullLabel,
                IsExpandable = false,
                Children = new List<DetailNode>()
            };
        }

        /// <summary>
        /// Builds the dependents node with children.
        /// </summary>
        private static DetailNode BuildDependentsNode(ResearchProjectDef project)
        {
            var children = new List<DetailNode>();

            var dependents = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(p => p.prerequisites != null && p.prerequisites.Contains(project))
                .OrderBy(p => p.LabelCap.ToString())
                .ToList();

            foreach (var dep in dependents)
            {
                string status = dep.IsFinished ? "Completed" :
                               dep.CanStartNow ? "Available" : "Locked";
                children.Add(new DetailNode
                {
                    Id = $"dependent_{dep.defName}",
                    Type = DetailNodeType.ResearchItem,
                    Label = $"{dep.LabelCap} - cost: {dep.CostApparent:F0} {status}",
                    LinkedProject = dep,
                    IsExpandable = false
                });
            }

            var node = new DetailNode
            {
                Id = "dependents",
                Type = DetailNodeType.Category,
                Label = children.Count > 0
                    ? $"Dependents ({children.Count} items)"
                    : "Dependents (none)",
                IsExpandable = children.Count > 0,
                Children = children
            };

            // Set parent references
            foreach (var child in children)
                child.Parent = node;

            // Restore expansion state
            node.IsExpanded = expandedNodes.Contains(node.Id);

            return node;
        }

        /// <summary>
        /// Builds the description content.
        /// </summary>
        private static string BuildDescriptionContent(ResearchProjectDef project)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Project: {project.LabelCap}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(project.description))
            {
                sb.AppendLine(project.description);
                sb.AppendLine();
            }

            if (project.CostApparent > 0)
            {
                sb.AppendLine($"Research Cost: {project.CostApparent:F0}");
            }
            else if (project.knowledgeCost > 0)
            {
                sb.AppendLine($"Knowledge Cost: {project.knowledgeCost:F0}");
                if (project.knowledgeCategory != null)
                {
                    sb.AppendLine($"Knowledge Category: {project.knowledgeCategory.LabelCap}");
                }
            }

            if (Find.ResearchManager.IsCurrentProject(project))
            {
                float progress = project.ProgressPercent * 100f;
                sb.AppendLine($"Progress: {progress:F1}%");
            }
            else if (project.IsFinished)
            {
                sb.AppendLine("Status: Completed");
            }
            else if (project.CanStartNow)
            {
                sb.AppendLine("Status: Available to research");
            }
            else
            {
                sb.AppendLine("Status: Locked");
            }

            if (project.requiredResearchBuilding != null)
            {
                sb.AppendLine($"Required Bench: {project.requiredResearchBuilding.LabelCap}");
            }

            if (project.requiredResearchFacilities != null && project.requiredResearchFacilities.Count > 0)
            {
                sb.Append("Required Facilities: ");
                sb.AppendLine(string.Join(", ", project.requiredResearchFacilities.Select(f => f.LabelCap)));
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Flattens the tree for navigation.
        /// </summary>
        private static List<DetailNode> BuildFlatNavigationList()
        {
            var flatList = new List<DetailNode>();

            foreach (var node in rootNodes)
            {
                AddNodeToFlatList(node, flatList);
            }

            return flatList;
        }

        /// <summary>
        /// Recursively adds nodes to the flat list.
        /// </summary>
        private static void AddNodeToFlatList(DetailNode node, List<DetailNode> flatList)
        {
            flatList.Add(node);

            if (node.IsExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    AddNodeToFlatList(child, flatList);
                }
            }
        }

        /// <summary>
        /// Announces the current selection.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            if (flatNavigationList.Count == 0 || currentProject == null)
            {
                TolkHelper.Speak("No items available");
                return;
            }

            var current = flatNavigationList[currentIndex];

            // Build announcement
            string announcement = current.Label;

            // Add expand/collapse indicator for categories
            if (current.Type == DetailNodeType.Category && current.IsExpandable)
            {
                announcement += current.IsExpanded ? " [Expanded]" : " [Collapsed]";
            }

            // For info nodes, append content BEFORE position suffix
            if (current.Type == DetailNodeType.Info && !string.IsNullOrEmpty(current.Content))
            {
                announcement += "\n\n" + current.Content;
            }

            // Calculate sibling position and append at the very END
            List<DetailNode> siblings;
            if (current.Parent == null)
            {
                siblings = rootNodes;
            }
            else
            {
                siblings = current.Parent.Children;
            }
            int siblingPosition = siblings.IndexOf(current) + 1;
            announcement += $" - Item {siblingPosition} of {siblings.Count}";

            TolkHelper.Speak(announcement);
        }
    }

    /// <summary>
    /// Represents a node in the research detail tree.
    /// </summary>
    public class DetailNode
    {
        public string Id { get; set; }
        public DetailNodeType Type { get; set; }
        public string Label { get; set; }
        public string Content { get; set; }
        public bool IsExpandable { get; set; }
        public bool IsExpanded { get; set; }
        public List<DetailNode> Children { get; set; } = new List<DetailNode>();
        public DetailNode Parent { get; set; }
        public ResearchProjectDef LinkedProject { get; set; }
    }

    /// <summary>
    /// Type of detail node.
    /// </summary>
    public enum DetailNodeType
    {
        Info,           // Description section
        Category,       // Prerequisites, Unlocks, Dependents headers
        ResearchItem,   // A research project that can be drilled into
        UnlockedItem,   // A building/recipe that can be inspected
        Action          // Start/Stop research button
    }
}
