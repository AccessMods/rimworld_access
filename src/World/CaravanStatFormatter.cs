using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Shared formatting for caravan/transport pod stat values.
    /// Handles edge cases like "Infinite" food, "Immobile" speed, etc.
    /// Used by CaravanFormationState, SplitCaravanState, and TransportPodLoadingState.
    /// </summary>
    public static class CaravanStatFormatter
    {
        /// <summary>
        /// Formats mass stat with overload warning.
        /// </summary>
        /// <param name="usage">Current mass usage in kg</param>
        /// <param name="capacity">Maximum mass capacity in kg</param>
        /// <returns>Formatted string like "Mass: 150.0 of 500.0 kg" or "Mass: 600.0 of 500.0 kg - OVERLOADED"</returns>
        public static string FormatMass(float usage, float capacity)
        {
            string result = $"Mass: {usage:F1} of {capacity:F1} kg";
            if (usage > capacity)
            {
                result += " - OVERLOADED";
            }
            return result;
        }

        /// <summary>
        /// Formats speed stat with immobile check for overloaded caravans.
        /// </summary>
        /// <param name="tilesPerDay">Movement speed in tiles per day</param>
        /// <param name="isOverloaded">Whether the caravan is over capacity</param>
        /// <param name="includeDescription">Whether to append the tooltip description</param>
        /// <returns>Formatted string like "Speed: 1.5 tiles per day" or "Speed: Immobile"</returns>
        public static string FormatSpeed(float tilesPerDay, bool isOverloaded, bool includeDescription = true)
        {
            string description = includeDescription ? $". {"CaravanMovementSpeedTip".Translate()}" : "";

            if (isOverloaded)
            {
                string immobileLabel = "TilesPerDayImmobile".Translate();
                return $"Speed: {immobileLabel}{description}".TrimEnd('.');
            }
            else if (tilesPerDay > 0)
            {
                return $"Speed: {tilesPerDay:F1} tiles per day{description}".TrimEnd('.');
            }
            else
            {
                return $"Speed: Cannot move{description}".TrimEnd('.');
            }
        }

        /// <summary>
        /// Formats food stat with infinite/none edge cases and spoilage warning.
        /// </summary>
        /// <param name="days">Days worth of food (>= 600 means infinite)</param>
        /// <param name="tillRot">Days until food spoils</param>
        /// <param name="includeDescription">Whether to append the tooltip description</param>
        /// <returns>Formatted string like "Food: 5.2 days, spoils in 3.1 days" or "Food: Infinite"</returns>
        public static string FormatFood(float days, float tillRot, bool includeDescription = true)
        {
            string description = includeDescription ? $". {"DaysWorthOfFoodTooltip".Translate()}" : "";

            // Match game behavior: >= 600 days means "Infinite" (no consumers or tons of food)
            if (days >= 600f)
            {
                string infiniteLabel = "InfiniteDaysWorthOfFoodInfo".Translate();
                return $"Food: {infiniteLabel}{description}".TrimEnd('.');
            }
            else if (days < 0.1f)
            {
                return $"Food: None{description}".TrimEnd('.');
            }
            else
            {
                string result = $"Food: {days:F1} days";
                // Only show spoilage if food will rot before it's consumed AND it's not "infinite"
                if (tillRot < days && tillRot > 0 && tillRot < 600f)
                {
                    result += $", spoils in {tillRot:F1} days";
                }
                result += description;
                return result.TrimEnd('.');
            }
        }

        /// <summary>
        /// Formats foraging stat with food type.
        /// </summary>
        /// <param name="foodType">The type of food foraged (can be null)</param>
        /// <param name="perDay">Amount foraged per day</param>
        /// <param name="includeDescription">Whether to append the tooltip description</param>
        /// <returns>Formatted string like "Foraging: 1.5 (berries) per day"</returns>
        public static string FormatForaging(ThingDef foodType, float perDay, bool includeDescription = true)
        {
            string description = includeDescription ? $". {"ForagedFoodPerDayTip".Translate()}" : "";

            string result = $"Foraging: {perDay:F1}";
            if (perDay > 0 && foodType != null)
            {
                result += $" ({foodType.label})";
            }
            result += $" per day{description}";
            return result.TrimEnd('.');
        }

        /// <summary>
        /// Formats visibility stat as percentage.
        /// </summary>
        /// <param name="visibility">Visibility value (0-1 range)</param>
        /// <param name="includeDescription">Whether to append the tooltip description</param>
        /// <returns>Formatted string like "Visibility: 75%"</returns>
        public static string FormatVisibility(float visibility, bool includeDescription = true)
        {
            string description = includeDescription ? $". {"CaravanVisibilityTip".Translate()}" : "";
            return $"Visibility: {visibility:P0}{description}".TrimEnd('.');
        }
    }
}
