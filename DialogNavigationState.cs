using Verse;

namespace RimWorldAccess
{
    public static class DialogNavigationState
    {
        private static Dialog_NodeTree currentDialog;
        private static int selectedOptionIndex = 0;
        private static bool textReadToClipboard = false;

        public static void Initialize(Dialog_NodeTree dialog)
        {
            // Only reset if we're switching to a different dialog or a new one
            if (currentDialog != dialog)
            {
                currentDialog = dialog;
                selectedOptionIndex = 0;
                textReadToClipboard = false;
            }
        }

        public static void Reset()
        {
            currentDialog = null;
            selectedOptionIndex = 0;
            textReadToClipboard = false;
        }

        public static int GetSelectedIndex()
        {
            return selectedOptionIndex;
        }

        public static void MoveUp(int optionCount)
        {
            if (optionCount == 0) return;

            selectedOptionIndex--;
            if (selectedOptionIndex < 0)
            {
                selectedOptionIndex = optionCount - 1;
            }
        }

        public static void MoveDown(int optionCount)
        {
            if (optionCount == 0) return;

            selectedOptionIndex++;
            if (selectedOptionIndex >= optionCount)
            {
                selectedOptionIndex = 0;
            }
        }

        public static bool HasReadText()
        {
            return textReadToClipboard;
        }

        public static void MarkTextAsRead()
        {
            textReadToClipboard = true;
        }
    }
}
