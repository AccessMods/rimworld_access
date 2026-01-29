using UnityEngine;

namespace RimWorldAccess
{
    /// <summary>
    /// Immutable wrapper around Unity's Event.current providing clean API and testability.
    /// Encapsulates key code and modifier state for keyboard input handling.
    /// </summary>
    public class KeyboardInputContext
    {
        /// <summary>
        /// The key that was pressed.
        /// </summary>
        public KeyCode Key { get; }

        /// <summary>
        /// True if Shift key is held.
        /// </summary>
        public bool Shift { get; }

        /// <summary>
        /// True if Ctrl key is held.
        /// </summary>
        public bool Ctrl { get; }

        /// <summary>
        /// True if Alt key is held.
        /// </summary>
        public bool Alt { get; }

        /// <summary>
        /// True if no modifier keys are held.
        /// </summary>
        public bool NoModifiers => !Shift && !Ctrl && !Alt;

        /// <summary>
        /// True if any modifier key is held.
        /// </summary>
        public bool HasModifiers => Shift || Ctrl || Alt;

        /// <summary>
        /// Reference to the underlying Unity Event (for edge cases).
        /// </summary>
        public Event UnityEvent { get; }

        /// <summary>
        /// Creates a context from Unity's Event.current.
        /// </summary>
        /// <param name="evt">The Unity Event to wrap</param>
        public KeyboardInputContext(Event evt)
        {
            UnityEvent = evt;
            Key = evt.keyCode;
            Shift = evt.shift;
            Ctrl = evt.control;
            Alt = evt.alt;
        }

        /// <summary>
        /// Creates a context for testing (without Unity Event).
        /// </summary>
        /// <param name="key">The key code</param>
        /// <param name="shift">Whether Shift is held</param>
        /// <param name="ctrl">Whether Ctrl is held</param>
        /// <param name="alt">Whether Alt is held</param>
        public KeyboardInputContext(KeyCode key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            UnityEvent = null;
            Key = key;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
        }

        /// <summary>
        /// True if the key is a letter (A-Z).
        /// </summary>
        public bool IsLetter => Key >= KeyCode.A && Key <= KeyCode.Z;

        /// <summary>
        /// True if the key is a number (0-9, top row).
        /// </summary>
        public bool IsNumber => Key >= KeyCode.Alpha0 && Key <= KeyCode.Alpha9;

        /// <summary>
        /// True if the key is alphanumeric (letter or number).
        /// </summary>
        public bool IsAlphanumeric => IsLetter || IsNumber;

        /// <summary>
        /// Gets the character representation of the key (lowercase for letters, digit for numbers).
        /// Returns '\0' if key is not alphanumeric.
        /// </summary>
        public char GetCharacter()
        {
            if (IsLetter)
            {
                return (char)('a' + (Key - KeyCode.A));
            }
            if (IsNumber)
            {
                return (char)('0' + (Key - KeyCode.Alpha0));
            }
            return '\0';
        }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            string modifiers = "";
            if (Ctrl) modifiers += "Ctrl+";
            if (Shift) modifiers += "Shift+";
            if (Alt) modifiers += "Alt+";
            return $"{modifiers}{Key}";
        }
    }
}
