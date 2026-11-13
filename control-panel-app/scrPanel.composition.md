Revision: 2

Revision note (r2): header re-budgeted for 44 px targets and ≥ 14 px text (gap G1); pills = rounded container + label (G2); mode selector = two buttons (G3); monospace Courier New (G4). Region IDs unchanged.

Inputs:
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-design.md (Revision 2)
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-discovery.md (Revision 1), gaps G1–G4
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-screens.md (Revision 1), S1
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-functionality.md (Revision 1)
- Spec §4.1, §6.1

# S1 — scrPanel

Shared design: create-design.md r2. Requirements: R1–R10. Actions: A1, A2, A3, A4.
Explicit constraint preserved (R1): "One screen named `scrPanel`, tablet landscape 16:9 (1366×768). The existing blank Screen1 is renamed/reused as this screen; no other screen."

## Arrangement (primary 1366×768)
A full-screen dark background with one vertical stack inset 16 px on all sides (1334×736 usable): header on top, body below.

```
┌ header (content-sized, 96 px) ───────────────────────────────────────────┐
│ S1.R1 title+subtitle (remaining width)  │ S1.R2 pills / S1.R3 controls   │
└──────────────────────────────────────────────────────────────────────────┘
┌ S1.R4 chat (48 %) ┐ ┌ S1.R5 trace  (40 % of column height) ─────────────┐
│                   │ ├ S1.R6 gateway (remaining height) ─────────────────┤
└───────────────────┘ └───────────────────────────────────────────────────┘
```
- Header: fixed to content height (96 px = 44 px pill row + 8 px gap + 44 px controls row), never shrinks, always in view. Width budget: right block ≈ 694 px (controls row is the widest), 16 px gap, title block takes the remaining ≈ 624 px.
- Body: takes all remaining height (≈ 624 px after 16 px gap). Columns separated by 16 px: chat ≈ 633 px (48 %), right column ≈ 685 px (52 %). Right column: trace ≈ 40 % (≈ 250 px), 16 px gap, gateway takes the remainder (≈ 358 px).
- Scroll owner at primary size: none at screen level (fits 768 px). Each panel will own its internal scroll in later phases; this is a deliberate decision so the header stays fixed and the three panels stay visible together. Currently the placeholders have no content and no scroll.
- Main content: the status header is the only populated region now and sits at the top, immediately reachable; the chat is the largest future surface and takes the left, as in the spec.

## Regions

### S1.R1 — Title block (R5)
- Purpose: identify the app and its stack. Static text.
- Position: left of the header, takes the remaining width after S1.R2/S1.R3 (≈ 640–700 px at 1366).
- Content priority: title first (19 px cyan, one line at ≈ 624 px), subtitle second (14 px muted) under it; at 1366 the subtitle wraps to two lines (≈ 70 px block total, fits the 96 px header) rather than truncating, so full text is always readable. Vertically centred in the header.
- States: static; same in every state, including first load (the skeleton shows this first).

### S1.R2 — Status pills (R8; A3, A4 outcomes)
- Purpose: show at a glance agent connection, guardrail and egress control.
- Position: top-right row of the header, content-sized, right-aligned. Order left to right: guardrail pill, egress pill, agent pill (circle + label). 12 px gaps. Row height 44 px. Each pill is a rounded container wrapping one 14 px label (content-sized: ≈ 170, ≈ 240, ≈ 160 px; total ≈ 600 px, within the 694 px block).
- Grouping: the three pills form one row; they are not interactive (except the egress tooltip).
- Content: "🛡 Guardrail activo" / "⚠ Sin guardrail"; "Egress · solo el AI Gateway" / "Egress · en auditoría" (tooltip: allowed hosts, comma-joined); "Agente activo" / "Sin conexión". Colours per create-design roles (verde/rojo/ámbar).
- States: first load: guardrail and egress hidden, agent pill red "Sin conexión" until GetState completes. Guardrail unreadable: guardrail and egress hidden. Hidden pills collapse and the remaining pills stay right-aligned, so the agent pill never moves from the right edge; the row keeps its 44 px height so the header does not shift. Refresh updates in place with no indicator.
- Hierarchy: the agent pill is the right-most anchor (the operator looks at it first); guardrail and egress read as supporting state.

