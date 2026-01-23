using HarmonyLib;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patches for the Scenario Builder (Page_ScenarioEditor).
    /// Initializes and closes the ScenarioBuilderState when the editor opens/closes.
    /// </summary>
    [HarmonyPatch(typeof(Page_ScenarioEditor))]
    public static class ScenarioBuilderPatch
    {
        /// <summary>
        /// Initialize state when the scenario editor opens.
        /// Page_ScenarioEditor overrides PreOpen, so we patch that.
        /// </summary>
        [HarmonyPatch("PreOpen")]
        [HarmonyPostfix]
        public static void PreOpen_Postfix(Page_ScenarioEditor __instance)
        {
            try
            {
                // Get the current scenario from the editor
                Scenario scenario = (Scenario)AccessTools.Field(typeof(Page_ScenarioEditor), "curScen").GetValue(__instance);

                // Initialize the scenario builder state
                ScenarioBuilderState.Open(scenario, __instance);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimWorld Access] Error in ScenarioBuilderPatch PreOpen: {ex}");
            }
        }
    }

    /// <summary>
    /// Patch Page.DoNext to block page advancement when our scenario builder states are active.
    /// CRITICAL: This is the most direct way to block advancement because:
    /// - OnAcceptKeyPressed calls DoNext()
    /// - DoBottomButtons ALSO checks KeyBindingDefOf.Accept.KeyDownEvent and calls DoNext()
    /// - Patching DoNext() catches BOTH paths
    /// </summary>
    [HarmonyPatch(typeof(Page))]
    [HarmonyPatch("DoNext")]
    public static class ScenarioBuilderDoNextPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Page __instance)
        {
            // Only intercept for Page_ScenarioEditor
            if (__instance is Page_ScenarioEditor)
            {
                // Block page advancement when ANY of our scenario builder states are active
                if (ScenarioBuilderState.IsActive ||
                    ScenarioBuilderPartEditState.IsActive ||
                    ScenarioBuilderAddPartState.IsActive ||
                    WindowlessScenarioLoadState.IsActive ||
                    WindowlessScenarioSaveState.IsActive ||
                    WindowlessScenarioDeleteConfirmState.IsActive)
                {
                    return false; // Block DoNext() - stay on scenario builder
                }
            }
            return true; // Let original method run (advances to next page normally)
        }
    }

    /// <summary>
    /// Patch Window.PostClose to detect when Page_ScenarioEditor closes.
    /// We patch Window because Page_ScenarioEditor doesn't override PostClose.
    /// </summary>
    [HarmonyPatch(typeof(Window))]
    [HarmonyPatch("PostClose")]
    public static class ScenarioBuilderClosePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Window __instance)
        {
            try
            {
                // Only handle Page_ScenarioEditor
                if (__instance is Page_ScenarioEditor && ScenarioBuilderState.IsActive)
                {
                    ScenarioBuilderState.Close();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimWorld Access] Error in ScenarioBuilderClosePatch: {ex}");
            }
        }
    }

    /// <summary>
    /// Patch Window.OnCancelKeyPressed to block Escape key from closing the editor.
    /// We want Escape to be handled by our state (to close overlays first).
    /// </summary>
    [HarmonyPatch(typeof(Window))]
    [HarmonyPatch("OnCancelKeyPressed")]
    public static class ScenarioBuilderCancelKeyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Window __instance)
        {
            // Only intercept for Page_ScenarioEditor
            if (__instance is Page_ScenarioEditor)
            {
                // Block the game's Cancel handling when our overlay states are active
                if (ScenarioBuilderPartEditState.IsActive ||
                    ScenarioBuilderAddPartState.IsActive ||
                    WindowlessScenarioLoadState.IsActive ||
                    WindowlessScenarioSaveState.IsActive ||
                    WindowlessScenarioDeleteConfirmState.IsActive)
                {
                    return false; // Skip original method - let our overlay handle the Escape
                }
            }

            return true; // Let original method run
        }
    }
}
