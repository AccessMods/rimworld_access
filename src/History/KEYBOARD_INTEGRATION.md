# Keyboard Integration Guide

Complete guide to integrating with UnifiedKeyboardPatch for the History module.

---

## Overview

UnifiedKeyboardPatch is the central keyboard input router for all accessibility features. It patches `UIRoot.UIRootOnGUI` at Prefix level and routes ALL keyboard input to appropriate State classes based on a priority system.

**Location:** `src/Input/UnifiedKeyboardPatch.cs` (~3,470 lines)

---

## Priority System

**Lower numbers = higher priority** (processed first)

### Priority Ranges

| Range | Purpose | Examples |
|-------|---------|----------|
| -1 to 0 | Blocking states (text input, modal dialogs) | ZoneRenameState, WindowlessDialogState |
| 0 to 1 | World map and overlay states | WorldObjectSelectionState, QuantityMenuState |
| 1 to 3 | Caravan and transport states | CaravanFormationState, TransportPodLoadingState |
| 3 to 5 | UI menus | TradeNavigationState, ResearchMenuState |
| 5 to 7 | Pawn/animal menus | AssignMenuState, AnimalsMenuState |
| 8 | Global Escape | Opens pause menu |
| 9 | Global Enter | Opens inspection |
| 10 | Critical handlers | Float menu orders |

### Why Priority Matters

When overlay menus are stacked (e.g., CaravanFormation → QuantityMenu → Inspection):
- QuantityMenuState (0.25) is checked BEFORE CaravanFormationState (0.3)
- Escape closes the topmost menu only
- Lower priority states don't see the key if higher priority consumed it

---

## Core Pattern

```csharp
// In UnifiedKeyboardPatch.Prefix()

// PRIORITY X.X: My Menu
if (MyMenuState.IsActive && !OverlayState.IsActive)
{
    if (MyMenuState.HandleInput(key))
    {
        Event.current.Use();  // CRITICAL: Consume event
        return;               // Exit early
    }
}
```

### Key Rules

1. **Always check IsActive** before calling HandleInput
2. **Exclude overlays** with `&& !OverlayState.IsActive`
3. **Call Event.current.Use()** after handling
4. **Return early** to prevent lower priorities from running

---

## HandleInput Pattern

State classes should implement:

```csharp
public static bool HandleInput(KeyCode key, bool shift = false, bool ctrl = false, bool alt = false)
{
    if (!isActive) return false;

    switch (key)
    {
        case KeyCode.Escape:
            if (typeahead.HasActiveSearch)
            {
                typeahead.ClearSearchAndAnnounce();
                return true;  // Handled - cleared search
            }
            Close();
            return true;  // Handled - closed menu

        case KeyCode.DownArrow:
            SelectNext();
            return true;

        case KeyCode.UpArrow:
            SelectPrevious();
            return true;

        case KeyCode.Return:
        case KeyCode.KeypadEnter:
            ExecuteSelected();
            return true;

        default:
            return false;  // Not handled - let next priority try
    }
}
```

**Return values:**
- `true` = "I handled this key" → Caller calls Event.current.Use()
- `false` = "Not my key" → Caller continues to next priority

---

## THE ESCAPE KEY PROBLEM (CRITICAL)

### The Problem

RimWorld has TWO independent keyboard systems:
1. **UnifiedKeyboardPatch** - Our handler in UIRoot.UIRootOnGUI
2. **Window.OnCancelKeyPressed** - RimWorld's built-in, called by WindowStack

When user presses Escape:
- Our patch runs and calls `Event.current.Use()`
- RimWorld's Window system STILL calls `OnCancelKeyPressed()` anyway
- **Result: TWO dialogs close instead of one**

### What Does NOT Work

```csharp
// DOES NOT WORK - RimWorld ignores "used" flag
Event.current.Use();

// DOES NOT WORK - Timing issues
Event.current.keyCode = KeyCode.None;

// DOES NOT WORK - Frame counter unreliable
if (closedOnFrame == Time.frameCount) return;
```

### THE SOLUTION: Patch Window.OnCancelKeyPressed

```csharp
[HarmonyPatch(typeof(Window), "OnCancelKeyPressed")]
[HarmonyPrefix]
public static bool Window_OnCancelKeyPressed_Prefix(Window __instance)
{
    // Only intercept for specific dialog types
    if (__instance is MainTabWindow_History)
    {
        // Block game's Escape if our overlay is active
        if (HistoryArchiveState.IsActive || StatisticsMenuState.IsActive)
        {
            return false;  // Skip original - let our state handle Escape
        }

        // Block if typeahead search active
        if (HistoryState.HasActiveTypeahead)
        {
            return false;  // Clear search first
        }
    }

    return true;  // Let original run
}
```

### Why This Works

- Harmony Prefix runs BEFORE RimWorld's code
- Returning `false` skips the original method entirely
- Complete control over what gets closed

---

## Adding the History Module: Step by Step

### Step 1: Create State Classes

