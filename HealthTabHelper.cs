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
    /// Helper class for health tab data extraction and interactions.
    /// Provides methods for medical settings, capacities, operations, and hediff information.
    /// </summary>
    public static class HealthTabHelper
    {
        /// <summary>
        /// Represents a capacity with its level and breakdown.
        /// </summary>
        public class CapacityInfo
        {
            public PawnCapacityDef Def { get; set; }
            public string Label { get; set; }
            public float Level { get; set; }
            public string LevelLabel { get; set; }
            public string DetailedBreakdown { get; set; }
        }

        /// <summary>
        /// Represents a body part with its hediffs.
        /// </summary>
        public class BodyPartInfo
        {
            public BodyPartRecord Part { get; set; }
            public string Label { get; set; }
            public float Health { get; set; }
            public float MaxHealth { get; set; }
            public float Efficiency { get; set; }
            public List<HediffInfo> Hediffs { get; set; }

            public BodyPartInfo()
            {
                Hediffs = new List<HediffInfo>();
            }
        }

        /// <summary>
        /// Represents a hediff (health condition).
        /// </summary>
        public class HediffInfo
        {
            public Hediff Hediff { get; set; }
            public string Label { get; set; }
            public string DetailedInfo { get; set; }
        }

        /// <summary>
        /// Represents a medical operation.
        /// </summary>
        public class OperationInfo
        {
            public RecipeDef Recipe { get; set; }
            public BodyPartRecord BodyPart { get; set; }
            public string Label { get; set; }
            public string Requirements { get; set; }
            public bool IsAvailable { get; set; }
            public string UnavailableReason { get; set; }
        }

        #region Medical Settings

        /// <summary>
        /// Gets the current food restriction for a pawn.
        /// </summary>
        public static string GetCurrentFoodRestriction(Pawn pawn)
        {
            if (pawn?.foodRestriction?.CurrentFoodPolicy == null)
                return "None";

            return pawn.foodRestriction.CurrentFoodPolicy.label;
        }

        /// <summary>
        /// Gets all available food restrictions.
        /// </summary>
        public static List<FoodPolicy> GetAvailableFoodRestrictions()
        {
            if (Current.Game?.foodRestrictionDatabase == null)
                return new List<FoodPolicy>();

            return Current.Game.foodRestrictionDatabase.AllFoodRestrictions.ToList();
        }

        /// <summary>
        /// Sets the food restriction for a pawn.
        /// </summary>
        public static bool SetFoodRestriction(Pawn pawn, FoodPolicy restriction)
        {
            try
            {
                if (pawn?.foodRestriction == null)
                    return false;

                pawn.foodRestriction.CurrentFoodPolicy = restriction;
                ClipboardHelper.CopyToClipboard($"Food restriction set to: {restriction.label}");
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error setting food restriction: {ex}");
                ClipboardHelper.CopyToClipboard("Error setting food restriction");
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return false;
            }
        }

        /// <summary>
        /// Gets the current medical care quality for a pawn.
        /// </summary>
        public static string GetCurrentMedicalCare(Pawn pawn)
        {
            if (pawn?.playerSettings == null)
                return "None";

            return pawn.playerSettings.medCare.GetLabel();
        }

        /// <summary>
        /// Gets all available medical care levels.
        /// </summary>
        public static List<MedicalCareCategory> GetAvailableMedicalCare()
        {
            return Enum.GetValues(typeof(MedicalCareCategory))
                .Cast<MedicalCareCategory>()
                .ToList();
        }

        /// <summary>
        /// Sets the medical care quality for a pawn.
        /// </summary>
        public static bool SetMedicalCare(Pawn pawn, MedicalCareCategory care)
        {
            try
            {
                if (pawn?.playerSettings == null)
                    return false;

                pawn.playerSettings.medCare = care;
                ClipboardHelper.CopyToClipboard($"Medical care set to: {care.GetLabel()}");
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error setting medical care: {ex}");
                ClipboardHelper.CopyToClipboard("Error setting medical care");
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return false;
            }
        }

        /// <summary>
        /// Gets whether self-tend is enabled for a pawn.
        /// </summary>
        public static bool GetSelfTendEnabled(Pawn pawn)
        {
            if (pawn?.playerSettings == null)
                return false;

            return pawn.playerSettings.selfTend;
        }

        /// <summary>
        /// Toggles self-tend for a pawn.
        /// </summary>
        public static bool ToggleSelfTend(Pawn pawn)
        {
            try
            {
                if (pawn?.playerSettings == null)
                    return false;

                pawn.playerSettings.selfTend = !pawn.playerSettings.selfTend;
                string status = pawn.playerSettings.selfTend ? "enabled" : "disabled";
                ClipboardHelper.CopyToClipboard($"Self-tend {status}");
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error toggling self-tend: {ex}");
                ClipboardHelper.CopyToClipboard("Error toggling self-tend");
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return false;
            }
        }

        #endregion

        #region Capacities

        /// <summary>
        /// Gets all capacity information for a pawn.
        /// </summary>
        public static List<CapacityInfo> GetCapacities(Pawn pawn)
        {
            var capacities = new List<CapacityInfo>();

            if (pawn?.health?.capacities == null)
                return capacities;

            // Get key capacities in order
            var keyCapacities = new List<PawnCapacityDef>
            {
                PawnCapacityDefOf.Consciousness,
                PawnCapacityDefOf.Sight,
                PawnCapacityDefOf.Hearing,
                PawnCapacityDefOf.Moving,
                PawnCapacityDefOf.Manipulation,
                PawnCapacityDefOf.Talking,
                PawnCapacityDefOf.Breathing,
                PawnCapacityDefOf.BloodFiltration,
                PawnCapacityDefOf.BloodPumping
            };

            foreach (var capacityDef in keyCapacities)
            {
                if (capacityDef == null || !pawn.health.capacities.CapableOf(capacityDef))
                    continue;

                float level = pawn.health.capacities.GetLevel(capacityDef);
                string label = capacityDef.LabelCap.ToString().StripTags();
                string levelLabel = GetCapacityLevelLabel(level);

                capacities.Add(new CapacityInfo
                {
                    Def = capacityDef,
                    Label = label,
                    Level = level,
                    LevelLabel = levelLabel,
                    DetailedBreakdown = GetCapacityBreakdown(pawn, capacityDef)
                });
            }

            return capacities;
        }

        /// <summary>
        /// Gets a human-readable label for a capacity level.
        /// </summary>
        private static string GetCapacityLevelLabel(float level)
        {
            if (level <= 0f)
                return "None (0%)";
            if (level < 0.4f)
                return $"Very Poor ({level:P0})";
            if (level < 0.7f)
                return $"Poor ({level:P0})";
            if (level < 1.0f)
                return $"Weakened ({level:P0})";
            if (level < 1.3f)
                return $"Good ({level:P0})";
            return $"Enhanced ({level:P0})";
        }

        /// <summary>
        /// Gets a detailed breakdown of what affects a capacity.
        /// </summary>
        private static string GetCapacityBreakdown(Pawn pawn, PawnCapacityDef capacity)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{capacity.LabelCap.ToString().StripTags()}:");
            sb.AppendLine();

            // Get capacity breakdown using PawnCapacityUtility
            var impactors = new List<PawnCapacityUtility.CapacityImpactor>();
            float level = PawnCapacityUtility.CalculateCapacityLevel(
                pawn.health.hediffSet,
                capacity,
                impactors
            );

            sb.AppendLine($"Current level: {level:P0}");
            sb.AppendLine();

            if (impactors != null && impactors.Count > 0)
            {
                sb.AppendLine("Factors:");
                foreach (var impactor in impactors)
                {
                    string readable = impactor.Readable(pawn);
                    if (!string.IsNullOrEmpty(readable))
                    {
                        sb.AppendLine($"  {readable}");
                    }
                }
            }
            else
            {
                sb.AppendLine("No factors affecting this capacity");
            }

            return sb.ToString().TrimEnd();
        }

        #endregion

        #region Operations

        /// <summary>
        /// Gets all queued operations for a pawn.
        /// </summary>
        public static List<Bill> GetQueuedOperations(Pawn pawn)
        {
            if (pawn?.BillStack == null)
                return new List<Bill>();

            return pawn.BillStack.Bills.ToList();
        }

        /// <summary>
        /// Gets all available operations for a pawn.
        /// </summary>
        public static List<OperationInfo> GetAvailableOperations(Pawn pawn)
        {
            var operations = new List<OperationInfo>();

            if (pawn?.health == null)
                return operations;

            // Get all medical recipes
            var recipes = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.AllRecipeUsers != null &&
                           r.AllRecipeUsers.Contains(pawn.def) &&
                           r.AvailableNow)
                .ToList();

            foreach (var recipe in recipes)
            {
                // Check if recipe applies to whole body or specific parts
                if (recipe.appliedOnFixedBodyParts != null && recipe.appliedOnFixedBodyParts.Count > 0)
                {
                    // Recipe applies to specific body parts
                    foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
                    {
                        if (recipe.appliedOnFixedBodyParts.Contains(part.def))
                        {
                            var opInfo = CreateOperationInfo(pawn, recipe, part);
                            operations.Add(opInfo);
                        }
                    }
                }
                else
                {
                    // Recipe applies to whole body
                    var opInfo = CreateOperationInfo(pawn, recipe, null);
                    operations.Add(opInfo);
                }
            }

            return operations;
        }

        private static OperationInfo CreateOperationInfo(Pawn pawn, RecipeDef recipe, BodyPartRecord part)
        {
            var opInfo = new OperationInfo
            {
                Recipe = recipe,
                BodyPart = part,
                Label = recipe.LabelCap.ToString().StripTags()
            };

            if (part != null)
            {
                opInfo.Label += $" ({part.Label})";
            }

            // Check availability
            var violations = new List<string>();
            if (!recipe.Worker.AvailableOnNow(pawn, part))
            {
                opInfo.IsAvailable = false;
                opInfo.UnavailableReason = "Not available on this pawn";
            }
            else
            {
                opInfo.IsAvailable = true;
            }

            // Get requirements
            var reqSb = new StringBuilder();
            if (recipe.ingredients != null && recipe.ingredients.Count > 0)
            {
                reqSb.Append("Requires: ");
                foreach (var ingredient in recipe.ingredients)
                {
                    reqSb.Append($"{ingredient.Summary}, ");
                }
                opInfo.Requirements = reqSb.ToString().TrimEnd(',', ' ');
            }

            return opInfo;
        }

        /// <summary>
        /// Adds an operation to a pawn's bill stack.
        /// </summary>
        public static bool AddOperation(Pawn pawn, RecipeDef recipe, BodyPartRecord part)
        {
            try
            {
                if (pawn?.BillStack == null)
                    return false;

                // Create bill with no parameters, then set properties
                Bill_Medical bill = new Bill_Medical();
                pawn.BillStack.AddBill(bill);
                bill.recipe = recipe;
                bill.Part = part;

                ClipboardHelper.CopyToClipboard($"Added operation: {recipe.LabelCap.ToString().StripTags()}");
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error adding operation: {ex}");
                ClipboardHelper.CopyToClipboard("Error adding operation");
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return false;
            }
        }

        /// <summary>
        /// Removes an operation from a pawn's bill stack.
        /// </summary>
        public static bool RemoveOperation(Pawn pawn, Bill bill)
        {
            try
            {
                if (pawn?.BillStack == null || bill == null)
                    return false;

                pawn.BillStack.Delete(bill);
                ClipboardHelper.CopyToClipboard($"Removed operation: {bill.LabelCap.ToString().StripTags()}");
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldAccess] Error removing operation: {ex}");
                ClipboardHelper.CopyToClipboard("Error removing operation");
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return false;
            }
        }

        #endregion

        #region Body Parts & Hediffs

        /// <summary>
        /// Gets all body parts with their hediffs organized.
        /// </summary>
        public static List<BodyPartInfo> GetBodyPartsWithHediffs(Pawn pawn)
        {
            var parts = new List<BodyPartInfo>();

            if (pawn?.health?.hediffSet == null)
                return parts;

            // First, get whole-body hediffs
            var wholeBodyHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Part == null && h.Visible)
                .ToList();

            if (wholeBodyHediffs.Count > 0)
            {
                var wholeBodyPart = new BodyPartInfo
                {
                    Part = null,
                    Label = "Whole Body",
                    Health = 0,
                    MaxHealth = 0,
                    Efficiency = 1.0f
                };

                foreach (var hediff in wholeBodyHediffs)
                {
                    wholeBodyPart.Hediffs.Add(CreateHediffInfo(hediff));
                }

                parts.Add(wholeBodyPart);
            }

            // Then get hediffs for each body part
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                var hediffs = pawn.health.hediffSet.hediffs
                    .Where(h => h.Part == part && h.Visible)
                    .ToList();

                if (hediffs.Count == 0)
                    continue;

                float partHealth = pawn.health.hediffSet.GetPartHealth(part);
                float maxHealth = part.def.GetMaxHealth(pawn);
                float efficiency = PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, part);

                var partInfo = new BodyPartInfo
                {
                    Part = part,
                    Label = part.Label,
                    Health = partHealth,
                    MaxHealth = maxHealth,
                    Efficiency = efficiency
                };

                foreach (var hediff in hediffs)
                {
                    partInfo.Hediffs.Add(CreateHediffInfo(hediff));
                }

                parts.Add(partInfo);
            }

            return parts;
        }

        private static HediffInfo CreateHediffInfo(Hediff hediff)
        {
            return new HediffInfo
            {
                Hediff = hediff,
                Label = hediff.LabelCap.ToString().StripTags(),
                DetailedInfo = GetHediffDetailedInfo(hediff)
            };
        }

        private static string GetHediffDetailedInfo(Hediff hediff)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{hediff.LabelCap.ToString().StripTags()}:");
            sb.AppendLine();

            // Description
            if (!string.IsNullOrEmpty(hediff.def.description))
            {
                sb.AppendLine(hediff.def.description);
                sb.AppendLine();
            }

            // Severity/Stage
            if (hediff.def.stages != null && hediff.def.stages.Count > 0)
            {
                sb.AppendLine($"Stage: {hediff.CurStageIndex + 1} of {hediff.def.stages.Count}");
                sb.AppendLine($"Severity: {hediff.Severity:F2}");
                sb.AppendLine();
            }

            // Immunity
            if (hediff.TryGetComp<HediffComp_Immunizable>() is HediffComp_Immunizable immunizable)
            {
                if (hediff.pawn != null && hediff.pawn.health?.immunity != null)
                {
                    float immunity = hediff.pawn.health.immunity.GetImmunity(hediff.def);
                    sb.AppendLine($"Immunity: {immunity:P0}");
                    sb.AppendLine();
                }
            }

            // Bleeding
            if (hediff.Bleeding)
            {
                sb.AppendLine($"Bleeding: {hediff.BleedRate:F2} per day");
                sb.AppendLine();
            }

            // Pain
            float pain = hediff.PainOffset;
            if (pain > 0.01f)
            {
                sb.AppendLine($"Pain: +{pain:P0}");
                sb.AppendLine();
            }

            // Tendable
            if (hediff.TendableNow())
            {
                sb.AppendLine("Can be tended");
                sb.AppendLine();
            }

            // Part efficiency
            if (hediff.Part != null)
            {
                sb.AppendLine($"Affects: {hediff.Part.Label}");
            }

            return sb.ToString().TrimEnd();
        }

        #endregion
    }
}
