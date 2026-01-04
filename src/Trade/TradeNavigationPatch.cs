using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Patches TradeDeal.TryExecute to handle the case where Dialog_Trade doesn't exist.
    /// When we close the visual Dialog_Trade for windowless trading, RimWorld's TryExecute
    /// still expects the window to exist for calling FlashSilver() on affordability failures.
    /// This patch handles that case gracefully.
    /// </summary>
    [HarmonyPatch(typeof(TradeDeal), "TryExecute")]
    public static class TradeDealTryExecutePatch
    {
        /// <summary>
        /// Prefix that handles the affordability check when Dialog_Trade doesn't exist.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(TradeDeal __instance, ref bool actuallyTraded, ref bool __result)
        {
            // Only intervene if we're in windowless trade mode (Dialog_Trade is closed)
            if (!TradeNavigationState.IsActive)
                return true; // Let original method run

            // Check if Dialog_Trade exists - if it does, let original method handle everything
            Dialog_Trade tradeWindow = Find.WindowStack.WindowOfType<Dialog_Trade>();
            if (tradeWindow != null)
                return true; // Let original method run

            // Dialog_Trade doesn't exist - we need to handle the affordability check ourselves
            // to prevent null reference when TryExecute tries to call FlashSilver()

            // Check gift mode first (handled differently)
            if (TradeSession.giftMode)
                return true; // Let original method run - gift mode doesn't use FlashSilver

            // Check affordability
            Tradeable currencyTradeable = __instance.CurrencyTradeable;
            if (currencyTradeable == null || currencyTradeable.CountPostDealFor(Transactor.Colony) < 0)
            {
                // Colony can't afford - announce via screen reader instead of flashing silver
                TolkHelper.Speak("Cannot complete trade: Colony cannot afford this trade", SpeechPriority.High);
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                actuallyTraded = false;
                __result = false;
                return false; // Skip original method
            }

            // Affordability check passed - let original method continue
            return true;
        }
    }

    /// <summary>
    /// Patches Dialog_Trade to intercept and replace it with the windowless trade interface.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_Trade), "PostOpen")]
    public static class TradeNavigationPatch_PostOpen
    {
        private static bool hasIntercepted = false;

        /// <summary>
        /// Postfix patch that runs after Dialog_Trade.PostOpen().
        /// Closes the visual dialog and opens the windowless trade interface.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(Dialog_Trade __instance)
        {
            // Only intercept once per dialog opening
            if (hasIntercepted)
                return;

            hasIntercepted = true;

            try
            {
                // Verify TradeSession is active
                if (!TradeSession.Active)
                {
                    Log.Warning("RimWorld Access: TradeSession is not active when Dialog_Trade opened");
                    return;
                }

                // Close the visual dialog immediately
                __instance.Close(doCloseSound: false);

                // Open the windowless trade interface
                TradeNavigationState.Open();
            }
            catch (System.Exception ex)
            {
                Log.Error($"RimWorld Access: Error intercepting trade dialog: {ex.Message}\n{ex.StackTrace}");
                hasIntercepted = false;
            }
        }

        /// <summary>
        /// Gets whether we've intercepted the current dialog.
        /// </summary>
        public static bool HasIntercepted => hasIntercepted;

        /// <summary>
        /// Resets the interception flag (called from PostClose patch).
        /// </summary>
        public static void ResetInterception()
        {
            hasIntercepted = false;
        }
    }

    /// <summary>
    /// Patches Dialog_Trade.Close to reset interception flag.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_Trade), "Close")]
    public static class TradeNavigationPatch_Close
    {
        /// <summary>
        /// Reset the interception flag when a dialog closes.
        /// This ensures we can intercept the next trade dialog.
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix()
        {
            TradeNavigationPatch_PostOpen.ResetInterception();
        }
    }
}
