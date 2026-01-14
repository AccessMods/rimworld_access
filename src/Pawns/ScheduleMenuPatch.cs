using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to intercept the Schedule tab opening and replace it with our accessible version.
    /// This ensures the accessible Schedule menu opens even when triggered from the world map.
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Schedule), nameof(MainTabWindow_Schedule.DoWindowContents))]
    public static class ScheduleMenuPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // If on the world map, switch to colony map first
            // Note: Check WorldNavigationState.IsActive instead of wantedMode because
            // the game's PostOpen may have already set wantedMode=None before DoWindowContents runs
            if (WorldNavigationState.IsActive)
            {
                CameraJumper.TryHideWorld();
                MapNavigationState.RestoreCursorForCurrentMap();
            }

            // Open our windowless version instead
            WindowlessScheduleState.Open();

            // Close the window that was just opened
            Find.WindowStack.TryRemove(typeof(MainTabWindow_Schedule), doCloseSound: false);

            // Return false to prevent the original DoWindowContents from executing
            return false;
        }
    }
}
