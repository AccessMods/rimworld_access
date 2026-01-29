using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Shared helper methods for pawn-related functionality.
    /// </summary>
    public static class PawnHelper
    {
        /// <summary>
        /// Gets the current activity/job for a pawn.
        /// Returns null if no activity or job report fails.
        /// </summary>
        public static string GetPawnActivity(Pawn pawn)
        {
            if (pawn?.CurJob == null) return null;
            try
            {
                string activity = pawn.CurJob.GetReport(pawn);
                return string.IsNullOrEmpty(activity) ? null : activity;
            }
            catch
            {
                // Job report can sometimes fail, just return null
                return null;
            }
        }
    }
}
