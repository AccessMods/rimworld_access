using System.Collections.Generic;
using HarmonyLib;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch on DesignatorManager.Select() to intercept ALL placements
    /// and route them through the accessible placement system.
    ///
    /// This is the SINGLE entry point for accessible placement mode. All placements
    /// (whether from architect menu, gizmos, or other sources) flow through here.
    ///
    /// This patch provides:
    /// - Screen reader announcements for what is being placed
    /// - Keyboard navigation via ShapePlacementState
    /// - Proper integration with the accessible architect system
    /// </summary>
    [HarmonyPatch(typeof(DesignatorManager))]
    [HarmonyPatch("Select")]
    public static class DesignatorManagerPatch
    {
        /// <summary>
        /// Postfix patch that intercepts designator selection and routes placement
        /// designators through the accessible system.
        /// </summary>
        /// <param name="des">The designator being selected</param>
        [HarmonyPostfix]
        public static void Postfix(Designator des)
        {
            // Skip if ShapePlacementState is already active
            if (ShapePlacementState.IsActive)
            {
                return;
            }

            // Handle Designator_Place (Build, Install, etc.)
            if (des is Designator_Place placeDesignator)
            {
                RouteToAccessiblePlacement(placeDesignator);
                return;
            }

            // Handle zone designators (ZoneAdd for expand, ZoneDelete for shrink)
            // These support shape-based placement just like building designators
            if (ShapeHelper.IsZoneDesignator(des))
            {
                RouteZoneToAccessiblePlacement(des);
                return;
            }
        }

        /// <summary>
        /// Routes a gizmo-triggered placement designator to the accessible system.
        /// Always uses ShapePlacementState for consistent UX, even for single-cell buildings.
        /// </summary>
        /// <param name="designator">The placement designator from the gizmo</param>
        private static void RouteToAccessiblePlacement(Designator_Place designator)
        {
            // Get the item name for announcement
            string itemName = ArchitectHelper.GetSanitizedLabel(designator);

            // Get available shapes - always use the first (default) shape
            var availableShapes = ShapeHelper.GetAvailableShapes(designator);
            ShapeType defaultShape = availableShapes.Count > 0 ? availableShapes[0] : ShapeType.Manual;
            string shapeName = ShapeHelper.GetShapeName(defaultShape);

            // Always enter ShapePlacementState for consistent UX
            // Note: We don't use ArchitectState here as this is a standalone gizmo placement
            ShapePlacementState.Enter(designator, defaultShape);

            // The Enter method handles the announcement, so we're done
            Log.Message($"[DesignatorManagerPatch] Routed gizmo placement to ShapePlacementState: {itemName} with {shapeName}");
        }

        /// <summary>
        /// Routes a zone designator (expand/shrink) to the accessible system.
        /// Always uses ShapePlacementState for consistent UX, even when only Manual shape is available.
        /// </summary>
        /// <param name="designator">The zone designator</param>
        private static void RouteZoneToAccessiblePlacement(Designator designator)
        {
            // Get the zone name for announcement
            string zoneName = ArchitectHelper.GetSanitizedLabel(designator);

            // Get available shapes - always use the first (default) shape
            var availableShapes = ShapeHelper.GetAvailableShapes(designator);
            ShapeType defaultShape = availableShapes.Count > 0 ? availableShapes[0] : ShapeType.Manual;
            string shapeName = ShapeHelper.GetShapeName(defaultShape);

            // Always enter ShapePlacementState for consistent UX
            ShapePlacementState.Enter(designator, defaultShape);

            // The Enter method handles the announcement
            Log.Message($"[DesignatorManagerPatch] Routed zone designator to ShapePlacementState: {zoneName} with {shapeName}");
        }
    }
}
