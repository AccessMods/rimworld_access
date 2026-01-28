using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to render visual preview of keyboard-based selection.
    /// Uses RimWorld's native highlighting materials so sighted observers can see what's being selected.
    /// </summary>
    [HarmonyPatch(typeof(MapInterface))]
    [HarmonyPatch("MapInterfaceUpdate")]
    public static class SelectionPreviewPatch
    {
        /// <summary>
        /// Postfix patch to draw selection preview after normal map interface update.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            // Render zone creation mode preview
            if (ZoneCreationState.IsInCreationMode)
            {
                // Draw preview cells using native green highlight material
                if (ZoneCreationState.PreviewCells.Count > 0)
                {
                    RenderCells(ZoneCreationState.PreviewCells.ToList(), DesignatorUtility.DragHighlightCellMat);
                }

                // Draw already-selected cells
                if (ZoneCreationState.SelectedCells.Count > 0)
                {
                    RenderCells(ZoneCreationState.SelectedCells, DesignatorUtility.DragHighlightCellMat);
                }

                // Draw rectangle outline if in preview mode
                if (ZoneCreationState.IsInPreviewMode)
                {
                    RenderRectangleOutline(ZoneCreationState.RectangleStart.Value,
                                           ZoneCreationState.RectangleEnd.Value);
                }
            }

            // Render area painting mode preview
            if (AreaPaintingState.IsActive)
            {
                // Draw preview cells using native green highlight material
                if (AreaPaintingState.PreviewCells.Count > 0)
                {
                    RenderCells(AreaPaintingState.PreviewCells.ToList(), DesignatorUtility.DragHighlightCellMat);
                }

                // Draw already-staged cells
                if (AreaPaintingState.StagedCells.Count > 0)
                {
                    RenderCells(AreaPaintingState.StagedCells, DesignatorUtility.DragHighlightCellMat);
                }

                // Draw rectangle outline if in preview mode
                if (AreaPaintingState.IsInPreviewMode)
                {
                    RenderRectangleOutline(AreaPaintingState.RectangleStart.Value,
                                           AreaPaintingState.RectangleEnd.Value);
                }
            }

            // Render architect mode placement preview (for cells that were toggled via Space in single-cell mode)
            if (ArchitectState.IsInPlacementMode)
            {
                // Draw already-selected cells (from single-cell toggle mode)
                if (ArchitectState.SelectedCells.Count > 0)
                {
                    RenderCells(ArchitectState.SelectedCells, DesignatorUtility.DragHighlightCellMat);
                }
            }

            // Render shape placement preview (two-point placement)
            if (ShapePlacementState.IsActive && ShapePlacementState.PreviewCells != null && ShapePlacementState.PreviewCells.Count > 0)
            {
                // Draw preview cells using native green highlight material
                RenderCells(ShapePlacementState.PreviewCells, DesignatorUtility.DragHighlightCellMat);

                // Draw rectangle outline connecting first point to current cursor
                if (ShapePlacementState.FirstPoint.HasValue)
                {
                    IntVec3 firstPoint = ShapePlacementState.FirstPoint.Value;
                    IntVec3 secondPoint = ShapePlacementState.SecondPoint ?? MapNavigationState.CurrentCursorPosition;
                    RenderRectangleOutline(firstPoint, secondPoint);
                }
            }

            // Render viewing mode highlights (after shape placement)
            if (ViewingModeState.IsActive)
            {
                // Capture reference once to avoid state changes during render
                var obstacleCells = ViewingModeState.ObstacleCells;

                // Highlight all obstacle cells in red
                if (obstacleCells != null && obstacleCells.Count > 0)
                {
                    GenDraw.DrawFieldEdges(obstacleCells, Color.red);

                    // Highlight current obstacle more prominently with yellow/bright color
                    // The current obstacle is tracked by ScannerState's temporary category
                    if (ScannerState.IsInTemporaryCategory())
                    {
                        IntVec3 currentObstacle = ScannerState.GetCurrentItemPosition();
                        if (currentObstacle.IsValid && obstacleCells.Contains(currentObstacle))
                        {
                            GenDraw.DrawFieldEdges(new List<IntVec3> { currentObstacle }, Color.yellow);
                        }
                    }
                }

                // Highlight placed blueprints with green edges for visibility
                if (ViewingModeState.PlacedBlueprints != null && ViewingModeState.PlacedBlueprints.Count > 0)
                {
                    List<IntVec3> blueprintCells = new List<IntVec3>();
                    foreach (Thing blueprint in ViewingModeState.PlacedBlueprints)
                    {
                        if (blueprint != null && !blueprint.Destroyed)
                        {
                            blueprintCells.Add(blueprint.Position);
                        }
                    }
                    if (blueprintCells.Count > 0)
                    {
                        GenDraw.DrawFieldEdges(blueprintCells, Color.green);
                    }
                }
            }
        }

        /// <summary>
        /// Renders cell highlights using native RimWorld materials.
        /// </summary>
        private static void RenderCells(IEnumerable<IntVec3> cells, Material material)
        {
            foreach (var cell in cells)
            {
                Vector3 pos = cell.ToVector3Shifted();
                pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                Graphics.DrawMesh(MeshPool.plane10, pos, Quaternion.identity, material, 0);
            }
        }

        /// <summary>
        /// Renders rectangle outline using native GenDraw.
        /// </summary>
        private static void RenderRectangleOutline(IntVec3 start, IntVec3 end)
        {
            // Calculate the rectangle bounds
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            // Use GenDraw.DrawFieldEdges for consistent look with native selection
            CellRect rect = CellRect.FromLimits(minX, minZ, maxX, maxZ);
            GenDraw.DrawFieldEdges(rect.Cells.ToList());
        }
    }
}
