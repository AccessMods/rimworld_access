using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State class for displaying mood information of the selected pawn.
    /// Triggered by Alt+M key combination.
    /// </summary>
    public static class MoodState
    {
        /// <summary>
        /// Displays mood information for the currently selected pawn.
        /// Shows mood level, mood description, and all thoughts affecting mood.
        /// </summary>
        public static void DisplayMoodInfo()
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

            // Get mood information using PawnInfoHelper
            string moodInfo = PawnInfoHelper.GetMoodInfo(pawnAtCursor);

            TolkHelper.Speak(moodInfo);
        }
    }
}
