using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// State management for keyboard navigation in Dialog_FormCaravan.
    /// Provides three-tab interface for selecting pawns, items, and travel supplies.
    /// </summary>
    public static class CaravanFormationState
    {
        private enum Tab
        {
            Pawns,
            Items,
            TravelSupplies
        }

        private static bool isActive = false;
        private static Dialog_FormCaravan currentDialog = null;
        private static Tab currentTab = Tab.Pawns;
        private static int selectedIndex = 0;
        private static bool isChoosingDestination = false;

        /// <summary>
        /// Gets whether caravan formation keyboard navigation is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Gets whether we're currently choosing a destination for the caravan.
        /// </summary>
        public static bool IsChoosingDestination => isChoosingDestination;

        /// <summary>
        /// Triggers caravan reformation from the current map (for temporary encounter maps after ambushes).
        /// This checks if the current map is a temporary map and opens Dialog_FormCaravan with reform=true.
        /// </summary>
        public static void TriggerReformation()
        {
            Map currentMap = Find.CurrentMap;

            if (currentMap == null)
            {
                TolkHelper.Speak("No map loaded", SpeechPriority.High);
                return;
            }

            // Check if this is a temporary map that requires reformation
            if (currentMap.IsPlayerHome)
            {
                TolkHelper.Speak("Cannot reform caravan from home settlement. Use world map to form new caravans.", SpeechPriority.High);
                return;
            }

            // Get the FormCaravanComp from the map's parent world object
            MapParent mapParent = currentMap.Parent;
            if (mapParent == null)
            {
                TolkHelper.Speak("Map has no parent world object", SpeechPriority.High);
                return;
            }

            FormCaravanComp formCaravanComp = mapParent.GetComponent<FormCaravanComp>();
            if (formCaravanComp == null)
            {
                TolkHelper.Speak("This map does not support caravan reformation", SpeechPriority.High);
                return;
            }

            // Check if reformation is allowed (no active threats, etc.)
            if (!formCaravanComp.CanFormOrReformCaravanNow)
            {
                // Try to get the reason why reformation is blocked
                if (GenHostility.AnyHostileActiveThreatToPlayer(currentMap, countDormantPawnsAsHostile: false))
                {
                    TolkHelper.Speak("Cannot reform caravan while enemies are present", SpeechPriority.High);
                }
                else
                {
                    TolkHelper.Speak("Cannot reform caravan at this time", SpeechPriority.High);
                }
                return;
            }

            // Open Dialog_FormCaravan in reform mode
            Dialog_FormCaravan reformDialog = new Dialog_FormCaravan(currentMap, reform: true);
            Find.WindowStack.Add(reformDialog);

            TolkHelper.Speak("Opening caravan reformation dialog", SpeechPriority.Normal);
        }

        /// <summary>
        /// Opens keyboard navigation for the specified Dialog_FormCaravan.
        /// </summary>
        public static void Open(Dialog_FormCaravan dialog)
        {
            if (dialog == null)
            {
                TolkHelper.Speak("No caravan formation dialog available", SpeechPriority.High);
                return;
            }

            isActive = true;
            currentDialog = dialog;
            currentTab = Tab.Pawns;
            selectedIndex = 0;

            // Disable auto-select travel supplies to prevent it from resetting our manual selections
            DisableAutoSelectTravelSupplies();

            TolkHelper.Speak("Caravan formation dialog opened. Use Left/Right to switch tabs, Up/Down to navigate, +/- or Enter to adjust. Press D to choose destination, navigate with arrows and press Enter to confirm. Press T to send, R to reset, Escape to cancel.");
            AnnounceCurrentTab();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Closes keyboard navigation.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentDialog = null;
            currentTab = Tab.Pawns;
            selectedIndex = 0;
        }

        /// <summary>
        /// Gets the transferables list from the current dialog using reflection.
        /// </summary>
        private static List<TransferableOneWay> GetTransferables()
        {
            if (currentDialog == null)
                return new List<TransferableOneWay>();

            try
            {
                FieldInfo field = AccessTools.Field(typeof(Dialog_FormCaravan), "transferables");
                if (field != null)
                {
                    var result = field.GetValue(currentDialog);
                    if (result is List<TransferableOneWay> transferables)
                    {
                        return transferables;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"RimWorld Access: Failed to get transferables from Dialog_FormCaravan: {ex.Message}");
            }

            return new List<TransferableOneWay>();
        }

        /// <summary>
        /// Gets transferables for the current tab.
        /// </summary>
        private static List<TransferableOneWay> GetCurrentTabTransferables()
        {
            List<TransferableOneWay> allTransferables = GetTransferables();

            switch (currentTab)
            {
                case Tab.Pawns:
                    // Pawns: anything that's a Pawn
                    return allTransferables
                        .Where(t => t.ThingDef.category == ThingCategory.Pawn)
                        .ToList();

                case Tab.TravelSupplies:
                    // Travel Supplies: use RimWorld's official filtering logic from CaravanUIUtility.GetTransferableCategory
                    return allTransferables
                        .Where(t => GetTransferableCategory(t) == TransferableCategory.TravelSupplies)
                        .ToList();

                case Tab.Items:
                    // Items: everything that's not a pawn and not travel supplies
                    return allTransferables
                        .Where(t => GetTransferableCategory(t) == TransferableCategory.Item)
                        .ToList();

                default:
                    return allTransferables;
            }
        }

        /// <summary>
        /// Replicates RimWorld's CaravanUIUtility.GetTransferableCategory logic.
        /// This determines whether an item is a Pawn, Travel Supply, or regular Item.
        /// </summary>
        private static TransferableCategory GetTransferableCategory(TransferableOneWay t)
        {
            if (t.ThingDef.category == ThingCategory.Pawn)
            {
                return TransferableCategory.Pawn;
            }

            // Travel Supplies include:
            // 1. Medicine (in the Medicine thing category)
            // 2. Food (ingestible, not drug, not corpse, not tree)
            // 3. Bedrolls (beds that caravans can use)
            if ((!t.ThingDef.thingCategories.NullOrEmpty() && t.ThingDef.thingCategories.Contains(ThingCategoryDefOf.Medicine)) ||
                (t.ThingDef.IsIngestible && !t.ThingDef.IsDrug && !t.ThingDef.IsCorpse && (t.ThingDef.plant == null || !t.ThingDef.plant.IsTree)) ||
                (t.AnyThing.GetInnerIfMinified().def.IsBed && t.AnyThing.GetInnerIfMinified().def.building != null && t.AnyThing.GetInnerIfMinified().def.building.bed_caravansCanUse))
            {
                return TransferableCategory.TravelSupplies;
            }

            return TransferableCategory.Item;
        }

        /// <summary>
        /// Enum matching RimWorld's internal TransferableCategory enum.
        /// </summary>
        private enum TransferableCategory
        {
            Pawn,
            Item,
            TravelSupplies
        }

        /// <summary>
        /// Announces the current tab.
        /// </summary>
        private static void AnnounceCurrentTab()
        {
            string tabName = "";
            switch (currentTab)
            {
                case Tab.Pawns:
                    tabName = "Pawns tab";
                    break;
                case Tab.Items:
                    tabName = "Items tab";
                    break;
                case Tab.TravelSupplies:
                    tabName = "Travel Supplies tab";
                    break;
            }

            List<TransferableOneWay> tabTransferables = GetCurrentTabTransferables();
            TolkHelper.Speak($"{tabName}, {tabTransferables.Count} items");
        }

        /// <summary>
        /// Announces the currently selected item.
        /// </summary>
        private static void AnnounceCurrentItem()
        {
            List<TransferableOneWay> transferables = GetCurrentTabTransferables();

            if (transferables.Count == 0)
            {
                TolkHelper.Speak("No items in this tab");
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= transferables.Count)
            {
                selectedIndex = 0;
            }

            TransferableOneWay transferable = transferables[selectedIndex];
            int position = selectedIndex + 1;
            int total = transferables.Count;

            StringBuilder announcement = new StringBuilder();
            announcement.Append($"{position} of {total}: ");

            if (transferable.AnyThing is Pawn pawn)
            {
                // Pawn announcement
                announcement.Append(pawn.LabelShortCap);

                if (pawn.story != null && !pawn.story.TitleCap.NullOrEmpty())
                {
                    announcement.Append($", {pawn.story.TitleCap}");
                }

                if (transferable.CountToTransfer > 0)
                {
                    announcement.Append(" - Selected");
                }
                else
                {
                    announcement.Append(" - Not selected");
                }
            }
            else
            {
                // Item announcement
                announcement.Append(transferable.LabelCap);

                int current = transferable.CountToTransfer;
                int max = transferable.GetMaximumToTransfer();

                announcement.Append($" - {current} of {max}");

                // Add mass information if significant
                if (current > 0)
                {
                    float totalMass = transferable.AnyThing.GetStatValue(StatDefOf.Mass) * current;
                    if (totalMass >= 1f)
                    {
                        announcement.Append($", {totalMass:F1} kg");
                    }
                }
            }

            TolkHelper.Speak(announcement.ToString());
        }

        /// <summary>
        /// Selects the next item in the current tab.
        /// </summary>
        public static void SelectNext()
        {
            List<TransferableOneWay> transferables = GetCurrentTabTransferables();

            if (transferables.Count == 0)
            {
                TolkHelper.Speak("No items in this tab");
                return;
            }

            selectedIndex++;
            if (selectedIndex >= transferables.Count)
                selectedIndex = 0;

            AnnounceCurrentItem();
        }

        /// <summary>
        /// Selects the previous item in the current tab.
        /// </summary>
        public static void SelectPrevious()
        {
            List<TransferableOneWay> transferables = GetCurrentTabTransferables();

            if (transferables.Count == 0)
            {
                TolkHelper.Speak("No items in this tab");
                return;
            }

            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = transferables.Count - 1;

            AnnounceCurrentItem();
        }

        /// <summary>
        /// Switches to the next tab.
        /// </summary>
        public static void NextTab()
        {
            currentTab = (Tab)(((int)currentTab + 1) % 3);
            selectedIndex = 0;
            AnnounceCurrentTab();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Switches to the previous tab.
        /// </summary>
        public static void PreviousTab()
        {
            currentTab = (Tab)(((int)currentTab + 2) % 3);
            selectedIndex = 0;
            AnnounceCurrentTab();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Adjusts the quantity of the selected item.
        /// </summary>
        public static void AdjustQuantity(int delta)
        {
            List<TransferableOneWay> transferables = GetCurrentTabTransferables();

            if (transferables.Count == 0)
            {
                TolkHelper.Speak("No items in this tab");
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= transferables.Count)
                return;

            TransferableOneWay transferable = transferables[selectedIndex];

            if (transferable.AnyThing is Pawn)
            {
                // For pawns, toggle selection (0 or max)
                if (transferable.CountToTransfer > 0)
                {
                    transferable.AdjustTo(0);
                    TolkHelper.Speak("Deselected");
                }
                else
                {
                    int max = transferable.GetMaximumToTransfer();
                    transferable.AdjustTo(max);
                    TolkHelper.Speak("Selected");
                }
            }
            else
            {
                // For items, adjust by delta
                // Check if the item is interactive first
                if (!transferable.Interactive)
                {
                    TolkHelper.Speak("This item cannot be adjusted");
                    return;
                }

                AcceptanceReport canAdjust = transferable.CanAdjustBy(delta);
                if (canAdjust.Accepted)
                {
                    transferable.AdjustBy(delta);
                    NotifyTransferablesChanged();
                    AnnounceCurrentItem();
                }
                else
                {
                    // Report the specific reason why adjustment failed
                    string reason = canAdjust.Reason.NullOrEmpty() ? "Cannot adjust quantity" : canAdjust.Reason;
                    TolkHelper.Speak(reason);
                }
            }

            NotifyTransferablesChanged();
        }

        /// <summary>
        /// Toggles selection for the current item (same as Enter key).
        /// </summary>
        public static void ToggleSelection()
        {
            List<TransferableOneWay> transferables = GetCurrentTabTransferables();

            if (transferables.Count == 0)
            {
                TolkHelper.Speak("No items in this tab");
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= transferables.Count)
                return;

            TransferableOneWay transferable = transferables[selectedIndex];

            if (transferable.AnyThing is Pawn)
            {
                // For pawns, toggle between 0 and max
                if (transferable.CountToTransfer > 0)
                {
                    transferable.AdjustTo(0);
                    TolkHelper.Speak($"Deselected {transferable.LabelCap}");
                }
                else
                {
                    int max = transferable.GetMaximumToTransfer();
                    transferable.AdjustTo(max);
                    TolkHelper.Speak($"Selected {transferable.LabelCap}");
                }
            }
            else
            {
                // For items, increment by 1
                // Check if the item is interactive first
                if (!transferable.Interactive)
                {
                    TolkHelper.Speak("This item cannot be adjusted");
                    return;
                }

                AcceptanceReport canAdjust = transferable.CanAdjustBy(1);
                if (canAdjust.Accepted)
                {
                    transferable.AdjustBy(1);
                    NotifyTransferablesChanged();
                    AnnounceCurrentItem();
                }
                else
                {
                    // Report the specific reason why adjustment failed
                    string reason = canAdjust.Reason.NullOrEmpty() ? "Cannot increase quantity" : canAdjust.Reason;
                    TolkHelper.Speak(reason);
                }
            }

            NotifyTransferablesChanged();
        }

        /// <summary>
        /// Opens the route planner to choose a destination for the caravan.
        /// Switches to world view and enables destination selection mode.
        /// </summary>
        public static void ChooseRoute()
        {
            if (currentDialog == null)
            {
                TolkHelper.Speak("No dialog available", SpeechPriority.High);
                return;
            }

            try
            {
                // Close the dialog temporarily (don't clear currentDialog - we need it to return)
                if (Find.WindowStack != null)
                {
                    Find.WindowStack.TryRemove(currentDialog, doCloseSound: false);
                }

                // Switch to world view
                CameraJumper.TryShowWorld();

                // Enter destination selection mode
                isChoosingDestination = true;

                // Make sure world navigation is active
                if (!WorldNavigationState.IsActive)
                {
                    WorldNavigationState.Open();
                }

                TolkHelper.Speak("Choosing caravan destination. Use arrow keys to navigate the world map, Enter to select destination, or Escape to cancel.");
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Failed to open route planner: {ex.Message}", SpeechPriority.High);
                Log.Error($"RimWorld Access: Failed to start route planner: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the destination for the caravan and returns to the formation dialog.
        /// </summary>
        public static void SetDestination(PlanetTile destinationTile)
        {
            if (currentDialog == null)
            {
                TolkHelper.Speak("No dialog available", SpeechPriority.High);
                isChoosingDestination = false;
                return;
            }

            try
            {
                // Call Notify_ChoseRoute to properly set destination and calculate exit tile
                MethodInfo notifyChoseRouteMethod = AccessTools.Method(typeof(Dialog_FormCaravan), "Notify_ChoseRoute");
                if (notifyChoseRouteMethod == null)
                {
                    TolkHelper.Speak("Failed to access Notify_ChoseRoute method", SpeechPriority.High);
                    isChoosingDestination = false;
                    return;
                }

                // This sets destinationTile, calculates startingTile, and updates other state
                notifyChoseRouteMethod.Invoke(currentDialog, new object[] { destinationTile });

                // Also set the route via the world route planner for visual feedback
                if (Find.WorldRoutePlanner != null)
                {
                    Find.WorldRoutePlanner.Start(currentDialog);
                    Find.WorldRoutePlanner.TryAddWaypoint(destinationTile, true);
                    Find.WorldRoutePlanner.Stop();
                }

                // Return to map view
                CameraJumper.TryHideWorld();

                // Reopen the dialog
                if (Find.WindowStack != null)
                {
                    Find.WindowStack.Add(currentDialog);
                }

                // Exit destination selection mode
                isChoosingDestination = false;

                // Announce destination set
                string tileInfo = WorldInfoHelper.GetTileSummary(destinationTile);
                TolkHelper.Speak($"Destination set to {tileInfo}. Returning to caravan formation dialog.");
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Failed to set destination: {ex.Message}", SpeechPriority.High);
                Log.Error($"RimWorld Access: Failed to set caravan destination: {ex.Message}");
                isChoosingDestination = false;
            }
        }

        /// <summary>
        /// Cancels destination selection and returns to the formation dialog.
        /// </summary>
        public static void CancelDestinationSelection()
        {
            if (currentDialog == null)
            {
                isChoosingDestination = false;
                return;
            }

            try
            {
                // Return to map view
                CameraJumper.TryHideWorld();

                // Reopen the dialog
                if (Find.WindowStack != null)
                {
                    Find.WindowStack.Add(currentDialog);
                }

                // Exit destination selection mode
                isChoosingDestination = false;

                TolkHelper.Speak("Destination selection cancelled. Returning to caravan formation dialog.");
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Failed to cancel destination selection: {ex.Message}", SpeechPriority.High);
                Log.Error($"RimWorld Access: Failed to cancel destination selection: {ex.Message}");
                isChoosingDestination = false;
            }
        }

        /// <summary>
        /// Attempts to send the caravan by calling Dialog_FormCaravan.TrySend() via reflection.
        /// </summary>
        public static void Send()
        {
            if (currentDialog == null)
            {
                TolkHelper.Speak("No dialog available", SpeechPriority.High);
                return;
            }

            try
            {
                MethodInfo method = AccessTools.Method(typeof(Dialog_FormCaravan), "TrySend");
                if (method != null)
                {
                    TolkHelper.Speak("Attempting to send caravan...");

                    // Temporarily deactivate keyboard navigation so confirmation dialogs can be accessed
                    // (TrySend may show "low food" or other warnings that require confirmation)
                    bool wasActive = isActive;
                    isActive = false;

                    method.Invoke(currentDialog, null);

                    // If the dialog is still in the window stack, reactivate keyboard navigation
                    // (This happens when a confirmation dialog is shown)
                    // If the dialog closed successfully, PostClose will have been called already
                    if (currentDialog != null && Find.WindowStack != null && Find.WindowStack.IsOpen(currentDialog))
                    {
                        isActive = wasActive;
                    }
                    // TrySend will show error messages if validation fails
                    // If successful, the dialog will close and CaravanFormationPatch.PostClose will be called
                }
                else
                {
                    TolkHelper.Speak("Failed to send caravan - method not found", SpeechPriority.High);
                }
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Failed to send caravan: {ex.Message}", SpeechPriority.High);
                Log.Error($"RimWorld Access: Failed to call TrySend on Dialog_FormCaravan: {ex.Message}");
                // Reactivate on error
                if (currentDialog != null)
                {
                    isActive = true;
                }
            }
        }

        /// <summary>
        /// Resets all selections by calling Dialog_FormCaravan.CalculateAndRecacheTransferables() via reflection.
        /// </summary>
        public static void Reset()
        {
            if (currentDialog == null)
            {
                TolkHelper.Speak("No dialog available", SpeechPriority.High);
                return;
            }

            try
            {
                MethodInfo method = AccessTools.Method(typeof(Dialog_FormCaravan), "CalculateAndRecacheTransferables");
                if (method != null)
                {
                    method.Invoke(currentDialog, null);
                    selectedIndex = 0;
                    TolkHelper.Speak("Selections reset");
                    AnnounceCurrentItem();
                }
                else
                {
                    TolkHelper.Speak("Failed to reset - method not found", SpeechPriority.High);
                }
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Failed to reset: {ex.Message}", SpeechPriority.High);
                Log.Error($"RimWorld Access: Failed to call CalculateAndRecacheTransferables: {ex.Message}");
            }
        }

        /// <summary>
        /// Disables auto-select travel supplies feature to prevent it from resetting manual selections.
        /// </summary>
        private static void DisableAutoSelectTravelSupplies()
        {
            if (currentDialog == null)
                return;

            try
            {
                FieldInfo field = AccessTools.Field(typeof(Dialog_FormCaravan), "autoSelectTravelSupplies");
                if (field != null)
                {
                    field.SetValue(currentDialog, false);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to disable auto-select travel supplies: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the dialog that transferables have changed, which recalculates mass/food stats.
        /// </summary>
        private static void NotifyTransferablesChanged()
        {
            if (currentDialog == null)
                return;

            try
            {
                MethodInfo method = AccessTools.Method(typeof(Dialog_FormCaravan), "Notify_TransferablesChanged");
                if (method != null)
                {
                    method.Invoke(currentDialog, null);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to call Notify_TransferablesChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles keyboard input for caravan formation.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            if (!isActive)
                return false;

            // When choosing destination, let world navigation handle the input
            if (isChoosingDestination)
                return false;

            switch (key)
            {
                case KeyCode.UpArrow:
                    if (!shift && !ctrl && !alt)
                    {
                        SelectPrevious();
                        return true;
                    }
                    break;

                case KeyCode.DownArrow:
                    if (!shift && !ctrl && !alt)
                    {
                        SelectNext();
                        return true;
                    }
                    break;

                case KeyCode.LeftArrow:
                    if (!shift && !ctrl && !alt)
                    {
                        PreviousTab();
                        return true;
                    }
                    break;

                case KeyCode.RightArrow:
                    if (!shift && !ctrl && !alt)
                    {
                        NextTab();
                        return true;
                    }
                    break;

                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals: // Shift+Equals is usually +
                    if (!ctrl && !alt)
                    {
                        AdjustQuantity(1);
                        return true;
                    }
                    break;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    if (!ctrl && !alt)
                    {
                        AdjustQuantity(-1);
                        return true;
                    }
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (!shift && !ctrl && !alt)
                    {
                        ToggleSelection();
                        return true;
                    }
                    break;

                case KeyCode.D:
                    if (!shift && !ctrl && !alt)
                    {
                        ChooseRoute();
                        return true;
                    }
                    break;

                case KeyCode.T:
                    if (!shift && !ctrl && !alt)
                    {
                        Send();
                        return true;
                    }
                    break;

                case KeyCode.R:
                    if (!shift && !ctrl && !alt)
                    {
                        Reset();
                        return true;
                    }
                    break;

                case KeyCode.Escape:
                    // Let the dialog handle its own closing
                    // CaravanFormationPatch.PostClose will call Close()
                    return false;

                default:
                    return false;
            }

            return false;
        }
    }
}
