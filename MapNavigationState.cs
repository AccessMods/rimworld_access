using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Maintains the state of map navigation for accessibility features.
    /// Tracks the current cursor position as the user navigates the map with arrow keys.
    /// </summary>
    public static class MapNavigationState
    {
        private static IntVec3 currentCursorPosition = IntVec3.Invalid;
        private static string lastAnnouncedInfo = "";
        private static bool isInitialized = false;
        private static bool suppressMapNavigation = false;

        /// <summary>
        /// Gets or sets the current cursor position on the map.
        /// </summary>
        public static IntVec3 CurrentCursorPosition
        {
            get => currentCursorPosition;
            set => currentCursorPosition = value;
        }

        /// <summary>
        /// Gets or sets the last announced tile information to avoid repetition.
        /// </summary>
        public static string LastAnnouncedInfo
        {
            get => lastAnnouncedInfo;
            set => lastAnnouncedInfo = value;
        }

        /// <summary>
        /// Indicates whether the navigation state has been initialized for the current map.
        /// </summary>
        public static bool IsInitialized
        {
            get => isInitialized;
            set => isInitialized = value;
        }

        /// <summary>
        /// Gets or sets whether map navigation should be suppressed (e.g., when menus are open).
        /// When true, arrow keys will not move the map cursor.
        /// </summary>
        public static bool SuppressMapNavigation
        {
            get => suppressMapNavigation;
            set => suppressMapNavigation = value;
        }

        /// <summary>
        /// Initializes the cursor position to the center of the current map view or camera position.
        /// </summary>
        public static void Initialize(Map map)
        {
            if (map == null)
            {
                currentCursorPosition = IntVec3.Invalid;
                isInitialized = false;
                return;
            }

            // Start at the camera's current position
            if (Find.CameraDriver != null)
            {
                currentCursorPosition = Find.CameraDriver.MapPosition;
            }
            else
            {
                // Fallback to map center if camera driver not available
                currentCursorPosition = map.Center;
            }

            lastAnnouncedInfo = "";
            isInitialized = true;
        }

        /// <summary>
        /// Moves the cursor position by the given offset, ensuring it stays within map bounds.
        /// Returns true if the position changed.
        /// </summary>
        public static bool MoveCursor(IntVec3 offset, Map map)
        {
            if (map == null || !isInitialized)
                return false;

            IntVec3 newPosition = currentCursorPosition + offset;

            // Clamp to map bounds
            newPosition.x = UnityEngine.Mathf.Clamp(newPosition.x, 0, map.Size.x - 1);
            newPosition.z = UnityEngine.Mathf.Clamp(newPosition.z, 0, map.Size.z - 1);

            if (newPosition != currentCursorPosition)
            {
                currentCursorPosition = newPosition;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets the navigation state (useful when changing maps or returning to main menu).
        /// </summary>
        public static void Reset()
        {
            currentCursorPosition = IntVec3.Invalid;
            lastAnnouncedInfo = "";
            isInitialized = false;
        }

        /// <summary>
        /// Jumps to the next tile with a different terrain type in the specified direction.
        /// Returns true if the position changed.
        /// </summary>
        public static bool JumpToNextTerrainType(IntVec3 direction, Map map)
        {
            if (map == null || !isInitialized)
                return false;

            // Get the current terrain at the cursor position
            TerrainDef currentTerrain = currentCursorPosition.GetTerrain(map);
            if (currentTerrain == null)
                return false;

            IntVec3 searchPosition = currentCursorPosition;

            // Search in the specified direction until we find a different terrain type
            // Limit search to prevent infinite loops
            int maxSteps = UnityEngine.Mathf.Max(map.Size.x, map.Size.z);

            for (int step = 0; step < maxSteps; step++)
            {
                // Move one step in the direction
                searchPosition += direction;

                // Check if we're still within map bounds
                if (!searchPosition.InBounds(map))
                {
                    // Hit map boundary, clamp to edge and stop
                    searchPosition.x = UnityEngine.Mathf.Clamp(searchPosition.x, 0, map.Size.x - 1);
                    searchPosition.z = UnityEngine.Mathf.Clamp(searchPosition.z, 0, map.Size.z - 1);
                    break;
                }

                // Check if this tile has a different terrain type
                TerrainDef searchTerrain = searchPosition.GetTerrain(map);
                if (searchTerrain != null && searchTerrain != currentTerrain)
                {
                    // Found a different terrain type, stop searching
                    break;
                }
            }

            // Update position if we moved
            if (searchPosition != currentCursorPosition)
            {
                currentCursorPosition = searchPosition;
                return true;
            }

            return false;
        }
    }
}
