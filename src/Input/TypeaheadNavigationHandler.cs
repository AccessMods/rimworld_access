using System.Collections.Generic;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Base class for menu navigation with typeahead search support.
    /// Eliminates ~300 lines of duplicated typeahead code across 15+ states.
    ///
    /// Integrates with TypeaheadSearchHelper to provide:
    /// - Arrow keys navigate within search matches when search active
    /// - Escape clears search first, then closes menu
    /// - Home/End clear search before jumping
    /// - Backspace edits search
    /// - Alphanumeric keys add to search
    ///
    /// Subclasses must implement:
    /// - GetTypeaheadHelper() - Return the TypeaheadSearchHelper instance
    /// - GetItemLabels() - Return list of item labels for searching
    /// - GetCurrentIndex() - Return current selected index
    /// - SetCurrentIndex() - Set current selected index
    /// - AnnounceCurrentSelection() - Announce current item to screen reader
    /// - CloseMenu() - Close the menu
    /// - NavigateNextNormal() - Navigate to next item (no search)
    /// - NavigatePreviousNormal() - Navigate to previous item (no search)
    /// </summary>
    public abstract class TypeaheadNavigationHandler : BaseNavigationHandler
    {
        /// <summary>
        /// Get the TypeaheadSearchHelper instance for this menu.
        /// </summary>
        protected abstract TypeaheadSearchHelper GetTypeaheadHelper();

        /// <summary>
        /// Get list of item labels for searching.
        /// </summary>
        protected abstract List<string> GetItemLabels();

        /// <summary>
        /// Get current selected index.
        /// </summary>
        protected abstract int GetCurrentIndex();

        /// <summary>
        /// Set current selected index.
        /// </summary>
        protected abstract void SetCurrentIndex(int index);

        /// <summary>
        /// Announce current selection to screen reader.
        /// Should include search status if typeahead is active.
        /// </summary>
        protected abstract void AnnounceCurrentSelection();

        /// <summary>
        /// Close the menu.
        /// </summary>
        protected abstract void CloseMenu();

        /// <summary>
        /// Navigate to next item (no search).
        /// </summary>
        protected abstract void NavigateNextNormal();

        /// <summary>
        /// Navigate to previous item (no search).
        /// </summary>
        protected abstract void NavigatePreviousNormal();

        /// <summary>
        /// Navigate to next item (with search awareness).
        /// </summary>
        protected override bool OnSelectNext()
        {
            var typeahead = GetTypeaheadHelper();

            // If search is active and has matches, navigate within matches
            if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
            {
                int newIndex = typeahead.GetNextMatch(GetCurrentIndex());
                if (newIndex >= 0)
                {
                    SetCurrentIndex(newIndex);
                    AnnounceCurrentSelection();
                    return true;
                }
            }

            // No search or no matches - navigate normally
            NavigateNextNormal();
            return true;
        }

        /// <summary>
        /// Navigate to previous item (with search awareness).
        /// </summary>
        protected override bool OnSelectPrevious()
        {
            var typeahead = GetTypeaheadHelper();

            // If search is active and has matches, navigate within matches
            if (typeahead.HasActiveSearch && !typeahead.HasNoMatches)
            {
                int newIndex = typeahead.GetPreviousMatch(GetCurrentIndex());
                if (newIndex >= 0)
                {
                    SetCurrentIndex(newIndex);
                    AnnounceCurrentSelection();
                    return true;
                }
            }

            // No search or no matches - navigate normally
            NavigatePreviousNormal();
            return true;
        }

        /// <summary>
        /// Handle Escape key: clear search first, then close menu.
        /// </summary>
        protected override bool OnGoBack()
        {
            var typeahead = GetTypeaheadHelper();

            if (typeahead.HasActiveSearch)
            {
                // Clear search and announce
                typeahead.ClearSearchAndAnnounce();
                AnnounceCurrentSelection();
                return true;
            }

            // No active search - close menu
            CloseMenu();
            NotifyRouterClosed();
            return true;
        }

        /// <summary>
        /// Handle Home key: clear search, then jump to first.
        /// </summary>
        protected override bool OnJumpToFirst()
        {
            var typeahead = GetTypeaheadHelper();

            // Clear search if active
            if (typeahead.HasActiveSearch)
            {
                typeahead.ClearSearch();
            }

            // Subclass must provide implementation
            return HandleJumpToFirst();
        }

        /// <summary>
        /// Handle End key: clear search, then jump to last.
        /// </summary>
        protected override bool OnJumpToLast()
        {
            var typeahead = GetTypeaheadHelper();

            // Clear search if active
            if (typeahead.HasActiveSearch)
            {
                typeahead.ClearSearch();
            }

            // Subclass must provide implementation
            return HandleJumpToLast();
        }

        /// <summary>
        /// Handle custom input for typeahead (letters, numbers, backspace).
        /// </summary>
        protected override bool HandleCustomInput(KeyboardInputContext context)
        {
            var typeahead = GetTypeaheadHelper();

            // Handle backspace
            if (context.Key == KeyCode.Backspace && typeahead.HasActiveSearch)
            {
                var labels = GetItemLabels();
                if (typeahead.ProcessBackspace(labels, out int newIndex))
                {
                    if (newIndex >= 0)
                    {
                        SetCurrentIndex(newIndex);
                    }
                    AnnounceCurrentSelection();
                    return true;
                }
            }

            // Handle alphanumeric typeahead
            if (context.IsAlphanumeric)
            {
                char c = context.GetCharacter();
                var labels = GetItemLabels();

                if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
                {
                    if (newIndex >= 0)
                    {
                        SetCurrentIndex(newIndex);
                    }
                    AnnounceCurrentSelection();
                    return true;
                }
                else
                {
                    // No matches found - typeahead auto-cleared
                    TolkHelper.Speak($"No matches for {typeahead.LastFailedSearch}");
                    AnnounceCurrentSelection();
                    return true;
                }
            }

            // Not a typeahead key - let subclass handle
            return HandleCustomTypeaheadInput(context);
        }

        /// <summary>
        /// Jump to first item (after search is cleared).
        /// Override to implement custom behavior.
        /// </summary>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool HandleJumpToFirst() => false;

        /// <summary>
        /// Jump to last item (after search is cleared).
        /// Override to implement custom behavior.
        /// </summary>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool HandleJumpToLast() => false;

        /// <summary>
        /// Handle additional custom input beyond typeahead.
        /// Override to implement state-specific key handling.
        /// </summary>
        /// <param name="context">Input context with key and modifiers</param>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool HandleCustomTypeaheadInput(KeyboardInputContext context) => false;
    }
}
