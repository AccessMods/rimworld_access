using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State management for transport pod world map targeting.
    /// Tracks when launch targeting is active and provides fuel cost calculations.
    /// Works with WorldScannerState to announce fuel costs for each destination.
    /// </summary>
    public static class TransportPodLaunchState
    {
        private static bool isActive = false;
        private static CompLaunchable currentLaunchable = null;
        private static float cachedFuelLevel = 0f;
        private static float cachedMaxRange = 0f;
        private static bool isConfirmingDestination = false;

        /// <summary>
        /// Gets whether launch targeting mode is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Gets whether we're in the process of confirming a destination.
        /// Used by DialogInterceptionPatch to intercept FloatMenus for arrival options.
        /// </summary>
        public static bool IsConfirmingDestination => isConfirmingDestination;

        /// <summary>
        /// Gets the available fuel level for the current launch.
        /// </summary>
        public static float AvailableFuel => cachedFuelLevel;

        /// <summary>
        /// Gets the maximum launch distance at current fuel level.
        /// </summary>
        public static float MaxRange => cachedMaxRange;

        /// <summary>
        /// Opens launch targeting state when StartChoosingDestination is called.
        /// </summary>
        public static void Open(CompLaunchable launchable)
        {
            if (launchable == null)
            {
                Log.Warning("RimWorld Access: TransportPodLaunchState.Open called with null launchable");
                return;
            }

            currentLaunchable = launchable;
            isActive = true;

            // Cache fuel info
            cachedFuelLevel = TransportPodHelper.GetFuelLevel(launchable);
            cachedMaxRange = TransportPodHelper.GetMaxLaunchDistance(launchable);

            TolkHelper.Speak($"Launch targeting. {cachedFuelLevel:F0} chemfuel available, max range {cachedMaxRange:F0} tiles. Use scanner to browse destinations.");
        }

        /// <summary>
        /// Closes launch targeting state.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentLaunchable = null;
            cachedFuelLevel = 0f;
            cachedMaxRange = 0f;
            isConfirmingDestination = false;
        }

        /// <summary>
        /// Clears the confirming destination flag.
        /// Called after the float menu has been processed.
        /// </summary>
        public static void ClearConfirmingFlag()
        {
            isConfirmingDestination = false;
        }

        /// <summary>
        /// Calculates the fuel cost to reach a destination at the given distance.
        /// </summary>
        public static float CalculateFuelCost(float distanceInTiles)
        {
            if (currentLaunchable == null)
                return float.MaxValue;

            return TransportPodHelper.CalculateFuelCost(currentLaunchable, distanceInTiles);
        }

        /// <summary>
        /// Checks if a destination at the given distance is reachable.
        /// </summary>
        public static bool CanReachDistance(float distanceInTiles)
        {
            return distanceInTiles <= cachedMaxRange;
        }

        /// <summary>
        /// Builds a fuel cost announcement for a destination.
        /// </summary>
        public static string GetFuelCostAnnouncement(float distanceInTiles)
        {
            if (!isActive || currentLaunchable == null)
                return "";

            return TransportPodHelper.BuildFuelCostAnnouncement(currentLaunchable, distanceInTiles);
        }

        /// <summary>
        /// Gets the origin tile for distance calculations.
        /// </summary>
        public static int GetOriginTile()
        {
            if (currentLaunchable?.parent?.Map == null)
                return -1;

            return currentLaunchable.parent.Map.Tile;
        }

        /// <summary>
        /// Handles keyboard input during launch targeting.
        /// Returns true if input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            if (!isActive)
                return false;

            // If WorldTargeter stopped (e.g., after successful launch), close our state
            if (Find.WorldTargeter == null || !Find.WorldTargeter.IsTargeting)
            {
                Close();
                return false;
            }

            // Enter - confirm current destination
            // But skip if WindowlessFloatMenuState is active (arrival options menu is showing)
            if ((key == KeyCode.Return || key == KeyCode.KeypadEnter) && !shift && !ctrl && !alt)
            {
                // If the float menu is showing arrival options, let it handle Enter
                if (WindowlessFloatMenuState.IsActive)
                    return false;

                ConfirmCurrentDestination();
                return true;
            }

            // Escape - cancel targeting
            // But skip if WindowlessFloatMenuState is active (let it close first)
            if (key == KeyCode.Escape)
            {
                if (WindowlessFloatMenuState.IsActive)
                    return false;

                CancelTargeting();
                return true;
            }

            // F - announce fuel status
            if (key == KeyCode.F && !shift && !ctrl && !alt)
            {
                AnnounceFuelStatus();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Confirms the currently selected world tile as the destination.
        /// Uses reflection to invoke the WorldTargeter's action callback, which triggers
        /// the game's arrival options logic (and may create a FloatMenu).
        /// </summary>
        private static void ConfirmCurrentDestination()
        {
            if (!WorldNavigationState.IsActive)
            {
                TolkHelper.Speak("World navigation not active", SpeechPriority.High);
                return;
            }

            PlanetTile selectedTile = WorldNavigationState.CurrentSelectedTile;
            if (!selectedTile.Valid)
            {
                TolkHelper.Speak("No valid tile selected", SpeechPriority.High);
                return;
            }

            // Check if destination is in range
            int originTile = GetOriginTile();
            if (originTile < 0)
            {
                TolkHelper.Speak("Cannot determine origin tile", SpeechPriority.High);
                return;
            }

            float distance = Find.WorldGrid.ApproxDistanceInTiles(originTile, selectedTile);
            if (!CanReachDistance(distance))
            {
                float fuelNeeded = CalculateFuelCost(distance);
                TolkHelper.Speak($"Destination out of range. Need {fuelNeeded:F0} chemfuel, have {cachedFuelLevel:F0}.", SpeechPriority.High);
                return;
            }

            if (Find.WorldTargeter == null || !Find.WorldTargeter.IsTargeting)
            {
                TolkHelper.Speak("World targeter not active", SpeechPriority.High);
                return;
            }

            // Set flag so DialogInterceptionPatch knows to intercept any FloatMenu
            isConfirmingDestination = true;

            try
            {
                // Get the action field from WorldTargeter using reflection
                var actionField = typeof(WorldTargeter).GetField("action", BindingFlags.NonPublic | BindingFlags.Instance);
                if (actionField == null)
                {
                    TolkHelper.Speak("Cannot access world targeter action", SpeechPriority.High);
                    isConfirmingDestination = false;
                    return;
                }

                var action = actionField.GetValue(Find.WorldTargeter) as Func<GlobalTargetInfo, bool>;
                if (action == null)
                {
                    TolkHelper.Speak("No targeting action available", SpeechPriority.High);
                    isConfirmingDestination = false;
                    return;
                }

                // Create GlobalTargetInfo for the selected tile
                // Check for world objects at the tile first (like settlements)
                GlobalTargetInfo targetInfo;
                var worldObjects = Find.WorldObjects?.ObjectsAt(selectedTile)?.ToList();
                if (worldObjects != null && worldObjects.Count > 0)
                {
                    // Use the first world object (typically a settlement or site)
                    targetInfo = new GlobalTargetInfo(worldObjects[0]);
                }
                else
                {
                    // Just a tile with no world object
                    targetInfo = new GlobalTargetInfo(selectedTile);
                }

                // Invoke the action callback
                // If it returns true, targeting is complete (single option was executed)
                // If it returns false, a FloatMenu was likely created for multiple options
                bool completed = action(targetInfo);

                if (completed)
                {
                    // Single option was available and executed (e.g., form caravan on empty tile)
                    Find.WorldTargeter.StopTargeting();
                    isConfirmingDestination = false;

                    // Announce that target was selected (no float menu appeared)
                    TolkHelper.Speak("Target selected", SpeechPriority.Normal);
                    // Don't close here - the action handles what comes next
                }
                else
                {
                    // Multiple options available - FloatMenu should have been intercepted
                    // The flag will be cleared when the menu is processed
                    TolkHelper.Speak($"Destination: {distance:F0} tiles, {TransportPodHelper.BuildFuelCostAnnouncement(currentLaunchable, distance)}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"RimWorld Access: Error confirming destination: {ex}");
                TolkHelper.Speak("Error selecting destination", SpeechPriority.High);
                isConfirmingDestination = false;
            }
        }

        /// <summary>
        /// Cancels launch targeting and returns to map.
        /// </summary>
        private static void CancelTargeting()
        {
            // Cache the return target BEFORE closing state (Close() nulls currentLaunchable)
            Thing returnTarget = currentLaunchable?.parent;
            Map returnMap = returnTarget?.Map;

            // Stop world targeting
            if (Find.WorldTargeter != null && Find.WorldTargeter.IsTargeting)
            {
                Find.WorldTargeter.StopTargeting();
            }

            // Close our state
            Close();
            TolkHelper.Speak("Launch targeting cancelled", SpeechPriority.Normal);

            // Return to map view using cached target
            if (returnMap != null && returnTarget != null)
            {
                CameraJumper.TryJump(returnTarget);
            }
        }

        /// <summary>
        /// Announces the current fuel status.
        /// </summary>
        private static void AnnounceFuelStatus()
        {
            TolkHelper.Speak($"Fuel: {cachedFuelLevel:F0} chemfuel. Max range: {cachedMaxRange:F0} tiles.", SpeechPriority.Normal);
        }

        /// <summary>
        /// Called by WorldScannerState to check if fuel costs should be announced.
        /// </summary>
        public static bool ShouldAnnounceFuelCosts()
        {
            return isActive && currentLaunchable != null;
        }

        /// <summary>
        /// Called by WorldNavigationPatch when world targeting ends.
        /// </summary>
        public static void OnWorldTargetingEnded()
        {
            if (isActive)
            {
                Close();
            }
        }
    }
}
