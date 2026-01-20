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
        /// </summary>
        public static void SetText(string text)
        {
            currentText = text ?? "";
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
        /// </summary>
        public static void HandleCharacter(char character)
        {
            currentText += character;
            TolkHelper.Speak(character.ToString(), SpeechPriority.High);
        }

        /// <summary>
        /// Handles backspace - removes last character and announces deletion.
        /// </summary>
        public static void HandleBackspace()
        {
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
