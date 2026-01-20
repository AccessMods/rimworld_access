using System;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages zone renaming with text input.
    /// Allows typing a new zone name with Enter to confirm and Escape to cancel.
    /// Uses TextInputHelper for shared text input logic.
    /// </summary>
    public static class ZoneRenameState
    {
        private static bool isActive = false;
        private static Zone currentZone = null;
        private static string originalName = "";

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the rename dialog for the specified zone.
        /// </summary>
        public static void Open(Zone zone)
        {
            if (zone == null)
            {
                Log.Error("Cannot open rename dialog: zone is null");
                return;
            }

            currentZone = zone;
            originalName = zone.label;
            TextInputHelper.SetText("");  // Start empty
            isActive = true;

            TolkHelper.Speak($"Renaming {originalName}. Type new name and press Enter, Escape to cancel.");
            Log.Message($"Opened rename dialog for zone: {originalName}");
        }

        /// <summary>
        /// Closes the rename dialog without saving.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentZone = null;
            originalName = "";
            TextInputHelper.Clear();
        }

        /// <summary>
        /// Handles character input for text entry.
        /// </summary>
        public static void HandleCharacter(char character)
        {
            if (!isActive)
                return;

            TextInputHelper.HandleCharacter(character);
        }

        /// <summary>
        /// Handles backspace key to delete last character.
        /// </summary>
        public static void HandleBackspace()
        {
            if (!isActive)
                return;

            TextInputHelper.HandleBackspace();
        }

        /// <summary>
        /// Reads the current text.
        /// </summary>
        public static void ReadCurrentText()
        {
            if (!isActive)
                return;

            TextInputHelper.ReadCurrentText();
        }

        /// <summary>
        /// Confirms the rename and applies the new name.
        /// </summary>
        public static void Confirm()
        {
            if (!isActive || currentZone == null)
                return;

            string newName = TextInputHelper.CurrentText;

            // Validate name
            if (string.IsNullOrWhiteSpace(newName))
            {
                TolkHelper.Speak("Cannot set empty name. Enter a name or press Escape to cancel.", SpeechPriority.High);
                return;
            }

            try
            {
                // Set the new name
                currentZone.label = newName;
                TolkHelper.Speak($"Renamed to {newName}", SpeechPriority.High);
                Log.Message($"Renamed zone from '{originalName}' to '{newName}'");
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Error renaming zone: {ex.Message}", SpeechPriority.High);
                Log.Error($"Error renaming zone: {ex}");
            }
            finally
            {
                Close();
            }
        }

        /// <summary>
        /// Cancels the rename without saving.
        /// </summary>
        public static void Cancel()
        {
            if (!isActive)
                return;

            TolkHelper.Speak("Rename cancelled");
            Log.Message("Zone rename cancelled");
            Close();
        }
    }
}
