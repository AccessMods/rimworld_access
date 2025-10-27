# RimWorld Access - Comprehensive TODO List

This document tracks all remaining features to implement for the RimWorld Access accessibility mod. Items are prioritized by importance for gameplay accessibility.

---

## Currently Implemented ✓

- ✓ Main menu navigation
- ✓ Map navigation (arrow keys)
- ✓ Architect menu (category, tool, material selection, placement)
- ✓ Zone creation and management (stockpiles, growing zones)
- ✓ Storage settings and filtering
- ✓ Plant selection for growing zones
- ✓ Work assignment menu (Alt+W)
- ✓ Basic pawn info reading (Alt+H/N/G/S/T/R/C)
- ✓ Notification system (messages, letters, alerts)
- ✓ Dialog and float menu navigation
- ✓ Time controls (pause, speed adjustment)
- ✓ Pawn selection and detail inspection
- ✓ Colonist editor (character creation)
- ✓ Scenario selection
- ✓ Storyteller selection
- ✓ World generation parameters
- ✓ Starting site selection
- ✓ Forbid/unforbid toggle (F key)
- ✓ Jump menu (J key for locations)
- ✓ Windowless pause menu
- ✓ Windowless save menu
- ✓ Windowless options menu

---

## PRIORITY 1: CRITICAL - Essential for Basic Gameplay

### 1. Research Management Interface
**Priority: CRITICAL**

**What it does:** The research tree allows players to unlock new technologies, buildings, and capabilities. This is essential for game progression.

**Why needed:** Without research access, players cannot progress beyond primitive technology. Research is mandatory for most playthroughs.

**Implementation requirements:**
- Patch `MainTabWindow_Research` (RimWorld/MainTabWindow_Research.cs)
- Create `ResearchNavigationState.cs` to track selected project
- Implement tree navigation (up/down for projects, left/right for prerequisites)
- Display research info: name, description, prerequisites, cost, current progress
- Handle research selection and starting/stopping research
- Show available vs locked projects
- Display research bench requirements

**Key game files:**
- `RimWorld/MainTabWindow_Research.cs` - Main research UI
- `RimWorld/ResearchManager.cs` - Research state tracking
- `RimWorld/ResearchProjectDef.cs` - Research definitions
- `Verse/Find.cs` - Access to `Find.ResearchManager`

---

### 2. Trading Interface
**Priority: CRITICAL**

**What it does:** Trading with visiting caravans and settlements is essential for acquiring resources, selling excess goods, and obtaining rare items.

**Why needed:** Trading is often the only way to obtain certain items (components, medicine, advanced materials). Cannot play effectively without trade access.

**Implementation requirements:**
- Patch `Dialog_Trade` (RimWorld/Dialog_Trade.cs)
- Create `TradingNavigationState.cs` for menu navigation
- Implement column navigation (your items, their items, trade queue)
- Support item browsing with filtering
- Handle quantity adjustment
- Display prices, silver counts, and trade value calculations
- Support gift mode
- Handle accept/cancel trade actions

**Key game files:**
- `RimWorld/Dialog_Trade.cs:72-580` - Main trading dialog
- `RimWorld/Tradeable.cs` - Tradeable item wrapper
- `RimWorld/TradeUtility.cs` - Trade calculations

---

### 3. Combat Draft/Undraft System
**Priority: CRITICAL**

**What it does:** The draft system allows direct control of colonists for combat, positioning, and emergency actions.

**Why needed:** Essential for defending against raids and controlling combat situations. Without this, cannot respond to threats effectively.

**Implementation requirements:**
- Patch `Selector.cs` or draft gizmo commands
- Create hotkey (D key?) for toggle draft on selected pawns
- Announce draft state changes
- Display drafted pawn count
- Handle group draft/undraft operations
- Integrate with existing pawn selection system

**Key game files:**
- `RimWorld/Pawn_DraftController.cs` - Draft state management
- `Verse/Selector.cs` - Pawn selection
- `RimWorld/Command_Toggle.cs` - Draft button gizmo

---

### 4. Medical Operations Interface
**Priority: CRITICAL**

