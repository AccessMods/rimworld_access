using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    public static class AnimalsMenuHelper
    {
        // Column type enumeration for fixed columns
        public enum ColumnType
        {
            Name,
            Bond,
            Master,
            Slaughter,
            Gender,
            LifeStage,
            Age,
            Pregnant,
            // Dynamic training columns inserted here
            FollowDrafted,
            FollowFieldwork,
            AllowedArea,
            MedicalCare,
            FoodRestriction,
            ReleaseToWild
        }

        private static List<TrainableDef> cachedTrainables = null;
        private static int fixedColumnsBeforeTraining = 8; // Name through Pregnant
        private static int fixedColumnsAfterTraining = 6; // FollowDrafted through ReleaseToWild

        // Get all trainable definitions (cached)
        public static List<TrainableDef> GetAllTrainables()
        {
            if (cachedTrainables == null)
            {
                cachedTrainables = DefDatabase<TrainableDef>.AllDefsListForReading
                    .Where(t => !t.specialTrainable)
                    .OrderByDescending(t => t.listPriority)
                    .ToList();
            }
            return cachedTrainables;
        }

        // Get total column count (fixed + dynamic training columns)
        public static int GetTotalColumnCount()
        {
            return fixedColumnsBeforeTraining + GetAllTrainables().Count + fixedColumnsAfterTraining;
        }

        // Get column name by index (using RimWorld's localized strings)
        public static string GetColumnName(int columnIndex)
        {
            if (columnIndex < fixedColumnsBeforeTraining)
            {
                // Fixed columns before training - use localized strings
                ColumnType type = (ColumnType)columnIndex;
                switch (type)
                {
                    case ColumnType.Name: return "Name";
                    case ColumnType.Bond: return "BondInfo".Translate().Resolve();
                    case ColumnType.Master: return "Master".Translate().Resolve();
                    case ColumnType.Slaughter: return "DesignatorSlaughter".Translate().Resolve();
                    case ColumnType.Gender: return "Sex".Translate().Resolve();
                    case ColumnType.LifeStage: return "LifeStage".Translate().Resolve();
                    case ColumnType.Age: return "Age";
                    case ColumnType.Pregnant: return HediffDefOf.Pregnant.LabelCap.Resolve();
                    default: return type.ToString();
                }
            }
            else if (columnIndex < fixedColumnsBeforeTraining + GetAllTrainables().Count)
            {
                // Training columns - already localized via LabelCap
                int trainableIndex = columnIndex - fixedColumnsBeforeTraining;
                return GetAllTrainables()[trainableIndex].LabelCap;
            }
            else
            {
                // Fixed columns after training - use localized strings
                int fixedIndex = columnIndex - fixedColumnsBeforeTraining - GetAllTrainables().Count;
                ColumnType type = (ColumnType)(fixedColumnsBeforeTraining + fixedIndex);
                switch (type)
                {
                    case ColumnType.FollowDrafted: return "CreatureFollowDrafted".Translate().Resolve();
                    case ColumnType.FollowFieldwork: return "CreatureFollowFieldwork".Translate().Resolve();
                    case ColumnType.AllowedArea: return "AllowedArea".Translate().Resolve();
                    case ColumnType.MedicalCare: return "MedicalCare".Translate().Resolve();
                    case ColumnType.FoodRestriction: return "FoodRestriction".Translate().Resolve();
                    case ColumnType.ReleaseToWild: return "DesignatorReleaseAnimalToWild".Translate().Resolve();
                    default: return type.ToString().Replace("_", " ");
                }
            }
        }

        // Get column value for a pawn
        public static string GetColumnValue(Pawn pawn, int columnIndex)
        {
            if (columnIndex < fixedColumnsBeforeTraining)
            {
                // Fixed columns before training
                switch ((ColumnType)columnIndex)
                {
                    case ColumnType.Name:
                        return GetAnimalName(pawn);
                    case ColumnType.Bond:
                        return GetBondStatus(pawn);
                    case ColumnType.Master:
                        return GetMasterName(pawn);
                    case ColumnType.Slaughter:
                        return GetSlaughterStatus(pawn);
                    case ColumnType.Gender:
                        return GetGender(pawn);
                    case ColumnType.LifeStage:
                        return GetLifeStage(pawn);
                    case ColumnType.Age:
                        return GetAge(pawn);
                    case ColumnType.Pregnant:
                        return GetPregnancyStatus(pawn);
                }
            }
            else if (columnIndex < fixedColumnsBeforeTraining + GetAllTrainables().Count)
            {
                // Training columns
                int trainableIndex = columnIndex - fixedColumnsBeforeTraining;
                TrainableDef trainable = GetAllTrainables()[trainableIndex];
                return GetTrainingStatus(pawn, trainable);
            }
            else
            {
                // Fixed columns after training
                int fixedIndex = columnIndex - fixedColumnsBeforeTraining - GetAllTrainables().Count;
                ColumnType type = (ColumnType)(fixedColumnsBeforeTraining + fixedIndex);
                switch (type)
                {
                    case ColumnType.FollowDrafted:
                        return GetFollowDrafted(pawn);
                    case ColumnType.FollowFieldwork:
                        return GetFollowFieldwork(pawn);
                    case ColumnType.AllowedArea:
                        return GetAllowedArea(pawn);
                    case ColumnType.MedicalCare:
                        return GetMedicalCare(pawn);
                    case ColumnType.FoodRestriction:
                        return GetFoodRestriction(pawn);
                    case ColumnType.ReleaseToWild:
                        return GetReleaseToWildStatus(pawn);
                }
            }
            return "Unknown";
        }

        // Check if column is interactive (can be changed with Enter key)
        public static bool IsColumnInteractive(int columnIndex)
        {
            if (columnIndex < fixedColumnsBeforeTraining)
            {
                ColumnType type = (ColumnType)columnIndex;
                return type == ColumnType.Master || type == ColumnType.Slaughter;
            }
            else if (columnIndex < fixedColumnsBeforeTraining + GetAllTrainables().Count)
            {
                return true; // All training columns are interactive
            }
            else
            {
                int fixedIndex = columnIndex - fixedColumnsBeforeTraining - GetAllTrainables().Count;
                ColumnType type = (ColumnType)(fixedColumnsBeforeTraining + fixedIndex);
                return type == ColumnType.FollowDrafted ||
                       type == ColumnType.FollowFieldwork ||
                       type == ColumnType.AllowedArea ||
                       type == ColumnType.MedicalCare ||
                       type == ColumnType.FoodRestriction ||
                       type == ColumnType.ReleaseToWild;
            }
        }

        // === Fixed Column Accessors ===

        public static string GetAnimalName(Pawn pawn)
        {
            string name = pawn.Name != null ? pawn.Name.ToStringShort : pawn.def.LabelCap.ToString();
            return $"{name} ({pawn.def.LabelCap})";
        }

        public static string GetBondStatus(Pawn pawn)
        {
            if (pawn.relations == null) return "NotBonded".Translate().Resolve();

            Pawn bondedPawn = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedPawn != null)
            {
                return "BondedTo".Translate(bondedPawn.Named("PAWN")).Resolve();
            }
            return "NotBonded".Translate().Resolve();
        }

        public static string GetMasterName(Pawn pawn)
        {
            if (pawn.playerSettings == null || pawn.playerSettings.Master == null)
            {
                return "None".Translate().Resolve();
            }
            return pawn.playerSettings.Master.Name.ToStringShort;
        }

        public static string GetSlaughterStatus(Pawn pawn)
        {
            if (pawn.Map == null) return "N/A";

            Designation designation = pawn.Map.designationManager.DesignationOn(pawn, DesignationDefOf.Slaughter);
            string markedLabel = DesignationDefOf.Slaughter.label.CapitalizeFirst();
            string notMarkedLabel = "None".Translate().Resolve();
            return designation != null ? markedLabel : notMarkedLabel;
        }

        public static string GetGender(Pawn pawn)
        {
            // Use RimWorld's localized gender labels
            return pawn.gender.GetLabel(animal: true).CapitalizeFirst();
        }

        public static string GetLifeStage(Pawn pawn)
        {
            if (pawn.ageTracker == null) return "Unknown";
            return pawn.ageTracker.CurLifeStage.label.CapitalizeFirst();
        }

        public static string GetAge(Pawn pawn)
        {
            if (pawn.ageTracker == null) return "Unknown";
            // Use RimWorld's localized age string
            return pawn.ageTracker.AgeNumberString;
        }

        public static string GetPregnancyStatus(Pawn pawn)
        {
            if (pawn.gender != Gender.Female) return "N/A";
            if (pawn.health?.hediffSet == null) return "None".Translate().Resolve();

            Hediff_Pregnant pregnancy = (Hediff_Pregnant)pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            if (pregnancy != null)
            {
                // Use hediff's localized label and progress
                return $"{pregnancy.LabelCap} ({pregnancy.GestationProgress.ToStringPercent()})";
            }
            return "None".Translate().Resolve();
        }

        // === Training Column Accessors ===

        public static string GetTrainingStatus(Pawn pawn, TrainableDef trainable)
        {
            if (pawn.training == null) return "N/A";

            AcceptanceReport canTrain = pawn.training.CanAssignToTrain(trainable);

            string statusText = "";

            if (!canTrain.Accepted)
            {
                statusText = "CannotTrain".Translate().Resolve();
                // Add the reason why they can't train (already localized by RimWorld)
                if (!string.IsNullOrEmpty(canTrain.Reason))
                {
                    statusText += " - " + canTrain.Reason;
                }
            }
            else
            {
                bool wanted = pawn.training.GetWanted(trainable);
                if (!wanted)
                {
                    statusText = "Disabled".Translate().Resolve();
                }
                else if (pawn.training.HasLearned(trainable))
                {
                    statusText = "Trained".Translate().Resolve();
                }
                else
                {
                    // Use reflection to access internal GetSteps method
                    var getStepsMethod = typeof(Pawn_TrainingTracker).GetMethod("GetSteps",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (getStepsMethod != null)
                    {
                        int steps = (int)getStepsMethod.Invoke(pawn.training, new object[] { trainable });
                        if (steps > 0)
                        {
                            statusText = "TrainingInProgress".Translate().Resolve() + $" ({steps}/{trainable.steps})";
                        }
                        else
                        {
                            statusText = "NotStarted".Translate().Resolve();
                        }
                    }
                    else
                    {
                        statusText = "NotStarted".Translate().Resolve();
                    }
                }

                // Add prerequisite information if not learned and has prerequisites
                if (!pawn.training.HasLearned(trainable) && trainable.prerequisites != null && trainable.prerequisites.Count > 0)
                {
                    foreach (var prereq in trainable.prerequisites)
                    {
                        if (!pawn.training.HasLearned(prereq))
                        {
                            statusText += " - " + "TrainingNeedsPrerequisite".Translate(prereq.LabelCap).Resolve();
                            break; // Only show first missing prerequisite to keep it concise
                        }
                    }
                }
            }

            // Add training description (already localized)
            if (!string.IsNullOrEmpty(trainable.description))
            {
                statusText += " - " + trainable.description;
            }

            return statusText;
        }

        public static TrainableDef GetTrainableAtColumn(int columnIndex)
        {
            if (columnIndex < fixedColumnsBeforeTraining ||
                columnIndex >= fixedColumnsBeforeTraining + GetAllTrainables().Count)
            {
                return null;
            }

            int trainableIndex = columnIndex - fixedColumnsBeforeTraining;
            return GetAllTrainables()[trainableIndex];
        }

        // === Follow Settings ===

        public static string GetFollowDrafted(Pawn pawn)
        {
            if (pawn.playerSettings == null) return "N/A";

            return pawn.playerSettings.followDrafted ? "Yes".Translate().Resolve() : "No".Translate().Resolve();
        }

        public static string GetFollowFieldwork(Pawn pawn)
        {
            if (pawn.playerSettings == null) return "N/A";

            return pawn.playerSettings.followFieldwork ? "Yes".Translate().Resolve() : "No".Translate().Resolve();
        }

        // === Area Restriction ===

        public static string GetAllowedArea(Pawn pawn)
        {
            if (pawn.playerSettings == null) return "N/A";

            Area area = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
            if (area == null)
            {
                return "Unrestricted".Translate().Resolve();
            }
            return area.Label;
        }

        public static List<Area> GetAvailableAreas()
        {
            if (Find.CurrentMap == null) return new List<Area>();

            return Find.CurrentMap.areaManager.AllAreas
                .Where(a => a.AssignableAsAllowed())
                .ToList();
        }

        // === Medical Care ===

        public static string GetMedicalCare(Pawn pawn)
        {
            if (pawn.playerSettings == null) return "N/A";

            MedicalCareCategory category = pawn.playerSettings.medCare;
            return category.GetLabel();
        }

        public static List<MedicalCareCategory> GetMedicalCareLevels()
        {
            return Enum.GetValues(typeof(MedicalCareCategory))
                .Cast<MedicalCareCategory>()
                .ToList();
        }

        // === Food Restriction ===

        public static string GetFoodRestriction(Pawn pawn)
        {
            if (pawn.foodRestriction == null || pawn.foodRestriction.CurrentFoodPolicy == null)
            {
                return "Unrestricted".Translate().Resolve();
            }
            return pawn.foodRestriction.CurrentFoodPolicy.label;
        }

        public static List<FoodPolicy> GetFoodPolicies()
        {
            if (Current.Game == null) return new List<FoodPolicy>();

            return Current.Game.foodRestrictionDatabase.AllFoodRestrictions.ToList();
        }

        // === Release to Wild ===

        public static string GetReleaseToWildStatus(Pawn pawn)
        {
            if (pawn.Map == null) return "N/A";

            Designation designation = pawn.Map.designationManager.DesignationOn(pawn, DesignationDefOf.ReleaseAnimalToWild);
            string markedLabel = DesignationDefOf.ReleaseAnimalToWild.label.CapitalizeFirst();
            string notMarkedLabel = "None".Translate().Resolve();
            return designation != null ? markedLabel : notMarkedLabel;
        }

        // === Master Assignment ===

        public static List<Pawn> GetAvailableColonists()
        {
            if (Find.CurrentMap == null) return new List<Pawn>();

            return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && !p.Downed)
                .OrderBy(p => p.Name.ToStringShort)
                .ToList();
        }

        // === Sorting ===

        public static List<Pawn> SortAnimalsByColumn(List<Pawn> animals, int columnIndex, bool descending)
        {
            IEnumerable<Pawn> sorted = null;

            if (columnIndex < fixedColumnsBeforeTraining)
            {
                ColumnType type = (ColumnType)columnIndex;
                switch (type)
                {
                    case ColumnType.Name:
                        sorted = animals.OrderBy(p => p.Name?.ToStringShort ?? p.def.label);
                        break;
                    case ColumnType.Bond:
                        sorted = animals.OrderBy(p => GetBondStatus(p));
                        break;
                    case ColumnType.Master:
                        sorted = animals.OrderBy(p => GetMasterName(p));
                        break;
                    case ColumnType.Slaughter:
                        sorted = animals.OrderBy(p => GetSlaughterStatus(p));
                        break;
                    case ColumnType.Gender:
                        sorted = animals.OrderBy(p => p.gender);
                        break;
                    case ColumnType.LifeStage:
                        sorted = animals.OrderBy(p => p.ageTracker.CurLifeStageIndex);
                        break;
                    case ColumnType.Age:
                        sorted = animals.OrderBy(p => p.ageTracker.AgeBiologicalYearsFloat);
                        break;
                    case ColumnType.Pregnant:
                        sorted = animals.OrderBy(p => GetPregnancyStatus(p));
                        break;
                    default:
                        sorted = animals;
                        break;
                }
            }
            else if (columnIndex < fixedColumnsBeforeTraining + GetAllTrainables().Count)
            {
                // Sort by training status
                TrainableDef trainable = GetTrainableAtColumn(columnIndex);
                if (trainable != null)
                {
                    sorted = animals.OrderBy(p => GetTrainingStatus(p, trainable));
                }
                else
                {
                    sorted = animals;
                }
            }
            else
            {
                int fixedIndex = columnIndex - fixedColumnsBeforeTraining - GetAllTrainables().Count;
                ColumnType type = (ColumnType)(fixedColumnsBeforeTraining + fixedIndex);
                switch (type)
                {
                    case ColumnType.FollowDrafted:
                        sorted = animals.OrderBy(p => GetFollowDrafted(p));
                        break;
                    case ColumnType.FollowFieldwork:
                        sorted = animals.OrderBy(p => GetFollowFieldwork(p));
                        break;
                    case ColumnType.AllowedArea:
                        sorted = animals.OrderBy(p => GetAllowedArea(p));
                        break;
                    case ColumnType.MedicalCare:
                        sorted = animals.OrderBy(p => GetMedicalCare(p));
                        break;
                    case ColumnType.FoodRestriction:
                        sorted = animals.OrderBy(p => GetFoodRestriction(p));
                        break;
                    case ColumnType.ReleaseToWild:
                        sorted = animals.OrderBy(p => GetReleaseToWildStatus(p));
                        break;
                    default:
                        sorted = animals;
                        break;
                }
            }

            if (descending)
            {
                sorted = sorted.Reverse();
            }

            return sorted.ToList();
        }
    }
}
