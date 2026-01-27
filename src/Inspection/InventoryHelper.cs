using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for collecting and organizing colony-wide inventory data
    /// </summary>
    public static class InventoryHelper
    {
        /// <summary>
        /// Represents an aggregated inventory item with its total quantity and storage locations.
        /// Items are grouped by ThingDef + Stuff (material) + Quality to avoid incorrectly
        /// combining items like "steel knife (excellent)" with "plasteel knife (poor)".
        /// </summary>
        public class InventoryItem
        {
            public ThingDef Def { get; set; }
            public ThingDef Stuff { get; set; } // Material the item is made of (null if no stuff)
            public QualityCategory? Quality { get; set; } // Quality level (null if no quality component)
            public int TotalQuantity { get; set; }
            public List<IntVec3> StorageLocations { get; set; }
            public List<Thing> Things { get; set; } // Actual thing references for actions like Install
            public bool IsMinifiedThing { get; set; } // True if these are uninstalled furniture
            public Pawn CarrierPawn { get; set; } // Pawn carrying this item (null for storage items)

            /// <summary>
            /// True if this item is being carried by a pawn rather than stored in a stockpile/building
            /// </summary>
            public bool IsCarried => CarrierPawn != null;

            public InventoryItem(ThingDef def, ThingDef stuff = null, QualityCategory? quality = null)
            {
                Def = def;
                Stuff = stuff;
                Quality = quality;
                TotalQuantity = 0;
                StorageLocations = new List<IntVec3>();
                Things = new List<Thing>();
                IsMinifiedThing = false;
                CarrierPawn = null;
            }

            public string GetDisplayLabel()
            {
                // Build label with material prefix if applicable
                string itemName;
                if (Stuff != null)
                {
                    itemName = $"{Stuff.LabelAsStuff} {Def.label}";
                }
                else
                {
                    itemName = Def.label;
                }

                // Capitalize first letter
                if (!string.IsNullOrEmpty(itemName))
                {
                    itemName = char.ToUpper(itemName[0]) + itemName.Substring(1);
                }

                // Add quality if applicable
                string qualitySuffix = Quality.HasValue ? $" ({Quality.Value})" : "";

                if (IsCarried)
                {
                    return $"{itemName}{qualitySuffix} x{TotalQuantity} (carried by {CarrierPawn.LabelShort})";
                }
                return $"{itemName}{qualitySuffix} x{TotalQuantity}";
            }
        }

        /// <summary>
        /// Represents a category with its items and subcategories
        /// </summary>
        public class CategoryNode
        {
            public ThingCategoryDef CategoryDef { get; set; }
            public List<InventoryItem> Items { get; set; }
            public List<CategoryNode> SubCategories { get; set; }
            public int TotalItemCount { get; set; }

            public CategoryNode(ThingCategoryDef categoryDef)
            {
                CategoryDef = categoryDef;
                Items = new List<InventoryItem>();
                SubCategories = new List<CategoryNode>();
                TotalItemCount = 0;
            }

            public string GetDisplayLabel()
            {
                if (CategoryDef == null) return "Uncategorized";
                if (Items.Count > 0)
                {
                    return $"{CategoryDef.LabelCap} ({Items.Count} types)";
                }
                return CategoryDef.LabelCap;
            }
        }

        /// <summary>
        /// Collects all items from stockpiles and storage buildings across the colony.
        /// Uses a HashSet to prevent duplicate counting.
        /// </summary>
        public static List<Thing> GetAllStoredItems()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("InventoryHelper: Cannot get stored items - no current map");
                return new List<Thing>();
            }

            // Use HashSet to prevent duplicates (items on shelves in stockpiles could be counted twice)
            HashSet<Thing> uniqueItems = new HashSet<Thing>();

            // Get items from stockpiles
            if (map.zoneManager?.AllZones != null)
            {
                foreach (Zone zone in map.zoneManager.AllZones)
                {
                    if (zone is Zone_Stockpile stockpile)
                    {
                        SlotGroup slotGroup = stockpile.GetSlotGroup();
                        if (slotGroup?.HeldThings != null)
                        {
                            foreach (Thing item in slotGroup.HeldThings)
                            {
                                uniqueItems.Add(item);
                            }
                        }
                    }
                }
            }

            // Get items from storage buildings
            if (map.listerBuildings != null)
            {
                foreach (Building_Storage storage in map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>())
                {
                    SlotGroup slotGroup = storage.GetSlotGroup();
                    if (slotGroup?.HeldThings != null)
                    {
                        foreach (Thing item in slotGroup.HeldThings)
                        {
                            uniqueItems.Add(item);
                        }
                    }
                }
            }

            return uniqueItems.ToList();
        }

        /// <summary>
        /// Collects all items carried by owned pawns (colonists and animals) on the current map.
        /// Returns a dictionary mapping each Thing to its carrier Pawn.
        /// </summary>
        public static Dictionary<Thing, Pawn> GetAllPawnCarriedItems()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("InventoryHelper: Cannot get pawn-carried items - no current map");
                return new Dictionary<Thing, Pawn>();
            }

            Dictionary<Thing, Pawn> carriedItems = new Dictionary<Thing, Pawn>();

            // Get items from player faction pawns (colonists and animals)
            foreach (Pawn pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (pawn.inventory?.innerContainer == null) continue;

                foreach (Thing item in pawn.inventory.innerContainer)
                {
                    if (item != null)
                    {
                        carriedItems[item] = pawn;
                    }
                }
            }

            return carriedItems;
        }

        /// <summary>
        /// Aggregates items by ThingDef + Stuff (material) + Quality, summing quantities and tracking locations.
        /// This ensures items like "steel knife (excellent)" and "plasteel knife (poor)" are shown separately.
        /// For MinifiedThings (uninstalled furniture), uses the inner thing's properties.
        /// </summary>
        public static List<InventoryItem> AggregateStacks(List<Thing> items)
        {
            // Key: (ThingDef, Stuff, Quality) - keeps items with different materials/quality separate
            Dictionary<(ThingDef, ThingDef, QualityCategory?), InventoryItem> aggregated =
                new Dictionary<(ThingDef, ThingDef, QualityCategory?), InventoryItem>();

            foreach (Thing item in items)
            {
                if (item?.def == null) continue;

                // For MinifiedThings, use the inner thing's properties for categorization
                Thing thingToCheck = item;
                bool isMinified = item is MinifiedThing;
                if (isMinified)
                {
                    Thing innerThing = item.GetInnerIfMinified();
                    if (innerThing != null)
                    {
                        thingToCheck = innerThing;
                    }
                }

                ThingDef defToUse = thingToCheck.def;
                ThingDef stuffToUse = thingToCheck.Stuff;

                // Get quality if the item has a quality component
                QualityCategory? qualityToUse = null;
                var qualityComp = thingToCheck.TryGetComp<CompQuality>();
                if (qualityComp != null)
                {
                    qualityToUse = qualityComp.Quality;
                }

                var key = (defToUse, stuffToUse, qualityToUse);
                if (!aggregated.ContainsKey(key))
                {
                    aggregated[key] = new InventoryItem(defToUse, stuffToUse, qualityToUse);
                    aggregated[key].IsMinifiedThing = isMinified;
                }

                InventoryItem invItem = aggregated[key];
                invItem.TotalQuantity += item.stackCount;

                // Store reference to actual thing (for Install action)
                if (invItem.Things.Count < 10)
                {
                    invItem.Things.Add(item);
                }

                // Store the location of this item (for jump-to functionality)
                if (invItem.StorageLocations.Count < 10)
                {
                    IntVec3 position = item.Position;
                    if (!invItem.StorageLocations.Contains(position))
                    {
                        invItem.StorageLocations.Add(position);
                    }
                }
            }

            return aggregated.Values.ToList();
        }

        /// <summary>
        /// Aggregates pawn-carried items, keeping items from different pawns separate.
        /// Each pawn + ThingDef + Stuff + Quality combination becomes a separate InventoryItem.
        /// </summary>
        public static List<InventoryItem> AggregatePawnCarriedItems(Dictionary<Thing, Pawn> carriedItems)
        {
            // Key: (ThingDef, Stuff, Quality, Pawn) - keeps items from different pawns and with different properties separate
            Dictionary<(ThingDef, ThingDef, QualityCategory?, Pawn), InventoryItem> aggregated =
                new Dictionary<(ThingDef, ThingDef, QualityCategory?, Pawn), InventoryItem>();

            foreach (var kvp in carriedItems)
            {
                Thing item = kvp.Key;
                Pawn carrier = kvp.Value;

                if (item?.def == null || carrier == null) continue;

                // For MinifiedThings, use the inner thing's properties for categorization
                Thing thingToCheck = item;
                bool isMinified = item is MinifiedThing;
                if (isMinified)
                {
                    Thing innerThing = item.GetInnerIfMinified();
                    if (innerThing != null)
                    {
                        thingToCheck = innerThing;
                    }
                }

                ThingDef defToUse = thingToCheck.def;
                ThingDef stuffToUse = thingToCheck.Stuff;

                // Get quality if the item has a quality component
                QualityCategory? qualityToUse = null;
                var qualityComp = thingToCheck.TryGetComp<CompQuality>();
                if (qualityComp != null)
                {
                    qualityToUse = qualityComp.Quality;
                }

                var key = (defToUse, stuffToUse, qualityToUse, carrier);
                if (!aggregated.ContainsKey(key))
                {
                    aggregated[key] = new InventoryItem(defToUse, stuffToUse, qualityToUse)
                    {
                        CarrierPawn = carrier,
                        IsMinifiedThing = isMinified
                    };
                }

                InventoryItem invItem = aggregated[key];
                invItem.TotalQuantity += item.stackCount;

                // Store reference to actual thing
                if (invItem.Things.Count < 10)
                {
                    invItem.Things.Add(item);
                }

                // For carried items, storage location is the carrier's position
                if (invItem.StorageLocations.Count == 0)
                {
                    invItem.StorageLocations.Add(carrier.Position);
                }
            }

            return aggregated.Values.ToList();
        }

        /// <summary>
        /// Groups inventory items by their categories, building a hierarchical tree
        /// </summary>
        /// <param name="storageItems">Storage items aggregated by ThingDef + Stuff + Quality</param>
        /// <param name="pawnCarriedItems">Optional list of pawn-carried items (kept separate per pawn)</param>
        public static List<CategoryNode> BuildCategoryTree(List<InventoryItem> storageItems, List<InventoryItem> pawnCarriedItems = null)
        {
            // Build a dictionary of all categories that have items
            Dictionary<ThingCategoryDef, CategoryNode> categoryNodes = new Dictionary<ThingCategoryDef, CategoryNode>();

            // Helper method to add an item to its categories
            void AddItemToCategories(InventoryItem item)
            {
                ThingDef thingDef = item.Def;
                if (thingDef.thingCategories == null || thingDef.thingCategories.Count == 0)
                {
                    // Item has no category - skip it
                    return;
                }

                // Add item to all its categories
                foreach (ThingCategoryDef category in thingDef.thingCategories)
                {
                    // Ensure category node exists
                    if (!categoryNodes.ContainsKey(category))
                    {
                        categoryNodes[category] = new CategoryNode(category);
                    }

                    // Add item to this category
                    categoryNodes[category].Items.Add(item);
                    categoryNodes[category].TotalItemCount++;

                    // Ensure all parent categories exist
                    ThingCategoryDef parentCategory = category.parent;
                    while (parentCategory != null)
                    {
                        if (!categoryNodes.ContainsKey(parentCategory))
                        {
                            categoryNodes[parentCategory] = new CategoryNode(parentCategory);
                        }
                        categoryNodes[parentCategory].TotalItemCount++;
                        parentCategory = parentCategory.parent;
                    }
                }
            }

            // Add storage items
            foreach (InventoryItem item in storageItems)
            {
                AddItemToCategories(item);
            }

            // Add pawn-carried items (if any)
            if (pawnCarriedItems != null)
            {
                foreach (InventoryItem item in pawnCarriedItems)
                {
                    AddItemToCategories(item);
                }
            }

            // Collect uncategorized items (items with no thingCategories)
            List<InventoryItem> uncategorizedItems = new List<InventoryItem>();
            foreach (InventoryItem item in storageItems)
            {
                ThingDef thingDef = item.Def;
                if (thingDef.thingCategories == null || thingDef.thingCategories.Count == 0)
                {
                    uncategorizedItems.Add(item);
                }
            }
            if (pawnCarriedItems != null)
            {
                foreach (InventoryItem item in pawnCarriedItems)
                {
                    if (item.Def.thingCategories == null || item.Def.thingCategories.Count == 0)
                    {
                        uncategorizedItems.Add(item);
                    }
                }
            }

            // Build the tree structure by linking parents and children
            foreach (var kvp in categoryNodes)
            {
                ThingCategoryDef category = kvp.Key;
                CategoryNode node = kvp.Value;

                if (category.parent != null && categoryNodes.ContainsKey(category.parent))
                {
                    CategoryNode parentNode = categoryNodes[category.parent];
                    if (!parentNode.SubCategories.Contains(node))
                    {
                        parentNode.SubCategories.Add(node);
                    }
                }
            }

            // Find root categories (categories with no parent or whose parent isn't in our tree)
            // Skip the actual "Root" ThingCategoryDef and treat its children as top-level
            List<CategoryNode> rootCategories = new List<CategoryNode>();
            foreach (var kvp in categoryNodes)
            {
                ThingCategoryDef category = kvp.Key;
                CategoryNode node = kvp.Value;

                // Skip the actual "Root" category - we'll show its children instead
                if (category == ThingCategoryDefOf.Root)
                    continue;

                // This is a root if it has no parent, its parent isn't in our category set,
                // or its parent is the "Root" category (which we're skipping)
                if (category.parent == null ||
                    !categoryNodes.ContainsKey(category.parent) ||
                    category.parent == ThingCategoryDefOf.Root)
                {
                    rootCategories.Add(node);
                }
            }

            // Sort root categories by label
            rootCategories.Sort((a, b) => string.Compare(a.CategoryDef.label, b.CategoryDef.label));

            // Sort subcategories and items within each node
            SortCategoryNode(rootCategories);

            // Add uncategorized node if there are any uncategorized items
            if (uncategorizedItems.Count > 0)
            {
                var uncategorizedNode = new CategoryNode(null); // null signals uncategorized
                foreach (var item in uncategorizedItems)
                {
                    uncategorizedNode.Items.Add(item);
                }
                uncategorizedNode.TotalItemCount = uncategorizedItems.Count;
                uncategorizedNode.Items.Sort((a, b) => string.Compare(a.Def.label, b.Def.label));
                rootCategories.Add(uncategorizedNode);
            }

            return rootCategories;
        }

        /// <summary>
        /// Recursively sorts subcategories and items within a category tree
        /// </summary>
        private static void SortCategoryNode(List<CategoryNode> nodes)
        {
            foreach (CategoryNode node in nodes)
            {
                // Sort subcategories alphabetically
                if (node.SubCategories.Count > 0)
                {
                    node.SubCategories.Sort((a, b) => string.Compare(a.CategoryDef.label, b.CategoryDef.label));
                    SortCategoryNode(node.SubCategories); // Recurse
                }

                // Sort items alphabetically
                if (node.Items.Count > 0)
                {
                    node.Items.Sort((a, b) => string.Compare(a.Def.label, b.Def.label));
                }
            }
        }

        /// <summary>
        /// Gets the first storage location for a given ThingDef
        /// </summary>
        public static IntVec3? FindFirstStorageLocation(ThingDef thingDef)
        {
            Map map = Find.CurrentMap;
            if (map == null) return null;

            // Check stockpiles
            if (map.zoneManager?.AllZones != null)
            {
                foreach (Zone zone in map.zoneManager.AllZones)
                {
                    if (zone is Zone_Stockpile stockpile)
                    {
                        SlotGroup slotGroup = stockpile.GetSlotGroup();
                        if (slotGroup?.HeldThings != null)
                        {
                            foreach (Thing item in slotGroup.HeldThings)
                            {
                                if (item.def == thingDef)
                                {
                                    return item.Position;
                                }
                            }
                        }
                    }
                }
            }

            // Check storage buildings
            if (map.listerBuildings != null)
            {
                foreach (Building_Storage storage in map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>())
                {
                    SlotGroup slotGroup = storage.GetSlotGroup();
                    if (slotGroup?.HeldThings != null)
                    {
                        foreach (Thing item in slotGroup.HeldThings)
                        {
                            if (item.def == thingDef)
                            {
                                return item.Position;
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
