using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Base class for tabbed interfaces (e.g., CaravanFormation: Pawns/Items/Supplies).
    /// Handles Tab navigation with wrap-around (Shift+Tab not supported due to Steam overlay conflict).
    ///
    /// Subclasses must implement:
    /// - GetTabCount() - Return number of tabs
    /// - GetCurrentTab() - Return current tab index (0-based)
    /// - SetCurrentTab() - Set current tab index
    /// - AnnounceCurrentTab() - Announce current tab to screen reader
    ///
    /// Subclasses can optionally override:
    /// - OnTabChanged() - Hook called after tab changes
    /// </summary>
    public abstract class TabNavigationHandler : BaseNavigationHandler
    {
        /// <summary>
        /// Get the number of tabs.
        /// </summary>
        protected abstract int GetTabCount();

        /// <summary>
        /// Get current tab index (0-based).
        /// </summary>
        protected abstract int GetCurrentTab();

        /// <summary>
        /// Set current tab index (0-based).
        /// </summary>
        protected abstract void SetCurrentTab(int tabIndex);

        /// <summary>
        /// Announce current tab to screen reader.
        /// Should include tab name and position (e.g., "Pawns tab, 1 of 3").
        /// </summary>
        protected abstract void AnnounceCurrentTab();

        /// <summary>
        /// Hook called after tab changes.
        /// Override to implement custom behavior (e.g., reset selected index).
        /// </summary>
        protected virtual void OnTabChanged() { }

        /// <summary>
        /// Handle custom input for tab navigation.
        /// </summary>
        protected override bool HandleCustomInput(KeyboardInputContext context)
        {
            // Tab key: cycle forward with wrap-around
            // Note: Shift+Tab not supported as it triggers Steam overlay
            if (context.Key == KeyCode.Tab)
            {
                int tabCount = GetTabCount();
                int currentTab = GetCurrentTab();
                int newTab = (currentTab + 1) % tabCount;

                SetCurrentTab(newTab);
                OnTabChanged();
                AnnounceCurrentTab();
                return true;
            }

            // Not a tab navigation key - let subclass handle
            return HandleCustomTabInput(context);
        }

        /// <summary>
        /// Handle additional custom input beyond tab navigation.
        /// Override to implement state-specific key handling.
        /// </summary>
        /// <param name="context">Input context with key and modifiers</param>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool HandleCustomTabInput(KeyboardInputContext context) => false;
    }
}
