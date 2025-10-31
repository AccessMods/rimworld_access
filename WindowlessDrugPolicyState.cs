using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages the state for the windowless drug policy management interface.
    /// Provides keyboard navigation for creating, editing, and managing drug policies.
    /// </summary>
    public static class WindowlessDrugPolicyState
    {
        private static bool isActive = false;
        private static DrugPolicy selectedPolicy = null;
        private static int selectedPolicyIndex = 0;
        private static List<DrugPolicy> allPolicies = new List<DrugPolicy>();

        // Drug entry navigation
        private static int selectedDrugIndex = 0;
        private static int selectedSettingIndex = 0;

        // Navigation state
        public enum NavigationMode
        {
            PolicyList,      // Navigating the list of policies
            PolicyActions,   // Selecting actions (New, Rename, Delete, etc.)
            DrugList,        // Navigating the list of drugs
            DrugSettings     // Editing settings for a specific drug
        }

        private static NavigationMode currentMode = NavigationMode.PolicyList;
        private static int selectedActionIndex = 0;

        // Available actions
        private static readonly string[] policyActions = new string[]
        {
            "New Policy",
            "Rename Policy",
            "Duplicate Policy",
            "Delete Policy",
            "Set as Default",
            "Edit Drugs",
            "Close"
        };

        // Drug settings (for editing)
        private static readonly string[] drugSettings = new string[]
        {
            "Allow for Addiction",
            "Allow for Joy",
            "Allow Scheduled",
            "Days Frequency",
            "Only if Mood Below",
            "Only if Joy Below",
            "Take to Inventory",
            "Back to Drug List"
        };

        public static bool IsActive => isActive;
        public static DrugPolicy SelectedPolicy => selectedPolicy;
        public static NavigationMode CurrentMode => currentMode;

        /// <summary>
        /// Opens the drug policy management interface.
        /// </summary>
        public static void Open(DrugPolicy initialPolicy = null)
        {
            isActive = true;
            currentMode = NavigationMode.PolicyList;
            selectedActionIndex = 0;
            selectedDrugIndex = 0;
            selectedSettingIndex = 0;

            LoadPolicies();

            // Select the initial policy if provided
            if (initialPolicy != null && allPolicies.Contains(initialPolicy))
            {
                selectedPolicyIndex = allPolicies.IndexOf(initialPolicy);
                selectedPolicy = initialPolicy;
            }
            else if (allPolicies.Count > 0)
            {
                selectedPolicyIndex = 0;
                selectedPolicy = allPolicies[0];
            }

            UpdateClipboard();
        }

        /// <summary>
        /// Closes the drug policy management interface.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            selectedPolicy = null;
            selectedPolicyIndex = 0;
            allPolicies.Clear();
            currentMode = NavigationMode.PolicyList;
            selectedDrugIndex = 0;
            selectedSettingIndex = 0;

            ClipboardHelper.CopyToClipboard("Drug policy manager closed");
        }

        /// <summary>
        /// Loads all drug policies from the game database.
        /// </summary>
        private static void LoadPolicies()
        {
            allPolicies.Clear();
            if (Current.Game?.drugPolicyDatabase != null)
            {
                allPolicies = Current.Game.drugPolicyDatabase.AllPolicies.ToList();
            }
        }

        /// <summary>
        /// Moves selection to the next policy in the list.
        /// </summary>
        public static void SelectNextPolicy()
        {
            if (allPolicies.Count == 0)
                return;

            selectedPolicyIndex = (selectedPolicyIndex + 1) % allPolicies.Count;
            selectedPolicy = allPolicies[selectedPolicyIndex];
            UpdateClipboard();
        }

        /// <summary>
        /// Moves selection to the previous policy in the list.
        /// </summary>
        public static void SelectPreviousPolicy()
        {
            if (allPolicies.Count == 0)
                return;

            selectedPolicyIndex--;
            if (selectedPolicyIndex < 0)
                selectedPolicyIndex = allPolicies.Count - 1;

            selectedPolicy = allPolicies[selectedPolicyIndex];
            UpdateClipboard();
        }

        /// <summary>
        /// Switches from policy list to actions mode.
        /// </summary>
        public static void EnterActionsMode()
        {
            if (currentMode == NavigationMode.PolicyList)
            {
                currentMode = NavigationMode.PolicyActions;
                selectedActionIndex = 0;
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Returns to policy list mode from actions or drug mode.
        /// </summary>
        public static void ReturnToPolicyList()
        {
            currentMode = NavigationMode.PolicyList;
            UpdateClipboard();
        }

        /// <summary>
        /// Moves to the next action in the actions menu.
        /// </summary>
        public static void SelectNextAction()
        {
            selectedActionIndex = (selectedActionIndex + 1) % policyActions.Length;
            UpdateClipboard();
        }

        /// <summary>
        /// Moves to the previous action in the actions menu.
        /// </summary>
        public static void SelectPreviousAction()
        {
            selectedActionIndex--;
            if (selectedActionIndex < 0)
                selectedActionIndex = policyActions.Length - 1;
            UpdateClipboard();
        }

        /// <summary>
        /// Executes the currently selected action.
        /// </summary>
        public static void ExecuteAction()
        {
            if (currentMode == NavigationMode.PolicyActions)
            {
                string action = policyActions[selectedActionIndex];

                switch (action)
                {
                    case "New Policy":
                        CreateNewPolicy();
                        break;
                    case "Rename Policy":
                        RenamePolicy();
                        break;
                    case "Duplicate Policy":
                        DuplicatePolicy();
                        break;
                    case "Delete Policy":
                        DeletePolicy();
                        break;
                    case "Set as Default":
                        SetAsDefault();
                        break;
                    case "Edit Drugs":
                        EditDrugs();
                        break;
                    case "Close":
                        Close();
                        break;
                }
            }
        }

        /// <summary>
        /// Creates a new drug policy.
        /// </summary>
        private static void CreateNewPolicy()
        {
            if (Current.Game?.drugPolicyDatabase != null)
            {
                DrugPolicy newPolicy = Current.Game.drugPolicyDatabase.MakeNewDrugPolicy();
                LoadPolicies();
                selectedPolicyIndex = allPolicies.IndexOf(newPolicy);
                selectedPolicy = newPolicy;
                ClipboardHelper.CopyToClipboard($"Created new drug policy: {newPolicy.label}");
            }
        }

        /// <summary>
        /// Opens the rename dialog for the selected policy.
        /// </summary>
        private static void RenamePolicy()
        {
            if (selectedPolicy != null)
            {
                Find.WindowStack.Add(new Dialog_RenamePolicy(selectedPolicy));
                ClipboardHelper.CopyToClipboard($"Rename policy: {selectedPolicy.label}. Enter new name and press Enter.");
            }
        }

        /// <summary>
        /// Duplicates the selected policy.
        /// </summary>
        private static void DuplicatePolicy()
        {
            if (selectedPolicy != null && Current.Game?.drugPolicyDatabase != null)
            {
                DrugPolicy newPolicy = Current.Game.drugPolicyDatabase.MakeNewDrugPolicy();
                newPolicy.label = selectedPolicy.label + " (copy)";
                newPolicy.CopyFrom(selectedPolicy);
                LoadPolicies();
                selectedPolicyIndex = allPolicies.IndexOf(newPolicy);
                selectedPolicy = newPolicy;
                ClipboardHelper.CopyToClipboard($"Duplicated policy: {newPolicy.label}");
            }
        }

        /// <summary>
        /// Deletes the selected policy with confirmation.
        /// </summary>
        private static void DeletePolicy()
        {
            if (selectedPolicy == null)
                return;

            if (Current.Game?.drugPolicyDatabase != null)
            {
                AcceptanceReport result = Current.Game.drugPolicyDatabase.TryDelete(selectedPolicy);
                if (result.Accepted)
                {
                    string deletedName = selectedPolicy.label;
                    LoadPolicies();

                    // Select another policy
                    if (allPolicies.Count > 0)
                    {
                        selectedPolicyIndex = 0;
                        selectedPolicy = allPolicies[0];
                    }
                    else
                    {
                        selectedPolicy = null;
                        selectedPolicyIndex = 0;
                    }

                    ClipboardHelper.CopyToClipboard($"Deleted policy: {deletedName}");
                }
                else
                {
                    ClipboardHelper.CopyToClipboard($"Cannot delete: {result.Reason}");
                }
            }
        }

        /// <summary>
        /// Sets the selected policy as the default.
        /// </summary>
        private static void SetAsDefault()
        {
            if (selectedPolicy != null && Current.Game?.drugPolicyDatabase != null)
            {
                Current.Game.drugPolicyDatabase.SetDefault(selectedPolicy);
                ClipboardHelper.CopyToClipboard($"Set {selectedPolicy.label} as default drug policy");
            }
        }

        /// <summary>
        /// Opens the drug list for editing.
        /// </summary>
        private static void EditDrugs()
        {
            if (selectedPolicy != null)
            {
                currentMode = NavigationMode.DrugList;
                selectedDrugIndex = 0;
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Moves to the next drug in the drug list.
        /// </summary>
        public static void SelectNextDrug()
        {
            if (selectedPolicy == null || selectedPolicy.Count == 0)
                return;

            selectedDrugIndex = (selectedDrugIndex + 1) % selectedPolicy.Count;
            UpdateClipboard();
        }

        /// <summary>
        /// Moves to the previous drug in the drug list.
        /// </summary>
        public static void SelectPreviousDrug()
        {
            if (selectedPolicy == null || selectedPolicy.Count == 0)
                return;

            selectedDrugIndex--;
            if (selectedDrugIndex < 0)
                selectedDrugIndex = selectedPolicy.Count - 1;

            UpdateClipboard();
        }

        /// <summary>
        /// Opens settings for the selected drug.
        /// </summary>
        public static void EditDrugSettings()
        {
            if (selectedPolicy != null && selectedDrugIndex >= 0 && selectedDrugIndex < selectedPolicy.Count)
            {
                currentMode = NavigationMode.DrugSettings;
                selectedSettingIndex = 0;
                UpdateClipboard();
            }
        }

        /// <summary>
        /// Returns to drug list from drug settings.
        /// </summary>
        public static void ReturnToDrugList()
        {
            currentMode = NavigationMode.DrugList;
            UpdateClipboard();
        }

        /// <summary>
        /// Moves to the next setting in drug settings mode.
        /// </summary>
        public static void SelectNextSetting()
        {
            selectedSettingIndex = (selectedSettingIndex + 1) % drugSettings.Length;
            UpdateClipboard();
        }

        /// <summary>
        /// Moves to the previous setting in drug settings mode.
        /// </summary>
        public static void SelectPreviousSetting()
        {
            selectedSettingIndex--;
            if (selectedSettingIndex < 0)
                selectedSettingIndex = drugSettings.Length - 1;

            UpdateClipboard();
        }

        /// <summary>
        /// Toggles or adjusts the selected setting.
        /// </summary>
        public static void ToggleSetting()
        {
            if (selectedPolicy == null || selectedDrugIndex < 0 || selectedDrugIndex >= selectedPolicy.Count)
                return;

            DrugPolicyEntry entry = selectedPolicy[selectedDrugIndex];
            string settingName = drugSettings[selectedSettingIndex];

            switch (settingName)
            {
                case "Allow for Addiction":
                    entry.allowedForAddiction = !entry.allowedForAddiction;
                    ClipboardHelper.CopyToClipboard($"Allow for Addiction: {(entry.allowedForAddiction ? "Yes" : "No")}");
                    break;

                case "Allow for Joy":
                    entry.allowedForJoy = !entry.allowedForJoy;
                    ClipboardHelper.CopyToClipboard($"Allow for Joy: {(entry.allowedForJoy ? "Yes" : "No")}");
                    break;

                case "Allow Scheduled":
                    entry.allowScheduled = !entry.allowScheduled;
                    ClipboardHelper.CopyToClipboard($"Allow Scheduled: {(entry.allowScheduled ? "Yes" : "No")}");
                    break;

                case "Back to Drug List":
                    ReturnToDrugList();
                    break;
            }
        }

        /// <summary>
        /// Adjusts numeric settings (left = decrease, right = increase).
        /// </summary>
        public static void AdjustSetting(int direction)
        {
            if (selectedPolicy == null || selectedDrugIndex < 0 || selectedDrugIndex >= selectedPolicy.Count)
                return;

            DrugPolicyEntry entry = selectedPolicy[selectedDrugIndex];
            string settingName = drugSettings[selectedSettingIndex];

            switch (settingName)
            {
                case "Days Frequency":
                    entry.daysFrequency += direction * 0.5f;
                    if (entry.daysFrequency < 0.5f) entry.daysFrequency = 0.5f;
                    if (entry.daysFrequency > 30f) entry.daysFrequency = 30f;
                    ClipboardHelper.CopyToClipboard($"Days Frequency: {entry.daysFrequency:F1}");
                    break;

                case "Only if Mood Below":
                    entry.onlyIfMoodBelow += direction * 0.05f;
                    if (entry.onlyIfMoodBelow < 0f) entry.onlyIfMoodBelow = 0f;
                    if (entry.onlyIfMoodBelow > 1f) entry.onlyIfMoodBelow = 1f;
                    ClipboardHelper.CopyToClipboard($"Only if Mood Below: {entry.onlyIfMoodBelow:P0}");
                    break;

                case "Only if Joy Below":
                    entry.onlyIfJoyBelow += direction * 0.05f;
                    if (entry.onlyIfJoyBelow < 0f) entry.onlyIfJoyBelow = 0f;
                    if (entry.onlyIfJoyBelow > 1f) entry.onlyIfJoyBelow = 1f;
                    ClipboardHelper.CopyToClipboard($"Only if Joy Below: {entry.onlyIfJoyBelow:P0}");
                    break;

                case "Take to Inventory":
                    entry.takeToInventory += direction;
                    if (entry.takeToInventory < 0) entry.takeToInventory = 0;
                    ClipboardHelper.CopyToClipboard($"Take to Inventory: {entry.takeToInventory}");
                    break;
            }
        }

        /// <summary>
        /// Updates the clipboard with the current selection.
        /// </summary>
        private static void UpdateClipboard()
        {
            if (currentMode == NavigationMode.PolicyList)
            {
                if (selectedPolicy != null)
                {
                    bool isDefault = Current.Game?.drugPolicyDatabase?.DefaultDrugPolicy() == selectedPolicy;
                    string defaultMarker = isDefault ? " (default)" : "";
                    ClipboardHelper.CopyToClipboard($"Drug policy {selectedPolicyIndex + 1}/{allPolicies.Count}: {selectedPolicy.label}{defaultMarker}. Press Tab for actions.");
                }
                else
                {
                    ClipboardHelper.CopyToClipboard("No drug policies available. Press Tab to create one.");
                }
            }
            else if (currentMode == NavigationMode.PolicyActions)
            {
                string action = policyActions[selectedActionIndex];
                ClipboardHelper.CopyToClipboard($"Action {selectedActionIndex + 1}/{policyActions.Length}: {action}. Press Enter to execute, Tab/Shift+Tab or arrows to navigate, Escape to return to policy list.");
            }
            else if (currentMode == NavigationMode.DrugList)
            {
                if (selectedPolicy != null && selectedPolicy.Count > 0)
                {
                    DrugPolicyEntry entry = selectedPolicy[selectedDrugIndex];
                    string drugName = entry.drug.label;
                    string status = GetDrugStatusSummary(entry);
                    ClipboardHelper.CopyToClipboard($"Drug {selectedDrugIndex + 1}/{selectedPolicy.Count}: {drugName}. {status}. Press Enter to edit, Escape to return.");
                }
                else
                {
                    ClipboardHelper.CopyToClipboard("No drugs in policy.");
                }
            }
            else if (currentMode == NavigationMode.DrugSettings)
            {
                if (selectedPolicy != null && selectedDrugIndex >= 0 && selectedDrugIndex < selectedPolicy.Count)
                {
                    DrugPolicyEntry entry = selectedPolicy[selectedDrugIndex];
                    string settingName = drugSettings[selectedSettingIndex];
                    string settingValue = GetSettingValue(entry, settingName);
                    ClipboardHelper.CopyToClipboard($"{entry.drug.label} - Setting {selectedSettingIndex + 1}/{drugSettings.Length}: {settingName} = {settingValue}. Use Space to toggle, Left/Right to adjust.");
                }
            }
        }

        /// <summary>
        /// Gets a summary of the drug's status.
        /// </summary>
        private static string GetDrugStatusSummary(DrugPolicyEntry entry)
        {
            List<string> parts = new List<string>();

            if (entry.allowedForAddiction) parts.Add("Addiction");
            if (entry.allowedForJoy) parts.Add("Joy");
            if (entry.allowScheduled) parts.Add("Scheduled");

            if (parts.Count == 0)
                return "Not allowed";

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets the value of the currently selected setting.
        /// </summary>
        private static string GetSettingValue(DrugPolicyEntry entry, string settingName)
        {
            switch (settingName)
            {
                case "Allow for Addiction":
                    return entry.allowedForAddiction ? "Yes" : "No";
                case "Allow for Joy":
                    return entry.allowedForJoy ? "Yes" : "No";
                case "Allow Scheduled":
                    return entry.allowScheduled ? "Yes" : "No";
                case "Days Frequency":
                    return $"{entry.daysFrequency:F1} days";
                case "Only if Mood Below":
                    return $"{entry.onlyIfMoodBelow:P0}";
                case "Only if Joy Below":
                    return $"{entry.onlyIfJoyBelow:P0}";
                case "Take to Inventory":
                    return entry.takeToInventory.ToString();
                case "Back to Drug List":
                    return "";
                default:
                    return "Unknown";
            }
        }
    }
}
