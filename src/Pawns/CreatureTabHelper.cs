using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for extracting creature-specific information for accessibility.
    /// Provides information about diet, temperature comfort, production stats, and other
    /// creature characteristics that appear in RimWorld's info card for animals and humans.
    /// </summary>
    public static class CreatureTabHelper
    {
        /// <summary>
        /// Gets all creature information items as a list of label-value pairs.
        /// Used to build the tree view for the creature tab.
        /// </summary>
        public static List<(string Label, string Value, string Description)> GetCreatureInfoItems(Pawn pawn)
        {
            var items = new List<(string Label, string Value, string Description)>();

            if (pawn?.def?.race == null)
                return items;

            var race = pawn.def.race;
            bool isAnimal = !race.Humanlike;

            // Diet information
            string dietInfo = GetDietInfo(pawn);
            if (!string.IsNullOrEmpty(dietInfo))
            {
                items.Add(("Diet", dietInfo, "What this creature can eat"));
            }

            // Comfortable temperature range
            string tempRange = GetComfortableTemperatureRange(pawn);
            if (!string.IsNullOrEmpty(tempRange))
            {
                items.Add(("Comfortable Temperature", tempRange, "Temperature range where this creature is comfortable"));
            }

            // Life expectancy
            if (race.lifeExpectancy > 0)
            {
                items.Add(("Life Expectancy", $"{race.lifeExpectancy:F0} years", "Expected natural lifespan"));
            }

            // Body size
            float bodySize = pawn.BodySize;
            items.Add(("Body Size", $"{bodySize:F2}", "Physical body size affecting carrying capacity and food consumption"));

            // Hunger rate / food consumption
            string hungerInfo = GetHungerInfo(pawn);
            if (!string.IsNullOrEmpty(hungerInfo))
            {
                items.Add(("Food Consumption", hungerInfo, "Nutrition consumed per day"));
            }

            // Animal-specific information
            if (isAnimal)
            {
                // Wildness
                float wildness = pawn.def.GetStatValueAbstract(StatDefOf.Wildness);
                if (wildness > 0)
                {
                    items.Add(("Wildness", $"{wildness:P0}", "How difficult this animal is to tame"));
                }

                // Trainability
                if (race.trainability != null)
                {
                    items.Add(("Trainability", race.trainability.LabelCap.ToString(), "What level of training this animal can achieve"));
                }

                // Available training
                string availableTraining = GetAvailableTraining(pawn);
                if (!string.IsNullOrEmpty(availableTraining))
                {
                    items.Add(("Available Training", availableTraining, "Training options available for this animal"));
                }

                // Harmed revenge chance
                float revengeChance = PawnUtility.GetManhunterOnDamageChance(pawn);
                if (revengeChance > 0)
                {
                    items.Add(("Harmed Revenge Chance", $"{revengeChance:P0}", "Chance to go manhunter when harmed"));
                }

                // Tame failed revenge chance
                float tameFailChance = PawnUtility.GetManhunterOnTameFailChance(pawn);
                if (tameFailChance > 0)
                {
                    items.Add(("Tame Failed Revenge Chance", $"{tameFailChance:P0}", "Chance to go manhunter when taming fails"));
                }

                // Pack animal
                items.Add(("Pack Animal", race.packAnimal ? "Yes" : "No", "Can carry items in caravans"));

                // Blocked by fences
                bool blockedByFences = pawn.FenceBlocked;
                items.Add(("Blocked By Fences", blockedByFences ? "Yes" : "No", "Whether this animal is contained by fences"));

                // Nuzzle interval
                if (race.nuzzleMtbHours > 0)
                {
                    float nuzzleMtb = NuzzleUtility.GetNuzzleMTBHours(pawn);
                    int nuzzleTicks = UnityEngine.Mathf.RoundToInt(nuzzleMtb * 2500f);
                    items.Add(("Nuzzle Interval", nuzzleTicks.ToStringTicksToPeriod(), "How often this animal nuzzles its master"));
                }

                // Roam interval
                if (race.roamMtbDays.HasValue && pawn.Roamer)
                {
                    float roamDays = pawn.RoamMtbDays ?? race.roamMtbDays.Value;
                    int roamTicks = UnityEngine.Mathf.RoundToInt(roamDays * 60000f);
                    items.Add(("Roam Interval", roamTicks.ToStringTicksToPeriod(), "How often this animal attempts to roam"));
                }

                // Add production stats
                AddProductionStats(items, pawn.def);
            }

            // Leather type (for both animals and humans with Biotech)
            if (race.leatherDef != null)
            {
                items.Add(("Leather Type", race.leatherDef.LabelCap.ToString(), "Type of leather produced when butchered"));
            }

            // Meat amount
            float meatAmount = pawn.def.GetStatValueAbstract(StatDefOf.MeatAmount);
            if (meatAmount > 0)
            {
                items.Add(("Meat Amount", $"{meatAmount:F0}", "Amount of meat produced when butchered"));
            }

            return items;
        }

        /// <summary>
        /// Gets diet information for the creature.
        /// </summary>
        public static string GetDietInfo(Pawn pawn)
        {
            if (pawn?.def?.race == null)
                return null;

            var race = pawn.def.race;

            // Check for mutant override
            if (pawn.IsMutant && pawn.mutant?.Def?.overrideFoodType == true)
            {
                return pawn.mutant.Def.foodType.ToHumanString().CapitalizeFirst();
            }

            // Skip for mechanoids and anomaly entities
            if (race.IsMechanoid || race.IsAnomalyEntity)
                return null;

            return race.foodType.ToHumanString().CapitalizeFirst();
        }

        /// <summary>
        /// Gets the comfortable temperature range for the creature.
        /// </summary>
        public static string GetComfortableTemperatureRange(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return null;

            float minTemp = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float maxTemp = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);

            return $"{minTemp.ToStringTemperature("F0")} ~ {maxTemp.ToStringTemperature("F0")}";
        }

        /// <summary>
        /// Gets hunger/food consumption information.
        /// </summary>
        public static string GetHungerInfo(Pawn pawn)
        {
            if (pawn?.needs?.food == null)
                return null;

            float nutritionPerDay = pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * 60000f;
            return $"{nutritionPerDay:0.##} nutrition/day";
        }

        /// <summary>
        /// Gets available training options for an animal.
        /// </summary>
        public static string GetAvailableTraining(Pawn pawn)
        {
            if (pawn?.def?.race == null || pawn.def.race.Humanlike)
                return null;

            var trainables = DefDatabase<TrainableDef>.AllDefsListForReading
                .Where(td =>
                {
                    bool visible;
                    return Pawn_TrainingTracker.CanAssignToTrain(td, pawn.def, out visible, pawn);
                })
                .OrderByDescending(td => td.listPriority)
                .Select(td => td.label)
                .ToList();

            if (trainables.Count == 0)
                return null;

            return string.Join(", ", trainables).CapitalizeFirst();
        }

        /// <summary>
        /// Adds animal production stats (eggs, milk, wool, breeding).
        /// </summary>
        private static void AddProductionStats(List<(string Label, string Value, string Description)> items, ThingDef def)
        {
            if (def == null)
                return;

            var race = def.race;

            // Gestation time
            float gestationDays = AnimalProductionUtility.GestationDaysLitter(def);
            if (gestationDays > 0)
            {
                items.Add(("Gestation Time", $"{gestationDays:F1} days", "Time to produce offspring"));
            }

            // Litter size
            IntRange litterSize = AnimalProductionUtility.OffspringRange(def);
            if (litterSize != IntRange.One)
            {
                items.Add(("Litter Size", litterSize.ToString(), "Number of offspring per birth"));
            }

            // Growth time (days to adulthood)
            float growthDays = AnimalProductionUtility.DaysToAdulthood(def);
            if (growthDays > 0)
            {
                items.Add(("Growth Time", $"{growthDays:F0} days", "Time from birth to adulthood"));
            }

            // Grass to maintain (for herbivores)
            float grassToMaintain = AnimalProductionUtility.GrassToMaintain(def);
            if (grassToMaintain > 0)
            {
                items.Add(("Grass To Maintain", $"{grassToMaintain:F1} tiles", "Number of grass tiles needed to sustain this animal"));
            }

            // Egg production
            var eggComp = def.GetCompProperties<CompProperties_EggLayer>();
            if (eggComp != null)
            {
                ThingDef eggDef = eggComp.eggUnfertilizedDef ?? eggComp.eggFertilizedDef;
                if (eggDef != null)
                {
                    items.Add(("Egg Type", eggDef.LabelCap.ToString(), "Type of egg produced"));
                }

                float eggsPerYear = AnimalProductionUtility.EggsPerYear(def);
                items.Add(("Eggs Per Year", $"{eggsPerYear:F1}", "Average number of eggs laid per year"));

                float eggNutrition = AnimalProductionUtility.EggNutrition(def);
                if (eggNutrition > 0)
                {
                    items.Add(("Egg Nutrition", $"{eggNutrition:F2}", "Nutrition value per egg"));
                }

                float eggValue = AnimalProductionUtility.EggMarketValue(def);
                if (eggValue > 0)
                {
                    items.Add(("Egg Market Value", $"{eggValue:F1} silver", "Market value per egg"));
                }
            }

            // Milk production
            var milkComp = def.GetCompProperties<CompProperties_Milkable>();
            if (milkComp != null)
            {
                items.Add(("Milk Type", milkComp.milkDef.LabelCap.ToString(), "Type of milk produced"));
                items.Add(("Milk Amount", $"{milkComp.milkAmount}", "Amount of milk per milking"));
                items.Add(("Milk Interval", $"{milkComp.milkIntervalDays} days", "Days between milkings"));

                float milkPerYear = AnimalProductionUtility.MilkPerYear(def);
                items.Add(("Milk Per Year", $"{milkPerYear:F0}", "Total milk production per year"));

                float milkValue = AnimalProductionUtility.MilkMarketValue(def);
                if (milkValue > 0)
                {
                    items.Add(("Milk Value Per Year", $"{AnimalProductionUtility.MilkMarketValuePerYear(def):F0} silver", "Total milk market value per year"));
                }
            }

            // Wool production
            var woolComp = def.GetCompProperties<CompProperties_Shearable>();
            if (woolComp != null)
            {
                items.Add(("Wool Type", woolComp.woolDef.LabelCap.ToString(), "Type of wool produced"));
                items.Add(("Wool Amount", $"{woolComp.woolAmount}", "Amount of wool per shearing"));
                items.Add(("Wool Interval", $"{woolComp.shearIntervalDays} days", "Days between shearings"));

                float woolPerYear = AnimalProductionUtility.WoolPerYear(def);
                items.Add(("Wool Per Year", $"{woolPerYear:F0}", "Total wool production per year"));

                float woolValue = AnimalProductionUtility.WoolMarketValue(def);
                if (woolValue > 0)
                {
                    items.Add(("Wool Value Per Year", $"{AnimalProductionUtility.WoolMarketValuePerYear(def):F0} silver", "Total wool market value per year"));
                }
            }
        }

        /// <summary>
        /// Gets a summary string of all creature information.
        /// </summary>
        public static string GetCreatureSummary(Pawn pawn)
        {
            var sb = new StringBuilder();
            var items = GetCreatureInfoItems(pawn);

            foreach (var item in items)
            {
                sb.AppendLine($"{item.Label}: {item.Value}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
