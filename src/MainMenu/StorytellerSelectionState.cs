using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Navigation level for the storyteller selection.
    /// </summary>
    public enum StorytellerSelectionLevel
    {
        StorytellerList,      // Choosing a storyteller
        DifficultyList,       // Choosing difficulty preset
        CustomSectionList,    // Navigating custom difficulty sections
        CustomSettingsList    // Navigating settings within a section
    }

    /// <summary>
    /// Manages keyboard navigation for the storyteller selection page.
    /// Supports storyteller selection, difficulty presets, and custom difficulty settings.
    /// </summary>
    public static class StorytellerSelectionState
    {
        private static bool isActive = false;
        private static StorytellerSelectionLevel currentLevel = StorytellerSelectionLevel.StorytellerList;

        // Storyteller and difficulty lists
        private static int selectedStorytellerIndex = 0;
        private static int selectedDifficultyIndex = 0;
        private static List<StorytellerDef> storytellers = new List<StorytellerDef>();
        private static List<DifficultyDef> difficulties = new List<DifficultyDef>();

        // Custom difficulty navigation
        private static int selectedSectionIndex = 0;
        private static int selectedSettingIndex = 0;
        private static List<DifficultySection> sections = new List<DifficultySection>();

        // Typeahead search
        private static TypeaheadSearchHelper typeahead = new TypeaheadSearchHelper();

        public static bool IsActive => isActive;
        public static StorytellerSelectionLevel CurrentLevel => currentLevel;
        public static bool HasActiveSearch => typeahead.HasActiveSearch;

        // Helper to get current enabled settings for navigation
        private static List<DifficultySetting> GetEnabledSettings()
        {
            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return new List<DifficultySetting>();
            return sections[selectedSectionIndex].Settings.Where(s => s.IsEnabled).ToList();
        }

        // Map from enabled setting index to actual setting index
        private static int EnabledIndexToActualIndex(int enabledIndex)
        {
            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return 0;
            var allSettings = sections[selectedSectionIndex].Settings;
            int enabledCount = 0;
            for (int i = 0; i < allSettings.Count; i++)
            {
                if (allSettings[i].IsEnabled)
                {
                    if (enabledCount == enabledIndex)
                        return i;
                    enabledCount++;
                }
            }
            return 0;
        }

        // Map from actual setting index to enabled setting index
        private static int ActualIndexToEnabledIndex(int actualIndex)
        {
            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return 0;
            var allSettings = sections[selectedSectionIndex].Settings;
            int enabledCount = 0;
            for (int i = 0; i < actualIndex && i < allSettings.Count; i++)
            {
                if (allSettings[i].IsEnabled)
                    enabledCount++;
            }
            return enabledCount;
        }

        /// <summary>
        /// Opens the storyteller selection navigation.
        /// </summary>
        public static void Open()
        {
            isActive = true;
            currentLevel = StorytellerSelectionLevel.StorytellerList;
            typeahead.ClearSearch();

            // Get all visible storytellers
            storytellers = DefDatabase<StorytellerDef>.AllDefs
                .Where(st => st.listVisible)
                .OrderBy(st => st.listOrder)
                .ToList();

            // Get all difficulties
            difficulties = DefDatabase<DifficultyDef>.AllDefs.ToList();

            // Find current selections
            Storyteller current = Current.Game.storyteller;
            selectedStorytellerIndex = storytellers.IndexOf(current.def);
            if (selectedStorytellerIndex < 0) selectedStorytellerIndex = 0;

            selectedDifficultyIndex = difficulties.IndexOf(current.difficultyDef);
            if (selectedDifficultyIndex < 0) selectedDifficultyIndex = 0;

            // Build custom difficulty sections
            BuildCustomDifficultySections();

            AnnounceCurrentState();
        }

        /// <summary>
        /// Closes the storyteller selection.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentLevel = StorytellerSelectionLevel.StorytellerList;
            typeahead.ClearSearch();
        }

        /// <summary>
        /// Moves selection to next item.
        /// </summary>
        public static void SelectNext()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    selectedStorytellerIndex = MenuHelper.SelectNext(selectedStorytellerIndex, storytellers.Count);
                    ApplyStorytellerSelection();
                    break;

                case StorytellerSelectionLevel.DifficultyList:
                    selectedDifficultyIndex = MenuHelper.SelectNext(selectedDifficultyIndex, difficulties.Count);
                    ApplyDifficultySelection();
                    break;

                case StorytellerSelectionLevel.CustomSectionList:
                    selectedSectionIndex = MenuHelper.SelectNext(selectedSectionIndex, sections.Count);
                    break;

                case StorytellerSelectionLevel.CustomSettingsList:
                    {
                        var enabledSettings = GetEnabledSettings();
                        if (enabledSettings.Count > 0)
                        {
                            int enabledIndex = ActualIndexToEnabledIndex(selectedSettingIndex);
                            enabledIndex = MenuHelper.SelectNext(enabledIndex, enabledSettings.Count);
                            selectedSettingIndex = EnabledIndexToActualIndex(enabledIndex);
                        }
                    }
                    break;
            }

            AnnounceCurrentState();
        }

        /// <summary>
        /// Moves selection to previous item.
        /// </summary>
        public static void SelectPrevious()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    selectedStorytellerIndex = MenuHelper.SelectPrevious(selectedStorytellerIndex, storytellers.Count);
                    ApplyStorytellerSelection();
                    break;

                case StorytellerSelectionLevel.DifficultyList:
                    selectedDifficultyIndex = MenuHelper.SelectPrevious(selectedDifficultyIndex, difficulties.Count);
                    ApplyDifficultySelection();
                    break;

                case StorytellerSelectionLevel.CustomSectionList:
                    selectedSectionIndex = MenuHelper.SelectPrevious(selectedSectionIndex, sections.Count);
                    break;

                case StorytellerSelectionLevel.CustomSettingsList:
                    {
                        var enabledSettings = GetEnabledSettings();
                        if (enabledSettings.Count > 0)
                        {
                            int enabledIndex = ActualIndexToEnabledIndex(selectedSettingIndex);
                            enabledIndex = MenuHelper.SelectPrevious(enabledIndex, enabledSettings.Count);
                            selectedSettingIndex = EnabledIndexToActualIndex(enabledIndex);
                        }
                    }
                    break;
            }

            AnnounceCurrentState();
        }

        /// <summary>
        /// Switches between storyteller and difficulty selection (Tab key).
        /// Only works at top two levels.
        /// </summary>
        public static void SwitchLevel()
        {
            if (currentLevel == StorytellerSelectionLevel.StorytellerList)
            {
                currentLevel = StorytellerSelectionLevel.DifficultyList;
                typeahead.ClearSearch();
                AnnounceCurrentState();
            }
            else if (currentLevel == StorytellerSelectionLevel.DifficultyList)
            {
                currentLevel = StorytellerSelectionLevel.StorytellerList;
                typeahead.ClearSearch();
                AnnounceCurrentState();
            }
            // Tab does nothing in custom settings levels - use Escape to go back
        }

        /// <summary>
        /// Handles Enter key - confirms selection or enters deeper level.
        /// </summary>
        public static void ExecuteOrEnter()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                case StorytellerSelectionLevel.DifficultyList:
                    // Check if Custom difficulty is selected
                    if (currentLevel == StorytellerSelectionLevel.DifficultyList &&
                        selectedDifficultyIndex >= 0 && selectedDifficultyIndex < difficulties.Count &&
                        difficulties[selectedDifficultyIndex].isCustom)
                    {
                        // Enter custom settings
                        currentLevel = StorytellerSelectionLevel.CustomSectionList;
                        selectedSectionIndex = 0;
                        typeahead.ClearSearch();
                        TolkHelper.Speak("Custom difficulty settings. Use Up and Down to navigate sections, Enter to edit settings, Escape to go back.");
                        AnnounceCurrentState();
                    }
                    else
                    {
                        // Confirm and close
                        Confirm();
                    }
                    break;

                case StorytellerSelectionLevel.CustomSectionList:
                    // Enter the selected section
                    if (sections.Count > 0 && selectedSectionIndex < sections.Count)
                    {
                        var enabledSettings = GetEnabledSettings();
                        if (enabledSettings.Count > 0)
                        {
                            currentLevel = StorytellerSelectionLevel.CustomSettingsList;
                            selectedSettingIndex = EnabledIndexToActualIndex(0); // First enabled setting
                            typeahead.ClearSearch();
                            AnnounceCurrentState();
                        }
                        else
                        {
                            TolkHelper.Speak("No settings available in this section");
                        }
                    }
                    break;

                case StorytellerSelectionLevel.CustomSettingsList:
                    // Toggle checkbox settings
                    ToggleCurrentSetting();
                    break;
            }
        }

        /// <summary>
        /// Handles Escape key - goes back one level or closes.
        /// Returns true if handled, false if should close the dialog.
        /// </summary>
        public static bool GoBack()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.CustomSettingsList:
                    currentLevel = StorytellerSelectionLevel.CustomSectionList;
                    typeahead.ClearSearch();
                    AnnounceCurrentState();
                    return true;

                case StorytellerSelectionLevel.CustomSectionList:
                    currentLevel = StorytellerSelectionLevel.DifficultyList;
                    typeahead.ClearSearch();
                    AnnounceCurrentState();
                    return true;

                default:
                    // At top level - let caller close the dialog
                    return false;
            }
        }

        /// <summary>
        /// Adjusts the current setting value (Left/Right arrows).
        /// </summary>
        public static void AdjustCurrentSetting(int direction)
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return;

            var settings = sections[selectedSectionIndex].Settings;
            if (settings.Count == 0 || selectedSettingIndex >= settings.Count)
                return;

            var setting = settings[selectedSettingIndex];
            setting.Adjust(direction);
            // Use adjustment announcement (may be shorter than full announcement for some settings)
            TolkHelper.Speak(setting.GetAdjustmentAnnouncement());
        }

        /// <summary>
        /// Toggles the current checkbox setting (Space or Enter).
        /// </summary>
        public static void ToggleCurrentSetting()
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return;

            var settings = sections[selectedSectionIndex].Settings;
            if (settings.Count == 0 || selectedSettingIndex >= settings.Count)
                return;

            var setting = settings[selectedSettingIndex];
            setting.Toggle();
            AnnounceCurrentState();
        }

        /// <summary>
        /// Opens the reset to preset menu (Alt+R).
        /// Resets ALL custom difficulty settings to match a preset.
        /// </summary>
        public static void OpenResetToPresetMenu()
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSectionList &&
                currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            var options = new List<FloatMenuOption>();
            foreach (DifficultyDef def in DefDatabase<DifficultyDef>.AllDefs)
            {
                if (!def.isCustom)
                {
                    DifficultyDef localDef = def;
                    options.Add(new FloatMenuOption(def.LabelCap, () =>
                    {
                        Current.Game.storyteller.difficulty.CopyFrom(localDef);
                        TolkHelper.Speak($"All settings reset to {localDef.LabelCap}");
                        // Rebuild sections to reflect new values
                        BuildCustomDifficultySections();
                        AnnounceCurrentState();
                    }));
                }
            }

            // Use accessible WindowlessFloatMenuState instead of native FloatMenu
            WindowlessFloatMenuState.Open(options, colonistOrders: false);
            TolkHelper.Speak("Reset all settings to preset. Select a difficulty preset.");
        }

        /// <summary>
        /// Jumps to the first item in the current list (Home key).
        /// </summary>
        public static void JumpToFirst()
        {
            typeahead.ClearSearch();
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    selectedStorytellerIndex = 0;
                    ApplyStorytellerSelection();
                    break;
                case StorytellerSelectionLevel.DifficultyList:
                    selectedDifficultyIndex = 0;
                    ApplyDifficultySelection();
                    break;
                case StorytellerSelectionLevel.CustomSectionList:
                    selectedSectionIndex = 0;
                    break;
                case StorytellerSelectionLevel.CustomSettingsList:
                    var enabledSettings = GetEnabledSettings();
                    if (enabledSettings.Count > 0)
                    {
                        selectedSettingIndex = EnabledIndexToActualIndex(0);
                    }
                    break;
            }
            AnnounceCurrentState();
        }

        /// <summary>
        /// Jumps to the last item in the current list (End key).
        /// </summary>
        public static void JumpToLast()
        {
            typeahead.ClearSearch();
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    selectedStorytellerIndex = storytellers.Count - 1;
                    ApplyStorytellerSelection();
                    break;
                case StorytellerSelectionLevel.DifficultyList:
                    selectedDifficultyIndex = difficulties.Count - 1;
                    ApplyDifficultySelection();
                    break;
                case StorytellerSelectionLevel.CustomSectionList:
                    selectedSectionIndex = sections.Count - 1;
                    break;
                case StorytellerSelectionLevel.CustomSettingsList:
                    var enabledSettings = GetEnabledSettings();
                    if (enabledSettings.Count > 0)
                    {
                        selectedSettingIndex = EnabledIndexToActualIndex(enabledSettings.Count - 1);
                    }
                    break;
            }
            AnnounceCurrentState();
        }

        /// <summary>
        /// Adjusts slider by a percentage of its total possible positions.
        /// </summary>
        /// <param name="percent">Percentage of total positions to move (e.g., 0.1 = 10%, -0.25 = -25%)</param>
        public static void AdjustCurrentSettingByPercent(float percent)
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return;

            var settings = sections[selectedSectionIndex].Settings;
            if (settings.Count == 0 || selectedSettingIndex >= settings.Count)
                return;

            var setting = settings[selectedSettingIndex];
            if (setting is DifficultySliderSetting sliderSetting)
            {
                sliderSetting.AdjustByPercentOfPositions(percent);
                TolkHelper.Speak(setting.GetAdjustmentAnnouncement());
            }
            else
            {
                // For checkboxes, just toggle
                setting.Toggle();
                TolkHelper.Speak(setting.GetAdjustmentAnnouncement());
            }
        }

        /// <summary>
        /// Sets the current slider setting to its maximum value (Shift+Home).
        /// </summary>
        public static void SetCurrentSettingToMax()
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return;

            var settings = sections[selectedSectionIndex].Settings;
            if (settings.Count == 0 || selectedSettingIndex >= settings.Count)
                return;

            var setting = settings[selectedSettingIndex];
            if (setting is DifficultySliderSetting sliderSetting)
            {
                sliderSetting.SetToMax();
                TolkHelper.Speak(setting.GetAdjustmentAnnouncement());
            }
        }

        /// <summary>
        /// Sets the current slider setting to its minimum value (Shift+End).
        /// </summary>
        public static void SetCurrentSettingToMin()
        {
            if (currentLevel != StorytellerSelectionLevel.CustomSettingsList)
                return;

            if (sections.Count == 0 || selectedSectionIndex >= sections.Count)
                return;

            var settings = sections[selectedSectionIndex].Settings;
            if (settings.Count == 0 || selectedSettingIndex >= settings.Count)
                return;

            var setting = settings[selectedSettingIndex];
            if (setting is DifficultySliderSetting sliderSetting)
            {
                sliderSetting.SetToMin();
                TolkHelper.Speak(setting.GetAdjustmentAnnouncement());
            }
        }

        /// <summary>
        /// Clears the typeahead search.
        /// </summary>
        public static void ClearTypeaheadSearch()
        {
            typeahead.ClearSearchAndAnnounce();
        }

        /// <summary>
        /// Processes typeahead character input.
        /// </summary>
        public static void ProcessTypeaheadCharacter(char c)
        {
            var labels = GetCurrentLevelLabels();
            if (labels.Count == 0) return;

            if (typeahead.ProcessCharacterInput(c, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    SetCurrentIndex(newIndex);
                    AnnounceWithSearch();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{typeahead.LastFailedSearch}'");
            }
        }

        /// <summary>
        /// Processes backspace for typeahead search.
        /// </summary>
        public static void ProcessBackspace()
        {
            if (!typeahead.HasActiveSearch) return;

            var labels = GetCurrentLevelLabels();
            if (typeahead.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    SetCurrentIndex(newIndex);
                }
                AnnounceWithSearch();
            }
        }

        /// <summary>
        /// Selects next typeahead match.
        /// </summary>
        public static void SelectNextMatch()
        {
            int currentIndex = GetCurrentIndex();
            int newIndex = typeahead.GetNextMatch(currentIndex);
            if (newIndex >= 0)
            {
                SetCurrentIndex(newIndex);
                AnnounceWithSearch();
            }
        }

        /// <summary>
        /// Selects previous typeahead match.
        /// </summary>
        public static void SelectPreviousMatch()
        {
            int currentIndex = GetCurrentIndex();
            int newIndex = typeahead.GetPreviousMatch(currentIndex);
            if (newIndex >= 0)
            {
                SetCurrentIndex(newIndex);
                AnnounceWithSearch();
            }
        }

        /// <summary>
        /// Confirms selection and closes the page.
        /// </summary>
        public static void Confirm()
        {
            TolkHelper.Speak("Storyteller and difficulty confirmed. Closing selection.");
            Close();
            Find.WindowStack.TryRemove(typeof(Page_SelectStorytellerInGame));
        }

        // ===== Private Methods =====

        private static void ApplyStorytellerSelection()
        {
            if (selectedStorytellerIndex >= 0 && selectedStorytellerIndex < storytellers.Count)
            {
                Storyteller storyteller = Current.Game.storyteller;
                StorytellerDef oldDef = storyteller.def;
                storyteller.def = storytellers[selectedStorytellerIndex];

                if (storyteller.def != oldDef)
                {
                    storyteller.Notify_DefChanged();
                    TutorSystem.Notify_Event("ChooseStoryteller");
                }
            }
        }

        private static void ApplyDifficultySelection()
        {
            if (selectedDifficultyIndex >= 0 && selectedDifficultyIndex < difficulties.Count)
            {
                Storyteller storyteller = Current.Game.storyteller;
                DifficultyDef selectedDiff = difficulties[selectedDifficultyIndex];
                storyteller.difficultyDef = selectedDiff;

                // Copy difficulty values from the def (only for non-custom)
                if (!selectedDiff.isCustom)
                {
                    storyteller.difficulty.CopyFrom(selectedDiff);
                }
            }
        }

        private static void BuildCustomDifficultySections()
        {
            sections.Clear();
            // Capture the Storyteller reference, not the Difficulty object directly.
            // This ensures lambdas always access the current difficulty values
            // even if the difficulty object were to be replaced.
            Storyteller storyteller = Current.Game.storyteller;

            // ===== LEFT COLUMN SECTIONS (from DrawCustomLeft) =====

            // Threats Section
            var threats = new DifficultySection("DifficultyThreatSection".Translate());
            threats.Settings.Add(new DifficultySliderSetting("threatScale", () => storyteller.difficulty.threatScale, v => storyteller.difficulty.threatScale = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            threats.Settings.Add(new DifficultyCheckboxSetting("allowBigThreats", () => storyteller.difficulty.allowBigThreats, v => storyteller.difficulty.allowBigThreats = v));
            threats.Settings.Add(new DifficultyCheckboxSetting("allowViolentQuests", () => storyteller.difficulty.allowViolentQuests, v => storyteller.difficulty.allowViolentQuests = v));
            threats.Settings.Add(new DifficultyCheckboxSetting("allowIntroThreats", () => storyteller.difficulty.allowIntroThreats, v => storyteller.difficulty.allowIntroThreats = v));
            threats.Settings.Add(new DifficultyCheckboxSetting("predatorsHuntHumanlikes", () => storyteller.difficulty.predatorsHuntHumanlikes, v => storyteller.difficulty.predatorsHuntHumanlikes = v));
            threats.Settings.Add(new DifficultyCheckboxSetting("allowExtremeWeatherIncidents", () => storyteller.difficulty.allowExtremeWeatherIncidents, v => storyteller.difficulty.allowExtremeWeatherIncidents = v));
            if (ModsConfig.BiotechActive)
            {
                threats.Settings.Add(new DifficultySliderSetting("wastepackInfestationChanceFactor", () => storyteller.difficulty.wastepackInfestationChanceFactor, v => storyteller.difficulty.wastepackInfestationChanceFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            }
            sections.Add(threats);

            // Economy Section
            var economy = new DifficultySection("DifficultyEconomySection".Translate());
            economy.Settings.Add(new DifficultySliderSetting("cropYieldFactor", () => storyteller.difficulty.cropYieldFactor, v => storyteller.difficulty.cropYieldFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("mineYieldFactor", () => storyteller.difficulty.mineYieldFactor, v => storyteller.difficulty.mineYieldFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("butcherYieldFactor", () => storyteller.difficulty.butcherYieldFactor, v => storyteller.difficulty.butcherYieldFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            if (ModsConfig.IsActive("ludeon.rimworld.odyssey"))
            {
                economy.Settings.Add(new DifficultySliderSetting("fishingYieldFactor", () => storyteller.difficulty.fishingYieldFactor, v => storyteller.difficulty.fishingYieldFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            }
            economy.Settings.Add(new DifficultySliderSetting("researchSpeedFactor", () => storyteller.difficulty.researchSpeedFactor, v => storyteller.difficulty.researchSpeedFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("questRewardValueFactor", () => storyteller.difficulty.questRewardValueFactor, v => storyteller.difficulty.questRewardValueFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("raidLootPointsFactor", () => storyteller.difficulty.raidLootPointsFactor, v => storyteller.difficulty.raidLootPointsFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("tradePriceFactorLoss", () => storyteller.difficulty.tradePriceFactorLoss, v => storyteller.difficulty.tradePriceFactorLoss = v, 0f, 0.5f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("maintenanceCostFactor", () => storyteller.difficulty.maintenanceCostFactor, v => storyteller.difficulty.maintenanceCostFactor = v, 0.01f, 1f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("scariaRotChance", () => storyteller.difficulty.scariaRotChance, v => storyteller.difficulty.scariaRotChance = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("enemyDeathOnDownedChanceFactor", () => storyteller.difficulty.enemyDeathOnDownedChanceFactor, v => storyteller.difficulty.enemyDeathOnDownedChanceFactor = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            economy.Settings.Add(new DifficultySliderSetting("nomadicMineableResourcesFactor", () => storyteller.difficulty.nomadicMineableResourcesFactor, v => storyteller.difficulty.nomadicMineableResourcesFactor = v, 0f, 2f, 0.01f, ToStringStyle.PercentZero));
            sections.Add(economy);

            // Ideology Section (DLC)
            if (ModsConfig.IdeologyActive)
            {
                var ideology = new DifficultySection("DifficultyIdeologySection".Translate());
                ideology.Settings.Add(new DifficultySliderSetting("lowPopConversionBoost", () => storyteller.difficulty.lowPopConversionBoost, v => storyteller.difficulty.lowPopConversionBoost = v, 1f, 5f, 1f, ToStringStyle.Integer, ToStringNumberSense.Factor));
                sections.Add(ideology);
            }

            // Anomaly Section (DLC)
            if (ModsConfig.AnomalyActive)
            {
                var anomaly = new DifficultySection("DifficultyAnomalySection".Translate());

                // Playstyle selector - allows switching between Standard, Ambient Horror, etc.
                anomaly.Settings.Add(new AnomalyPlaystyleSetting(
                    () => storyteller.difficulty.AnomalyPlaystyleDef,
                    v => storyteller.difficulty.AnomalyPlaystyleDef = v,
                    () => BuildCustomDifficultySections())); // Rebuild sections when playstyle changes

                // Override threat fraction slider (shown when playstyle uses overrideThreatFraction)
                anomaly.Settings.Add(new DifficultySliderSetting(
                    "Difficulty_AnomalyThreats_Label".Translate(),
                    "Difficulty_AnomalyThreats_Info".Translate(),
                    () => storyteller.difficulty.overrideAnomalyThreatsFraction ?? 0.15f,
                    v => storyteller.difficulty.overrideAnomalyThreatsFraction = v,
                    0f, 1f, 0.01f, ToStringStyle.PercentZero,
                    enabledCondition: () => storyteller.difficulty.AnomalyPlaystyleDef?.overrideThreatFraction == true));

                // Separate threat fraction sliders (shown when playstyle uses displayThreatFractionSliders)
                anomaly.Settings.Add(new DifficultySliderSetting(
                    "Difficulty_AnomalyThreatsInactive_Label".Translate(),
                    "Difficulty_AnomalyThreatsInactive_Info".Translate(),
                    () => storyteller.difficulty.anomalyThreatsInactiveFraction,
                    v => storyteller.difficulty.anomalyThreatsInactiveFraction = v,
                    0f, 1f, 0.01f, ToStringStyle.PercentZero,
                    enabledCondition: () => storyteller.difficulty.AnomalyPlaystyleDef?.displayThreatFractionSliders == true &&
                                           storyteller.difficulty.AnomalyPlaystyleDef?.overrideThreatFraction != true));

                anomaly.Settings.Add(new DifficultySliderSetting(
                    "Difficulty_AnomalyThreatsActive_Label".Translate(),
                    "Difficulty_AnomalyThreatsActive_Info".Translate(
                        Mathf.Clamp01(storyteller.difficulty.anomalyThreatsActiveFraction).ToStringPercent(),
                        Mathf.Clamp01(storyteller.difficulty.anomalyThreatsActiveFraction * 1.5f).ToStringPercent()),
                    () => storyteller.difficulty.anomalyThreatsActiveFraction,
                    v => storyteller.difficulty.anomalyThreatsActiveFraction = v,
                    0.1f, 1f, 0.01f, ToStringStyle.PercentZero,
                    enabledCondition: () => storyteller.difficulty.AnomalyPlaystyleDef?.displayThreatFractionSliders == true &&
                                           storyteller.difficulty.AnomalyPlaystyleDef?.overrideThreatFraction != true));

                // Study efficiency slider (shown when playstyle uses displayStudyFactorSlider)
                anomaly.Settings.Add(new DifficultySliderSetting(
                    "Difficulty_StudyEfficiency_Label".Translate(),
                    "Difficulty_StudyEfficiency_Info".Translate(),
                    () => storyteller.difficulty.studyEfficiencyFactor,
                    v => storyteller.difficulty.studyEfficiencyFactor = v,
                    0f, 5f, 0.01f, ToStringStyle.PercentZero,
                    enabledCondition: () => storyteller.difficulty.AnomalyPlaystyleDef?.displayStudyFactorSlider == true));

                sections.Add(anomaly);
            }

            // Children Section (Biotech DLC)
            if (ModsConfig.BiotechActive)
            {
                var children = new DifficultySection("DifficultyChildrenSection".Translate());
                children.Settings.Add(new DifficultyCheckboxSetting("noBabiesOrChildren", () => storyteller.difficulty.noBabiesOrChildren, v => storyteller.difficulty.noBabiesOrChildren = v));
                children.Settings.Add(new DifficultyCheckboxSetting("babiesAreHealthy", () => storyteller.difficulty.babiesAreHealthy, v => storyteller.difficulty.babiesAreHealthy = v));
                // Conditional settings - only enabled if children are enabled
                children.Settings.Add(new DifficultyCheckboxSetting("childRaidersAllowed", () => storyteller.difficulty.childRaidersAllowed, v => storyteller.difficulty.childRaidersAllowed = v, () => !storyteller.difficulty.noBabiesOrChildren));
                if (ModsConfig.AnomalyActive)
                {
                    children.Settings.Add(new DifficultyCheckboxSetting("childShamblersAllowed", () => storyteller.difficulty.childShamblersAllowed, v => storyteller.difficulty.childShamblersAllowed = v, () => !storyteller.difficulty.noBabiesOrChildren));
                }
                children.Settings.Add(new DifficultySliderSetting("childAgingRate", () => storyteller.difficulty.childAgingRate, v => storyteller.difficulty.childAgingRate = v, 1f, 6f, 1f, ToStringStyle.Integer, ToStringNumberSense.Factor));
                children.Settings.Add(new DifficultySliderSetting("adultAgingRate", () => storyteller.difficulty.adultAgingRate, v => storyteller.difficulty.adultAgingRate = v, 1f, 6f, 1f, ToStringStyle.Integer, ToStringNumberSense.Factor));
                sections.Add(children);
            }

            // ===== RIGHT COLUMN SECTIONS (from DrawCustomRight) =====

            // General Section
            var general = new DifficultySection("DifficultyGeneralSection".Translate());
            general.Settings.Add(new DifficultySliderSetting("colonistMoodOffset", () => storyteller.difficulty.colonistMoodOffset, v => storyteller.difficulty.colonistMoodOffset = v, -20f, 20f, 1f, ToStringStyle.Integer, ToStringNumberSense.Offset));
            general.Settings.Add(new DifficultySliderSetting("foodPoisonChanceFactor", () => storyteller.difficulty.foodPoisonChanceFactor, v => storyteller.difficulty.foodPoisonChanceFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("manhunterChanceOnDamageFactor", () => storyteller.difficulty.manhunterChanceOnDamageFactor, v => storyteller.difficulty.manhunterChanceOnDamageFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("playerPawnInfectionChanceFactor", () => storyteller.difficulty.playerPawnInfectionChanceFactor, v => storyteller.difficulty.playerPawnInfectionChanceFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("diseaseIntervalFactor", () => storyteller.difficulty.diseaseIntervalFactor, v => storyteller.difficulty.diseaseIntervalFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero, ToStringNumberSense.Absolute, true, 100f));
            general.Settings.Add(new DifficultySliderSetting("enemyReproductionRateFactor", () => storyteller.difficulty.enemyReproductionRateFactor, v => storyteller.difficulty.enemyReproductionRateFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("deepDrillInfestationChanceFactor", () => storyteller.difficulty.deepDrillInfestationChanceFactor, v => storyteller.difficulty.deepDrillInfestationChanceFactor = v, 0f, 5f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("friendlyFireChanceFactor", () => storyteller.difficulty.friendlyFireChanceFactor, v => storyteller.difficulty.friendlyFireChanceFactor = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultySliderSetting("allowInstantKillChance", () => storyteller.difficulty.allowInstantKillChance, v => storyteller.difficulty.allowInstantKillChance = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            general.Settings.Add(new DifficultyCheckboxSetting("peacefulTemples", () => storyteller.difficulty.peacefulTemples, v => storyteller.difficulty.peacefulTemples = v, null, true));
            general.Settings.Add(new DifficultyCheckboxSetting("allowCaveHives", () => storyteller.difficulty.allowCaveHives, v => storyteller.difficulty.allowCaveHives = v));
            general.Settings.Add(new DifficultyCheckboxSetting("unwaveringPrisoners", () => storyteller.difficulty.unwaveringPrisoners, v => storyteller.difficulty.unwaveringPrisoners = v));
            sections.Add(general);

            // Player Tools Section
            var playerTools = new DifficultySection("DifficultyPlayerToolsSection".Translate());
            playerTools.Settings.Add(new DifficultyCheckboxSetting("allowTraps", () => storyteller.difficulty.allowTraps, v => storyteller.difficulty.allowTraps = v));
            playerTools.Settings.Add(new DifficultyCheckboxSetting("allowTurrets", () => storyteller.difficulty.allowTurrets, v => storyteller.difficulty.allowTurrets = v));
            playerTools.Settings.Add(new DifficultyCheckboxSetting("allowMortars", () => storyteller.difficulty.allowMortars, v => storyteller.difficulty.allowMortars = v));
            playerTools.Settings.Add(new DifficultyCheckboxSetting("classicMortars", () => storyteller.difficulty.classicMortars, v => storyteller.difficulty.classicMortars = v));
            sections.Add(playerTools);

            // Adaptation Section
            var adaptation = new DifficultySection("DifficultyAdaptationSection".Translate());
            adaptation.Settings.Add(new DifficultySliderSetting("adaptationGrowthRateFactorOverZero", () => storyteller.difficulty.adaptationGrowthRateFactorOverZero, v => storyteller.difficulty.adaptationGrowthRateFactorOverZero = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            adaptation.Settings.Add(new DifficultySliderSetting("adaptationEffectFactor", () => storyteller.difficulty.adaptationEffectFactor, v => storyteller.difficulty.adaptationEffectFactor = v, 0f, 1f, 0.01f, ToStringStyle.PercentZero));
            adaptation.Settings.Add(new DifficultyCheckboxSetting("fixedWealthMode", () => storyteller.difficulty.fixedWealthMode, v => storyteller.difficulty.fixedWealthMode = v));
            // fixedWealthTimeFactor is displayed as months (12 / factor) and only enabled when fixedWealthMode is true
            // Use Mathf.Max to prevent division by zero
            adaptation.Settings.Add(new DifficultySliderSetting(
                "fixedWealthTimeFactor",
                () => Mathf.Round(12f / Mathf.Max(0.01f, storyteller.difficulty.fixedWealthTimeFactor)),
                v => storyteller.difficulty.fixedWealthTimeFactor = 12f / Mathf.Max(1f, v),
                1f, 20f, 1f, ToStringStyle.Integer, ToStringNumberSense.Absolute, false, 0f,
                () => storyteller.difficulty.fixedWealthMode));
            sections.Add(adaptation);

            // Reset All Settings to Preset Section
            // This special section allows resetting all custom settings to match a difficulty preset
            var resetSection = new DifficultySection("Reset All Settings to Preset (Alt+R)", "presets");
            foreach (DifficultyDef def in DefDatabase<DifficultyDef>.AllDefs)
            {
                if (!def.isCustom)
                {
                    DifficultyDef localDef = def;
                    resetSection.Settings.Add(new DifficultyResetSetting(
                        localDef.LabelCap,
                        (localDef.description ?? "").StripTags(),
                        () =>
                        {
                            storyteller.difficulty.CopyFrom(localDef);
                            TolkHelper.Speak($"All settings reset to {localDef.LabelCap}");
                            // Rebuild sections to reflect new values
                            BuildCustomDifficultySections();
                            // Navigate back to section list
                            currentLevel = StorytellerSelectionLevel.CustomSectionList;
                            AnnounceCurrentState();
                        }));
                }
            }
            sections.Add(resetSection);
        }

        private static List<string> GetCurrentLevelLabels()
        {
            var labels = new List<string>();
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    foreach (var st in storytellers)
                        labels.Add(st.label);
                    break;
                case StorytellerSelectionLevel.DifficultyList:
                    foreach (var diff in difficulties)
                        labels.Add(diff.LabelCap);
                    break;
                case StorytellerSelectionLevel.CustomSectionList:
                    foreach (var section in sections)
                        labels.Add(section.Name);
                    break;
                case StorytellerSelectionLevel.CustomSettingsList:
                    if (sections.Count > 0 && selectedSectionIndex < sections.Count)
                    {
                        // Only include enabled settings to match navigation behavior
                        foreach (var setting in sections[selectedSectionIndex].Settings.Where(s => s.IsEnabled))
                            labels.Add(setting.Label);
                    }
                    break;
            }
            return labels;
        }

        private static int GetCurrentIndex()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    return selectedStorytellerIndex;
                case StorytellerSelectionLevel.DifficultyList:
                    return selectedDifficultyIndex;
                case StorytellerSelectionLevel.CustomSectionList:
                    return selectedSectionIndex;
                case StorytellerSelectionLevel.CustomSettingsList:
                    // Return enabled index for consistency with typeahead labels
                    return ActualIndexToEnabledIndex(selectedSettingIndex);
                default:
                    return 0;
            }
        }

        private static void SetCurrentIndex(int index)
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    selectedStorytellerIndex = index;
                    ApplyStorytellerSelection();
                    break;
                case StorytellerSelectionLevel.DifficultyList:
                    selectedDifficultyIndex = index;
                    ApplyDifficultySelection();
                    break;
                case StorytellerSelectionLevel.CustomSectionList:
                    selectedSectionIndex = index;
                    break;
                case StorytellerSelectionLevel.CustomSettingsList:
                    // Typeahead index is based on enabled settings only, convert to actual index
                    selectedSettingIndex = EnabledIndexToActualIndex(index);
                    break;
            }
        }

        private static void AnnounceWithSearch()
        {
            if (typeahead.HasActiveSearch)
            {
                string itemName = GetCurrentItemAnnouncement();
                TolkHelper.Speak($"{itemName}, {typeahead.CurrentMatchPosition} of {typeahead.MatchCount} matches for '{typeahead.SearchBuffer}'");
            }
            else
            {
                AnnounceCurrentState();
            }
        }

        private static string GetCurrentItemAnnouncement()
        {
            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    if (selectedStorytellerIndex >= 0 && selectedStorytellerIndex < storytellers.Count)
                    {
                        var st = storytellers[selectedStorytellerIndex];
                        return $"{st.label}. {st.description.StripTags()}";
                    }
                    break;
                case StorytellerSelectionLevel.DifficultyList:
                    if (selectedDifficultyIndex >= 0 && selectedDifficultyIndex < difficulties.Count)
                    {
                        var diff = difficulties[selectedDifficultyIndex];
                        string customHint = diff.isCustom ? " Press Enter to customize settings." : "";
                        return $"{diff.LabelCap}. {diff.description.StripTags()}{customHint}";
                    }
                    break;
                case StorytellerSelectionLevel.CustomSectionList:
                    if (selectedSectionIndex >= 0 && selectedSectionIndex < sections.Count)
                    {
                        var section = sections[selectedSectionIndex];
                        return $"{section.Name}. {section.Settings.Count} {section.ItemsLabel}";
                    }
                    break;
                case StorytellerSelectionLevel.CustomSettingsList:
                    if (sections.Count > 0 && selectedSectionIndex < sections.Count)
                    {
                        var settings = sections[selectedSectionIndex].Settings;
                        if (selectedSettingIndex >= 0 && selectedSettingIndex < settings.Count)
                        {
                            return settings[selectedSettingIndex].GetAnnouncement();
                        }
                    }
                    break;
            }
            return "";
        }

        private static void AnnounceCurrentState()
        {
            string announcement = GetCurrentItemAnnouncement();
            string position = "";

            switch (currentLevel)
            {
                case StorytellerSelectionLevel.StorytellerList:
                    position = MenuHelper.FormatPosition(selectedStorytellerIndex, storytellers.Count);
                    if (!string.IsNullOrEmpty(position))
                        announcement += $" {position}";
                    announcement += " Press Tab to switch to difficulty selection.";
                    break;

                case StorytellerSelectionLevel.DifficultyList:
                    position = MenuHelper.FormatPosition(selectedDifficultyIndex, difficulties.Count);
                    if (!string.IsNullOrEmpty(position))
                        announcement += $" {position}";
                    announcement += " Press Tab to switch to storyteller selection. Press Enter to confirm.";
                    break;

                case StorytellerSelectionLevel.CustomSectionList:
                    position = MenuHelper.FormatPosition(selectedSectionIndex, sections.Count);
                    if (!string.IsNullOrEmpty(position))
                        announcement += $" {position}";
                    break;

                case StorytellerSelectionLevel.CustomSettingsList:
                    if (sections.Count > 0 && selectedSectionIndex < sections.Count)
                    {
                        // Use enabled settings count for correct position announcement
                        var enabledSettings = GetEnabledSettings();
                        int enabledIndex = ActualIndexToEnabledIndex(selectedSettingIndex);
                        position = MenuHelper.FormatPosition(enabledIndex, enabledSettings.Count);
                        if (!string.IsNullOrEmpty(position))
                            announcement += $" {position}";
                    }
                    break;
            }

            TolkHelper.Speak(announcement);
        }

        // ===== Private Nested Classes =====

        /// <summary>
        /// Represents a section of difficulty settings (e.g., Threats, Economy).
        /// </summary>
        private class DifficultySection
        {
            public string Name { get; }
            public List<DifficultySetting> Settings { get; }
            public string ItemsLabel { get; }

            public DifficultySection(string name, string itemsLabel = "settings")
            {
                Name = name;
                Settings = new List<DifficultySetting>();
                ItemsLabel = itemsLabel;
            }
        }

        /// <summary>
        /// Base class for difficulty settings.
        /// </summary>
        private abstract class DifficultySetting
        {
            public string Label { get; protected set; }
            public string Tooltip { get; protected set; }
            protected Func<bool> enabledCondition;

            public bool IsEnabled => enabledCondition == null || enabledCondition();

            /// <summary>
            /// Gets the announcement when adjusting the setting (left/right arrows).
            /// Defaults to the full announcement, but can be overridden to be shorter.
            /// </summary>
            public virtual string GetAdjustmentAnnouncement() => GetAnnouncement();

            public abstract string GetAnnouncement();
            public abstract void Toggle();
            public abstract void Adjust(int direction);
        }

        /// <summary>
        /// Checkbox (boolean) difficulty setting.
        /// </summary>
        private class DifficultyCheckboxSetting : DifficultySetting
        {
            private readonly Func<bool> getter;
            private readonly Action<bool> setter;
            private readonly bool invert;

            public DifficultyCheckboxSetting(string optionName, Func<bool> getter, Action<bool> setter, Func<bool> enabledCondition = null, bool invert = false)
            {
                this.getter = getter;
                this.setter = setter;
                this.enabledCondition = enabledCondition;
                this.invert = invert;

                string invertSuffix = invert ? "_Inverted" : "";
                string capitalizedName = optionName.CapitalizeFirst();
                Label = $"Difficulty_{capitalizedName}{invertSuffix}_Label".Translate();
                Tooltip = $"Difficulty_{capitalizedName}{invertSuffix}_Info".Translate();
            }

            public override string GetAnnouncement()
            {
                if (!IsEnabled)
                {
                    return $"{Label}: Disabled";
                }

                bool displayValue = invert ? !getter() : getter();
                string valueStr = displayValue ? "On" : "Off";
                return $"{Label}. {valueStr}. {Tooltip}";
            }

            public override void Toggle()
            {
                if (!IsEnabled) return;
                bool current = getter();
                setter(!current);
            }

            public override void Adjust(int direction)
            {
                // Checkboxes toggle on left/right too
                Toggle();
            }
        }

        /// <summary>
        /// Slider (float) difficulty setting.
        /// </summary>
        private class DifficultySliderSetting : DifficultySetting
        {
            private readonly Func<float> getter;
            private readonly Action<float> setter;
            private readonly float min;
            private readonly float max;
            private readonly float step;
            private readonly ToStringStyle style;
            private readonly ToStringNumberSense numberSense;
            private readonly bool reciprocate;
            private readonly float reciprocalCutoff;

            public DifficultySliderSetting(string optionName, Func<float> getter, Action<float> setter,
                float min, float max, float step, ToStringStyle style,
                ToStringNumberSense numberSense = ToStringNumberSense.Absolute,
                bool reciprocate = false, float reciprocalCutoff = 1000f,
                Func<bool> enabledCondition = null)
            {
                this.getter = getter;
                this.setter = setter;
                this.min = min;
                this.max = max;
                this.step = step;
                this.style = style;
                this.numberSense = numberSense;
                this.reciprocate = reciprocate;
                this.reciprocalCutoff = reciprocalCutoff;
                this.enabledCondition = enabledCondition;

                string invertSuffix = reciprocate ? "_Inverted" : "";
                string capitalizedName = optionName.CapitalizeFirst();
                Label = $"Difficulty_{capitalizedName}{invertSuffix}_Label".Translate();
                Tooltip = $"Difficulty_{capitalizedName}{invertSuffix}_Info".Translate();
            }

            // Constructor for custom label/tooltip (used by Anomaly settings)
            public DifficultySliderSetting(string label, string tooltip, Func<float> getter, Action<float> setter,
                float min, float max, float step, ToStringStyle style,
                ToStringNumberSense numberSense = ToStringNumberSense.Absolute,
                bool reciprocate = false, float reciprocalCutoff = 1000f,
                Func<bool> enabledCondition = null)
            {
                this.getter = getter;
                this.setter = setter;
                this.min = min;
                this.max = max;
                this.step = step;
                this.style = style;
                this.numberSense = numberSense;
                this.reciprocate = reciprocate;
                this.reciprocalCutoff = reciprocalCutoff;
                this.enabledCondition = enabledCondition;

                Label = label;
                Tooltip = tooltip;
            }

            public override string GetAnnouncement()
            {
                if (!IsEnabled)
                {
                    return $"{Label}: Disabled";
                }

                float value = getter();
                if (reciprocate)
                {
                    value = Reciprocal(value, reciprocalCutoff);
                }
                string valueStr = value.ToStringByStyle(style, numberSense);
                return $"{Label}. {valueStr}. {Tooltip}";
            }

            public override void Toggle()
            {
                // Sliders don't toggle, adjust instead
                Adjust(1);
            }

            public override void Adjust(int direction)
            {
                if (!IsEnabled) return;

                float current = getter();
                if (reciprocate)
                {
                    current = Reciprocal(current, reciprocalCutoff);
                }

                float newValue = Mathf.Clamp(current + (step * direction), min, max);
                newValue = GenMath.RoundTo(newValue, step);

                if (reciprocate)
                {
                    newValue = Reciprocal(newValue, reciprocalCutoff);
                }

                setter(newValue);
            }

            /// <summary>
            /// Adjusts the slider by a percentage of its total possible positions.
            /// </summary>
            /// <param name="percent">Percentage of total positions to move (0.1 = 10%, 0.25 = 25%)</param>
            public void AdjustByPercentOfPositions(float percent)
            {
                if (!IsEnabled) return;

                float current = getter();
                if (reciprocate)
                {
                    current = Reciprocal(current, reciprocalCutoff);
                }

                // Calculate total number of discrete positions
                float range = max - min;
                int totalPositions = Mathf.Max(1, Mathf.RoundToInt(range / step));

                // Calculate how many steps to move (at least 1)
                int stepsToMove = Mathf.Max(1, Mathf.RoundToInt(totalPositions * Mathf.Abs(percent)));
                if (percent < 0) stepsToMove = -stepsToMove;

                float adjustment = step * stepsToMove;
                float newValue = Mathf.Clamp(current + adjustment, min, max);
                newValue = GenMath.RoundTo(newValue, step);

                if (reciprocate)
                {
                    newValue = Reciprocal(newValue, reciprocalCutoff);
                }

                setter(newValue);
            }

            /// <summary>
            /// Sets the slider to its maximum value.
            /// </summary>
            public void SetToMax()
            {
                if (!IsEnabled) return;

                float newValue = max;
                if (reciprocate)
                {
                    newValue = Reciprocal(newValue, reciprocalCutoff);
                }
                setter(newValue);
            }

            /// <summary>
            /// Sets the slider to its minimum value.
            /// </summary>
            public void SetToMin()
            {
                if (!IsEnabled) return;

                float newValue = min;
                if (reciprocate)
                {
                    newValue = Reciprocal(newValue, reciprocalCutoff);
                }
                setter(newValue);
            }

            private static float Reciprocal(float f, float cutOff)
            {
                cutOff *= 10f;
                if (Mathf.Abs(f) < 0.01f)
                {
                    return cutOff;
                }
                if (f >= 0.99f * cutOff)
                {
                    return 0f;
                }
                return 1f / f;
            }
        }

        /// <summary>
        /// Special setting for resetting all difficulty settings to a preset.
        /// </summary>
        private class DifficultyResetSetting : DifficultySetting
        {
            private readonly Action executeAction;

            public DifficultyResetSetting(string label, string tooltip, Action executeAction)
            {
                Label = label;
                Tooltip = string.IsNullOrEmpty(tooltip) ? "Resets all custom difficulty settings to this preset" : tooltip;
                this.executeAction = executeAction;
            }

            public override string GetAnnouncement()
            {
                return $"{Label}. {Tooltip}";
            }

            public override void Toggle()
            {
                executeAction?.Invoke();
            }

            public override void Adjust(int direction)
            {
                // Reset settings don't adjust - execute on Enter instead
            }
        }

        /// <summary>
        /// Setting for selecting an AnomalyPlaystyleDef.
        /// </summary>
        private class AnomalyPlaystyleSetting : DifficultySetting
        {
            private readonly Func<AnomalyPlaystyleDef> getter;
            private readonly Action<AnomalyPlaystyleDef> setter;
            private readonly List<AnomalyPlaystyleDef> options;
            private readonly Action onChanged;

            public AnomalyPlaystyleSetting(Func<AnomalyPlaystyleDef> getter, Action<AnomalyPlaystyleDef> setter, Action onChanged = null)
            {
                this.getter = getter;
                this.setter = setter;
                this.onChanged = onChanged;
                Label = "ChooseAnomalyPlaystyle".Translate();
                Tooltip = "Select an anomaly playstyle";
                options = DefDatabase<AnomalyPlaystyleDef>.AllDefs.ToList();
            }

            public override string GetAnnouncement()
            {
                var current = getter();
                string description = current?.description?.StripTags() ?? "";
                // Full announcement with label for navigation (up/down arrows)
                return $"{Label}. {current?.LabelCap ?? "None"}. {description}";
            }

            public override string GetAdjustmentAnnouncement()
            {
                var current = getter();
                string description = current?.description?.StripTags() ?? "";
                // Skip the long label when adjusting (left/right arrows) - just announce the value
                return $"{current?.LabelCap ?? "None"}. {description}";
            }

            public override void Toggle()
            {
                // Cycle to next option on Enter/Space
                Adjust(1);
            }

            public override void Adjust(int direction)
            {
                if (options.Count == 0) return;

                var current = getter();
                int currentIndex = options.IndexOf(current);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = (currentIndex + direction + options.Count) % options.Count;
                var newValue = options[newIndex];

                // Handle special case when switching to overrideThreatFraction mode
                if (!current.overrideThreatFraction && newValue.overrideThreatFraction)
                {
                    // Initialize override value when switching to a playstyle that uses it
                    var storyteller = Current.Game.storyteller;
                    if (!storyteller.difficulty.overrideAnomalyThreatsFraction.HasValue)
                    {
                        storyteller.difficulty.overrideAnomalyThreatsFraction = 0.15f;
                    }
                }

                setter(newValue);
                onChanged?.Invoke();
            }
        }
    }
}
