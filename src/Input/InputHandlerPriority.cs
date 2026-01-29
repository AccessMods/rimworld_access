namespace RimWorldAccess
{
    /// <summary>
    /// Standard priority levels for keyboard input handlers.
    /// Lower numbers = higher priority (processed first).
    /// Uses semantic bands (spaced by 100) to allow easy insertion of new handlers.
    /// </summary>
    public static class InputHandlerPriority
    {
        // ===== Priority Bands (spaced by 100) =====

        /// <summary>Text input blocks everything</summary>
        public const int TextInput = 100;

        /// <summary>Modal overlays (inspection, quantity menus)</summary>
        public const int ModalOverlay = 200;

        /// <summary>Dialogs (caravan, trade, split)</summary>
        public const int Dialog = 300;

        /// <summary>Menus (quests, animals, research, pause)</summary>
        public const int Menu = 400;

        /// <summary>In-game shortcuts (draft, unforbid, gizmos)</summary>
        public const int Shortcut = 500;

        /// <summary>Global shortcuts (pause menu, info, orders)</summary>
        public const int Global = 600;

        // ===== Specific Handlers (use offsets within bands) =====

        // Text Input Band (100-199)
        /// <summary>Zone rename - needs unrestricted text input (was priority -1)</summary>
        public const int ZoneRename = TextInput + 0;

        /// <summary>Windowless dialog text fields (was priority -0.5)</summary>
        public const int WindowlessDialog = TextInput + 10;

        // Modal Overlay Band (200-299)
        /// <summary>Inspection overlay (was priority -0.25)</summary>
        public const int WindowlessInspection = ModalOverlay + 0;

        /// <summary>Quantity selection menu (was priority 0.25)</summary>
        public const int QuantityMenu = ModalOverlay + 10;

        // Dialog Band (300-399)
        /// <summary>Caravan formation dialog (was priority 0.3)</summary>
        public const int CaravanFormation = Dialog + 0;

        /// <summary>Split caravan dialog (was priority 0.35)</summary>
        public const int SplitCaravan = Dialog + 5;

        /// <summary>Yes/No confirmation dialogs (was priority 2)</summary>
        public const int ConfirmationDialog = Dialog + 10;

        /// <summary>Delete confirmation dialogs (was priority 1)</summary>
        public const int DeleteConfirmation = Dialog + 15;

        /// <summary>Area painting mode (was priority 2.5)</summary>
        public const int AreaPainting = Dialog + 20;

        /// <summary>Temperature control dialog (was priority 2.6)</summary>
        public const int TempControl = Dialog + 25;

        /// <summary>Transport pod loading dialog (was priority 0.32)</summary>
        public const int TransportPodLoading = Dialog + 30;

        /// <summary>Transport pod launch dialog (was priority 0.36)</summary>
        public const int TransportPodLaunch = Dialog + 35;

        // Menu Band (400-499)
        /// <summary>Save/Load menu (was priority 3)</summary>
        public const int WindowlessSaveMenu = Menu + 0;

        /// <summary>Pause menu (was priority 4)</summary>
        public const int WindowlessPauseMenu = Menu + 10;

        /// <summary>History tab (was priority 4.2)</summary>
        public const int HistoryTab = Menu + 15;

        /// <summary>Storyteller settings (was priority 4.5)</summary>
        public const int StorytellerSelection = Menu + 18;

        /// <summary>Options menu (was priority 4.6)</summary>
        public const int WindowlessOptionsMenu = Menu + 20;

        /// <summary>Schedule editor (was priority 4.55)</summary>
        public const int WindowlessSchedule = Menu + 25;

        /// <summary>Research detail view (was priority 4.6)</summary>
        public const int WindowlessResearchDetail = Menu + 30;

        /// <summary>Research tree (was priority 4.7)</summary>
        public const int WindowlessResearchMenu = Menu + 35;

        /// <summary>Quest list menu (was priority 4.73)</summary>
        public const int QuestMenu = Menu + 40;

        /// <summary>Wildlife menu (was priority 4.73)</summary>
        public const int WildlifeMenu = Menu + 45;

        /// <summary>Animals menu (was priority 4.74)</summary>
        public const int AnimalsMenu = Menu + 50;

        /// <summary>Assignment menu (was priority 4.78)</summary>
        public const int AssignMenu = Menu + 55;

        /// <summary>Storage settings menu (was priority 4.7791)</summary>
        public const int StorageSettingsMenu = Menu + 60;

        /// <summary>Float menu for orders (was priority 5)</summary>
        public const int WindowlessFloatMenu = Menu + 70;

        /// <summary>Settlement browser on world map (was priority 0)</summary>
        public const int SettlementBrowser = Menu + 75;

        /// <summary>Caravan stats menu (was priority 0.22)</summary>
        public const int CaravanStats = Menu + 80;

        /// <summary>Route planner on world map (was priority 0.6)</summary>
        public const int RoutePlanner = Menu + 82;

        /// <summary>World scanner (was priority 0.5)</summary>
        public const int WorldScanner = Menu + 84;

        // Shortcut Band (500-599)
        /// <summary>R key - draft toggle (was priority 6)</summary>
        public const int DraftShortcut = Shortcut + 0;

        /// <summary>Alt+F - unforbid all items (was priority 6.53)</summary>
        public const int UnforbidShortcut = Shortcut + 10;

        /// <summary>Alt+B - combat log (was priority 6.525)</summary>
        public const int CombatLog = Shortcut + 20;

        /// <summary>Schedule shortcuts (was priority 6.6)</summary>
        public const int ScheduleShortcut = Shortcut + 30;

        /// <summary>Gizmo navigation (was priority 4.78)</summary>
        public const int GizmoNavigation = Shortcut + 40;

        /// <summary>Health tab shortcut (was priority 4.81)</summary>
        public const int HealthTabShortcut = Shortcut + 50;

        /// <summary>Prisoner tab shortcut (was priority 4.85)</summary>
        public const int PrisonerTabShortcut = Shortcut + 55;

        /// <summary>Notification menu (was priority 4.77)</summary>
        public const int NotificationMenu = Shortcut + 60;

        // Global Band (600-699)
        /// <summary>F1 - global pause menu (was priority 8)</summary>
        public const int GlobalPauseMenu = Global + 0;

        /// <summary>I key - global inspection (was priority 9)</summary>
        public const int GlobalInspection = Global + 10;

        /// <summary>Right bracket - colonist orders (was priority 10)</summary>
        public const int GlobalColonistOrders = Global + 20;
    }
}
