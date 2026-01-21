using HarmonyLib;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Patches KeyBindingDef.JustPressed to block RimWorld's built-in search bar
    /// when our scanner search is active.
    /// </summary>
    [HarmonyPatch(typeof(KeyBindingDef))]
    [HarmonyPatch("JustPressed", MethodType.Getter)]
    public static class ScannerSearchPatch
    {
        /// <summary>
        /// Returns false for OpenMapSearch keybinding when scanner search is active.
        /// This prevents RimWorld's map/world search dialog from opening when
        /// the user presses Z to use our scanner search feature.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(KeyBindingDef __instance, ref bool __result)
        {
            // Only intercept the OpenMapSearch keybinding
            if (__instance == KeyBindingDefOf.OpenMapSearch && ScannerSearchState.IsActive)
            {
                __result = false;
                return false; // Skip original method
            }
            return true; // Run original method
        }
    }
}
