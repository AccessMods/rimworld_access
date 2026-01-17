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

            // Only intercept placement designators (Designator_Build, Designator_Install, Designator_Place)
            if (!(des is Designator_Place placeDesignator))
            {
                return;
            }

            // Route to accessible placement
            RouteToAccessiblePlacement(placeDesignator);
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
    }
}
