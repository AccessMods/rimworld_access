using System;
using System.Collections.Generic;
using System.Linq;
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

                IsActive = true;
                AnnounceQuantity();
            }
            else
            {
                TolkHelper.Speak("Text editing not supported for this field.");
                onCompleteCallback?.Invoke(); // Call callback so parent state refreshes
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

            TolkHelper.Speak($"{currentField?.Name ?? "Value"}: {value}. Use Up/Down to adjust, Enter to confirm.");
        }

        private static void QuantityIncrease(int amount = 1)
        {
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
                    Close(applyChanges: true);
                    TolkHelper.Speak($"Selected: {dropdownOptions[selectedOptionIndex].label}");
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
            // Determine increment amount
            int increment = 1;
            if (shift) increment = 10;
            if (ctrl) increment = 5;

            switch (key)
            {
                case KeyCode.UpArrow:
                    QuantityIncrease(increment);
                    return true;
                case KeyCode.DownArrow:
                    QuantityDecrease(increment);
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
                // CRITICAL: Consume all navigation keys to prevent them leaking to parent state
                // Without this, Left/Right/Tab would be handled by ScenarioBuilderState
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    // Silently consume - these keys don't apply to quantity editing
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles character input for typeahead in dropdown mode.
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

            return false;
        }

        #endregion
    }
}
