# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

RimWorld Access is a C# mod for RimWorld that provides screen reader accessibility. It uses Harmony patches to inject keyboard navigation into RimWorld's UI and the Tolk library to communicate with screen readers (NVDA, JAWS) with SAPI fallback.

**Technology Stack:**
- .NET Framework 4.7.2
- HarmonyLib 2.3.3 (runtime patching)
- Tolk.dll + nvdaControllerClient64.dll (screen reader integration via P/Invoke)
- RimWorld 1.6 assemblies

## Building and Testing
### Build Commands

```bash
# Build and auto-deploy to RimWorld/Mods/RimWorldAccess/
dotnet build

# Build release package to release/RimWorldAccess/
dotnet build -c Release
```

**Build Output:**
- DLL: `bin/Debug/net472/rimworld_access.dll`
- Auto-deploys to: `$(RimWorldDir)\Mods\RimWorldAccess\Assemblies\`
- Native DLLs (Tolk.dll, nvdaControllerClient64.dll) copied to mod root

## Code Architecture

### Core Pattern: State + Patch

Every feature follows this architecture:

1. **State class** (`*State.cs`)
   - Maintains navigation state (selected index, list of items, etc.)
   - `IsActive` flag checked by UnifiedKeyboardPatch
   - Methods: `Open()`, `Close()`, `SelectNext()`, `SelectPrevious()`
   - Calls `TolkHelper.Speak()` to announce selections

2. **Patch class** (`*Patch.cs`)
   - Harmony patches that intercept RimWorld methods
   - Initializes State when UI opens (PostOpen/Postfix)
   - Resets State when UI closes
   - May inject accessibility into rendering code

3. **Helper class** (`*Helper.cs`)
   - Data extraction utilities
   - Reusable functions for formatting announcements
   - No state management

### Module Organization

The codebase is organized into 18 modules by game feature:

| Module | Files | Purpose |
|--------|-------|---------|
| **Core/** | 2 | Mod entry point, Harmony initialization |
| **ScreenReader/** | 3 | TolkHelper and audio integration |
| **Input/** | 1 | UnifiedKeyboardPatch - central input router |
| **MainMenu/** | 19 | Main menu and game setup flow |
| **Map/** | 9 | Map navigation, cursor, scanner |
| **World/** | 8 | World map, settlements, caravans |
| **Building/** | 22 | Construction, zones, areas |
| **Inspection/** | 22 | Building/object inspection UI, Info Card |
| **Pawns/** | 25 | Pawn info and character tabs |
| **Work/** | 2 | Work priorities and schedules |
| **Animals/** | 6 | Animal and wildlife management |
| **Prisoner/** | 3 | Prisoner management |
| **Quests/** | 3 | Quests and notifications |
| **Combat/** | 2 | Combat and targeting |
| **Trade/** | 3 | Trading system |
| **Research/** | 2 | Research system |
| **UI/** | 13 | Generic dialogs and windowless menus |

Each module has its own `CLAUDE.md` with detailed documentation.

### Central Systems

**UnifiedKeyboardPatch** (`Input/UnifiedKeyboardPatch.cs`)
- Central keyboard input router for ALL accessibility features
- Patches `UIRoot.UIRootOnGUI` at Prefix level
- Priority system (lower number = higher priority, range -1 to 10)
- Checks `IsActive` flags before routing to State classes
- Calls `Event.current.Use()` to consume events and prevent default game behavior

**TolkHelper** (`ScreenReader/TolkHelper.cs`)
- Direct screen reader integration via P/Invoke
- `TolkHelper.Speak(text, priority)` used by all modules
- Three priorities: Low (don't interrupt), Normal, High (interrupt)
- Initialized in `Core/rimworld_access.cs`

**MapNavigationState** (`Map/MapNavigationState.cs`)
- Provides `CurrentCursorPosition` (IntVec3) used by 10+ modules
- Arrow key navigation with camera follow
- Jump modes for terrain features

### Dependency Graph

```
Core/rimworld_access.cs (entry point)
  └── ScreenReader/TolkHelper (initialize)
        └── Input/UnifiedKeyboardPatch (routes to all modules)
              ├── MainMenu/
              ├── Map/ → [Building, Inspection, Quests, Combat]
              ├── Pawns/ → [Work, Prisoner]
              ├── World/ → [Quests]
              ├── Animals/
              ├── Trade/
              ├── Research/
              └── UI/ → [All modules]
