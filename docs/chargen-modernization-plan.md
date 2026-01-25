# Character Creation Modernization Plan

This document captures all issues, bugs, and enhancement requests for the RimWorld Access character creation flow. Each section corresponds to a screen in the creation process.

## Source Code Locations

- **Mod source**: `C:\Users\jar\repos\rimworld_access\chargen\src\`
- **Game decompiled source**: `C:\Users\jar\repos\rimworld_access\decompiled\`

---

## 1. Choose Scenario Screen

### Bugs
- [ ] **BUG-1.1**: Category prefix "[Built-in]" appears at start of announcement instead of end
  - Current: "[Built-in] Crashlanded - Three crashlanded survivors..."
  - Expected: "Crashlanded - Three crashlanded survivors... (Built-in)"
  - Location: `ScenarioNavigationState.cs` line ~195 in `CopySelectedToClipboard()`

### Missing Features
- [ ] **MISSING-1.1**: Scenario Builder is completely inaccessible (appears as "Custom" button in UI)
- [ ] **MISSING-1.2**: Tab key should navigate to scenario details panel showing starting items/resources
- [ ] **MISSING-1.3**: Tooltips should be appended to spoken text where they exist

### Enhancements
- [ ] **ENH-1.1**: Present all information the game shows about each scenario (starting items, conditions, etc.)

---

## 2. Choose AI Storyteller Screen

### Bugs
- [ ] **BUG-2.1**: Shift+Tab doesn't work - only Tab works for navigation between sections
- [ ] **BUG-2.2**: Permadeath/Reload mode toggle requires pressing Enter twice to select
  - Should be a checkbox toggled with Space
  - Should be unchecked by default
  - Should allow proceeding without ever touching it
- [ ] **BUG-2.3**: XML section title tags appearing in difficulty selector text (e.g., `<section_title>`, `</section_title>`)
  - Note: Similar issue was fixed in game settings menu - reuse that code

### Missing Features
- [ ] **MISSING-2.1**: Custom difficulty settings not accessible from character creation
  - Already built for pause menu (Edit Storyteller Settings)
  - Need to wire existing system into this screen
  - Ensure pause-menu-specific code doesn't interfere

### Enhancements
- [ ] **ENH-2.1**: Tutorial/help text should announce once on screen entry, not on every tab cycle
  - Example: "The AI storyteller creates events like pirate raids..."

---

## 3. Create World Screen

### Bugs
- [ ] **BUG-3.1**: Slider direction is inverted - right arrow moves to "Low" when already at "Normal"
  - Likely array indexing issue (position 0 treated as "Normal" but actual values start at 1)
  - Right arrow should increase value, left arrow should decrease

### Missing Features
- [ ] **MISSING-3.1**: Faction configuration panel not accessible
  - Tab should navigate to faction settings
- [ ] **MISSING-3.2**: Advanced settings panel not accessible
  - Tab should navigate to advanced settings (third tab stop)

### Enhancements
- [ ] **ENH-3.1**: Remove "Field" prefix from announcements (say "Planet Coverage" not "Field Planet Coverage")
- [ ] **ENH-3.2**: World seed text input should use the new TextInputState class
- [ ] **ENH-3.3**: Tab navigation structure should be:
  1. World Settings (seed, coverage, etc.)
  2. Faction Settings
  3. Advanced Settings
  4. Enter to generate

---

## 4. World Map (Site Selection)

### Critical Bugs
- [ ] **BUG-4.1**: "No starting site selected. Press R to randomize..." message repeats endlessly until a key is pressed
  - Extremely poor UX - must be fixed
- [ ] **BUG-4.2**: Dialog boxes (e.g., acidic smog warning) don't receive keyboard focus
  - Arrow keys control the next screen instead of the dialog
  - Pressing Escape dismisses both the dialog AND the next screen
  - User gets sent back to Create World screen

### Missing Features
- [ ] **MISSING-4.1**: Features from in-game world map not available:
  - Z key scanner/search
  - Number keys for tile stats
  - All functionality from `src/World/` module
- [ ] **MISSING-4.2**: Terrain descriptions not announced during navigation
  - Game provides rich flavor text (e.g., "Forests of coniferous trees, despite the harsh winters...")
  - Should be spoken as user arrows between tiles

### Enhancements
- [ ] **ENH-4.1**: Use same arrow key navigation math as in-game world map
  - Reference: `WorldMapNavigationHelper`
  - Ensure consistent movement behavior between character creation and in-game world maps
- [ ] **ENH-4.2**: Experience between character creation world map and in-game world map should be identical or very similar
- [ ] **ENH-4.3**: Always use translation strings for localization

---

## 5. Choose Ideology Screen (Ideology DLC)

### Bugs
- [ ] **BUG-5.1**: Arrow keys can move between what should be separate tab sections
  - "Load Saved Ideology" should be last item before tab stop
  - Arrow keys should NOT cross into preset/custom sections

### Critical Issues
- [ ] **CRITICAL-5.1**: Entering custom ideology creation completely breaks keyboard access
  - Shows "You must choose a structure meme" message
  - All keyboard navigation lost
  - Requires game restart to recover

### Missing Features
- [ ] **MISSING-5.1**: Custom Fluid Ideology builder - completely inaccessible (BUILD FROM SCRATCH)
- [ ] **MISSING-5.2**: Custom Fixed Ideology builder - completely inaccessible (BUILD FROM SCRATCH)
- [ ] **MISSING-5.3**: Tab stops needed between sections:
  1. Play Classic / Load Saved
  2. Custom Ideologies (Fluid/Fixed)
  3. Ideology Presets

### Enhancements
- [ ] **ENH-5.1**: Support MenuHelper features (type-ahead search)
- [ ] **ENH-5.2**: If ideology presets have additional statistics, Tab should show them

---

## 6. Create Characters Screen

### Bugs
- [ ] **BUG-6.1**: After randomizing pawn (R key), current position not announced
  - Should announce: new pawn name, age, then current tree position
  - Currently only says "Randomized Pawn 1" + name + age
- [ ] **BUG-6.2**: Drag/drop uses Spacebar instead of Control+Up/Down
  - Inconsistent with rest of UI
  - Control+Up/Down should reorder pawns
  - Should announce new position (e.g., "James now between Alice and Julia" or "James at top, pawn 1")

### Critical UX Issues
- [ ] **CRITICAL-6.1**: Enter key immediately starts the game
  - Too easy to accidentally trigger (reflex to press Enter)
  - Should require Alt+S or Shift+Enter to start game
  - Allow Enter for tree navigation (expand/collapse)

### Enhancements
- [ ] **ENH-6.1**: Convert to proper TreeView
  - Currently simulates treeview without being one
  - Right arrow to expand pawn details
  - Left arrow to collapse
  - Preserve cursor position when tree rebuilds (e.g., after randomize)
- [ ] **ENH-6.2**: Remove verbose instructions
  - Don't say "press right arrow to view details, left arrow to go back" every time
  - TreeView behavior is self-explanatory
- [ ] **ENH-6.3**: Respect item group announcement settings from options menu
- [ ] **ENH-6.4**: Support standard menu features:
  - Type-ahead search
  - Star key navigation
  - All modern menu conveniences
- [ ] **ENH-6.5**: Show skill descriptions if game provides them
  - e.g., "Mining level 3 - Used for extracting resources from rock"
- [ ] **ENH-6.6**: Rename (E key) should use new TextInputState class
- [ ] **ENH-6.7**: Pawn details should mirror in-game inspect screen
  - Familiarize new players with the interface they'll use in-game
  - Exclude irrelevant tabs (gear, etc.)

---

## 7. Screens to Build from Scratch

### New Development Required
- [ ] **NEW-7.1**: Scenario Generator/Builder
  - Accessed via "Custom" button on Choose Scenario screen
  - Completely inaccessible currently
  - Full keyboard navigation needed

- [ ] **NEW-7.2**: Ideology Builder (Custom Fluid & Fixed)
  - Currently breaks the game when entered
  - Requires full implementation
  - Structure meme selection, precepts, etc.

---

## 8. Cross-Cutting Concerns

### Consistency
- [ ] **CROSS-8.1**: All text inputs should use TextInputState class
- [ ] **CROSS-8.2**: All menus should support MenuHelper features
- [ ] **CROSS-8.3**: Always use translation strings for localization
- [ ] **CROSS-8.4**: Always provide descriptive/flavor text that the game displays
- [ ] **CROSS-8.5**: Tooltips should be appended to announcements as additional sentences

### Code Reuse Opportunities
- World map features from `src/World/` module
- XML tag stripping from game settings menu
- Custom difficulty from pause menu storyteller settings
- TextInputState for all text fields
- TreeView for hierarchical data

---

## Priority Order (By Character Creation Flow)

Work through issues in the order a user experiences them:

### Phase 1: Choose Scenario Screen
- BUG-1.1 - "Building" prefix on scenarios
- MISSING-1.2 - Tab to scenario details panel
- MISSING-1.3 - Tooltips appended to spoken text
- ENH-1.1 - Present all scenario information
- *(Defer: NEW-7.1 Scenario Builder - build from scratch later)*

### Phase 2: Choose AI Storyteller Screen
- BUG-2.1 - Shift+Tab not working
- BUG-2.2 - Permadeath toggle requires double-enter (convert to checkbox)
- BUG-2.3 - XML tags in difficulty text
- MISSING-2.1 - Wire in custom difficulty settings
- ENH-2.1 - Tutorial text announces once only

### Phase 3: Create World Screen
- BUG-3.1 - Slider direction inverted
- ENH-3.1 - Remove "Field" prefix
- ENH-3.2 - Use TextInputState for world seed
- MISSING-3.1 - Faction settings tab
- MISSING-3.2 - Advanced settings tab
- ENH-3.3 - Proper tab navigation structure

### Phase 4: World Map (Site Selection)
- BUG-4.1 - Endless "no site selected" message loop
- BUG-4.2 - Dialog boxes not receiving keyboard focus
- MISSING-4.1 - Port features from in-game world map (Z search, number keys)
- MISSING-4.2 - Terrain descriptions announced
- ENH-4.1 - Use WorldMapNavigationHelper math
- ENH-4.2 - Match in-game world map experience

### Phase 5: Choose Ideology Screen
- BUG-5.1 - Arrow keys crossing tab boundaries
- MISSING-5.3 - Tab stops between sections
- ENH-5.1 - MenuHelper features (type-ahead)
- CRITICAL-5.1 - Prevent keyboard loss on custom ideology entry
- *(Defer: NEW-7.2 Ideology Builder - build from scratch later)*

### Phase 6: Create Characters Screen
- CRITICAL-6.1 - Enter accidentally starts game (change to Alt+S)
- ENH-6.1 - Convert to proper TreeView
- ENH-6.2 - Remove verbose instructions
- ENH-6.3 - Respect item group settings
- ENH-6.4 - Standard menu features (type-ahead, star key)
- BUG-6.1 - Announce position after randomize
- BUG-6.2 - Drag/drop with Ctrl+Up/Down
- ENH-6.5 - Skill descriptions
- ENH-6.6 - TextInputState for rename
- ENH-6.7 - Mirror in-game inspect screen

### Phase 7: New Screens (Build from Scratch)
- NEW-7.1 - Scenario Builder
- NEW-7.2 - Ideology Builder (Fluid & Fixed)

---

## Implementation Notes

### Files to Investigate
- `src/MainMenu/` - Character creation screens
- `src/World/` - World map features to port
- `src/UI/` - Dialog handling, menu helpers
- Game settings menu - XML tag stripping code
- Pause menu - Custom difficulty code
- `WorldMapNavigationHelper` - Navigation math

### Testing Checklist
- [ ] Complete character creation flow without sighted assistance
- [ ] All keyboard shortcuts working
- [ ] No repeated/looping announcements
- [ ] Dialogs receive proper focus
- [ ] Tab/Shift+Tab work in all screens
- [ ] TreeView navigation intuitive
- [ ] No accidental game starts

---

## PHASE 1 IMPLEMENTATION PLAN: Choose Scenario Screen

### Files to Modify
- `src/MainMenu/ScenarioNavigationState.cs`
- `src/MainMenu/ScenarioSelectionPatch.cs`

### BUG-1.1: Move Category Suffix to End

**Current code (ScenarioNavigationState.cs lines 71-79):**
```csharp
private static void CopySelectedToClipboard()
{
    Scenario selected = SelectedScenario;
    if (selected == null) return;

    string categoryPrefix = GetCategoryPrefix(selected);
    string text = $"{categoryPrefix}{selected.name} - {selected.summary}";
    TolkHelper.Speak(text);
}
```

**Change to:**
```csharp
private static void CopySelectedToClipboard()
{
    Scenario selected = SelectedScenario;
    if (selected == null) return;

    string categorySuffix = GetCategorySuffix(selected);
    string text = $"{selected.name} - {selected.summary}{categorySuffix}";
    TolkHelper.Speak(text);
}
```

**Also rename GetCategoryPrefix to GetCategorySuffix (lines 82-95):**
```csharp
private static string GetCategorySuffix(Scenario scenario)
{
    switch (scenario.Category)
    {
        case ScenarioCategory.FromDef:
            return " (Built-in)";
        case ScenarioCategory.CustomLocal:
            return " (Custom)";
        case ScenarioCategory.SteamWorkshop:
            return " (Workshop)";
        default:
            return "";
    }
}
```

**Also update ScenarioSelectionPatch.cs lines 47-48 and 225-237** to match.

### MISSING-1.2: Tab to Scenario Details Panel

**Add to ScenarioNavigationState.cs:**
```csharp
public static bool DetailPanelActive { get; private set; } = false;

