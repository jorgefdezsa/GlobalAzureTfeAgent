Revision: 2

Revision note (r2): reconciles feasibility with design r2 / composition r2 (44 px targets, ≥ 14 px text, 96 px header, width budget, rounded-container pills, two Classic Buttons, Courier New). G1, G2 and G4 are closed. G3 is closed with one disclosed residual. The data/API/dataset sections are unchanged from r1 (no new tool calls; the document-dependent choices were not re-probed, so recheck them after any Screen1 rename/replacement, theme change or connector update). Approval is reset to Pending.

Inputs:
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-functionality.md (Revision 1)
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-screens.md (Revision 1)
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-design.md (Revision 2)
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/scrPanel.composition.md (Revision 2)
- Spec /Users/jorgefdezsa/Sources/NetCoreConf/docs/especificacion-canvas-app.md §3.3, §4, §5, §6.1, §7.1, §7.2
- Tool evidence this session: `list_controls`, `describe_control` (GroupContainer, Timer, Label, Classic/Button, ModernButton, ModernBadge, ModernTabList), `list_apis`, `describe_api GlobalDemoControl`, `list_data_sources`
- Local doc state: `App.pa.yaml` holds only `Theme: =PowerAppsTheme`. `Screen1.pa.yaml` holds only `LoadingSpinnerColor`. Both are blank.
- No `sync_canvas` tool is exposed to this agent, so I did not pull the session state. The coordinator should sync before building.

## Verdict

Status: Ready. Every requested region and action has a feasible candidate under design r2. Residual compromises (G3 selected-state semantics, G5 letter spacing, runtime-unverified items G6) are listed under "Gaps and decisions". No mock data is needed.

## Sizing and width-budget feasibility (design r2 / composition r2)
Arithmetic from the composition, at 1366×768 (usable 1334×736 after the 16 px margin):
- Header 96 px = 44 pill row + 8 gap + 44 controls row. Body = 736 − 96 − 16 = 624 px. The right column splits as (624 − 16) × 40 % ≈ 243 px trace and ≈ 365 px gateway. The composition states ≈ 250 / ≈ 358. Use `FillPortions` 40:60 and not the stated pixel figures (difference is only rounding).
- Width: controls row = 180 + 180 + 12 + 120 + 12 + 190 = 694 px. Title block = 1334 − 694 − 16 = 624 px. The pill row (≈ 170 + 240 + 160 + 2 × 12 = ≈ 594 px) fits inside 694 px.
- Title: ≈ 36 characters at 19 px Semibold ≈ 360 px, so it fits on one line. Subtitle: ≈ 104 characters at 14 px ≈ 750 px, so it wraps to two lines at 624 px. Title and two subtitle lines ≈ 70 px, which fits the 96 px header. These are estimates and not measured; text widths are runtime-unverified.
- Tool-backed minimums: `Classic/Button` defaults to a 160×40 minimum and `Label` to 150×40. 180×44 and 190×44 buttons are above these. The 120 px session label is below the Label's 150 px default minimum, so `LayoutMinWidth` must be set explicitly (to 0 or ≤ 120). The pills' inner Labels need the same override. Without overrides the 694 px row grows to ≈ 724 px and the title block shrinks.
- Session label fit: "Sesión nueva" (12 characters) and "Sesión " + 6 characters (13 characters) in Courier New 14 px ≈ 8.4 px per character ≈ 101–110 px. The 120 px budget leaves ≈ 10 px, so padding must be 0 and wrap off. **Recommendation:** budget 130 px for the label and recompute the block as ≈ 704 px (title block 614 px; subtitle still wraps to 2 lines). This is a tightness risk, not a gap.
- Button text fit: "Directo al backend" (18 characters) at 14 px Semibold ≈ 135–145 px, within 180 px. "Nueva conversación" (18 characters) ≈ 140–150 px, within 190 px with about 20 px of padding per side. Both fit one line.
- Pills: the Label cannot size to its text, so each pill has a budgeted fixed width. Longest text "Egress · solo el AI Gateway" (27 characters) ≈ 205–215 px plus 2 × 12 px padding ≈ 240 px. OK. "🛡 Guardrail activo" (≈ 140 px + padding) fits ≈ 170 px. Agent pill: circle (≈ 12 px) + 8 px gap + "Agente activo" (≈ 100 px) + padding ≈ 160 px. Emoji glyph width (🛡, ⚠) depends on the runtime font and is unverified.
- Hit targets: all header interactive items are ≥ 44 px tall and wider than 44 px. The pills are 44 px tall, so the egress tooltip target is valid.
- 1024×768: the composition's right block stays at 694 px, so the title block = 992 − 694 − 16 = 282 px. Title (≈ 360 px) wraps to two lines, subtitle to about four, so ≈ 52 + 80 ≈ 132 px, which matches the "≈ 136 px" max. The header must therefore be **content-sized** (not fixed 96) below ≈ 1366. Feasible with an AutoLayout header with no fixed height and `AutoHeight` labels; the reflow result is runtime-unverified. The body then has ≈ 580 px (736 − 136 − 16 ≈ 584).
- < 1024: header stacks and the root becomes the vertical scroll owner (see Composition feasibility). The mode buttons at half width (375: ≈ 171 px, 320: ≈ 144 px) are above or near the 160 px `Classic/Button` default minimum, so their `LayoutMinWidth` must be 0. Text wraps to two lines and the button grows above 44 px, never below. This is feasible if `AutoHeight`/minimum height are set; the wrap behaviour of a `Classic/Button` label is runtime-unverified. If a Classic Button does not grow with its text, set an explicit height of ≥ 64 px for the narrow branch.

