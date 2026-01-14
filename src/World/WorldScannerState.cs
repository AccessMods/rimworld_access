using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Represents a biome region (contiguous area of the same biome).
    /// </summary>
    public class BiomeRegion
    {
        public PlanetTile CenterTile { get; set; }
        public int TileCount { get; set; }
        public string SizeDescription { get; set; } // e.g., "approximately 75 tiles"
        public float Distance { get; set; } // Distance from cursor to center

        public BiomeRegion(PlanetTile centerTile, int tileCount)
        {
            CenterTile = centerTile;
            TileCount = tileCount;
            SizeDescription = $"approximately {tileCount} tiles";
        }
    }

    /// <summary>
    /// Represents a road segment.
    /// </summary>
    public class RoadSegment
    {
        public PlanetTile CenterTile { get; set; }
        public int TileCount { get; set; }
        public float Distance { get; set; }

        public RoadSegment(PlanetTile centerTile, int tileCount)
        {
            CenterTile = centerTile;
            TileCount = tileCount;
        }
    }

    /// <summary>
    /// Represents an item in the world scanner.
    /// Can be a single world object, or a "type" with multiple instances (biome regions, road segments).
    /// </summary>
    public class WorldScannerItem
    {
        public WorldObject WorldObject { get; set; }
        public PlanetTile Tile { get; set; }
        public string Label { get; set; }
        public string QuestName { get; set; }
        public Faction Faction { get; set; }

        // For biome/road types with multiple instances
        public List<BiomeRegion> BiomeRegions { get; set; }
        public List<RoadSegment> RoadSegments { get; set; }

        public bool HasInstances => (BiomeRegions != null && BiomeRegions.Count > 1) ||
                                    (RoadSegments != null && RoadSegments.Count > 1);
        public int InstanceCount => BiomeRegions?.Count ?? RoadSegments?.Count ?? 1;

        /// <summary>
        /// Constructor for world objects (settlements, caravans, quest sites, etc.)
        /// </summary>
        public WorldScannerItem(WorldObject worldObject)
        {
            WorldObject = worldObject;
            Tile = worldObject.Tile;
            Faction = worldObject.Faction;
            Label = worldObject.LabelShort ?? worldObject.Label ?? "Unknown";
        }

        /// <summary>
        /// Constructor for biome types with regions.
        /// </summary>
        public WorldScannerItem(string biomeName, List<BiomeRegion> regions)
        {
            Label = biomeName;
            BiomeRegions = regions;
            if (regions.Count > 0)
            {
                Tile = regions[0].CenterTile;
            }
        }

        /// <summary>
        /// Constructor for road types with segments.
        /// </summary>
        public WorldScannerItem(string roadName, List<RoadSegment> segments)
        {
            Label = roadName;
            RoadSegments = segments;
            if (segments.Count > 0)
            {
                Tile = segments[0].CenterTile;
            }
        }

        /// <summary>
        /// Gets the tile for a specific instance index.
        /// </summary>
        public PlanetTile GetTileAtInstance(int instanceIndex)
        {
            if (BiomeRegions != null && instanceIndex < BiomeRegions.Count)
                return BiomeRegions[instanceIndex].CenterTile;
            if (RoadSegments != null && instanceIndex < RoadSegments.Count)
                return RoadSegments[instanceIndex].CenterTile;
            return Tile;
        }

        /// <summary>
        /// Calculates distance from origin tile to this item (or a specific instance).
        /// </summary>
        public float GetDistance(PlanetTile fromTile, int instanceIndex = 0)
        {
            if (!fromTile.Valid || Find.WorldGrid == null)
                return 0f;

            PlanetTile targetTile = GetTileAtInstance(instanceIndex);
            if (!targetTile.Valid)
                return 0f;

            return Find.WorldGrid.ApproxDistanceInTiles(fromTile, targetTile);
        }

        /// <summary>
        /// Gets the compass direction from the origin tile to this item.
        /// </summary>
        public string GetDirectionFrom(PlanetTile fromTile, int instanceIndex = 0)
        {
            PlanetTile targetTile = GetTileAtInstance(instanceIndex);
            if (!fromTile.Valid || !targetTile.Valid || Find.WorldGrid == null)
                return "";

            Vector3 fromPos = Find.WorldGrid.GetTileCenter(fromTile);
            Vector3 toPos = Find.WorldGrid.GetTileCenter(targetTile);
            Vector3 direction = (toPos - fromPos).normalized;

            if (WorldNavigationState.IsInPoleTerritory)
            {
                return GetRelativeDirection(fromPos, direction);
            }

            return GetCompassDirection(fromPos, direction);
        }

        private string GetCompassDirection(Vector3 fromPos, Vector3 direction)
        {
            Vector3 up = fromPos.normalized;
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up).normalized;
            Vector3 east = Vector3.Cross(up, north).normalized;
            Vector3 flatDir = Vector3.ProjectOnPlane(direction, up).normalized;

            float dotNorth = Vector3.Dot(flatDir, north);
            float dotEast = Vector3.Dot(flatDir, east);
            double angle = Math.Atan2(dotEast, dotNorth) * (180.0 / Math.PI);
            if (angle < 0) angle += 360;

            if (angle >= 337.5 || angle < 22.5) return "North";
            if (angle >= 22.5 && angle < 67.5) return "Northeast";
            if (angle >= 67.5 && angle < 112.5) return "East";
            if (angle >= 112.5 && angle < 157.5) return "Southeast";
            if (angle >= 157.5 && angle < 202.5) return "South";
            if (angle >= 202.5 && angle < 247.5) return "Southwest";
            if (angle >= 247.5 && angle < 292.5) return "West";
            return "Northwest";
        }

        private string GetRelativeDirection(Vector3 fromPos, Vector3 direction)
        {
            Vector3 up = fromPos.normalized;
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up).normalized;
            Vector3 east = Vector3.Cross(up, north).normalized;
            Vector3 flatDir = Vector3.ProjectOnPlane(direction, up).normalized;

            float dotNorth = Vector3.Dot(flatDir, north);
            float dotEast = Vector3.Dot(flatDir, east);
            double angle = Math.Atan2(dotEast, dotNorth) * (180.0 / Math.PI);
            if (angle < 0) angle += 360;

            if (angle >= 337.5 || angle < 22.5) return "Ahead";
            if (angle >= 22.5 && angle < 67.5) return "Ahead-right";
            if (angle >= 67.5 && angle < 112.5) return "Right";
            if (angle >= 112.5 && angle < 157.5) return "Behind-right";
            if (angle >= 157.5 && angle < 202.5) return "Behind";
            if (angle >= 202.5 && angle < 247.5) return "Behind-left";
            if (angle >= 247.5 && angle < 292.5) return "Left";
            return "Ahead-left";
        }
    }

    /// <summary>
    /// Represents a subcategory in the world scanner.
    /// </summary>
    public class WorldScannerSubcategory
    {
        public string Name { get; set; }
        public List<WorldScannerItem> Items { get; set; }

        public WorldScannerSubcategory(string name)
        {
            Name = name;
            Items = new List<WorldScannerItem>();
        }

        public bool IsEmpty => Items == null || Items.Count == 0;
    }

    /// <summary>
    /// Represents a category in the world scanner.
    /// </summary>
    public class WorldScannerCategory
    {
        public string Name { get; set; }
        public List<WorldScannerSubcategory> Subcategories { get; set; }

        public WorldScannerCategory(string name)
        {
            Name = name;
            Subcategories = new List<WorldScannerSubcategory>();
        }

        public bool IsEmpty => Subcategories == null || Subcategories.All(sc => sc.IsEmpty);
        public int TotalItemCount => Subcategories?.Sum(sc => sc.Items?.Count ?? 0) ?? 0;
    }

    /// <summary>
    /// Scanner for world map objects with 4-level navigation.
    /// - Ctrl+PageUp/Down: Categories (Settlements, Quest Sites, Caravans, Biomes, Roads)
    /// - Shift+PageUp/Down: Subcategories (e.g., Player/Allied/Neutral/Hostile settlements)
    /// - PageUp/Down: Item types within subcategory
    /// - Alt+PageUp/Down: Instances of same type (biome regions, road segments)
    /// </summary>
    public static class WorldScannerState
    {
        private static List<WorldScannerCategory> categories = new List<WorldScannerCategory>();
        private static int currentCategoryIndex = 0;
        private static int currentSubcategoryIndex = 0;
        private static int currentItemIndex = 0;
        private static int currentInstanceIndex = 0;
        private static bool autoJumpMode = false;

        // Cache for expensive biome/road calculations
        private static Dictionary<string, List<BiomeRegion>> cachedBiomeRegions = null;
        private static Dictionary<string, List<RoadSegment>> cachedRoadSegments = null;
        private static PlanetTile lastCacheOrigin = PlanetTile.Invalid;

        /// <summary>
        /// Toggles auto-jump mode on/off.
        /// </summary>
        public static void ToggleAutoJumpMode()
        {
            autoJumpMode = !autoJumpMode;
            string status = autoJumpMode ? "enabled" : "disabled";
            TolkHelper.Speak($"Auto-jump mode {status}", SpeechPriority.High);
        }

        /// <summary>
        /// Refreshes the world scanner categories and items.
        /// </summary>
        private static void RefreshItems()
        {
            if (!WorldNavigationState.IsActive || !WorldNavigationState.IsInitialized)
            {
                TolkHelper.Speak("World navigation not active", SpeechPriority.High);
                return;
            }

            PlanetTile originTile = WorldNavigationState.CurrentSelectedTile;
            categories.Clear();

            // Category 0: Route Waypoints (only when route planner is active)
            if (Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active && Find.WorldRoutePlanner.waypoints.Count > 0)
            {
                var waypointsCategory = CreateWaypointsCategory(originTile);
                if (!waypointsCategory.IsEmpty) categories.Add(waypointsCategory);
            }

            // Category 1: Settlements (with faction subcategories)
            var settlementsCategory = CreateSettlementsCategory(originTile);
            if (!settlementsCategory.IsEmpty) categories.Add(settlementsCategory);

            // Category 2: Quest Sites
            var questSitesCategory = CreateQuestSitesCategory(originTile);
            if (!questSitesCategory.IsEmpty) categories.Add(questSitesCategory);

            // Category 3: Caravans
            var caravansCategory = CreateCaravansCategory(originTile);
            if (!caravansCategory.IsEmpty) categories.Add(caravansCategory);

            // Category 4: Other Sites
            var otherSitesCategory = CreateOtherSitesCategory(originTile);
            if (!otherSitesCategory.IsEmpty) categories.Add(otherSitesCategory);

            // Category 5: Biomes (lazy-loaded when accessed)
            var biomesCategory = CreateBiomesCategory(originTile);
            if (!biomesCategory.IsEmpty) categories.Add(biomesCategory);

            // Category 6: Roads
            var roadsCategory = CreateRoadsCategory(originTile);
            if (!roadsCategory.IsEmpty) categories.Add(roadsCategory);

            // Category 7: Space objects (not accessible yet)
            var spaceCategory = CreateSpaceObjectsCategory();
            if (!spaceCategory.IsEmpty) categories.Add(spaceCategory);

            if (categories.Count == 0)
            {
                TolkHelper.Speak("No world objects found", SpeechPriority.High);
                return;
            }

            ValidateIndices();
        }

        #region Category Creators

        private static WorldScannerCategory CreateWaypointsCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Route Waypoints");
            var subcat = new WorldScannerSubcategory("Waypoints");

            WorldRoutePlanner planner = Find.WorldRoutePlanner;
            if (planner == null || !planner.Active)
            {
                category.Subcategories.Add(subcat);
                return category;
            }

            for (int i = 0; i < planner.waypoints.Count; i++)
            {
                RoutePlannerWaypoint waypoint = planner.waypoints[i];
                if (waypoint == null || !waypoint.Tile.Valid)
                    continue;

                var item = new WorldScannerItem(waypoint);

                StringBuilder label = new StringBuilder();
                label.Append($"Waypoint {i + 1}");

                string tileName = WorldInfoHelper.GetTileSummary(waypoint.Tile, includeRouteInfo: false, minimal: true);
                if (!string.IsNullOrEmpty(tileName))
                {
                    label.Append($": {tileName}");
                }

                if (i >= 1)
                {
                    int ticksToWaypoint = planner.GetTicksToWaypoint(i);
                    string timeString = ticksToWaypoint.ToStringTicksToDays("0.#");
                    label.Append($". Estimated travel time: {timeString}");
                }
                else
                {
                    label.Append(" (Start)");
                }

                item.Label = label.ToString();
                subcat.Items.Add(item);
            }

            category.Subcategories.Add(subcat);
            return category;
        }

        private static WorldScannerCategory CreateSettlementsCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Settlements");

            var allSubcat = new WorldScannerSubcategory("All");
            var playerSubcat = new WorldScannerSubcategory("Player");
            var alliedSubcat = new WorldScannerSubcategory("Allied");
            var neutralSubcat = new WorldScannerSubcategory("Neutral");
            var hostileSubcat = new WorldScannerSubcategory("Hostile");

            var settlements = Find.WorldObjects?.Settlements;
            if (settlements != null)
            {
                foreach (var settlement in settlements)
                {
                    if (settlement.Faction == null || !settlement.Tile.Valid)
                        continue;

                    // Skip non-surface objects (space stations, etc.)
                    if (!IsOnSurfaceLayer(settlement))
                        continue;

                    var item = new WorldScannerItem(settlement);

                    // Add to All subcategory
                    allSubcat.Items.Add(item);

                    if (settlement.Faction == Faction.OfPlayer)
                    {
                        playerSubcat.Items.Add(item);
                    }
                    else
                    {
                        var relation = settlement.Faction.RelationKindWith(Faction.OfPlayer);
                        switch (relation)
                        {
                            case FactionRelationKind.Ally:
                                alliedSubcat.Items.Add(item);
                                break;
                            case FactionRelationKind.Neutral:
                                neutralSubcat.Items.Add(item);
                                break;
                            case FactionRelationKind.Hostile:
                                hostileSubcat.Items.Add(item);
                                break;
                        }

                    }
                }
            }

            // Sort each subcategory by distance
            SortItemsByDistance(allSubcat.Items, originTile);
            SortItemsByDistance(playerSubcat.Items, originTile);
            SortItemsByDistance(alliedSubcat.Items, originTile);
            SortItemsByDistance(neutralSubcat.Items, originTile);
            SortItemsByDistance(hostileSubcat.Items, originTile);

            category.Subcategories.Add(allSubcat);
            category.Subcategories.Add(playerSubcat);
            category.Subcategories.Add(alliedSubcat);
            category.Subcategories.Add(neutralSubcat);
            category.Subcategories.Add(hostileSubcat);

            return category;
        }

        private static WorldScannerCategory CreateQuestSitesCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Quest Sites");
            var subcat = new WorldScannerSubcategory("Active Quests");

            if (Find.QuestManager != null)
            {
                var activeQuests = Find.QuestManager.questsInDisplayOrder
                    .Where(q => q.State == QuestState.Ongoing && !q.hidden && !q.hiddenInUI)
                    .ToList();

                foreach (Quest quest in activeQuests)
                {
                    foreach (GlobalTargetInfo target in quest.QuestLookTargets)
                    {
                        if (!target.IsValid || !target.IsWorldTarget)
                            continue;

                        WorldObject worldObj = null;
                        PlanetTile tile = PlanetTile.Invalid;

                        if (target.HasWorldObject && target.WorldObject != null)
                        {
                            worldObj = target.WorldObject;
                            tile = worldObj.Tile;
                        }
                        else if (target.Tile.Valid)
                        {
                            tile = target.Tile;
                            worldObj = Find.WorldObjects?.ObjectsAt(tile)?.FirstOrDefault();
                        }

                        if (!tile.Valid || worldObj == null) continue;

                        // Skip non-surface objects (space quests)
                        if (!IsOnSurfaceLayer(worldObj))
                            continue;

                        // Skip player settlements
                        if (worldObj is Settlement settlement && settlement.Faction == Faction.OfPlayer)
                            continue;

                        var item = new WorldScannerItem(worldObj);
                        item.QuestName = quest.name.StripTags();
                        subcat.Items.Add(item);
                    }
                }
            }

            SortItemsByDistance(subcat.Items, originTile);
            category.Subcategories.Add(subcat);
            return category;
        }

        private static WorldScannerCategory CreateCaravansCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Caravans");
            var subcat = new WorldScannerSubcategory("Player Caravans");

            var caravans = Find.WorldObjects?.Caravans?
                .Where(c => c.Faction == Faction.OfPlayer && IsOnSurfaceLayer(c))
                .ToList();

            if (caravans != null)
            {
                foreach (var caravan in caravans)
                {
                    var item = new WorldScannerItem(caravan);
                    subcat.Items.Add(item);
                }
            }

            SortItemsByDistance(subcat.Items, originTile);
            category.Subcategories.Add(subcat);
            return category;
        }

        private static WorldScannerCategory CreateOtherSitesCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Other Sites");
            var subcat = new WorldScannerSubcategory("Sites");

            var allObjects = Find.WorldObjects?.AllWorldObjects;
            if (allObjects != null)
            {
                var questTiles = new HashSet<int>();
                if (Find.QuestManager != null)
                {
                    foreach (var quest in Find.QuestManager.questsInDisplayOrder.Where(q => q.State == QuestState.Ongoing))
                    {
                        foreach (var target in quest.QuestLookTargets)
                        {
                            if (target.IsValid && target.IsWorldTarget)
                            {
                                if (target.HasWorldObject)
                                    questTiles.Add(target.WorldObject.Tile);
                                else if (target.Tile.Valid)
                                    questTiles.Add(target.Tile);
                            }
                        }
                    }
                }

                foreach (var worldObj in allObjects)
                {
                    if (worldObj is Settlement || worldObj is Caravan)
                        continue;
                    if (questTiles.Contains(worldObj.Tile))
                        continue;
                    if (!worldObj.Tile.Valid)
                        continue;
                    // Skip non-surface objects (space objects)
                    if (!IsOnSurfaceLayer(worldObj))
                        continue;

                    var item = new WorldScannerItem(worldObj);
                    subcat.Items.Add(item);
                }
            }

            SortItemsByDistance(subcat.Items, originTile);
            category.Subcategories.Add(subcat);
            return category;
        }

        private static WorldScannerCategory CreateBiomesCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Biomes");
            var subcat = new WorldScannerSubcategory("All Biomes");

            // Check if we need to rebuild the cache
            if (cachedBiomeRegions == null || !lastCacheOrigin.Valid ||
                Find.WorldGrid.ApproxDistanceInTiles(lastCacheOrigin, originTile) > 50)
            {
                cachedBiomeRegions = CollectBiomeRegions(originTile);
                lastCacheOrigin = originTile;
            }

            // Create items for each biome type, sorted by closest region
            var biomeItems = new List<WorldScannerItem>();
            foreach (var kvp in cachedBiomeRegions)
            {
                string biomeName = kvp.Key;
                var regions = kvp.Value;

                if (regions.Count == 0) continue;

                // Update distances for all regions
                foreach (var region in regions)
                {
                    region.Distance = Find.WorldGrid.ApproxDistanceInTiles(originTile, region.CenterTile);
                }

                // Sort regions by distance
                regions = regions.OrderBy(r => r.Distance).ToList();

                var item = new WorldScannerItem(biomeName, regions);
                biomeItems.Add(item);
            }

            // Sort biome types by their closest region
            biomeItems = biomeItems
                .OrderBy(i => i.GetDistance(originTile, 0))
                .ToList();

            subcat.Items.AddRange(biomeItems);
            category.Subcategories.Add(subcat);
            return category;
        }

        private static WorldScannerCategory CreateRoadsCategory(PlanetTile originTile)
        {
            var category = new WorldScannerCategory("Roads");
            var subcat = new WorldScannerSubcategory("All Roads");

            // Check if we need to rebuild the cache
            if (cachedRoadSegments == null || !lastCacheOrigin.Valid ||
                Find.WorldGrid.ApproxDistanceInTiles(lastCacheOrigin, originTile) > 50)
            {
                cachedRoadSegments = CollectRoadSegments(originTile);
                // lastCacheOrigin already set by biome collection
            }

            var roadItems = new List<WorldScannerItem>();
            foreach (var kvp in cachedRoadSegments)
            {
                string roadName = kvp.Key;
                var segments = kvp.Value;

                if (segments.Count == 0) continue;

                // Update distances
                foreach (var segment in segments)
                {
                    segment.Distance = Find.WorldGrid.ApproxDistanceInTiles(originTile, segment.CenterTile);
                }

                segments = segments.OrderBy(s => s.Distance).ToList();

                var item = new WorldScannerItem(roadName, segments);
                roadItems.Add(item);
            }

            roadItems = roadItems
                .OrderBy(i => i.GetDistance(originTile, 0))
                .ToList();

            subcat.Items.AddRange(roadItems);
            category.Subcategories.Add(subcat);
            return category;
        }

        /// <summary>
        /// Checks if a world object is on the surface layer (not in space/orbit).
        /// </summary>
        private static bool IsOnSurfaceLayer(WorldObject worldObject)
        {
            if (worldObject == null || !worldObject.Tile.Valid)
                return false;

            // Check if the tile's layer is the surface layer
            return worldObject.Tile.Layer == Find.WorldGrid.Surface;
        }

        /// <summary>
        /// Creates a category for space objects that aren't accessible yet.
        /// Only shown when space objects exist (Odyssey DLC).
        /// </summary>
        private static WorldScannerCategory CreateSpaceObjectsCategory()
        {
            var category = new WorldScannerCategory("Not Accessible Yet");
            var subcat = new WorldScannerSubcategory("Space Objects");

            var allObjects = Find.WorldObjects?.AllWorldObjects;
            if (allObjects != null)
            {
                foreach (var worldObj in allObjects)
                {
                    if (!worldObj.Tile.Valid)
                        continue;

                    // Only include objects NOT on the surface layer
                    if (IsOnSurfaceLayer(worldObj))
                        continue;

                    // Create item with just the label - no distance/direction
                    // since those calculations would fail for space objects
                    var item = new WorldScannerItem(worldObj);
                    subcat.Items.Add(item);
                }
            }

            category.Subcategories.Add(subcat);
            return category;
        }

        #endregion

        #region Biome and Road Collection

        private static Dictionary<string, List<BiomeRegion>> CollectBiomeRegions(PlanetTile originTile)
        {
            var result = new Dictionary<string, List<BiomeRegion>>();

            if (Find.WorldGrid == null || Find.World == null)
                return result;

            // Get tiles within a reasonable range (performance optimization)
            int maxRange = 100; // tiles
            var visited = new HashSet<int>();
            var tilesByBiome = new Dictionary<string, HashSet<int>>();

            // BFS from origin to collect nearby tiles by biome
            var queue = new Queue<int>();
            queue.Enqueue(originTile);
            visited.Add(originTile);

            while (queue.Count > 0 && visited.Count < 5000) // Limit total tiles
            {
                int currentTileId = queue.Dequeue();
                PlanetTile currentTile = new PlanetTile(currentTileId);

                if (!currentTile.Valid) continue;

                float dist = Find.WorldGrid.ApproxDistanceInTiles(originTile, currentTile);
                if (dist > maxRange) continue;

                Tile tileData = currentTile.Tile;
                if (tileData?.PrimaryBiome != null)
                {
                    string biomeName = tileData.PrimaryBiome.LabelCap;
                    if (!tilesByBiome.ContainsKey(biomeName))
                        tilesByBiome[biomeName] = new HashSet<int>();
                    tilesByBiome[biomeName].Add(currentTileId);
                }

                // Add neighbors
                var neighbors = new List<PlanetTile>();
                Find.WorldGrid.GetTileNeighbors(currentTile, neighbors);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // For each biome, find contiguous regions using flood fill
            foreach (var kvp in tilesByBiome)
            {
                string biomeName = kvp.Key;
                var biomeTiles = new HashSet<int>(kvp.Value);
                var regions = new List<BiomeRegion>();

                while (biomeTiles.Count > 0)
                {
                    int startTile = biomeTiles.First();
                    var regionTiles = FloodFillRegion(startTile, biomeTiles);

                    if (regionTiles.Count > 0)
                    {
                        // Find center tile (closest to centroid)
                        PlanetTile centerTile = FindRegionCenter(regionTiles);
                        var region = new BiomeRegion(centerTile, regionTiles.Count);
                        regions.Add(region);
                    }

                    // Remove processed tiles
                    foreach (int tile in regionTiles)
                        biomeTiles.Remove(tile);
                }

                if (regions.Count > 0)
                    result[biomeName] = regions;
            }

            return result;
        }

        private static HashSet<int> FloodFillRegion(int startTile, HashSet<int> validTiles)
        {
            var region = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(startTile);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!validTiles.Contains(current) || region.Contains(current))
                    continue;

                region.Add(current);

                var neighbors = new List<PlanetTile>();
                Find.WorldGrid.GetTileNeighbors(new PlanetTile(current), neighbors);
                foreach (var neighbor in neighbors)
                {
                    if (validTiles.Contains(neighbor) && !region.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return region;
        }

        private static PlanetTile FindRegionCenter(HashSet<int> regionTiles)
        {
            if (regionTiles.Count == 0)
                return PlanetTile.Invalid;

            // Calculate centroid
            Vector3 centroid = Vector3.zero;
            foreach (int tileId in regionTiles)
            {
                centroid += Find.WorldGrid.GetTileCenter(new PlanetTile(tileId));
            }
            centroid /= regionTiles.Count;

            // Find tile closest to centroid
            int closestTile = regionTiles.First();
            float closestDist = float.MaxValue;

            foreach (int tileId in regionTiles)
            {
                Vector3 tilePos = Find.WorldGrid.GetTileCenter(new PlanetTile(tileId));
                float dist = Vector3.Distance(tilePos, centroid);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTile = tileId;
                }
            }

            return new PlanetTile(closestTile);
        }

        private static Dictionary<string, List<RoadSegment>> CollectRoadSegments(PlanetTile originTile)
        {
            var result = new Dictionary<string, List<RoadSegment>>();

            if (Find.WorldGrid == null)
                return result;

            int maxRange = 100;
            var visited = new HashSet<int>();
            var roadTilesByType = new Dictionary<string, HashSet<int>>();

            var queue = new Queue<int>();
            queue.Enqueue(originTile);
            visited.Add(originTile);

            while (queue.Count > 0 && visited.Count < 5000)
            {
                int currentTileId = queue.Dequeue();
                PlanetTile currentTile = new PlanetTile(currentTileId);

                if (!currentTile.Valid) continue;

                float dist = Find.WorldGrid.ApproxDistanceInTiles(originTile, currentTile);
                if (dist > maxRange) continue;

                Tile tileData = currentTile.Tile;
                if (tileData is SurfaceTile surfaceTile && surfaceTile.Roads != null)
                {
                    foreach (var roadLink in surfaceTile.Roads)
                    {
                        string roadName = roadLink.road.LabelCap;
                        if (!roadTilesByType.ContainsKey(roadName))
                            roadTilesByType[roadName] = new HashSet<int>();
                        roadTilesByType[roadName].Add(currentTileId);
                    }
                }

                var neighbors = new List<PlanetTile>();
                Find.WorldGrid.GetTileNeighbors(currentTile, neighbors);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // For roads, we treat each connected section as a "segment"
            foreach (var kvp in roadTilesByType)
            {
                string roadName = kvp.Key;
                var roadTiles = new HashSet<int>(kvp.Value);
                var segments = new List<RoadSegment>();

                while (roadTiles.Count > 0)
                {
                    int startTile = roadTiles.First();
                    var segmentTiles = FloodFillRoadSegment(startTile, roadTiles, roadName);

                    if (segmentTiles.Count > 0)
                    {
                        PlanetTile centerTile = FindRegionCenter(segmentTiles);
                        var segment = new RoadSegment(centerTile, segmentTiles.Count);
                        segments.Add(segment);
                    }

                    foreach (int tile in segmentTiles)
                        roadTiles.Remove(tile);
                }

                if (segments.Count > 0)
                    result[roadName] = segments;
            }

            return result;
        }

        private static HashSet<int> FloodFillRoadSegment(int startTile, HashSet<int> validTiles, string roadType)
        {
            var segment = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(startTile);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!validTiles.Contains(current) || segment.Contains(current))
                    continue;

                segment.Add(current);

                var neighbors = new List<PlanetTile>();
                Find.WorldGrid.GetTileNeighbors(new PlanetTile(current), neighbors);
                foreach (var neighbor in neighbors)
                {
                    if (validTiles.Contains(neighbor) && !segment.Contains(neighbor))
                    {
                        // Check if neighbor has the same road type
                        Tile neighborData = neighbor.Tile;
                        if (neighborData is SurfaceTile surfaceTile && surfaceTile.Roads != null)
                        {
                            bool hasRoadType = surfaceTile.Roads.Any(r => r.road.LabelCap == roadType);
                            if (hasRoadType)
                                queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return segment;
        }

        #endregion

        #region Navigation

        private static void SortItemsByDistance(List<WorldScannerItem> items, PlanetTile originTile)
        {
            items.Sort((a, b) => a.GetDistance(originTile, 0).CompareTo(b.GetDistance(originTile, 0)));
        }

        private static void ValidateIndices()
        {
            if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
                currentCategoryIndex = 0;

            var category = GetCurrentCategory();
            if (category != null)
            {
                if (currentSubcategoryIndex < 0 || currentSubcategoryIndex >= category.Subcategories.Count)
                    currentSubcategoryIndex = 0;
                SkipEmptySubcategories(forward: true);
            }

            var subcat = GetCurrentSubcategory();
            if (subcat != null)
            {
                if (currentItemIndex < 0 || currentItemIndex >= subcat.Items.Count)
                    currentItemIndex = 0;
            }

            var item = GetCurrentItem();
            if (item != null)
            {
                if (currentInstanceIndex < 0 || currentInstanceIndex >= item.InstanceCount)
                    currentInstanceIndex = 0;
            }
        }

        private static void SkipEmptySubcategories(bool forward)
        {
            var category = GetCurrentCategory();
            if (category == null) return;

            int attempts = 0;
            int maxAttempts = category.Subcategories.Count;

            while ((GetCurrentSubcategory()?.IsEmpty ?? true) && attempts < maxAttempts)
            {
                if (forward)
                {
                    currentSubcategoryIndex++;
                    if (currentSubcategoryIndex >= category.Subcategories.Count)
                        currentSubcategoryIndex = 0;
                }
                else
                {
                    currentSubcategoryIndex--;
                    if (currentSubcategoryIndex < 0)
                        currentSubcategoryIndex = category.Subcategories.Count - 1;
                }
                attempts++;
            }
        }

        private static WorldScannerCategory GetCurrentCategory()
        {
            if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
                return null;
            return categories[currentCategoryIndex];
        }

        private static WorldScannerSubcategory GetCurrentSubcategory()
        {
            var category = GetCurrentCategory();
            if (category == null) return null;
            if (currentSubcategoryIndex < 0 || currentSubcategoryIndex >= category.Subcategories.Count)
                return null;
            return category.Subcategories[currentSubcategoryIndex];
        }

        private static WorldScannerItem GetCurrentItem()
        {
            var subcat = GetCurrentSubcategory();
            if (subcat == null) return null;
            if (currentItemIndex < 0 || currentItemIndex >= subcat.Items.Count)
                return null;
            return subcat.Items[currentItemIndex];
        }

        /// <summary>
        /// Moves to the next category (Ctrl+PageDown).
        /// </summary>
        public static void NextCategory()
        {
            if (!WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            currentCategoryIndex++;
            if (currentCategoryIndex >= categories.Count)
                currentCategoryIndex = 0;

            currentSubcategoryIndex = 0;
            currentItemIndex = 0;
            currentInstanceIndex = 0;
            SkipEmptySubcategories(forward: true);

            AnnounceCurrentCategory();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the previous category (Ctrl+PageUp).
        /// </summary>
        public static void PreviousCategory()
        {
            if (!WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            currentCategoryIndex--;
            if (currentCategoryIndex < 0)
                currentCategoryIndex = categories.Count - 1;

            currentSubcategoryIndex = 0;
            currentItemIndex = 0;
            currentInstanceIndex = 0;
            SkipEmptySubcategories(forward: true);

            AnnounceCurrentCategory();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the next subcategory (Shift+PageDown).
        /// </summary>
        public static void NextSubcategory()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var category = GetCurrentCategory();
            if (category == null || category.Subcategories.Count <= 1)
            {
                TolkHelper.Speak("No subcategories", SpeechPriority.Normal);
                return;
            }

            int startIndex = currentSubcategoryIndex;
            do
            {
                currentSubcategoryIndex++;
                if (currentSubcategoryIndex >= category.Subcategories.Count)
                    currentSubcategoryIndex = 0;
                if (currentSubcategoryIndex == startIndex) break;
            } while (GetCurrentSubcategory()?.IsEmpty ?? true);

            currentItemIndex = 0;
            currentInstanceIndex = 0;

            AnnounceCurrentSubcategory();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the previous subcategory (Shift+PageUp).
        /// </summary>
        public static void PreviousSubcategory()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var category = GetCurrentCategory();
            if (category == null || category.Subcategories.Count <= 1)
            {
                TolkHelper.Speak("No subcategories", SpeechPriority.Normal);
                return;
            }

            int startIndex = currentSubcategoryIndex;
            do
            {
                currentSubcategoryIndex--;
                if (currentSubcategoryIndex < 0)
                    currentSubcategoryIndex = category.Subcategories.Count - 1;
                if (currentSubcategoryIndex == startIndex) break;
            } while (GetCurrentSubcategory()?.IsEmpty ?? true);

            currentItemIndex = 0;
            currentInstanceIndex = 0;

            AnnounceCurrentSubcategory();
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the next item type (PageDown).
        /// </summary>
        public static void NextItem()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var subcat = GetCurrentSubcategory();
            if (subcat == null || subcat.Items.Count == 0)
            {
                TolkHelper.Speak("No items in this category", SpeechPriority.Normal);
                return;
            }

            currentItemIndex++;
            if (currentItemIndex >= subcat.Items.Count)
                currentItemIndex = 0;

            currentInstanceIndex = 0;

            if (autoJumpMode)
                JumpToCurrent();
            else
                AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the previous item type (PageUp).
        /// </summary>
        public static void PreviousItem()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var subcat = GetCurrentSubcategory();
            if (subcat == null || subcat.Items.Count == 0)
            {
                TolkHelper.Speak("No items in this category", SpeechPriority.Normal);
                return;
            }

            currentItemIndex--;
            if (currentItemIndex < 0)
                currentItemIndex = subcat.Items.Count - 1;

            currentInstanceIndex = 0;

            if (autoJumpMode)
                JumpToCurrent();
            else
                AnnounceCurrentItem();
        }

        /// <summary>
        /// Moves to the next instance of the current item type (Alt+PageDown).
        /// </summary>
        public static void NextInstance()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var item = GetCurrentItem();
            if (item == null || !item.HasInstances)
            {
                TolkHelper.Speak("No instances to navigate", SpeechPriority.Normal);
                return;
            }

            currentInstanceIndex++;
            if (currentInstanceIndex >= item.InstanceCount)
                currentInstanceIndex = 0;

            if (autoJumpMode)
                JumpToCurrent();
            else
                AnnounceCurrentInstance();
        }

        /// <summary>
        /// Moves to the previous instance of the current item type (Alt+PageUp).
        /// </summary>
        public static void PreviousInstance()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var item = GetCurrentItem();
            if (item == null || !item.HasInstances)
            {
                TolkHelper.Speak("No instances to navigate", SpeechPriority.Normal);
                return;
            }

            currentInstanceIndex--;
            if (currentInstanceIndex < 0)
                currentInstanceIndex = item.InstanceCount - 1;

            if (autoJumpMode)
                JumpToCurrent();
            else
                AnnounceCurrentInstance();
        }

        /// <summary>
        /// Jumps the camera to the current item/instance (Home key).
        /// </summary>
        public static void JumpToCurrent()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var item = GetCurrentItem();
            if (item == null)
            {
                TolkHelper.Speak("No item selected", SpeechPriority.High);
                return;
            }

            PlanetTile targetTile = item.GetTileAtInstance(currentInstanceIndex);

            WorldNavigationState.CurrentSelectedTile = targetTile;

            if (Find.WorldSelector != null)
            {
                Find.WorldSelector.ClearSelection();
                if (item.WorldObject != null)
                    Find.WorldSelector.Select(item.WorldObject);
                Find.WorldSelector.SelectedTile = targetTile;
            }

            if (Find.WorldCameraDriver != null)
            {
                Find.WorldCameraDriver.JumpTo(targetTile);
                Find.WorldCameraDriver.RotateSoNorthIsUp();
            }

            if (item.HasInstances)
                AnnounceCurrentInstance();
            else
                AnnounceCurrentItem();
        }

        /// <summary>
        /// Reads distance and direction to current item (End key).
        /// </summary>
        public static void ReadDistanceAndDirection()
        {
            if (!WorldNavigationState.IsActive) return;

            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
            }

            var item = GetCurrentItem();
            if (item == null)
            {
                TolkHelper.Speak("No item selected", SpeechPriority.High);
                return;
            }

            PlanetTile originTile = WorldNavigationState.CurrentSelectedTile;
            float distance = item.GetDistance(originTile, currentInstanceIndex);
            string direction = item.GetDirectionFrom(originTile, currentInstanceIndex);

            TolkHelper.Speak($"{direction}, {distance:F0} tiles", SpeechPriority.Normal);
        }

        #endregion

        #region Announcements

        private static void AnnounceCurrentCategory()
        {
            var category = GetCurrentCategory();
            if (category == null) return;

            int catPos = currentCategoryIndex + 1;
            int catTotal = categories.Count;

            TolkHelper.Speak($"{category.Name}, {category.TotalItemCount} items. Category {catPos} of {catTotal}", SpeechPriority.Normal);
        }

        private static void AnnounceCurrentSubcategory()
        {
            var subcat = GetCurrentSubcategory();
            var category = GetCurrentCategory();
            if (subcat == null || category == null) return;

            int subPos = currentSubcategoryIndex + 1;
            int subTotal = category.Subcategories.Count;

            TolkHelper.Speak($"{subcat.Name}, {subcat.Items.Count} items. Subcategory {subPos} of {subTotal}", SpeechPriority.Normal);
        }

        private static void AnnounceCurrentItem()
        {
            var item = GetCurrentItem();
            if (item == null)
            {
                TolkHelper.Speak("No items in this category", SpeechPriority.Normal);
                return;
            }

            var subcat = GetCurrentSubcategory();
            var category = GetCurrentCategory();

            // Check if this is a space object (not on surface layer)
            bool isSpaceObject = item.WorldObject != null && !IsOnSurfaceLayer(item.WorldObject);

            var parts = new List<string>();

            // Skip distance/direction calculations for space objects
            PlanetTile originTile = WorldNavigationState.CurrentSelectedTile;
            float distance = 0f;
            string direction = "";

            if (!isSpaceObject)
            {
                distance = item.GetDistance(originTile, 0);
                direction = item.GetDirectionFrom(originTile, 0);

                // Check reachability from last waypoint if route planner has waypoints
                PlanetTile targetTile = item.GetTileAtInstance(0);
                if (targetTile.Valid && Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active &&
                    Find.WorldRoutePlanner.waypoints.Count > 0 && Find.WorldReachability != null)
                {
                    // Get the last waypoint's tile as the origin for reachability check
                    var lastWaypoint = Find.WorldRoutePlanner.waypoints[Find.WorldRoutePlanner.waypoints.Count - 1];
                    if (lastWaypoint != null && lastWaypoint.Tile.Valid)
                    {
                        if (!Find.WorldReachability.CanReach(lastWaypoint.Tile, targetTile))
                        {
                            parts.Add("Unreachable");
                        }
                    }
                }
            }

            parts.Add(item.Label);

            if (!string.IsNullOrEmpty(item.QuestName))
                parts.Add($"Quest: {item.QuestName}");

            // For settlements, add faction type and goodwill (matching WorldInfoHelper format)
            if (item.WorldObject is Settlement settlement && item.Faction != null && item.Faction != Faction.OfPlayer)
            {
                // Faction name and type (e.g., "The Tribe, Tribal")
                string factionType = item.Faction.def?.LabelCap ?? "";
                if (!string.IsNullOrEmpty(factionType) && factionType != item.Faction.Name)
                    parts.Add($"{item.Faction.Name}, {factionType}");
                else
                    parts.Add(item.Faction.Name);

                // Relationship with goodwill (e.g., "Neutral +15")
                string relationship = item.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" :
                                     item.Faction.PlayerRelationKind.GetLabelCap();
                int goodwill = item.Faction.PlayerGoodwill;
                string goodwillStr = goodwill >= 0 ? $"+{goodwill}" : goodwill.ToString();
                parts.Add($"{relationship} {goodwillStr}");

            }

            // For biomes/roads, show instance count
            if (item.HasInstances)
            {
                if (item.BiomeRegions != null)
                    parts.Add($"{item.BiomeRegions[0].SizeDescription}");
                else if (item.RoadSegments != null)
                    parts.Add($"{item.RoadSegments[0].TileCount} tiles");

                parts.Add($"{item.InstanceCount} regions");
            }

            if (isSpaceObject)
            {
                parts.Add("In space");
            }
            else if (!string.IsNullOrEmpty(direction) && distance > 0.1f)
            {
                parts.Add($"{direction}, {distance:F0} tiles");
            }
            else if (distance <= 0.1f)
            {
                parts.Add("Current location");
            }

            // Add fuel cost if transport pod launch targeting is active
            if (!isSpaceObject && TransportPodLaunchState.ShouldAnnounceFuelCosts() && distance > 0.1f)
            {
                string fuelInfo = TransportPodLaunchState.GetFuelCostAnnouncement(distance);
                if (!string.IsNullOrEmpty(fuelInfo))
                    parts.Add(fuelInfo);
            }

            int pos = currentItemIndex + 1;
            int total = subcat?.Items.Count ?? 0;
            parts.Add($"{pos} of {total}");

            TolkHelper.Speak(string.Join(". ", parts), SpeechPriority.Normal);
        }

        private static void AnnounceCurrentInstance()
        {
            var item = GetCurrentItem();
            if (item == null || !item.HasInstances) return;

            PlanetTile originTile = WorldNavigationState.CurrentSelectedTile;
            float distance = item.GetDistance(originTile, currentInstanceIndex);
            string direction = item.GetDirectionFrom(originTile, currentInstanceIndex);

            var parts = new List<string>();

            // Check reachability from last waypoint if route planner has waypoints
            PlanetTile targetTile = item.GetTileAtInstance(currentInstanceIndex);
            if (targetTile.Valid && Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active &&
                Find.WorldRoutePlanner.waypoints.Count > 0 && Find.WorldReachability != null)
            {
                var lastWaypoint = Find.WorldRoutePlanner.waypoints[Find.WorldRoutePlanner.waypoints.Count - 1];
                if (lastWaypoint != null && lastWaypoint.Tile.Valid)
                {
                    if (!Find.WorldReachability.CanReach(lastWaypoint.Tile, targetTile))
                    {
                        parts.Add("Unreachable");
                    }
                }
            }

            parts.Add(item.Label);

            if (item.BiomeRegions != null && currentInstanceIndex < item.BiomeRegions.Count)
            {
                var region = item.BiomeRegions[currentInstanceIndex];
                parts.Add(region.SizeDescription);
            }
            else if (item.RoadSegments != null && currentInstanceIndex < item.RoadSegments.Count)
            {
                var segment = item.RoadSegments[currentInstanceIndex];
                parts.Add($"{segment.TileCount} tiles");
            }

            if (!string.IsNullOrEmpty(direction) && distance > 0.1f)
                parts.Add($"{direction}, {distance:F0} tiles");
            else if (distance <= 0.1f)
                parts.Add("Current location");

            // Add fuel cost if transport pod launch targeting is active
            if (TransportPodLaunchState.ShouldAnnounceFuelCosts() && distance > 0.1f)
            {
                string fuelInfo = TransportPodLaunchState.GetFuelCostAnnouncement(distance);
                if (!string.IsNullOrEmpty(fuelInfo))
                    parts.Add(fuelInfo);
            }

            int pos = currentInstanceIndex + 1;
            int total = item.InstanceCount;
            parts.Add($"Region {pos} of {total}");

            TolkHelper.Speak(string.Join(". ", parts), SpeechPriority.Normal);
        }

        #endregion

        /// <summary>
        /// Resets the scanner state.
        /// </summary>
        public static void Reset()
        {
            categories.Clear();
            currentCategoryIndex = 0;
            currentSubcategoryIndex = 0;
            currentItemIndex = 0;
            currentInstanceIndex = 0;
            cachedBiomeRegions = null;
            cachedRoadSegments = null;
            lastCacheOrigin = PlanetTile.Invalid;
        }
    }
}