```csharp
// src/History/HistoryState.cs
public static class HistoryState
{
    private static bool isActive;
    private static HistoryTab currentTab;

    public static bool IsActive => isActive;
    public static HistoryTab CurrentTab => currentTab;

    public static void Open()
    {
        isActive = true;
        currentTab = HistoryTab.Graph;  // Default
        TolkHelper.Speak("History. Graph tab.");
    }

    public static void Close()
    {
        isActive = false;
        // Close any sub-states
        HistoryGraphState.Close();
        HistoryArchiveState.Close();
        HistoryStatisticsState.Close();
    }

    public static void SwitchToTab(HistoryTab tab)
    {
        // Close current tab's state
        CloseCurrentTabState();

        currentTab = tab;

        // Open new tab's state
        OpenCurrentTabState();

        AnnounceTab();
    }

    public static bool HandleInput(KeyCode key, bool shift, bool ctrl, bool alt)
    {
        // Tab switching: Tab / Shift+Tab
        if (key == KeyCode.Tab)
        {
            if (shift)
                SwitchToPreviousTab();
            else
                SwitchToNextTab();
            return true;
        }

        // Escape: Close history window
        if (key == KeyCode.Escape)
        {
            // Let sub-states handle Escape first
            return false;
        }

        return false;
    }
}
```

### Step 2: Create Harmony Patches

```csharp
// src/History/HistoryPatch.cs
[HarmonyPatch(typeof(MainTabWindow_History))]
public static class HistoryPatch
{
    [HarmonyPatch("PostOpen")]
    [HarmonyPostfix]
    public static void PostOpen_Postfix(MainTabWindow_History __instance)
    {
        HistoryState.Open();
    }

    [HarmonyPatch("PostClose")]
    [HarmonyPostfix]
    public static void PostClose_Postfix()
    {
        HistoryState.Close();
    }

    // CRITICAL: Block RimWorld's Escape handling
    [HarmonyPatch(typeof(Window), "OnCancelKeyPressed")]
    [HarmonyPrefix]
    public static bool Window_OnCancelKeyPressed_Prefix(Window __instance)
    {
        if (__instance is MainTabWindow_History)
        {
            // Block if any sub-state is handling input
            if (HistoryGraphState.IsActive ||
                HistoryArchiveState.IsActive ||
                HistoryStatisticsState.IsActive)
            {
                return false;
            }
        }
        return true;
    }
}
```

### Step 3: Add to UnifiedKeyboardPatch

Find appropriate priority level (suggest 4.0-4.5 for History):

```csharp
// ===== PRIORITY 4.2: History Tab =====
if (HistoryState.IsActive)
{
    // Check sub-states first (they have higher priority within History)

    // Statistics navigation
    if (HistoryStatisticsState.IsActive)
    {
        if (HistoryStatisticsState.HandleInput(key))
        {
            Event.current.Use();
            return;
        }
    }

    // Archive/Messages navigation
    if (HistoryArchiveState.IsActive)
    {
        if (HistoryArchiveState.HandleInput(key, shift, ctrl, alt))
        {
            Event.current.Use();
            return;
        }
    }

    // Graph navigation
    if (HistoryGraphState.IsActive)
    {
        if (HistoryGraphState.HandleInput(key, shift, ctrl, alt))
        {
            Event.current.Use();
            return;
        }
    }

    // Tab-level input (Tab key to switch tabs)
    if (HistoryState.HandleInput(key, shift, ctrl, alt))
    {
        Event.current.Use();
        return;
    }
}
```

### Step 4: Add to KeyboardHelper

```csharp
// In KeyboardHelper.IsAnyAccessibilityMenuActive()
public static bool IsAnyAccessibilityMenuActive()
{
    return WindowlessFloatMenuState.IsActive
        || HistoryState.IsActive           // Add History
        || HistoryArchiveState.IsActive    // Add sub-states if needed
        || HistoryStatisticsState.IsActive
        || HistoryGraphState.IsActive
        // ... other states ...
        ;
}
```

---

## Example: Research Menu Integration

Study `WindowlessResearchMenuState` as a reference (Priority 4.8):

```csharp
// From UnifiedKeyboardPatch.cs lines ~2050-2130

if (WindowlessResearchMenuState.IsActive && !WindowlessDialogState.IsActive)
{
    // Detail view sub-navigation
    if (WindowlessResearchMenuState.IsInDetailView)
    {
        if (WindowlessResearchMenuState.HandleDetailInput(key))
        {
            Event.current.Use();
            return;
        }
    }

    // List navigation
    if (key == KeyCode.UpArrow) { WindowlessResearchMenuState.SelectPrevious(); Event.current.Use(); return; }
    if (key == KeyCode.DownArrow) { WindowlessResearchMenuState.SelectNext(); Event.current.Use(); return; }
    if (key == KeyCode.Return) { WindowlessResearchMenuState.EnterDetailView(); Event.current.Use(); return; }
    if (key == KeyCode.Escape) { WindowlessResearchMenuState.HandleEscape(); Event.current.Use(); return; }

    // Typeahead
    if (char.IsLetterOrDigit(evt.character))
    {
        WindowlessResearchMenuState.HandleTypeahead(evt.character);
        Event.current.Use();
        return;
    }
}
```

