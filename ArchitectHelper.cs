using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Helper class for working with RimWorld's architect system.
    /// Provides methods to retrieve categories, designators, and materials.
    /// </summary>
    public static class ArchitectHelper
    {
        /// <summary>
        /// Gets all visible designation categories for the current game state.
        /// </summary>
        public static List<DesignationCategoryDef> GetAllCategories()
        {
            List<DesignationCategoryDef> categories = new List<DesignationCategoryDef>();

            foreach (DesignationCategoryDef categoryDef in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                // Check if category is visible (research unlocked, etc.)
                if (categoryDef.Visible)
                {
                    categories.Add(categoryDef);
                }
            }

            // Sort by order
            categories.SortBy(c => c.order);

            return categories;
        }

        /// <summary>
        /// Gets all allowed designators for a specific category.
        /// </summary>
        public static List<Designator> GetDesignatorsForCategory(DesignationCategoryDef category)
        {
            if (category == null)
                return new List<Designator>();

            List<Designator> designators = new List<Designator>();

            try
            {
                // First check if we have AllResolvedDesignators (this includes ideology and all resolved designators)
                List<Designator> allDesignators = category.AllResolvedDesignators;

                if (allDesignators == null || allDesignators.Count == 0)
                {
                    MelonLoader.MelonLogger.Warning($"No resolved designators found for category: {category.defName}");
                    return designators;
                }

                MelonLoader.MelonLogger.Msg($"Found {allDesignators.Count} designators in category: {category.defName}");

                // Get allowed designators (filters by game rules)
                foreach (Designator designator in category.ResolvedAllowedDesignators)
                {
                    // Skip dropdown designators - we'll handle their contents instead
                    if (designator is Designator_Dropdown dropdown)
                    {
                        // Add all elements from the dropdown
                        if (dropdown.Elements != null)
                        {
                            foreach (Designator element in dropdown.Elements)
                            {
                                designators.Add(element);
                            }
                        }
                    }
                    else
                    {
                        designators.Add(designator);
                    }
                }

                MelonLoader.MelonLogger.Msg($"After filtering: {designators.Count} designators available");
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error getting designators for category {category.defName}: {ex}");
            }

            return designators;
        }

        /// <summary>
        /// Gets all valid stuff (materials) for a buildable that requires stuff.
        /// </summary>
        public static List<ThingDef> GetMaterialsForBuildable(BuildableDef buildable)
        {
            List<ThingDef> materials = new List<ThingDef>();

            if (buildable is ThingDef thingDef && thingDef.MadeFromStuff)
            {
                // Get all stuff that can be used to make this thing
                foreach (ThingDef stuffDef in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (stuffDef.IsStuff && stuffDef.stuffProps.CanMake(thingDef))
                    {
                        materials.Add(stuffDef);
                    }
                }

                // Sort by commonality - most common materials first
                materials.SortBy(m => -m.BaseMarketValue);
            }

            return materials;
        }

        /// <summary>
        /// Creates a Designator_Build for a specific buildable and material.
        /// </summary>
        public static Designator_Build CreateBuildDesignator(BuildableDef buildable, ThingDef stuffDef)
        {
            Designator_Build designator = new Designator_Build(buildable);

            // Set the stuff if provided
            if (stuffDef != null && buildable is ThingDef thingDef && thingDef.MadeFromStuff)
            {
                designator.SetStuffDef(stuffDef);
            }

            return designator;
        }

        /// <summary>
        /// Checks if a designator supports multi-cell designation (e.g., mining, plant cutting).
        /// </summary>
        public static bool SupportsMultiCellDesignation(Designator designator)
        {
            // Most cell-based designators support multiple cells
            if (designator is Designator_Cells)
                return true;

            // Build designators can be placed on multiple cells if not a single-tile building
            if (designator is Designator_Build buildDesignator)
            {
                // Buildings are typically placed one at a time
                // But we can allow multiple placements in sequence
                return false;
            }

            return false;
        }

        /// <summary>
        /// Gets a user-friendly description of what a designator does.
        /// </summary>
        public static string GetDesignatorDescription(Designator designator)
        {
            if (!string.IsNullOrEmpty(designator.Desc))
                return designator.Desc;

            // Provide default descriptions for common designator types
            if (designator is Designator_Mine)
                return "Mine rock and minerals";
            else if (designator is Designator_Build)
                return "Construct buildings and structures";
            else if (designator is Designator_PlantsHarvestWood)
                return "Chop down trees for wood";
            else if (designator is Designator_PlantsCut)
                return "Cut plants";
            else if (designator is Designator_Hunt)
                return "Hunt animals for meat";
            else if (designator is Designator_Tame)
                return "Tame wild animals";

            return designator.Label;
        }

        /// <summary>
        /// Gets the default or most commonly available material for a buildable.
        /// </summary>
        public static ThingDef GetDefaultMaterial(BuildableDef buildable)
        {
            if (buildable is ThingDef thingDef && thingDef.MadeFromStuff)
            {
                // Try to get the default stuff
                ThingDef defaultStuff = GenStuff.DefaultStuffFor(thingDef);
                if (defaultStuff != null)
                    return defaultStuff;

                // Fall back to the first available material
                List<ThingDef> materials = GetMaterialsForBuildable(buildable);
                if (materials.Count > 0)
                    return materials[0];
            }

            return null;
        }

        /// <summary>
        /// Checks if a buildable requires material selection.
        /// </summary>
        public static bool RequiresMaterialSelection(BuildableDef buildable)
        {
            if (buildable is ThingDef thingDef)
            {
                return thingDef.MadeFromStuff;
            }
            return false;
        }

        /// <summary>
        /// Formats a list of materials as FloatMenuOptions.
        /// </summary>
        public static List<FloatMenuOption> CreateMaterialOptions(BuildableDef buildable, Action<ThingDef> onSelected)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            List<ThingDef> materials = GetMaterialsForBuildable(buildable);

            foreach (ThingDef material in materials)
            {
                // Check if we have this material available
                int availableCount = 0;
                if (Find.CurrentMap != null)
                {
                    availableCount = Find.CurrentMap.resourceCounter.GetCount(material);
                }

                string label = material.LabelCap;
                if (availableCount > 0)
                {
                    label += $" ({availableCount} available)";
                }
                else
                {
                    label += " (none available)";
                }

                options.Add(new FloatMenuOption(label, () => onSelected(material)));
            }

            return options;
        }

        /// <summary>
        /// Formats a list of designators as FloatMenuOptions.
        /// </summary>
        public static List<FloatMenuOption> CreateDesignatorOptions(List<Designator> designators, Action<Designator> onSelected)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (Designator designator in designators)
            {
                string label = designator.LabelCap;

                // Add action
                options.Add(new FloatMenuOption(label, () => onSelected(designator)));
            }

            return options;
        }

        /// <summary>
        /// Formats categories as FloatMenuOptions.
        /// </summary>
        public static List<FloatMenuOption> CreateCategoryOptions(List<DesignationCategoryDef> categories, Action<DesignationCategoryDef> onSelected)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (DesignationCategoryDef category in categories)
            {
                string label = category.LabelCap;
                options.Add(new FloatMenuOption(label, () => onSelected(category)));
            }

            return options;
        }
    }
}