**What it does:** Medical operations menu (bills on medical beds) manages surgeries, treatments, and medical procedures.

**Why needed:** Critical for treating injuries, installing bionics, and performing life-saving procedures. Cannot manage pawn health effectively without this.

**Implementation requirements:**
- Enhance existing `ITab_Bills` handling for medical beds
- Create `MedicalOperationsState.cs` for surgery queue
- Display available operations for selected patient
- Show body part selection options
- Handle operation queueing and removal
- Display success chances and requirements
- Support medicine quality selection

**Key game files:**
- `RimWorld/ITab_Bills.cs` - Bill/operation interface
- `RimWorld/Dialog_BillConfig.cs` - Surgery configuration
- `RimWorld/RecipeDef.cs` - Surgery recipes
- `RimWorld/HealthCardUtility.cs:247-475` - Medical operations listing

---

### 5. Production Bills Management
**Priority: CRITICAL**

**What it does:** Production bills control what items are crafted at workbenches (weapons, clothing, food, components, etc.).

**Why needed:** Production is core to survival - making meals, crafting equipment, creating medicine. Cannot sustain colony without production access.

**Implementation requirements:**
- Enhance existing `ITab_Bills` handling for production benches
- Create `ProductionBillsState.cs` for bill management
- Support adding new bills from recipe list
- Handle bill configuration (repeat mode, ingredients, quality, storage)
- Display bill queue and priority order
- Support bill reordering and deletion
- Show material filters and ingredient radius

**Key game files:**
- `RimWorld/ITab_Bills.cs:1-391` - Bills tab interface
- `RimWorld/Dialog_BillConfig.cs:1-736` - Bill configuration dialog
- `RimWorld/Bill.cs` - Bill data structure
- `RimWorld/BillStack.cs` - Bill queue management

---

## PRIORITY 2: HIGH - Important for Typical Gameplay

### 6. Faction Relations Interface
**Priority: HIGH**

**What it does:** The factions tab shows relationships with other factions, goodwill levels, and diplomatic options.

**Why needed:** Important for managing alliances, preventing hostile factions, and accessing faction-specific features (quests, caravans, settlements).

**Implementation requirements:**
- Patch `MainTabWindow_Factions` (RimWorld/MainTabWindow_Factions.cs)
- Create `FactionsNavigationState.cs` for faction list navigation
- Display faction names, goodwill levels, and relationships
- Show faction bases and settlements on world map
- Support diplomatic actions (requests, hostile actions)
- Display faction info (leader, ideoligion, characteristics)

**Key game files:**
- `RimWorld/MainTabWindow_Factions.cs` - Factions overview
- `RimWorld/Faction.cs` - Faction data
- `RimWorld/FactionRelation.cs` - Relationship tracking

---

### 7. Outfit/Apparel Policy Management
**Priority: HIGH**

**What it does:** Outfit policies control what clothing pawns automatically equip based on temperature, protection, and restrictions.

**Why needed:** Essential for managing colonist equipment efficiently. Without this, must manually manage all clothing which is impractical.

**Implementation requirements:**
- Patch outfit assignment dialog
- Create `OutfitPolicyState.cs` for policy editing
- Display current outfit policy for selected pawn
- Support creating/editing/deleting policies
- Handle apparel filtering (by quality, hit points, temperature range)
- Display forced wear items

**Key game files:**
- `RimWorld/Dialog_ManageOutfits.cs` - Outfit management dialog
- `RimWorld/OutfitDatabase.cs` - Outfit storage
- `RimWorld/ApparelPolicy.cs:1-178` - Outfit policy definition
- `RimWorld/ITab_Pawn_Gear.cs` - Gear tab with outfit assignment

---

### 8. Drug Policy Management
**Priority: HIGH**

**What it does:** Drug policies control when pawns take medicine, recreational drugs, and combat enhancers.

**Why needed:** Important for medical treatment automation and preventing drug addiction. Affects colony health and efficiency.

