namespace RimWorldAccess
{
    /// <summary>
    /// Shared helper for text input handling in rename dialogs.
    /// Manages text buffer and provides common input operations with screen reader announcements.
    /// Used by ZoneRenameState, StorageRenameState, and other text input states.
    /// </summary>
    public static class TextInputHelper
    {
        private static string currentText = "";
        private static bool replaceOnFirstKeystroke = false;

        /// <summary>
        /// Gets the current text in the buffer.
        /// </summary>
        public static string CurrentText => currentText;

        /// <summary>
        /// Gets whether the buffer is empty.
        /// </summary>
        public static bool IsEmpty => string.IsNullOrEmpty(currentText);

        /// <summary>
        /// Sets the initial text in the buffer.
        /// When replaceOnType is true (default), first keystroke will replace all text.
        /// </summary>
        public static void SetText(string text, bool replaceOnType = true)
        {
            currentText = text ?? "";
            replaceOnFirstKeystroke = replaceOnType && !string.IsNullOrEmpty(currentText);
        }

        /// <summary>
        /// Clears the text buffer.
        /// </summary>
        public static void Clear()
        {
            currentText = "";
        }

        /// <summary>
        /// Handles character input - adds character to buffer and announces it.
        /// If replaceOnFirstKeystroke is set, clears existing text first.
        /// </summary>
        public static void HandleCharacter(char character)
        {
            if (replaceOnFirstKeystroke)
            {
                // First keystroke replaces all existing text
                currentText = "";
                replaceOnFirstKeystroke = false;
            }
            currentText += character;
            TolkHelper.Speak(character.ToString(), SpeechPriority.High);
        }

        /// <summary>
        /// Handles backspace - removes last character and announces deletion.
        /// Pressing backspace cancels replace-on-type mode (user wants to edit, not replace).
        /// </summary>
        public static void HandleBackspace()
        {
            // Backspace cancels replace mode - user wants to edit existing text
            replaceOnFirstKeystroke = false;

            if (string.IsNullOrEmpty(currentText))
                return;

            char removed = currentText[currentText.Length - 1];
            currentText = currentText.Substring(0, currentText.Length - 1);
            TolkHelper.Speak($"Deleted {removed}", SpeechPriority.High);
        }

        /// <summary>
        /// Reads the current text aloud.
        /// </summary>
        public static void ReadCurrentText()
        {
            if (string.IsNullOrEmpty(currentText))
            {
                TolkHelper.Speak("Empty");
            }
            else
            {
                TolkHelper.Speak(currentText);
            }
        }
    }
}
