# Trade Module

## Purpose
Trading interface navigation. Keeps Dialog_Trade open and works with game's APIs.

## Files
**Patches:** TradeNavigationPatch.cs, SellableItemsNavigationPatch.cs
**States:** TradeNavigationState.cs, SellableItemsState.cs

## Tab Structure
Three tabs (Left/Right to switch):
- **[Trader]'s Items** - Items they have (uses trader name, e.g., "Muffalo's Items")
- **Trade Summary** - Items with pending trades (only visible when items are queued)
- **Your Items** - Items you have

Shared items appear in BOTH inventory tabs with full buy/sell information.

### Trade Summary Tab
The Trade Summary tab provides a quick view of all pending trades:
- **Conditional visibility**: Only appears when items have non-zero quantities
- **Sorting**: Buying items first (positive quantities), then selling items (negative), alphabetical within each
- **Balance entry**: Navigable entry at the bottom showing net spending/receiving
- **Tab position memory**: Each tab remembers cursor position when switching away
- **Auto-switch**: When Trade Summary becomes empty (last item removed), auto-returns to previous tab with restored position

**Balance entry formats:**
- Normal trade: "Net balance: Spending 50 silver" or "Net balance: Receiving 100 silver" or "Net balance: Balanced trade"
- Gift mode: "Goodwill +15"
- Favor trades: Shows favor amount instead of silver

## Key Shortcuts
- **Left/Right** - Switch tabs
- **Up/Down** - Navigate items (in list) / Adjust quantity (in quantity mode)
- **Shift+Up/Down** - Adjust quantity by ±10
- **Ctrl+Up/Down** - Adjust quantity by ±100
- **Enter** - Enter/exit quantity adjustment mode
- **Home / Shift+Home** - Max action (context-aware: max sell if selling, max buy if buying/shared, max gift if gifting)
- **End / Shift+End** - Opposite or reset (max sell for shared items, reset to 0 otherwise)
- **Delete** - Reset current item to zero
- **Alt+B** - Announce trade balance (your silver and current balance)
- **Space** - Accept trade
- **Tab** - Price breakdown (opens StatBreakdownState)
- **Alt+I** - Inspect item (opens WindowlessInspectionState)
- **Alt+R** - Reset current item
- **Shift+Alt+R** - Reset all trades
- **Alt+G** - Toggle gift mode
- **Escape** - Exit quantity mode / clear search / close trade

## Announcement Format

**Non-shared items (silver trades):**
- "Steel. 20 available. $2.25."
- "Jade knife. 1 available. $250, very expensive."

**Shared items (in either tab):**
- Their Items tab: "Steel. Theirs: 40 at $4.33. Yours: 20 at $2.25."
- Your Items tab: "Steel. Yours: 20 at $2.25. Theirs: 40 at $4.33."

**Favor trades (royal tribute collector):**
- "Jade. 5 available. 2 favor."

