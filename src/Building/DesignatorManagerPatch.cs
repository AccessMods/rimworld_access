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
        /// Announces what's being placed and enters the appropriate placement mode.
        /// </summary>
        /// <param name="designator">The placement designator from the gizmo</param>
        private static void RouteToAccessiblePlacement(Designator_Place designator)
        {
            // Get the item name for announcement
            string itemName = ArchitectHelper.GetSanitizedLabel(designator);
            BuildableDef placingDef = designator.PlacingDef;

            // Build announcement based on what's being placed
            string announcement;

            // Check if shapes are available for this designator
            var availableShapes = ShapeHelper.GetAvailableShapes(designator);
            bool supportsShapes = availableShapes.Count > 1; // More than just Manual

            if (supportsShapes)
            {
                // Use the game's default shape (first in the list)
                ShapeType defaultShape = availableShapes[0];
                string shapeName = ShapeHelper.GetShapeName(defaultShape);

                // Enter ShapePlacementState with the default shape
                // Note: We don't use ArchitectState here as this is a standalone gizmo placement
                ShapePlacementState.Enter(designator, defaultShape);

                // The Enter method handles the announcement, so we're done
                Log.Message($"[DesignatorManagerPatch] Routed gizmo placement to shape mode: {itemName} with {shapeName}");
            }
            else
            {
                // Manual mode - single item placement
                // Get size and rotation info for the announcement
                string sizeInfo = "";
                string rotationInfo = "";

                if (placingDef != null)
                {
                    IntVec2 size = placingDef.Size;
                    Rot4 rotation = Rot4.North;

                    // Get current rotation from the designator via reflection
                    var rotField = AccessTools.Field(typeof(Designator_Place), "placingRot");
                    if (rotField != null)
                    {
                        rotation = (Rot4)rotField.GetValue(designator);
                    }

                    sizeInfo = ArchitectState.GetSizeDescription(size, rotation);
                    rotationInfo = $"Facing {ArchitectState.GetRotationName(rotation)}";
                }

                // Build the announcement
                if (!string.IsNullOrEmpty(sizeInfo) && !string.IsNullOrEmpty(rotationInfo))
                {
                    announcement = $"Placing {itemName}. {sizeInfo}. {rotationInfo}. Move to position and press Space to place, R to rotate, Escape to cancel.";
                }
                else
                {
                    announcement = $"Placing {itemName}. Move to position and press Space to place, R to rotate, Escape to cancel.";
                }

                TolkHelper.Speak(announcement);
                Log.Message($"[DesignatorManagerPatch] Routed gizmo placement to manual mode: {itemName}");
            }
        }

        /// <summary>
        /// Routes a zone designator (expand/shrink) to the accessible system.
        /// Zone designators support shape-based selection just like building designators.
        /// </summary>
        /// <param name="designator">The zone designator</param>
        private static void RouteZoneToAccessiblePlacement(Designator designator)
        {
            // Get the zone name for announcement
            string zoneName = ArchitectHelper.GetSanitizedLabel(designator);

            // Check if shapes are available for this designator
            var availableShapes = ShapeHelper.GetAvailableShapes(designator);
            bool supportsShapes = availableShapes.Count > 1; // More than just Manual

            if (supportsShapes)
            {
                // Use the game's default shape (first in the list)
                ShapeType defaultShape = availableShapes[0];
                string shapeName = ShapeHelper.GetShapeName(defaultShape);

                // Enter ShapePlacementState with the default shape
                ShapePlacementState.Enter(designator, defaultShape);

                // The Enter method handles the announcement
                Log.Message($"[DesignatorManagerPatch] Routed zone designator to shape mode: {zoneName} with {shapeName}");
            }
            else
            {
                // Manual mode for zones - check if expanding or shrinking
                string operation = ShapeHelper.IsDeleteDesignator(designator) ? "Shrinking" : "Expanding";
                TolkHelper.Speak($"{operation} {zoneName}. Space to set corners, Enter to confirm, Escape to cancel.");
                Log.Message($"[DesignatorManagerPatch] Routed zone designator to manual mode: {zoneName}");
            }
        }
    }
}