public static void ToggleDetailPanel()
{
    DetailPanelActive = !DetailPanelActive;
    if (DetailPanelActive)
    {
        AnnounceScenarioDetails();
    }
    else
    {
        TolkHelper.Speak("Scenario list");
        CopySelectedToClipboard();
    }
}

private static void AnnounceScenarioDetails()
{
    Scenario selected = SelectedScenario;
    if (selected == null) return;

    string fullInfo = selected.GetFullInformationText();
    TolkHelper.Speak($"Scenario details: {fullInfo}");
}
```

**Add Tab handling to ScenarioSelectionPatch.cs Prefix (after line 89):**
```csharp
else if (keyCode == KeyCode.Tab)
{
    ScenarioNavigationState.ToggleDetailPanel();
    Event.current.Use();
    patchActive = true;
}
```

### MISSING-1.3: Append Tooltips for Invalid Scenarios

**Update CopySelectedToClipboard in ScenarioNavigationState.cs:**
```csharp
private static void CopySelectedToClipboard()
{
    Scenario selected = SelectedScenario;
    if (selected == null) return;

    string categorySuffix = GetCategorySuffix(selected);
    string text = $"{selected.name} - {selected.summary}{categorySuffix}";

    // Add tooltip for invalid scenarios
    if (!selected.valid)
    {
        text += $". Warning: {"ScenPart_Error".Translate()}";
    }

    TolkHelper.Speak(text);
}
```

---

## PHASE 2 IMPLEMENTATION PLAN: Choose AI Storyteller Screen

### Files to Modify
- `src/MainMenu/StorytellerNavigationState.cs`
- `src/MainMenu/StorytellerSelectionPatch.cs`

### BUG-2.1: Add Shift+Tab Support

**Update StorytellerSelectionPatch.cs lines 46-52:**
```csharp
if (keyCode == KeyCode.Tab && !Event.current.shift)
{
    // Forward cycle
    CycleNavigationModeForward();
    Event.current.Use();
    patchActive = true;
}
else if (keyCode == KeyCode.Tab && Event.current.shift)
{
    // Backward cycle
    CycleNavigationModeBackward();
    Event.current.Use();
    patchActive = true;
}
```

**Rename CycleNavigationMode to CycleNavigationModeForward and add new method:**
```csharp
private static void CycleNavigationModeForward()
{
    switch (currentMode)
    {
        case NavigationMode.Storyteller:
            currentMode = NavigationMode.Difficulty;
            AnnounceDifficultyMode();
            break;
        case NavigationMode.Difficulty:
            currentMode = NavigationMode.Permadeath;
            AnnouncePermadeathMode();
            break;
        case NavigationMode.Permadeath:
            currentMode = NavigationMode.Storyteller;
            AnnounceStorytellerMode();
            break;
    }
}

