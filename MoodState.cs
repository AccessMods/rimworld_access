using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Pawn selectedPawn = null;
            if (MapNavigationState.IsInitialized)
            {
                IntVec3 cursorPosition = MapNavigationState.CurrentCursorPosition;
                if (cursorPosition.IsValid && cursorPosition.InBounds(Find.CurrentMap))
                {
                    selectedPawn = Find.CurrentMap.thingGrid.ThingsListAt(cursorPosition)
                        .OfType<Pawn>().FirstOrDefault();
                }
            }

            // Fall back to selected pawn
            if (selectedPawn == null)
                selectedPawn = Find.Selector?.FirstSelectedObject as Pawn;

            if (selectedPawn == null)
            {
                TolkHelper.Speak("No pawn selected");
                return;
            }

            // Check if pawn has needs
            if (selectedPawn.needs == null)
            {
                TolkHelper.Speak($"{selectedPawn.LabelShort} has no needs");
                return;
            }

            // Check if pawn has mood need
            if (selectedPawn.needs.mood == null)
            {
                TolkHelper.Speak($"{selectedPawn.LabelShort} has no mood");
                return;
            }

            // Build mood information
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {selectedPawn.LabelShort} Mood ===");

            Need_Mood mood = selectedPawn.needs.mood;

            // Current mood level and description
            float moodPercentage = mood.CurLevelPercentage * 100f;
            string moodDescription = mood.MoodString;
            sb.AppendLine($"Mood: {moodPercentage:F0}% ({moodDescription})");

            // Mental break thresholds (only if pawn can have mental breaks)
            if (selectedPawn.mindState?.mentalBreaker != null &&
                selectedPawn.mindState.mentalBreaker.CanDoRandomMentalBreaks)
            {
                sb.AppendLine($"\nBreak Thresholds:");
                sb.AppendLine($"  Minor: {selectedPawn.mindState.mentalBreaker.BreakThresholdMinor:P0}");
                sb.AppendLine($"  Major: {selectedPawn.mindState.mentalBreaker.BreakThresholdMajor:P0}");
                sb.AppendLine($"  Extreme: {selectedPawn.mindState.mentalBreaker.BreakThresholdExtreme:P0}");
            }

            // Get thoughts affecting mood
            List<Thought> thoughtGroups = new List<Thought>();
            PawnNeedsUIUtility.GetThoughtGroupsInDisplayOrder(mood, thoughtGroups);

            if (thoughtGroups.Count > 0)
            {
                sb.AppendLine($"\nThoughts affecting mood ({thoughtGroups.Count}):");

                // Process each thought group
                List<Thought> thoughtGroup = new List<Thought>();
                foreach (Thought group in thoughtGroups)
                {
                    mood.thoughts.GetMoodThoughts(group, thoughtGroup);

                    if (thoughtGroup.Count == 0)
                        continue;

                    // Get the leading thought (most severe in the group)
                    Thought leadingThought = PawnNeedsUIUtility.GetLeadingThoughtInGroup(thoughtGroup);

                    if (leadingThought == null || !leadingThought.VisibleInNeedsTab)
                        continue;

                    // Get mood offset for this thought group
                    float moodOffset = mood.thoughts.MoodOffsetOfGroup(group);

                    // Format the thought label
                    string thoughtLabel = leadingThought.LabelCap;
                    if (thoughtGroup.Count > 1)
                    {
                        thoughtLabel = $"{thoughtLabel} x{thoughtGroup.Count}";
                    }

                    // Format mood offset with sign
                    string offsetText = moodOffset.ToString("+0;-0;0");

                    sb.AppendLine($"  {thoughtLabel}: {offsetText}");

                    thoughtGroup.Clear();
                }
            }
            else
            {
                sb.AppendLine("\nNo thoughts affecting mood");
            }

            // Copy to clipboard for screen reader
            TolkHelper.Speak(sb.ToString().TrimEnd());
        }
    }
}
