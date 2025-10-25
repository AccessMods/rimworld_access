using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a windowless options menu.
    /// For now, this opens the actual Options dialog since it's a complex UI.
    /// In the future, this could be expanded to provide fully accessible options navigation.
    /// </summary>
    public static class WindowlessOptionsMenuState
    {
        private static bool isActive = false;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the options menu.
        /// For now, we open the actual game's options dialog.
        /// </summary>
        public static void Open()
        {
            isActive = true;

            // Close pause menu
            WindowlessPauseMenuState.Close();

            // Open the actual options dialog
            Find.WindowStack.Add(new Dialog_Options());

            ClipboardHelper.CopyToClipboard("Options menu opened");

            // Immediately close this state since we're delegating to the real dialog
            Close();
        }

        /// <summary>
        /// Closes the options menu.
        /// </summary>
        public static void Close()
        {
            isActive = false;
        }

        /// <summary>
        /// Returns to the pause menu.
        /// </summary>
        public static void GoBack()
        {
            Close();
            WindowlessPauseMenuState.Open();
        }
    }
}
