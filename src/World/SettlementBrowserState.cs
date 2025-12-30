using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Faction filter type for settlement browser.
    /// </summary>
    public enum FactionFilter
    {
        All,
        Player,
        Allied,
        Neutral,
        Hostile
    }

    /// <summary>
    /// State management for the settlement browser (S key in world view).
    /// Allows browsing settlements by faction and distance.
    /// </summary>
    public static class SettlementBrowserState
    {
        private static bool isActive = false;
        private static List<Settlement> filteredSettlements = new List<Settlement>();
        private static int currentIndex = 0;
        private static FactionFilter currentFilter = FactionFilter.All;
        private static PlanetTile originTile = PlanetTile.Invalid;

        /// <summary>
        /// Gets whether the settlement browser is currently active.
        /// </summary>
        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the settlement browser from the specified origin tile.
        /// </summary>
        public static void Open(PlanetTile origin)
        {
            if (Find.WorldObjects == null)
            {
                TolkHelper.Speak("World objects not available", SpeechPriority.High);
                return;
            }

            isActive = true;
            originTile = origin;
            currentIndex = 0;
            currentFilter = FactionFilter.All;

            RefreshSettlementList();

            if (filteredSettlements.Count == 0)
            {
                TolkHelper.Speak("No settlements found");
                return;
            }

            AnnounceCurrentSettlement();
        }

        /// <summary>
        /// Closes the settlement browser.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            filteredSettlements.Clear();
            currentIndex = 0;
            originTile = PlanetTile.Invalid;
            TolkHelper.Speak("Settlement browser closed");
        }

        /// <summary>
        /// Refreshes the settlement list based on current filter.
        /// </summary>
        private static void RefreshSettlementList()
        {
            if (Find.WorldObjects?.Settlements == null)
            {
                filteredSettlements.Clear();
                return;
            }

            List<Settlement> allSettlements = Find.WorldObjects.Settlements;

            // Filter by faction relationship
            IEnumerable<Settlement> filtered = allSettlements;

            switch (currentFilter)
            {
                case FactionFilter.Player:
                    filtered = allSettlements.Where(s => s.Faction == Faction.OfPlayer);
                    break;

                case FactionFilter.Allied:
                    filtered = allSettlements.Where(s =>
                        s.Faction != Faction.OfPlayer &&
                        !s.Faction.HostileTo(Faction.OfPlayer) &&
                        s.Faction.PlayerRelationKind == FactionRelationKind.Ally);
                    break;

                case FactionFilter.Neutral:
                    filtered = allSettlements.Where(s =>
                        s.Faction != Faction.OfPlayer &&
                        !s.Faction.HostileTo(Faction.OfPlayer) &&
                        s.Faction.PlayerRelationKind == FactionRelationKind.Neutral);
                    break;

                case FactionFilter.Hostile:
                    filtered = allSettlements.Where(s =>
                        s.Faction != Faction.OfPlayer &&
                        s.Faction.HostileTo(Faction.OfPlayer));
                    break;

                case FactionFilter.All:
                default:
                    // No filtering
                    break;
            }

            // Sort by distance from origin tile
            if (originTile.Valid && Find.WorldGrid != null)
            {
                filteredSettlements = filtered
                    .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(originTile, s.Tile))
                    .ToList();
            }
            else
            {
                filteredSettlements = filtered.ToList();
            }

            // Validate current index
            if (currentIndex >= filteredSettlements.Count)
                currentIndex = 0;
        }

        /// <summary>
        /// Cycles to the next faction filter.
        /// </summary>
        public static void NextFilter()
        {
            currentFilter = (FactionFilter)(((int)currentFilter + 1) % 5);
            currentIndex = 0;
            RefreshSettlementList();
            AnnounceFilter();

            if (filteredSettlements.Count > 0)
            {
                AnnounceCurrentSettlement();
            }
            else
            {
                TolkHelper.Speak("No settlements match this filter");
            }
        }

        /// <summary>
        /// Cycles to the previous faction filter.
        /// </summary>
        public static void PreviousFilter()
        {
            currentFilter = (FactionFilter)(((int)currentFilter + 4) % 5);
            currentIndex = 0;
            RefreshSettlementList();
            AnnounceFilter();

            if (filteredSettlements.Count > 0)
            {
                AnnounceCurrentSettlement();
            }
            else
            {
                TolkHelper.Speak("No settlements match this filter");
            }
        }

        /// <summary>
        /// Selects the next settlement in the list.
        /// </summary>
        public static void SelectNext()
        {
            if (filteredSettlements.Count == 0)
            {
                TolkHelper.Speak("No settlements available");
                return;
            }

            currentIndex++;
            if (currentIndex >= filteredSettlements.Count)
                currentIndex = 0;

            AnnounceCurrentSettlement();
        }

        /// <summary>
        /// Selects the previous settlement in the list.
        /// </summary>
        public static void SelectPrevious()
        {
            if (filteredSettlements.Count == 0)
            {
                TolkHelper.Speak("No settlements available");
                return;
            }

            currentIndex--;
            if (currentIndex < 0)
                currentIndex = filteredSettlements.Count - 1;

            AnnounceCurrentSettlement();
        }

        /// <summary>
        /// Jumps the camera to the currently selected settlement and closes the browser.
        /// </summary>
        public static void JumpToSelected()
        {
            if (filteredSettlements.Count == 0 || currentIndex < 0 || currentIndex >= filteredSettlements.Count)
            {
                TolkHelper.Speak("No settlement selected");
                return;
            }

            Settlement selected = filteredSettlements[currentIndex];

            // Update world navigation state
            WorldNavigationState.CurrentSelectedTile = selected.Tile;

            // Sync with game's selection system
            if (Find.WorldSelector != null)
            {
                Find.WorldSelector.ClearSelection();
                Find.WorldSelector.Select(selected);
                Find.WorldSelector.SelectedTile = selected.Tile;
            }

            // Jump camera
            if (Find.WorldCameraDriver != null)
            {
                Find.WorldCameraDriver.JumpTo(selected.Tile);
            }

            // Close browser first
            Close();

            // Announce the tile info (which includes the settlement name)
            WorldNavigationState.AnnounceTile();
        }

        /// <summary>
        /// Announces the current faction filter.
        /// </summary>
        private static void AnnounceFilter()
        {
            string filterName;
            switch (currentFilter)
            {
                case FactionFilter.Player:
                    filterName = "Player settlements";
                    break;
                case FactionFilter.Allied:
                    filterName = "Allied settlements";
                    break;
                case FactionFilter.Neutral:
                    filterName = "Neutral settlements";
                    break;
                case FactionFilter.Hostile:
                    filterName = "Hostile settlements";
                    break;
                case FactionFilter.All:
                    filterName = "All settlements";
                    break;
                default:
                    filterName = "Unknown filter";
                    break;
            }

            TolkHelper.Speak($"{filterName}, {filteredSettlements.Count} found");
        }

        /// <summary>
        /// Announces the currently selected settlement.
        /// </summary>
        private static void AnnounceCurrentSettlement()
        {
            if (filteredSettlements.Count == 0)
            {
                TolkHelper.Speak("No settlements available");
                return;
            }

            if (currentIndex < 0 || currentIndex >= filteredSettlements.Count)
                return;

            Settlement settlement = filteredSettlements[currentIndex];

            // Calculate distance from origin
            float distance = 0f;
            if (originTile.Valid && Find.WorldGrid != null)
            {
                distance = Find.WorldGrid.ApproxDistanceInTiles(originTile, settlement.Tile);
            }

            // Get faction relationship
            string relationship = "Unknown";
            if (settlement.Faction == Faction.OfPlayer)
            {
                relationship = "Player";
            }
            else if (settlement.Faction.HostileTo(Faction.OfPlayer))
            {
                relationship = "Hostile";
            }
            else
            {
                relationship = settlement.Faction.PlayerRelationKind.GetLabel();
            }

            // Build announcement
            int position = currentIndex + 1;
            int total = filteredSettlements.Count;
            string announcement = $"{position} of {total}: {settlement.Label}, {settlement.Faction.Name}, {relationship}, {distance:F1} tiles";

            TolkHelper.Speak(announcement);
        }

        /// <summary>
        /// Handles keyboard input for the settlement browser.
        /// Called from WorldNavigationPatch or UnifiedKeyboardPatch.
        /// </summary>
        public static bool HandleInput(UnityEngine.KeyCode key)
        {
            if (!isActive)
                return false;

            bool shift = UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) ||
                        UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);

            switch (key)
            {
                case UnityEngine.KeyCode.UpArrow:
                    if (shift)
                    {
                        // Shift+Up does nothing in settlement browser
                        return false;
                    }
                    SelectPrevious();
                    return true;

                case UnityEngine.KeyCode.DownArrow:
                    if (shift)
                    {
                        // Shift+Down does nothing in settlement browser
                        return false;
                    }
                    SelectNext();
                    return true;

                case UnityEngine.KeyCode.LeftArrow:
                    if (shift)
                    {
                        PreviousFilter();
                        return true;
                    }
                    return false;

                case UnityEngine.KeyCode.RightArrow:
                    if (shift)
                    {
                        NextFilter();
                        return true;
                    }
                    return false;

                case UnityEngine.KeyCode.Return:
                case UnityEngine.KeyCode.KeypadEnter:
                    JumpToSelected();
                    return true;

                case UnityEngine.KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return false;
            }
        }
    }
}
