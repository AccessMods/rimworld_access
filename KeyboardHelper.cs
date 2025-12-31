using System;

namespace RimWorldAccess
{
    public static class KeyboardHelper
    {
        /// <summary>
        /// Returns true if ANY modal accessibility menu is currently active.
        /// When true, ALL keyboard input should go to that menu, not the game.
        /// </summary>
        public static bool IsAnyAccessibilityMenuActive()
        {
            // Windowless menus (main accessibility menus)
            return WindowlessFloatMenuState.IsActive
                || WindowlessInventoryState.IsActive
                || WindowlessInspectionState.IsActive
                || WindowlessSaveMenuState.IsActive
                || WindowlessOptionsMenuState.IsActive
                || WindowlessPauseMenuState.IsActive
                || WindowlessResearchMenuState.IsActive
                || WindowlessResearchDetailState.IsActive
                || WindowlessScheduleState.IsActive
                || WindowlessDialogState.IsActive
                || WindowlessConfirmationState.IsActive
                || WindowlessAreaState.IsActive
                // Policy menus
                || WindowlessOutfitPolicyState.IsActive
                || WindowlessFoodPolicyState.IsActive
                || WindowlessDrugPolicyState.IsActive
                // Main gameplay menus
                || SettlementBrowserState.IsActive
                || CaravanFormationState.IsActive
                || CaravanStatsState.IsActive
                || QuestMenuState.IsActive
                || QuestLocationsBrowserState.IsActive
                || NotificationMenuState.IsActive
                || AssignMenuState.IsActive
                || WorkMenuState.IsActive
                || StorageSettingsMenuState.IsActive
                || ZoneSettingsMenuState.IsActive
                || ZoneRenameState.IsActive
                || PlantSelectionMenuState.IsActive
                || GizmoNavigationState.IsActive
                || TradeNavigationState.IsActive
                || TradeConfirmationState.IsActive
                // Bills and building menus
                || BillsMenuState.IsActive
                || BillConfigState.IsActive
                || BuildingInspectState.IsActive
                || RangeEditMenuState.IsActive
                || TempControlMenuState.IsActive
                // Building component controls
                || ForbidControlState.IsActive
                || FlickableComponentState.IsActive
                || BreakdownableComponentState.IsActive
                || DoorControlState.IsActive
                || RefuelableComponentState.IsActive
                || UninstallControlState.IsActive
                || BedAssignmentState.IsActive
                // Pawn inspection tabs
                || CharacterTabState.IsActive
                || HealthTabState.IsActive
                || NeedsTabState.IsActive
                || SocialTabState.IsActive
                || TrainingTabState.IsActive
                || PrisonerTabState.IsActive
                // Filter navigation
                || ThingFilterMenuState.IsActive
                || ThingFilterNavigationState.IsActive
                // Other menus
                || ArchitectState.IsActive
                || AnimalsMenuState.IsActive
                || WildlifeMenuState.IsActive
                || ModListState.IsActive
                || StorytellerSelectionState.IsActive
                || PlaySettingsMenuState.IsActive
                || AreaPaintingState.IsActive;
        }
    }
}
