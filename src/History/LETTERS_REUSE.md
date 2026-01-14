# Letters Tab Reuse for History Archive

This document explains how to reuse the existing Letters/Notifications system (L key) for the History menu's Archive tab.

## Executive Summary

The existing `NotificationMenuState` in `src/Quests/NotificationMenuState.cs` (1417 lines) provides a complete navigation system for letters, messages, and alerts. **70-75% can be reused directly** for the History Archive tab with an adapter pattern.

---

## Existing Architecture

### Core State Class: NotificationMenuState

**Location:** `src/Quests/NotificationMenuState.cs`

**Key Features:**
- Two-level navigation: List view → Detail view
- Typeahead search across items
- Button extraction and navigation
- Jump to location support
- Delete support (for letters)

### Public Methods Available for Reuse

```csharp
// Lifecycle
NotificationMenuState.Open()              // Collect and initialize
NotificationMenuState.Close()             // Clean up

// List Navigation
NotificationMenuState.SelectNext()        // Down arrow
NotificationMenuState.SelectPrevious()    // Up arrow
NotificationMenuState.JumpToFirst()       // Home key
NotificationMenuState.JumpToLast()        // End key

// Detail View
NotificationMenuState.EnterDetailView()   // Enter on item
NotificationMenuState.GoBackToList()      // Escape from detail
NotificationMenuState.SelectNextDetailPosition()      // Down in detail
NotificationMenuState.SelectPreviousDetailPosition()  // Up in detail

// Button Navigation
NotificationMenuState.SelectNextButton()      // Right arrow
NotificationMenuState.SelectPreviousButton()  // Left arrow
NotificationMenuState.ActivateCurrentButton() // Enter on button

// Search
NotificationMenuState.HandleTypeahead(char)   // Type to filter
NotificationMenuState.HandleBackspace()       // Delete from search
NotificationMenuState.AnnounceWithSearch()    // Announce with context

// Properties
NotificationMenuState.IsActive
NotificationMenuState.IsInDetailView
NotificationMenuState.IsInButtonsSection
NotificationMenuState.CurrentIndex
```

### NotificationItem Inner Class

Wraps different notification types with unified interface:

```csharp
private class NotificationItem
{
    public NotificationType Type { get; }          // Message/Letter/Alert
    public string Label { get; }                   // Title for list
    public string Explanation { get; }             // Full content
    public bool HasValidTarget { get; }            // Can jump
    public int Timestamp { get; }                  // Sort key
    public string[] ExplanationLines { get; }      // For scrolling

    public Letter GetSourceLetter() { }
    public Alert GetSourceAlert() { }
    public GlobalTargetInfo GetPrimaryTarget() { }
}
```

### ButtonInfo Class

```csharp
private class ButtonInfo
{
    public string Label { get; set; }
    public Action Action { get; set; }
    public bool IsDisabled { get; set; }
    public string DisabledReason { get; set; }
}
```

---

## Current Data Collection

NotificationMenuState collects from three sources:

```csharp
// 1. Live Messages (via reflection)
var liveMessages = typeof(Messages).GetField("liveMessages",
    BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as List<Message>;

// 2. Letters
var letters = Find.LetterStack.LettersListForReading;

// 3. Active Alerts
var activeAlerts = typeof(AlertsReadout).GetField("activeAlerts",
    BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(Find.Alerts) as List<Alert>;

// Sorted: Newest first by Timestamp
items.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
```

---

## Announcement Formats

### List View
```
"Letter: My Letter Title. 2 of 5"
```

### Detail View
- Position 0: "Letter: Title"
- Positions 1-N: Individual content lines
- Positions N+1+: Button announcements

### Button
```
"Button Label. Button. 1 of 3"
"Button Label (disabled). Button. 1 of 3"
```

### With Search
```
"Letter: Title. 2 of 5, match 1 of 3 for 'raid'"
```

---

## Input Routing (Priority 4.77)

| Key | Action | Condition |
|-----|--------|-----------|
| Home | JumpToFirst() | Always |
| End | JumpToLast() | Always |
| Escape | HandleEscape() | Clear search or go back |
| Backspace | HandleBackspace() | List view only |
| Up | SelectPrevious() | With/without typeahead |
| Down | SelectNext() | With/without typeahead |
| Left | SelectPreviousButton() | Button section |
| Right | SelectNextButton() | Button section |
| Enter | EnterDetailView() or ActivateCurrentButton() | Context-dependent |
| ] | DeleteSelected() | Letters only |
| A-Z, 0-9 | HandleTypeahead(c) | List view only |

---

## Reuse Strategy for History Archive

### Option A: Create Wrapper State (Recommended)

Create `HistoryArchiveState` that delegates to similar logic but uses Archive data:

```csharp
public static class HistoryArchiveState
{
    private static bool isActive;
    private static List<ArchiveItemWrapper> items;
    private static int selectedIndex;
    private static bool isInDetailView;
    private static TypeaheadSearchHelper typeahead;

    // Filters from History tab checkboxes
    private static bool showLetters = true;
    private static bool showMessages = false;

    public static void Open()
    {
        items = Find.Archive.archivables
            .Where(FilterItem)
            .OrderByDescending(a => a.CreatedTicksGame)
            .Select(a => new ArchiveItemWrapper(a))
            .ToList();

        isActive = true;
        selectedIndex = 0;
        typeahead.ClearSearch();
        Announce();
    }

    private static bool FilterItem(IArchivable item)
    {
        if (showLetters && item is Letter) return true;
        if (showMessages && item is Message) return true;
        return false;
    }

    // Navigation methods reuse same patterns as NotificationMenuState
}
```

### Option B: Extend NotificationMenuState

