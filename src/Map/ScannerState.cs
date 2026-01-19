using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    public static class ScannerState
    {
        private static List<ScannerCategory> categories = new List<ScannerCategory>();
        private static int currentCategoryIndex = 0;
        private static int currentSubcategoryIndex = 0;
        private static int currentItemIndex = 0;
        private static int currentBulkIndex = 0; // Index within a bulk group
        private static bool autoJumpMode = false; // Auto-jump to items when navigating

        // Saved focus state for temporary category operations
        private static int savedCategoryIndex = -1;
        private static int savedSubcategoryIndex = -1;
        private static int savedItemIndex = -1;
        private static int savedBulkIndex = -1;

        // Temporary category tracking
        private static ScannerCategory temporaryCategory = null;

        /// <summary>
        /// Toggles auto-jump mode on/off (Alt+Home).
        /// When enabled, cursor automatically jumps to items as you navigate.
        /// </summary>
        public static void ToggleAutoJumpMode()
        {
            autoJumpMode = !autoJumpMode;
            string status = autoJumpMode ? "enabled" : "disabled";
            TolkHelper.Speak($"Auto-jump mode {status}", SpeechPriority.High);
        }

        /// <summary>
        /// Saves the current scanner focus state for later restoration.
        /// Used when temporarily switching to a different category (e.g., viewing obstacles).
        /// </summary>
        public static void SaveFocus()
        {
            savedCategoryIndex = currentCategoryIndex;
            savedSubcategoryIndex = currentSubcategoryIndex;
            savedItemIndex = currentItemIndex;
            savedBulkIndex = currentBulkIndex;
        }

        /// <summary>
        /// Restores the previously saved scanner focus state.
        /// Call this after removing a temporary category to return to the previous position.
        /// </summary>
        public static void RestoreFocus()
        {
            if (savedCategoryIndex >= 0)
            {
                currentCategoryIndex = savedCategoryIndex;
                currentSubcategoryIndex = savedSubcategoryIndex;
                currentItemIndex = savedItemIndex;
                currentBulkIndex = savedBulkIndex;

                // Reset saved state
                savedCategoryIndex = -1;
                savedSubcategoryIndex = -1;
                savedItemIndex = -1;
                savedBulkIndex = -1;

                // Validate indices in case the category list changed
                ValidateIndices();
            }
        }

        /// <summary>
        /// Creates a temporary category with the given name and items, and selects it.
        /// Only one temporary category can exist at a time.
        /// The temporary category is a proper scanner category that:
        /// - Appears in the category cycle (Ctrl+PageUp/Down)
        /// - Is preserved across RefreshItems() calls
        /// - Is cleared when Invalidate() is called (e.g., map switch)
        /// </summary>
        /// <param name="name">The name for the temporary category</param>
        /// <param name="items">The items to include in the category</param>
        public static void CreateTemporaryCategory(string name, List<ScannerItem> items)
        {
            // Remove any existing temporary category first
            RemoveTemporaryCategory();

            // Create the new temporary category with a single subcategory
            temporaryCategory = new ScannerCategory(name);
            var subcategory = new ScannerSubcategory($"{name}-All");
            subcategory.Items.AddRange(items);
            temporaryCategory.Subcategories.Add(subcategory);

            // Add to the categories list
            categories.Add(temporaryCategory);

            // Select the temporary category
            currentCategoryIndex = categories.Count - 1;
            currentSubcategoryIndex = 0;
            currentItemIndex = 0;
            currentBulkIndex = 0;
        }

        /// <summary>
        /// Removes the temporary category if one exists.
        /// Call RestoreFocus() after this to return to the previous scanner position.
        /// </summary>
        public static void RemoveTemporaryCategory()
        {
            if (temporaryCategory != null)
            {
                categories.Remove(temporaryCategory);
                temporaryCategory = null;
            }
        }

        /// <summary>
        /// Recalculates distances for all items from the current cursor position.
        /// Does NOT re-sort items or refresh the list from the map.
        /// </summary>
        private static void RecalculateDistances()
        {
            if (!MapNavigationState.IsInitialized)
                return;

            var cursorPos = MapNavigationState.CurrentCursorPosition;

            foreach (var category in categories)
            {
                foreach (var subcat in category.Subcategories)
                {
                    foreach (var item in subcat.Items)
                    {
                        // Recalculate distance for the primary position
                        if (item.IsTerrain)
                        {
                            item.Distance = (item.Position - cursorPos).LengthHorizontal;
                        }
                        else if (item.Thing != null)
                        {
                            item.Distance = (item.Thing.Position - cursorPos).LengthHorizontal;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Invalidates the scanner cache, forcing a refresh on next access.
        /// Call this when switching maps or when map contents have significantly changed.
        /// Also clears any temporary category since it's no longer valid.
        /// </summary>
        public static void Invalidate()
        {
            categories.Clear();
            temporaryCategory = null;
            currentCategoryIndex = 0;
            currentSubcategoryIndex = 0;
            currentItemIndex = 0;
            currentBulkIndex = 0;
        }

        /// <summary>
        /// Refreshes the scanner item list based on current cursor position.
        /// Called automatically by navigation methods.
        /// Preserves any temporary category that may be active.
        /// </summary>
        private static void RefreshItems()
        {
            if (!MapNavigationState.IsInitialized)
            {
                TolkHelper.Speak("Map navigation not initialized", SpeechPriority.High);
                return;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                TolkHelper.Speak("No active map", SpeechPriority.High);
                return;
            }

            // Save temporary category before refresh
            var savedTemporaryCategory = temporaryCategory;

            // Collect items
            var cursorPos = MapNavigationState.CurrentCursorPosition;
            categories = ScannerHelper.CollectMapItems(map, cursorPos);

            // Re-add temporary category if it existed
            if (savedTemporaryCategory != null)
            {
                temporaryCategory = savedTemporaryCategory;
                categories.Add(temporaryCategory);
            }

            if (categories.Count == 0)
            {
                TolkHelper.Speak("No items found on map", SpeechPriority.High);
                return;
            }

            // Validate and adjust indices if needed
            ValidateIndices();
        }

        public static void NextItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentSubcat = GetCurrentSubcategory();
            if (currentSubcat == null || currentSubcat.Items.Count == 0) return;

            currentItemIndex++;
            if (currentItemIndex >= currentSubcat.Items.Count)
            {
                currentItemIndex = 0; // Wrap to first item
            }

            currentBulkIndex = 0; // Reset bulk index when changing items

            // Recalculate distances from current cursor position
            RecalculateDistances();

            // Auto-jump if enabled
            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentItem();
            }
        }

        public static void PreviousItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentSubcat = GetCurrentSubcategory();
            if (currentSubcat == null || currentSubcat.Items.Count == 0) return;

            currentItemIndex--;
            if (currentItemIndex < 0)
            {
                currentItemIndex = currentSubcat.Items.Count - 1; // Wrap to last item
            }

            currentBulkIndex = 0; // Reset bulk index when changing items

            // Recalculate distances from current cursor position
            RecalculateDistances();

            // Auto-jump if enabled
            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentItem();
            }
        }

        public static void NextBulkItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentItem = GetCurrentItem();
            if (currentItem == null || !currentItem.IsBulkGroup) return;

            currentBulkIndex++;
            if (currentBulkIndex >= currentItem.BulkCount)
            {
                currentBulkIndex = 0; // Wrap to first bulk item
            }

            // Recalculate distances from current cursor position
            RecalculateDistances();

            // Auto-jump if enabled
            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentBulkItem();
            }
        }

        public static void PreviousBulkItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentItem = GetCurrentItem();
            if (currentItem == null || !currentItem.IsBulkGroup) return;

            currentBulkIndex--;
            if (currentBulkIndex < 0)
            {
                currentBulkIndex = currentItem.BulkCount - 1; // Wrap to last bulk item
            }

            // Recalculate distances from current cursor position
            RecalculateDistances();

            // Auto-jump if enabled
            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentBulkItem();
            }
        }

        public static void NextCategory()
        {
            if (WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            currentCategoryIndex++;
            if (currentCategoryIndex >= categories.Count)
            {
                currentCategoryIndex = 0; // Wrap to first category
            }

            // Reset subcategory and item indices
            currentSubcategoryIndex = 0;
            currentItemIndex = 0;

            // Skip empty subcategories
            SkipEmptySubcategories(forward: true);

            AnnounceCurrentCategory();
            AnnounceCurrentItem();
        }

        public static void PreviousCategory()
        {
            if (WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            currentCategoryIndex--;
            if (currentCategoryIndex < 0)
            {
                currentCategoryIndex = categories.Count - 1; // Wrap to last category
            }

            // Reset subcategory and item indices
            currentSubcategoryIndex = 0;
            currentItemIndex = 0;

            // Skip empty subcategories
            SkipEmptySubcategories(forward: true);

            AnnounceCurrentCategory();
            AnnounceCurrentItem();
        }

        public static void NextSubcategory()
        {
            if (WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            var currentCategory = GetCurrentCategory();
            if (currentCategory == null) return;

            int startIndex = currentSubcategoryIndex;
            do
            {
                currentSubcategoryIndex++;
                if (currentSubcategoryIndex >= currentCategory.Subcategories.Count)
                {
                    currentSubcategoryIndex = 0; // Wrap to first subcategory
                }

                // Break if we've cycled through all subcategories
                if (currentSubcategoryIndex == startIndex)
                    break;

            } while (GetCurrentSubcategory()?.IsEmpty ?? true);

            // Reset item index
            currentItemIndex = 0;

            AnnounceCurrentSubcategory();
            AnnounceCurrentItem();
        }

        public static void PreviousSubcategory()
        {
            if (WorldNavigationState.IsActive) return;

            RefreshItems();
            if (categories.Count == 0) return;

            var currentCategory = GetCurrentCategory();
            if (currentCategory == null) return;

            int startIndex = currentSubcategoryIndex;
            do
            {
                currentSubcategoryIndex--;
                if (currentSubcategoryIndex < 0)
                {
                    currentSubcategoryIndex = currentCategory.Subcategories.Count - 1; // Wrap to last subcategory
                }

                // Break if we've cycled through all subcategories
                if (currentSubcategoryIndex == startIndex)
                    break;

            } while (GetCurrentSubcategory()?.IsEmpty ?? true);

            // Reset item index
            currentItemIndex = 0;

            AnnounceCurrentSubcategory();
            AnnounceCurrentItem();
        }

        public static void JumpToCurrent()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentItem = GetCurrentItem();
            if (currentItem == null)
            {
                TolkHelper.Speak("No item selected", SpeechPriority.High);
                return;
            }

            IntVec3 targetPosition;

            if (currentItem.IsTerrain)
            {
                // For terrain regions, jump to the region center
                if (currentItem.HasTerrainRegions && currentBulkIndex < currentItem.TerrainRegions.Count)
                {
                    targetPosition = currentItem.TerrainRegions[currentBulkIndex].CenterPosition;
                }
                // Legacy: bulk terrain positions
                else if (currentItem.BulkTerrainPositions != null && currentBulkIndex < currentItem.BulkTerrainPositions.Count)
                {
                    targetPosition = currentItem.BulkTerrainPositions[currentBulkIndex];
                }
                else
                {
                    targetPosition = currentItem.Position;
                }
            }
            else if (currentItem.IsDesignation)
            {
                // For designations, check if we're navigating bulk designations
                if (currentItem.BulkDesignations != null && currentBulkIndex < currentItem.BulkDesignations.Count)
                {
                    targetPosition = currentItem.BulkDesignations[currentBulkIndex].target.Cell;
                }
                else
                {
                    targetPosition = currentItem.Position;
                }
            }
            else if (currentItem.IsZone)
            {
                // For zones, use the calculated center position
                targetPosition = currentItem.Position;
            }
            else if (currentItem.IsRoom)
            {
                // For rooms, use the calculated center position
                targetPosition = currentItem.Position;
            }
            else
            {
                // Get the actual thing to jump to (considering bulk index)
                Thing targetThing = currentItem.Thing;
                if (currentItem.IsBulkGroup && currentBulkIndex < currentItem.BulkCount)
                {
                    targetThing = currentItem.BulkThings[currentBulkIndex];
                }
                targetPosition = targetThing.Position;
            }

            // Update map cursor position
            MapNavigationState.CurrentCursorPosition = targetPosition;

            // Jump camera to position
            Find.CameraDriver.JumpToCurrentMapLoc(targetPosition);

            // Announce the item being jumped to
            if (autoJumpMode)
            {
                // In auto-jump mode, announce item details
                if (currentItem.IsBulkGroup)
                {
                    AnnounceCurrentBulkItem();
                }
                else
                {
                    AnnounceCurrentItem();
                }
            }
            else
            {
                // Manual jump (Home key) - just announce the jump
                TolkHelper.Speak($"Jumped to {currentItem.Label}", SpeechPriority.Normal);
            }
        }

        /// <summary>
        /// Gets the position of the currently selected item in the scanner.
        /// Used by visual preview patches to highlight the current item.
        /// Returns IntVec3.Invalid if no item is selected.
        /// </summary>
        public static IntVec3 GetCurrentItemPosition()
        {
            var currentItem = GetCurrentItem();
            if (currentItem == null)
                return IntVec3.Invalid;

            return currentItem.Position;
        }

        /// <summary>
        /// Checks if the scanner is currently focused on a temporary category.
        /// </summary>
        public static bool IsInTemporaryCategory()
        {
            return temporaryCategory != null && GetCurrentCategory() == temporaryCategory;
        }

        public static void ReadDistanceAndDirection()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentItem = GetCurrentItem();
            if (currentItem == null)
            {
                TolkHelper.Speak("No item selected", SpeechPriority.High);
                return;
            }

            // Recalculate distances from current cursor position
            RecalculateDistances();

            IntVec3 targetPos;

            if (currentItem.IsTerrain)
            {
                // For terrain regions, use region center
                if (currentItem.HasTerrainRegions && currentBulkIndex < currentItem.TerrainRegions.Count)
                {
                    targetPos = currentItem.TerrainRegions[currentBulkIndex].CenterPosition;
                }
                // Legacy: bulk terrain positions
                else if (currentItem.BulkTerrainPositions != null && currentBulkIndex < currentItem.BulkTerrainPositions.Count)
                {
                    targetPos = currentItem.BulkTerrainPositions[currentBulkIndex];
                }
                else
                {
                    targetPos = currentItem.Position;
                }
            }
            else
            {
                // Get the actual thing (considering bulk index)
                targetPos = currentItem.Position;
                if (currentItem.IsBulkGroup && currentBulkIndex < currentItem.BulkCount)
                {
                    if (currentItem.BulkThings != null && currentBulkIndex < currentItem.BulkThings.Count)
                    {
                        Thing targetThing = currentItem.BulkThings[currentBulkIndex];
                        targetPos = targetThing.Position;
                    }
                }
            }

            var cursorPos = MapNavigationState.CurrentCursorPosition;
            var distance = (targetPos - cursorPos).LengthHorizontal;
            var direction = currentItem.GetDirectionFrom(cursorPos);

            TolkHelper.Speak($"{distance:F1} tiles, {direction}", SpeechPriority.Normal);
        }

        private static ScannerCategory GetCurrentCategory()
        {
            if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
                return null;

            return categories[currentCategoryIndex];
        }

        private static ScannerSubcategory GetCurrentSubcategory()
        {
            var category = GetCurrentCategory();
            if (category == null) return null;

            if (currentSubcategoryIndex < 0 || currentSubcategoryIndex >= category.Subcategories.Count)
                return null;

            return category.Subcategories[currentSubcategoryIndex];
        }

        private static ScannerItem GetCurrentItem()
        {
            var subcat = GetCurrentSubcategory();
            if (subcat == null) return null;

            if (currentItemIndex < 0 || currentItemIndex >= subcat.Items.Count)
                return null;

            return subcat.Items[currentItemIndex];
        }

        private static void ValidateIndices()
        {
            // Ensure category index is valid
            if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
            {
                currentCategoryIndex = 0;
            }

            // Ensure subcategory index is valid and not empty
            var category = GetCurrentCategory();
            if (category != null)
            {
                if (currentSubcategoryIndex < 0 || currentSubcategoryIndex >= category.Subcategories.Count)
                {
                    currentSubcategoryIndex = 0;
                }

                // Skip to first non-empty subcategory
                SkipEmptySubcategories(forward: true);
            }

            // Ensure item index is valid
            var subcat = GetCurrentSubcategory();
            if (subcat != null)
            {
                if (currentItemIndex < 0 || currentItemIndex >= subcat.Items.Count)
                {
                    currentItemIndex = 0;
                }
            }
        }

        private static void SkipEmptySubcategories(bool forward)
        {
            var category = GetCurrentCategory();
            if (category == null) return;

            int startIndex = currentSubcategoryIndex;
            int attempts = 0;
            int maxAttempts = category.Subcategories.Count;

            while ((GetCurrentSubcategory()?.IsEmpty ?? true) && attempts < maxAttempts)
            {
                if (forward)
                {
                    currentSubcategoryIndex++;
                    if (currentSubcategoryIndex >= category.Subcategories.Count)
                    {
                        currentSubcategoryIndex = 0;
                    }
                }
                else
                {
                    currentSubcategoryIndex--;
                    if (currentSubcategoryIndex < 0)
                    {
                        currentSubcategoryIndex = category.Subcategories.Count - 1;
                    }
                }

                attempts++;
            }

            // If all subcategories are empty, reset to start
            if (GetCurrentSubcategory()?.IsEmpty ?? true)
            {
                currentSubcategoryIndex = startIndex;
            }
        }

        private static void AnnounceCurrentCategory()
        {
            var category = GetCurrentCategory();
            if (category == null) return;

            TolkHelper.Speak($"{category.Name} - {category.TotalItemCount} items", SpeechPriority.Normal);
        }

        private static void AnnounceCurrentSubcategory()
        {
            var subcat = GetCurrentSubcategory();
            if (subcat == null) return;

            TolkHelper.Speak($"{subcat.Name} - {subcat.Items.Count} items", SpeechPriority.Normal);
        }

        private static void AnnounceCurrentItem()
        {
            var item = GetCurrentItem();
            if (item == null)
            {
                TolkHelper.Speak("No items in this category", SpeechPriority.Normal);
                return;
            }

            // Special handling for terrain with regions
            if (item.HasTerrainRegions)
            {
                var region = item.TerrainRegions[currentBulkIndex];
                var cursorPos = MapNavigationState.CurrentCursorPosition;
                var distance = (region.CenterPosition - cursorPos).LengthHorizontal;

                string announcement = $"{item.Label}: {region.SizeDescription}, {distance:F1} tiles away";

                if (item.RegionCount > 1)
                {
                    int position = currentBulkIndex + 1;
                    announcement += $", region {position} of {item.RegionCount}";
                }

                // Show total tile count across all regions
                announcement += $", {item.TotalTileCount} tiles total";

                TolkHelper.Speak(announcement, SpeechPriority.Normal);
                return;
            }

            // Build announcement without position info
            string basicAnnouncement = $"{item.Label} - {item.Distance:F1} tiles away";

            // Add bulk count if this is a grouped item
            if (item.IsBulkGroup)
            {
                int position = currentBulkIndex + 1;
                basicAnnouncement += $", {position} of {item.BulkCount}";
            }

            TolkHelper.Speak(basicAnnouncement, SpeechPriority.Normal);
        }

        private static void AnnounceCurrentBulkItem()
        {
            var item = GetCurrentItem();
            if (item == null || !item.IsBulkGroup)
                return;

            if (currentBulkIndex < 0 || currentBulkIndex >= item.BulkCount)
                return;

            // For terrain regions (adjacency-grouped)
            if (item.HasTerrainRegions)
            {
                if (currentBulkIndex >= item.TerrainRegions.Count)
                    return;

                var region = item.TerrainRegions[currentBulkIndex];
                var regionCursorPos = MapNavigationState.CurrentCursorPosition;
                var regionDistance = (region.CenterPosition - regionCursorPos).LengthHorizontal;
                int regionPosition = currentBulkIndex + 1;

                string announcement = $"{item.Label}: {region.SizeDescription}, {regionDistance:F1} tiles away, region {regionPosition} of {item.RegionCount}";
                TolkHelper.Speak(announcement, SpeechPriority.Normal);
                return;
            }

            // For legacy terrain bulk groups (non-adjacent grouping)
            if (item.IsTerrain && item.BulkTerrainPositions != null)
            {
                var terrainPosition = currentBulkIndex + 1;
                TolkHelper.Speak($"{item.Label} - {terrainPosition} of {item.BulkCount}", SpeechPriority.Normal);
                return;
            }

            // For designation bulk groups
            if (item.IsDesignation)
            {
                if (item.BulkDesignations == null || currentBulkIndex >= item.BulkDesignations.Count)
                    return;

                var targetDesignation = item.BulkDesignations[currentBulkIndex];
                var desCursorPos = MapNavigationState.CurrentCursorPosition;
                var desDistance = (targetDesignation.target.Cell - desCursorPos).LengthHorizontal;
                var desPosition = currentBulkIndex + 1;

                // Build label from the specific designation target
                string designationLabel;
                if (targetDesignation.target.HasThing && targetDesignation.target.Thing != null)
                {
                    designationLabel = targetDesignation.target.Thing.LabelShort;
                }
                else
                {
                    // For cell-based designations, use the main item label
                    designationLabel = item.Label;
                }

                TolkHelper.Speak($"{designationLabel} - {desDistance:F1} tiles away, {desPosition} of {item.BulkCount}", SpeechPriority.Normal);
                return;
            }

            // For thing bulk groups, get label from the actual thing at this index
            if (item.BulkThings == null || currentBulkIndex >= item.BulkThings.Count)
                return;

            var targetThing = item.BulkThings[currentBulkIndex];
            if (targetThing == null)
                return;

            var cursorPos = MapNavigationState.CurrentCursorPosition;
            var distance = (targetThing.Position - cursorPos).LengthHorizontal;
            var position = currentBulkIndex + 1;

            // Build label from this specific thing, not the group label
            string thingLabel = targetThing.LabelShort ?? targetThing.def?.label ?? item.Label;

            TolkHelper.Speak($"{thingLabel} - {distance:F1} tiles away, {position} of {item.BulkCount}", SpeechPriority.Normal);
        }

        /// <summary>
        /// Jumps to the first item in the current subcategory.
        /// </summary>
        public static void JumpToFirstItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentSubcat = GetCurrentSubcategory();
            if (currentSubcat == null || currentSubcat.Items.Count == 0) return;

            currentItemIndex = 0;
            currentBulkIndex = 0;
            RecalculateDistances();

            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentItem();
            }
        }

        /// <summary>
        /// Jumps to the last item in the current subcategory.
        /// </summary>
        public static void JumpToLastItem()
        {
            if (WorldNavigationState.IsActive) return;

            // Initialize scanner if not already done
            if (categories.Count == 0)
            {
                RefreshItems();
                if (categories.Count == 0) return;
                AnnounceCurrentCategory();
            }

            var currentSubcat = GetCurrentSubcategory();
            if (currentSubcat == null || currentSubcat.Items.Count == 0) return;

            currentItemIndex = currentSubcat.Items.Count - 1;
            currentBulkIndex = 0;
            RecalculateDistances();

            if (autoJumpMode)
            {
                JumpToCurrent();
            }
            else
            {
                AnnounceCurrentItem();
            }
        }
    }
}
