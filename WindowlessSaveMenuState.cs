using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using RimWorld;

namespace RimWorldAccess
{
    /// <summary>
    /// Mode for save menu - either saving or loading.
    /// </summary>
    public enum SaveLoadMode
    {
        Save,
        Load
    }

    /// <summary>
    /// Manages a windowless save/load file browser.
    /// Provides keyboard navigation through save files without rendering UI.
    /// </summary>
    public static class WindowlessSaveMenuState
    {
        private static List<SaveFileInfo> saveFiles = null;
        private static int selectedIndex = 0;
        private static bool isActive = false;
        private static SaveLoadMode currentMode = SaveLoadMode.Load;
        private static string typedSaveName = "";

        public static bool IsActive => isActive;

        /// <summary>
        /// Opens the save/load menu.
        /// </summary>
        public static void Open(SaveLoadMode mode)
        {
            currentMode = mode;
            ReloadFiles();
            selectedIndex = 0;
            isActive = true;

            // For save mode, initialize with default name
            if (mode == SaveLoadMode.Save)
            {
                if (Faction.OfPlayer.HasName)
                {
                    typedSaveName = Faction.OfPlayer.Name;
                }
                else
                {
                    typedSaveName = SaveGameFilesUtility.UnusedDefaultFileName(Faction.OfPlayer.def.LabelCap);
                }
            }

            // Announce first file or save mode
            AnnounceCurrentState();
        }

        /// <summary>
        /// Closes the save/load menu.
        /// </summary>
        public static void Close()
        {
            saveFiles = null;
            selectedIndex = 0;
            isActive = false;
            typedSaveName = "";
        }

