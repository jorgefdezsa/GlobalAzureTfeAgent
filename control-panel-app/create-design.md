Revision: 2

Revision note (r2, gap G1/G2/G3/G4 from create-discovery.md r1): no exceptions to builder rules. Header targets raised to 44 px, all text ≥ 14 px (panel titles included), header re-budgeted. Pills = rounded container wrapping a label; mode selector = two separate buttons; monospace = Courier New.

Inputs:
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-discovery.md (Revision 1), gaps G1–G4
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-screens.md (Revision 1)
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-functionality.md (Revision 1), R1–R5, R8
- Spec /Users/jorgefdezsa/Sources/NetCoreConf/docs/especificacion-canvas-app.md §4 and §6.1

# Shared design

Scope: one screen (S1 `scrPanel`), so these decisions are shared by its regions and by the later-phase placeholders. There are no screen families and no intentional differences.

## Character
A dark, dense, glanceable operator console shown on a stage or demo display. Information is read at a distance, so status is carried by colour plus text (never colour alone). No decoration beyond panel borders. Familiar header-over-two-columns layout fits the five-zone task, so no novelty is added.

## App identity and labels
- Title (fixed text, R5): "¿Cuándo aterriza? · asistente-vuelos".
- Subtitle (fixed text, R5): "Foundry Hosted Agent · .NET 10 · Agent Framework | tools por MCP | gobernado desde Foundry (AI Gateway)".
- All UI text is Spanish and copied from the spec. No navigation exists (single screen), so there is no navigation treatment.
- Panel titles are added only when their phases arrive (AS3); when they do they follow the panel-title style below.

## Target viewports
| Anchor | Size (w×h) | Role |
|---|---|---|
| Primary | 1366×768 (R1, tablet landscape 16:9) | Design target; all measurements below are for it |
| Smaller landscape | 1024×768 | Reasoning anchor: same arrangement, proportions rescale |
| Narrow | 375×667 (and 320×568 as the floor) | Reasoning anchor only; the app is not designed for phones, but must stay usable |

Adaptation:
- ≥ 1024 wide: arrangement identical to primary; regions rescale by proportion and the fixed-size items keep their sizes.
- < 1024 wide: body columns stack (chat above the right column, trace above gateway) and the screen becomes a vertical scroll owner. The header reflows (see S1 composition); at 1024 wide it grows to about 136 px. Nothing is hidden: every pill, the mode selector, the session label and "Nueva conversación" remain reachable without leaving the header.

## Spacing and density
- Screen outer margin 16 px; gap between regions 16 px.
- Panel: 1 px border, radius 14, inner padding 16 vertical / 18 horizontal (R4).
- Header: compact but touch-sized; items vertically centred within rows; 12 px gap between pills, 12 px between groups, 8 px between the two right-block rows.
- Minimum interactive target 44×44 px; pills are 44 px tall too so that the egress pill (tooltip on focus) is a valid target.
- Density: high for header, spacious (panel padding only) for empty placeholders.

## Type scale
| Role | Font | Size | Weight | Colour role |
|---|---|---|---|---|
| App title | Segoe UI | 19 px | Semibold | Accent cyan |
| Subtitle | Segoe UI | 14 px | Regular | Muted |
| Pill text | Segoe UI | 14 px | Semibold | Dark on filled pill / see colour roles |
| Mode button text | Segoe UI | 14 px | Semibold | Dark on active; body text colour on inactive |
| Session label | Courier New (monospace) | 14 px | Regular | Muted |
| Action button text | Segoe UI | 14 px | Semibold | Body text |
| Panel title (later phases) | Segoe UI | 14 px, uppercase, wide letter spacing | Semibold | Purple |
| Tool names / arguments / requests (later phases) | Courier New | 14 px | Regular | Body text |

Minimum text size 14 px everywhere; the former 11.5 px panel-title style is dropped (no exception).

## Colour roles (named tokens only, R3/R4; no literals in controls)
| Role | Token |
|---|---|
| Screen background | colFondo |
| Panel fill | colPanel |
| Raised/secondary fill (inactive segmented track, later assistant messages) | colPanel2 |
| Borders and dividers | colLinea |
| Body text | colTexto |
| Muted text (subtitle, session, pending) | colApagado |
| Accent / selected state / title | colCian |
| Panel titles | colMorado |
| OK / active | colVerde |
| Blocked / egress / error / disconnected | colRojo |
| Limited / audit-only egress | colAmbar |

Contrast intent: body and muted text on panel fill reads clearly on the dark theme (muted is for secondary information only, never for status the operator must act on). Pills are a fully rounded container (tinted token fill, token border) holding one text label. Mode selector is two separate adjacent buttons (no segmented widget); the selected one uses cyan fill. Pills are a tinted token fill with token-coloured border and text on the fill chosen to remain legible; the state word is always in the label. The selected segment is cyan fill with dark text (colFondo as text colour). A user-message colour, if needed in a later phase, must be added as a new named token.

## Interaction and accessibility intent
- Hit targets in the header at least 44×44 px (tablet touch); the two mode buttons and "Nueva conversación" are each at least 44 px tall and wider than 44 px.
- Keyboard/reading order follows visual order: title → subtitle → status pills → mode selector → session → "Nueva conversación" → (later) chat, trace, gateway.
- Selected mode is conveyed by fill and by selected-state semantics, not colour alone.
- Pills carry their full text; the egress tooltip is supplementary, never the only place for state.
- Background refreshes produce no loading indicator or layout shift: pills appear/disappear in place within a reserved row (see S1.R2).