## Data and API contracts

### Data sources
`list_data_sources` succeeded and returned 0 sources. That is a successful empty result, not a tool failure. The only data path is the connector `GlobalDemoControl`, which is real. **No mock data is proposed or needed.** Every region in scope reads the connector, and the functionality forbids invented data.

### Connector `GlobalDemoControl` (`describe_api` succeeded)
Call form is `GlobalDemoControl.<OperationId>()`. Operations used in scope are below. The shapes were compared with spec §3.3.

| Operation | Kind | Returned contract (tool-backed) | vs spec §3.3 |
|---|---|---|---|
| `GetState` | Record, not behavior-only | `agentConfigured:b, apim:s, blockedAll:b, blockedTools:*[Value:s], priceCached:n, priceInput:n, priceOutput:n, rateLimit:n` | Matches all 8 fields. `blockedTools` is a table with field `Value`, which settles AS1. |
| `GetGuardrail` | Record, not behavior-only | `allowedHosts:*[Value:s], egressMode:s, found:b, jailbreakBlocking:b` | Matches. `allowedHosts` is a table with field `Value`, so the tooltip join needs `Concat(varGuardrail.allowedHosts, Value, ", ")`. |
| `GetTools` | Table | `*[description:s, name:s, sampleArgs:O]` | Matches. `sampleArgs` is typed `O` (untyped object), which matters for `ClearCollect` into `colTools`. |
| `GetRequests` | Table | `*[at:d, detail:s, kind:s, ms:n, outcome:s, status:n]` | Matches the fields. `at` is already typed `d` (date-time), not text. The spec note to apply `DateTimeValue` does not apply to the connector type, and later phases should not wrap it. |

Out of scope and not called: `SetBlockedAll`, `SetBlockedTools` (parameter `tools: Table`, spec says `string[]`), `SetRateLimit`, `GetAudit`, `TestTool` (`arguments: UntypedObject`), `ChatSync`. Contracts for these were observed and match spec §3.3/§3.2. Later phases must re-describe them.

