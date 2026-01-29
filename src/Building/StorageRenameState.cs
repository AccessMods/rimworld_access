using System;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldAccess
{
    /// <summary>
    /// Manages storage group naming/renaming with text input.
    /// Handles both creating new named groups and renaming existing groups.
    /// Uses TextInputHelper for shared text input logic.
    /// </summary>
    public static class StorageRenameState
    {
        private static bool isActive = false;
        private static IStorageGroupMember currentMember = null;
        private static string originalName = "";
        private static bool isCreatingNewGroup = false;

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the rename dialog for the specified storage member.
        /// If the member has no group, creates a new group when confirmed.
        /// </summary>
        public static void Open(IStorageGroupMember member)
        {
            if (member == null)
            {
                Log.Error("Cannot open storage rename dialog: member is null");
                return;
            }

            currentMember = member;
            isCreatingNewGroup = member.Group == null;

            if (isCreatingNewGroup)
            {
                TextInputHelper.SetText("");
                originalName = "";
                TolkHelper.Speak("Name this storage group. Type name and press Enter, Escape to cancel.");
            }
            else
            {
                originalName = member.Group.RenamableLabel ?? "";
                TextInputHelper.SetText("");  // Start empty
                TolkHelper.Speak($"Renaming {originalName}. Type new name and press Enter, Escape to cancel.");
            }

            isActive = true;
            Log.Message($"Opened storage rename dialog, creating new: {isCreatingNewGroup}");
        }

        /// <summary>
        /// Closes the rename dialog without saving.
        /// </summary>
        public static void Close()
        {
            isActive = false;
            currentMember = null;
            originalName = "";
            isCreatingNewGroup = false;
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
        /// Creates a new group if needed, validates name uniqueness.
        /// </summary>
        public static void Confirm()
        {
            if (!isActive || currentMember == null)
                return;

            string newName = TextInputHelper.CurrentText;

            // Validate: not empty
            if (string.IsNullOrWhiteSpace(newName))
            {
                TolkHelper.Speak("Cannot set empty name. Enter a name or press Escape to cancel.", SpeechPriority.High);
                return;
            }

            // Get the map for validation
            Map map = null;
            if (currentMember is Thing thing)
            {
                map = thing.Map;
            }

            if (map == null)
            {
                TolkHelper.Speak("Error: Cannot find map.", SpeechPriority.High);
                Close();
                return;
            }

            // Validate: unique across zones
            bool zoneConflict = map.zoneManager.AllZones.Any(z => z.label == newName);
            if (zoneConflict)
            {
                TolkHelper.Speak($"Name {newName} is already used by a zone. Choose a different name.", SpeechPriority.High);
                return;
            }

            // Validate: unique across storage groups (except current group if renaming)
            // Check all storage buildings on the map for groups with this name
            StorageGroup currentGroup = currentMember.Group;
            bool groupConflict = false;
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is IStorageGroupMember member && member.Group != null)
                {
                    if (member.Group.RenamableLabel == newName && member.Group != currentGroup)
                    {
                        groupConflict = true;
                        break;
                    }
                }
            }
            if (groupConflict)
            {
                TolkHelper.Speak($"Name {newName} is already used by another storage group. Choose a different name.", SpeechPriority.High);
                return;
            }

            try
            {
                if (isCreatingNewGroup)
                {
                    // Create new group
                    StorageGroup newGroup = map.storageGroups.NewGroup();
                    newGroup.InitFrom(currentMember);
                    currentMember.SetStorageGroup(newGroup);
                    newGroup.RenamableLabel = newName;
                    TolkHelper.Speak($"Created storage group {newName}", SpeechPriority.High);
                    Log.Message($"Created new storage group: {newName}");
                }
                else
                {
                    // Rename existing group
                    currentGroup.RenamableLabel = newName;
                    TolkHelper.Speak($"Renamed to {newName}", SpeechPriority.High);
                    Log.Message($"Renamed storage group from '{originalName}' to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                TolkHelper.Speak($"Error: {ex.Message}", SpeechPriority.High);
                Log.Error($"Error in storage rename: {ex}");
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

            TolkHelper.Speak("Cancelled");
            Log.Message("Storage rename cancelled");
            Close();
        }
    }
}
