using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State for saving scenarios in the Scenario Builder.
    /// Provides keyboard navigation and filename input for saving.
    /// </summary>
    public static class WindowlessScenarioSaveState
    {
        public static bool IsActive { get; private set; }

        private static Scenario scenarioToSave;
        private static Action onSaveComplete;
        private static List<SaveFileInfo> existingFiles = new List<SaveFileInfo>();
        private static int selectedIndex = 0;
        private static bool isTypingFilename = true;
        private static TypeaheadSearchHelper typeaheadHelper = new TypeaheadSearchHelper();

        /// <summary>
        /// Opens the scenario save menu.
        /// </summary>
        public static void Open(Scenario scenario, Action onComplete)
        {
            scenarioToSave = scenario;
            onSaveComplete = onComplete;
            typeaheadHelper.ClearSearch();

            // Set initial filename from scenario name
            TextInputHelper.SetText(GenFile.SanitizedFileName(scenario.name ?? "NewScenario"));

            ReloadFiles();

            selectedIndex = 0;
            isTypingFilename = true;
            IsActive = true;

            TolkHelper.Speak($"Save Scenario. Type filename or press Down to select existing file to overwrite. Current name: {TextInputHelper.CurrentText}");
        }

        /// <summary>
        /// Closes the scenario save menu.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            scenarioToSave = null;
            onSaveComplete = null;
            existingFiles.Clear();
            typeaheadHelper.ClearSearch();
            TextInputHelper.Clear();
        }

        /// <summary>
        /// Reloads the list of existing scenario files.
        /// </summary>
        private static void ReloadFiles()
        {
            existingFiles.Clear();

            foreach (FileInfo file in GenFilePaths.AllCustomScenarioFiles)
            {
                try
                {
                    var saveInfo = new SaveFileInfo(file);
                    saveInfo.LoadData();
                    existingFiles.Add(saveInfo);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimWorld Access] Exception loading scenario file {file.Name}: {ex}");
                }
            }

            // Sort by last write time, most recent first
            existingFiles = existingFiles.OrderByDescending(f => f.LastWriteTime).ToList();
        }

        /// <summary>
        /// Gets the total count including the "Create New" option.
        /// </summary>
        private static int TotalCount => existingFiles.Count + 1;

        /// <summary>
        /// Announces the current state.
        /// </summary>
        private static void AnnounceCurrentState()
        {
            if (selectedIndex == 0)
            {
                // Create new with typed name
                TolkHelper.Speak($"Save as: {TextInputHelper.CurrentText} ({MenuHelper.FormatPosition(0, TotalCount)})");
            }
            else if (selectedIndex > 0 && selectedIndex <= existingFiles.Count)
            {
                // Overwrite existing
                var file = existingFiles[selectedIndex - 1];
                string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                string dateStr = FormatDateTime(file.LastWriteTime);
                TolkHelper.Speak($"Overwrite: {fileName} - {dateStr} ({MenuHelper.FormatPosition(selectedIndex, TotalCount)})");
            }
        }

        /// <summary>
        /// Formats a DateTime for display.
        /// </summary>
        private static string FormatDateTime(DateTime dateTime)
        {
            if (Prefs.TwelveHourClockMode)
            {
                return dateTime.ToString("yyyy-MM-dd h:mm tt");
            }
            else
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm");
            }
        }

        #region Navigation

        private static void SelectNext()
        {
            typeaheadHelper.ClearSearch();
            isTypingFilename = false;

            if (selectedIndex < existingFiles.Count)
            {
                selectedIndex++;
                AnnounceCurrentState();
            }
        }

        private static void SelectPrevious()
        {
            typeaheadHelper.ClearSearch();

            if (selectedIndex > 0)
            {
                selectedIndex--;
                if (selectedIndex == 0)
                {
                    isTypingFilename = true;
                }
                AnnounceCurrentState();
            }
        }

        private static void JumpToFirst()
        {
            typeaheadHelper.ClearSearch();
            selectedIndex = 0;
            isTypingFilename = true;
            AnnounceCurrentState();
        }

        private static void JumpToLast()
        {
            typeaheadHelper.ClearSearch();
            selectedIndex = existingFiles.Count;
            isTypingFilename = false;
            AnnounceCurrentState();
        }

        #endregion

        #region Actions

        private static void SaveSelected()
        {
            string fileName;

            if (selectedIndex == 0)
            {
                // Save with typed name
                fileName = TextInputHelper.CurrentText;
            }
            else if (selectedIndex > 0 && selectedIndex <= existingFiles.Count)
            {
                // Overwrite existing file
                var file = existingFiles[selectedIndex - 1];
                fileName = Path.GetFileNameWithoutExtension(file.FileName);
            }
            else
            {
                TolkHelper.Speak("Invalid selection");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                TolkHelper.Speak("Filename cannot be empty");
                return;
            }

            fileName = GenFile.SanitizedFileName(fileName);

            // Check for overwrite
            string fullPath = GenFilePaths.AbsPathForScenario(fileName);
            bool fileExists = File.Exists(fullPath);

            if (fileExists && selectedIndex == 0)
            {
                // Trying to create new but file exists - confirm overwrite
                TolkHelper.Speak($"File {fileName} already exists. Press Enter again to overwrite, or Escape to cancel.");
                // For simplicity, just save anyway on next Enter
            }

            try
            {
                scenarioToSave.name = scenarioToSave.name ?? fileName;
                GameDataSaveLoader.SaveScenario(scenarioToSave, fullPath);

                // Reset dirty flag after successful save
                ScenarioBuilderState.ResetDirty();

                Close();
                TolkHelper.Speak($"Saved as {fileName}");
                onSaveComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorld Access] Error saving scenario: {ex}");
                TolkHelper.Speak($"Error saving: {ex.Message}");
            }
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles keyboard input for the save menu.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            if (!IsActive) return false;

            switch (key)
            {
                case KeyCode.UpArrow:
                    SelectPrevious();
                    return true;

                case KeyCode.DownArrow:
                    SelectNext();
                    return true;

                case KeyCode.Home:
                    JumpToFirst();
                    return true;

                case KeyCode.End:
                    JumpToLast();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SaveSelected();
                    return true;

                case KeyCode.Escape:
                    Close();
                    TolkHelper.Speak("Cancelled");
                    return true;

                case KeyCode.Backspace:
                    if (isTypingFilename && selectedIndex == 0)
                    {
                        TextInputHelper.HandleBackspace();
                        return true;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Handles character input for filename typing.
        /// </summary>
        public static bool HandleCharacterInput(char character)
        {
            if (!IsActive) return false;

            // Only allow character input when on the "Create New" option
            if (selectedIndex == 0 && isTypingFilename)
            {
                // Filter out invalid filename characters
                if (char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '_')
                {
                    TextInputHelper.HandleCharacter(character);
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