Findings that affect implementation:
- The `Get*` operations are not "behavior only", so they are callable from `App.OnStart`, timer `OnTimerEnd` and (if needed) named formulas. The `Set*`, `TestTool` and `ChatSync` operations are behavior-only, so they are valid only in event handlers.
- Permissions, connection existence and DLP grouping (spec §3.1, §9) are not observable from the authoring tools. Whether the connection is signed in and the backend reachable is **unverified until runtime**. The failure paths in R6/A3 cover this.
- Responses have not been executed. Error text for `varGwError` comes from `FirstError.Message` at runtime and is unobserved.
- `ClearCollect(colTools, GlobalDemoControl.GetTools())` stores an untyped-object column. Whether a collection accepts it is **runtime-unverified**. If it is rejected, `colTools` can drop `sampleArgs`, or only that column can be kept as untyped in a variable. This matters only from phase 6, since `colTools` is not displayed now.
- Each response schema is a tool-reported contract. No call has run, so no actual payload or empty-list behavior is evidenced.

### Dataset bounds (CreateGalleryLayout)
No list is rendered in this scope: no Gallery, Data table or repeated surface. The lists are loaded only for later phases.

| Source | Classification | Maximum | Invariants / invalidating changes |
|---|---|---|---|
| `colTools` (`GetTools`) | live / unbounded from the app's view | **unknown** | The backend returns `Catalog.Tools`. Its size is not evidenced from the connector. Any later phase that renders it needs a scroll/paging decision then. |
| `colRequests` (`GetRequests`) | live / unbounded | **unknown** (backend `RequestLog.Latest`, cap not evidenced) | Refreshed every 5 s and replaced by `ClearCollect`. Later phase 6.4 must resolve the list's scroll owner. |

No scroll or paging decision is needed for this scope. Nothing renders these lists, so there is no truncation or display cap.

### Images
None required. No `search_stock_images` call was made.

## Composition feasibility

Shell mode is **fixed-chrome**, one full-viewport vertical AutoLayout root (`conRaiz`) with `conCabecera` and `conCuerpo` beneath it.
- **Containers.** `GroupContainer` is feasible with `Variant: AutoLayout`. Declared nesting is supported, and YAML children are supported. Direction, gap, padding, alignment, wrap and overflow are input properties. Children have `FillPortions`, `AlignInContainer`, `LayoutMinWidth/Height` and max sizes.
  - Defaults to override: child container minimums default to 250×100 and leaf minimums are larger (Label 150×40, Classic/Button 160×40, ModernButton 96×32). All must be set to 0 or deliberate values, or the 1024-wide and narrow layouts break.
  - The 48/52 split is expressible as `FillPortions` 48:52. Use portions, not "%" widths.
  - `LayoutDirection`, `LayoutWrap` and `LayoutOverflowY` are inputs, so the <1024 stacking can be driven by a formula on local width. The runtime result of this reflow is unobserved.
- **Panels.** `conChat`, `conTraza` and `conGateway` as empty AutoLayout GroupContainers can carry Fill, border (thickness/color/style), the four radii and padding.
- **Scroll owner.** No scroll owner at ≥1024 (the root is stationary). At <1024 the design makes the root a vertical scroll owner (`LayoutOverflowY = Scroll`). That conflicts with `QACHK-ROOT-NOT-SCROLLABLE` only in the fixed-chrome sense. Treat it as the page-flow mode for the narrow branch, with direct children of the root given `FillPortions: 0` and explicit heights. The composition's per-region heights (chat ≈320, trace ≈200, gateway ≈280) already do this.
- **Confirmation.** None is required (functionality and screens say "Nueva conversación" is immediate). `Confirm` was not needed.
- **Hidden timers.** Timer's `Visible` is an input, so `Visible = false` is expressible. A hidden Timer should keep running with `AutoStart`, but that is **runtime-unverified**. Timer cannot contain children. To satisfy `QACHK-ROOT-CONTAINMENT`, place both timers as children of `conRaiz` (or of the header), not as screen-level siblings. They need `AlignInContainer`/`FillPortions`/min sizes set to zero. Whether a `Visible = false` child leaves the flow with zero footprint is unverified.
- **Pill collapse.** The composition expects hidden pills to leave the row so the agent pill stays right-aligned. That needs `Visible = false` on a child of a horizontal AutoLayout to drop out of the flow. This is expected platform behavior but unobserved here.

### Region/action → candidate table