private static void CycleNavigationModeBackward()
{
    switch (currentMode)
    {
        case NavigationMode.Storyteller:
            currentMode = NavigationMode.Permadeath;
            AnnouncePermadeathMode();
            break;
        case NavigationMode.Difficulty:
            currentMode = NavigationMode.Storyteller;
            AnnounceStorytellerMode();
            break;
        case NavigationMode.Permadeath:
            currentMode = NavigationMode.Difficulty;
            AnnounceDifficultyMode();
            break;
    }
}
```

### BUG-2.2: Permadeath as Proper Toggle

**Change permadeath handling in StorytellerSelectionPatch.cs:**
- Remove Enter requirement, make Space toggle it
- Set permadeathChosen = true on first visit to ensure game can proceed

**Update lines 65-73:**
```csharp
else if (keyCode == KeyCode.Space && currentMode == NavigationMode.Permadeath)
{
    StorytellerNavigationState.TogglePermadeath();
    Event.current.Use();
    patchActive = true;
}
```

**Update AnnouncePermadeathMode to show current state and toggle instruction:**
```csharp
private static void AnnouncePermadeathMode()
{
    // Ensure permadeathChosen is set so game can proceed
    if (!Find.GameInitData.permadeathChosen)
    {
        Find.GameInitData.permadeathChosen = true;
        Find.GameInitData.permadeath = false; // Default to Reload Anytime
    }

    string currentMode = Find.GameInitData.permadeath
        ? "Commitment Mode (Permadeath)"
        : "Reload Anytime Mode";
    TolkHelper.Speak($"Permadeath selection: {currentMode}. Press Space to toggle.");
}
```

### BUG-2.3: Strip XML Tags from Difficulty Description

**Update StorytellerNavigationState.cs line 148:**
```csharp
if (!string.IsNullOrEmpty(difficulty.description))
{
    text += $" - {difficulty.description.StripTags()}";
}
```

### ENH-2.1: Help Text Only on First Entry

**Add tracking flags to StorytellerSelectionPatch.cs:**
```csharp
private static bool announcedStorytellerHelp = false;
private static bool announcedDifficultyHelp = false;
private static bool announcedPermadeathHelp = false;

private static void AnnounceStorytellerMode()
{
    string announcement = "Storyteller selection";
    if (!announcedStorytellerHelp)
    {
        announcement += ". Use Up/Down arrows to choose storyteller";
        announcedStorytellerHelp = true;
    }
    TolkHelper.Speak(announcement);
}

private static void AnnounceDifficultyMode()
{
    string announcement = "Difficulty selection";
    if (!announcedDifficultyHelp)
    {
        announcement += ". Use Up/Down arrows to choose difficulty";
        announcedDifficultyHelp = true;
    }
    TolkHelper.Speak(announcement);
}

private static void AnnouncePermadeathMode()
{
    // ... include current state
    if (!announcedPermadeathHelp)
    {
        // ... include help text
        announcedPermadeathHelp = true;
    }
}
```

**Reset flags in ResetAnnouncement():**
```csharp
public static void ResetAnnouncement()
{
    hasAnnouncedTitle = false;
    currentMode = NavigationMode.Storyteller;
    announcedStorytellerHelp = false;
    announcedDifficultyHelp = false;
    announcedPermadeathHelp = false;
}
```