```

## Common Development Tasks

### Adding a New Feature

1. **Choose module directory** (or create new one under `src/`)
2. **Create State class:**
   ```csharp
   public static class MyFeatureState
   {
       public static bool IsActive { get; set; }
       private static int selectedIndex = 0;

       public static void Open()
       {
           IsActive = true;
           TolkHelper.Speak("Feature opened", SpeechPriority.Normal);
       }

       public static void Close()
       {
           IsActive = false;
       }
   }
   ```

3. **Create Patch class:**
   ```csharp
   [HarmonyPatch(typeof(RimWorldClass))]
   [HarmonyPatch("MethodName")]
   public static class MyFeaturePatch
   {
       [HarmonyPostfix]
       public static void Postfix()
       {
           MyFeatureState.Open();
       }
   }
   ```

4. **Add input routing to UnifiedKeyboardPatch.cs:**
   ```csharp
   // Priority 5: My Feature (K key)
   if (MyFeatureState.IsActive && key == KeyCode.K)
   {
       MyFeatureState.ExecuteAction();
       Event.current.Use();
       return;
   }
   ```

5. **Update module's CLAUDE.md**

### Modifying Existing Features

1. **Find the module** - Use module table above or search by feature name
2. **Read module's CLAUDE.md** - Understand dependencies and patterns
3. **Identify files:**
   - State class for navigation logic
   - Patch class for Harmony integration
   - Helper class for utilities
4. **Test thoroughly** - Verify screen reader announcements and keyboard navigation

### Finding Code

**By game feature:** Navigate to corresponding module directory (e.g., trading → `Trade/`)

**By keyboard shortcut:** Check `Input/UnifiedKeyboardPatch.cs` for complete routing

**By screen reader announcement:** Search for `TolkHelper.Speak()` calls
**RimWorld's decompiled code**: A decompiled copy of RimWorld is located at `./decompiled`. Search this code before making changes that require integration with the game's methods.  

## Harmony Patching Notes

- All patches auto-apply via `harmony.PatchAll()` in `Core/rimworld_access.cs`
- Use `[HarmonyPriority]` to control patch execution order (only needed for conflicts)
- **Prefix patches:** Run before original method, can block execution with `return false`
- **Postfix patches:** Run after original method completes
- Use `AccessTools` for reflection (accessing private methods/fields)

## Screen Reader Integration

- TolkHelper uses P/Invoke to native Tolk.dll functions
- Fallback chain: Detected screen reader → Direct NVDA → SAPI
- All navigation actions should announce via `TolkHelper.Speak()`
- Use `SpeechPriority.Low` for rapid navigation (don't interrupt)
- Use `SpeechPriority.High` for critical alerts

## State Lifecycle

1. Patch's PostOpen/Postfix initializes state: `MyState.Open()`, `IsActive = true`
2. UnifiedKeyboardPatch routes input to state while `IsActive == true`
3. State handles navigation, announces via TolkHelper
4. State closes: `MyState.Close()`, `IsActive = false`

## Important Conventions

- **IsActive flags:** All State classes must have this to prevent input conflicts
- **Event consumption:** Always call `Event.current.Use()` after handling input
- **Priority order:** Lower numbers = higher priority in UnifiedKeyboardPatch
- **Module CLAUDE.md:** Keep up to date with architectural changes
- **Conventional commits:** Use `feat:`, `fix:`, `refactor:`, `docs:`, etc.

## Workflow Notes

- **Main branch:** `master`
- **Bug reports:** Only test with Harmony + RimWorld Access enabled (no other mods)
- **Pull requests:** Link to issue using `Closes #123` or `Fixes #123`
- **Before opening PRs:** Open issue first for new features, wait for feedback

## Project Structure

```
mod/
├── src/                    # All C# source code (18 modules)
│   ├── Core/              # Entry point, initialization
│   ├── Input/             # UnifiedKeyboardPatch
│   ├── ScreenReader/      # TolkHelper, audio
│   ├── Map/               # Map navigation, scanner
│   └── [15 other modules]
├── About/                 # About.xml (mod metadata)
├── Sounds/                # Embedded audio resources
├── Tolk.dll               # Native screen reader library
├── nvdaControllerClient64.dll
├── rimworld_access.csproj # MSBuild project file
└── GamePaths.props.template

Build output:
├── bin/Debug/net472/      # Compiled DLL
└── release/RimWorldAccess/ # Release package
```

