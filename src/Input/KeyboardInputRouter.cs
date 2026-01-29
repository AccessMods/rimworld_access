using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Central registry and dispatcher for keyboard input handlers.
    /// Discovers handlers automatically via reflection (zero registration bookkeeping).
    /// Routes input to active handlers in priority order (lower number = higher priority).
    /// </summary>
    public static class KeyboardInputRouter
    {
        private static List<IKeyboardInputHandler> allHandlers = null;
        private static int lastClosedFrame = -1;

        /// <summary>
        /// Shadow mode control (default: enabled during Phase 1).
        /// When enabled, router logs what would happen but doesn't consume events.
        /// </summary>
        public static bool ShadowModeEnabled { get; set; } = true;

        /// <summary>
        /// Shadow mode logging control (default: enabled).
        /// When enabled, router logs active handlers and priority conflicts.
        /// </summary>
        public static bool ShadowModeLoggingEnabled { get; set; } = true;

        /// <summary>
        /// Lazy initialization - discovers all IKeyboardInputHandler implementations via reflection.
        /// Called once on first ProcessInput call.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (allHandlers != null)
                return;

            try
            {
                // Discover all IKeyboardInputHandler implementations
                var handlerTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IKeyboardInputHandler).IsAssignableFrom(t)
                        && !t.IsInterface && !t.IsAbstract);

                allHandlers = handlerTypes
                    .Select(GetHandlerInstance)
                    .Where(h => h != null)
                    .OrderBy(h => h.Priority)
                    .ToList();

                Log.Message($"[KeyboardInputRouter] Successfully initialized with {allHandlers.Count} handlers");

                if (ShadowModeLoggingEnabled && allHandlers.Count > 0)
                {
                    LogPriorityTable();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KeyboardInputRouter] Failed to initialize: {ex}");
                allHandlers = new List<IKeyboardInputHandler>(); // Empty list to prevent re-initialization attempts
            }
        }

        /// <summary>
        /// Gets handler instance from type (supports singleton pattern for static State classes).
        /// </summary>
        private static IKeyboardInputHandler GetHandlerInstance(Type type)
        {
            try
            {
                // Try static Instance field (for static State classes)
                var instanceField = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceField != null)
                {
                    return instanceField.GetValue(null) as IKeyboardInputHandler;
                }

                // Try static Instance property (for static State classes)
                var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    return instanceProperty.GetValue(null) as IKeyboardInputHandler;
                }

                // For regular classes, try instantiation
                return Activator.CreateInstance(type) as IKeyboardInputHandler;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KeyboardInputRouter] Failed to get instance of {type.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Process keyboard input by dispatching to handlers in priority order.
        /// </summary>
        /// <param name="context">Input context with key and modifiers</param>
        /// <returns>True if event was consumed, false to continue routing</returns>
        public static bool ProcessInput(KeyboardInputContext context)
        {
            EnsureInitialized();

            if (allHandlers == null || allHandlers.Count == 0)
                return false;

            // Shadow mode: log what would happen but don't consume events
            if (ShadowModeEnabled)
            {
                if (ShadowModeLoggingEnabled)
                {
                    LogShadowModeExecution(context);
                }
                return false; // Let UnifiedKeyboardPatch handle normally
            }

            // Active mode: actually route input
            try
            {
                foreach (var handler in allHandlers)
                {
                    if (!handler.IsActive)
                        continue;

                    if (handler.HandleInput(context))
                    {
                        return true; // Event consumed
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KeyboardInputRouter] Error processing input: {ex}");
            }

            return false; // Event not consumed
        }

        /// <summary>
        /// Log shadow mode execution and detect priority conflicts.
        /// </summary>
        private static void LogShadowModeExecution(KeyboardInputContext context)
        {
            var activeHandlers = allHandlers.Where(h => h.IsActive).ToList();

            if (activeHandlers.Count == 0)
                return;

            // Detect priority conflicts (two active handlers at same priority)
            var priorityGroups = activeHandlers.GroupBy(h => h.Priority);
            foreach (var group in priorityGroups.Where(g => g.Count() > 1))
            {
                Log.Warning($"[KeyboardInputRouter] PRIORITY CONFLICT at {group.Key}: " +
                    string.Join(", ", group.Select(h => h.GetType().Name)));
            }

            // Log execution order
            Log.Message($"[KeyboardInputRouter] Active handlers for {context}: " +
                string.Join(" → ", activeHandlers.Select(h => $"{h.GetType().Name}({h.Priority})")));
        }

        /// <summary>
        /// Log priority table (called once during initialization).
        /// </summary>
        private static void LogPriorityTable()
        {
            Log.Message("[KeyboardInputRouter] Handler priority table:");
            foreach (var handler in allHandlers)
            {
                Log.Message($"  {handler.Priority,4}: {handler.GetType().Name}");
            }
        }

        /// <summary>
        /// Notify router that a handler closed this frame (for Escape isolation).
        /// Handlers should call this in their Close() method to prevent parent windows from also closing.
        /// </summary>
        public static void NotifyHandlerClosed()
        {
            lastClosedFrame = Time.frameCount;
        }

        /// <summary>
        /// Check if any handler closed this frame (for Escape isolation).
        /// UnifiedKeyboardPatch uses this to prevent Escape key from propagating to parent windows.
        /// </summary>
        public static bool WasAnyHandlerClosedThisFrame()
        {
            return Time.frameCount == lastClosedFrame;
        }

        /// <summary>
        /// Get all registered handlers (for debugging/testing).
        /// </summary>
        public static IReadOnlyList<IKeyboardInputHandler> GetHandlers()
        {
            EnsureInitialized();
            return allHandlers.AsReadOnly();
        }

        /// <summary>
        /// Reset discovery (for testing).
        /// </summary>
        public static void ResetDiscovery()
        {
            allHandlers = null;
            lastClosedFrame = -1;
        }
    }
}