**Price quality indicators** (matches game's red/green colors):
- Buying: "great deal", "good deal", (normal), "pricey", "very expensive"
- Selling: "great offer", "good offer", (normal), "low offer", "poor offer"

**When trade pending:**
- "Steel. Buying 10. Total: $43."

**Trade Summary tab announcements:**
- Items include position: "Steel. 1 of 5. Buying 10. Total: $43."
- Balance entry: "Net balance: Spending 50 silver. 5 of 5."

## Trade Quantity System

**Normal Trade Mode:**
- **Negative numbers** = selling (giving to trader)
- **Positive numbers** = buying (taking from trader)

**Gift Mode (IMPORTANT - inverted sign convention!):**
- **Positive numbers** = gifting (giving to trader)
- The game's `PositiveCountDirection` flips to `Destination` in gift mode
- `GetMaximumToTransfer()` returns YOUR item count (positive, max gift)
- `GetMinimumToTransfer()` returns 0 (can't take from trader)

**Direction Logic via `IsSellingPrimaryContext()`:**
- Selling-primary (you have item, trader doesn't, NOT gift mode): Up = more negative (sell more)
- Buying/shared/gift mode: Up = more positive (buy/gift more)

- Home/Shift+Home goes to "top of list" (max action for context)
- End/Shift+End goes to "bottom" (opposite for shared, reset for non-shared)

## Architecture
Dialog_Trade stays open while we provide keyboard navigation. All quantity changes use game's `Tradeable.AdjustTo()` method. Visual UI updates in sync via `CountToTransferChanged()` reflection call.

Harmony patches on `Window.OnCancelKeyPressed` and `Window.OnAcceptKeyPressed` prevent RimWorld from closing the dialog when our overlay menus are active or when Enter is pressed.

### Trade Summary Implementation
- `tradeSummaryList` - Populated during `RefreshTradeables()` with items where `CountToTransfer != 0`
- `tabPositions` - Dictionary storing cursor position for each tab
- `previousCategory` - Tracks last tab for auto-switch return
- Balance entry is a virtual entry at index `list.Count` (not a real Tradeable)
- `IsOnBalanceEntry()` helper checks if cursor is on the balance entry

## Dependencies
**Requires:** ScreenReader/, Input/, UI/StatBreakdownState, Inspection/WindowlessInspectionState

## Testing
- [ ] Trade window navigable with Dialog_Trade visible
- [ ] Three tabs when trades pending: Trader's Items, Trade Summary, Your Items
- [ ] Two tabs when no pending trades: Trader's Items, Your Items
- [ ] Trader's Items tab uses trader name (e.g., "Muffalo's Items")
- [ ] Shared items appear in BOTH inventory tabs with full buy/sell info
- [ ] Trade Summary shows only items with pending quantities
- [ ] Trade Summary sorted: buying first, then selling, alphabetical within
- [ ] Trade Summary balance entry navigable at end of list
- [ ] Balance entry shows correct spending/receiving amount
- [ ] Balance entry respects "announce position" setting
- [ ] Gift mode balance shows "Goodwill +X"
- [ ] Tab position memory works across switches
- [ ] Auto-switch to previous tab when Trade Summary empties
- [ ] End key jumps to balance entry in Trade Summary
- [ ] Price quality indicators show for non-normal prices
- [ ] Dollar sign shows in silver trade prices
- [ ] "favor" shows instead of "$" for royal tribute trades
- [ ] Up/Down arrows adjust quantity based on context (selling-primary vs buying/shared)
- [ ] Home/Shift+Home sets to max action (context-aware)
- [ ] End/Shift+End sets to opposite (shared) or reset (non-shared/gift)
- [ ] Delete resets current item to zero
- [ ] Quantity adjustments update visual UI
- [ ] Alt+B announces player silver and spending/receiving balance
- [ ] Tab shows price breakdown
- [ ] Alt+I shows item inspection
- [ ] Accept trade (Space) works
- [ ] Enter key doesn't close dialog
- [ ] Escape announces "Trade cancelled" when closing
- [ ] Escape properly closes overlays before trade

---

# Sellable Items Dialog (Dialog_SellableItems)

## Purpose
Read-only informational dialog showing what items a settlement trader will buy. Accessed via the "Show sellable items" gizmo on settlements in the world map.

## Key Shortcuts
- **Left/Right** - Switch category tabs
- **Up/Down** - Navigate items within current tab
- **Home** - Jump to first item
- **End** - Jump to last item
- **Typing** - Typeahead search (jump to items by name)
- **Backspace** - Delete last search character
- **Escape** - Clear search first, then close dialog

## Announcement Format

**On open:**
- "[Trader name] will buy. [Restock info]. [Tab count] categories. Switch tabs: Left, Right. Navigate: Up, Down."

**Restock info variants:**
- "Next restock: 2.5 days."
- "Not visited yet."
- "Restocked since last visit."

**Tab switch:**
- "[Category name]. [Count] items."

**Item announcement:**
- "[Item name]. [Description]. [X of Y]." (position only if setting enabled)
- Example: "Steel. A strong metal used for construction and crafting. 3 of 15."

**With typeahead search:**
- "Search 'ste': Steel. A strong metal... 3 of 15."
- "No matches for 'xyz'. Steel. A strong metal... 3 of 15."

## Architecture
Dialog_SellableItems stays open while we provide keyboard navigation. Uses reflection to access:
- `tabs` (List<TabRecord>) - The category tabs
- `currentCategory` (ThingCategoryDef) - Currently selected category
- `pawnsTabOpen` (bool) - Whether Pawns tab is active
- `GetSellableItemsInCategory()` - Method to get items for current tab
- `trader` (ITrader) - For restock information

Tab switching calls `TabRecord.clickedAction` to activate tabs and keep dialog state in sync.

## Testing
- [ ] Dialog opens with trader name and restock info announced
- [ ] Left/Right arrows switch between category tabs
- [ ] Tab position memory works (switch away and back)
- [ ] Up/Down navigate items with wrap-around (if setting enabled)
- [ ] Home/End jump to first/last item
- [ ] Typeahead search filters items correctly
- [ ] Backspace modifies search
- [ ] Escape clears search first, then closes dialog
- [ ] Item announcements include name + description
- [ ] Position (X of Y) only shown if setting enabled
- [ ] Empty categories announce "No items"
- [ ] No double-close bug on Escape
