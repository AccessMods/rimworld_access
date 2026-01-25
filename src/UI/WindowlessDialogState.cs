using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages a windowless, keyboard-accessible dialog system.
    /// Replaces visual dialogs with screen reader announcements and keyboard navigation.
    /// </summary>
    public static class WindowlessDialogState
    {
        private static Window currentDialog;
        private static List<DialogElement> elements = new List<DialogElement>();
        private static int selectedIndex = 0;
        private static DialogElement editingElement = null;

        /// <summary>
        /// Tracks the frame number when the dialog was opened.
        /// Used to skip input handling on the same frame to prevent the triggering key
        /// (e.g., Escape) from immediately activating a button in the dialog.
        /// </summary>
        private static int openedOnFrame = -1;

        public static bool IsActive => currentDialog != null;
        public static bool IsEditingTextField => editingElement != null;

        /// <summary>
        /// Tracks whether the current intercepted dialog has forcePause set.
        /// Used by WindowsForcePausePatch to maintain game pause behavior.
        /// </summary>
        public static bool ShouldForcePause { get; private set; }

        /// <summary>
        /// Opens a windowless version of the given dialog.
        /// </summary>
        public static void Open(Window dialog)
        {
            if (dialog == null)
                return;

            Close();

            // Pause the game when a dialog opens
            if (Current.ProgramState == ProgramState.Playing && Find.TickManager != null)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            }

            // Close any open menus that might interfere
            CloseActiveMenus();

            currentDialog = dialog;

            // Track the frame when opened to skip input on this frame
            // This prevents the triggering key (e.g., Escape) from immediately activating Cancel
            openedOnFrame = Time.frameCount;

            // Track if this dialog should force pause - used by WindowsForcePausePatch
            // to prevent game systems from thinking no modal dialog is open
            ShouldForcePause = dialog.forcePause;

            elements = DialogElementExtractor.ExtractElements(dialog);

            // Insert dialog description as first element so users can re-read it
            string title = DialogElementExtractor.GetDialogTitle(dialog)?.StripTags() ?? "";
            string message = DialogElementExtractor.GetDialogMessage(dialog)?.StripTags() ?? "";
            string descriptionText = BuildDescriptionText(title, message);

            if (!string.IsNullOrEmpty(descriptionText))
            {
                elements.Insert(0, new DialogDescriptionElement { Title = title, Message = message });
            }

            // Start on the first action (element 1), not the description (element 0)
            selectedIndex = elements.Count > 1 ? 1 : 0;
            editingElement = null;

            // Announce dialog opened with full description
            string announcement = BuildDialogAnnouncement(title, message);
            TolkHelper.Speak(announcement, SpeechPriority.High);

            // Announce the focused element (first action, not the description)
            if (elements.Count > 1)
            {
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Closes any active menus that might interfere with dialog navigation.
        /// </summary>
        private static void CloseActiveMenus()
        {
            // Close architect menu
            if (ArchitectState.IsActive)
            {
                Find.DesignatorManager?.Deselect();
            }

            // Close zone creation mode
            if (ZoneCreationState.IsInCreationMode)
            {
                // Zone creation will be blocked by MapNavigationState.SuppressMapNavigation
                // which checks WindowlessDialogState.IsActive
            }

            // Most other states will be automatically blocked by their checks for
            // WindowlessDialogState.IsActive or will be suppressed by MapNavigationState
            // No need to explicitly close them
        }

        /// <summary>
        /// Closes the current windowless dialog.
        /// </summary>
        public static void Close()
        {
            if (currentDialog != null)
            {
                // Remove from window stack if still present
                Find.WindowStack.TryRemove(currentDialog, doCloseSound: false);
                currentDialog = null;
            }

            elements.Clear();
            selectedIndex = 0;
            editingElement = null;
            ShouldForcePause = false;
            openedOnFrame = -1;
        }

        /// <summary>
        /// Handles keyboard input for the windowless dialog.
        /// Returns true if the event was consumed.
        /// </summary>
        public static bool HandleInput(Event evt)
        {
            if (!IsActive)
                return false;

            if (evt.type != EventType.KeyDown)
                return false;

            // Skip input handling on the same frame the dialog was opened
            // This prevents the triggering key (e.g., Escape) from immediately activating Cancel
            if (Time.frameCount == openedOnFrame)
                return false;

            KeyCode key = evt.keyCode;

            // If we're editing a text field, handle differently
            if (editingElement != null && editingElement is TextFieldElement textField)
            {
                return HandleTextFieldInput(textField, evt);
            }

            // Alt+R: Randomize current field (for Dialog_NamePawn)
            if (key == KeyCode.R && evt.alt)
            {
                if (currentDialog?.GetType().Name == "Dialog_NamePawn" &&
                    selectedIndex >= 0 && selectedIndex < elements.Count &&
                    elements[selectedIndex] is TextFieldElement nameField)
                {
                    // Calculate field index (accounting for description element at index 0)
                    int fieldIndex = selectedIndex;
                    if (elements.Count > 0 && elements[0] is DialogDescriptionElement)
                    {
                        fieldIndex = selectedIndex - 1;
                    }

                    NamePawnDialogHelper.RandomizeField(currentDialog, nameField, fieldIndex);
                    evt.Use();
                    return true;
                }
            }

            // Navigation
            if (key == KeyCode.UpArrow)
            {
                SelectPrevious();
                evt.Use();
                return true;
            }
            else if (key == KeyCode.DownArrow)
            {
                SelectNext();
                evt.Use();
                return true;
            }
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                ActivateCurrentElement();
                evt.Use();
                return true;
            }
            else if (key == KeyCode.Escape)
            {
                // Try to find cancel button, otherwise just close
                ButtonElement cancelButton = elements.Find(e => e is ButtonElement btn && btn.IsCancel) as ButtonElement;
                if (cancelButton != null)
                {
                    cancelButton.Execute();

                    // If cancelling a confirmation dialog over caravan formation, reset the send attempted flag
                    // so that a subsequent Escape will properly announce "Caravan formation cancelled"
                    if (CaravanFormationState.IsActive)
                    {
                        CaravanFormationState.ResetSendAttempted();
                    }
                }
                Close();
                evt.Use();
                return true;
            }

            return false;
        }

        private static bool HandleTextFieldInput(TextFieldElement textField, Event evt)
        {
            KeyCode key = evt.keyCode;

            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                // Close editing mode
                editingElement = null;
                TolkHelper.Speak($"Editing complete. Current value: {textField.Value}");
                evt.Use();
                return true;
            }
            else if (key == KeyCode.Escape)
            {
                // Cancel editing
                editingElement = null;
                TolkHelper.Speak("Editing cancelled");
                evt.Use();
                return true;
            }
            else if (key == KeyCode.Backspace)
            {
                if (textField.Value.Length > 0)
                {
                    textField.Value = textField.Value.Substring(0, textField.Value.Length - 1);
                    TolkHelper.Speak(textField.Value.Length > 0 ? textField.Value[textField.Value.Length - 1].ToString() : "Empty");
                }
                evt.Use();
                return true;
            }
            else if (key == KeyCode.Delete)
            {
                textField.Value = "";
                TolkHelper.Speak("Cleared");
                evt.Use();
                return true;
            }
            else if (evt.character != '\0' && evt.character != '\n' && evt.character != '\r')
            {
                // Add character to text field
                if (textField.Value.Length < textField.MaxLength)
                {
                    textField.Value += evt.character;
                    TolkHelper.Speak(evt.character.ToString());
                }
                evt.Use();
                return true;
            }

            return false;
        }

        private static void SelectNext()
        {
            if (elements.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectNext(selectedIndex, elements.Count);
            AnnounceCurrentElement();
        }

        private static void SelectPrevious()
        {
            if (elements.Count == 0)
                return;

            selectedIndex = MenuHelper.SelectPrevious(selectedIndex, elements.Count);
            AnnounceCurrentElement();
        }

        private static void ActivateCurrentElement()
        {
            if (selectedIndex < 0 || selectedIndex >= elements.Count)
                return;

            DialogElement element = elements[selectedIndex];

            if (element is DialogDescriptionElement descElement)
            {
                // Re-read the description
                TolkHelper.Speak(descElement.GetAnnouncement());
            }
            else if (element is ButtonElement button)
            {
                button.Execute();

                // If it's a confirm or close button, close the dialog
                if (button.IsConfirm || button.IsClose)
                {
                    Close();
                }
            }
            else if (element is TextFieldElement textField)
            {
                // Enter editing mode
                editingElement = textField;
                TolkHelper.Speak($"Editing {textField.Label}. Current value: {textField.Value}. Type to change, Enter to confirm, Escape to cancel.");
            }
        }

        private static void AnnounceCurrentElement()
        {
            if (selectedIndex < 0 || selectedIndex >= elements.Count)
                return;

            DialogElement element = elements[selectedIndex];

            // Description element doesn't show position
            if (element is DialogDescriptionElement)
            {
                TolkHelper.Speak(element.GetAnnouncement());
                return;
            }

            // For action elements, show position relative to action count (excluding description)
            // Description is always at index 0 if present, so actions start at index 1
            bool hasDescription = elements.Count > 0 && elements[0] is DialogDescriptionElement;
            int actionIndex = hasDescription ? selectedIndex - 1 : selectedIndex;
            int actionCount = hasDescription ? elements.Count - 1 : elements.Count;

            string position = MenuHelper.FormatPosition(actionIndex, actionCount);
            string announcement = string.IsNullOrEmpty(position)
                ? element.GetAnnouncement()
                : $"{element.GetAnnouncement()}. {position}";
            TolkHelper.Speak(announcement);
        }

        private static string BuildDescriptionText(string title, string message)
        {
            string text = "";
            if (!string.IsNullOrEmpty(title))
            {
                text += title;
            }
            if (!string.IsNullOrEmpty(message))
            {
                if (!string.IsNullOrEmpty(text))
                    text += ". ";
                text += message;
            }
            return text;
        }

        private static string BuildDialogAnnouncement(string title, string message)
        {
            string announcement = "Dialog opened. ";

            if (!string.IsNullOrEmpty(title))
            {
                announcement += title + ". ";
            }

            if (!string.IsNullOrEmpty(message))
            {
                announcement += message;
                // Only add period if message doesn't already end with punctuation
                if (!message.EndsWith(".") && !message.EndsWith("!") && !message.EndsWith("?"))
                {
                    announcement += ".";
                }
                announcement += " ";
            }

            // Subtract 1 from count since element 0 is the description, not an action
            int actionCount = elements.Count > 0 ? elements.Count - 1 : 0;
            announcement += $"{actionCount} elements. Use arrow keys to navigate, Enter to activate, Escape to cancel.";

            return announcement;
        }

        public static Window GetCurrentDialog()
        {
            return currentDialog;
        }
    }

    /// <summary>
    /// Base class for dialog elements that can be interacted with.
    /// </summary>
    public abstract class DialogElement
    {
        public string Label { get; set; }

        public abstract string GetAnnouncement();
    }

    /// <summary>
    /// Represents a button in the dialog.
    /// </summary>
    public class ButtonElement : DialogElement
    {
        public Action Action { get; set; }
        public bool IsConfirm { get; set; }
        public bool IsCancel { get; set; }
        public bool IsClose { get; set; }
        public bool Disabled { get; set; }
        public string DisabledReason { get; set; }

        public override string GetAnnouncement()
        {
            string announcement = $"{Label} button";

            if (Disabled && !string.IsNullOrEmpty(DisabledReason))
            {
                announcement += $" (Disabled: {DisabledReason})";
            }

            return announcement;
        }

        public void Execute()
        {
            if (Disabled)
            {
                TolkHelper.Speak($"Cannot activate: {DisabledReason}");
                return;
            }

            TolkHelper.Speak($"Activated {Label}");
            Action?.Invoke();
        }
    }

    /// <summary>
    /// Represents a text field in the dialog.
    /// </summary>
    public class TextFieldElement : DialogElement
    {
        private string backingValue;
        private Action<string> onValueChanged;

        public string Value
        {
            get => backingValue;
            set
            {
                backingValue = value;
                onValueChanged?.Invoke(value);
            }
        }

        public int MaxLength { get; set; } = 1000;

        public TextFieldElement(string label, string initialValue, Action<string> onValueChanged)
        {
            Label = label;
            backingValue = initialValue;
            this.onValueChanged = onValueChanged;
        }

        public override string GetAnnouncement()
        {
            string valueAnnouncement = string.IsNullOrEmpty(Value) ? "Empty" : Value;
            return $"Text field: {Label}. Current value: {valueAnnouncement}. Press Enter to edit.";
        }
    }

    /// <summary>
    /// Represents a label or message in the dialog.
    /// </summary>
    public class LabelElement : DialogElement
    {
        public string Text { get; set; }

        public override string GetAnnouncement()
        {
            return $"Label: {Text}";
        }
    }

    /// <summary>
    /// Represents the dialog description (title + message) as a navigable element.
    /// Allows users to re-read the dialog text by navigating to this element.
    /// </summary>
    public class DialogDescriptionElement : DialogElement
    {
        public string Title { get; set; }
        public string Message { get; set; }

        public override string GetAnnouncement()
        {
            string text = "Dialog description: ";
            if (!string.IsNullOrEmpty(Title))
            {
                text += Title;
            }
            if (!string.IsNullOrEmpty(Message))
            {
                if (!string.IsNullOrEmpty(Title))
                    text += ". ";
                text += Message;
            }
            return text;
        }
    }
}