| Region / action | Candidate (exact query identity) | Fit and limitations (tool-backed) |
|---|---|---|
| S1 `conRaiz`, `conCabecera`, `conCuerpo`, `conChat`, `conDerecha`, `conTraza`, `conGateway` (R2, S1.R4–R6) | `GroupContainer`, `Variant: AutoLayout` | Feasible, including nested AutoLayout, Fill/Border/Radius and padding. Defaults must be overridden (see above). |
| S1.R1 title and subtitle (R5) | `Label` | Feasible. `Size`, `Color`, `Font` (Segoe UI), `FontWeight`, `Wrap`, `AutoHeight` and padding are inputs. Wrap and padding must be set deliberately. |
| S1.R2 guardrail, egress and agent pills (R8) | **Option A (recommended): `GroupContainer` AutoLayout wrapper (Fill, Border, Radius) + child `Label` (text, Color, Tooltip).** Option B: `ModernBadge`. | **`Label` has no `Radius*` input**, so the spec's "Label con relleno y radio" is not achievable with a bare Label. Option A keeps Label, as the spec and functionality ask. A container has no `Tooltip`, so the egress tooltip goes on the inner Label. A Label cannot size to its text width, so each pill needs a budgeted fixed width from its longest text. Option B (`ModernBadge`) has Fill/Color/Radius/Tooltip/Content/`AccessibleLabel`/Icon in one control. It is a different control from the spec's Label and its Tint/Intent surface must be checked for contrast. Not chosen unless the coordinator prefers it. |
| S1.R2 agent circle (R8, AS2) | `GroupContainer` AutoLayout with equal Width/Height and a radius of half the size and token Fill, or `Circle` (listed) | Both feasible. The container route needs no new control. `Circle` was not described because the container fully resolves the need. |
| S1.R3 segmented selector, A1 | **Recommended: two `Classic/Button`**. Alternatives: two `ModernButton`; `ModernTabList` | `Classic/Button` exposes `Fill`, `Color`, Radius*, Hover*/Pressed* and the Font enum. That allows exact token fills (cyan with colFondo text vs colPanel2 with colTexto) with no variant-surface ambiguity. It exposes **no `AccessibleLabel` and no selected-state output**. `Text` is the accessible name, and the selected state is conveyed by fill and by text/Tooltip only. `ModernButton` has `AccessibleLabel`, Radius*, Color and `BasePaletteColor`, but **no `Fill`**. Its surface depends on `Appearance`, so cyan fill with dark text must be achieved through `BasePaletteColor` and `Appearance` (variant contrast risk). `ModernTabList` exposes a `Selected` record and `Default`, so selected state is native. It has no `Fill`/radius input. Its minimums are 300×60, and its default appearance does not give cyan-fill/dark-text segments. See gap G3. |
| S1.R3 "Nueva conversación", A2 | `Classic/Button` (or `ModernButton`) | Feasible. `OnSelect` handles the reset. `AutoDisableOnSelect` is true by default on `Classic/Button`. It is harmless for an instant action but should be set deliberately. |
| S1.R3 session label | `Label` | Feasible. Font set to Consolas through the `Font` enum. **Consolas is not in the enum** (values include Arial, Courier New, Dancing Script, Georgia, Great Vibes, Lato…, Open Sans…, Patrick Hand, Segoe UI, Verdana). **`Courier New` is the available monospace.** This is a compromise on R4 ("Consolas or similar"), which allows it. |
| R9 timers `tmrEstado`, `tmrGuardrail` (A4) | `Timer` | `Duration`, `Repeat`, `AutoStart`, `OnTimerEnd`, `Visible` and `AutoPause` are inputs. `AutoStart` and `Repeat` default to false and must be set on both timers. `tmrGuardrail` also needs `AutoStart` (the functionality names it only as "repeat"). `AutoPause` defaults to true. It is harmless on a single screen. |
| R3 tokens | `App.Formulas` named formulas | See below. |
| R6 startup, A3 | `App.OnStart` | See below. |
| Panel titles (later phases) | `Label` | `Label` has **no letter-spacing input**, so the "wide letter spacing" in R4 is not achievable with Label. Not needed now (AS3). Later phases must accept the compromise or revisit. |

