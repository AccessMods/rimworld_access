namespace RimWorldAccess
{
    /// <summary>
    /// Contract for keyboard input handlers.
    /// Handlers are discovered automatically via reflection and invoked by KeyboardInputRouter in priority order.
    /// </summary>
    public interface IKeyboardInputHandler
    {
        /// <summary>
        /// Priority level (lower = higher priority, processed first).
        /// Use constants from InputHandlerPriority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Whether this handler should receive input.
        /// Router only invokes active handlers.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Handle keyboard input.
        /// </summary>
        /// <param name="context">Input context with key and modifiers</param>
        /// <returns>True if event was consumed (prevent further routing), false to continue routing</returns>
        bool HandleInput(KeyboardInputContext context);
    }
}