Add a mode flag to NotificationMenuState:

```csharp
public enum NotificationMode { Live, Archive }
private static NotificationMode mode = NotificationMode.Live;

public static void OpenArchive(bool letters, bool messages)
{
    mode = NotificationMode.Archive;
    // Collect from Find.Archive instead of live sources
}
```

**Recommendation:** Option A is cleaner - creates dedicated state without modifying working code.

---

## ArchiveItemWrapper Adapter

```csharp
public class ArchiveItemWrapper
{
    private readonly IArchivable source;

    public ArchiveItemWrapper(IArchivable archivable)
    {
        source = archivable;
    }

    // Map IArchivable to NotificationItem-like interface
    public string Label => source.ArchivedLabel;
    public string Tooltip => source.ArchivedTooltip;
    public string[] TooltipLines => SplitToLines(source.ArchivedTooltip);
    public int Timestamp => source.CreatedTicksGame;
    public bool HasValidTarget => source.LookTargets?.IsValid ?? false;
    public GlobalTargetInfo PrimaryTarget => source.LookTargets?.TryGetPrimaryTarget()
                                              ?? GlobalTargetInfo.Invalid;
    public Texture Icon => source.ArchivedIcon;
    public Color IconColor => source.ArchivedIconColor;
    public bool IsPinned => Find.Archive.IsPinned(source);

    // Actions
    public void Open() => source.OpenArchived();
    public void TogglePin() => Find.Archive.SetPinned(source, !IsPinned);
    public void JumpTo() => CameraJumper.TryJumpAndSelect(PrimaryTarget);
}
```

---

## Button Set for Archive Items

Archive items don't have ChoiceLetter buttons. Create standard set:

```csharp
private static List<ButtonInfo> GetArchiveButtons(ArchiveItemWrapper item)
{
    var buttons = new List<ButtonInfo>();

    // 1. Open
    buttons.Add(new ButtonInfo
    {
        Label = "Open",
        Action = () => {
            item.Open();
            Close();
        }
    });

    // 2. Jump to Location (if valid)
    if (item.HasValidTarget)
    {
        buttons.Add(new ButtonInfo
        {
            Label = "Jump to Location",
            Action = () => item.JumpTo()
        });
    }

    // 3. Pin/Unpin
    buttons.Add(new ButtonInfo
    {
        Label = item.IsPinned ? "Unpin" : "Pin",
        Action = () => {
            item.TogglePin();
            RefreshButtons(); // Update button label
            TolkHelper.Speak(item.IsPinned ? "Pinned" : "Unpinned");
        }
    });

    return buttons;
}
```

---

## Key Differences: Live vs Archive

| Aspect | Live (L key) | Archive (History) |
|--------|--------------|-------------------|
| Data source | LetterStack, Messages, Alerts | Find.Archive.archivables |
| Filtering | None (shows all) | Checkboxes: Letters/Messages |
| Delete | Yes (] key for letters) | No (archived permanently) |
| Pin | No | Yes |
| Sorting | Newest first | Oldest first (or pinned first) |
| Buttons | Letter choices, alert actions | Open, Jump, Pin/Unpin |
| Item limit | No limit | 200 non-pinned max |

---

## Keyboard Shortcuts for Archive

| Key | Action |
|-----|--------|
| Up/Down | Navigate items |
| Enter | Enter detail view / Activate button |
| Escape | Go back / Clear search / Close |
| Left/Right | Navigate buttons (in detail) |
| Home/End | Jump to first/last |
| A-Z, 0-9 | Typeahead search |
| Backspace | Delete search character |
| P | Toggle pin (shortcut) |
| J | Jump to location (shortcut) |

---

## Integration with History Tab

When History tab opens to Messages tab:

1. Detect tab via Harmony patch on `MainTabWindow_History`
2. Check `curTab == HistoryTab.Messages`
3. Auto-open `HistoryArchiveState` when navigating
4. Sync filter checkboxes with state variables
5. Route input through UnifiedKeyboardPatch

```csharp
// In UnifiedKeyboardPatch, at appropriate priority:
if (HistoryArchiveState.IsActive)
{
    if (HistoryArchiveState.HandleInput(key, shift, ctrl, alt))
    {
        Event.current.Use();
        return;
    }
}
```

---

## Reusable Helpers

These can be used directly without modification:

| Helper | Location | Purpose |
|--------|----------|---------|
| `MenuHelper.SelectNext()` | UI/MenuHelper.cs | Navigation with wrapping |
| `MenuHelper.SelectPrevious()` | UI/MenuHelper.cs | Navigation with wrapping |
| `MenuHelper.FormatPosition()` | UI/MenuHelper.cs | "X of Y" formatting |
| `TypeaheadSearchHelper` | UI/TypeaheadSearchHelper.cs | Search functionality |
| `TolkHelper.Speak()` | ScreenReader/TolkHelper.cs | Announcements |

---

## Testing Checklist

- [ ] L key still opens live notifications (unchanged)
- [ ] History Archive navigates correctly
- [ ] Detail view shows full tooltip text
- [ ] Buttons work (Open, Jump, Pin)
- [ ] Search filters items
- [ ] Pin/Unpin persists
- [ ] Filter checkboxes sync with state
- [ ] Escape closes properly (no double-close)
- [ ] Announcements match expected format

---

## Estimated Implementation

| Component | Lines | Reuse % |
|-----------|-------|---------|
| HistoryArchiveState | ~400 | 60% from NotificationMenuState |
| ArchiveItemWrapper | ~80 | New |
| UnifiedKeyboardPatch additions | ~50 | Pattern reuse |
| HistoryPatch.cs | ~100 | New |
| **Total new code** | ~250 | vs ~800 from scratch |
