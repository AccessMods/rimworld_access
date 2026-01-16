using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    // Type aliases for cleaner code - use CaravanInspectState's types for consistency
    using TreeNode = CaravanInspectState.TreeNode;
    using NodeType = CaravanInspectState.NodeType;

    /// <summary>
    /// Handles inspection when world objects are present at a tile.
    /// Uses tree view pattern consistent with WindowlessInspectionState on local maps.
    /// All objects at tile are shown as root nodes with their inspection details as children.
    /// Uses CaravanInspectState.TreeNode for consistency with caravan inspection.
    /// </summary>
    public static class WorldObjectSelectionState
    {
        public static bool IsActive { get; private set; } = false;

        private static TreeNode rootNode = null;
        private static List<TreeNode> visibleNodes = new List<TreeNode>();
        private static int selectedIndex = 0;
        private static PlanetTile currentTile;
        private static Dictionary<TreeNode, TreeNode> lastChildPerParent = new Dictionary<TreeNode, TreeNode>();

        /// <summary>
        /// Opens the world object inspection for the given tile.
        /// If only one inspectable object exists, directly opens it.
        /// </summary>
        public static void Open(PlanetTile tile)
        {
            if (!tile.Valid || Find.WorldObjects == null)
            {
                TolkHelper.Speak("No objects here");
                return;
            }

            currentTile = tile;

            // Get all world objects at this tile that can be inspected
            var worldObjects = Find.WorldObjects.ObjectsAt(tile)
                .Where(obj => IsInspectable(obj))
                .OrderBy(obj => GetObjectSortOrder(obj))
                .ToList();

            if (worldObjects.Count == 0)
            {
                TolkHelper.Speak("No objects here to inspect");
                return;
            }

            // If only one object, directly open appropriate inspector
            if (worldObjects.Count == 1)
            {
                ActivateWorldObject(worldObjects[0]);
                return;
            }

            // Multiple objects - open tree view
            IsActive = true;
            selectedIndex = 0;
            lastChildPerParent.Clear();
            MenuHelper.ResetLevel("WorldObjectInspect");

            BuildTree(worldObjects);

            SoundDefOf.TabOpen.PlayOneShotOnCamera();

            // Announce opening
            string announcement = $"{worldObjects.Count} objects at this tile";
            TolkHelper.Speak(announcement);
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Closes the world object selection state.
        /// </summary>
        public static void Close()
        {
            if (!IsActive)
                return;

            IsActive = false;
            rootNode = null;
            visibleNodes.Clear();
            selectedIndex = 0;
            lastChildPerParent.Clear();
            SoundDefOf.TabClose.PlayOneShotOnCamera();
        }

        /// <summary>
        /// Builds the tree structure for world objects.
        /// </summary>
        private static void BuildTree(List<WorldObject> worldObjects)
        {
            rootNode = new TreeNode
            {
                Type = NodeType.Root,
                Label = "Root",
                IsExpanded = true,
                CanExpand = true
            };

            foreach (var obj in worldObjects)
            {
                AddWorldObjectNode(rootNode, obj);
            }

            RebuildVisibleList();
        }

        /// <summary>
        /// Adds a world object node to the tree.
        /// </summary>
        private static void AddWorldObjectNode(TreeNode parent, WorldObject obj)
        {
            string label = GetObjectLabel(obj);

            var node = new TreeNode
            {
                Type = NodeType.WorldObject,
                Label = label,
                Depth = 0,
                CanExpand = true,
                IsExpanded = false,
                Parent = parent,
                Data = obj
            };

            // Set OnActivate after node is created so lambda can capture it
            node.OnActivate = () => BuildWorldObjectChildren(node, obj);

            parent.Children.Add(node);
        }

        /// <summary>
        /// Builds children for a world object when expanded.
        /// </summary>
        private static void BuildWorldObjectChildren(TreeNode objectNode, WorldObject obj)
        {
            if (objectNode.Children.Count > 0)
                return; // Already built

            if (obj is Caravan caravan && caravan.Faction == Faction.OfPlayer)
            {
                // Use CaravanInspectState's tree building for caravans
                CaravanInspectState.BuildCaravanCategoriesFor(objectNode, caravan);
            }
            else if (obj is Settlement settlement)
            {
                BuildSettlementChildren(objectNode, settlement);
            }
            else if (obj is Site site)
            {
                BuildSiteChildren(objectNode, site);
            }
            else if (obj is MapParent mapParent)
            {
                BuildMapParentChildren(objectNode, mapParent);
            }
            else
            {
                // Generic world object - show description
                BuildGenericWorldObjectChildren(objectNode, obj);
            }
        }

        /// <summary>
        /// Builds children for a settlement.
        /// </summary>
        private static void BuildSettlementChildren(TreeNode parent, Settlement settlement)
        {
            int depth = parent.Depth + 1;

            // Player settlement with map - can enter
            if (settlement.Faction == Faction.OfPlayer && settlement.HasMap)
            {
                var enterNode = new TreeNode
                {
                    Type = NodeType.Action,
                    Label = "Enter Settlement",
                    Depth = depth,
                    Parent = parent,
                    Data = settlement,
                    OnActivate = () =>
                    {
                        Close();
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(settlement.Map.Center, settlement.Map));
                        TolkHelper.Speak($"Entering {settlement.Label}");
                    }
                };
                parent.Children.Add(enterNode);
            }

            // Faction info
            if (settlement.Faction != null)
            {
                AddDetailNode(parent, depth, $"Faction: {settlement.Faction.Name}");

                // Relation if not player
                if (settlement.Faction != Faction.OfPlayer)
                {
                    FactionRelationKind relation = settlement.Faction.RelationKindWith(Faction.OfPlayer);
                    AddDetailNode(parent, depth, $"Relation: {relation}");
                }
            }

            // Trade availability for non-hostile settlements
            if (settlement.Faction != Faction.OfPlayer &&
                settlement.Faction != null &&
                !settlement.Faction.HostileTo(Faction.OfPlayer))
            {
                AddDetailNode(parent, depth, "Can trade here");
            }
        }

        /// <summary>
        /// Builds children for a site.
        /// </summary>
        private static void BuildSiteChildren(TreeNode parent, Site site)
        {
            int depth = parent.Depth + 1;

            // Site description
            string desc = site.GetDescription();
            if (!string.IsNullOrEmpty(desc))
            {
                desc = desc.StripTags();
                // Truncate long descriptions
                if (desc.Length > 200)
                    desc = desc.Substring(0, 200) + "...";

                AddDetailNode(parent, depth, desc);
            }

            // Faction if present
            if (site.Faction != null)
            {
                AddDetailNode(parent, depth, $"Faction: {site.Faction.Name}");
            }
        }

        /// <summary>
        /// Builds children for a generic map parent.
        /// </summary>
        private static void BuildMapParentChildren(TreeNode parent, MapParent mapParent)
        {
            int depth = parent.Depth + 1;

            // Can enter if has map
            if (mapParent.HasMap)
            {
                var enterNode = new TreeNode
                {
                    Type = NodeType.Action,
                    Label = "Enter",
                    Depth = depth,
                    Parent = parent,
                    Data = mapParent,
                    OnActivate = () =>
                    {
                        Close();
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(mapParent.Map.Center, mapParent.Map));
                        TolkHelper.Speak($"Entering {mapParent.Label}");
                    }
                };
                parent.Children.Add(enterNode);
            }

            // Description
            string desc = mapParent.GetDescription();
            if (!string.IsNullOrEmpty(desc))
            {
                desc = desc.StripTags();
                if (desc.Length > 200)
                    desc = desc.Substring(0, 200) + "...";

                AddDetailNode(parent, depth, desc);
            }
        }

        /// <summary>
        /// Builds children for a generic world object.
        /// </summary>
        private static void BuildGenericWorldObjectChildren(TreeNode parent, WorldObject obj)
        {
            int depth = parent.Depth + 1;

            string desc = obj.GetDescription();
            if (!string.IsNullOrEmpty(desc))
            {
                desc = desc.StripTags();
                AddDetailNode(parent, depth, desc);
            }
            else
            {
                AddDetailNode(parent, depth, obj.Label);
            }
        }

        /// <summary>
        /// Helper to add a detail text node.
        /// </summary>
        private static void AddDetailNode(TreeNode parent, int depth, string label)
        {
            var node = new TreeNode
            {
                Type = NodeType.DetailText,
                Label = label,
                Depth = depth,
                Parent = parent
            };
            parent.Children.Add(node);
        }

        /// <summary>
        /// Rebuilds the flattened visible nodes list.
        /// </summary>
        private static void RebuildVisibleList()
        {
            visibleNodes.Clear();

            if (rootNode == null)
                return;

            foreach (var child in rootNode.Children)
            {
                AddVisibleNodes(child);
            }
        }

        private static void AddVisibleNodes(TreeNode node)
        {
            visibleNodes.Add(node);

            if (node.IsExpanded && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    AddVisibleNodes(child);
                }
            }
        }

        /// <summary>
        /// Determines if a world object can be inspected.
        /// </summary>
        private static bool IsInspectable(WorldObject obj)
        {
            if (obj == null)
                return false;

            // Player caravans
            if (obj is Caravan caravan && caravan.Faction == Faction.OfPlayer)
                return true;

            // Player settlements with maps (can enter)
            if (obj is Settlement settlement && settlement.Faction == Faction.OfPlayer && settlement.HasMap)
                return true;

            // Other faction settlements (for info)
            if (obj is Settlement otherSettlement && otherSettlement.Faction != Faction.OfPlayer)
                return true;

            // Sites, camps, etc.
            if (obj is Site || obj is MapParent)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the sort order for object types.
        /// Lower numbers appear first.
        /// </summary>
        private static int GetObjectSortOrder(WorldObject obj)
        {
            // Player caravans first
            if (obj is Caravan c && c.Faction == Faction.OfPlayer)
                return 0;

            // Player settlements second
            if (obj is Settlement s && s.Faction == Faction.OfPlayer)
                return 1;

            // Other settlements third
            if (obj is Settlement)
                return 2;

            // Everything else
            return 3;
        }

        /// <summary>
        /// Gets a descriptive label for a world object.
        /// </summary>
        private static string GetObjectLabel(WorldObject obj)
        {
            if (obj is Caravan caravan)
            {
                return $"{caravan.Label} (caravan)";
            }

            if (obj is Settlement settlement)
            {
                if (settlement.Faction == Faction.OfPlayer)
                    return $"{settlement.Label} (your settlement)";
                else
                    return $"{settlement.Label} ({settlement.Faction?.Name ?? "unknown faction"})";
            }

            if (obj is Site site)
            {
                return $"{site.Label} (site)";
            }

            return obj.Label;
        }

        /// <summary>
        /// Activates a world object directly (when only one at tile).
        /// </summary>
        private static void ActivateWorldObject(WorldObject obj)
        {
            if (obj is Caravan caravan && caravan.Faction == Faction.OfPlayer)
            {
                CaravanInspectState.Open(caravan);
            }
            else if (obj is Settlement settlement && settlement.Faction == Faction.OfPlayer && settlement.HasMap)
            {
                // Enter player settlement
                CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(settlement.Map.Center, settlement.Map));
                TolkHelper.Speak($"Entering {settlement.Label}");
            }
            else if (obj is Settlement otherSettlement)
            {
                // For other faction settlements, announce info
                string info = $"{otherSettlement.Label}. {otherSettlement.Faction?.Name ?? "Unknown faction"}.";
                if (otherSettlement.Faction != null)
                {
                    FactionRelationKind relation = otherSettlement.Faction.RelationKindWith(Faction.OfPlayer);
                    info += $" {relation}.";
                }
                TolkHelper.Speak(info);
            }
            else
            {
                // For other objects, just announce their label and description
                string info = obj.Label;
                if (!string.IsNullOrEmpty(obj.GetDescription()))
                {
                    info += ". " + obj.GetDescription().StripTags();
                }
                TolkHelper.Speak(info);
            }
        }

        #region Navigation

        /// <summary>
        /// Selects the next item.
        /// </summary>
        public static void SelectNext()
        {
            if (visibleNodes.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectNext(selectedIndex, visibleNodes.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Selects the previous item.
        /// </summary>
        public static void SelectPrevious()
        {
            if (visibleNodes.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, visibleNodes.Count);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Expands the selected node (Right arrow).
        /// </summary>
        public static void Expand()
        {
            if (visibleNodes.Count == 0 || selectedIndex >= visibleNodes.Count)
                return;

            var node = visibleNodes[selectedIndex];

            if (!node.CanExpand)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Cannot expand");
                return;
            }

            if (node.IsExpanded)
            {
                // Already expanded - move to first child
                if (node.Children.Count > 0)
                {
                    int childIndex = visibleNodes.IndexOf(node.Children[0]);
                    if (childIndex >= 0)
                    {
                        selectedIndex = childIndex;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        AnnounceCurrentSelection();
                    }
                }
                return;
            }

            // Build children lazily
            if (node.OnActivate != null && node.Children.Count == 0)
            {
                node.OnActivate();
            }

            if (node.Children.Count == 0)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("No items to show");
                return;
            }

            // Expand
            node.IsExpanded = true;
            RebuildVisibleList();
            SoundDefOf.Click.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Collapses the selected node (Left arrow).
        /// </summary>
        public static void Collapse()
        {
            if (visibleNodes.Count == 0 || selectedIndex >= visibleNodes.Count)
                return;

            var node = visibleNodes[selectedIndex];

            // If expanded, collapse
            if (node.CanExpand && node.IsExpanded)
            {
                node.IsExpanded = false;
                RebuildVisibleList();

                if (selectedIndex >= visibleNodes.Count)
                    selectedIndex = Math.Max(0, visibleNodes.Count - 1);

                SoundDefOf.Click.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
                return;
            }

            // Move to parent
            var parent = node.Parent;
            while (parent != null && !parent.CanExpand)
            {
                parent = parent.Parent;
            }

            if (parent == null || parent == rootNode)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                TolkHelper.Speak("Already at top level");
                return;
            }

            lastChildPerParent[parent] = node;

            int parentIndex = visibleNodes.IndexOf(parent);
            if (parentIndex >= 0)
            {
                selectedIndex = parentIndex;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                AnnounceCurrentSelection();
            }
        }

        /// <summary>
        /// Activates the selected item (Enter key).
        /// </summary>
        public static void ActivateSelected()
        {
            if (visibleNodes.Count == 0 || selectedIndex >= visibleNodes.Count)
                return;

            var node = visibleNodes[selectedIndex];

            // If expandable and collapsed, expand
            if (node.CanExpand && !node.IsExpanded)
            {
                Expand();
                return;
            }

            // If has action (and is an action type), execute it
            if (node.OnActivate != null && node.Type == NodeType.Action)
            {
                node.OnActivate();
                SoundDefOf.Click.PlayOneShotOnCamera();
                return;
            }

            // For detail text or stats, just read again
            if (node.Type == NodeType.DetailText || node.Type == NodeType.Stat)
            {
                TolkHelper.Speak(node.GetDisplayLabel());
                return;
            }

            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            TolkHelper.Speak("No action available");
        }

        /// <summary>
        /// Jumps to first item.
        /// With Ctrl, jumps to absolute first. Otherwise jumps to first sibling at current level.
        /// </summary>
        private static void JumpToFirst(bool ctrlPressed = false)
        {
            if (visibleNodes == null || visibleNodes.Count == 0) return;
            MenuHelper.HandleTreeHomeKey(visibleNodes, ref selectedIndex, node => node.Depth, ctrlPressed, ClearAndAnnounce);
        }

        /// <summary>
        /// Jumps to last item.
        /// With Ctrl, jumps to absolute last. Otherwise jumps to last sibling/descendant at current level.
        /// </summary>
        private static void JumpToLast(bool ctrlPressed = false)
        {
            if (visibleNodes == null || visibleNodes.Count == 0) return;
            MenuHelper.HandleTreeEndKey(visibleNodes, ref selectedIndex, node => node.Depth,
                node => node.IsExpanded, node => node.CanExpand && node.Children != null && node.Children.Count > 0,
                ctrlPressed, ClearAndAnnounce);
        }

        /// <summary>
        /// Helper for tree navigation callbacks - plays sound and announces selection.
        /// </summary>
        private static void ClearAndAnnounce()
        {
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            AnnounceCurrentSelection();
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Gets sibling position for announcement.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(TreeNode node)
        {
            if (node.Parent == null || node.Parent == rootNode)
            {
                // Root level
                int idx = rootNode.Children.IndexOf(node) + 1;
                return (idx, rootNode.Children.Count);
            }

            var siblings = node.Parent.Children;
            int position = siblings.IndexOf(node) + 1;
            return (position, siblings.Count);
        }

        /// <summary>
        /// Announces the current selection.
        /// </summary>
        private static void AnnounceCurrentSelection()
        {
            if (visibleNodes.Count == 0 || selectedIndex >= visibleNodes.Count)
                return;

            var node = visibleNodes[selectedIndex];
            string label = node.GetDisplayLabel().StripTags();

            // State indicator for expandable items
            string stateIndicator = "";
            if (node.CanExpand)
            {
                stateIndicator = node.IsExpanded ? " expanded" : " collapsed";
            }

            // Position among siblings
            var (position, total) = GetSiblingPosition(node);
            string positionPart = MenuHelper.FormatPosition(position - 1, total);

            // Level suffix
            string levelSuffix = MenuHelper.GetLevelSuffix("WorldObjectInspect", node.Depth);

            string announcement = $"{label}{stateIndicator}. {positionPart}{levelSuffix}";
            TolkHelper.Speak(announcement);
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles keyboard input.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            if (!IsActive)
                return false;

            if (key == KeyCode.UpArrow && !shift && !ctrl && !alt)
            {
                SelectPrevious();
                return true;
            }

            if (key == KeyCode.DownArrow && !shift && !ctrl && !alt)
            {
                SelectNext();
                return true;
            }

            if (key == KeyCode.RightArrow && !shift && !ctrl && !alt)
            {
                Expand();
                return true;
            }

            if (key == KeyCode.LeftArrow && !shift && !ctrl && !alt)
            {
                Collapse();
                return true;
            }

            if ((key == KeyCode.Return || key == KeyCode.KeypadEnter) && !shift && !ctrl && !alt)
            {
                ActivateSelected();
                return true;
            }

            if (key == KeyCode.Escape && !shift && !ctrl && !alt)
            {
                Close();
                TolkHelper.Speak("Selection closed");
                return true;
            }

            if (key == KeyCode.Home && !shift && !alt)
            {
                JumpToFirst(Event.current.control);
                Event.current.Use();
                return true;
            }

            if (key == KeyCode.End && !shift && !alt)
            {
                JumpToLast(Event.current.control);
                Event.current.Use();
                return true;
            }

            return false;
        }

        #endregion
    }
}
