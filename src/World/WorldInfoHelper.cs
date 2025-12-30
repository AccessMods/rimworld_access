using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for extracting information about world tiles and settlements.
    /// Used by WorldNavigationState and SettlementBrowserState.
    /// </summary>
    public static class WorldInfoHelper
    {
        /// <summary>
        /// Gets a brief summary of a world tile (for navigation announcements).
        /// </summary>
        public static string GetTileSummary(PlanetTile planetTile)
        {
            if (!planetTile.Valid || Find.WorldGrid == null)
                return "Invalid tile";

            Tile tile = planetTile.Tile;
            if (tile == null)
                return "Unknown tile";

            StringBuilder summary = new StringBuilder();

            // Add biome
            if (tile.PrimaryBiome != null)
            {
                summary.Append(tile.PrimaryBiome.LabelCap);
            }

            // Add hilliness
            if (tile.hilliness != Hilliness.Impassable && tile.hilliness != Hilliness.Undefined)
            {
                summary.Append($", {tile.hilliness.GetLabelCap()}");
            }

            // Add temperature (average)
            float temp = tile.temperature;
            summary.Append($", {temp:F0}°C");

            // Check for world objects at this tile
            if (Find.WorldObjects != null)
            {
                List<WorldObject> objectsAtTile = Find.WorldObjects.ObjectsAt(planetTile).ToList();

                if (objectsAtTile.Count > 0)
                {
                    // Prioritize settlements
                    Settlement settlement = objectsAtTile.OfType<Settlement>().FirstOrDefault();
                    if (settlement != null)
                    {
                        summary.Append($", {settlement.Label}");

                        // Add faction info
                        if (settlement.Faction != null)
                        {
                            if (settlement.Faction == Faction.OfPlayer)
                            {
                                summary.Append(" (Player)");
                            }
                            else
                            {
                                string relationship = settlement.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" :
                                                     settlement.Faction.PlayerRelationKind.GetLabel();
                                summary.Append($" ({settlement.Faction.Name}, {relationship})");
                            }
                        }
                    }

                    // Check for caravans on this tile (regardless of settlement presence)
                    Caravan caravan = objectsAtTile.OfType<Caravan>().FirstOrDefault();
                    if (caravan != null)
                    {
                        summary.Append($", {caravan.Label}");

                        // Add faction info for caravan
                        if (caravan.Faction != null && caravan.Faction != Faction.OfPlayer)
                        {
                            summary.Append($" ({caravan.Faction.Name})");
                        }
                    }

                    // If no settlement or caravan, list other world objects
                    if (settlement == null && caravan == null)
                    {
                        WorldObject firstObject = objectsAtTile.FirstOrDefault();
                        if (firstObject != null)
                        {
                            summary.Append($", {firstObject.Label}");
                        }
                    }
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// Gets detailed information about a world tile (for I key).
        /// </summary>
        public static string GetDetailedTileInfo(PlanetTile planetTile)
        {
            if (!planetTile.Valid || Find.WorldGrid == null)
                return "Invalid tile";

            Tile tile = planetTile.Tile;
            if (tile == null)
                return "Unknown tile";

            StringBuilder info = new StringBuilder();

            // Coordinates
            Vector2 longlat = Find.WorldGrid.LongLatOf(planetTile);
            info.AppendLine($"Coordinates: {longlat.y:F2}° N, {longlat.x:F2}° E");

            // Biome
            if (tile.PrimaryBiome != null)
            {
                info.AppendLine($"Biome: {tile.PrimaryBiome.LabelCap}");
            }

            // Hilliness
            if (tile.hilliness != Hilliness.Undefined)
            {
                info.AppendLine($"Hilliness: {tile.hilliness.GetLabelCap()}");
            }

            // Elevation
            info.AppendLine($"Elevation: {tile.elevation:F0}m");

            // Temperature
            info.AppendLine($"Temperature: Average {tile.temperature:F0}°C");

            // Pollution (if Biotech active)
            if (ModsConfig.BiotechActive && tile.pollution > 0)
            {
                info.AppendLine($"Pollution: {tile.pollution:F0}%");
            }

            // World objects at this tile
            if (Find.WorldObjects != null)
            {
                List<WorldObject> objectsAtTile = Find.WorldObjects.ObjectsAt(planetTile).ToList();

                if (objectsAtTile.Count > 0)
                {
                    info.AppendLine("\nWorld Objects:");

                    foreach (WorldObject obj in objectsAtTile)
                    {
                        info.Append($"  - {obj.Label}");

                        // Add type-specific information
                        if (obj is Settlement settlement)
                        {
                            if (settlement.Faction != null)
                            {
                                string relationship = settlement.Faction == Faction.OfPlayer ? "Player" :
                                                     settlement.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" :
                                                     settlement.Faction.PlayerRelationKind.GetLabel();
                                info.Append($" ({settlement.Faction.Name}, {relationship})");
                            }
                        }
                        else if (obj is Caravan caravan)
                        {
                            if (caravan.Faction != null)
                            {
                                info.Append($" (Caravan, {caravan.Faction.Name})");
                            }
                        }
                        else if (obj is Site site)
                        {
                            info.Append(" (Site)");
                        }

                        info.AppendLine();
                    }
                }
            }

            return info.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets information about a specific settlement.
        /// </summary>
        public static string GetSettlementInfo(Settlement settlement)
        {
            if (settlement == null)
                return "No settlement";

            StringBuilder info = new StringBuilder();

            info.AppendLine($"Settlement: {settlement.Label}");

            if (settlement.Faction != null)
            {
                info.AppendLine($"Faction: {settlement.Faction.Name}");

                if (settlement.Faction == Faction.OfPlayer)
                {
                    info.AppendLine("Relationship: Player Colony");
                }
                else
                {
                    string relationship = settlement.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" :
                                         settlement.Faction.PlayerRelationKind.GetLabel();
                    info.AppendLine($"Relationship: {relationship}");

                    // Add goodwill
                    int goodwill = settlement.Faction.PlayerGoodwill;
                    info.AppendLine($"Goodwill: {goodwill}");
                }
            }

            // Add visitable/attackable status
            if (settlement.Visitable)
            {
                info.AppendLine("Status: Visitable");
            }

            if (settlement.Attackable)
            {
                info.AppendLine("Status: Attackable");
            }

            return info.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets a list of all settlements sorted by distance from a given tile.
        /// </summary>
        public static List<Settlement> GetSettlementsByDistance(PlanetTile fromTile)
        {
            if (!fromTile.Valid || Find.WorldObjects?.Settlements == null || Find.WorldGrid == null)
                return new List<Settlement>();

            return Find.WorldObjects.Settlements
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(fromTile, s.Tile))
                .ToList();
        }

        /// <summary>
        /// Gets the player's home settlement (first player settlement found).
        /// </summary>
        public static Settlement GetPlayerHomeSettlement()
        {
            if (Find.WorldObjects?.Settlements == null)
                return null;

            return Find.WorldObjects.Settlements
                .FirstOrDefault(s => s.Faction == Faction.OfPlayer);
        }

        /// <summary>
        /// Gets all player caravans.
        /// </summary>
        public static List<Caravan> GetPlayerCaravans()
        {
            if (Find.WorldObjects?.Caravans == null)
                return new List<Caravan>();

            return Find.WorldObjects.Caravans
                .Where(c => c.Faction == Faction.OfPlayer)
                .ToList();
        }
    }
}
