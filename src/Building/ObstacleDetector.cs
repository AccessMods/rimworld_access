using System.Collections.Generic;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Shared helper for detecting obstacles in a set of cells.
    /// Used by both enclosure detection (walls) and zone creation.
    /// </summary>
    public static class ObstacleDetector
    {
        /// <summary>
        /// Finds obstacles in a set of cells.
        /// Obstacles are things that would need to be removed for construction/farming.
        /// </summary>
        /// <param name="map">The current map</param>
        /// <param name="cells">The cells to check for obstacles</param>
        /// <param name="cursorPosition">The cursor position for distance calculations</param>
        /// <returns>List of ScannerItems representing obstacles, sorted by distance</returns>
        public static List<ScannerItem> FindObstacles(
            Map map,
            IEnumerable<IntVec3> cells,
            IntVec3 cursorPosition)
        {
            var obstacles = new List<ScannerItem>();
            var processedThings = new HashSet<Thing>();

            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map))
                    continue;

                // Check for natural rock edifice (mineable rock/ore)
                Building edifice = cell.GetEdifice(map);
                if (edifice != null &&
                    edifice.def.building?.isNaturalRock == true &&
                    !processedThings.Contains(edifice))
                {
                    obstacles.Add(new ScannerItem(edifice, cursorPosition));
                    processedThings.Add(edifice);
                    continue; // Natural rock covers the whole cell, skip other things
                }

                // Check other things at the cell
                List<Thing> things = cell.GetThingList(map);
                foreach (Thing thing in things)
                {
                    if (processedThings.Contains(thing))
                        continue;

                    if (IsObstacle(thing))
                    {
                        obstacles.Add(new ScannerItem(thing, cursorPosition));
                        processedThings.Add(thing);
                    }
                }
            }

            // Sort by distance from cursor (closest first)
            obstacles.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return obstacles;
        }

        /// <summary>
        /// Determines if a thing is an obstacle.
        /// Obstacles: mineable tiles, structures, trees, items on ground.
        /// NOT obstacles: blueprints, frames, pawns.
        /// </summary>
        /// <param name="thing">The thing to check</param>
        /// <returns>True if the thing is an obstacle</returns>
        public static bool IsObstacle(Thing thing)
        {
            if (thing == null)
                return false;

            // Blueprints/frames are NOT obstacles (user placed them intentionally)
            if (thing.def.IsBlueprint || thing.def.IsFrame)
                return false;

            // Pawns are NOT obstacles (they move)
            if (thing is Pawn)
                return false;

            // Filth is NOT an obstacle
            if (thing.def.category == ThingCategory.Filth)
                return false;

            // Natural rock (mineable tiles) IS an obstacle
            if (thing is Building building && building.def.building?.isNaturalRock == true)
                return true;

            // Trees ARE obstacles
            if (thing is Plant plant && plant.def.plant?.IsTree == true)
                return true;

            // Other structures/buildings ARE obstacles
            if (thing is Building)
                return true;

            // Items on ground ARE obstacles
            if (thing.def.category == ThingCategory.Item)
                return true;

            return false;
        }

        /// <summary>
        /// Adds obstacles to the scanner as a temporary category.
        /// If obstacles is empty, removes any existing temporary category.
        /// </summary>
        /// <param name="obstacles">List of obstacle ScannerItems</param>
        /// <param name="categoryName">Name for the scanner category</param>
        public static void AddToScanner(List<ScannerItem> obstacles, string categoryName)
        {
            if (obstacles == null || obstacles.Count == 0)
            {
                ScannerState.RemoveTemporaryCategory();
                return;
            }

            ScannerState.CreateTemporaryCategory(categoryName, obstacles);
        }

        /// <summary>
        /// Removes the obstacle category from the scanner.
        /// </summary>
        public static void ClearFromScanner()
        {
            ScannerState.RemoveTemporaryCategory();
        }
    }
}