**Implementation requirements:**
- Patch drug policy dialog
- Create `DrugPolicyState.cs` for policy editing
- Display drug rules (take for joy, medical, scheduled)
- Support drug threshold configuration
- Handle addiction risk warnings
- Display current assignments

**Key game files:**
- `RimWorld/Dialog_ManageDrugPolicies.cs` - Drug policy dialog
- `RimWorld/DrugPolicy.cs:1-117` - Drug policy definition
- `RimWorld/DrugPolicyDatabase.cs` - Policy storage

---

### 9. Food Restriction Policies
**Priority: HIGH**

**What it does:** Food policies restrict what meals and ingredients pawns are allowed to eat.

**Why needed:** Important for managing food quality, preventing wastage of valuable ingredients, and accommodating dietary restrictions.

**Implementation requirements:**
- Patch food restriction dialog
- Create `FoodPolicyState.cs` for policy editing
- Display food categories (raw, simple meals, fine meals, etc.)
- Support checkbox navigation for allowed foods
- Handle policy assignment to pawns

**Key game files:**
- `RimWorld/Dialog_ManageFoodRestrictions.cs` - Food restriction dialog
- `RimWorld/FoodRestriction.cs` - Food policy definition
- `RimWorld/FoodRestrictionDatabase.cs` - Policy storage

---

### 10. Schedule/Timetable Interface
**Priority: HIGH**

**What it does:** The schedule tab controls when pawns work, sleep, and have recreation throughout the day (24-hour grid).

**Why needed:** Important for optimizing pawn efficiency and preventing exhaustion. Affects colony productivity significantly.

**Implementation requirements:**
- Patch `MainTabWindow_Schedule` (RimWorld/MainTabWindow_Schedule.cs)
- Create `ScheduleNavigationState.cs` for grid navigation
- Implement 2D navigation (pawn selection + hour selection)
- Display current schedule for selected hour
- Support assignment changes (work/sleep/anything/recreation)
- Handle policy presets
- Display all pawns' schedules simultaneously

**Key game files:**
- `RimWorld/MainTabWindow_Schedule.cs` - Schedule interface
- `RimWorld/Pawn_TimetableTracker.cs` - Individual schedules
- `RimWorld/TimeAssignmentDef.cs` - Schedule assignment types

---

### 11. Pawn Assignment Tab (Beds/Areas)
**Priority: HIGH**

**What it does:** The assign tab controls bed ownership, area restrictions, and master assignments for animals.

**Why needed:** Important for colonist happiness (private bedrooms), managing animal zones, and bonding animals with handlers.

**Implementation requirements:**
- Patch `MainTabWindow_Assign` (RimWorld/MainTabWindow_Assign.cs)
- Create `AssignmentNavigationState.cs` for grid navigation
- Display bed assignments per pawn
- Show area restrictions
- Support changing assignments
- Handle prisoner and slave assignments

**Key game files:**
- `RimWorld/MainTabWindow_Assign.cs` - Assignment interface
- `RimWorld/Pawn_Ownership.cs` - Bed ownership
- `Verse/Pawn.cs` - Pawn area restrictions

---

### 12. Animal Management Tab
**Priority: HIGH**

**What it does:** The animals tab shows all colony animals, their training progress, bonds, and health status.

**Why needed:** Important for managing animal workforce (haulers, fighters, egg layers). Animals are valuable colony assets.

**Implementation requirements:**
- Patch `MainTabWindow_Animals` (RimWorld/MainTabWindow_Animals.cs)
- Create `AnimalManagementState.cs` for animal list navigation
- Display animal names, training levels, bonds
- Show health status and zones
- Support training assignment
- Display master assignments
- Handle slaughter marking

**Key game files:**
- `RimWorld/MainTabWindow_Animals.cs` - Animals interface
- `RimWorld/Pawn_TrainingTracker.cs` - Training progress
- `Verse/Pawn.cs` - Animal data

---

### 13. Quest Management Interface
**Priority: HIGH**

**What it does:** The quests tab displays active and available quests with objectives, rewards, and expiration timers.

**Why needed:** Quests provide rewards, story progression, and special opportunities. Important for advanced gameplay and specific ending paths.

