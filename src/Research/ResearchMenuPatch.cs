using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to intercept the Research tab opening and replace it with our accessible version.
    /// This ensures the accessible Research menu opens even when triggered from the world map.
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.DoWindowContents))]
    public static class ResearchMenuPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // If on the world map, switch to colony map first
            if (Find.World?.renderer?.wantedMode == WorldRenderMode.Planet)
            {
                CameraJumper.TryHideWorld();
                MapNavigationState.RestoreCursorForCurrentMap();
            }

            // Open our windowless version instead
            WindowlessResearchMenuState.Open();

            // Close the window that was just opened
            Find.WindowStack.TryRemove(typeof(MainTabWindow_Research), doCloseSound: false);

            // Return false to prevent the original DoWindowContents from executing
            return false;
        }
    }
}