---

## Example: Assignment Menu Integration

Study `AssignMenuState` as reference (Priority 6.0):

```csharp
// From UnifiedKeyboardPatch.cs lines ~2400-2500

if (AssignMenuState.IsActive && !WindowlessDialogState.IsActive)
{
    if (AssignMenuState.HandleInput(key, shift, ctrl, alt))
    {
        Event.current.Use();
        return;
    }
}
```

AssignMenuState has internal sub-state handling for different views.

---

## Common Mistakes to Avoid

### Mistake 1: Forgetting Event.current.Use()

```csharp
// WRONG - Event leaks to game
if (key == KeyCode.F)
{
    MyState.DoSomething();
    return;  // Missing Use()!
}

// CORRECT
if (key == KeyCode.F)
{
    MyState.DoSomething();
    Event.current.Use();
    return;
}
```

### Mistake 2: Wrong Priority Order

```csharp
// WRONG - Main state before overlay
if (HistoryState.IsActive) { ... }       // Priority 4.2
if (HistoryArchiveState.IsActive) { ... } // This is sub-state!

// CORRECT - Check sub-states within parent's block
if (HistoryState.IsActive)
{
    if (HistoryArchiveState.IsActive) { ... }  // Sub-state first
    // Then parent-level handling
}
```

### Mistake 3: Not Excluding Overlays

```csharp
// WRONG - Overlay might interfere
if (HistoryState.IsActive)
{
    // What if WindowlessDialogState is showing a confirmation?
}

// CORRECT
if (HistoryState.IsActive && !WindowlessDialogState.IsActive)
{
    // Safe from confirmation dialogs
}
```

### Mistake 4: Missing Escape Isolation

```csharp
// WRONG - Double close bug
// Your Escape handler AND Window.OnCancelKeyPressed both run

// CORRECT - Patch the Cancel handler
[HarmonyPatch(typeof(Window), "OnCancelKeyPressed")]
[HarmonyPrefix]
public static bool Prefix(Window __instance)
{
    if (__instance is MainTabWindow_History && MySubState.IsActive)
        return false;
    return true;
}
```

### Mistake 5: Checking IsActive After Null Reference

```csharp
// WRONG - May crash if items is null
if (HistoryState.items.Count > 0)

// CORRECT - Check IsActive first (guarantees initialization)
if (HistoryState.IsActive && HistoryState.items.Count > 0)
```

---

## Escape Key Flow Chart

```
User presses Escape
    ↓
UnifiedKeyboardPatch.Prefix()
    ├─ Priority -1 to 0: Modal states
    ├─ Priority 0 to 4: Overlay and menu states
    │   ├─ HistoryArchiveState.IsActive?
    │   │   ├─ Has search? → Clear search, return
    │   │   └─ No search? → Close archive view, return
    │   └─ HistoryStatisticsState.IsActive?
    │       └─ Close statistics view, return
    ├─ Priority 4.2: HistoryState
    │   └─ All sub-states closed? → Let Escape close window
    ├─ Priority 8: Global Escape
    │   └─ !IsAnyAccessibilityMenuActive? → Open pause menu
    ↓
KeyboardIsolationPatch.Prefix()
    └─ IsAnyAccessibilityMenuActive? → Use() remaining events
    ↓
Window.OnCancelKeyPressed() [If not patched to return false]
    └─ Closes the RimWorld dialog
```

---

## Testing Checklist

- [ ] Tab opens with F9 key
- [ ] Tab switching works (Tab / Shift+Tab)
- [ ] Each sub-tab navigates correctly
- [ ] Escape closes only the topmost view
- [ ] Escape with search clears search first
- [ ] No double-close bugs
- [ ] Other accessibility menus still work
- [ ] F9 key correctly opens History when not in other menus
- [ ] Global Escape still opens pause when History closed

---

## Suggested Priority for History

```
Priority 4.2: HistoryState (main tab)
    Priority 4.21: HistoryGraphState (sub-state)
    Priority 4.22: HistoryArchiveState (sub-state)
    Priority 4.23: HistoryStatisticsState (sub-state)
```

Place after WindowlessResearchMenuState (4.8) but these are all within the HistoryState block, so actual priority is determined by check order within that block.

---

## Files to Modify

1. **src/Input/UnifiedKeyboardPatch.cs**
   - Add HistoryState handling at priority ~4.2
   - Add to existing window checks

2. **src/Input/KeyboardHelper.cs**
   - Add HistoryState (and sub-states) to IsAnyAccessibilityMenuActive()

3. **src/History/HistoryPatch.cs** (new)
   - PostOpen/PostClose patches
   - Window.OnCancelKeyPressed patch

4. **src/History/HistoryState.cs** (new)
   - Main state class

5. **src/History/HistoryGraphState.cs** (new)
   - Graph tab state

6. **src/History/HistoryArchiveState.cs** (new)
   - Messages/Archive tab state

7. **src/History/HistoryStatisticsState.cs** (new)
   - Statistics tab state