**Implementation requirements:**
- Patch `MainTabWindow_Quests` (RimWorld/MainTabWindow_Quests.cs)
- Create `QuestNavigationState.cs` for quest list
- Display quest names, descriptions, and objectives
- Show reward information
- Display expiration timers
- Support quest acceptance/rejection
- Show active quest progress

**Key game files:**
- `RimWorld/MainTabWindow_Quests.cs` - Quests interface
- `RimWorld/Quest.cs` - Quest data structure
- `RimWorld/QuestManager.cs` - Quest tracking

---

### 14. Prisoner Management Enhancements
**Priority: HIGH**

**What it does:** Prisoner interaction options control recruitment, execution, release, and medical treatment.

**Why needed:** Prisoners are a key recruitment source and diplomatic consideration. Need proper management tools.

**Implementation requirements:**
- Enhance existing pawn info with prisoner-specific commands
- Create `PrisonerCommandState.cs` for interaction options
- Support recruitment attempts
- Handle prisoner release/execution
- Display mood, resistance, and recruitment chance
- Support cell assignment

**Key game files:**
- `RimWorld/ITab_Pawn_Guest.cs` - Prisoner/guest tab
- `RimWorld/Pawn_GuestTracker.cs:1-483` - Guest/prisoner state
- `RimWorld/InteractionWorker_RecruitAttempt.cs` - Recruitment

---

### 15. Caravan Formation Interface
**Priority: HIGH**

**What it does:** Caravan formation dialog selects pawns, animals, and items for overland travel.

**Why needed:** Essential for visiting other settlements, trading, completing quests, and moving to new map locations.

**Implementation requirements:**
- Patch `Dialog_FormCaravan` (RimWorld.Planet/Dialog_FormCaravan.cs)
- Create `CaravanFormationState.cs` for member selection
- Support pawn selection/deselection
- Handle animal selection
- Display item packing lists
- Show caravan capacity and travel time estimates
- Support destination selection

**Key game files:**
- `RimWorld.Planet/Dialog_FormCaravan.cs:1-674` - Caravan formation
- `RimWorld.Planet/CaravanFormingUtility.cs` - Formation logic
- `RimWorld.Planet/Caravan.cs` - Caravan data

---

## PRIORITY 3: MEDIUM - Quality of Life Features

### 16. Detailed Inspector Tab Navigation
**Priority: MEDIUM**

**What it does:** Inspector tabs show detailed information about selected objects/pawns (Character, Gear, Social, Training, Health, etc.).

**Why needed:** Currently basic info is readable with Alt+keys, but full tab navigation would provide comprehensive access to all details.

**Implementation requirements:**
- Patch `InspectPaneFiller.cs` (RimWorld/InspectPaneFiller.cs)
- Create `InspectorTabState.cs` for tab switching
- Implement per-tab navigation for complex tabs
- Support tab-specific keyboard shortcuts
- Display all tab data in readable format

**Key game files:**
- `RimWorld/InspectPaneFiller.cs:1-279` - Inspector rendering
- `RimWorld/ITab_*.cs` - Individual tab implementations
- `Verse/InspectTabBase.cs` - Tab base class

---

### 17. Gizmo/Command Reading
**Priority: MEDIUM**

**What it does:** Gizmos are context-sensitive command buttons that appear when selecting objects (e.g., "Force attack", "Deconstruct", "Install").

**Why needed:** Many important commands are only available through gizmos. Currently must use mouse or guess hotkeys.

**Implementation requirements:**
- Create hotkey to list available gizmos for selected object
- Create `GizmoNavigationState.cs` for gizmo selection
- Display gizmo labels and descriptions
- Support gizmo activation via keyboard
- Handle gizmo hotkeys announcement

**Key game files:**
- `Verse/Command.cs` - Base gizmo class
- `RimWorld/Command_*.cs` - Specific command types
- `Verse/GizmoGridDrawer.cs` - Gizmo rendering

---

### 18. Thing/Item Inspection Details
**Priority: MEDIUM**

**What it does:** When selecting objects on the map, detailed information appears in the inspector (quality, hit points, contents, etc.).