### S1.R3 — Session and mode controls (R8; A1, A2)
- Purpose: choose call mode, see the session, restart conversation.
- Position: second row of the right header block, under the pills, right-aligned. Left to right: mode selector (two adjacent equal buttons, 180 px × 44 px each = 360 px, no gap between them), 12 px, session label (≈ 120 px, Courier New 14 px, 44 px row height, vertically centred), 12 px, "Nueva conversación" button (≈ 190 px × 44 px, secondary style: panel2 fill, line border, body text). Row total ≈ 694 px.
- Content: buttons "Vía AI Gateway" / "Directo al backend", 14 px text. Selected button: cyan fill with dark text; unselected: panel2 fill with body text. Default on load: "Vía AI Gateway". Session label "Sesión nueva" or "Sesión " + last 6 characters.
- Actions: A1 changes the selection immediately, no feedback beyond the highlight. A2 resets the session label to "Sesión nueva"; there is no confirmation (per functionality), and the action sits in the header where it is pressed, so the result is visible in place.
- States: pressing "Nueva conversación" when already empty shows no change; no error state exists for either control. Selection and session persist across refreshes (screens doc round trips).
- Hierarchy: mode selector is the primary control (cyan on selection); the button is secondary.

### S1.R4 — Chat placeholder (R2, R4)
- Purpose: reserved panel for the later chat phase. Empty (AS3).
- Position: left column of the body, 48 % width, full body height.
- Styling: panel fill, 1 px line border, radius 14, 16/18 padding. No title, no content, no empty-state text (nothing invented).
- Later phase: will own its own scroll for messages with a fixed input row at the bottom; not designed now.

### S1.R5 — Trace placeholder (R2, R4)
- Purpose: reserved panel for the trace. Empty, panel styled. Top of the right column, ≈ 40 % of its height.

### S1.R6 — Gateway placeholder (R2, R4)
- Purpose: reserved panel for gateway controls and the requests/audit tabs. Empty, panel styled. Bottom of the right column, takes the remaining height (≈ 60 %).

### Non-visual (R6, R7, R9)
Startup load, variable declarations and the two hidden timers have no layout presence; timers are not visible and take no space.

## Reading and focus order
1. S1.R1 title, subtitle (read-only).
2. S1.R2 guardrail pill, egress pill (tooltip available on focus/hover), agent pill.
3. S1.R3 "Vía AI Gateway", "Directo al backend", session label, "Nueva conversación".
4. S1.R4, S1.R5, S1.R6 (placeholders have no focusable content now).
Persistent labels: every control shows its text (no icon-only controls); the session label is always visible, including when empty. Pill state is conveyed by text, not colour alone. Selected segment is exposed as selected.

## Feedback
- Error: connection or state failure appears only as the red "Sin conexión" pill (the error text stays in `varGwError` and is not shown, per functionality). Guardrail failure hides the pills rather than showing an error.
- Empty: placeholders intentionally blank; no empty-state copy.
- Loading: no spinner; defaults described in S1.R2/S1.R3.

## Viewport adaptation
| Viewport | Change |
|---|---|
| 1366×768 | As above. |
| 1024×768 | Same arrangement. Title block narrows to ≈ 282 px; title may wrap to two lines and subtitle to about four, so the header grows to fit (≈ 136 px max) and the body takes the rest (≈ 580 px). Right-block controls keep their 44 px sizes and widths. |
| 375×667 (floor 320×568) | The screen becomes the vertical scroll owner. Header stacks: S1.R1 on top (full width, subtitle wraps), then S1.R2 pills wrapping onto as many 44 px lines as needed, then S1.R3 as: the two mode buttons each half width (button text may wrap to two lines, growing the button above 44 px, never below), then session label (≈ 120 px) and "Nueva conversación" (remaining width, ≥ 150 px) on one row, 44 px tall; at 320 the label may wrap onto a second line. All text stays ≥ 14 px. Body stacks: chat (min height ≈ 320 px), trace (≈ 200 px), gateway (≈ 280 px). All header content stays reachable at the top before any scrolling into the panels. |
Header remains the first item at every size; panels never overlap it.
