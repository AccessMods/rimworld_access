namespace RimWorldAccess
{
    /// <summary>
    /// Standard priority levels for keyboard input handlers.
    ///
    /// PRIORITY SYSTEM DESIGN:
    /// - Lower numbers = higher priority (processed first)
    /// - Uses semantic bands (spaced by 100) for grouping related handlers
    /// - Within bands, use increments of 5 for consistent spacing
    /// - Explicit priorities are initialization-order independent and self-documenting
    ///
    /// WHY EXPLICIT PRIORITIES:
    /// - Deterministic: Priority is visible at compile-time, not runtime discovery order
    /// - Assembly.GetTypes() order is NOT guaranteed across runs
    /// - Easy to reason about: Can see relationships between handlers
    /// - Easy to insert: Need priority between 400 and 405? Use 402
    /// - Testable: Static priorities make tests predictable
    ///
    /// ADDING NEW HANDLERS:
    /// 1. Identify which band the handler belongs to (TextInput/ModalOverlay/Dialog/Menu/Shortcut/Global)
    /// 2. Find related handlers in that band
    /// 3. Use next available slot (increment of 5) after related handlers
    /// 4. If inserting between handlers, use intermediate value (e.g., 402 between 400 and 405)
    /// 5. Document WHY the handler needs its specific priority relative to others
    ///
    /// SPACING CONVENTION:
    /// - Standard increment: 5 (leaves room for insertions)
    /// - To insert between handlers: Use +1, +2, +3, or +4
    /// - Example: Priority 403 fits between 400 and 405
    /// </summary>
    public static class InputHandlerPriority
    {
        // ===== Priority Bands (spaced by 100) =====

        /// <summary>Text input blocks everything (100-199)</summary>
        public const int TextInput = 100;

        /// <summary>Modal overlays that appear over dialogs (200-299)</summary>
        public const int ModalOverlay = 200;

        /// <summary>Modal dialogs (300-399)</summary>
        public const int Dialog = 300;

        /// <summary>Standard menus and navigation (400-499)</summary>
        public const int Menu = 400;

        /// <summary>In-game shortcuts that work during gameplay (500-599)</summary>
        public const int Shortcut = 500;

        /// <summary>Global shortcuts that work everywhere (600-699)</summary>
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

        // Menu Band (400-499) - Standard menus with no specific ordering requirement
        // Grouped by functional similarity, spaced by 5

        /// <summary>Save/Load menu (was priority 3)</summary>
        public const int WindowlessSaveMenu = Menu + 0;

        /// <summary>Pause menu (was priority 4)</summary>
        public const int WindowlessPauseMenu = Menu + 5;

        /// <summary>Options menu (was priority 4.6)</summary>
        public const int WindowlessOptionsMenu = Menu + 10;

        /// <summary>History tab (was priority 4.2)</summary>
        public const int HistoryTab = Menu + 15;

        /// <summary>Storyteller settings (was priority 4.5)</summary>
        public const int StorytellerSelection = Menu + 20;

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
        public const int WindowlessFloatMenu = Menu + 65;

        /// <summary>Settlement browser on world map (was priority 0)</summary>
        public const int SettlementBrowser = Menu + 70;

        /// <summary>Caravan stats menu (was priority 0.22)</summary>
        public const int CaravanStats = Menu + 75;

        /// <summary>Route planner on world map (was priority 0.6)</summary>
        public const int RoutePlanner = Menu + 80;

        /// <summary>World scanner (was priority 0.5)</summary>
        public const int WorldScanner = Menu + 85;

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