**Why needed:** Important for assessing item value, condition, and suitability for tasks.

**Implementation requirements:**
- Enhance existing map navigation to announce thing details
- Create hotkey for detailed thing inspection at cursor
- Display quality, hit points, material, stack count
- Show special properties (beauty, cleanliness, temperature)
- Handle storage contents listing

**Key game files:**
- `Verse/Thing.cs` - Base thing class
- `RimWorld/CompQuality.cs` - Quality component
- `Verse/ThingWithComps.cs` - Thing with components

---

### 19. Room Stats and Quality
**Priority: MEDIUM**

**What it does:** Room stats show impressiveness, cleanliness, space, and other factors affecting mood and function.

**Why needed:** Room quality significantly affects colonist mood. Need to know room stats for optimization.

**Implementation requirements:**
- Create hotkey to read room stats at current cursor position
- Display room type (bedroom, dining room, hospital, etc.)
- Show impressiveness, wealth, cleanliness, space
- List room effects on mood
- Display room owners if applicable

**Key game files:**
- `Verse/Room.cs:1-408` - Room data and stats
- `RimWorld/RoomStatWorker.cs` - Room stat calculations
- `RimWorld/RoomRoleWorker.cs` - Room type detection

---

### 20. World Map Navigation
**Priority: MEDIUM**

**What it does:** The world map shows settlements, terrain, and travel routes on the planetary scale.

**Why needed:** Important for planning travel, selecting trade destinations, and understanding geographic context.

**Implementation requirements:**
- Patch world rendering or world interaction
- Create `WorldMapNavigationState.cs` for tile selection
- Implement world tile navigation with arrow keys
- Display tile information (biome, terrain, temperature, settlements)
- Support settlement/site selection
- Show travel times from current location

**Key game files:**
- `RimWorld.Planet/WorldInterface.cs` - World interaction
- `RimWorld.Planet/WorldGrid.cs` - Tile grid
- `RimWorld.Planet/WorldInspectPane.cs:1-71` - World inspector
- `Verse/Find.cs` - Access to `Find.World`

---

### 21. Temperature and Weather Reading
**Priority: MEDIUM**

**What it does:** Temperature and weather affect colonist comfort, crop growth, and survival.

**Why needed:** Important for planning heating/cooling and understanding environmental threats.

**Implementation requirements:**
- Create hotkey to read current temperature at cursor
- Display outdoor vs indoor temperature
- Show current weather conditions
- Display temperature trends
- List weather effects (crop growth, movement speed)

**Key game files:**
- `Verse/Map.cs` - Map with weather info
- `RimWorld/WeatherManager.cs` - Weather system
- `Verse/TemperatureCache.cs` - Temperature data

---

### 22. Power Grid Information
**Priority: MEDIUM**

**What it does:** Power grid status shows power production, consumption, and battery charge levels.

**Why needed:** Power management is critical for many buildings. Need to monitor power status.

**Implementation requirements:**
- Create hotkey to read power grid stats
- Display total power production and consumption
- Show battery charge levels
- List connected vs disconnected buildings
- Display power shortfalls

**Key game files:**
- `RimWorld/PowerNet.cs:1-130` - Power network
- `RimWorld/PowerNetManager.cs` - Power management
- `RimWorld/CompPower.cs` - Power components

---

### 23. Ideology and Precepts Interface
**Priority: MEDIUM**

**What it does:** The ideology tab shows colony beliefs, precepts, rituals, and religious structure.

**Why needed:** Ideology system affects mood, available actions, and social dynamics. Important for DLC content.

**Implementation requirements:**
- Patch `MainTabWindow_Ideos` (RimWorld/MainTabWindow_Ideos.cs)
- Create `IdeologyNavigationState.cs` for precept navigation
- Display ideology name and memes
- List precepts and their effects
- Show ritual requirements
- Display roles and role assignments

**Key game files:**
- `RimWorld/MainTabWindow_Ideos.cs` - Ideology interface
- `RimWorld/Ideo.cs` - Ideology data
- `RimWorld/Precept.cs` - Precept definitions

