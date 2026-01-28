using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Confirmation dialog for shelf linking when items are already in different groups.
    /// Uses Dialog_MessageBox for proper accessibility via MessageBoxAccessibilityPatch.
    /// </summary>
    public static class ShelfLinkingConfirmDialog
    {
        /// <summary>
        /// Shows a confirmation dialog for already-linked items using Dialog_MessageBox.
        /// The dialog is automatically accessible via MessageBoxAccessibilityPatch.
        /// </summary>
        /// <param name="alreadyLinked">List of items already in different groups</param>
        /// <param name="onYes">Action to execute if user confirms</param>
        /// <param name="onNo">Action to execute if user cancels</param>
        public static void Show(List<IStorageGroupMember> alreadyLinked, Action onYes, Action onNo)
        {
            // Build message with group names
            var groupDetails = alreadyLinked
                .Select(item => $"  - {ShelfLinkingHelper.GetStorageLabel(item)}: " +
                               $"linked to '{ShelfLinkingHelper.GetGroupName(item)}'")
                .ToList();

            string itemWord = alreadyLinked.Count == 1 ? "item is" : "items are";
            string message = $"{alreadyLinked.Count} {itemWord} already in different storage groups:\n\n" +
                            string.Join("\n", groupDetails) +
                            "\n\nMove them to this storage group?";

            Find.WindowStack.Add(new Dialog_MessageBox(
                message,
                "Yes, Link Anyway",
                onYes,
                "Don't Link",
                onNo,
                "Storage Linking",
                false
            ));
        }
    }
}
