using HarmonyLib;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Announces mortar shell impacts for accessibility.
    /// </summary>
    [HarmonyPatch(typeof(Projectile_Explosive))]
    [HarmonyPatch("Explode")]
    public static class ShellImpactPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Projectile_Explosive __instance)
        {
            // Only announce overhead projectiles (mortars, artillery)
            if (__instance.def?.projectile == null || !__instance.def.projectile.flyOverhead)
                return;

            // Only announce on player's current map
            if (__instance.Map != Find.CurrentMap)
                return;

            IntVec3 pos = __instance.Position;
            string location = $"{pos.x}, {pos.z}";

            // Check what's at the impact site
            string target = "";
            var things = pos.GetThingList(__instance.Map);
            foreach (var thing in things)
            {
                if (thing is Pawn pawn)
                {
                    target = pawn.LabelShort;
                    break;
                }
                if (thing is Building building && building.def.building != null)
                {
                    target = building.LabelShort;
                    break;
                }
            }

            // Determine source
            string source = "Shell";
            if (__instance.Launcher?.Faction == Faction.OfPlayer)
            {
                source = "Our shell";
            }
            else if (__instance.Launcher?.Faction != null && __instance.Launcher.Faction.HostileTo(Faction.OfPlayer))
            {
                source = "Enemy shell";
            }

            // Build announcement
            string announcement;
            if (!string.IsNullOrEmpty(target))
            {
                announcement = $"{source} hit {target} at {location}";
            }
            else
            {
                announcement = $"{source} landed at {location}";
            }

            TolkHelper.Speak(announcement, SpeechPriority.High);
        }
    }
}
