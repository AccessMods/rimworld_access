using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patches to enable keyboard accessibility for baby gene inspection.
    /// Intercepts when ITab_GenesPregnancy is opened and activates GeneInspectionState.
    /// </summary>
    public static class GeneInspectionPatch
    {
        /// <summary>
        /// Postfix patch for InspectPaneUtility.OpenTab to detect when ITab_GenesPregnancy opens.
        /// </summary>
        [HarmonyPatch(typeof(InspectPaneUtility), "OpenTab")]
        public static class OpenTab_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Type inspectTabType, InspectTabBase __result)
            {
                try
                {
                    // Check if this is the pregnancy genes tab
                    if (inspectTabType == typeof(ITab_GenesPregnancy) ||
                        (inspectTabType != null && typeof(ITab_GenesPregnancy).IsAssignableFrom(inspectTabType)))
                    {
                        // Get the currently selected pawn
                        var pawn = GetSelectedPregnantPawn();
                        if (pawn != null)
                        {
                            GeneInspectionState.Open(pawn);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[GeneInspectionPatch] Error in OpenTab postfix: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the currently selected pregnant pawn.
        /// </summary>
        private static Pawn GetSelectedPregnantPawn()
        {
            // Try to get from selector
            var selector = Find.Selector;
            if (selector?.SingleSelectedThing is Pawn pawn)
            {
                // Verify this pawn is pregnant
                if (HasPregnancyWithGenes(pawn))
                {
                    return pawn;
                }
            }

            // Fallback: check all selected objects
            if (selector?.SelectedObjects != null)
            {
                foreach (var obj in selector.SelectedObjects)
                {
                    if (obj is Pawn p && HasPregnancyWithGenes(p))
                    {
                        return p;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a pawn has a pregnancy hediff with genes.
        /// </summary>
        private static bool HasPregnancyWithGenes(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return false;

            return pawn.health.hediffSet.hediffs
                .OfType<HediffWithParents>()
                .Any(h => h.geneSet != null);
        }

        /// <summary>
        /// Patch to close GeneInspectionState when the inspect pane closes.
        /// We patch MainTabWindow_Inspect.CloseOpenTab to detect tab closure.
        /// </summary>
        [HarmonyPatch(typeof(MainTabWindow_Inspect), "CloseOpenTab")]
        public static class CloseOpenTab_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // Close our state when the tab is closed
                if (GeneInspectionState.IsActive)
                {
                    GeneInspectionState.Close();
                }
            }
        }

        /// <summary>
        /// Patch to close GeneInspectionState when the selection changes.
        /// </summary>
        [HarmonyPatch(typeof(Selector), "ClearSelection")]
        public static class ClearSelection_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (GeneInspectionState.IsActive)
                {
                    GeneInspectionState.Close();
                }
            }
        }
    }
}
