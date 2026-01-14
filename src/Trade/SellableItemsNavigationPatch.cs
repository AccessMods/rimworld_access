using HarmonyLib;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patches for Dialog_SellableItems to enable keyboard navigation.
    /// This dialog shows what items a settlement trader will buy.
    /// Note: Dialog_SellableItems doesn't override PostOpen/Close, so we patch Window directly.
    /// </summary>
    [HarmonyPatch]
    public static class SellableItemsNavigationPatch
    {
        /// <summary>
        /// Patch for Window.OnCancelKeyPressed to block the game's Escape key handling
        /// when our state is active.
        /// </summary>
        [HarmonyPatch(typeof(Window), "OnCancelKeyPressed")]
        [HarmonyPrefix]
        public static bool Window_OnCancelKeyPressed_Prefix(Window __instance)
        {
            // Only intercept for sellable items dialog
            if (__instance is Dialog_SellableItems)
            {
                // Block the game's Cancel handling when our state is active
                if (SellableItemsState.IsActive)
                {
                    return false; // Skip original method - let our handler close with announcement
                }
            }

            return true; // Let original method run
        }

        /// <summary>
        /// Postfix patch that runs after Window.PostOpen().
        /// Opens the keyboard navigation interface when Dialog_SellableItems opens.
        /// </summary>
        [HarmonyPatch(typeof(Window), "PostOpen")]
        [HarmonyPostfix]
        public static void Window_PostOpen_Postfix(Window __instance)
        {
            // Only handle Dialog_SellableItems
            if (__instance is Dialog_SellableItems sellableItemsDialog)
            {
                try
                {
                    SellableItemsState.Open(sellableItemsDialog);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"RimWorld Access: Error initializing sellable items navigation: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Prefix patch for Window.Close to clean up our state when Dialog_SellableItems closes.
        /// </summary>
        [HarmonyPatch(typeof(Window), "Close")]
        [HarmonyPrefix]
        public static void Window_Close_Prefix(Window __instance)
        {
            // Only handle Dialog_SellableItems
            if (__instance is Dialog_SellableItems)
            {
                // Clean up our navigation state when the dialog closes
                if (SellableItemsState.IsActive)
                {
                    SellableItemsState.OnDialogClosing();
                }
            }
        }
    }
}
