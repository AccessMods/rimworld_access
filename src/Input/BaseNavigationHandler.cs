using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Base class for simple menu navigation (Up/Down/Home/End/Enter/Escape).
    /// Eliminates ~200 lines of duplicated navigation code across 40+ states.
    ///
    /// Subclasses must implement:
    /// - OnSelectNext() - Navigate to next item
    /// - OnSelectPrevious() - Navigate to previous item
    /// - OnExecuteSelected() - Execute current item
    /// - OnGoBack() - Close menu or go back
    ///
    /// Subclasses can optionally override:
    /// - OnJumpToFirst() - Jump to first item (Home key)
    /// - OnJumpToLast() - Jump to last item (End key)
    /// - HandleCustomInput() - Handle additional keys
    /// </summary>
    public abstract class BaseNavigationHandler : IKeyboardInputHandler
    {
        /// <summary>
        /// Priority level (lower = higher priority, processed first).
        /// Use constants from InputHandlerPriority.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Whether this handler should receive input.
        /// Router only invokes active handlers.
        /// </summary>
        public abstract bool IsActive { get; }

        /// <summary>
        /// Handle keyboard input by routing to appropriate method.
        /// </summary>
        public bool HandleInput(KeyboardInputContext context)
        {
            if (!IsActive)
                return false;

            switch (context.Key)
            {
                case KeyCode.UpArrow:
                    return OnSelectPrevious();

                case KeyCode.DownArrow:
                    return OnSelectNext();

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    return OnExecuteSelected();

                case KeyCode.Escape:
                    return OnGoBack();

                case KeyCode.Home:
                    return OnJumpToFirst();

                case KeyCode.End:
                    return OnJumpToLast();
            }

            return HandleCustomInput(context);
        }

        /// <summary>
        /// Navigate to next item.
        /// </summary>
        /// <returns>True if event was handled</returns>
        protected abstract bool OnSelectNext();

        /// <summary>
        /// Navigate to previous item.
        /// </summary>
        /// <returns>True if event was handled</returns>
        protected abstract bool OnSelectPrevious();

        /// <summary>
        /// Execute the currently selected item.
        /// </summary>
        /// <returns>True if event was handled</returns>
        protected abstract bool OnExecuteSelected();

        /// <summary>
        /// Go back or close the menu.
        /// </summary>
        /// <returns>True if event was handled</returns>
        protected abstract bool OnGoBack();

        /// <summary>
        /// Jump to first item (Home key).
        /// Override to implement custom behavior.
        /// </summary>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool OnJumpToFirst() => false;

        /// <summary>
        /// Jump to last item (End key).
        /// Override to implement custom behavior.
        /// </summary>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool OnJumpToLast() => false;

        /// <summary>
        /// Handle custom input for keys not covered by base navigation.
        /// Override to implement state-specific key handling.
        /// </summary>
        /// <param name="context">Input context with key and modifiers</param>
        /// <returns>True if event was handled, false by default</returns>
        protected virtual bool HandleCustomInput(KeyboardInputContext context) => false;

        /// <summary>
        /// Notify router that this handler closed (for Escape isolation).
        /// Call this in your Close() method to prevent parent windows from also closing.
        /// </summary>
        protected void NotifyRouterClosed()
        {
            KeyboardInputRouter.NotifyHandlerClosed();
        }
    }
}
