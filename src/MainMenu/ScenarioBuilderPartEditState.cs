using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// State for editing individual part fields in the Scenario Builder.
    /// Handles dropdown selection and quantity editing.
    /// </summary>
    public static class ScenarioBuilderPartEditState
    {
        public static bool IsActive { get; private set; }

        private static ScenarioBuilderState.PartField currentField;
        private static Action onComplete;

        // Dropdown state
        private static List<(string label, object value)> dropdownOptions;
        private static int selectedOptionIndex = 0;
        private static TypeaheadSearchHelper dropdownTypeahead = new TypeaheadSearchHelper();

        // Quantity state
        private static int quantityValue;
        private static int quantityMin;
        private static int quantityMax;
        private static float floatQuantityValue;
        private static bool isFloatQuantity;
        private static string quantityTypedBuffer = "";

        /// <summary>
        /// Opens the field editor for the given field.
        /// </summary>
        public static void Open(ScenarioBuilderState.PartField field, Action onCompleteCallback)
        {
            // Clear any stale state first
            currentField = null;
            onComplete = null;
            dropdownOptions = null;
            dropdownTypeahead.ClearSearch();
            IsActive = false;

            if (field.Type == ScenarioBuilderState.FieldType.Dropdown)
            {
                // Initialize dropdown
                var options = field.Data as List<(string label, object value)>;
                if (options == null || options.Count == 0)
                {
                    TolkHelper.Speak("No options available for this field.");
                    onCompleteCallback?.Invoke(); // Call callback so parent state refreshes
                    return;
                }

                currentField = field;
                onComplete = onCompleteCallback;
                dropdownOptions = options;

                // Find current selection
                selectedOptionIndex = 0;
                for (int i = 0; i < dropdownOptions.Count; i++)
                {
                    if (dropdownOptions[i].label == field.CurrentValue)
                    {
                        selectedOptionIndex = i;
                        break;
                    }
                }

                IsActive = true;
                AnnounceDropdownOption();
            }
            else if (field.Type == ScenarioBuilderState.FieldType.Quantity)
            {
                currentField = field;
                onComplete = onCompleteCallback;

                // Initialize quantity
                if (field.Data is int[] intRange)
                {
                    quantityMin = intRange[0];
                    quantityMax = intRange[1];
                    isFloatQuantity = false;

                    // Parse current value
                    string valueStr = field.CurrentValue.Replace("%", "").Trim();
                    if (int.TryParse(valueStr, out int parsed))
                        quantityValue = parsed;
                    else
                        quantityValue = quantityMin;
                }
                else if (field.Data is float[] floatRange)
                {
                    quantityMin = 0;
                    quantityMax = 100;
                    isFloatQuantity = true;

                    // Parse percentage value
                    string valueStr = field.CurrentValue.Replace("%", "").Trim();
                    if (float.TryParse(valueStr, out float parsed))
                        floatQuantityValue = parsed / 100f;
                    else
                        floatQuantityValue = floatRange[0];
                }
                else
                {
                    // Default range
                    quantityMin = 1;
                    quantityMax = 100;
                    isFloatQuantity = false;
                    quantityValue = 1;
                }

                quantityTypedBuffer = ""; // Clear any previous typed input
                IsActive = true;
                AnnounceQuantity();
            }
            else if (field.Type == ScenarioBuilderState.FieldType.Text)
            {
                currentField = field;
                onComplete = onCompleteCallback;

                // Initialize text helper with full text from Data
                string fullText = field.Data as string ?? "";
                TextInputHelper.SetText(fullText);

                IsActive = true;
                AnnounceText();
            }
            else if (field.Type == ScenarioBuilderState.FieldType.Checkbox)
            {
                currentField = field;
                onComplete = onCompleteCallback;

                // Checkbox is simple - just toggle and close
                bool currentValue = field.CurrentValue == "Yes" || field.CurrentValue == "true" || field.CurrentValue == "True";
                bool newValue = !currentValue;

                field.CurrentValue = newValue ? "Yes" : "No";
                field.SetValue?.Invoke(newValue);
                ScenarioBuilderState.SetDirty();

                TolkHelper.Speak($"{field.Name}: {(newValue ? "Enabled" : "Disabled")}");
                onCompleteCallback?.Invoke();
                return; // Don't set IsActive - immediate toggle
            }
            else
            {
                TolkHelper.Speak("Unsupported field type.");
                onCompleteCallback?.Invoke();
            }
        }

        /// <summary>
        /// Closes the field editor.
        /// </summary>
        public static void Close(bool applyChanges)
        {
            if (applyChanges && currentField != null)
            {
                ApplyCurrentValue();
            }

            IsActive = false;
            currentField = null;
            dropdownOptions = null;
            dropdownTypeahead.ClearSearch();
            TextInputHelper.Clear();

            onComplete?.Invoke();
        }

        /// <summary>
        /// Applies the current value to the field.
        /// </summary>
        private static void ApplyCurrentValue()
        {
            if (currentField == null) return;

            if (currentField.Type == ScenarioBuilderState.FieldType.Dropdown && dropdownOptions != null)
            {
                var selected = dropdownOptions[selectedOptionIndex];
                currentField.SetValue?.Invoke(selected.value);
                currentField.CurrentValue = selected.label;
                ScenarioBuilderState.SetDirty();
            }
            else if (currentField.Type == ScenarioBuilderState.FieldType.Quantity)
            {
                if (isFloatQuantity)
                {
                    currentField.SetValue?.Invoke(floatQuantityValue);
                    currentField.CurrentValue = $"{floatQuantityValue * 100:F0}%";
                }
                else
                {
                    currentField.SetValue?.Invoke(quantityValue);
                    currentField.CurrentValue = quantityValue.ToString();
                }
                ScenarioBuilderState.SetDirty();
            }
            else if (currentField.Type == ScenarioBuilderState.FieldType.Text)
            {
                string newText = TextInputHelper.CurrentText;
                currentField.SetValue?.Invoke(newText);

                // Update display value (truncated for tree view)
                if (string.IsNullOrEmpty(newText))
                {
                    currentField.CurrentValue = "(empty)";
                }
                else if (newText.Contains("\n"))
                {
                    int newlineIndex = newText.IndexOf('\n');
                    currentField.CurrentValue = newText.Substring(0, Math.Min(newlineIndex, 60)) + "...";
                }
                else if (newText.Length > 60)
                {
                    currentField.CurrentValue = newText.Substring(0, 60) + "...";
                }
                else
                {
                    currentField.CurrentValue = newText;
                }

                ScenarioBuilderState.SetDirty();
            }
        }

        #region Dropdown Navigation

        private static void AnnounceDropdownOption()
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return;

            var option = dropdownOptions[selectedOptionIndex];
            string positionPart = MenuHelper.FormatPosition(selectedOptionIndex, dropdownOptions.Count);

            string text = $"{option.label}";
            if (!string.IsNullOrEmpty(positionPart))
            {
                text += $" ({positionPart})";
            }

            if (dropdownTypeahead.HasActiveSearch)
            {
                text += $", {dropdownTypeahead.CurrentMatchPosition} of {dropdownTypeahead.MatchCount} matches";
            }

            // Add description from Def objects if available
            // Special handling for FactionDef which has a computed Description property
            if (option.value is FactionDef fd && !string.IsNullOrEmpty(fd.Description))
            {
                string desc = fd.Description;
                int newlineIndex = desc.IndexOf('\n');
                if (newlineIndex > 0)
                    desc = desc.Substring(0, newlineIndex);
                text += $". {desc.Trim()}";
            }
            else if (option.value is Def def && !string.IsNullOrEmpty(def.description))
            {
                string desc = def.description;
                int newlineIndex = desc.IndexOf('\n');
                if (newlineIndex > 0)
                    desc = desc.Substring(0, newlineIndex);
                text += $". {desc.Trim()}";
            }

            TolkHelper.Speak(text);
        }

        private static void DropdownNext()
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return;

            dropdownTypeahead.ClearSearch();
            selectedOptionIndex = MenuHelper.SelectNext(selectedOptionIndex, dropdownOptions.Count);
            AnnounceDropdownOption();
        }

        private static void DropdownPrevious()
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return;

            dropdownTypeahead.ClearSearch();
            selectedOptionIndex = MenuHelper.SelectPrevious(selectedOptionIndex, dropdownOptions.Count);
            AnnounceDropdownOption();
        }

        private static void DropdownHome()
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return;

            dropdownTypeahead.ClearSearch();
            selectedOptionIndex = 0;
            AnnounceDropdownOption();
        }

        private static void DropdownEnd()
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return;

            dropdownTypeahead.ClearSearch();
            selectedOptionIndex = dropdownOptions.Count - 1;
            AnnounceDropdownOption();
        }

        private static bool HandleDropdownTypeahead(char character)
        {
            if (dropdownOptions == null || dropdownOptions.Count == 0) return false;

            var labels = dropdownOptions.Select(o => o.label).ToList();

            if (dropdownTypeahead.ProcessCharacterInput(character, labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedOptionIndex = newIndex;
                    AnnounceDropdownOption();
                }
            }
            else
            {
                TolkHelper.Speak($"No matches for '{dropdownTypeahead.LastFailedSearch}'");
            }

            return true;
        }

        private static bool HandleDropdownBackspace()
        {
            if (!dropdownTypeahead.HasActiveSearch) return false;

            var labels = dropdownOptions.Select(o => o.label).ToList();

            if (dropdownTypeahead.ProcessBackspace(labels, out int newIndex))
            {
                if (newIndex >= 0)
                {
                    selectedOptionIndex = newIndex;
                    AnnounceDropdownOption();
                }
            }

            return true;
        }

        #endregion

        #region Quantity Navigation

        private static void AnnounceQuantity()
        {
            string value;
            if (isFloatQuantity)
            {
                value = $"{floatQuantityValue * 100:F0}%";
            }
            else
            {
                value = quantityValue.ToString();
            }

            TolkHelper.Speak($"{currentField?.Name ?? "Value"}: {value}. Type a number or use Up/Down to adjust, Enter to confirm.");
        }

        private static void AnnounceText()
        {
            string text = TextInputHelper.CurrentText;
            int lineCount = text.Split('\n').Length;
            int wordCount = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            string summary;
            if (string.IsNullOrEmpty(text))
                summary = "empty";
            else if (lineCount > 1)
                summary = $"{lineCount} lines, {wordCount} words";
            else if (text.Length > 50)
                summary = $"{wordCount} words";
            else
                summary = text;

            TolkHelper.Speak($"{currentField?.Name ?? "Text"}: {summary}. Type to edit, Ctrl+V to paste, Insert to read all, Shift+Enter for new line, Enter to confirm, Escape to cancel.");
        }

        private static void QuantityIncrease(int amount = 1)
        {
            quantityTypedBuffer = ""; // Clear typed input when using arrows
            if (isFloatQuantity)
            {
                floatQuantityValue = Mathf.Clamp01(floatQuantityValue + (amount * 0.01f));
                TolkHelper.Speak($"{floatQuantityValue * 100:F0}%");
            }
            else
            {
                quantityValue = Mathf.Clamp(quantityValue + amount, quantityMin, quantityMax);
                TolkHelper.Speak(quantityValue.ToString());
            }
        }

        private static void QuantityDecrease(int amount = 1)
        {
            quantityTypedBuffer = ""; // Clear typed input when using arrows
            if (isFloatQuantity)
            {
                floatQuantityValue = Mathf.Clamp01(floatQuantityValue - (amount * 0.01f));
                TolkHelper.Speak($"{floatQuantityValue * 100:F0}%");
            }
            else
            {
                quantityValue = Mathf.Clamp(quantityValue - amount, quantityMin, quantityMax);
                TolkHelper.Speak(quantityValue.ToString());
            }
        }

        private static void QuantityMin()
        {
            quantityTypedBuffer = ""; // Clear typed input when using Home
            if (isFloatQuantity)
            {
                floatQuantityValue = 0f;
                TolkHelper.Speak("0%");
            }
            else
            {
                quantityValue = quantityMin;
                TolkHelper.Speak(quantityValue.ToString());
            }
        }

        private static void QuantityMax()
        {
            quantityTypedBuffer = ""; // Clear typed input when using End
            if (isFloatQuantity)
            {
                floatQuantityValue = 1f;
                TolkHelper.Speak("100%");
            }
            else
            {
                quantityValue = quantityMax;
                TolkHelper.Speak(quantityValue.ToString());
            }
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles keyboard input for the field editor.
        /// Returns true if the input was handled.
        /// </summary>
        public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            if (!IsActive) return false;

            if (currentField?.Type == ScenarioBuilderState.FieldType.Dropdown)
            {
                return HandleDropdownInput(key, shift, ctrl);
            }
            else if (currentField?.Type == ScenarioBuilderState.FieldType.Quantity)
            {
                return HandleQuantityInput(key, shift, ctrl);
            }
            else if (currentField?.Type == ScenarioBuilderState.FieldType.Text)
            {
                return HandleTextInput(key, shift, ctrl);
            }

            return false;
        }

        private static bool HandleDropdownInput(KeyCode key, bool shift, bool ctrl)
        {
            switch (key)
            {
                case KeyCode.UpArrow:
                    DropdownPrevious();
                    return true;
                case KeyCode.DownArrow:
                    DropdownNext();
                    return true;
                case KeyCode.Home:
                    DropdownHome();
                    return true;
                case KeyCode.End:
                    DropdownEnd();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    string selectedLabel = dropdownOptions[selectedOptionIndex].label;
                    Close(applyChanges: true);
                    TolkHelper.Speak($"Selected: {selectedLabel}");
                    return true;
                case KeyCode.Escape:
                    Close(applyChanges: false);
                    TolkHelper.Speak("Cancelled");
                    return true;
                case KeyCode.Backspace:
                    if (dropdownTypeahead.HasActiveSearch)
                    {
                        HandleDropdownBackspace();
                    }
                    // Always consume backspace in dropdown mode
                    return true;
                // CRITICAL: Consume all navigation keys to prevent them leaking to parent state
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                case KeyCode.Delete:
                    // Silently consume - these keys don't apply to dropdown editing
                    return true;
            }

            return false;
        }

        private static bool HandleQuantityInput(KeyCode key, bool shift, bool ctrl)
        {
            // Determine increment amount based on modifiers
            // Shift = 10, Ctrl = 100, Both = 100, None = 1
            int increment = 1;
            if (ctrl) increment = 100;
            else if (shift) increment = 10;

            switch (key)
            {
                case KeyCode.UpArrow:
                    QuantityIncrease(increment);
                    return true;
                case KeyCode.DownArrow:
                    QuantityDecrease(increment);
                    return true;
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals: // Unshifted + key on most keyboards
                    QuantityIncrease(1);
                    return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    QuantityDecrease(1);
                    return true;
                case KeyCode.Home:
                    QuantityMin();
                    return true;
                case KeyCode.End:
                    QuantityMax();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Close(applyChanges: true);
                    string finalValue = isFloatQuantity ? $"{floatQuantityValue * 100:F0}%" : quantityValue.ToString();
                    TolkHelper.Speak($"Set to: {finalValue}");
                    return true;
                case KeyCode.Escape:
                    Close(applyChanges: false);
                    TolkHelper.Speak("Cancelled");
                    return true;
                case KeyCode.Backspace:
                    // Handle backspace for typed number input
                    HandleQuantityBackspace();
                    return true;
                // CRITICAL: Consume all navigation keys to prevent them leaking to parent state
                // Without this, Left/Right/Tab would be handled by ScenarioBuilderState
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                case KeyCode.Delete:
                    // Silently consume - these keys don't apply to quantity editing
                    return true;
            }

            return false;
        }

        private static bool HandleTextInput(KeyCode key, bool shift, bool ctrl)
        {
            switch (key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (shift)
                    {
                        // Shift+Enter inserts a newline
                        TextInputHelper.HandleCharacter('\n');
                        TolkHelper.Speak("New line", SpeechPriority.High);
                        return true;
                    }
                    else
                    {
                        // Enter confirms the edit
                        Close(applyChanges: true);
                        TolkHelper.Speak("Text saved");
                        return true;
                    }
                case KeyCode.Escape:
                    Close(applyChanges: false);
                    TolkHelper.Speak("Cancelled");
                    return true;
                case KeyCode.Backspace:
                    TextInputHelper.HandleBackspace();
                    return true;
                case KeyCode.Insert:
                    TextInputHelper.ReadCurrentText();
                    return true;
                case KeyCode.V:
                    if (ctrl)
                    {
                        TextInputHelper.HandlePaste();
                        return true;
                    }
                    break;
                // Consume navigation keys to prevent leaking to parent state
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                case KeyCode.Home:
                case KeyCode.End:
                case KeyCode.Delete:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Handles character input for typeahead in dropdown mode or number input in quantity mode.
        /// </summary>
        public static bool HandleCharacterInput(char character)
        {
            if (!IsActive) return false;

            if (currentField?.Type == ScenarioBuilderState.FieldType.Dropdown)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return HandleDropdownTypeahead(character);
                }
            }
            else if (currentField?.Type == ScenarioBuilderState.FieldType.Quantity)
            {
                if (char.IsDigit(character))
                {
                    return HandleQuantityDigitInput(character);
                }
            }
            else if (currentField?.Type == ScenarioBuilderState.FieldType.Text)
            {
                // Accept all printable characters for text editing
                if (character >= ' ')
                {
                    TextInputHelper.HandleCharacter(character);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handles digit input for quantity fields - builds a number from typed digits.
        /// </summary>
        private static bool HandleQuantityDigitInput(char digit)
        {
            quantityTypedBuffer += digit;

            if (int.TryParse(quantityTypedBuffer, out int typedValue))
            {
                if (isFloatQuantity)
                {
                    // For percentages, treat typed value as percent
                    float clampedFloat = Mathf.Clamp01(typedValue / 100f);
                    floatQuantityValue = clampedFloat;
                    int clampedPercent = Mathf.RoundToInt(clampedFloat * 100);

                    if (typedValue != clampedPercent)
                    {
                        // Value was clamped - clear buffer and use friendly message
                        quantityTypedBuffer = clampedPercent.ToString();
                        string limitType = typedValue > 100 ? "maximum" : "minimum";
                        TolkHelper.Speak($"{clampedPercent}% ({limitType})");
                    }
                    else
                    {
                        TolkHelper.Speak($"{typedValue}%");
                    }
                }
                else
                {
                    // Clamp to valid range
                    quantityValue = Mathf.Clamp(typedValue, quantityMin, quantityMax);
                    if (typedValue != quantityValue)
                    {
                        // Value was clamped - clear buffer to clamped value and use friendly message
                        quantityTypedBuffer = quantityValue.ToString();
                        string limitType = typedValue > quantityMax ? "maximum" : "minimum";
                        TolkHelper.Speak($"{quantityValue} ({limitType})");
                    }
                    else
                    {
                        TolkHelper.Speak(quantityValue.ToString());
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Handles backspace in quantity mode - removes last typed digit.
        /// </summary>
        private static bool HandleQuantityBackspace()
        {
            if (string.IsNullOrEmpty(quantityTypedBuffer))
            {
                return false; // Nothing to delete
            }

            // Remove last character
            quantityTypedBuffer = quantityTypedBuffer.Substring(0, quantityTypedBuffer.Length - 1);

            if (string.IsNullOrEmpty(quantityTypedBuffer))
            {
                TolkHelper.Speak("Cleared");
                // Reset to min value
                if (isFloatQuantity)
                    floatQuantityValue = 0f;
                else
                    quantityValue = quantityMin;
            }
            else if (int.TryParse(quantityTypedBuffer, out int typedValue))
            {
                if (isFloatQuantity)
                {
                    floatQuantityValue = Mathf.Clamp01(typedValue / 100f);
                    TolkHelper.Speak($"{typedValue}%");
                }
                else
                {
                    quantityValue = Mathf.Clamp(typedValue, quantityMin, quantityMax);
                    TolkHelper.Speak(quantityValue.ToString());
                }
            }

            return true;
        }

        #endregion
    }
}