### `App.Formulas` and `App.OnStart` feasibility
- **Not verifiable with the authoring tools.** `describe_control` does not list an `App` control, so the `Formulas` and `OnStart` properties cannot be confirmed by metadata. The local `App.pa.yaml` has only `Theme`. They are standard App properties, but this is **unverified in this session**. The builder should confirm when writing, using the YAML syntax references, and should report any compile error rather than assume support.
- **Named colors.** Spec §4.2 uses `ColorValue("#050d24")` and similar. This is a pure constant formula, expected to work in `App.Formulas`. Only the formula text was checked, not execution. The 11 tokens required are `colFondo, colPanel, colPanel2, colLinea, colTexto, colApagado, colCian, colMorado, colVerde, colRojo, colAmbar`.
- **Screen fill and theme.** `colFondo` must be applied through the screen/root `Fill`. `Screen1` currently has no fill set.
- **Startup.** `Concurrent` with `ClearCollect`/`Set` inside, and `IfError` per call, matches spec §7.1. The `Get*` operations are not behavior-only and the `Set*` calls are only for later phases. Inside `Concurrent`, `varGwError` is written by the `IfError` branch and the order of writes is not deterministic. This does not matter here because GetState is the only writer. Keep the three `Set(...)` initializers after `Concurrent`. Runtime behavior of `IfError` around `ClearCollect` and the exact `FirstError.Message` text are unobserved.
- **Timers vs optimistic guard.** The reload handlers must not overwrite `varGw` while a guard flag is true (R9). The flag may be declared now (R7), but the connector `Set*` operations are not called yet.
- **Document dependence.** These choices depend on the current document state and must be rechecked after any of: control rename or replacement of Screen1, an app theme change, or a connector update. A connector re-import would change the response shapes above.

## Gaps and decisions

- **G1 — Closed (design r2).** Targets are 44 px, text ≥ 14 px and the header is 96 px. Arithmetic is in "Sizing and width-budget feasibility". Follow-ups: set the leaf minimums explicitly (session label ≤ 120–130 px; narrow-branch buttons 0) and prefer a 130 px session label.
- **G2 — Closed (design r2).** Pills are a rounded `GroupContainer` (Fill, Border, Radius) wrapping one `Label` (Option A). The egress Tooltip goes on the inner Label, because the container has no `Tooltip`. Fixed budgeted widths apply.
- **G3 — Closed with a residual.** Design r2 chose two separate `Classic/Button`s with a cyan fill on the selected one. Tool-backed: `Classic/Button` has no `AccessibleLabel` and no selected-state output. Design r2 still says "selected-state semantics" and "exposed as selected". That is not natively achievable; selection is conveyed by fill (cyan vs panel2), by `Text`, and optionally by a `Tooltip` stating the state. This is not colour alone, because the fill changes together with the text colour. Design/coordinator should word the acceptance check as "fill + text contrast" rather than "exposed as selected". If native semantics are required, the only alternative is `ModernTabList` (min 300×60, no token fill, no radius), which conflicts with the design. No further action is needed unless that wording is challenged.
- **G4 — Closed (design r2).** `Courier New` is the available monospace (Consolas is not in the Font enum).
- **G5 — Still open for later phases only.** Letter spacing is not available on Label, and design r2 still specifies "wide letter spacing" for panel titles. Not needed in this scope (AS3). Phases that add titles must drop the spacing or revisit the design.
- **G6 (runtime unverified).** Hidden timers running, `Visible = false` children leaving the flow, `ClearCollect` of an untyped column, App.Formulas/OnStart support and live connector responses cannot be observed with these tools. Each is covered by the failure semantics in R6 or by the builder reporting a compile error.
- **G7.** The `at` type difference (see "Data and API contracts") needs no action now. It is recorded so that phase 6 does not wrap it in `DateTimeValue`.

## Mock data
None proposed. The real connector is present and sufficient for every requested flow. No local-persistence limitations apply. Session and mode state is in-memory by design.

## Approval

Status: Approved
Revision tuple: functionality r1, screens r1, design r2, scrPanel.composition r2, discovery r2
Evidence: User input: "OK"
