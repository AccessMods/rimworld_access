using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for collecting and categorizing items on the map for the jump menu.
    /// Items are organized into categories and sorted by distance from the cursor.
    /// Filters out debris (rubble, chunks, slag) and separates natural terrain from buildings.
    /// </summary>
    public static class JumpMenuHelper
    {
        /// <summary>
        /// Represents an item in the jump menu with its distance from the cursor.
        /// </summary>
        public class JumpMenuItem
        {
            public Thing Thing { get; set; }
            public float Distance { get; set; }
            public string Label { get; set; }

            public JumpMenuItem(Thing thing, float distance)
            {
                Thing = thing;
                Distance = distance;
                Label = thing.LabelShort;
            }
        }

        /// <summary>
        /// Represents a category of items in the jump menu.
        /// </summary>
        public class JumpMenuCategory
        {
            public string Name { get; set; }
            public List<JumpMenuItem> Items { get; set; }
            public bool IsExpanded { get; set; }

            public JumpMenuCategory(string name)
            {
                Name = name;
                Items = new List<JumpMenuItem>();
                IsExpanded = false;
            }
        }

        /// <summary>
        /// Collects all items on the map and organizes them into categories sorted by distance.
        /// Filters out debris items like rubble, chunks, and slag.
        /// </summary>
        /// <param name="map">The current map</param>
        /// <param name="cursorPos">The cursor position to calculate distances from</param>
        /// <returns>List of categories with items</returns>
        public static List<JumpMenuCategory> CollectMapItems(Map map, IntVec3 cursorPos)
        {
            if (map == null)
                return new List<JumpMenuCategory>();

            var categories = new List<JumpMenuCategory>
            {
                new JumpMenuCategory("Colonists"),
                new JumpMenuCategory("Tame Animals"),
                new JumpMenuCategory("Wild Animals"),
                new JumpMenuCategory("Buildings"),
                new JumpMenuCategory("Trees"),
                new JumpMenuCategory("Plants"),
                new JumpMenuCategory("Items"),
                new JumpMenuCategory("Forbidden Items")
            };

            // Get all things on the map
            List<Thing> allThings = map.listerThings.AllThings;

            foreach (Thing thing in allThings)
            {
                // Skip things that are not spawned or are invalid
                if (!thing.Spawned || thing.Position == IntVec3.Invalid)
                    continue;

                // Skip debris items
                if (IsDebrisItem(thing))
                    continue;

                // Calculate distance from cursor
                float distance = thing.Position.DistanceTo(cursorPos);
                JumpMenuItem item = new JumpMenuItem(thing, distance);

                // Categorize the thing
                if (thing is Pawn pawn)
                {
                    CategorizePawn(pawn, item, categories);
                }
                else if (thing is Building building)
                {
                    CategorizeBuilding(building, item, categories);
                }
                else if (thing is Plant plant)
                {
                    CategorizePlant(plant, item, categories);
                }
                else
                {
                    CategorizeItem(thing, item, categories);
                }
            }

            // Sort items within each category by distance (closest first)
            foreach (var category in categories)
            {
                category.Items = category.Items.OrderBy(i => i.Distance).ToList();
            }

            // Remove empty categories
            categories.RemoveAll(c => c.Items.Count == 0);

            return categories;
        }

        /// <summary>
        /// Checks if a thing is debris that should be filtered out (rubble, chunks, slag, filth).
        /// </summary>
        private static bool IsDebrisItem(Thing thing)
        {
            // Filter out filth (rubble, dirt, blood, etc.)
            if (thing.def.IsFilth)
                return true;

            // Filter out chunks (rock chunks, slag chunks, etc.)
            if (thing.def.thingCategories != null)
            {
                foreach (var category in thing.def.thingCategories)
                {
                    if (category == ThingCategoryDefOf.Chunks ||
                        category == ThingCategoryDefOf.StoneChunks)
                        return true;
                }
            }

            // Additional check: filter by defName containing common debris keywords
            string defName = thing.def.defName.ToLower();
            if (defName.Contains("chunk") ||
                defName.Contains("slag") ||
                defName.Contains("rubble"))
                return true;

            return false;
        }

        /// <summary>
        /// Categorizes a pawn into the appropriate category.
        /// </summary>
        private static void CategorizePawn(Pawn pawn, JumpMenuItem item, List<JumpMenuCategory> categories)
        {
            // Check if it's a player faction pawn
            if (pawn.Faction == Faction.OfPlayer)
            {
                if (pawn.RaceProps.Animal)
                {
                    // Tame animal
                    categories.First(c => c.Name == "Tame Animals").Items.Add(item);
                }
                else
                {
                    // Colonist
                    categories.First(c => c.Name == "Colonists").Items.Add(item);
                }
            }
            else if (pawn.RaceProps.Animal)
            {
                // Wild animal
                categories.First(c => c.Name == "Wild Animals").Items.Add(item);
            }
            else
            {
                // Non-player pawn (raiders, visitors, etc.) - treat as colonists category for now
                // Could add a separate category for "Other Pawns" if needed
                categories.First(c => c.Name == "Colonists").Items.Add(item);
            }
        }

        /// <summary>
        /// Categorizes a building, filtering out natural terrain/rock.
        /// Only includes player-constructed buildings.
        /// </summary>
        private static void CategorizeBuilding(Building building, JumpMenuItem item, List<JumpMenuCategory> categories)
        {
            // Filter out natural rock and resource rock (not player-constructed)
            if (building.def.building != null)
            {
                if (building.def.building.isNaturalRock || building.def.building.isResourceRock)
                {
                    // Skip natural terrain like smoothed stone, granite walls, ore deposits
                    return;
                }
            }

            // Only add artificial/constructed buildings
            categories.First(c => c.Name == "Buildings").Items.Add(item);
        }

        /// <summary>
        /// Categorizes a plant into Trees or Plants category.
        /// </summary>
        private static void CategorizePlant(Plant plant, JumpMenuItem item, List<JumpMenuCategory> categories)
        {
            if (plant.def.plant != null && plant.def.plant.IsTree)
            {
                categories.First(c => c.Name == "Trees").Items.Add(item);
            }
            else
            {
                categories.First(c => c.Name == "Plants").Items.Add(item);
            }
        }

        /// <summary>
        /// Categorizes a general item into the Items or Forbidden Items category.
        /// Items like weapons, resources, food, etc.
        /// </summary>
        private static void CategorizeItem(Thing thing, JumpMenuItem item, List<JumpMenuCategory> categories)
        {
            // Check if the item is forbidden
            CompForbiddable forbiddable = thing.TryGetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
            {
                categories.First(c => c.Name == "Forbidden Items").Items.Add(item);
            }
            else
            {
                categories.First(c => c.Name == "Items").Items.Add(item);
            }
        }
    }
}
