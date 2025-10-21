using HarmonyLib;
using UnityEngine;
using Verse;
using System.Reflection;

namespace RimWorldAccess
{
    [HarmonyPatch(typeof(Dialog_NodeTree))]
    [HarmonyPatch("DoWindowContents")]
    public class DialogAccessibilityPatch
    {
        private static readonly Color HighlightColor = new Color(1f, 1f, 0f, 0.5f);

        // Postfix to handle keyboard navigation and clipboard reading
        static void Postfix(Dialog_NodeTree __instance, Rect inRect)
        {
            // Initialize navigation state for this dialog
            DialogNavigationState.Initialize(__instance);

            // Get the current node using reflection
            FieldInfo curNodeField = typeof(Dialog_NodeTree).GetField("curNode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (curNodeField == null)
            {
                MelonLoader.MelonLogger.Msg("Failed to get curNode field");
                return;
            }

            DiaNode curNode = (DiaNode)curNodeField.GetValue(__instance);
            if (curNode == null)
            {
                return;
            }

            // Read the dialog text to clipboard on first display
            if (!DialogNavigationState.HasReadText() && !string.IsNullOrEmpty(curNode.text))
            {
                string textToRead = curNode.text.ToString();
                ClipboardHelper.CopyToClipboard(textToRead);
                DialogNavigationState.MarkTextAsRead();
                MelonLoader.MelonLogger.Msg($"Dialog text read to clipboard: {textToRead.Substring(0, Mathf.Min(50, textToRead.Length))}...");
            }

            // Handle keyboard navigation
            if (Event.current.type == EventType.KeyDown)
            {
                int optionCount = curNode.options.Count;
                int selectedIndex = DialogNavigationState.GetSelectedIndex();

                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    DialogNavigationState.MoveUp(optionCount);
                    selectedIndex = DialogNavigationState.GetSelectedIndex();

                    // Read the newly selected option to clipboard
                    if (selectedIndex >= 0 && selectedIndex < optionCount)
                    {
                        string optionText = GetOptionText(curNode.options[selectedIndex]);
                        ClipboardHelper.CopyToClipboard(optionText);
                        MelonLoader.MelonLogger.Msg($"Selected option {selectedIndex}: {optionText}");
                    }

                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    DialogNavigationState.MoveDown(optionCount);
                    selectedIndex = DialogNavigationState.GetSelectedIndex();

                    // Read the newly selected option to clipboard
                    if (selectedIndex >= 0 && selectedIndex < optionCount)
                    {
                        string optionText = GetOptionText(curNode.options[selectedIndex]);
                        ClipboardHelper.CopyToClipboard(optionText);
                        MelonLoader.MelonLogger.Msg($"Selected option {selectedIndex}: {optionText}");
                    }

                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    // Activate the selected option
                    if (selectedIndex >= 0 && selectedIndex < optionCount)
                    {
                        DiaOption selectedOption = curNode.options[selectedIndex];

                        // Call the Activate method using reflection
                        MethodInfo activateMethod = typeof(DiaOption).GetMethod("Activate", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (activateMethod != null)
                        {
                            // Make sure the option has a reference to the dialog
                            selectedOption.dialog = __instance;
                            activateMethod.Invoke(selectedOption, null);
                            MelonLoader.MelonLogger.Msg($"Activated option {selectedIndex}");

                            // Reset state after activation
                            DialogNavigationState.Reset();
                        }
                    }

                    Event.current.Use();
                }
            }

            // Draw highlight on selected option
            DrawOptionHighlight(__instance, inRect, curNode);
        }

        // Postfix to reset state when dialog closes
        [HarmonyPatch(typeof(Dialog_NodeTree), "PostClose")]
        [HarmonyPostfix]
        static void PostClose_Postfix()
        {
            DialogNavigationState.Reset();
        }

        private static void DrawOptionHighlight(Dialog_NodeTree dialog, Rect inRect, DiaNode curNode)
        {
            int selectedIndex = DialogNavigationState.GetSelectedIndex();
            if (selectedIndex < 0 || selectedIndex >= curNode.options.Count)
            {
                return;
            }

            // Get necessary private fields using reflection
            FieldInfo titleField = typeof(Dialog_NodeTree).GetField("title", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo optTotalHeightField = typeof(Dialog_NodeTree).GetField("optTotalHeight", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo minOptionsAreaHeightField = typeof(Dialog_NodeTree).GetField("minOptionsAreaHeight", BindingFlags.NonPublic | BindingFlags.Instance);

            if (titleField == null || optTotalHeightField == null || minOptionsAreaHeightField == null)
            {
                return;
            }

            string title = (string)titleField.GetValue(dialog);
            float optTotalHeight = (float)optTotalHeightField.GetValue(dialog);
            float minOptionsAreaHeight = (float)minOptionsAreaHeightField.GetValue(dialog);

            // Calculate the dialog content rect (same as in DoWindowContents)
            Rect rect = inRect.AtZero();
            if (title != null)
            {
                rect.yMin += 53f;
            }

            // Calculate options area position (same as in DrawNode)
            Text.Font = GameFont.Small;
            float num = Mathf.Min(optTotalHeight, rect.height - 100f - 18f * 2f); // Margin is 18f in Window class
            float optionsStartY = rect.height - Mathf.Max(num, minOptionsAreaHeight);

            // Calculate the Y position of the selected option
            float yOffset = 0f;
            float width = rect.width - 16f - 30f; // Account for scroll bar and horizontal margins (15f on each side)

            for (int i = 0; i < selectedIndex; i++)
            {
                string optText = GetOptionText(curNode.options[i]);
                float height = Text.CalcHeight(optText, width);
                yOffset += height + 7f; // 7f is OptVerticalSpace
            }

            // Calculate the height of the selected option
            string selectedOptText = GetOptionText(curNode.options[selectedIndex]);
            float selectedHeight = Text.CalcHeight(selectedOptText, width);

            // Draw the highlight
            Rect highlightRect = new Rect(
                rect.x + 15f, // OptHorMargin
                rect.y + optionsStartY + yOffset,
                rect.width - 30f, // Full width minus margins
                selectedHeight
            );

            Color prevColor = GUI.color;
            GUI.color = HighlightColor;
            GUI.DrawTexture(highlightRect, BaseContent.WhiteTex);
            GUI.color = prevColor;
        }

        private static string GetOptionText(DiaOption option)
        {
            // Get the text field using reflection
            FieldInfo textField = typeof(DiaOption).GetField("text", BindingFlags.NonPublic | BindingFlags.Instance);
            if (textField != null)
            {
                string text = (string)textField.GetValue(option);

                // If option is disabled, append the disabled reason
                if (option.disabled && !string.IsNullOrEmpty(option.disabledReason))
                {
                    text = text + " (" + option.disabledReason + ")";
                }

                return text;
            }
            return "Unknown";
        }
    }
}