---

### 24. Ritual Execution Interface
**Priority: MEDIUM**

**What it does:** Ritual assignment dialog selects participants and roles for ceremonies.

**Why needed:** Rituals are important for ideology gameplay and provide colony-wide mood buffs.

**Implementation requirements:**
- Patch ritual assignment dialogs
- Create `RitualAssignmentState.cs` for role selection
- Display available roles
- Support participant assignment
- Show ritual requirements and expected outcome
- Handle ritual staging

**Key game files:**
- `RimWorld/Command_Ritual.cs` - Ritual command
- `RimWorld/RitualRoleAssignments.cs` - Role assignments
- `RimWorld/RitualOutcomeComp.cs` - Outcome calculation

---

### 25. Gene and Biotech Interface
**Priority: MEDIUM**

**What it does:** Gene tab shows inherited and installed genes, their effects, and xenotype information.

**Why needed:** Important for Biotech DLC content. Genes significantly affect pawn capabilities.

**Implementation requirements:**
- Patch gene-related interfaces
- Create `GeneNavigationState.cs` for gene lists
- Display gene names and effects
- Show xenotype information
- Display metabolic and complexity costs
- Handle gene implantation/extraction

**Key game files:**
- `RimWorld/ITab_Pawn_Genes.cs` - Gene tab
- `RimWorld/Gene.cs` - Gene data
- `RimWorld/Pawn_GeneTracker.cs` - Gene tracking

---

### 26. History Tab and Records
**Priority: MEDIUM**

**What it does:** History tab shows colony statistics, graphs, and records over time.

**Why needed:** Useful for tracking colony progress and achievements. Less critical but valuable for understanding performance.

**Implementation requirements:**
- Patch `MainTabWindow_History` (RimWorld/MainTabWindow_History.cs)
- Create `HistoryNavigationState.cs` for graph/stat selection
- Display statistics in text format
- List records and record holders
- Show trend information
- Support date range selection

**Key game files:**
- `RimWorld/MainTabWindow_History.cs` - History interface
- `RimWorld/PlaySettings.cs` - Game records
- `RimWorld/RecordsUtility.cs` - Record tracking

---

### 27. Mod Settings Interface
**Priority: MEDIUM**

**What it does:** Mod settings dialog allows configuration of installed mods.

**Why needed:** Important for customizing gameplay and configuring other mods.

**Implementation requirements:**
- Patch `Dialog_ModSettings` (Verse/Dialog_ModSettings.cs)
- Create `ModSettingsNavigationState.cs` for mod selection
- Display mod names and setting categories
- Support setting value changes
- Handle various setting types (checkboxes, sliders, dropdowns)

**Key game files:**
- `Verse/Dialog_ModSettings.cs` - Mod settings dialog
- `Verse/Mod.cs` - Mod base class with settings

---

### 28. Stockpile Priority System
**Priority: MEDIUM**

**What it does:** Stockpile priority determines which storage areas haulers fill first.

**Why needed:** Important for organizing storage efficiently. Currently can create zones but not set priorities easily.

**Implementation requirements:**
- Enhance existing zone creation with priority announcement
- Create hotkey to cycle stockpile priority
- Display current priority level
- Support priority adjustment for selected zone

**Key game files:**
- `Verse/Zone_Stockpile.cs` - Stockpile zone
- `Verse/StorageSettings.cs` - Storage configuration
- `Verse/Area.cs` - Base area class

---

### 29. Growing Zone Plant Type Reading
**Priority: MEDIUM**

**What it does:** Growing zones can be configured to grow specific plants.

**Why needed:** Currently can set plant type but better feedback on current setting would be helpful.

**Implementation requirements:**
- Enhance growing zone inspection
- Display currently assigned plant type
- Show expected yield and growth time
- Display soil quality at location

**Key game files:**
- `Verse/Zone_Growing.cs` - Growing zone
- `RimWorld/Plant.cs` - Plant data
- `Verse/PlantDef.cs` - Plant definitions

---

### 30. Corpse and Death Management
**Priority: MEDIUM**

