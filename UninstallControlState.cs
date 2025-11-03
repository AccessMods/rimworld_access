using System;
using Verse;
using RimWorld;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages keyboard navigation for uninstall functionality (Minifiable buildings).
    /// Allows designating furniture for uninstallation via keyboard shortcuts.
    /// </summary>
    public static class UninstallControlState
    {
        private static Building building = null;
        private static bool isActive = false;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the uninstall control menu for the given building.
        /// </summary>
        public static void Open(Building targetBuilding)
        {
            if (targetBuilding == null)
            {
                ClipboardHelper.CopyToClipboard("No building to configure");
                return;
            }

            if (!targetBuilding.def.Minifiable)
            ModLogger.Msg(string.Format("Uninstall check: def.Minifiable={0}, building type={1}", targetBuilding.def.Minifiable, targetBuilding.GetType().Name));
            {
                ClipboardHelper.CopyToClipboard("Building cannot be uninstalled");
                return;
            }

            building = targetBuilding;
            isActive = true;
            MapNavigationState.SuppressMapNavigation = true;

            AnnounceCurrentStatus();
        }

        /// <summary>
        /// Closes the uninstall control menu.
        /// </summary>
        public static void Close()
        {
            building = null;
            isActive = false;
            MapNavigationState.SuppressMapNavigation = false;
        }

        /// <summary>
        /// Toggles the uninstall designation.
        /// </summary>
        /// <summary>
        /// Toggles the uninstall designation.
        /// </summary>
        public static void ToggleUninstall()
        {
            if (building == null || building.Map == null)
                return;

            ModLogger.Msg(string.Format("ToggleUninstall called for {0}", building.LabelCap));

            var designation = building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall);

            if (designation != null)
            {
                // Remove uninstall designation
                building.Map.designationManager.RemoveDesignation(designation);
                ModLogger.Msg("Removed existing uninstall designation");
                ClipboardHelper.CopyToClipboard(string.Format("{0} - Uninstall designation removed", building.LabelCap));
            }
            else
            {
                // Check if building can be instantly uninstalled (god mode or no work cost)
                bool instantUninstall = UnityEngine.Debug.isDebugBuild || building.GetStatValue(StatDefOf.WorkToBuild) == 0f || building.def.IsFrame;
                
                ModLogger.Msg(string.Format("Adding uninstall designation. InstantUninstall: {0}, WorkToBuild: {1}", 
                    instantUninstall, building.GetStatValue(StatDefOf.WorkToBuild)));
                
                if (instantUninstall)
                {
                    // Instant uninstall like the game does in god mode
                    building.Uninstall();
                    ClipboardHelper.CopyToClipboard(string.Format("{0} - Instantly uninstalled", building.LabelCap));
                }
                else
                {
                    // Add uninstall designation
                    building.Map.designationManager.AddDesignation(new Designation(building, DesignationDefOf.Uninstall));
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    ClipboardHelper.CopyToClipboard(string.Format("{0} - Designated for uninstall", building.LabelCap));
                    
                    ModLogger.Msg("Designation added successfully");
                }
            }
        }

        /// <summary>
        /// Announces the current uninstall status to the clipboard for screen readers.
        /// </summary>
        private static void AnnounceCurrentStatus()
        {
            if (building == null || building.Map == null)
                return;

            string itemLabel = building.LabelCap;
            var designation = building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall);
            string status = designation != null ? "Designated for uninstall" : "Not designated";

            string announcement = string.Format("{0} - Status: {1}", itemLabel, status);

            ClipboardHelper.CopyToClipboard(announcement);
        }

        /// <summary>
        /// Gets a detailed status report.
        /// </summary>
        public static void AnnounceDetailedStatus()
        {
            if (building == null || building.Map == null)
                return;

            string details = string.Format("{0}\n", building.LabelCap);

            var designation = building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall);

            if (designation != null)
            {
                details += "Status: Designated for uninstall\n";
                details += "A colonist with the Construction skill will disassemble this.\n";
                details += "The building will be converted to a minified item that can be moved.";
            }
            else
            {
                details += "Status: Not designated for uninstall\n";
                details += "This building can be uninstalled to create a portable item.\n";
                details += "Press Space or Enter to designate for uninstall.";
            }

            ClipboardHelper.CopyToClipboard(details);
        }
    }
}
