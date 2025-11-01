using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for extracting inspection information for various object types.
    /// Provides category lists and detailed information for pawns, animals, buildings, items, and plants.
    /// </summary>
    public static class InspectionInfoHelper
    {
        /// <summary>
        /// Gets a one-line summary description of an object.
        /// </summary>
        public static string GetObjectSummary(object obj)
        {
            if (obj == null) return "Unknown object";

            if (obj is Pawn pawn)
            {
                string pawnType = pawn.RaceProps.Humanlike ? "Pawn" : "Animal";
                string status = "";

                if (pawn.Dead)
                    status = " (Dead)";
                else if (pawn.Downed)
                    status = " (Downed)";
                else if (pawn.Drafted)
                    status = " (Drafted)";

                return $"{pawnType}: {pawn.LabelCap.StripTags()}{status}";
            }

            if (obj is Building building)
            {
                return $"Building: {building.LabelCap.StripTags()}";
            }

            if (obj is Plant plant)
            {
                return $"Plant: {plant.LabelCap.StripTags()}";
            }

            if (obj is Thing thing)
            {
                return $"Item: {thing.LabelCap.StripTags()}";
            }

            if (obj is Zone zone)
            {
                return $"Zone: {zone.label}";
            }

            return obj.ToString();
        }

        /// <summary>
        /// Gets the list of available information categories for an object.
        /// </summary>
        public static List<string> GetAvailableCategories(object obj)
        {
            var categories = new List<string>();

            if (obj is Pawn pawn)
            {
                categories.Add("Overview");
                categories.Add("Health");

                if (pawn.RaceProps.Humanlike)
                {
                    categories.Add("Needs");
                    categories.Add("Mood");
                    categories.Add("Gear");
                    categories.Add("Skills");
                    categories.Add("Social");
                    categories.Add("Character");
                    categories.Add("Work Priorities");
                }
                else // Animal
                {
                    categories.Add("Needs");
                    if (pawn.training != null)
                        categories.Add("Training");
                }
            }
            else if (obj is Building building)
            {
                categories.Add("Overview");

                // Check for bills (workbench)
                if (building is IBillGiver)
                    categories.Add("Bills");

                // Check for storage
                if (building is IStoreSettingsParent || building is Building_Storage)
                    categories.Add("Storage");

                // Check for power
                var powerComp = building.TryGetComp<CompPowerTrader>();
                if (powerComp != null)
                    categories.Add("Power");
            }
            else if (obj is Plant plant)
            {
                categories.Add("Overview");
                categories.Add("Growth Info");
            }
            else if (obj is Thing)
            {
                categories.Add("Overview");
                categories.Add("Quality & Stats");
            }

            return categories;
        }

        /// <summary>
        /// Gets detailed information for a specific category of an object.
        /// </summary>
        public static string GetCategoryInfo(object obj, string category)
        {
            if (obj == null) return "No information available.";

            try
            {
                if (obj is Pawn pawn)
                {
                    return GetPawnCategoryInfo(pawn, category);
                }
                else if (obj is Building building)
                {
                    return GetBuildingCategoryInfo(building, category);
                }
                else if (obj is Plant plant)
                {
                    return GetPlantCategoryInfo(plant, category);
                }
                else if (obj is Thing thing)
                {
                    return GetThingCategoryInfo(thing, category);
                }
            }
            catch (Exception ex)
            {
                return $"Error retrieving {category} information: {ex.Message}";
            }

            return "No information available for this category.";
        }

        /// <summary>
        /// Gets category information for a pawn (colonist or animal).
        /// </summary>
        private static string GetPawnCategoryInfo(Pawn pawn, string category)
        {
            switch (category)
            {
                case "Overview":
                    return GetPawnOverview(pawn);

                case "Health":
                    return PawnInfoHelper.GetHealthInfo(pawn);

                case "Needs":
                    return PawnInfoHelper.GetNeedsInfo(pawn);

                case "Mood":
                    if (pawn.needs?.mood != null)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Mood: {pawn.needs.mood.CurLevelPercentage:P0}");
                        sb.AppendLine();

                        // Get mood thoughts
                        List<Thought> thoughts = new List<Thought>();
                        pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughts);

                        if (thoughts.Any())
                        {
                            sb.AppendLine("Recent Thoughts:");
                            foreach (var thought in thoughts.Take(10))
                            {
                                sb.AppendLine($"  {thought.LabelCap.StripTags()}: {thought.MoodOffset():+0.#;-0.#}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("No significant thoughts.");
                        }

                        return sb.ToString();
                    }
                    return "No mood information available.";

                case "Gear":
                    return PawnInfoHelper.GetGearInfo(pawn);

                case "Skills":
                    return PawnInfoHelper.GetCharacterInfo(pawn); // Includes skills

                case "Social":
                    return PawnInfoHelper.GetSocialInfo(pawn);

                case "Character":
                    return GetPawnCharacterInfo(pawn);

                case "Training":
                    return PawnInfoHelper.GetTrainingInfo(pawn);

                case "Work Priorities":
                    return PawnInfoHelper.GetWorkInfo(pawn);

                default:
                    return "Category not found.";
            }
        }

        /// <summary>
        /// Gets overview information for a pawn.
        /// </summary>
        private static string GetPawnOverview(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine(pawn.LabelCap.StripTags());
            sb.AppendLine();

            // Get the inspect string (current activity, status)
            string inspectString = pawn.GetInspectString();
            if (!string.IsNullOrEmpty(inspectString))
            {
                sb.AppendLine(inspectString);
                sb.AppendLine();
            }

            // Basic info
            if (pawn.RaceProps.Humanlike)
            {
                if (pawn.story != null)
                {
                    sb.AppendLine($"Age: {pawn.ageTracker.AgeBiologicalYears}");
                    if (pawn.gender != Gender.None)
                        sb.AppendLine($"Gender: {pawn.gender}");
                }
            }
            else
            {
                sb.AppendLine($"Animal: {pawn.def.label}");
                sb.AppendLine($"Age: {pawn.ageTracker.AgeBiologicalYears}");
            }

            // Health summary
            if (pawn.Dead)
                sb.AppendLine("Status: Dead");
            else if (pawn.Downed)
                sb.AppendLine("Status: Downed");
            else if (pawn.health?.summaryHealth?.SummaryHealthPercent != null)
                sb.AppendLine($"Health: {pawn.health.summaryHealth.SummaryHealthPercent:P0}");

            return sb.ToString();
        }

        /// <summary>
        /// Gets character information (traits and backstory) for a pawn.
        /// </summary>
        private static string GetPawnCharacterInfo(Pawn pawn)
        {
            var sb = new StringBuilder();

            if (pawn.story != null)
            {
                // Traits
                if (pawn.story.traits?.allTraits != null && pawn.story.traits.allTraits.Any())
                {
                    sb.AppendLine("Traits:");
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        sb.AppendLine($"  {trait.LabelCap.StripTags()}");
                    }
                    sb.AppendLine();
                }

                // Backstory
                if (pawn.story.Childhood != null)
                {
                    sb.AppendLine($"Childhood: {pawn.story.Childhood.TitleCapFor(pawn.gender)}");
                }
                if (pawn.story.Adulthood != null)
                {
                    sb.AppendLine($"Adulthood: {pawn.story.Adulthood.TitleCapFor(pawn.gender)}");
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Gets category information for a building.
        /// </summary>
        private static string GetBuildingCategoryInfo(Building building, string category)
        {
            switch (category)
            {
                case "Overview":
                    return GetBuildingOverview(building);

                case "Bills":
                    return GetBuildingBillsInfo(building);

                case "Storage":
                    return GetBuildingStorageInfo(building);

                case "Power":
                    return GetBuildingPowerInfo(building);

                default:
                    return "Category not found.";
            }
        }

        /// <summary>
        /// Gets overview information for a building.
        /// </summary>
        private static string GetBuildingOverview(Building building)
        {
            var sb = new StringBuilder();
            sb.AppendLine(building.LabelCap.StripTags());
            sb.AppendLine();

            // Get the inspect string
            string inspectString = building.GetInspectString();
            if (!string.IsNullOrEmpty(inspectString))
            {
                sb.AppendLine(inspectString);
                sb.AppendLine();
            }

            // Health
            if (building.HitPoints < building.MaxHitPoints)
            {
                float healthPercent = (float)building.HitPoints / building.MaxHitPoints;
                sb.AppendLine($"Health: {healthPercent:P0} ({building.HitPoints} / {building.MaxHitPoints})");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets bills information for a workbench.
        /// </summary>
        private static string GetBuildingBillsInfo(Building building)
        {
            if (building is IBillGiver billGiver && billGiver.BillStack != null)
            {
                var sb = new StringBuilder();

                if (billGiver.BillStack.Count == 0)
                {
                    return "No bills queued.";
                }

                sb.AppendLine($"Bills ({billGiver.BillStack.Count}):");
                sb.AppendLine();

                int index = 1;
                foreach (var bill in billGiver.BillStack.Bills)
                {
                    sb.AppendLine($"{index}. {bill.LabelCap.StripTags()}");

                    if (bill is Bill_Production productionBill)
                    {
                        if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount)
                            sb.AppendLine($"   Target: {productionBill.repeatCount}");
                        else if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount)
                            sb.AppendLine($"   Target: {productionBill.targetCount}");
                        else
                            sb.AppendLine($"   Mode: {productionBill.repeatMode.label}");
                    }

                    if (bill.suspended)
                        sb.AppendLine("   (Suspended)");

                    sb.AppendLine();
                    index++;
                }

                return sb.ToString();
            }

            return "This building does not have bills.";
        }

        /// <summary>
        /// Gets storage settings information for a storage building.
        /// </summary>
        private static string GetBuildingStorageInfo(Building building)
        {
            if (building is IStoreSettingsParent storeParent && storeParent.GetStoreSettings() != null)
            {
                var settings = storeParent.GetStoreSettings();
                var sb = new StringBuilder();

                sb.AppendLine($"Priority: {settings.Priority}");
                sb.AppendLine();

                // Get filter summary
                if (settings.filter != null)
                {
                    string summary = settings.filter.Summary;
                    if (!string.IsNullOrEmpty(summary))
                    {
                        sb.AppendLine("Allowed items:");
                        sb.AppendLine(summary);
                    }
                    else
                    {
                        sb.AppendLine("No items allowed.");
                    }
                }

                return sb.ToString();
            }

            return "This building does not have storage settings.";
        }

        /// <summary>
        /// Gets power information for a powered building.
        /// </summary>
        private static string GetBuildingPowerInfo(Building building)
        {
            var powerComp = building.TryGetComp<CompPowerTrader>();
            if (powerComp != null)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Power: {(powerComp.PowerOn ? "On" : "Off")}");
                sb.AppendLine($"Consumption: {powerComp.PowerOutput} W");

                if (!powerComp.PowerOn)
                {
                    sb.AppendLine();
                    sb.AppendLine("Power is currently off or unavailable.");
                }

                return sb.ToString();
            }

            return "This building does not use power.";
        }

        /// <summary>
        /// Gets category information for a plant.
        /// </summary>
        private static string GetPlantCategoryInfo(Plant plant, string category)
        {
            switch (category)
            {
                case "Overview":
                    return GetPlantOverview(plant);

                case "Growth Info":
                    return GetPlantGrowthInfo(plant);

                default:
                    return "Category not found.";
            }
        }

        /// <summary>
        /// Gets overview information for a plant.
        /// </summary>
        private static string GetPlantOverview(Plant plant)
        {
            var sb = new StringBuilder();
            sb.AppendLine(plant.LabelCap.StripTags());
            sb.AppendLine();

            // Get the inspect string
            string inspectString = plant.GetInspectString();
            if (!string.IsNullOrEmpty(inspectString))
            {
                sb.AppendLine(inspectString);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets detailed growth information for a plant.
        /// </summary>
        private static string GetPlantGrowthInfo(Plant plant)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Growth: {plant.Growth:P0}");
            sb.AppendLine($"Lifespan: {plant.Age} / {plant.def.plant.LifespanTicks.TicksToDays():F1} days");

            if (plant.Blighted)
                sb.AppendLine("Status: Blighted");
            else if (plant.Dying)
                sb.AppendLine("Status: Dying");

            if (plant.HarvestableNow)
                sb.AppendLine("Ready to harvest!");

            return sb.ToString();
        }

        /// <summary>
        /// Gets category information for a generic thing (item).
        /// </summary>
        private static string GetThingCategoryInfo(Thing thing, string category)
        {
            switch (category)
            {
                case "Overview":
                    return GetThingOverview(thing);

                case "Quality & Stats":
                    return GetThingQualityInfo(thing);

                default:
                    return "Category not found.";
            }
        }

        /// <summary>
        /// Gets overview information for a thing.
        /// </summary>
        private static string GetThingOverview(Thing thing)
        {
            var sb = new StringBuilder();
            sb.AppendLine(thing.LabelCap.StripTags());
            sb.AppendLine();

            // Stack count
            if (thing.stackCount > 1)
                sb.AppendLine($"Stack: {thing.stackCount}");

            // Get the inspect string
            string inspectString = thing.GetInspectString();
            if (!string.IsNullOrEmpty(inspectString))
            {
                sb.AppendLine(inspectString);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets quality and stats information for a thing.
        /// </summary>
        private static string GetThingQualityInfo(Thing thing)
        {
            var sb = new StringBuilder();

            // Quality
            var qualityComp = thing.TryGetComp<CompQuality>();
            if (qualityComp != null)
            {
                sb.AppendLine($"Quality: {qualityComp.Quality}");
                sb.AppendLine();
            }

            // Hit points
            if (thing.def.useHitPoints && thing.HitPoints < thing.MaxHitPoints)
            {
                float healthPercent = (float)thing.HitPoints / thing.MaxHitPoints;
                sb.AppendLine($"Health: {healthPercent:P0} ({thing.HitPoints} / {thing.MaxHitPoints})");
            }

            // Stuff (material)
            if (thing.Stuff != null)
            {
                sb.AppendLine($"Material: {thing.Stuff.LabelCap.ToString().StripTags()}");
            }

            // Market value
            sb.AppendLine($"Market Value: {thing.MarketValue:F0} silver");

            // Mass
            sb.AppendLine($"Mass: {thing.GetStatValue(StatDefOf.Mass):F2} kg");

            return sb.ToString();
        }
    }
}