**What it does:** Managing dead bodies (burial, cremation, butchering) is important for mood and resources.

**Why needed:** Improper corpse handling causes mood penalties. Need efficient management.

**Implementation requirements:**
- Create hotkey to list corpses on map
- Display corpse locations and states
- Support corpse-related commands (bury, cremate, butcher)
- Show grave assignments
- Display corpse rot status

**Key game files:**
- `Verse/Corpse.cs` - Corpse object
- `RimWorld/Building_Grave.cs` - Grave buildings
- `RimWorld/Alert_ColonistLeftUnburied.cs` - Corpse alerts

---

## PRIORITY 4: LOW - Nice to Have

### 31. Beauty and Environment Reading
**Priority: LOW**

**What it does:** Beauty stat affects colonist mood. Environment quality combines beauty, room stats, and outdoors/lit status.

**Why needed:** Useful for optimizing base layout for mood. Less critical than core systems.

**Implementation requirements:**
- Create hotkey to read beauty and environment stats
- Display beauty value at cursor location
- Show environment factors affecting mood
- List nearby beautiful/ugly objects

**Key game files:**
- `RimWorld/BeautyUtility.cs` - Beauty calculations
- `Verse/Thing.cs` - Thing beauty stat

---

### 32. Detailed Combat Stats Reading
**Priority: LOW**

**What it does:** Combat stat details show hit chances, armor penetration, damage calculations, etc.

**Why needed:** Useful for optimizing equipment loadouts. Less critical for basic gameplay.

**Implementation requirements:**
- Enhance weapon info reading
- Display detailed hit chance calculations
- Show armor values and penetration
- Display DPS and accuracy stats

**Key game files:**
- `Verse/Verb.cs` - Weapon verbs
- `RimWorld/StatPart_*.cs` - Stat calculation parts
- `RimWorld/VerbProperties.cs` - Weapon properties

---

### 33. Advanced Bill Filters (Ingredient Search)
**Priority: LOW**

**What it does:** Production bills can filter ingredients by quality, hit points, and specific items.

**Why needed:** Useful for fine-tuning production. Basic production works without this.

**Implementation requirements:**
- Enhance production bill interface
- Support ingredient filter configuration
- Display ingredient radius settings
- Show ingredient search priorities

**Key game files:**
- `RimWorld/Bill.cs` - Bill with filters
- `RimWorld/BillStoreModeDef.cs` - Storage modes
- `RimWorld/ThingFilter.cs` - Item filtering

---

### 34. Art and Quality Descriptions
**Priority: LOW**

**What it does:** Art pieces have procedurally generated descriptions and quality ratings.

**Why needed:** Flavor content, less critical than functional systems.

**Implementation requirements:**
- Enhance thing inspection for art
- Display art descriptions
- Show art beauty and quality
- Read tale/story content

**Key game files:**
- `RimWorld/CompArt.cs` - Art component
- `RimWorld/Tale.cs` - Story content
- `RimWorld/ITab_Art.cs` - Art tab

---

### 35. Detailed Skill Experience Reading
**Priority: LOW**

**What it does:** Skill tab shows exact experience points and progress to next level.

**Why needed:** Useful for tracking learning progress. Basic skill levels already announced.

**Implementation requirements:**
- Enhance pawn skill info
- Display exact XP values
- Show XP gain rates
- Display skill decay status

**Key game files:**
- `RimWorld/SkillRecord.cs` - Skill data
- `RimWorld/Pawn_SkillTracker.cs` - Skill tracking

---

### 36. Pathfinding Visualization/Feedback
**Priority: LOW**

**What it does:** Show where pawns plan to walk and what routes they'll take.

**Why needed:** Helpful for understanding pawn behavior and identifying pathfinding issues.

**Implementation requirements:**
- Create hotkey to read path from selected pawn to cursor
- Display path distance and estimated time
- Show obstacles and blockages
- Identify pathfinding failures

**Key game files:**
- `Verse/PathFinder.cs` - Pathfinding system
- `Verse/Pawn_PathFollower.cs` - Path following

---

### 37. Mechanitor and Mech Control
**Priority: LOW**

