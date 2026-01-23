using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State class for the Scenario Builder (Page_ScenarioEditor).
    /// Manages navigation between metadata fields and scenario parts tree.
    /// </summary>
    public static class ScenarioBuilderState
    {
        public static bool IsActive { get; private set; }

        // Sections of the builder
        public enum Section { Metadata, Parts }
        private static Section currentSection = Section.Metadata;

        // Metadata field indices
        private static int metadataIndex = 0;
        private const int MetadataFieldCount = 3; // Title, Summary, Description

        // Text editing state
        private static bool isEditingText = false;
        private static string editingFieldName = "";
        private static string originalValue = "";

        // Parts tree state - proper treeview pattern
        private static List<PartTreeItem> partsHierarchy = new List<PartTreeItem>(); // Root items (parts)
        private static List<TreeViewItem> flattenedParts = new List<TreeViewItem>(); // Flattened visible items
        private static int partsIndex = 0;

        // Typeahead for parts
        private static TypeaheadSearchHelper partsTypeaheadHelper = new TypeaheadSearchHelper();

        /// <summary>
        /// Represents an item in the flattened treeview (can be a part or a field).
        /// </summary>
        public class TreeViewItem
        {
            public string Label { get; set; }
            public int IndentLevel { get; set; }
            public bool IsExpandable { get; set; }
            public bool IsExpanded { get; set; }
            public PartTreeItem ParentPart { get; set; } // The part this item belongs to (null for parts)
            public PartField Field { get; set; } // The field (null for parts)
            public PartTreeItem AsPart { get; set; } // If this is a part, the PartTreeItem

            public bool IsPart => AsPart != null;
            public bool IsField => Field != null;
        }

        // Reference to the current scenario and editor page
        private static Scenario currentScenario;
        private static Page_ScenarioEditor currentPage;

        // For tracking changes
        private static int originalHash;

        // Level tracking for tree navigation
        private const string LevelTrackingKey = "ScenarioBuilderParts";

        /// <summary>
        /// Gets whether we're in text editing mode.
        /// </summary>
        public static bool IsEditingText => isEditingText;

        /// <summary>
        /// Gets the current scenario being edited.
        /// </summary>
        public static Scenario CurrentScenario => currentScenario;

        /// <summary>
        /// Gets the current section.
        /// </summary>
        public static Section CurrentSection => currentSection;

        /// <summary>
        /// Represents a part in the parts tree with its editable fields.
        /// </summary>
        public class PartTreeItem
        {
            public ScenPart Part { get; set; }
            public string Label { get; set; }
            public string Summary { get; set; }
            public List<PartField> Fields { get; set; } = new List<PartField>();
            public bool IsExpanded { get; set; }
            public int IndentLevel { get; set; }
        }

        /// <summary>
        /// Represents an editable field within a part.
        /// </summary>
        public class PartField
        {
            public string Name { get; set; }
            public FieldType Type { get; set; }
            public string CurrentValue { get; set; }
            public object Data { get; set; } // For dropdown: list of options
            public Action<object> SetValue { get; set; } // Callback to set the value
        }

        public enum FieldType { Dropdown, Quantity, Text }

        /// <summary>
        /// Opens the scenario builder state with the given scenario.
        /// </summary>
        public static void Open(Scenario scenario, Page_ScenarioEditor page)
        {
            currentScenario = scenario;
            currentPage = page;
            originalHash = scenario?.GetHashCode() ?? 0;

            currentSection = Section.Metadata;
            metadataIndex = 0;
            isEditingText = false;
            partsIndex = 0;
            partsTypeaheadHelper.ClearSearch();
            MenuHelper.ResetLevel(LevelTrackingKey);

            BuildPartsTree();
            FlattenPartsTree();

            IsActive = true;

            // Announce the builder opening
            string title = currentScenario?.name ?? "New Scenario";
            TolkHelper.Speak($"Scenario Builder: {title}. Metadata section. Press Tab to switch to Parts. Alt+L to load, Alt+S to save, Alt+R to randomize, Alt+A to add part.");
        }

        /// <summary>
        /// Closes the scenario builder state.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            isEditingText = false;
            currentScenario = null;
            currentPage = null;
            partsHierarchy.Clear();
            flattenedParts.Clear();
            partsTypeaheadHelper.ClearSearch();
            MenuHelper.ResetLevel(LevelTrackingKey);
        }

        /// <summary>
        /// Builds the parts tree from the current scenario.
        /// Iterates through ALL parts (matching the game's editor behavior).
        /// Preserves expanded state of parts when rebuilding.
        /// </summary>
        public static void BuildPartsTree()
        {
            // Remember which parts were expanded (by ScenPart reference)
            var expandedParts = new HashSet<ScenPart>();
            foreach (var existing in partsHierarchy)
            {
                if (existing.IsExpanded)
                    expandedParts.Add(existing.Part);
            }

            partsHierarchy.Clear();

            if (currentScenario == null)
                return;

            // Match game's ScenarioUI.DrawScenarioEditInterface - iterate ALL parts
            foreach (ScenPart part in currentScenario.AllParts)
            {
                var item = new PartTreeItem
                {
                    Part = part,
                    Label = part.Label,
                    Summary = GetPartSummary(part),
                    IsExpanded = expandedParts.Contains(part), // Preserve expanded state
                    IndentLevel = 0
                };

                // Extract editable fields from the part
                item.Fields = ExtractPartFields(part);

                partsHierarchy.Add(item);
            }
        }

        /// <summary>
        /// Flattens the parts tree for navigation.
        /// Includes parts and their fields (when expanded).
        /// Single-field parts are shown directly without needing to expand.
        /// </summary>
        private static void FlattenPartsTree()
        {
            flattenedParts.Clear();

            foreach (var part in partsHierarchy)
            {
                // Build label with summary for better context (e.g., "Start With - Silver x50")
                string partLabel = part.Label;
                if (!string.IsNullOrEmpty(part.Summary))
                {
                    partLabel += $" - {part.Summary}";
                }

                // SPECIAL CASE: Parts with exactly 1 field are shown directly as that field
                // This avoids requiring expand/collapse for simple items like "Player Faction"
                if (part.Fields.Count == 1)
                {
                    var field = part.Fields[0];
                    flattenedParts.Add(new TreeViewItem
                    {
                        Label = $"{partLabel}: {field.CurrentValue}",
                        IndentLevel = 0,
                        IsExpandable = false,
                        IsExpanded = false,
                        ParentPart = part, // Keep reference for context
                        Field = field,     // This IS the field - pressing Enter edits it
                        AsPart = part      // Also keep part reference for delete operations
                    });
                }
                // Parts with 0 fields (headers/non-editable) or 2+ fields (needs expand)
                else
                {
                    flattenedParts.Add(new TreeViewItem
                    {
                        Label = partLabel,
                        IndentLevel = 0,
                        IsExpandable = part.Fields.Count > 1, // Only expandable if 2+ fields
                        IsExpanded = part.IsExpanded,
                        ParentPart = null,
                        Field = null,
                        AsPart = part
                    });

                    // If expanded, add its fields as children
                    if (part.IsExpanded && part.Fields.Count > 1)
                    {
                        foreach (var field in part.Fields)
                        {
                            flattenedParts.Add(new TreeViewItem
                            {
                                Label = $"{field.Name}: {field.CurrentValue}",
                                IndentLevel = 1,
                                IsExpandable = false,
                                IsExpanded = false,
                                ParentPart = part,
                                Field = field,
                                AsPart = null
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a summary of the part's current values.
        /// </summary>
        private static string GetPartSummary(ScenPart part)
        {
            try
            {
                string summary = part.Summary(currentScenario);
                if (!string.IsNullOrEmpty(summary))
                {
                    // Clean up the summary - take first line only
                    int newlineIndex = summary.IndexOf('\n');
                    if (newlineIndex > 0)
                        summary = summary.Substring(0, newlineIndex);
                    return summary.Trim();
                }
            }
            catch
            {
                // Ignore errors in summary generation
            }
            return "";
        }

        /// <summary>
        /// Extracts editable fields from a ScenPart using reflection.
        /// </summary>
        private static List<PartField> ExtractPartFields(ScenPart part)
        {
            var fields = new List<PartField>();
            Type partType = part.GetType();

            // Check for common field patterns in ScenParts
            // Each ScenPart subclass has its own set of editable fields

            // Handle ScenPart_PlayerFaction (always present)
            var factionDefField = partType.GetField("factionDef", BindingFlags.NonPublic | BindingFlags.Instance);
            if (factionDefField != null && factionDefField.FieldType == typeof(FactionDef))
            {
                var currentFaction = (FactionDef)factionDefField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Faction",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentFaction?.LabelCap ?? "None",
                    Data = GetPlayerFactionOptions(),
                    SetValue = (val) => factionDefField.SetValue(part, val)
                });
            }

            // Handle ScenPart_PlanetLayer (surface layer)
            // Note: ScenPart_PlanetLayerFixed has CanEdit = false, check for that property
            var layerField = partType.GetField("layer", BindingFlags.Public | BindingFlags.Instance);
            if (layerField != null && layerField.FieldType == typeof(PlanetLayerDef))
            {
                // Check if this part type has CanEdit property set to false
                var canEditProp = partType.GetProperty("CanEdit", BindingFlags.NonPublic | BindingFlags.Instance);
                bool canEdit = canEditProp == null || (bool)canEditProp.GetValue(part);

                if (canEdit)
                {
                    var currentLayer = (PlanetLayerDef)layerField.GetValue(part);
                    fields.Add(new PartField
                    {
                        Name = "Planet Layer",
                        Type = FieldType.Dropdown,
                        CurrentValue = currentLayer?.LabelCap ?? "None",
                        Data = GetPlanetLayerOptions(),
                        SetValue = (val) => layerField.SetValue(part, val)
                    });
                }
                // If CanEdit is false, don't add field - part will show as read-only
            }

            // Handle ScenPart_PlayerPawnsArriveMethod - arrival method enum
            var arriveMethodField = partType.GetField("method", BindingFlags.NonPublic | BindingFlags.Instance);
            if (arriveMethodField != null && arriveMethodField.FieldType == typeof(PlayerPawnsArriveMethod))
            {
                var currentMethod = (PlayerPawnsArriveMethod)arriveMethodField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Arrival Method",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentMethod.ToStringHuman(),
                    Data = GetArriveMethodOptions(),
                    SetValue = (val) => arriveMethodField.SetValue(part, val)
                });
            }

            // Handle ScenPart_ThingCount and derived classes (Start With items)
            // Check for thingDef field (the item being given)
            var thingDefField = partType.GetField("thingDef", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (thingDefField != null && thingDefField.FieldType == typeof(ThingDef))
            {
                var currentThingDef = (ThingDef)thingDefField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Item",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentThingDef?.LabelCap ?? "None",
                    Data = GetStartingThingOptions(part),
                    SetValue = (val) =>
                    {
                        thingDefField.SetValue(part, val);
                        // Also reset stuff to default when item changes
                        var stuffField = partType.GetField("stuff", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        if (stuffField != null && val is ThingDef td)
                        {
                            stuffField.SetValue(part, GenStuff.DefaultStuffFor(td));
                        }
                    }
                });

                // If the thing is made from stuff, add a stuff selector
                if (currentThingDef != null && currentThingDef.MadeFromStuff)
                {
                    var stuffField = partType.GetField("stuff", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    if (stuffField != null)
                    {
                        var currentStuff = (ThingDef)stuffField.GetValue(part);
                        fields.Add(new PartField
                        {
                            Name = "Material",
                            Type = FieldType.Dropdown,
                            CurrentValue = currentStuff?.LabelCap ?? "Default",
                            Data = GetStuffOptions(currentThingDef),
                            SetValue = (val) => stuffField.SetValue(part, val)
                        });
                    }
                }

                // If the thing can have quality, add a quality selector
                if (currentThingDef != null && currentThingDef.HasComp(typeof(CompQuality)))
                {
                    var qualityField = partType.GetField("quality", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    if (qualityField != null)
                    {
                        var currentQuality = qualityField.GetValue(part);
                        string qualityLabel = "Default";
                        if (currentQuality != null && currentQuality is QualityCategory q)
                        {
                            qualityLabel = q.GetLabel().CapitalizeFirst();
                        }
                        fields.Add(new PartField
                        {
                            Name = "Quality",
                            Type = FieldType.Dropdown,
                            CurrentValue = qualityLabel,
                            Data = GetQualityOptions(),
                            SetValue = (val) => qualityField.SetValue(part, val)
                        });
                    }
                }
            }

            // Try to find count/amount fields (common pattern)
            var countField = partType.GetField("count", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (countField != null && countField.FieldType == typeof(int))
            {
                int currentValue = (int)countField.GetValue(part);
                // Game uses Widgets.TextFieldNumeric with min=1, max=1E+09 (1 billion)
                // We use int.MaxValue since it's an int field
                fields.Add(new PartField
                {
                    Name = "Count",
                    Type = FieldType.Quantity,
                    CurrentValue = currentValue.ToString(),
                    Data = new int[] { 1, int.MaxValue },
                    SetValue = (val) => countField.SetValue(part, val)
                });
            }

            // Try to find chance fields
            var chanceField = partType.GetField("chance", BindingFlags.NonPublic | BindingFlags.Instance);
            if (chanceField != null && chanceField.FieldType == typeof(float))
            {
                float currentValue = (float)chanceField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Chance",
                    Type = FieldType.Quantity,
                    CurrentValue = $"{currentValue * 100:F0}%",
                    Data = new float[] { 0f, 1f },
                    SetValue = (val) => chanceField.SetValue(part, val)
                });
            }

            // Try to find PawnKindDef field (for animals)
            var animalKindField = partType.GetField("animalKind", BindingFlags.NonPublic | BindingFlags.Instance);
            if (animalKindField != null && animalKindField.FieldType == typeof(PawnKindDef))
            {
                var currentKind = (PawnKindDef)animalKindField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Animal Type",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentKind?.LabelCap ?? "Random Pet",
                    Data = GetAnimalOptions(),
                    SetValue = (val) => animalKindField.SetValue(part, val)
                });
            }

            // Try to find TraitDef field
            var traitField = partType.GetField("trait", BindingFlags.NonPublic | BindingFlags.Instance);
            if (traitField != null && traitField.FieldType == typeof(TraitDef))
            {
                var currentTrait = (TraitDef)traitField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Trait",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentTrait?.label?.CapitalizeFirst() ?? "None",
                    Data = GetTraitOptions(),
                    SetValue = (val) => traitField.SetValue(part, val)
                });
            }

            // Try to find IncidentDef field
            var incidentField = partType.GetField("incident", BindingFlags.NonPublic | BindingFlags.Instance);
            if (incidentField != null && incidentField.FieldType == typeof(IncidentDef))
            {
                var currentIncident = (IncidentDef)incidentField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Incident",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentIncident?.LabelCap ?? "None",
                    Data = GetIncidentOptions(),
                    SetValue = (val) => incidentField.SetValue(part, val)
                });
            }

            // Try to find ResearchProjectDef field
            var researchField = partType.GetField("project", BindingFlags.NonPublic | BindingFlags.Instance);
            if (researchField != null && researchField.FieldType == typeof(ResearchProjectDef))
            {
                var currentProject = (ResearchProjectDef)researchField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Research",
                    Type = FieldType.Dropdown,
                    CurrentValue = currentProject?.LabelCap ?? "None",
                    Data = GetResearchOptions(),
                    SetValue = (val) => researchField.SetValue(part, val)
                });
            }

            // Handle ScenPart_ConfigPage_ConfigureStartingPawns - pawn count
            var pawnCountField = partType.GetField("pawnCount", BindingFlags.Public | BindingFlags.Instance);
            if (pawnCountField != null && pawnCountField.FieldType == typeof(int))
            {
                int currentValue = (int)pawnCountField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Starting Pawns",
                    Type = FieldType.Quantity,
                    CurrentValue = currentValue.ToString(),
                    Data = new int[] { 1, 10 }, // Game max is 10
                    SetValue = (val) => pawnCountField.SetValue(part, val)
                });
            }

            // Handle ScenPart_PawnFilter_Age - age range (min and max as separate fields)
            var ageRangeField = partType.GetField("allowedAgeRange", BindingFlags.Public | BindingFlags.Instance);
            if (ageRangeField != null && ageRangeField.FieldType == typeof(IntRange))
            {
                var currentRange = (IntRange)ageRangeField.GetValue(part);
                fields.Add(new PartField
                {
                    Name = "Minimum Age",
                    Type = FieldType.Quantity,
                    CurrentValue = currentRange.min.ToString(),
                    Data = new int[] { 15, 120 }, // Game limits from DoEditInterface
                    SetValue = (val) =>
                    {
                        var range = (IntRange)ageRangeField.GetValue(part);
                        range.min = (int)val;
                        // Ensure min doesn't exceed max - 4 (game requires 4 year gap)
                        if (range.min > range.max - 4)
                            range.min = range.max - 4;
                        ageRangeField.SetValue(part, range);
                    }
                });
                fields.Add(new PartField
                {
                    Name = "Maximum Age",
                    Type = FieldType.Quantity,
                    CurrentValue = currentRange.max.ToString(),
                    Data = new int[] { 19, 120 }, // Game min-max is 19
                    SetValue = (val) =>
                    {
                        var range = (IntRange)ageRangeField.GetValue(part);
                        range.max = (int)val;
                        // Ensure max is at least min + 4 (game requires 4 year gap)
                        if (range.max < range.min + 4)
                            range.max = range.min + 4;
                        ageRangeField.SetValue(part, range);
                    }
                });
            }

            // If no fields found, it's a part that can't be edited directly
            return fields;
        }

        #region Option Lists for Dropdowns

        private static List<(string label, object value)> GetPlayerFactionOptions()
        {
            var options = new List<(string, object)>();
            foreach (var faction in DefDatabase<FactionDef>.AllDefs.Where(f => f.isPlayer).OrderBy(f => f.label))
            {
                options.Add((faction.LabelCap, faction));
            }
            return options;
        }

        private static List<(string label, object value)> GetPlanetLayerOptions()
        {
            var options = new List<(string, object)>();
            foreach (var layer in DefDatabase<PlanetLayerDef>.AllDefs.OrderBy(l => l.label))
            {
                options.Add((layer.LabelCap, layer));
            }
            return options;
        }

        private static List<(string label, object value)> GetAnimalOptions()
        {
            var options = new List<(string, object)>();
            options.Add(("Random Pet", null));
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps.Animal).OrderBy(k => k.label))
            {
                options.Add((kind.LabelCap, kind));
            }
            return options;
        }

        private static List<(string label, object value)> GetTraitOptions()
        {
            var options = new List<(string, object)>();
            foreach (var trait in DefDatabase<TraitDef>.AllDefs.OrderBy(t => t.label))
            {
                options.Add((trait.label.CapitalizeFirst(), trait));
            }
            return options;
        }

        private static List<(string label, object value)> GetIncidentOptions()
        {
            var options = new List<(string, object)>();
            foreach (var incident in DefDatabase<IncidentDef>.AllDefs.Where(i => i.targetTags != null).OrderBy(i => i.label))
            {
                options.Add((incident.LabelCap, incident));
            }
            return options;
        }

        private static List<(string label, object value)> GetResearchOptions()
        {
            var options = new List<(string, object)>();
            foreach (var project in DefDatabase<ResearchProjectDef>.AllDefs.OrderBy(r => r.label))
            {
                options.Add((project.LabelCap, project));
            }
            return options;
        }

        private static List<(string label, object value)> GetArriveMethodOptions()
        {
            var options = new List<(string, object)>();
            foreach (PlayerPawnsArriveMethod method in Enum.GetValues(typeof(PlayerPawnsArriveMethod)))
            {
                // Skip Gravship if Odyssey DLC isn't active
                if (method == PlayerPawnsArriveMethod.Gravship && !ModsConfig.OdysseyActive)
                    continue;
                options.Add((method.ToStringHuman(), method));
            }
            return options;
        }

        private static List<(string label, object value)> GetStartingThingOptions(ScenPart part)
        {
            var options = new List<(string, object)>();

            // Use reflection to call PossibleThingDefs() if it exists (ScenPart_ThingCount has it)
            var possibleThingsMethod = part.GetType().GetMethod("PossibleThingDefs",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            IEnumerable<ThingDef> possibleThings;
            if (possibleThingsMethod != null)
            {
                possibleThings = (IEnumerable<ThingDef>)possibleThingsMethod.Invoke(part, null);
            }
            else
            {
                // Fallback: get all items and minifiable buildings (same as default PossibleThingDefs)
                possibleThings = DefDatabase<ThingDef>.AllDefs
                    .Where(d => (d.category == ThingCategory.Item && d.scatterableOnMapGen && !d.destroyOnDrop)
                             || (d.category == ThingCategory.Building && d.Minifiable));
            }

            foreach (var thing in possibleThings.OrderBy(t => t.label))
            {
                options.Add((thing.LabelCap, thing));
            }
            return options;
        }

        private static List<(string label, object value)> GetStuffOptions(ThingDef thingDef)
        {
            var options = new List<(string, object)>();
            if (thingDef == null || !thingDef.MadeFromStuff)
                return options;

            foreach (var stuff in GenStuff.AllowedStuffsFor(thingDef).OrderBy(s => s.label))
            {
                options.Add((stuff.LabelCap, stuff));
            }
            return options;
        }

        private static List<(string label, object value)> GetQualityOptions()
        {
            var options = new List<(string, object)>();
            // Add "Default" option (null value for QualityCategory?)
            options.Add(("Default", null));

            foreach (QualityCategory quality in QualityUtility.AllQualityCategories)
            {
                options.Add((quality.GetLabel().CapitalizeFirst(), quality));
            }
            return options;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Switches to the next section (Tab key).
        /// </summary>
        public static void NextSection()
        {
            if (isEditingText)
            {
                TolkHelper.Speak("Press Enter to confirm or Escape to cancel editing first");
                return;
            }

            if (currentSection == Section.Metadata)
            {
                currentSection = Section.Parts;
                partsIndex = 0;
                partsTypeaheadHelper.ClearSearch();
                MenuHelper.ResetLevel(LevelTrackingKey);
                TolkHelper.Speak("Parts section");
                AnnounceCurrentTreeItem();
            }
            else
            {
                currentSection = Section.Metadata;
                metadataIndex = 0;
                TolkHelper.Speak("Metadata section");
                AnnounceCurrentMetadataField();
            }
        }

        /// <summary>
        /// Switches to the previous section (Shift+Tab key).
        /// </summary>
        public static void PreviousSection()
        {
            // Same as NextSection since there are only 2 sections
            NextSection();
        }

        #endregion

        #region Metadata Navigation

        /// <summary>
        /// Moves to the next metadata field.
        /// </summary>
        public static void MetadataNext()
        {
            if (isEditingText) return;

            metadataIndex = MenuHelper.SelectNext(metadataIndex, MetadataFieldCount);
            AnnounceCurrentMetadataField();
        }

        /// <summary>
        /// Moves to the previous metadata field.
        /// </summary>
        public static void MetadataPrevious()
        {
            if (isEditingText) return;

            metadataIndex = MenuHelper.SelectPrevious(metadataIndex, MetadataFieldCount);
            AnnounceCurrentMetadataField();
        }

        /// <summary>
        /// Jumps to first metadata field.
        /// </summary>
        public static void MetadataHome()
        {
            if (isEditingText) return;

            metadataIndex = 0;
            AnnounceCurrentMetadataField();
        }

        /// <summary>
        /// Jumps to last metadata field.
        /// </summary>
        public static void MetadataEnd()
        {
            if (isEditingText) return;

            metadataIndex = MetadataFieldCount - 1;
            AnnounceCurrentMetadataField();
        }

        /// <summary>
        /// Announces the current metadata field.
        /// </summary>
        private static void AnnounceCurrentMetadataField()
        {
            if (currentScenario == null) return;

            string fieldName;
            string fieldValue;
            string hint = isEditingText ? "" : " Press Enter to edit.";

            switch (metadataIndex)
            {
                case 0:
                    fieldName = "Title";
                    fieldValue = currentScenario.name ?? "";
                    break;
                case 1:
                    fieldName = "Summary";
                    fieldValue = currentScenario.summary ?? "";
                    break;
                case 2:
                    fieldName = "Description";
                    fieldValue = currentScenario.description ?? "";
                    break;
                default:
                    return;
            }

            string positionPart = MenuHelper.FormatPosition(metadataIndex, MetadataFieldCount);
            string text = $"{fieldName}: {(string.IsNullOrEmpty(fieldValue) ? "(empty)" : fieldValue)}{hint}";

            if (!string.IsNullOrEmpty(positionPart))
            {
                text += $" ({positionPart})";
            }

            TolkHelper.Speak(text);
        }

        /// <summary>
        /// Begins editing the current metadata field.
        /// </summary>
        public static void BeginEditMetadataField()
        {
            if (currentScenario == null) return;

            switch (metadataIndex)
            {
                case 0:
                    editingFieldName = "Title";
                    originalValue = currentScenario.name ?? "";
                    break;
                case 1:
                    editingFieldName = "Summary";
                    originalValue = currentScenario.summary ?? "";
                    break;
                case 2:
                    editingFieldName = "Description";
                    originalValue = currentScenario.description ?? "";
                    break;
                default:
                    return;
            }

            TextInputHelper.SetText(originalValue);
            isEditingText = true;
            TolkHelper.Speak($"Editing {editingFieldName}. Current value: {(string.IsNullOrEmpty(originalValue) ? "empty" : originalValue)}. Type to replace, Enter to confirm, Escape to cancel.");
        }

        /// <summary>
        /// Confirms the current text edit.
        /// </summary>
        public static void ConfirmEdit()
        {
            if (!isEditingText || currentScenario == null) return;

            string newValue = TextInputHelper.CurrentText;

            switch (metadataIndex)
            {
                case 0:
                    currentScenario.name = newValue.TrimmedToLength(55);
                    break;
                case 1:
                    currentScenario.summary = newValue.TrimmedToLength(300);
                    break;
                case 2:
                    currentScenario.description = newValue.TrimmedToLength(1000);
                    break;
            }

            isEditingText = false;
            TolkHelper.Speak($"{editingFieldName} set to: {(string.IsNullOrEmpty(newValue) ? "empty" : newValue)}");
        }

        /// <summary>
        /// Cancels the current text edit.
        /// </summary>
        public static void CancelEdit()
        {
            if (!isEditingText) return;

            isEditingText = false;
            TextInputHelper.SetText(originalValue);
            TolkHelper.Speak($"Cancelled. {editingFieldName} unchanged.");
        }

        #endregion

        #region Parts TreeView Navigation

        /// <summary>
        /// Navigates up in the parts tree.
        /// </summary>
        public static void PartsNavigateUp()
        {
            if (flattenedParts.Count == 0)
            {
                TolkHelper.Speak("No scenario parts. Press Alt+A to add a part.");
                return;
            }

            partsTypeaheadHelper.ClearSearch();
            partsIndex = MenuHelper.SelectPrevious(partsIndex, flattenedParts.Count);
            AnnounceCurrentTreeItem();
        }

        /// <summary>
        /// Navigates down in the parts tree.
        /// </summary>
        public static void PartsNavigateDown()
        {
            if (flattenedParts.Count == 0)
            {
                TolkHelper.Speak("No scenario parts. Press Alt+A to add a part.");
                return;
            }

            partsTypeaheadHelper.ClearSearch();
            partsIndex = MenuHelper.SelectNext(partsIndex, flattenedParts.Count);
            AnnounceCurrentTreeItem();
        }

        /// <summary>
        /// Expands the current item or drills down to first child.
        /// </summary>
        public static void ExpandOrDrillDown()
        {
            if (flattenedParts.Count == 0 || partsIndex >= flattenedParts.Count)
                return;

            partsTypeaheadHelper.ClearSearch();
            var item = flattenedParts[partsIndex];

            if (item.IsPart && item.IsExpandable)
            {
                if (!item.IsExpanded)
                {
                    // Expand the part
                    item.AsPart.IsExpanded = true;
                    FlattenPartsTree();
                    AnnounceCurrentTreeItem();
                }
                else if (item.AsPart.Fields.Count > 0)
                {
                    // Already expanded - move to first child
                    partsIndex++;
                    AnnounceCurrentTreeItem();
                }
            }
            // For fields and non-expandable parts, Right arrow does nothing
            // (WCAG tree pattern: Right only expands/drills down, doesn't edit)
        }

        /// <summary>
        /// Activates the current item (Enter key behavior).
        /// Edits fields, provides feedback for non-editable parts.
        /// </summary>
        public static void ActivateCurrentItem()
        {
            if (flattenedParts.Count == 0 || partsIndex >= flattenedParts.Count)
                return;

            var item = flattenedParts[partsIndex];

            if (item.IsField)
            {
                // On a field - edit it
                EditCurrentField();
            }
            else if (item.IsPart && item.IsExpandable)
            {
                // On expandable part - toggle expansion
                ExpandOrDrillDown();
            }
            else if (item.IsPart && !item.IsExpandable)
            {
                // Part has no editable fields - provide feedback
                TolkHelper.Speak("This part has no editable fields. Press Delete to remove it.");
            }
        }

        /// <summary>
        /// Collapses the current item or drills up to parent.
        /// </summary>
        public static void CollapseOrDrillUp()
        {
            if (flattenedParts.Count == 0 || partsIndex >= flattenedParts.Count)
                return;

            partsTypeaheadHelper.ClearSearch();
            var item = flattenedParts[partsIndex];

            if (item.IsPart && item.IsExpanded)
            {
                // Collapse the part
                item.AsPart.IsExpanded = false;
                FlattenPartsTree();
                AnnounceCurrentTreeItem();
            }
            else if (item.IsField)
            {
                // Move to parent part
                for (int i = partsIndex - 1; i >= 0; i--)
                {
                    if (flattenedParts[i].IsPart && flattenedParts[i].AsPart == item.ParentPart)
                    {
                        partsIndex = i;
                        AnnounceCurrentTreeItem();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles Home key navigation using MenuHelper.
        /// </summary>
        public static void PartsHandleHomeKey(bool ctrlPressed)
        {
            if (flattenedParts.Count == 0) return;

            MenuHelper.HandleTreeHomeKey(
                flattenedParts,
                ref partsIndex,
                item => item.IndentLevel,
                ctrlPressed,
                () => { partsTypeaheadHelper.ClearSearch(); AnnounceCurrentTreeItem(); });
        }

        /// <summary>
        /// Handles End key navigation using MenuHelper.
        /// </summary>
        public static void PartsHandleEndKey(bool ctrlPressed)
        {
            if (flattenedParts.Count == 0) return;

            MenuHelper.HandleTreeEndKey(
                flattenedParts,
                ref partsIndex,
                item => item.IndentLevel,
                item => item.IsExpanded,
                item => item.IsExpandable && item.AsPart != null && item.AsPart.Fields.Count > 0,
                ctrlPressed,
                () => { partsTypeaheadHelper.ClearSearch(); AnnounceCurrentTreeItem(); });
        }

        /// <summary>
        /// WCAG tree view pattern: * key expands all siblings at the current level.
        /// </summary>
        public static void ExpandAllSiblings()
        {
            if (flattenedParts.Count == 0 || partsIndex >= flattenedParts.Count)
                return;

            partsTypeaheadHelper.ClearSearch();
            var currentItem = flattenedParts[partsIndex];

            // Get siblings at the same level
            List<PartTreeItem> siblings;
            if (currentItem.IsPart)
            {
                // Root level - all parts are siblings
                siblings = partsHierarchy;
            }
            else if (currentItem.IsField && currentItem.ParentPart != null)
            {
                // Fields don't expand, but we could expand all parent-level parts
                // For simplicity, announce that fields can't be expanded
                TolkHelper.Speak("Fields cannot be expanded.");
                return;
            }
            else
            {
                return;
            }

            // Find all collapsed sibling parts that can be expanded
            var collapsedSiblings = new List<PartTreeItem>();
            foreach (var sibling in siblings)
            {
                if (sibling.Fields.Count > 0 && !sibling.IsExpanded)
                {
                    collapsedSiblings.Add(sibling);
                }
            }

            // Check if there are any expandable items at this level
            bool hasExpandableItems = false;
            foreach (var sibling in siblings)
            {
                if (sibling.Fields.Count > 0)
                {
                    hasExpandableItems = true;
                    break;
                }
            }

            if (!hasExpandableItems)
            {
                TolkHelper.Speak("No parts with fields to expand at this level.");
                return;
            }

            if (collapsedSiblings.Count == 0)
            {
                TolkHelper.Speak("All parts already expanded at this level.");
                return;
            }

            // Expand all collapsed siblings
            foreach (var sibling in collapsedSiblings)
            {
                sibling.IsExpanded = true;
            }

            // Rebuild flattened list
            FlattenPartsTree();

            // Find current item in new flattened list
            for (int i = 0; i < flattenedParts.Count; i++)
            {
                if (flattenedParts[i].IsPart && flattenedParts[i].AsPart == currentItem.AsPart)
                {
                    partsIndex = i;
                    break;
                }
            }

            string expandedText = collapsedSiblings.Count == 1 ? "1 part expanded" : $"{collapsedSiblings.Count} parts expanded";
            TolkHelper.Speak(expandedText);
        }

        /// <summary>
        /// Gets the current tree item (or null if empty).
        /// </summary>
        private static TreeViewItem CurrentTreeItem =>
            flattenedParts.Count > 0 && partsIndex < flattenedParts.Count ? flattenedParts[partsIndex] : null;

        /// <summary>
        /// Gets the sibling position of an item within its level.
        /// </summary>
        private static (int position, int total) GetSiblingPosition(TreeViewItem item)
        {
            if (item.IsField && item.ParentPart != null)
            {
                // Count fields of the same parent
                int position = 0;
                int total = item.ParentPart.Fields.Count;
                for (int i = 0; i < item.ParentPart.Fields.Count; i++)
                {
                    if (item.ParentPart.Fields[i] == item.Field)
                    {
                        position = i + 1;
                        break;
                    }
                }
                return (position, total);
            }
            else if (item.IsPart)
            {
                // Count root parts
                int position = partsHierarchy.IndexOf(item.AsPart) + 1;
                return (position, partsHierarchy.Count);
            }

            return (1, 1);
        }

        /// <summary>
        /// Strips trailing punctuation (periods, colons, exclamation marks) from a string.
        /// Prevents patterns like ". :" or ": :" when concatenating strings.
        /// </summary>
        private static string StripTrailingPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.TrimEnd('.', ':', '!', '?', ',', ';');
        }

        /// <summary>
        /// Announces the current tree item.
        /// </summary>
        private static void AnnounceCurrentTreeItem()
        {
            if (flattenedParts.Count == 0)
            {
                TolkHelper.Speak("No scenario parts. Press Alt+A to add a part.");
                return;
            }

            var item = flattenedParts[partsIndex];
            var (position, total) = GetSiblingPosition(item);
            string positionPart = MenuHelper.FormatPosition(position - 1, total);
            string announcement;

            // Single-field items have BOTH AsPart and Field set - treat them as editable fields
            bool isSingleFieldPart = item.IsPart && item.IsField;

            if (isSingleFieldPart)
            {
                // Single-field part: show part label + field value + "Press Enter to edit"
                // Skip summary because it typically duplicates the field value
                var part = item.AsPart;
                var field = item.Field;
                string typeHint = field.Type == FieldType.Dropdown ? "dropdown" :
                                  field.Type == FieldType.Quantity ? "quantity" : "text";
                // Strip trailing punctuation to avoid ". :" patterns
                string partLabel = StripTrailingPunctuation(part.Label);
                string fieldValue = StripTrailingPunctuation(field.CurrentValue);
                announcement = $"{partLabel}: {fieldValue}. {typeHint}. Press Enter to edit.";
            }
            else if (item.IsPart)
            {
                var part = item.AsPart;
                string partLabel = StripTrailingPunctuation(part.Label);
                string summaryText = StripTrailingPunctuation(part.Summary);
                string summary = string.IsNullOrEmpty(summaryText) ? "" : $" ({summaryText})";

                if (item.IsExpandable)
                {
                    string state = item.IsExpanded ? "expanded" : "collapsed";
                    string itemCount = part.Fields.Count == 1 ? "1 field" : $"{part.Fields.Count} fields";
                    announcement = $"{partLabel}{summary}, {state}, {itemCount}";
                }
                else
                {
                    // Non-editable part (0 fields) - indicate read-only
                    announcement = $"{partLabel}{summary}, read only";
                }
            }
            else if (item.IsField)
            {
                // Regular field (child of expanded part)
                var field = item.Field;
                string typeHint = field.Type == FieldType.Dropdown ? "dropdown" :
                                  field.Type == FieldType.Quantity ? "quantity" : "text";
                string fieldName = StripTrailingPunctuation(field.Name);
                string fieldValue = StripTrailingPunctuation(field.CurrentValue);
                announcement = $"{fieldName}: {fieldValue}. {typeHint}. Press Enter to edit.";
            }
            else
            {
                announcement = item.Label;
            }

            if (!string.IsNullOrEmpty(positionPart))
            {
                announcement += $" ({positionPart})";
            }

            // Add level suffix
            announcement += MenuHelper.GetLevelSuffix(LevelTrackingKey, item.IndentLevel);

            TolkHelper.Speak(announcement);
        }

        /// <summary>
        /// Opens editor for the current field.
        /// </summary>
        public static void EditCurrentField()
        {
            if (flattenedParts.Count == 0 || partsIndex >= flattenedParts.Count) return;

            var item = flattenedParts[partsIndex];
            if (!item.IsField) return;

            var field = item.Field;

            // Open the appropriate editor based on field type
            ScenarioBuilderPartEditState.Open(field, () =>
            {
                // Callback when edit is complete - refresh the tree and re-announce
                BuildPartsTree();
                FlattenPartsTree();
                AnnounceCurrentTreeItem();
            });
        }

        /// <summary>
        /// Deletes the current part (if on a part, not a field).
        /// </summary>
        public static void DeletePart()
        {
            if (flattenedParts.Count == 0 || currentScenario == null) return;

            var item = flattenedParts[partsIndex];

            // Get the part (either the item itself or its parent)
            PartTreeItem partItem = item.IsPart ? item.AsPart : item.ParentPart;
            if (partItem == null) return;

            var part = partItem.Part;

            // Check if part can be removed
            if (!part.def.PlayerAddRemovable)
            {
                TolkHelper.Speak("This part cannot be removed.");
                return;
            }

            // Remember the part label before removing
            string label = partItem.Label;

            // Remove the part
            currentScenario.RemovePart(part);
            BuildPartsTree();
            FlattenPartsTree();

            // Adjust index if needed
            if (partsIndex >= flattenedParts.Count)
            {
                partsIndex = Math.Max(0, flattenedParts.Count - 1);
            }

            TolkHelper.Speak($"Removed {label}.");
            AnnounceCurrentTreeItem();
        }

        /// <summary>
        /// Moves the current part up in the list.
        /// </summary>
        public static void MovePartUp()
        {
            if (flattenedParts.Count == 0 || currentScenario == null) return;

            var item = flattenedParts[partsIndex];
            PartTreeItem partItem = item.IsPart ? item.AsPart : item.ParentPart;
            if (partItem == null) return;

            int partHierarchyIndex = partsHierarchy.IndexOf(partItem);
            if (partHierarchyIndex <= 0) return;

            var part = partItem.Part;
            string partName = StripTrailingPunctuation(part.Label);

            if (!currentScenario.CanReorder(part, ReorderDirection.Up))
            {
                TolkHelper.Speak($"Cannot move {partName} up.");
                return;
            }

            currentScenario.Reorder(part, ReorderDirection.Up);
            BuildPartsTree();
            FlattenPartsTree();

            // Find the new position of the part in the flattened list
            int newIndex = -1;
            for (int i = 0; i < flattenedParts.Count; i++)
            {
                if (flattenedParts[i].IsPart && flattenedParts[i].AsPart.Part == part)
                {
                    partsIndex = i;
                    newIndex = i;
                    break;
                }
            }

            // Build context-aware announcement
            int newHierarchyIndex = partsHierarchy.FindIndex(p => p.Part == part);
            if (newHierarchyIndex == 0)
            {
                TolkHelper.Speak($"Moved {partName} to top of list.");
            }
            else
            {
                string aboveName = StripTrailingPunctuation(partsHierarchy[newHierarchyIndex - 1].Label);
                TolkHelper.Speak($"Moved {partName} up, now below {aboveName}.");
            }
        }

        /// <summary>
        /// Moves the current part down in the list.
        /// </summary>
        public static void MovePartDown()
        {
            if (flattenedParts.Count == 0 || currentScenario == null) return;

            var item = flattenedParts[partsIndex];
            PartTreeItem partItem = item.IsPart ? item.AsPart : item.ParentPart;
            if (partItem == null) return;

            int partHierarchyIndex = partsHierarchy.IndexOf(partItem);
            if (partHierarchyIndex >= partsHierarchy.Count - 1) return;

            var part = partItem.Part;
            string partName = StripTrailingPunctuation(part.Label);

            if (!currentScenario.CanReorder(part, ReorderDirection.Down))
            {
                TolkHelper.Speak($"Cannot move {partName} down.");
                return;
            }

            currentScenario.Reorder(part, ReorderDirection.Down);
            BuildPartsTree();
            FlattenPartsTree();

            // Find the new position of the part in the flattened list
            for (int i = 0; i < flattenedParts.Count; i++)
            {
                if (flattenedParts[i].IsPart && flattenedParts[i].AsPart.Part == part)
                {
                    partsIndex = i;
                    break;
                }
            }

            // Build context-aware announcement
            int newHierarchyIndex = partsHierarchy.FindIndex(p => p.Part == part);
            if (newHierarchyIndex == partsHierarchy.Count - 1)
            {
                TolkHelper.Speak($"Moved {partName} to bottom of list.");
            }
            else
            {
                string belowName = StripTrailingPunctuation(partsHierarchy[newHierarchyIndex + 1].Label);
                TolkHelper.Speak($"Moved {partName} down, now above {belowName}.");
            }
        }

        #endregion

        #region Parts Typeahead

        /// <summary>
        /// Gets whether parts typeahead is active.
        /// </summary>
        public static bool PartsHasActiveSearch => partsTypeaheadHelper.HasActiveSearch;

        /// <summary>
        /// Handles typeahead search in parts list.
        /// </summary>
        public static bool HandlePartsTypeahead(char character)
        {
            if (flattenedParts.Count == 0)
                return false;

            var labels = flattenedParts.Select(p => p.Label).ToList();

            if (partsTypeaheadHelper.ProcessCharacterInput(character, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    partsIndex = newIndex;
                    AnnouncePartsWithSearch();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{partsTypeaheadHelper.LastFailedSearch}'");
            }

            return true;
        }

        /// <summary>
        /// Handles backspace in parts typeahead.
        /// </summary>
        public static bool HandlePartsTypeaheadBackspace()
        {
            if (!partsTypeaheadHelper.HasActiveSearch)
                return false;

            var labels = flattenedParts.Select(p => p.Label).ToList();

            if (partsTypeaheadHelper.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    partsIndex = newIndex;
                    AnnouncePartsWithSearch();
                }
            }

            return true;
        }

        /// <summary>
        /// Clears the parts typeahead search.
        /// </summary>
        public static bool ClearPartsTypeahead()
        {
            if (partsTypeaheadHelper.ClearSearchAndAnnounce())
            {
                AnnounceCurrentTreeItem();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Navigates to next typeahead match.
        /// </summary>
        public static bool SelectNextPartsMatch()
        {
            if (!partsTypeaheadHelper.HasActiveSearch)
                return false;

            int next = partsTypeaheadHelper.GetNextMatch(partsIndex);
            if (next >= 0)
            {
                partsIndex = next;
                AnnouncePartsWithSearch();
            }
            return true;
        }

        /// <summary>
        /// Navigates to previous typeahead match.
        /// </summary>
        public static bool SelectPreviousPartsMatch()
        {
            if (!partsTypeaheadHelper.HasActiveSearch)
                return false;

            int prev = partsTypeaheadHelper.GetPreviousMatch(partsIndex);
            if (prev >= 0)
            {
                partsIndex = prev;
                AnnouncePartsWithSearch();
            }
            return true;
        }

        private static void AnnouncePartsWithSearch()
        {
            if (flattenedParts.Count == 0) return;

            var item = flattenedParts[partsIndex];
            string label = item.IsPart ? item.AsPart.Label : item.Label;

            if (partsTypeaheadHelper.HasActiveSearch)
            {
                TolkHelper.Speak($"{label}, {partsTypeaheadHelper.CurrentMatchPosition} of {partsTypeaheadHelper.MatchCount} matches for '{partsTypeaheadHelper.SearchBuffer}'");
            }
            else
            {
                AnnounceCurrentTreeItem();
            }
        }

        #endregion

        #region Actions (Hotkeys)

        /// <summary>
        /// Opens the add part menu (Alt+A).
        /// </summary>
        public static void OpenAddPartMenu()
        {
            if (currentScenario == null) return;

            ScenarioBuilderAddPartState.Open(currentScenario, (ScenPartDef selectedDef) =>
            {
                if (selectedDef != null)
                {
                    // Add the part
                    ScenPart newPart = ScenarioMaker.MakeScenPart(selectedDef);
                    newPart.Randomize();

                    // Access the internal parts list via reflection
                    var partsField = AccessTools.Field(typeof(Scenario), "parts");
                    var partsList = partsField.GetValue(currentScenario) as List<ScenPart>;
                    partsList?.Add(newPart);

                    // Refresh and select the new part
                    BuildPartsTree();
                    FlattenPartsTree();

                    // Find the new part in the flattened list (it's the last part at level 0)
                    for (int i = flattenedParts.Count - 1; i >= 0; i--)
                    {
                        if (flattenedParts[i].IsPart)
                        {
                            partsIndex = i;
                            break;
                        }
                    }

                    currentSection = Section.Parts;

                    TolkHelper.Speak($"Added {selectedDef.LabelCap}. Press Right to edit fields.");
                }
            });
        }

        /// <summary>
        /// Opens the load scenario dialog (Alt+L).
        /// </summary>
        public static void OpenLoadDialog()
        {
            WindowlessScenarioLoadState.Open((Scenario loadedScenario) =>
            {
                if (loadedScenario != null && currentPage != null)
                {
                    // Update the editor with the loaded scenario
                    AccessTools.Field(typeof(Page_ScenarioEditor), "curScen").SetValue(currentPage, loadedScenario);
                    AccessTools.Field(typeof(Page_ScenarioEditor), "seedIsValid").SetValue(currentPage, false);

                    currentScenario = loadedScenario;
                    BuildPartsTree();
                    FlattenPartsTree();
                    currentSection = Section.Metadata;
                    metadataIndex = 0;
                    partsIndex = 0;

                    TolkHelper.Speak($"Loaded scenario: {loadedScenario.name}");
                }
            });
        }

        /// <summary>
        /// Opens the save scenario dialog (Alt+S).
        /// </summary>
        public static void OpenSaveDialog()
        {
            if (currentScenario == null) return;

            WindowlessScenarioSaveState.Open(currentScenario, () =>
            {
                TolkHelper.Speak("Scenario saved.");
            });
        }

        /// <summary>
        /// Randomizes the scenario seed (Alt+R).
        /// </summary>
        public static void RandomizeSeed()
        {
            if (currentPage == null) return;

            // Call the private RandomizeSeedAndScenario method
            var method = AccessTools.Method(typeof(Page_ScenarioEditor), "RandomizeSeedAndScenario");
            if (method != null)
            {
                method.Invoke(currentPage, null);

                // Refresh our reference to the scenario
                currentScenario = (Scenario)AccessTools.Field(typeof(Page_ScenarioEditor), "curScen").GetValue(currentPage);
                BuildPartsTree();
                FlattenPartsTree();
                partsIndex = 0;

                TolkHelper.Speak($"Randomized. New scenario: {currentScenario?.name ?? "New Scenario"}");
            }
        }

        /// <summary>
        /// Checks if the scenario has been modified.
        /// </summary>
        public static bool IsDirty()
        {
            if (currentScenario == null) return false;
            return currentScenario.GetHashCode() != originalHash;
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles keyboard input for the scenario builder.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            // Handle text editing mode separately
            if (isEditingText)
            {
                return HandleTextEditInput(key, shift, ctrl, alt);
            }

            // Alt key combinations (global hotkeys)
            if (alt)
            {
                switch (key)
                {
                    case KeyCode.L:
                        OpenLoadDialog();
                        return true;
                    case KeyCode.S:
                        OpenSaveDialog();
                        return true;
                    case KeyCode.R:
                        RandomizeSeed();
                        return true;
                    case KeyCode.A:
                        OpenAddPartMenu();
                        return true;
                }
            }

            // Tab to switch sections
            if (key == KeyCode.Tab)
            {
                if (shift)
                    PreviousSection();
                else
                    NextSection();
                return true;
            }

            // Section-specific navigation
            if (currentSection == Section.Metadata)
            {
                return HandleMetadataInput(key, shift, ctrl);
            }
            else
            {
                return HandlePartsInput(key, shift, ctrl);
            }
        }

        private static bool HandleTextEditInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            switch (key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ConfirmEdit();
                    return true;
                case KeyCode.Escape:
                    CancelEdit();
                    return true;
                case KeyCode.Backspace:
                    TextInputHelper.HandleBackspace();
                    return true;
            }

            // Character input is handled via Event.current.character in the patch
            return false;
        }

        private static bool HandleMetadataInput(KeyCode key, bool shift, bool ctrl)
        {
            switch (key)
            {
                case KeyCode.UpArrow:
                    MetadataPrevious();
                    return true;
                case KeyCode.DownArrow:
                    MetadataNext();
                    return true;
                case KeyCode.Home:
                    MetadataHome();
                    return true;
                case KeyCode.End:
                    MetadataEnd();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    BeginEditMetadataField();
                    return true;
            }

            return false;
        }

        private static bool HandlePartsInput(KeyCode key, bool shift, bool ctrl)
        {
            // Handle typeahead backspace
            if (key == KeyCode.Backspace && partsTypeaheadHelper.HasActiveSearch)
            {
                HandlePartsTypeaheadBackspace();
                return true;
            }

            // Handle escape to clear search
            if (key == KeyCode.Escape)
            {
                if (partsTypeaheadHelper.HasActiveSearch)
                {
                    ClearPartsTypeahead();
                    return true;
                }
                // Let escape pass through to close the builder
                return false;
            }

            // Ctrl+Up/Down for reordering (check first to avoid conflict with regular navigation)
            if (ctrl)
            {
                if (key == KeyCode.UpArrow)
                {
                    MovePartUp();
                    return true;
                }
                if (key == KeyCode.DownArrow)
                {
                    MovePartDown();
                    return true;
                }
            }

            switch (key)
            {
                case KeyCode.UpArrow:
                    if (partsTypeaheadHelper.HasActiveSearch)
                        SelectPreviousPartsMatch();
                    else
                        PartsNavigateUp();
                    return true;
                case KeyCode.DownArrow:
                    if (partsTypeaheadHelper.HasActiveSearch)
                        SelectNextPartsMatch();
                    else
                        PartsNavigateDown();
                    return true;
                case KeyCode.RightArrow:
                    // WCAG tree pattern: Right expands or drills down to first child
                    ExpandOrDrillDown();
                    return true;
                case KeyCode.LeftArrow:
                    // WCAG tree pattern: Left collapses or drills up to parent
                    CollapseOrDrillUp();
                    return true;
                case KeyCode.Home:
                    PartsHandleHomeKey(ctrl);
                    return true;
                case KeyCode.End:
                    PartsHandleEndKey(ctrl);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Enter activates the current item (edit field, expand, or feedback)
                    ActivateCurrentItem();
                    return true;
                case KeyCode.Delete:
                    DeletePart();
                    return true;
                case KeyCode.KeypadMultiply:
                    // WCAG tree pattern: * expands all siblings
                    ExpandAllSiblings();
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles character input for typeahead or text editing.
        /// </summary>
        public static bool HandleCharacterInput(char character)
        {
            if (isEditingText)
            {
                TextInputHelper.HandleCharacter(character);
                return true;
            }

            if (currentSection == Section.Parts)
            {
                // WCAG tree pattern: * expands all siblings
                if (character == '*')
                {
                    ExpandAllSiblings();
                    return true;
                }

                if (char.IsLetterOrDigit(character))
                {
                    return HandlePartsTypeahead(character);
                }
            }

            return false;
        }

        #endregion
    }
}
