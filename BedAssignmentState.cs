using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages keyboard navigation for bed assignment.
    /// Handles assigning/unassigning pawns to beds, changing bed type, and toggling medical status.
    /// </summary>
    public static class BedAssignmentState
    {
        private enum MenuLevel
        {
            MainMenu,
            AssignMenu,
            UnassignMenu,
            BedTypeMenu
        }

        private static bool isActive = false;
        private static Building_Bed selectedBed = null;
        private static MenuLevel currentMenuLevel = MenuLevel.MainMenu;
        private static int selectedIndex = 0;
        private static List<string> menuOptions = new List<string>();
        private static List<Pawn> candidatePawns = new List<Pawn>();
        private static List<Pawn> assignedPawns = new List<Pawn>();

        public static bool IsActive => isActive;
        public static Building_Bed SelectedBed => selectedBed;

        /// <summary>
        /// Opens the bed assignment menu for the given bed.
        /// </summary>
        public static void Open(Building_Bed bed)
        {
            if (bed == null)
            {
                ClipboardHelper.CopyToClipboard("No bed to configure");
                return;
            }

            selectedBed = bed;
            isActive = true;
            currentMenuLevel = MenuLevel.MainMenu;
            selectedIndex = 0;

            BuildMainMenu();
        }

        /// <summary>
        /// Closes the bed assignment menu.
        /// </summary>
        public static void Close()
        {
            selectedBed = null;
            isActive = false;
            currentMenuLevel = MenuLevel.MainMenu;
            selectedIndex = 0;
            menuOptions.Clear();
            candidatePawns.Clear();
            assignedPawns.Clear();
        }

        /// <summary>
        /// Selects the next menu option.
        /// </summary>
        public static void SelectNext()
        {
            if (menuOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % menuOptions.Count;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Selects the previous menu option.
        /// </summary>
        public static void SelectPrevious()
        {
            if (menuOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + menuOptions.Count) % menuOptions.Count;
            AnnounceCurrentSelection();
        }

        /// <summary>
        /// Executes the currently selected menu option.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (selectedIndex >= menuOptions.Count)
                return;

            switch (currentMenuLevel)
            {
                case MenuLevel.MainMenu:
                    ExecuteMainMenuOption();
                    break;
                case MenuLevel.AssignMenu:
                    ExecuteAssignMenuOption();
                    break;
                case MenuLevel.UnassignMenu:
                    ExecuteUnassignMenuOption();
                    break;
                case MenuLevel.BedTypeMenu:
                    ExecuteBedTypeMenuOption();
                    break;
            }
        }

        /// <summary>
        /// Goes back to the previous menu level or closes the menu.
        /// </summary>
        public static void GoBack()
        {
            if (currentMenuLevel == MenuLevel.MainMenu)
            {
                Close();
                ClipboardHelper.CopyToClipboard("Bed menu closed");
            }
            else
            {
                // Go back to main menu
                currentMenuLevel = MenuLevel.MainMenu;
                selectedIndex = 0;
                BuildMainMenu();
            }
        }

        #region Main Menu

        private static void BuildMainMenu()
        {
            menuOptions.Clear();

            if (selectedBed == null)
            {
                Close();
                return;
            }

            // Build menu options
            menuOptions.Add("Assign pawn");

            // Add unassign option if bed has assignments
            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;
            if (comp != null && comp.AssignedPawnsForReading.Count > 0)
            {
                menuOptions.Add("Unassign pawn");
            }

            menuOptions.Add("Change bed type");
            menuOptions.Add("Toggle medical");
            menuOptions.Add("Close menu");

            AnnounceMainMenu();
        }

        private static void AnnounceMainMenu()
        {
            if (selectedBed == null)
                return;

            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;

            // Build bed info string
            string bedInfo = $"{selectedBed.LabelCap}";

            // Add bed type
            if (selectedBed.ForPrisoners)
                bedInfo += " - For prisoners";
            else if (selectedBed.ForSlaves)
                bedInfo += " - For slaves";
            else if (selectedBed.ForColonists)
                bedInfo += " - For colonists";

            // Add medical status
            bedInfo += selectedBed.Medical ? " - Medical" : " - Not medical";

            // Add assignment info
            if (comp != null)
            {
                if (comp.AssignedPawnsForReading.Count > 0)
                {
                    string assignedNames = string.Join(", ", comp.AssignedPawnsForReading.Select(p => p.LabelShort));
                    bedInfo += $" - Assigned to: {assignedNames}";
                }
                else
                {
                    bedInfo += " - Unassigned";
                }
            }

            // Announce bed info and current option
            string announcement = bedInfo;
            if (menuOptions.Count > 0 && selectedIndex < menuOptions.Count)
            {
                announcement += $" - {menuOptions[selectedIndex]}";
            }

            ClipboardHelper.CopyToClipboard(announcement);
        }

        private static void ExecuteMainMenuOption()
        {
            if (selectedIndex >= menuOptions.Count)
                return;

            string option = menuOptions[selectedIndex];

            switch (option)
            {
                case "Assign pawn":
                    OpenAssignMenu();
                    break;
                case "Unassign pawn":
                    OpenUnassignMenu();
                    break;
                case "Change bed type":
                    OpenBedTypeMenu();
                    break;
                case "Toggle medical":
                    ToggleMedical();
                    break;
                case "Close menu":
                    Close();
                    ClipboardHelper.CopyToClipboard("Bed menu closed");
                    break;
            }
        }

        #endregion

        #region Assign Menu

        private static void OpenAssignMenu()
        {
            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;
            if (comp == null)
            {
                ClipboardHelper.CopyToClipboard("Cannot assign pawns to this bed");
                return;
            }

            // Get candidate pawns
            candidatePawns = comp.AssigningCandidates.ToList();

            if (candidatePawns.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No available pawns to assign");
                return;
            }

            // Build menu options
            menuOptions.Clear();
            foreach (Pawn pawn in candidatePawns)
            {
                string option = pawn.LabelShort;

                // Check if pawn can be assigned
                if (!comp.CanAssignTo(pawn))
                {
                    option += " (Cannot assign)";
                }
                else if (comp.IdeoligionForbids(pawn))
                {
                    option += " (Ideology forbids)";
                }
                else if (pawn.ownership?.OwnedBed != null)
                {
                    option += " (Already assigned)";
                }

                menuOptions.Add(option);
            }

            currentMenuLevel = MenuLevel.AssignMenu;
            selectedIndex = 0;

            // Announce first option
            ClipboardHelper.CopyToClipboard($"Assign pawn - {menuOptions[0]}");
        }

        private static void ExecuteAssignMenuOption()
        {
            if (selectedIndex >= candidatePawns.Count)
                return;

            Pawn selectedPawn = candidatePawns[selectedIndex];
            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;

            if (comp == null)
            {
                ClipboardHelper.CopyToClipboard("Cannot assign pawn");
                return;
            }

            // Check if pawn can be assigned
            if (!comp.CanAssignTo(selectedPawn))
            {
                ClipboardHelper.CopyToClipboard($"Cannot assign {selectedPawn.LabelShort} to this bed");
                return;
            }

            if (comp.IdeoligionForbids(selectedPawn))
            {
                ClipboardHelper.CopyToClipboard($"Ideology forbids {selectedPawn.LabelShort} from using this bed");
                return;
            }

            // Try to assign the pawn
            comp.TryAssignPawn(selectedPawn);

            // Check if assignment succeeded
            if (comp.AssignedPawnsForReading.Contains(selectedPawn))
            {
                ClipboardHelper.CopyToClipboard($"{selectedPawn.LabelShort} assigned to {selectedBed.LabelCap}");
            }
            else
            {
                ClipboardHelper.CopyToClipboard($"Failed to assign {selectedPawn.LabelShort}");
            }

            // Go back to main menu
            currentMenuLevel = MenuLevel.MainMenu;
            selectedIndex = 0;
            BuildMainMenu();
        }

        #endregion

        #region Unassign Menu

        private static void OpenUnassignMenu()
        {
            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;
            if (comp == null)
            {
                ClipboardHelper.CopyToClipboard("Cannot unassign pawns from this bed");
                return;
            }

            // Get assigned pawns
            assignedPawns = comp.AssignedPawnsForReading.ToList();

            if (assignedPawns.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No pawns assigned to this bed");
                return;
            }

            // Build menu options
            menuOptions.Clear();
            foreach (Pawn pawn in assignedPawns)
            {
                menuOptions.Add(pawn.LabelShort);
            }

            currentMenuLevel = MenuLevel.UnassignMenu;
            selectedIndex = 0;

            // Announce first option
            ClipboardHelper.CopyToClipboard($"Unassign pawn - {menuOptions[0]}");
        }

        private static void ExecuteUnassignMenuOption()
        {
            if (selectedIndex >= assignedPawns.Count)
                return;

            Pawn selectedPawn = assignedPawns[selectedIndex];
            CompAssignableToPawn_Bed comp = selectedBed.CompAssignableToPawn as CompAssignableToPawn_Bed;

            if (comp == null)
            {
                ClipboardHelper.CopyToClipboard("Cannot unassign pawn");
                return;
            }

            // Try to unassign the pawn
            comp.TryUnassignPawn(selectedPawn, true, false);

            // Check if unassignment succeeded
            if (!comp.AssignedPawnsForReading.Contains(selectedPawn))
            {
                ClipboardHelper.CopyToClipboard($"{selectedPawn.LabelShort} unassigned from {selectedBed.LabelCap}");
            }
            else
            {
                ClipboardHelper.CopyToClipboard($"Failed to unassign {selectedPawn.LabelShort}");
            }

            // Go back to main menu
            currentMenuLevel = MenuLevel.MainMenu;
            selectedIndex = 0;
            BuildMainMenu();
        }

        #endregion

        #region Bed Type Menu

        private static void OpenBedTypeMenu()
        {
            menuOptions.Clear();
            menuOptions.Add("Colonist");
            menuOptions.Add("Prisoner");
            menuOptions.Add("Slave");

            currentMenuLevel = MenuLevel.BedTypeMenu;
            selectedIndex = 0;

            // Set initial selection to current bed type
            if (selectedBed.ForPrisoners)
                selectedIndex = 1;
            else if (selectedBed.ForSlaves)
                selectedIndex = 2;
            else
                selectedIndex = 0;

            // Announce current selection
            ClipboardHelper.CopyToClipboard($"Change bed type - {menuOptions[selectedIndex]} (current)");
        }

        private static void ExecuteBedTypeMenuOption()
        {
            if (selectedIndex >= menuOptions.Count)
                return;

            BedOwnerType newOwnerType = BedOwnerType.Colonist;
            switch (selectedIndex)
            {
                case 0:
                    newOwnerType = BedOwnerType.Colonist;
                    break;
                case 1:
                    newOwnerType = BedOwnerType.Prisoner;
                    break;
                case 2:
                    newOwnerType = BedOwnerType.Slave;
                    break;
            }

            // Check if changing to prisoner type
            if (newOwnerType == BedOwnerType.Prisoner)
            {
                // Validate that the room is a valid prison cell
                Room room = selectedBed.GetRoom();
                if (room == null || !room.IsPrisonCell)
                {
                    ClipboardHelper.CopyToClipboard("Cannot set bed for prisoners - not in a prison cell. Mark room as prison first.");
                    return;
                }
            }

            // Change bed owner type
            selectedBed.ForPrisoners = (newOwnerType == BedOwnerType.Prisoner);
            if (ModsConfig.IdeologyActive)
            {
                // Use the proper method if Ideology is active
                selectedBed.SetBedOwnerTypeByInterface(newOwnerType);
            }
            else
            {
                // Manually set ForPrisoners for non-Ideology
                selectedBed.ForPrisoners = (newOwnerType == BedOwnerType.Prisoner);
            }

            ClipboardHelper.CopyToClipboard($"Bed type changed to: {menuOptions[selectedIndex]}");

            // Go back to main menu
            currentMenuLevel = MenuLevel.MainMenu;
            selectedIndex = 0;
            BuildMainMenu();
        }

        #endregion

        #region Toggle Medical

        private static void ToggleMedical()
        {
            if (selectedBed == null)
                return;

            // Toggle medical status
            selectedBed.Medical = !selectedBed.Medical;

            string status = selectedBed.Medical ? "Medical" : "Not medical";
            ClipboardHelper.CopyToClipboard($"Bed medical status: {status}");

            // Rebuild main menu to reflect changes
            BuildMainMenu();
        }

        #endregion

        #region Helpers

        private static void AnnounceCurrentSelection()
        {
            if (menuOptions.Count == 0 || selectedIndex >= menuOptions.Count)
                return;

            string prefix = "";
            switch (currentMenuLevel)
            {
                case MenuLevel.MainMenu:
                    prefix = "";
                    break;
                case MenuLevel.AssignMenu:
                    prefix = "Assign: ";
                    break;
                case MenuLevel.UnassignMenu:
                    prefix = "Unassign: ";
                    break;
                case MenuLevel.BedTypeMenu:
                    prefix = "Bed type: ";
                    break;
            }

            ClipboardHelper.CopyToClipboard($"{prefix}{menuOptions[selectedIndex]}");
        }

        #endregion
    }
}
