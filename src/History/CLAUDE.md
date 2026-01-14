# History Module

This module provides accessibility for RimWorld's History tab (one of the bottom screen tabs, right of World map).

## Overview

The History tab displays colony statistics, archived messages, and graphs over time. It's implemented in `MainTabWindow_History` (1010x640 pixels).

## Tab Structure

The History window contains **3 tabs** (defined in `HistoryTab` enum):

| Tab | Purpose | Key Content |
|-----|---------|-------------|
| **Graph** | Historical trend visualization | Time-series curves, legends, period selection |
| **Messages** | Archive of letters/messages | Searchable, filterable, pinnable list |
| **Statistics** | Colony stats summary | Plain text metrics display |

---

## Tab 1: Graph

### UI Layout

```
+--------------------------------------------------+
| [Tab: Graph] [Tab: Messages] [Tab: Statistics]   |
+--------------------------------------------------+
| [Graph Selector Dropdown ▼]                      |
+--------------------------------------------------+
|                                                  |
|   GRAPH AREA (450px tall)                        |
|   - Y-axis labels on left                        |
|   - X-axis shows days                            |
|   - Colored curves for each metric               |
|   - Vertical marks for Tales (events)            |
|   - Hover shows value at cursor                  |
|                                                  |
+--------------------------------------------------+
| LEGEND: [color] Label1  [color] Label2  ...      |
+--------------------------------------------------+
| [Last 30 Days] [Last 100 Days] [Last 300 Days]   |
| [All Days]                                       |
+--------------------------------------------------+
```

### Graph Types (HistoryAutoRecorderGroups)

Each group contains one or more curves:

| Graph Group | Metrics Tracked |
|-------------|-----------------|
| **Wealth** | Total, Buildings, Items, Pawns |
| **Colonists** | Free colonist count |
| **Prisoners** | Prisoner count |
| **Mood** | Average colonist mood (0-100%) |
| **Threat** | Current threat points |
| **Adaptation** | Story adaptation levels |
| **Population** | Pop intent, pop adaptation |

### Accessibility Needs

1. **Graph selector** - Navigate between graph types
2. **Period buttons** - Select time range (30/100/300/All days)
3. **Legend** - Read what each curve represents
4. **Data points** - Access actual values at time points
5. **Tales/marks** - Announce significant events on graph

### Data Flow

```
HistoryAutoRecorderGroup
  └── List<HistoryAutoRecorder>
        └── List<float> records (one per day)
              └── Rendered as SimpleCurve
                    └── CurvePoint (x=day, y=value)
```

---

## Tab 2: Messages

### UI Layout

```
+--------------------------------------------------+
| [Tab: Graph] [Tab: Messages] [Tab: Statistics]   |
+--------------------------------------------------+
| [✓] Show Letters  [ ] Show Messages   [Search🔍] |
+--------------------------------------------------+
| LEFT HALF (scrollable list)  | RIGHT HALF       |
| +-----+----+------+--------+ | (Tooltip/Preview)|
| | Pin |Icon| Date | Label  | | Full text of     |
| | 30px|30px| 90px | rest   | | selected item    |
| +-----+----+------+--------+ |                  |
| | 📌 | ✉️ | 5 Jug | Raid   | |                  |
| |    | 📜 | 3 Apr | Quest  | |                  |
| | ...                      | |                  |
+--------------------------------------------------+
```

### Row Structure (30px tall each)

| Column | Width | Content |
|--------|-------|---------|
| Pin | 30px | Pin icon (toggleable) |
| Icon | 30px | Letter/message type icon |
| Date | 90px | Short date format |
| Label | Remaining | Truncated message text |

### Filters

- **Show Letters** checkbox (default: ON)
- **Show Messages** checkbox (default: OFF)
- **Search bar** - Filters by `ArchivedLabel`

### Archive Limits

- Maximum 200 non-pinned items
- Pinned items are never culled
- Sorted by creation tick (oldest first)

### Archivable Types

1. **Letters** (`Verse.Letter`) - Major notifications with icons
2. **Messages** (`Verse.Message`) - Temporary notifications (no icon)
3. **ArchivedDialogs** - Stored dialogue conversations

### Interactions

- **Left-click row**: Opens archived item
- **Right-click row**: Jump camera to location
- **Click pin icon**: Toggle pinned state

### IArchivable Interface

```csharp
interface IArchivable {
    Texture ArchivedIcon;      // Icon to display
    Color ArchivedIconColor;   // Icon tint
    string ArchivedLabel;      // Row text
    string ArchivedTooltip;    // Full preview text
    int CreatedTicksGame;      // Sort timestamp
    bool CanCullArchivedNow;   // Can be auto-removed
    LookTargets LookTargets;   // Camera jump target
    void OpenArchived();       // Open full content
}
```

### Accessibility Needs

1. **Filter toggles** - Toggle Letters/Messages visibility
2. **Search** - Filter archive items
3. **List navigation** - Navigate rows with Up/Down
4. **Pin toggle** - Pin/unpin items
5. **Open item** - View full content
6. **Jump to location** - Camera jump
7. **Row announcement** - "Pinned, Letter icon, 5 Jugust, Raid from Pirates"

---

## Tab 3: Statistics

### UI Layout