**What it does:** Mechanitor system controls mechanoids (robotic workers/fighters) in Biotech DLC.

**Why needed:** Important for Biotech DLC content but not base game.

**Implementation requirements:**
- Patch mechanitor interfaces
- Create mech command navigation
- Display mech status and capabilities
- Support mech commands and recharging

**Key game files:**
- `RimWorld/CompOverseerSubject.cs` - Mech control
- `RimWorld/MainTabWindow_Mechs.cs` - Mech tab
- `RimWorld/Pawn_MechanitorTracker.cs` - Mechanitor tracking

---

### 38. Advanced World Features (Sites, Quests)
**Priority: LOW**

**What it does:** World map shows quest sites, ruins, and points of interest.

**Why needed:** Useful for advanced gameplay but not essential for basic colony management.

**Implementation requirements:**
- Enhance world map navigation with site detection
- Display site types and contents
- Show quest locations
- Support site interaction

**Key game files:**
- `RimWorld.Planet/Site.cs` - World sites
- `RimWorld.Planet/WorldObject.cs` - World objects
- `RimWorld.Planet/SitePart.cs` - Site components

---

### 39. Learning Helper/Debug Features
**Priority: LOW**

**What it does:** Provide meta-information about game state, available commands, and learning resources.

**Why needed:** Helps new players learn the mod's capabilities.

**Implementation requirements:**
- Create help menu accessible via hotkey
- Display available commands and hotkeys
- Provide context-sensitive help
- Show mod feature list

**Implementation approach:**
- Create `HelpMenuState.cs` with command listing
- Document all hotkeys in accessible format

---

### 40. Pregnancy and Reproduction System
**Priority: LOW**

**What it does:** Biotech DLC adds pregnancy, babies, and child development.

**Why needed:** Important for Biotech DLC but not base game.

**Implementation requirements:**
- Patch pregnancy-related interfaces
- Display pregnancy status and progress
- Show baby/child development stages
- Support baby care commands

**Key game files:**
- `RimWorld/Hediff_Pregnant.cs` - Pregnancy tracking
- `RimWorld/Hediff_Labor.cs` - Birth mechanics
- `RimWorld/Pawn_AgeTracker.cs` - Age and growth

---

## Implementation Notes

### Development Priorities

1. **Start with Priority 1 (Critical)** - These are blockers for basic gameplay
2. **Move to Priority 2 (High)** - Important for comfortable play
3. **Address Priority 3 (Medium)** - Quality of life improvements
4. **Consider Priority 4 (Low)** - Only after core features complete

### Common Patterns

Most features will follow similar patterns:
- **Harmony patch** on relevant UI class (prefix/postfix)
- **Navigation state class** to track user position in menu
- **Helper class** for data extraction and formatting
- **Keyboard input handling** in UIRoot patch or specialized patch
- **Clipboard announcements** for screen reader feedback
- **Visual highlighting** (optional) for sighted assistance

### Testing Priorities

Focus testing on:
1. Core survival features (food, health, safety)
2. Production and research (progression)
3. Combat and defense (threats)
4. Trading and diplomacy (resources)
5. Quality of life (efficiency)

### Performance Considerations

- Avoid expensive operations in frequently-called patches
- Cache data when possible to reduce recalculation
- Use efficient navigation structures (lists, dictionaries)
- Minimize string allocations in hot paths

---

## Long-term Goals

### Beyond Current Scope
- **Mod compatibility patches** - Handle popular mods' custom UI
- **Audio cues** - Optional sound effects for events (complementing screen reader)
- **Macro/automation system** - Record and replay common action sequences
- **Spatial audio** - 3D sound positioning for map awareness
- **Voice commands** - Speech recognition for commands (ambitious)

### Community Feedback
After core features complete:
- Gather user feedback from blind/low-vision players
- Identify pain points and usability issues
- Prioritize enhancements based on actual usage
- Create user documentation and tutorials

---

**Last Updated:** 2025-10-27
**Current Version:** 1.0.0
**Total Items:** 40 (20 implemented, 20 pending)
