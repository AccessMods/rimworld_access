using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a "virtual" float menu without actually displaying a window.
    /// Stores FloatMenuOptions and handles keyboard navigation, then executes the selected option.
    /// </summary>
    public static class WindowlessFloatMenuState
    {
        private static List<FloatMenuOption> currentOptions = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static bool givesColonistOrders = false;
        private static List<object> savedSelection = null;

        /// <summary>
        /// Gets whether the windowless menu is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the windowless menu with the given options.
        /// </summary>
        public static void Open(List<FloatMenuOption> options, bool colonistOrders)
        {
            currentOptions = options;
            selectedIndex = 0;
            isActive = true;
            givesColonistOrders = colonistOrders;

            // Save current selection - some FloatMenu actions expect specific objects to be selected
            savedSelection = Find.Selector?.SelectedObjects?.ToList();

            // Announce the first option
            if (selectedIndex >= 0 && selectedIndex < options.Count)
            {
                string optionText = options[selectedIndex].Label;
                if (options[selectedIndex].Disabled)
                {
                    optionText += " (unavailable)";
                }
                TolkHelper.Speak(optionText);
            }
        }

        /// <summary>
        /// Closes the windowless menu.
        /// </summary>
        public static void Close()
        {
            currentOptions = null;
            selectedIndex = 0;
            isActive = false;
        }

        /// <summary>
        /// Moves selection to the next option.
        /// </summary>
        public static void SelectNext()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % currentOptions.Count;

            // Announce the new selection
            if (selectedIndex >= 0 && selectedIndex < currentOptions.Count)
            {
                string optionText = currentOptions[selectedIndex].Label;
                if (currentOptions[selectedIndex].Disabled)
                {
                    optionText += " (unavailable)";
                }
                TolkHelper.Speak(optionText);
            }
        }

        /// <summary>
        /// Moves selection to the previous option.
        /// </summary>
        public static void SelectPrevious()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + currentOptions.Count) % currentOptions.Count;

            // Announce the new selection
            if (selectedIndex >= 0 && selectedIndex < currentOptions.Count)
            {
                string optionText = currentOptions[selectedIndex].Label;
                if (currentOptions[selectedIndex].Disabled)
                {
                    optionText += " (unavailable)";
                }
                TolkHelper.Speak(optionText);
            }
        }

        /// <summary>
        /// Executes the currently selected option.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (currentOptions == null || currentOptions.Count == 0)
                return;

            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count)
                return;

            FloatMenuOption selectedOption = currentOptions[selectedIndex];

            if (selectedOption.Disabled)
            {
                TolkHelper.Speak(selectedOption.Label + " - unavailable");
                return;
            }

            // Restore saved selection before executing - some actions check Find.Selector.SelectedObjects
            if (savedSelection != null && Find.Selector != null)
            {
                Find.Selector.ClearSelection();
                foreach (var obj in savedSelection)
                {
                    if (obj is ISelectable selectable)
                    {
                        Find.Selector.Select(selectable, playSound: false, forceDesignatorDeselect: false);
                    }
                }
            }

            // Close the menu BEFORE executing the action
            // This allows the action to open a new menu if needed
            Close();

            // Call the Chosen method to execute the option's action
            selectedOption.Chosen(givesColonistOrders, null);

            // Announce selection
            TolkHelper.Speak($"{selectedOption.Label} selected");
        }
    }
}