```
+--------------------------------------------------+
| [Tab: Graph] [Tab: Messages] [Tab: Statistics]   |
+--------------------------------------------------+
| STATISTICS (400px width, left-aligned)           |
|                                                  |
| Play time: 2 days, 5 hours, 32 minutes           |
|                                                  |
| Storyteller: Cassandra Classic                   |
| Difficulty: Rough                                |
|                                                  |
| Colony Wealth:                                   |
|   Total: 45,230                                  |
|   Items: 23,100                                  |
|   Buildings: 18,430                              |
|   Colonists + Animals: 3,700                     |
|                                                  |
| Threats: 12 events                               |
| Enemy raids: 8                                   |
| Damage taken: 2,340                              |
|                                                  |
| Colonists killed: 1                              |
| Colonists launched: 0                            |
+--------------------------------------------------+
```

### Data Sources

| Stat | Source |
|------|--------|
| Play time | `GameInfo.RealPlayTimeInteracting` |
| Storyteller | `Find.Storyteller.def.LabelCap` |
| Difficulty | `Find.Storyteller.difficultyDef.LabelCap` |
| Wealth | `Find.CurrentMap.wealthWatcher.*` |
| Events | `Find.StoryWatcher.statsRecord.*` |
| Damage | `Find.CurrentMap.damageWatcher.DamageTakenEver` |

### Accessibility Implementation

**Navigate with Up/Down arrows.** Each statistic is a separate menu item.

**Announcement Format:**
```
Statistic Name: Value. (Tooltip, if present.)
```

**Example Announcements:**
- "Play time: 2 days, 5 hours, 32 minutes. 1 of 12"
- "Storyteller: Cassandra Classic. 2 of 12"
- "Total Wealth: 45,230. (Combined value of all colony assets.) 5 of 12"
- "Colonists killed: 1. 11 of 12"

### Statistics List (Proposed Order)

| # | Statistic | Has Tooltip? |
|---|-----------|--------------|
| 1 | Play time | No |
| 2 | Storyteller | No |
| 3 | Difficulty | No |
| 4 | Total Wealth | Maybe |
| 5 | Item Wealth | Maybe |
| 6 | Building Wealth | Maybe |
| 7 | Pawn Wealth | Maybe |
| 8 | Threat events | No |
| 9 | Enemy raids | No |
| 10 | Damage taken | No |
| 11 | Colonists killed | No |
| 12 | Colonists launched | No |

### Keyboard Mappings

| Key | Action |
|-----|--------|
| Up | Previous statistic |
| Down | Next statistic |
| Home | First statistic |
| End | Last statistic |
| Escape | Close / Go back to tab selection |

---

## Key Decompiled Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `MainTabWindow_History` | RimWorld/ | Main UI window |
| `History` | RimWorld/ | Data container |
| `Archive` | RimWorld/ | Message storage |
| `HistoryAutoRecorder` | RimWorld/ | Individual metric tracker |
| `HistoryAutoRecorderGroup` | RimWorld/ | Groups of metrics |
| `HistoryAutoRecorderWorker_*` | RimWorld/ | Data pull implementations |
| `SimpleCurveDrawer` | Verse/ | Graph rendering |
| `HistoryEventsManager` | RimWorld/ | Event tracking |

---

## Implementation Strategy

### Priority Order

1. **Tab navigation** - Switch between Graph/Messages/Statistics
2. **Statistics tab** - Simplest, just read text
3. **Messages tab** - List navigation with filtering
4. **Graph tab** - Most complex, requires creative approach

### Files to Create

- `HistoryState.cs` - Main state management
- `HistoryPatch.cs` - Harmony patches for MainTabWindow_History
- `HistoryHelper.cs` - Data extraction utilities

### Keyboard Mappings (Proposed)

| Key | Action |
|-----|--------|
| Tab / Shift+Tab | Switch between main tabs |
| Up/Down | Navigate items (Messages) or graph types (Graph) |
| Enter | Open/select item |
| P | Toggle pin (Messages tab) |
| L | Toggle Letters filter (Messages tab) |
| M | Toggle Messages filter (Messages tab) |
| / | Focus search (Messages tab) |
| 1-4 | Quick period select (Graph: 30/100/300/All days) |
| Left/Right | Navigate time on graph |
| Home/End | Jump to start/end of graph data |

### Graph Accessibility Approach

Since graphs are visual, provide:
1. **Summary announcement**: "Wealth graph showing 4 curves over 45 days"
2. **Legend navigation**: Announce each curve name/color
3. **Data navigation**: Move through time points, announce values
4. **Event markers**: Announce Tales at their positions
5. **Trend summary**: "Wealth increasing from 10,000 to 45,000"

---

## RimWorld Integration Points

### Window Opening

`MainTabWindow_History` extends `MainTabWindow`:
- Opens via bottom tab bar
- `DoWindowContents(Rect inRect)` renders content
- `curTab` static field persists selected tab

### Graph Rendering

```csharp
// In DoGraphPage
SimpleCurveDrawer.DrawCurves(
    graphRect,
    curves,
    curveDrawerStyle,
    marks,      // Tales as vertical lines
    legendRect
);
```

### Archive Access

```csharp
// Get filtered archive items
foreach (IArchivable item in Find.Archive.archivables)
{
    if (showLetters && item is Letter) // include
    if (showMessages && item is Message) // include
}
```

---

## State Lifecycle

1. **Tab opens**: Detect via Harmony patch on `MainTabWindow_History`
2. **Initialize**: Set `IsActive = true`, announce current tab
3. **Handle input**: Route through UnifiedKeyboardPatch
4. **Tab closes**: Set `IsActive = false`, clean up

## Testing Notes

- Test with long game saves (more history data)
- Test all graph types
- Test with pinned and unpinned messages
- Test filter combinations
- Test search functionality
- Verify camera jump works from Messages tab
