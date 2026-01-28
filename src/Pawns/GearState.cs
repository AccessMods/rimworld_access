using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State class for displaying gear information of the pawn at the cursor position.
    /// Triggered by Alt+G key combination.
    /// </summary>
    public static class GearState
    {
        /// <summary>
        /// Displays gear information for the pawn at the current cursor position.
        /// Shows weapon being wielded and apparel being worn, with quality.
        /// </summary>
        public static void DisplayGearInfo()
        {
            // Check if we're in-game
            if (Current.ProgramState != ProgramState.Playing)
            {
                TolkHelper.Speak("Not in game");
                return;
            }

            // Check if there's a current map
            if (Find.CurrentMap == null)
            {
                TolkHelper.Speak("No map loaded");
                return;
            }

            // Try pawn at cursor first
            Pawn pawnAtCursor = null;
            if (MapNavigationState.IsInitialized)
            {
                IntVec3 cursorPosition = MapNavigationState.CurrentCursorPosition;
                if (cursorPosition.IsValid && cursorPosition.InBounds(Find.CurrentMap))
                {
                    pawnAtCursor = Find.CurrentMap.thingGrid.ThingsListAt(cursorPosition)
                        .OfType<Pawn>().FirstOrDefault();
                }
            }

            // Fall back to selected pawn
            if (pawnAtCursor == null)
                pawnAtCursor = Find.Selector?.FirstSelectedObject as Pawn;

            if (pawnAtCursor == null)
            {
                TolkHelper.Speak("No pawn selected");
                return;
            }

            // Get gear information using PawnInfoHelper
            string gearInfo = PawnInfoHelper.GetGearInfo(pawnAtCursor);

            // Speak to screen reader
            TolkHelper.Speak(gearInfo);
        }
    }
}
