using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Harmony patch to add Tab key for opening the accessible architect menu.
    /// Handles category selection, tool selection, and material selection.
    /// </summary>
    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class ArchitectMenuPatch
    {
        private static float lastArchitectKeyTime = 0f;
        private const float ArchitectKeyCooldown = 0.3f;

        /// <summary>
        /// Prefix patch to check for A key press at GUI event level.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static void Prefix()
        {
            // If any accessibility menu is active, don't intercept - let UnifiedKeyboardPatch handle it
            if (KeyboardHelper.IsAnyAccessibilityMenuActive())
                return;

            // Only process keyboard events
            if (Event.current.type != EventType.KeyDown)
                return;

            KeyCode key = Event.current.keyCode;

            // Only process Tab key for opening the architect menu
            if (key != KeyCode.Tab)
                return;

            // Cooldown to prevent accidental double-presses
            if (Time.time - lastArchitectKeyTime < ArchitectKeyCooldown)
                return;

            lastArchitectKeyTime = Time.time;

            // Only process during normal gameplay with a valid map
            if (Find.CurrentMap == null || !MapNavigationState.IsInitialized)
                return;

            // Don't process if any dialog or window that prevents camera motion is open
            if (Find.WindowStack != null && Find.WindowStack.WindowsPreventCameraMotion)
                return;

            // Don't process if already in zone creation mode
            if (ZoneCreationState.IsInCreationMode)
                return;

            // Don't process if windowless orders menu is active
            if (WindowlessFloatMenuState.IsActive)
                return;

            // Don't process if schedule window is active
            if (WindowlessScheduleState.IsActive)
                return;

            // If already in architect mode (but in placement), cancel back to menu
            if (ArchitectState.IsInPlacementMode)
            {
                ArchitectState.Cancel();
                Event.current.Use();
                return;
            }

            // If architect mode is active (in category/tool selection), close it
            if (ArchitectState.IsActive)
            {
                ArchitectState.Reset();
                TolkHelper.Speak("Architect menu closed");
                Event.current.Use();
                return;
            }

            // Open the architect category menu
            OpenCategoryMenu();

            // Consume the event
            Event.current.Use();
        }

        /// <summary>
        /// Opens the category selection menu.
        /// </summary>
        private static void OpenCategoryMenu()
        {
            // Get all visible categories
            List<DesignationCategoryDef> categories = ArchitectHelper.GetAllCategories();

            if (categories.Count == 0)
            {
                TolkHelper.Speak("No architect categories available");
                return;
            }

            // Create menu options
            List<FloatMenuOption> options = ArchitectHelper.CreateCategoryOptions(
                categories,
                OnCategorySelected
            );

            // Enter category selection mode
            ArchitectState.EnterCategorySelection();

            // Open the windowless menu
            WindowlessFloatMenuState.Open(options, false); // false = doesn't give colonist orders

            Log.Message("Opened architect category menu");
        }

        /// <summary>
        /// Called when a category is selected from the menu.
        /// </summary>
        private static void OnCategorySelected(DesignationCategoryDef category)
        {
            // Get all designators in this category
            List<Designator> designators = ArchitectHelper.GetDesignatorsForCategory(category);

            if (designators.Count == 0)
            {
                TolkHelper.Speak($"No tools available in {category.LabelCap}");
                ArchitectState.Reset();
                return;
            }

            // Enter tool selection mode
            ArchitectState.EnterToolSelection(category);

            // Create menu options for designators
            List<FloatMenuOption> options = ArchitectHelper.CreateDesignatorOptions(
                designators,
                OnDesignatorSelected
            );

            // Open the windowless menu
            WindowlessFloatMenuState.Open(options, false);

            Log.Message($"Opened tool menu for category: {category.defName}");
        }

        /// <summary>
        /// Called when a designator (tool) is selected from the menu.
        /// </summary>
        private static void OnDesignatorSelected(Designator designator)
        {
            // Check if this is a zone designator - show mode selection menu
            if (IsZoneDesignator(designator))
            {
                ShowZoneModeSelectionMenu(designator);
                return;
            }

            // Check if this is a build designator that needs material selection
            if (designator is Designator_Build buildDesignator)
            {
                BuildableDef buildable = buildDesignator.PlacingDef;

                if (ArchitectHelper.RequiresMaterialSelection(buildable))
                {
                    // Show material selection menu
                    ShowMaterialMenu(buildable, designator);
                    return;
                }
            }

            // No material selection needed - go straight to placement
            ArchitectState.EnterPlacementMode(designator);
        }

        /// <summary>
        /// Shows the material selection menu for a buildable.
        /// </summary>
        private static void ShowMaterialMenu(BuildableDef buildable, Designator originalDesignator)
        {
            // Create material options
            List<FloatMenuOption> options = ArchitectHelper.CreateMaterialOptions(
                buildable,
                (material) => OnMaterialSelected(buildable, material)
            );

            if (options.Count == 0)
            {
                TolkHelper.Speak($"No materials available for {buildable.label}");
                ArchitectState.Reset();
                return;
            }

            // Enter material selection mode
            ArchitectState.EnterMaterialSelection(buildable, originalDesignator);

            // Open the windowless menu
            WindowlessFloatMenuState.Open(options, false);

            Log.Message($"Opened material menu for: {buildable.defName}");
        }

        /// <summary>
        /// Called when a material is selected.
        /// Creates the build designator and enters placement mode.
        /// </summary>
        private static void OnMaterialSelected(BuildableDef buildable, ThingDef material)
        {
            // Create a build designator with the selected material
            Designator_Build designator = ArchitectHelper.CreateBuildDesignator(buildable, material);

            // Enter placement mode
            ArchitectState.EnterPlacementMode(designator, material);
        }

        /// <summary>
        /// Checks if a designator is a zone/area/cell-based designator.
        /// This includes zones (stockpiles, growing zones), areas (home, roof), and other multi-cell designators.
        /// Uses reflection to check the type hierarchy since we can't directly reference RimWorld types.
        /// </summary>
        private static bool IsZoneDesignator(Designator designator)
        {
            if (designator == null)
                return false;

            // Check if this designator's type hierarchy includes "Designator_Cells"
            // This covers all multi-cell designators: zones, areas, roofs, etc.
            System.Type type = designator.GetType();
            while (type != null)
            {
                if (type.Name == "Designator_Cells")
                    return true;
                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Shows the mode selection menu for zone creation (Manual, Borders, Corners).
        /// Stores the designator for later use.
        /// </summary>
        private static void ShowZoneModeSelectionMenu(Designator designator)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Manual selection mode
            options.Add(new FloatMenuOption("Manual selection", () =>
            {
                OnZoneModeSelected(designator, ZoneCreationMode.Manual);
            }));

            // Borders mode
            options.Add(new FloatMenuOption("Borders mode", () =>
            {
                OnZoneModeSelected(designator, ZoneCreationMode.Borders);
            }));

            // Corners mode
            options.Add(new FloatMenuOption("Corners mode", () =>
            {
                OnZoneModeSelected(designator, ZoneCreationMode.Corners);
            }));

            // Open the windowless menu
            WindowlessFloatMenuState.Open(options, false);

            string zoneName = designator.Label ?? "zone";
            TolkHelper.Speak($"Select creation mode for {zoneName}");
            Log.Message($"Opened zone mode selection menu for {zoneName}");
        }

        /// <summary>
        /// Called when a zone creation mode is selected.
        /// Enters placement mode with the designator and sets up ZoneCreationState.
        /// </summary>
        private static void OnZoneModeSelected(Designator designator, ZoneCreationMode mode)
        {
            // Enter architect placement mode with the zone designator
            ArchitectState.EnterPlacementMode(designator);

            // Also set the zone creation mode in ArchitectState for use during placement
            ArchitectState.SetZoneCreationMode(mode);

            Log.Message($"Zone creation mode selected: {mode} for designator {designator.Label}");
        }
    }
}
