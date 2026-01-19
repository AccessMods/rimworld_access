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
            if (des is Designator_Place)
            {
                RouteToAccessiblePlacement(des);
                return;
            }

            // Handle zone designators (ZoneAdd for expand, ZoneDelete for shrink)
            // These support shape-based placement just like building designators
            if (ShapeHelper.IsZoneDesignator(des))
            {
                RouteToAccessiblePlacement(des);
                return;
            }
        }

        /// <summary>
        /// Routes a designator to the accessible placement system.
        /// Always uses ShapePlacementState for consistent UX, even for single-cell buildings.
        /// Works with both placement designators (Build, Install) and zone designators.
        /// </summary>
        /// <param name="designator">The designator to route</param>
        private static void RouteToAccessiblePlacement(Designator designator)
        {
            var availableShapes = ShapeHelper.GetAvailableShapes(designator);
            ShapeType defaultShape = availableShapes.Count > 0 ? availableShapes[0] : ShapeType.Manual;
            ShapePlacementState.Enter(designator, defaultShape);
        }
    }
}
