using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch for WorldInterface.HandleLowPriorityInput to add keyboard navigation for world map.
    /// Intercepts arrow key input to navigate between world tiles.
    /// Automatically opens/closes world navigation state when entering/leaving world view.
    /// </summary>
    [HarmonyPatch(typeof(WorldInterface))]
    [HarmonyPatch("HandleLowPriorityInput")]
    public static class WorldNavigationPatch
    {
        private static bool lastFrameWasWorldView = false;

        /// <summary>
        /// Prefix patch that intercepts keyboard input for world map navigation.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static void Prefix()
        {
            // Detect if we're in world view
            bool isWorldView = Current.ProgramState == ProgramState.Playing &&
                              Find.World != null &&
                              Find.World.renderer != null &&
                              Find.World.renderer.wantedMode == WorldRenderMode.Planet;

            // Handle state transitions
            if (isWorldView && !lastFrameWasWorldView)
            {
                // Just entered world view
                WorldNavigationState.Open();
            }
            else if (!isWorldView && lastFrameWasWorldView)
            {
                // Just left world view
                WorldNavigationState.Close();
            }

            lastFrameWasWorldView = isWorldView;

            // Only process input if in world view and state is active
            if (!isWorldView || !WorldNavigationState.IsActive)
                return;

            // Skip world navigation input if caravan formation dialog is active
            // (the dialog handles its own input via CaravanFormationPatch)
            // BUT allow navigation when choosing destination
            if (CaravanFormationState.IsActive && !CaravanFormationState.IsChoosingDestination)
                return;

            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            KeyCode key = Event.current.keyCode;

            // Skip if no actual key
            if (key == KeyCode.None)
                return;

            // Check for modifier keys
            bool shift = Event.current.shift;
            bool ctrl = Event.current.control;
            bool alt = Event.current.alt;

            // Handle caravan stats viewer input first if it's active
            if (CaravanStatsState.IsActive)
            {
                if (CaravanStatsState.HandleInput(key))
                {
                    Event.current.Use();
                    return;
                }
            }

            // Handle quest locations browser input if it's active
            if (QuestLocationsBrowserState.IsActive)
            {
                if (QuestLocationsBrowserState.HandleInput(key))
                {
                    Event.current.Use();
                    return;
                }
            }

            // Handle settlement browser input if it's active
            if (SettlementBrowserState.IsActive)
            {
                if (SettlementBrowserState.HandleInput(key))
                {
                    Event.current.Use();
                    return;
                }
            }

            // Handle arrow key navigation
            if (key == KeyCode.UpArrow || key == KeyCode.DownArrow ||
                key == KeyCode.LeftArrow || key == KeyCode.RightArrow)
            {
                WorldNavigationState.HandleArrowKey(key);
                Event.current.Use();
                return;
            }

            // Handle Home key - jump to player's home settlement
            if (key == KeyCode.Home && !shift && !ctrl && !alt)
            {
                WorldNavigationState.JumpToHome();
                Event.current.Use();
                return;
            }

            // Handle End key - jump to nearest player caravan
            if (key == KeyCode.End && !shift && !ctrl && !alt)
            {
                WorldNavigationState.JumpToNearestCaravan();
                Event.current.Use();
                return;
            }

            // Note: Comma and Period keys for caravan cycling are handled in UnifiedKeyboardPatch
            // at a higher priority to prevent colonist selection from intercepting them

            // Handle S key - open settlement browser
            if (key == KeyCode.S && !shift && !ctrl && !alt)
            {
                WorldNavigationState.OpenSettlementBrowser();
                Event.current.Use();
                return;
            }

            // Handle Q key - open quest locations browser
            if (key == KeyCode.Q && !shift && !ctrl && !alt)
            {
                WorldNavigationState.OpenQuestLocationsBrowser();
                Event.current.Use();
                return;
            }

            // Handle I key - show caravan stats (if caravan selected) or read detailed tile information
            if (key == KeyCode.I && !shift && !ctrl && !alt)
            {
                Caravan selectedCaravan = WorldNavigationState.GetSelectedCaravan();
                if (selectedCaravan != null)
                {
                    WorldNavigationState.ShowCaravanStats();
                }
                else
                {
                    WorldNavigationState.ReadDetailedTileInfo();
                }
                Event.current.Use();
                return;
            }

            // Handle C key - form caravan at selected settlement
            if (key == KeyCode.C && !shift && !ctrl && !alt)
            {
                WorldNavigationState.FormCaravanAtSelectedSettlement();
                Event.current.Use();
                return;
            }

            // Handle ] key - give orders to selected caravan
            if (key == KeyCode.RightBracket && !shift && !ctrl && !alt)
            {
                WorldNavigationState.GiveCaravanOrders();
                Event.current.Use();
                return;
            }

            // Handle Enter key - set caravan destination if in destination selection mode
            if ((key == KeyCode.Return || key == KeyCode.KeypadEnter) && !shift && !ctrl && !alt)
            {
                if (CaravanFormationState.IsChoosingDestination)
                {
                    CaravanFormationState.SetDestination(WorldNavigationState.CurrentSelectedTile);
                    Event.current.Use();
                    return;
                }
            }

            // Handle Escape key - close caravan stats, quest locations browser, settlement browser, cancel destination selection, or let RimWorld handle it
            if (key == KeyCode.Escape)
            {
                if (CaravanFormationState.IsChoosingDestination)
                {
                    CaravanFormationState.CancelDestinationSelection();
                    Event.current.Use();
                    return;
                }
                else if (CaravanStatsState.IsActive)
                {
                    CaravanStatsState.Close();
                    Event.current.Use();
                    return;
                }
                else if (QuestLocationsBrowserState.IsActive)
                {
                    QuestLocationsBrowserState.Close();
                    Event.current.Use();
                    return;
                }
                else if (SettlementBrowserState.IsActive)
                {
                    SettlementBrowserState.Close();
                    Event.current.Use();
                    return;
                }
                // Otherwise, let RimWorld's default behavior handle it (return to map)
            }
        }

        /// <summary>
        /// Postfix patch to draw visual highlight on selected tile.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Only draw if world navigation is active
            if (!WorldNavigationState.IsActive || !WorldNavigationState.IsInitialized)
                return;

            PlanetTile selectedTile = WorldNavigationState.CurrentSelectedTile;
            if (!selectedTile.Valid)
                return;

            // Draw highlight using RimWorld's world renderer
            // The game's WorldSelector already handles drawing selection highlights,
            // so we just need to ensure our tile is selected in the game's system
            // (which is already done in WorldNavigationState)

            // For additional visual feedback, we could draw text overlay
            DrawTileInfoOverlay(selectedTile);
        }

        /// <summary>
        /// Draws an overlay showing current tile information at the top of the screen.
        /// </summary>
        private static void DrawTileInfoOverlay(PlanetTile tile)
        {
            if (!tile.Valid)
                return;

            // Get screen dimensions
            float screenWidth = UI.screenWidth;

            // Create overlay rect (top-center of screen)
            float overlayWidth = 600f;
            float overlayHeight = 80f;
            float overlayX = (screenWidth - overlayWidth) / 2f;
            float overlayY = 20f;

            Rect overlayRect = new Rect(overlayX, overlayY, overlayWidth, overlayHeight);

            // Draw semi-transparent background
            Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            Widgets.DrawBoxSolid(overlayRect, backgroundColor);

            // Draw border
            Color borderColor = new Color(0.5f, 0.7f, 1.0f, 1.0f);
            Widgets.DrawBox(overlayRect, 2);

            // Draw text
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Get tile info
            string tileInfo = WorldInfoHelper.GetTileSummary(tile);
            string instructions = "Arrows: Navigate | Home: Home | End: Caravan | ,/.: Cycle Caravans | S: Settlements | Q: Quest Locations | I: Details | C: Form | ]: Orders";

            Rect infoRect = new Rect(overlayX, overlayY + 15f, overlayWidth, 30f);
            Rect instructionsRect = new Rect(overlayX, overlayY + 45f, overlayWidth, 25f);

            Widgets.Label(infoRect, tileInfo);

            Text.Font = GameFont.Tiny;
            Widgets.Label(instructionsRect, instructions);

            // Reset text settings
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
