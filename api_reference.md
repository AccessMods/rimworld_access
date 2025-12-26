# RimWorld Decompiled Code - API Reference

This document provides a comprehensive reference to the decompiled RimWorld codebase structure, organized by namespace and folder. This reference is intended for modders working with the RimWorld Access mod or other RimWorld modifications.
**Total Files:** ~9,000+ C# files

**Total Namespaces:** 14+ major namespaces plus external libraries

---

## Table of Contents

1. [Root Level Files](#root-level-files)
2. [RimWorld Namespace](#rimworld-namespace)
3. [Verse Namespace](#verse-namespace)
4. [Verse.AI Namespace](#verseai-namespace)
5. [Verse.AI.Group Namespace](#verseaigroup-namespace)
6. [Verse.Glow Namespace](#verseglow-namespace)
7. [Verse.Grammar Namespace](#versegrammar-namespace)
8. [Verse.Noise Namespace](#versenoise-namespace)
9. [Verse.Profile Namespace](#verseprofile-namespace)
10. [Verse.Sound Namespace](#versesound-namespace)
11. [Verse.Steam Namespace](#versesteam-namespace)
12. [Verse.Utility Namespace](#verseutility-namespace)
13. [RimWorld.BaseGen Namespace](#rimworldbasegen-namespace)
14. [RimWorld.IO Namespace](#rimworldio-namespace)
15. [RimWorld.Planet Namespace](#rimworldplanet-namespace)
16. [RimWorld.QuestGen Namespace](#rimworldquestgen-namespace)
17. [RimWorld.SketchGen Namespace](#rimworldsketchgen-namespace)
18. [RimWorld.Utility Namespace](#rimworldutility-namespace)
19. [LudeonTK Namespace](#ludeontk-namespace)
20. [External Libraries](#external-libraries)
21. [Architecture Patterns](#architecture-patterns)

---

## Root Level Files

**Location:** `/decompiled/` (32 files)

These are utility and specialized classes in the root namespace, primarily handling complex generation systems and special behaviors.

### Layout & Structure Generation (11 files)

| File | Purpose |
|------|---------|
| `LayoutWorker.cs` | Base class for procedural layout generation for various site structures and complexes |
| `LayoutWorker_AncientStockpile.cs` | Layout generation for ancient stockpile sites |
| `LayoutWorker_Labyrinth.cs` | Complex labyrinth layout generation with puzzle-like room configurations |
| `LayoutWorker_OrbitalPlatform.cs` | Layout for orbital platform structures |
| `LayoutWorker_SimpleRuin.cs` | Simple ruined structure layout generation |
| `LayoutWorker_Structure.cs` | Generic structure layout with detailed room and corridor generation |
| `LayoutWorkerComplex.cs` | Base class for complex multi-level layout generation |
| `LayoutWorkerComplex_Ancient.cs` | Ancient complex layout generation system |
| `LayoutWorkerComplex_Mechanitor.cs` | Mechanitor-related complex layout generation |

### Specialized Systems (8 files)

| File | Purpose |
|------|---------|
| `CallBossgroupUtility.cs` | Utilities for summoning boss groups via devices and managing boss group mechanics |
| `Command_CallBossgroup.cs` | UI command for calling boss groups |
| `DarknessCombatUtility.cs` | Handles darkness-based combat modifiers (shooting accuracy, melee hit chance, dodge chance based on lighting conditions and ideology) |
| `HistoryEventUtility.cs` | Utilities for recording and managing historical game events |
| `JobDriver_BreastfeedCarryToMom.cs` | AI job driver for babies to carry feed items to nursing mother pawns |
| `JobDriver_BreastfeedCarryToDownedMom.cs` | Variant for downed nursing mothers |
| `MechanitorUtility.cs` | Utilities for mechanitor and mech-related interactions |
| `MechWorkUtility.cs` | Utilities for mechanical work and maintenance |

### Data & Utility (7 files)

| File | Purpose |
|------|---------|
| `Delay.cs` | Simple delay/timeout utility structure |
| `DevWindowDrawing.cs` | Utilities for drawing developer debug windows |
| `FleckParallelizationInfo.cs` | Data structure for parallel processing of visual effects (flecks) |
| `FleckUtility.cs` | Utilities for managing game flecks (small visual effects like blood splatters) |
| `PerformanceBenchmarkUtility.cs` | Performance benchmarking and profiling utilities |
| `ResearchUtility.cs` | Utilities for research system interactions and calculations |
| `ScenarioUtility.cs` | Utilities for scenario setup and management |

### Specialized Content (4 files)

| File | Purpose |
|------|---------|
| `Screen_ArchonexusSettlementCinematics.cs` | UI screen displaying cinematics for Archonexus settlement sequences |
| `StructureGenParams.cs` | Parameters for procedural structure generation |
| `Thought_FoodEaten.cs` | Specialized thought class for food eating reactions |
| `WeaponClassDef.cs` | Definition for weapon classifications |
| `WeaponClassPairDef.cs` | Definition for paired weapon classes |

### Auto-Generated/Special (2 files)

| File | Purpose |
|------|---------|
| `__JobReflectionRegistrationOutput__18251418111571612730.cs` | Auto-generated job reflection registration |
| `UnitySourceGeneratedAssemblyMonoScriptTypes_v1.cs` | Unity-generated assembly script types |
| `-BurstDirectCallInitializer.cs` | Burst compiler initialization for performance optimization |

---

## RimWorld Namespace

**Location:** `/decompiled/RimWorld/` (5,913 files)

The largest namespace containing all game-specific systems and mechanics. This is the primary namespace for game content and gameplay systems.

### Overview

The RimWorld namespace is organized into several major functional categories:

### Job & AI Systems (600+ files)

#### JobDriver_*.cs Files
Implement specific pawn work activities. Each JobDriver defines how a pawn performs a specific task:

**Common JobDrivers:**
- `JobDriver_Blind.cs`, `JobDriver_Dance.cs`, `JobDriver_Floordrawing.cs` - Recreation/social activities
- `JobDriver_Hunt.cs`, `JobDriver_NatureRunning.cs` - Hunting and outdoor work
- `JobDriver_TendPatient.cs`, `JobDriver_TendEntity.cs` - Medical care
- `JobDriver_LayDown.cs`, `JobDriver_LayDownAwake.cs` - Sleep and rest
- `JobDriver_UseCommsConsole.cs`, `JobDriver_Radiotalking.cs` - Communication
- `JobDriver_WatchTelevision.cs`, `JobDriver_WatchBuilding.cs`, `JobDriver_ViewArt.cs` - Entertainment
- `JobDriver_Sacrifice.cs`, `JobDriver_DeliverPawnToAltar.cs` - Religious/ritual work
- `JobDriver_VisitGrave.cs`, `JobDriver_VisitJoyThing.cs`, `JobDriver_VisitSickPawn.cs` - Social visits
- `JobDriver_InstallImplant.cs`, `JobDriver_GetReimplanted.cs` - Bionic operations
- `JobDriver_Hack.cs`, `JobDriver_ActivateMonolith.cs`, `JobDriver_InspectGravEngine.cs` - Ancient technology interaction
- `JobDriver_UseItem.cs`, `JobDriver_UseItemResearchBench.cs` - General item usage

#### JobGiver_*.cs Files
AI decision makers for assigning jobs based on conditions:

- `JobGiver_Binge*.cs` - Addiction-driven behavior
- `JobGiver_AITrash*.cs` - Garbage and waste management
- `JobGiver_FleePotentialExplosion.cs`, `JobGiver_FindOxygen.cs` - Emergency behavior
- `JobGiver_SentryPatrol.cs` - Military patrol duties
- `JobGiver_AIReleaseMechs.cs` - Mechanoid release management

#### Toils_*.cs Files
Reusable job action components that can be combined to create complex job behaviors:

- `Toils_Tend.cs` - Medical treatment actions
- `Toils_Misc.cs` - Miscellaneous work toils
- `Toils_Refuel.cs` - Refueling actions
- `Toils_Interpersonal.cs` - Social interaction toils

### Alert System (80+ files)

#### Alert_*.cs Files
In-game notifications for various conditions:

**Colonist & Pawn Alerts:**
- `Alert_ColonistLeftUnburied.cs` - Dead bodies
- `Alert_Boredom.cs`, `Alert_Biostarvation.cs` - Pawn needs
- `Alert_FireStartingSpree.cs` - Mental break warnings

**Animal Management:**
- `Alert_AnimalPenNeeded.cs`, `Alert_AnimalRoaming.cs` - Animal management

**Equipment & Combat:**
- `Alert_BrawlerHasRangedWeapon.cs` - Equipment warnings

**Dozens more covering all major game systems**

### Thought & Mental State System (50+ files)

#### Thought_*.cs Files
Pawn thoughts affecting mood. Covers social, environmental, ideological, and circumstantial thoughts:

- `Thought_FoodEaten.cs` - Reactions to food types
- Various thought implementations for mood management

#### MentalState_*.cs Files
Severe pawn behavioral states during mental breaks

#### MentalBreakWorker_*.cs Files
Mental breakdown definitions and triggers

### Ability/Powers System (30+ files)

Core ability system components:

| File | Purpose |
|------|---------|
| `Ability.cs` | Base ability class |
| `AbilityCategoryDef.cs` | Ability category definitions |
| `AbilityDef.cs` | Ability definitions |
| `AbilityDefOf.cs` | Static references to common abilities |
| `AbilityComp.cs` | Component system for ability modifiers |
| `AbilityUtility.cs` | Ability helper functions |

### Building & Structure Systems (100+ files)

#### Building_*.cs Files
Specialized building types:

- `Building_Bed.cs` - Sleeping structures
- `Building_Door.cs` - Door mechanics and pathfinding
- `Building_TurretGun.cs` - Defensive turrets
- `Building_Power*.cs` - Power generation/transmission buildings

### Comp Systems (200+ files)

#### CompProperties_*.cs and Comp_*.cs Files
Component-based system for building/pawn behaviors:

**Major Comp Categories:**
- Power, heat, temperature management
- Breakdowns and maintenance
- Storage and inventory
- Combat and damage
- Production and crafting
- Many specialized systems

This component system allows buildings and pawns to have modular, extensible behaviors.

### Pawn & Character Systems (200+ files)

Core pawn entity management:

| File | Purpose |
|------|---------|
| `Pawn.cs` | Core pawn entity (colonists, animals, enemies) |
| `PawnKindDef.cs` | Definition of pawn types |
| `PawnRelationUtility.cs` | Relationship calculations |
| `PawnGenerationRequest.cs` | Pawn creation parameters |

**Related Systems:**
- Health and medical system
- Psychology and mood system
- Needs management (food, rest, recreation, comfort)
- Skills and trait systems
- Relationships and social interactions

### Crafting & Production (50+ files)

Work order and recipe system:

| File | Purpose |
|------|---------|
| `Bill.cs` | Individual work order |
| `BillStack.cs` | Work order queue management |
| `RecipeDef.cs` | Recipe definitions |
| `Ingrediant.cs` | Recipe ingredient requirements |

### Combat & Weapons (80+ files)

Combat mechanics and damage system:

**Key Files:**
- `Verb_*.cs` files - Weapon attack types (shooting, melee, explosions)
- `Projectile.cs` - Projectile mechanics and ballistics
- `Damage*.cs` files - Damage calculation and application
- `Combat*.cs` utilities - Combat calculations and modifiers

### Research & Technology (30+ files)

Technology progression system:

| File | Purpose |
|------|---------|
| `ResearchManager.cs` | Research progress tracking |
| `ResearchProjectDef.cs` | Research definition |
| `TechBook.cs` | Technology knowledge items |

### Faction & Diplomacy (40+ files)

Inter-faction relationship system:

| File | Purpose |
|------|---------|
| `Faction.cs` | Faction definition and state |
| `FactionRelation.cs` | Inter-faction relationships |
| `NegotiationUtility.cs` | Trade and negotiation mechanics |

### Miscellaneous Utilities (300+ files)

General purpose utilities:

- `GenText.cs`, `GenUI.cs`, `GenMath.cs` - General utilities
- `*Def.cs` files - Static definition collections
- `*Utility.cs` - Domain-specific utilities
- Statistical modifiers, trait definitions, dialogue systems, etc.

---

## Verse Namespace

**Location:** `/decompiled/Verse/` (1,747 files)

Core engine systems shared across all RimWorld content. The fundamental game framework that other namespaces build upon.

### Overview

Verse is the core engine layer, providing fundamental systems like data structures, UI, graphics, sound, and low-level game mechanics.

### Data Types & Structures (60+ files)

Core coordinate and mathematical types:

| File | Purpose |
|------|---------|
| `IntVec3.cs` | 3D integer coordinate system (primary tile coordinate type) |
| `IntVec2.cs` | 2D integer coordinate system |
| `Rot4.cs` | 4-direction rotation (North, East, South, West) |
| `FloatRange.cs`, `IntRange.cs` | Numeric range containers |
| `Vector2Utility.cs`, `Vector3Utility.cs`, `IntVec3Utility.cs` | Vector math utilities |
| `ByteRange.cs` | Byte-based ranges |
| `RotEnum.cs`, `RotEnumExtensions.cs`, `RotationDirection.cs` | Rotation enumerations |

### Logging & Debugging (20+ files)

Debug and logging infrastructure:

| File | Purpose |
|------|---------|
| `Log.cs` | Central logging system |
| `LogMessage.cs`, `LogMessageType.cs` | Log message definitions |
| `LogMessageQueue.cs` | Queued logging |
| `DebugLogsUtility.cs` | Debug logging helpers |

### Collection & Data Management (50+ files)

Collection utilities and data structures:

| File | Purpose |
|------|---------|
| `GenList.cs`, `GenDictionary.cs` | Collection utilities |
| `SimplePool.cs`, `FullPool.cs` | Object pooling systems |
| `SimpleLinearPool.cs` | Linear pool allocator |
| `LRUCache.cs` | Least recently used cache implementation |
| `EventQueue.cs` | Event queue system |

### Parsing & String Utilities (40+ files)

Text processing and parsing:

| File | Purpose |
|------|---------|
| `ParseHelper.cs` | Parsing utilities |
| `GenString.cs` | String manipulation |
| `GenericConverter.cs` | Type conversion |
| `MathEvaluator*.cs` files | Mathematical expression evaluation |
| `GenAttribute.cs` | Attribute reflection utilities |
| `GenTypes.cs` | Type system utilities |
| `XmlHelper.cs` | XML parsing assistance |
| `CultureInfoUtility.cs` | Locale/culture utilities |

### UI & Graphics (60+ files)

User interface system:

| File | Purpose |
|------|---------|
| `UI.cs` | UI management |
| `InspectTabBase.cs`, `InspectTabManager.cs` | Inspector UI system |
| `TexGame.cs` | Game textures |
| `GameplayTipWindow.cs` | Gameplay hints |
| `ModSummaryWindow.cs` | Mod information display |
| `ShaderParameter.cs` | Shader parameters |

### Abilities & Effects (40+ files)

Effect and ability framework:

| File | Purpose |
|------|---------|
| `AbilityCompProperties.cs` | Ability component properties |
| `DeathActionWorker*.cs` files | Death effect handling |
| `DeathActionProperties*.cs` | Death effect definitions |
| `ConditionalStatAffecter*.cs` | Conditional stat modifiers |
| `SubEffecter_*.cs` files | Effect sub-systems |

### Genetic & Biological (20+ files)

Gene and biological trait system:

| File | Purpose |
|------|---------|
| `GeneticTraitData.cs` | Genetic trait definitions |
| `GeneticBodyType.cs` | Body type genes |
| `EndogeneCategory.cs` | Gene categories |
| `GeneCategoryDef.cs` | Gene category definitions |
| `GeneSymbolPack.cs` | Gene visual symbols |
| `PassionMod.cs` | Passion level modifiers |

### Rituals & Events (20+ files)

Ritual and event framework:

| File | Purpose |
|------|---------|
| `RitualStagePositions.cs` | Ritual stage positioning |
| `RitualStageOnTickActions.cs` | Ritual tick events |
| `PawnRitualReference.cs` | Pawn ritual involvement |
| `PawnRenderTreeDef.cs` | Pawn render tree definitions |
| `TagFilter.cs` | Tag filtering system |
| `DamageFactor.cs` | Damage modification factors |
| `OptionCategoryDef.cs` | Option categories |

### Utility & Misc (80+ files)

General utilities:

| File | Purpose |
|------|---------|
| `GenRadial.cs` | Radial calculations and patterns |
| `GenAdj.cs`, `GenAdjFast.cs` | Adjacent cell calculations |
| `AcceptanceReport.cs` | Approval/rejection reporting |
| `ToStringNumberSense.cs` | Number formatting |
| `ToStringStyle.cs` | String styling |
| `AnyEnum.cs` | Generic enum utilities |
| `Predicate.cs` | Predicate helper |
| `MurmurHash.cs` | Hash function implementation |
| `FreezeManager.cs` | Game freeze state management |
| `WorldFloodFiller.cs` | World flood fill algorithm |

### Game Initialization (20+ files)

Game startup and initialization:

| File | Purpose |
|------|---------|
| `BaseContent.cs` | Base game content management |
| `UnityData.cs`, `UnityDataInitializer.cs` | Unity engine integration |
| `StaticConstructorOnStartup.cs` | Runtime initialization |
| `StaticConstructorOnStartupUtility.cs` | Initialization utilities |
| `BackCompatibility.cs` | Save game version compatibility |
| `BackCompatibilityConverter*.cs` | Version conversion handlers |

### Enumerations & Constants (20+ files)

Core enumerations:

| File | Purpose |
|------|---------|
| `LogMessageType.cs` | Log types |
| `AltitudeLayer.cs`, `Altitudes.cs` | Rendering altitude layers |
| `AnimalNameDisplayMode.cs` | Name display options |
| `AnimalType.cs` | Animal categorization |
| `ApparelLayerDef.cs` | Clothing layers |
| `Danger.cs` | Danger levels |
| `WaterBodyType.cs` | Water types |
| `ToStringNumberSense.cs` | Number display options |

### Performance & Optimization (30+ files)

Performance optimization systems:

| File | Purpose |
|------|---------|
| `MoteCounter.cs` | Visual effect counting |
| `PawnWaterRippleMaker.cs` | Ripple effect management |
| `LongEventHandler.cs` | Long-running operation handling |

### Area & Territory (20+ files)

Area definition and management:

| File | Purpose |
|------|---------|
| `Area.cs`, `AreaManager.cs` | Area definition and management |
| `AreaUtility.cs`, `AreaOverlap.cs`, `AreaSource.cs` | Area utilities |
| `ArenaUtility.cs` | Arena area utilities |
| `AnimalPen*.cs` files | Animal pen enclosure system |

### Misc Content (60+ files)

Additional systems:

| File | Purpose |
|------|---------|
| `AddedBodyPartProps.cs` | Body part properties |
| `AlternateGraphic.cs` | Alternate visual representations |
| `AnimationDef.cs`, `AnimationPart.cs`, `AnimationUtility.cs` | Animation system |
| `AnimationWorker_*.cs` files | Specific animation implementations |
| `AttachableThing.cs`, `AttachmentUtility.cs` | Attachment system |
| `AudioSourceUtility.cs` | Audio source management |
| `DangerUtility.cs` | Danger assessments |
| `ActiveTip.cs` | Tooltip system |

---

## Verse.AI Namespace

**Location:** `/decompiled/Verse/AI/` (278 files)

Artificial Intelligence and behavior systems for pawns and creatures.

### Overview

The AI system uses a hierarchical decision-making structure with Think Nodes, Job Givers, Jobs, and Toils.

### Job System (50+ files)

Core job execution framework:

| File | Purpose |
|------|---------|
| `Toil.cs` | Base job action unit (smallest work component) |
| `ToilCompleteMode.cs` | Job completion conditions |
| `ToilEffects.cs` | Effects that occur during toils |
| `ToilFailConditions.cs` | Conditions that fail jobs |
| `ToilJumpConditions.cs` | Conditions that jump to other toils |

#### Base JobDriver Implementations

| File | Purpose |
|------|---------|
| `JobDriver_Goto.cs` | Movement only |
| `JobDriver_Wait.cs` | Idle waiting |
| `JobDriver_AttackMelee.cs` | Melee combat |
| `JobDriver_CastAbility*.cs` | Ability casting |
| `JobDriver_PlayStatic.cs`, `JobDriver_PlayToys.cs`, `JobDriver_PlayWalking.cs` | Recreation |

### Toil Builder Utilities (5+ files)

Pre-built toil collections:

| File | Purpose |
|------|---------|
| `Toils_General.cs` | General-purpose toils |
| `Toils_Combat.cs` | Combat toils |
| `Toils_Interact.cs` | Interaction toils |
| `Toils_Effects.cs` | Visual/audio effect toils |
| `Toils_JobTransforms.cs` | Job modification toils |

### Mental State System (60+ files)

Mental break and state management:

| File | Purpose |
|------|---------|
| `MentalState.cs` | Base mental state class |
| `MentalStateWorker.cs` | Mental state decision maker |
| `MentalStateHandler.cs` | Mental state management |
| `MentalFitGenerator.cs` | Mental health assessment |

#### Mental Break Workers

`MentalBreakWorker_*.cs` - Specific mental break types

#### Mental State Implementations

| File | Purpose |
|------|---------|
| `MentalState_Berserk.cs` | Violent rages |
| `MentalState_PanicFlee*.cs` | Fear responses |
| `MentalState_Tantrum*.cs` | Anger tantrums |
| `MentalState_Binging*.cs` | Addiction binges |
| `MentalState_WanderConfused.cs` | Confusion |
| `MentalState_Manhunter.cs` | Wild animal aggression |
| `MentalState_InsultingSpree*.cs` | Social outbursts |
| `MentalState_FireStartingSpree.cs` | Arson |
| `MentalState_SlaughterThing.cs` | Destructive rage |
| `MentalState_GiveUpExit.cs` | Despair/surrender |

### Think Node System (30+ files)

AI decision tree:

| File | Purpose |
|------|---------|
| `ThinkNode.cs` | Base AI decision node |
| `ThinkNode_Conditional.cs` | Conditional decision branches |
| `ThinkNode_Tagger.cs` | Action tagging |
| `ThinkNode_Subtree.cs` | Behavior tree branching |
| `ThinkNode_Random.cs` | Random selection |
| `ThinkNode_ChancePerHour_*.cs` | Probability-based decisions |
| `ThinkNode_ChancePerHour_Forage.cs` | Foraging |
| `ThinkNode_ChancePerHour_InsectDigChance.cs` | Insect behavior |

### Utility & Helpers (20+ files)

Support utilities:

| File | Purpose |
|------|---------|
| `MurderousRageMentalStateUtility.cs` | Murderous rage handling |
| `JobGiver_PickupDroppedWeapon.cs` | Weapon pickup logic |

---

## Verse.AI.Group Namespace

**Location:** `/decompiled/Verse/AI/Group/` (129 files)

Group behavior and multi-pawn coordination systems.

### Overview

The Lord system manages coordinated group behaviors like raids, caravans, and trading parties.

### Core Group Management

| File | Purpose |
|------|---------|
| `LordJob.cs` | Base lord job (group mission) |
| `LordToil.cs` | Group behavior state |
| `Lord.cs` | Group leader/state manager |
| `LordManager.cs` | Multi-group coordinator |
| `LordUtility.cs` | Group utilities |
| `PawnGroup.cs` | Group definition |

### Lord Toils (Group Behaviors)

`LordToil_*.cs` files - Specific group behaviors:

| File | Purpose |
|------|---------|
| `LordToil_Travel.cs` | Group movement |
| `LordToil_AssaultColony.cs` | Raids |
| `LordToil_TravelExit.cs` | Group departure |
| `LordToil_Trade.cs` | Trading caravans |
| `LordToil_DefendPoint.cs` | Defense positions |
| `LordToil_PrepareForAssault.cs` | Raid preparation |

### Transitions

`Transition_*.cs` files - Group state transitions

---

## Verse.Glow Namespace

**Location:** `/decompiled/Verse/Glow/` (7 files)

Light and glow rendering system.

| File | Purpose |
|------|---------|
| `GlowGrid.cs` | Grid-based light calculation |
| `PsychGlow.cs` | Psychological glow levels (mood/visibility) |
| `GlowFlooder.cs` | Light propagation algorithm |
| `SkyColorSet.cs` | Sky color management |

---

## Verse.Grammar Namespace

**Location:** `/decompiled/Verse/Grammar/` (9 files)

Grammar and text generation systems for dynamic narrative text.

| File | Purpose |
|------|---------|
| `GrammarRule.cs` | Grammar rule definition |
| `GrammarRequest.cs` | Text generation request |
| `GrammarResolverAccumulator.cs` | Rule resolution |
| `RulePackDef.cs` | Rule pack definitions |

Related grammar and NPC dialogue generation systems.

---

## Verse.Noise Namespace

**Location:** `/decompiled/Verse/Noise/` (51 files)

Procedural noise generation for world and map generation.

### Core Noise Systems

| File | Purpose |
|------|---------|
| `Perlin.cs` | Perlin noise implementation |
| `NoiseDebugUI.cs` | Debug visualization |
| `NoiseRenderer.cs` | Noise rendering |
| `Billow.cs`, `Ridged.cs` | Noise modifier classes |
| `NoiseRenderer_NoisePlanes.cs` | Noise plane rendering |

Various noise utility and configuration classes for procedural generation.

---

## Verse.Profile Namespace

**Location:** `/decompiled/Verse/Profile/` (1 file)

Performance profiling system.

| File | Purpose |
|------|---------|
| `Profiler.cs` | Performance measurement and profiling utilities |

---

## Verse.Sound Namespace

**Location:** `/decompiled/Verse/Sound/` (69 files)

Complete audio and sound effect system.

### Core Sound Management (15+ files)

| File | Purpose |
|------|---------|
| `SoundRoot.cs` | Root sound definition |
| `SoundStarter.cs` | Sound playback initiation |
| `SoundInfo.cs` | Sound parameters |
| `Sustainer.cs` | Continuous sound management |
| `SustainerManager.cs` | Multi-sustainer coordination |
| `SampleOneShot.cs`, `SampleSustainer.cs` | Audio sample management |
| `SubSustainer.cs` | Sub-sound management |
| `SustainerScopeFader.cs` | Sound scope fading |

### Audio Source Management (10+ files)

| File | Purpose |
|------|---------|
| `AudioSourcePool.cs` | Audio source object pool |
| `AudioSourcePoolWorld.cs` | World audio sources |
| `AudioSourcePoolCamera.cs` | Camera-relative audio |
| `AudioSourceMaker.cs` | Audio source creation |
| `SoundSlotManager.cs` | Audio slot allocation |

### Sound Effects & Filtering (20+ files)

| File | Purpose |
|------|---------|
| `SoundFilter.cs` | Base sound filter |
| `SoundFilterLowPass.cs`, `SoundFilterHighPass.cs` | Frequency filters |
| `SoundFilterEcho.cs`, `SoundFilterReverb.cs` | Effect filters |
| `ReverbSetup.cs`, `ReverbCustomPresets.cs` | Reverb configuration |
| `SoundFilterUtility.cs` | Filter utilities |
| `ImpactSoundTypeDef.cs` | Impact sound definitions |

### Sound Parameter System (20+ files)

Dynamic sound parameter mapping:

#### Parameter Sources

`SoundParamSource_*.cs` files - Specific parameter sources:

| File | Purpose |
|------|---------|
| `SoundParamSource_Random.cs` | Random values |
| `SoundParamSource_Perlin.cs` | Perlin noise |
| `SoundParamSource_SourceAge.cs` | Sound age |
| `SoundParamSource_Underground.cs` | Underground detection |
| `SoundParamSource_External.cs` | External parameters |
| `SoundParamSource_AggregateSize.cs` | Size-based |
| `SoundParamSource_CameraAltitude.cs` | Camera height |
| `SoundParamSource_OutdoorTemperature.cs` | Temperature |
| `SoundParamSource_MusicPlayingFadeOut.cs` | Music fade |
| `SoundParamSource_AmbientVolume.cs` | Ambient volume |

#### Parameter Targets

`SoundParamTarget_*.cs` files - Specific targets:

- `SoundParamTarget_Volume.cs`
- `SoundParamTarget_Pitch.cs`
- `SoundParamTarget_PropertyLowPass.cs`
- `SoundParamTarget_PropertyHighPass.cs`
- `SoundParamTarget_PropertyEcho.cs`
- `SoundParamTarget_PropertyReverb.cs`

### Audio Grain System (10+ files)

| File | Purpose |
|------|---------|
| `AudioGrain.cs` | Base audio grain |
| `AudioGrain_Clip.cs` | Audio clip grain |
| `AudioGrain_Folder.cs` | Folder-based grain selection |
| `AudioGrain_Silence.cs` | Silent grain |
| `ResolvedGrain.cs` | Resolved audio grain |
| `ResolvedGrain_Clip.cs`, `ResolvedGrain_Silence.cs` | Resolved variants |

### Utilities & Configuration (10+ files)

| File | Purpose |
|------|---------|
| `SoundContext.cs` | Sound context/scope |
| `RepeatSelectMode.cs` | Repeat selection |
| `VoicePriorityMode.cs` | Voice priority |
| `MaintenanceType.cs` | Sound maintenance |
| `TimeType.cs` | Time type constants |
| `DebugSoundEventsLog.cs` | Debug logging |
| `MouseoverSounds.cs` | Mouse over audio |
| `SoundSizeAggregator.cs`, `ISizeReporter.cs` | Size reporting |

---

## Verse.Steam Namespace

**Location:** `/decompiled/Verse/Steam/` (14 files)

Steam platform integration for workshop, achievements, and friends.

| File | Purpose |
|------|---------|
| `SteamManager.cs` | Steam platform manager |
| `SteamUtility.cs` | Steam utilities |

Related Steam API integration and configuration.

---

## Verse.Utility Namespace

**Location:** `/decompiled/Verse/Utility/` (1 file)

| File | Purpose |
|------|---------|
| `Utility.cs` | Miscellaneous utility functions |

---

## RimWorld.BaseGen Namespace

**Location:** `/decompiled/RimWorld/BaseGen/` (125 files)

Procedural base and dungeon generation system using symbol resolution.

### Overview

BaseGen uses a recursive symbol resolution system to generate complex structures from abstract descriptions.

### Core Generation (10+ files)

| File | Purpose |
|------|---------|
| `BaseGen.cs` | Main generation coordinator |
| `BaseGenUtility.cs` | Generation utilities |
| `GlobalSettings.cs` | Generation settings |
| `SymbolStack.cs` | Symbol resolution stack |
| `SymbolResolver.cs` | Base symbol resolver |
| `ResolveParams.cs` | Generation parameters |
| `InteriorSymbolResolverUtility.cs` | Interior generation utilities |

### Symbol Resolvers (100+ specialized files)

All inherit from `SymbolResolver` and handle specific generation tasks.

#### Ancient Structure Symbols

| File | Purpose |
|------|---------|
| `SymbolResolver_AncientAltar.cs` | Ancient altars |
| `SymbolResolver_AncientTemple.cs` | Ancient temples |
| `SymbolResolver_AncientShrine.cs` | Ancient shrines |
| `SymbolResolver_AncientShrinesGroup.cs` | Shrine groups |
| `SymbolResolver_AncientRuins.cs` | Ruined structures |
| `SymbolResolver_AncientCryptosleepCasket.cs` | Cryptosleep pods |
| `SymbolResolver_Archonexus.cs` | Archonexus megastructure |
| `SymbolResolver_ArchonexusResearchBuildings.cs` | Archonexus research areas |
| `SymbolResolver_AncientComplex*.cs` (5 variants) | Large ancient complexes |

#### Base Layout Symbols

| File | Purpose |
|------|---------|
| `SymbolResolver_BasePart_Indoors.cs` | Indoor base areas |
| `SymbolResolver_BasePart_Outdoors.cs` | Outdoor base areas |
| `SymbolResolver_BasePart_Indoors_Division_Split.cs` | Indoor subdivisions |
| `SymbolResolver_BasePart_Outdoors_Division_Grid.cs` | Outdoor grid layout |
| `SymbolResolver_BasePart_Outdoors_Division_Split.cs` | Outdoor subdivisions |

#### Room Types

Indoor room generators:

| File | Purpose |
|------|---------|
| `SymbolResolver_BasePart_Indoors_Leaf_Barracks.cs` | Military barracks |
| `SymbolResolver_BasePart_Indoors_Leaf_BatteryRoom.cs` | Power storage |
| `SymbolResolver_BasePart_Indoors_Leaf_Brewery.cs` | Alcohol production |
| `SymbolResolver_BasePart_Indoors_Leaf_DiningRoom.cs` | Dining areas |
| `SymbolResolver_BasePart_Indoors_Leaf_ThroneRoom.cs` | Throne rooms |
| `SymbolResolver_BasePart_Indoors_Leaf_Gravcore.cs` | Gravity cores |
| `SymbolResolver_BasePart_Indoors_Leaf_WorshippedTerminal.cs` | Religious terminals |

#### Outdoor Areas

| File | Purpose |
|------|---------|
| `SymbolResolver_BasePart_Outdoors_Leaf_Farm.cs` | Farms |
| `SymbolResolver_BasePart_Outdoors_Leaf_Building.cs` | Buildings |
| `SymbolResolver_BasePart_Outdoors_Leaf_LandingPad.cs` | Landing pads |
| `SymbolResolver_BasePart_Outdoors_Leaf_PowerPlant.cs` | Power generation |
| `SymbolResolver_BasePart_Outdoors_Leaf_Empty.cs` | Empty areas |

#### Defensive Structures

| File | Purpose |
|------|---------|
| `SymbolResolver_EdgeWalls.cs` | Perimeter walls |
| `SymbolResolver_EdgeSandbags.cs` | Sandbag fortifications |
| `SymbolResolver_EdgeDefense.cs` | General edge defenses |
| `SymbolResolver_EdgeThing.cs` | Edge objects |
| `SymbolResolver_EdgeMannedMortar.cs` | Mortar emplacements |

#### Interior Features

| File | Purpose |
|------|---------|
| `SymbolResolver_IndoorLighting.cs` | Lighting systems |
| `SymbolResolver_OutdoorLighting.cs` | Outdoor lights |
| `SymbolResolver_Roof.cs` | Roofing |
| `SymbolResolver_Doors.cs` | Door placement |
| `SymbolResolver_Bed.cs` | Bed placement |
| `SymbolResolver_FillWithBeds.cs` | Multiple beds |
| `SymbolResolver_PlaceChairsNearTables.cs` | Furniture arrangement |
| `SymbolResolver_GenericRoom.cs` | Generic interior rooms |
| `SymbolResolver_EmptyRoom.cs` | Empty rooms |

#### Special Content

| File | Purpose |
|------|---------|
| `SymbolResolver_Hives.cs` | Insectoid hives |
| `SymbolResolver_Infestation.cs` | Infestations |
| `SymbolResolver_Ambush.cs` | Ambush setups |
| `SymbolResolver_RandomMechanoidGroup.cs` | Mechanoid groups |
| `SymbolResolver_SleepingMechanoids.cs` | Sleeping mech setups |
| `SymbolResolver_DesiccatedCorpses.cs` | Dead bodies |

---

## RimWorld.IO Namespace

**Location:** `/decompiled/RimWorld/IO/` (8 files)

File I/O and save/load system.

| File | Purpose |
|------|---------|
| `SaveCompression.cs` | Save file compression |
| `DataSerializing.cs` | Data serialization |
| `ScribeMetaHeaderUtility.cs` | Save file headers |

Related file operations and data persistence.

---

## RimWorld.Planet Namespace

**Location:** `/decompiled/RimWorld/Planet/` (302 files)

World map, planet generation, and overland mechanics.

### World Generation (40+ files)

| File | Purpose |
|------|---------|
| `WorldGen.cs` | World generation coordinator |

#### World Generation Steps

`WorldGenStep_*.cs` files - Sequential generation steps:

| File | Purpose |
|------|---------|
| `WorldGenStep_Tiles.cs` | Base tile generation |
| `WorldGenStep_Terrain.cs` | Terrain type assignment |
| `WorldGenStep_Rivers.cs` | River generation |
| `WorldGenStep_Roads.cs` | Road networks |
| `WorldGenStep_Lakes.cs` | Water body generation |
| `WorldGenStep_Landmarks.cs` | Landmark placement |
| `WorldGenStep_Pollution.cs` | Pollution system |
| `WorldGenStep_Mutators.cs` | World mutations |
| `WorldGenStep_TileElementsCore.cs` | Core tile elements |
| `WorldGenStep_TileElementsOdyssey.cs` | Odyssey elements |
| `WorldGenStep_Grids.cs` | Grid generation |
| `WorldGenStep_Components.cs` | Component initialization |

### Feature Generation (20+ files)

World-scale geographic features:

| File | Purpose |
|------|---------|
| `WorldFeature.cs` | World feature system |

#### Feature Workers

`FeatureWorker_*.cs` files - Feature types:

| File | Purpose |
|------|---------|
| `FeatureWorker_Biome.cs` | Biome regions |
| `FeatureWorker_MountainRange.cs` | Mountains |
| `FeatureWorker_Archipelago.cs` | Island groups |
| `FeatureWorker_Island.cs` | Individual islands |
| `FeatureWorker_Peninsula.cs` | Peninsulas |
| `FeatureWorker_Protrusion.cs` | Land protrusions |
| `FeatureWorker_Bay.cs` | Bay formations |
| `FeatureWorker_Cluster.cs` | Terrain clusters |
| `FeatureWorker_FloodFill.cs` | Area filling |
| `FeatureWorker_OuterOcean.cs` | Ocean edges |

### World Rendering (15+ files)

Layered world rendering system:

| File | Purpose |
|------|---------|
| `WorldDrawLayer.cs` | Base render layer |
| `WorldDynamicDrawManager.cs` | Dynamic rendering coordination |

#### Draw Layers

`WorldDrawLayer_*.cs` files - Specific layers:

| File | Purpose |
|------|---------|
| `WorldDrawLayer_Clouds.cs` | Cloud rendering |
| `WorldDrawLayer_Rivers.cs` | River rendering |
| `WorldDrawLayer_Roads.cs` | Road rendering |
| `WorldDrawLayer_Glow.cs` | Glow/light effects |
| `WorldDrawLayer_Hills.cs` | Terrain features |
| `WorldDrawLayer_Paths.cs` | Pawn paths |
| `WorldDrawLayer_Satellites.cs` | Orbital objects |
| `WorldDrawLayer_Landmarks.cs` | Landmark rendering |
| `WorldDrawLayer_Pollution.cs` | Pollution display |
| `WorldDrawLayer_CurrentMapTile.cs` | Current location |
| `WorldDrawLayer_SelectedTile.cs` | Selected tile |

### World Data Management (30+ files)

| File | Purpose |
|------|---------|
| `WorldGrid.cs` | Tile grid system |
| `WorldInfo.cs` | World information storage |
| `PositionData.cs` | Position data |
| `GlobalTargetInfo.cs` | Cross-world targeting |
| `Tile*.cs` files | Tile properties |
| `BiomeWorker.cs` | Biome determination |

### Pathfinding (10+ files)

World-scale pathfinding:

| File | Purpose |
|------|---------|
| `WorldPathing.cs` | World pathfinding |
| `WorldPathGrid.cs` | Pathfinding grid |
| `WorldPath.cs` | Pathfinding result |
| `WorldPathPool.cs` | Path object pooling |
| `WorldReachability.cs` | Reachability calculation |
| `WorldReachabilityUtility.cs` | Reachability utilities |

### Caravans (20+ files)

Caravan system for overland travel:

| File | Purpose |
|------|---------|
| `Caravan.cs` | Caravan group |
| `CaravanInventoryUtility.cs` | Caravan inventory |

#### Caravan Subsystems

`Caravan_*.cs` files:

| File | Purpose |
|------|---------|
| `Caravan_BabyTracker.cs` | Baby management |
| `Caravan_BedsTracker.cs` | Bed availability |
| `Caravan_CarryTracker.cs` | Carrying capacity |
| `Caravan_ForageTracker.cs` | Food foraging |
| `Caravan_NeedsTracker.cs` | Pawn needs |
| `Caravan_TraderTracker.cs` | Trade management |

### World Objects & Components (50+ files)

| File | Purpose |
|------|---------|
| `WorldObject.cs` | Base world object |
| `WorldObjectComp.cs` | World object component system |
| `Settlement.cs` | Settlement objects |
| `SitePartWorker.cs` | Site part definitions |
| `MapParent.cs` | Map parent container |

Various `WorldObjectComp*.cs` files for components.

### Utilities & Events (30+ files)

| File | Purpose |
|------|---------|
| `SettleUtility.cs` | Settlement establishment |
| `SettleInExistingMapUtility.cs` | Settlement joining |
| `FastTileFinder.cs` | Rapid tile searching |
| `WorldGizmoUtility.cs` | World gizmo management |
| `TimedDetectionRaids.cs` | Raid timing |
| `TimedMakeFactionHostile.cs` | Faction hostility |
| `TimedForcedExit.cs` | Forced exit events |
| `TilesPerDayCalculator.cs` | Travel speed |

---

## RimWorld.QuestGen Namespace

**Location:** `/decompiled/RimWorld/QuestGen/` (381 files)

Dynamic quest generation and quest system.

### Quest Generation Core (15+ files)

| File | Purpose |
|------|---------|
| `QuestGen.cs` | Main quest generation engine |
| `QuestNode.cs` | Base quest node |
| `QuestTextRequest.cs` | Text generation requests |
| `QuestGenUtility.cs` | Quest generation utilities |

#### Quest Generation Helpers

`QuestGen_*.cs` files:

| File | Purpose |
|------|---------|
| `QuestGen_Debug.cs` | Debug quest generation |
| `QuestGen_Delay.cs` | Delay utilities |
| `QuestGen_End.cs` | Ending quest generation |
| `QuestGen_Filter.cs` | Filtering logic |
| `QuestGen_Factions.cs` | Faction involvement |
| `QuestGen_Get.cs` | Retrieval operations |
| `QuestGen_HistoryEvents.cs` | Historical events |
| `QuestGen_Lord.cs` | Lord/group generation |

### Quest Nodes (300+ specialized files)

All extend `QuestNode` and define specific quest actions and generators.

#### Core Flow Nodes

| File | Purpose |
|------|---------|
| `QuestNode_Sequence.cs` | Sequential execution |
| `QuestNode_RandomNode.cs` | Random selection |
| `QuestNode_SubScript.cs` | Subscript execution |
| `QuestNode_LoopCount.cs` | Loop execution |
| `QuestNode_Signal.cs` | Signal handling |
| `QuestNode_SendSignals.cs` | Signal sending |

#### Conditional & Control

| File | Purpose |
|------|---------|
| `QuestNode_Log.cs` | Logging |
| `QuestNode_RuntimeLog.cs` | Runtime logging |
| `QuestNode_Message.cs` | Message display |
| `QuestNode_Letter.cs` | Letter sending |
| `QuestNode_Set.cs` | Variable setting |

#### Pawn & Group Generation

| File | Purpose |
|------|---------|
| `QuestNode_GetPawn.cs` | Pawn retrieval |
| `QuestNode_GetPawnKind.cs` | Pawn kind selection |
| `QuestNode_GetRandomPawnKindForFaction.cs` | Random pawn kind |
| `QuestNode_JoinPlayer.cs` | Pawn joining |
| `QuestNode_Leave.cs` | Pawn departure |

#### Rewards & Consequences

| File | Purpose |
|------|---------|
| `QuestNode_GiveRewards.cs` | Reward distribution |
| `QuestNode_GiveRoyalFavor.cs` | Royal favor |
| `QuestNode_GiveTechprints.cs` | Technology distribution |
| `QuestNode_EndGame.cs` | Game ending |

#### Random Selection

| File | Purpose |
|------|---------|
| `QuestNode_GetRandomElement.cs` | Random selection |
| `QuestNode_GetRandomElementByWeight.cs` | Weighted random |
| `QuestNode_GetRandomInRangeInt.cs` | Random integer |
| `QuestNode_GetRandomInRangeFloat.cs` | Random float |

---

## RimWorld.SketchGen Namespace

**Location:** `/decompiled/RimWorld/SketchGen/` (37 files)

Procedural sketch-based structure generation system.

| File | Purpose |
|------|---------|
| `SketchGen.cs` | Main sketch generation |
| `SketchResolver.cs` | Base sketch resolver |

Various `SketchResolver_*.cs` files for specific structure generators.

---

## RimWorld.Utility Namespace

**Location:** `/decompiled/RimWorld/Utility/` (4 files)

Miscellaneous RimWorld utilities.

General purpose utility functions and helpers specific to RimWorld.

---

## LudeonTK Namespace

**Location:** `/decompiled/LudeonTK/` (50 files)

Development and debugging toolkit (named after Ludeon Studios).

### Debug UI & Tools (20+ files)

| File | Purpose |
|------|---------|
| `DevGUI.cs` | Main developer GUI manager |
| `Window_Dev.cs` | Developer window |
| `Window_DevListing.cs` | Debug menu listing |
| `Window_DebugTable.cs` | Debug data tables |
| `Dialog_Debug.cs` | Debug dialog |
| `Dialog_OptionLister.cs` | Option listing |
| `EditWindow.cs` | Base edit window |
| `EditWindow_DefEditor.cs` | Definition editor |
| `EditWindow_TweakValues.cs` | Value tweaker |
| `EditWindow_DebugInspector.cs` | Object inspector |
| `EditWindow_Log.cs` | Log viewer |

### Debug Tabs & Menus (10+ files)

| File | Purpose |
|------|---------|
| `DebugTabMenu.cs` | Base debug tab |
| `DebugTabMenu_Actions.cs` | Action tab |
| `DebugTabMenu_Settings.cs` | Settings tab |
| `DebugTabMenu_Output.cs` | Output tab |
| `DebugTools.cs` | Debug action utilities |
| `DebugTool.cs` | Base debug tool |

### Debug Actions & Attributes (10+ files)

| File | Purpose |
|------|---------|
| `DebugActionAttribute.cs` | Debug action marker |
| `DebugActionYielderAttribute.cs` | Debug action generator |
| `DebugOutputAttribute.cs` | Debug output marker |
| `DebugActionType.cs` | Action type enum |
| `DebugActionButtonResult.cs` | Action result |
| `AllowedGameStates.cs` | Game state filtering |
| `DebugActionNode.cs` | Action node structure |
| `DebugLogsUtility.cs` | Debug logging |

### Noise Visualization (5+ files)

| File | Purpose |
|------|---------|
| `Dialog_DevNoiseBase.cs` | Base noise dialog |
| `Dialog_DevNoiseMap.cs` | Map noise visualization |
| `Dialog_DevNoiseWorld.cs` | World noise visualization |
| `Dialog_DevCelestial.cs` | Celestial noise |
| `Dialog_DevInfectionPathways.cs` | Infection pathways |

### Additional Tools (5+ files)

| File | Purpose |
|------|---------|
| `Dialog_DevMusic.cs` | Music debugger |
| `Dialog_DevPalette.cs` | Color palette viewer |
| `MeasureTool.cs` | Distance measurement |
| `MeasureWorldDistanceTool.cs` | World distance measurement |
| `TweakValue.cs` | Value tweaking utility |
| `DebugHistogram.cs` | Histogram display |
| `DebugTables.cs` | Table generation |

---

## External Libraries

### Gilzoide.ManagedJobs (4 files)

Job scheduling library for performance optimization:

| File | Purpose |
|------|---------|
| `ManagedJob.cs` | Base managed job |
| `ManagedJobFor.cs` | For-loop job |
| `ManagedJobParallelFor.cs` | Parallel for-loop |
| `ManagedJobParallelForTransform.cs` | Transform parallel job |

### KTrie

Trie data structure library for efficient string/key lookups.

### DelaunatorSharp

Delaunay triangulation library for mesh generation.

### Ionic.Zlib

Compression library for save file compression.

### Ionic.Crc

CRC checksum library for data integrity.

### NVorbis.NAudioSupport (1 file)

| File | Purpose |
|------|---------|
| `VorbisWaveReader.cs` | Ogg Vorbis audio decoder |

### RuntimeAudioClipLoader (3 files)

| File | Purpose |
|------|---------|
| `AudioFormat.cs` | Audio format definitions |
| `CustomAudioFileReader.cs` | Custom audio reading |
| `Manager.cs` | Audio loading manager |

### Unity.Collections (4 files)

Unity collections and data structures:

| File | Purpose |
|------|---------|
| `NativeHeapIndex.cs` | Heap index |
| `HeapData.cs` | Heap data structure |
| `HeapNode.cs` | Heap node |
| `NativeHeapDebugView.cs` | Debug view |
| `UnsafeHeap.cs` | Unsafe heap |

---

## Architecture Patterns

### Component System

Throughout the codebase, there's heavy use of a component system where:

- **Base classes** (e.g., `Comp`, `WorldObjectComp`, `AbilityComp`) define extensible properties
- **Derived classes** implement specific behaviors
- **Properties classes** (`CompProperties_*`) define configurable data

This allows for modular, extensible functionality without deep inheritance hierarchies.

### Def System

Definitions (Defs) are serializable configuration objects that allow data-driven design:

- **`*Def` classes** - Define game content (e.g., `ThingDef`, `PawnKindDef`, `RecipeDef`)
- **`*DefOf` classes** - Static references to commonly-used defs for performance
- Allow modding through XML configuration without code changes

### Generation System

Procedural generation uses recursive symbol resolution:

- **`SymbolResolver`** - Resolves abstract symbols into concrete map elements
- Parameters are passed down recursively through `ResolveParams`
- Each resolver can spawn child resolvers
- Enables complex generation from simple rules

### Pawn AI Architecture

Multi-layered decision system:

1. **ThinkNode tree** - High-level decisions (what should I do?)
2. **JobGiver** - Job assignment logic (what job should I take?)
3. **Job with Toils** - Task execution (how do I do it?)
4. **JobDriver** - Specific job implementation (detailed execution)

This separation allows for flexible, maintainable AI behavior.

### Networking & Save System

- **`Scribe` classes** handle serialization
- **`ExposeData` pattern** for save/load consistency
- Cross-reference management for object persistence
- Version compatibility handling via `BackCompatibility` classes

### Event System

- **Signals** - String-based event notifications
- **Triggers** - Condition-based event firing
- **History Events** - Tracked game events for quests and records

### Patching System (Harmony)

RimWorld uses Harmony for runtime code modification:

- **Prefix patches** - Run before original method
- **Postfix patches** - Run after original method
- **Transpiler patches** - Modify IL code directly

This is how mods like RimWorld Access modify game behavior without changing game files.

---

## Modding Notes

### Key Directories for Modders

1. **RimWorld/** - Game-specific systems (factions, jobs, alerts, abilities)
2. **Verse/** - Core engine (coordinates, UI, sound, data structures)
3. **Verse.AI/** - AI behavior system
4. **RimWorld.Planet/** - World map and overland travel
5. **RimWorld.BaseGen/** - Structure generation
6. **RimWorld.QuestGen/** - Quest system

### Common Modding Entry Points

- **`Def` XML files** - Define new content
- **Harmony patches** - Modify existing behavior
- **`MelonMod` classes** - Mod entry points
- **Component system** - Extend buildings/pawns
- **ThinkNodes/JobGivers** - Custom AI behaviors

### Important Base Classes

- `Thing` - Physical objects in the world
- `Pawn` - Characters (colonists, animals, enemies)
- `Building` - Constructed structures
- `WorldObject` - Objects on the world map
- `Def` - Definition objects (loaded from XML)
- `Comp` - Component for Things
- `JobDriver` - AI job implementation
- `ThinkNode` - AI decision node

### Useful Utilities

- `GenSpawn` - Spawning things
- `GenPlace` - Placing things
- `GenCollection` - Collection operations
- `GenText` - Text utilities
- `Find.*` - Access game managers (e.g., `Find.World`, `Find.Maps`)

---

## References

- **Decompiled Source Location:** `C:\Users\Shane Earley\documents\personal_projects\rimworld\decompiled\`
- **RimWorld Access Mod:** `C:\Users\Shane Earley\documents\personal_projects\rimworld\mod\`
- **Total Files Documented:** ~9,000+
- **Major Namespaces:** 14+

---

## Changelog

- **2025-10-21** - Initial API reference creation
  - Documented all major namespaces
  - Listed key files and their purposes
  - Included architecture patterns
  - Added modding notes

---

*This reference is based on the decompiled RimWorld source code and is intended for educational and modding purposes only. All rights to RimWorld belong to Ludeon Studios.*
