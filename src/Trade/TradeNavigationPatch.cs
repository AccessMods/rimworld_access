using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patches for Dialog_Trade to enable keyboard navigation.
    /// Keeps the dialog open and provides keyboard-accessible trading.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_Trade))]
    public static class TradeNavigationPatch
    {
        /// <summary>
        /// Prefix patch for CaravanArrivalAction_Trade.Arrived to save view state
        /// BEFORE the game calls CameraJumper.TryJumpAndSelect() which switches the view.
        /// This ensures we can restore the user to their original location after trading.
        /// </summary>
        [HarmonyPatch(typeof(CaravanArrivalAction_Trade), "Arrived")]
        [HarmonyPrefix]
        public static void CaravanArrivalAction_Trade_Arrived_Prefix()
        {
            // Save the current view state before RimWorld switches it
            TradeNavigationState.SaveViewStateBeforeTrade();
        }

        /// <summary>
        /// Patch for Window.OnCancelKeyPressed to block the game's Escape key handling
        /// when overlay menus are active over the trade dialog.
        /// </summary>
        [HarmonyPatch(typeof(Window), "OnCancelKeyPressed")]
        [HarmonyPrefix]
        public static bool Window_OnCancelKeyPressed_Prefix(Window __instance)
        {
            // Only intercept for trade dialog
            if (__instance is Dialog_Trade)
            {
                // Always block the game's Cancel handling when our trade state is active
                // This ensures our UnifiedKeyboardPatch handler runs and announces properly
                if (TradeNavigationState.IsActive)
                {
                    return false; // Skip original method - let our handler close with announcement
                }
            }

            return true; // Let original method run
        }

        /// <summary>
        /// Patch for Window.OnAcceptKeyPressed to block the game's Enter key handling.
        /// By default, Window.closeOnAccept is true, which closes the dialog on Enter.
        /// We need to block this so our Enter key handling works.
        /// </summary>
        [HarmonyPatch(typeof(Window), "OnAcceptKeyPressed")]
        [HarmonyPrefix]
        public static bool Window_OnAcceptKeyPressed_Prefix(Window __instance)
        {
            // Only intercept for trade dialog when our state is active
            if (__instance is Dialog_Trade && TradeNavigationState.IsActive)
            {
                // Always block the game's Accept handling - we handle Enter ourselves
                return false;
            }

            return true; // Let original method run
        }

        /// <summary>
        /// Postfix patch that runs after Dialog_Trade.PostOpen().
        /// Opens the keyboard navigation interface while keeping the dialog visible.
        /// </summary>
        [HarmonyPatch("PostOpen")]
        [HarmonyPostfix]
        public static void PostOpen_Postfix(Dialog_Trade __instance)
        {
            try
            {
                // Verify TradeSession is active
                if (!TradeSession.Active)
                {
                    Log.Warning("RimWorld Access: TradeSession is not active when Dialog_Trade opened");
                    return;
                }

                // Open the keyboard navigation interface, passing the dialog reference
                TradeNavigationState.Open(__instance);
            }
            catch (System.Exception ex)
            {
                Log.Error($"RimWorld Access: Error initializing trade navigation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Prefix patch for Dialog_Trade.Close to clean up our state.
        /// </summary>
        [HarmonyPatch("Close")]
        [HarmonyPrefix]
        public static void Close_Prefix(Dialog_Trade __instance)
        {
            // Clean up our navigation state when the dialog closes
            if (TradeNavigationState.IsActive)
            {
                TradeNavigationState.OnDialogClosing();
            }
        }
    }
}