        /// <summary>
        /// Moves selection to next file.
        /// </summary>
        public static void SelectNext()
        {
            if (saveFiles == null || saveFiles.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % saveFiles.Count;
            AnnounceCurrentState();
        }

        /// <summary>
        /// Moves selection to previous file.
        /// </summary>
        public static void SelectPrevious()
        {
            if (saveFiles == null || saveFiles.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + saveFiles.Count) % saveFiles.Count;
            AnnounceCurrentState();
        }

        /// <summary>
        /// Executes save or load on the selected file.
        /// </summary>
        public static void ExecuteSelected()
        {
            if (currentMode == SaveLoadMode.Save)
            {
                ExecuteSave();
            }
            else
            {
                ExecuteLoad();
            }
        }

        /// <summary>
        /// Deletes the currently selected save file.
        /// </summary>
        public static void DeleteSelected()
        {
            if (saveFiles == null || saveFiles.Count == 0)
                return;

            if (selectedIndex < 0 || selectedIndex >= saveFiles.Count)
                return;

            SaveFileInfo selectedFile = saveFiles[selectedIndex];
            string fileName = Path.GetFileNameWithoutExtension(selectedFile.FileName);

            // Open confirmation
            ClipboardHelper.CopyToClipboard($"Delete {fileName}? Press Enter to confirm, Escape to cancel");
            WindowlessDeleteConfirmationState.Open(selectedFile.FileInfo, () => {
                // After deletion, reload and reopen this menu
                ReloadFiles();
                if (selectedIndex >= saveFiles.Count)
                {
                    selectedIndex = Math.Max(0, saveFiles.Count - 1);
                }
                isActive = true; // Reactivate this menu
                AnnounceCurrentState();
            });

            // Temporarily deactivate while confirmation is active
            isActive = false;
        }

        private static void ExecuteSave()
        {
            string saveName = typedSaveName;

            if (string.IsNullOrEmpty(saveName))
            {
                ClipboardHelper.CopyToClipboard("Need a name for the save file");
                return;
            }

            saveName = GenFile.SanitizedFileName(saveName);

            // Close menu before saving
            Close();

            // Perform the save
            LongEventHandler.QueueLongEvent(delegate
            {
                GameDataSaveLoader.SaveGame(saveName);
            }, "SavingLongEvent", doAsynchronously: false, null);

            Messages.Message("SavedAs".Translate(saveName), MessageTypeDefOf.SilentInput, historical: false);
            PlayerKnowledgeDatabase.Save();

            ClipboardHelper.CopyToClipboard($"Saved as {saveName}");
        }

        private static void ExecuteLoad()
        {
            if (saveFiles == null || saveFiles.Count == 0)
            {
                ClipboardHelper.CopyToClipboard("No save files available");
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= saveFiles.Count)
                return;

            SaveFileInfo selectedFile = saveFiles[selectedIndex];
            string fileName = Path.GetFileNameWithoutExtension(selectedFile.FileName);

            // Close menu before loading
            Close();

            // Perform the load
            LongEventHandler.QueueLongEvent(delegate
            {
                GameDataSaveLoader.LoadGame(fileName);
            }, "LoadingLongEvent", doAsynchronously: true, GameAndMapInitExceptionHandlers.ErrorWhileLoadingGame);

            ClipboardHelper.CopyToClipboard($"Loading {fileName}");
        }

        private static void ReloadFiles()
        {
            saveFiles = new List<SaveFileInfo>();

            foreach (FileInfo file in GenFilePaths.AllSavedGameFiles)
            {
                try
                {
                    saveFiles.Add(new SaveFileInfo(file));
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception loading save file {file.Name}: {ex}");
                }
            }

            // Sort by last write time, most recent first
            saveFiles = saveFiles.OrderByDescending(f => f.LastWriteTime).ToList();
        }

        private static void AnnounceCurrentState()
        {
            if (currentMode == SaveLoadMode.Save)
            {
                if (saveFiles != null && saveFiles.Count > 0 && selectedIndex >= 0 && selectedIndex < saveFiles.Count)
                {
                    SaveFileInfo file = saveFiles[selectedIndex];
                    string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    ClipboardHelper.CopyToClipboard($"Overwrite: {fileName} - {file.LastWriteTime:yyyy-MM-dd HH:mm}");
                }
                else
                {
                    ClipboardHelper.CopyToClipboard($"Save as: {typedSaveName}");
                }
            }
            else // Load mode
            {
                if (saveFiles != null && saveFiles.Count > 0 && selectedIndex >= 0 && selectedIndex < saveFiles.Count)
                {
                    SaveFileInfo file = saveFiles[selectedIndex];
                    string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    ClipboardHelper.CopyToClipboard($"Load: {fileName} - {file.LastWriteTime:yyyy-MM-dd HH:mm}");
                }
                else
                {
                    ClipboardHelper.CopyToClipboard("No save files available");
                }
            }
        }

        /// <summary>
        /// Goes back to the pause menu.
        /// </summary>
        public static void GoBack()
        {
            Close();
            WindowlessPauseMenuState.Open();
        }
    }

    /// <summary>
    /// Handles confirmation for deleting save files.
    /// </summary>
    public static class WindowlessDeleteConfirmationState
    {
        private static bool isActive = false;
        private static FileInfo fileToDelete = null;
        private static Action onDeleteComplete = null;

        public static bool IsActive => isActive;

        public static void Open(FileInfo file, Action onComplete)
        {
            isActive = true;
            fileToDelete = file;
            onDeleteComplete = onComplete;
        }

        public static void Confirm()
        {
            if (!isActive || fileToDelete == null)
                return;

            string fileName = fileToDelete.Name;
            fileToDelete.Delete();
            ClipboardHelper.CopyToClipboard($"Deleted {fileName}");

            Action callback = onDeleteComplete;
            Close();
            callback?.Invoke();
        }

        public static void Cancel()
        {
            if (!isActive)
                return;

            ClipboardHelper.CopyToClipboard("Delete cancelled");

            Action callback = onDeleteComplete;
            Close();
            callback?.Invoke();
        }

        private static void Close()
        {
            isActive = false;
            fileToDelete = null;
            onDeleteComplete = null;
        }
    }
}
