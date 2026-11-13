Revision: 1

Inputs:
- Original request (verbatim): "Canvas App ... aqui te dejo las especificaciones" — spec file `/Users/jorgefdezsa/Sources/NetCoreConf/docs/especificacion-canvas-app.md` (sections cited below as §).
- Clarification (verbatim): "El conector GlobalDemoControl ya está añadido en Studio ... continúa con la fase 2."
- Coordinator scope: phase 1 work was lost, so this covers spec phases 1 AND 2 (§8.3). Later phases (gateway toggles, tester, trace, chat, audit tabs) are out of scope, but `conChat`/`conTraza`/`conGateway` must exist as empty placeholders.
- Coordinator constraints: all UI text in Spanish, texts copied from spec; colors only via named tokens (no RGBA literals in controls); do not invent data or mock: real source is the custom connector `GlobalDemoControl` (listed in list_apis; operations GetTools, GetState, GetGuardrail, SetBlockedAll, SetBlockedTools, SetRateLimit, GetRequests, GetAudit, TestTool, ChatSync). Screen1 is reused/renamed as the landing screen (spec asks for `scrPanel`).

## Users and essential tasks
- Operator of the "¿Cuándo aterriza?" demo (single role; no auth or roles in the app). Task in scope: see at a glance whether the agent is connected, whether the guardrail and egress control are active, choose the call mode (Gateway / Directo), see the session, and start a new conversation.

## Domain concepts
- Gateway state (`varGw`): blockedAll, blockedTools, rateLimit, agentConfigured, apim, prices.
- Guardrail (`varGuardrail`): found, jailbreakBlocking, egressMode, allowedHosts; Blank when unreadable.
- Tools catalogue (`colTools`), request log (`colRequests`), mode (`gateway` | `direct`), session id (`varPreviousId`).
- Persistence: none. In-memory variables/collections only; the source of truth is the `GlobalDemoControl` connector. No mock data is proposed or needed.

## Requirements
- R1 (Phase 1): One screen named `scrPanel`, tablet landscape 16:9 (1366×768). The existing blank Screen1 is renamed/reused as this screen; no other screen.
- R2 (Phase 1): Container structure per §4.1, with auto-layout so it rescales: `conRaiz` (vertical) → `conCabecera` (horizontal) and `conCuerpo` (horizontal, 2 columns) → `conChat` (width 48 %) and `conDerecha` (vertical, width 52 %) → `conTraza` and `conGateway`. `conChat`, `conTraza` and `conGateway` are empty placeholders now (no content, but panel styling applies).
- R3 (Phase 1): Design tokens defined once in `App.Formulas` as named colors, exactly: colFondo #050d24, colPanel #0b1a3f, colPanel2 #0e2250, colLinea #19316b, colTexto #dbe7ff, colApagado #7f93c2, colCian #2fe0ff, colMorado #c78cff, colVerde #36d399, colRojo #ff6b5e, colAmbar #ffc861. Constraint: "colores solo desde los tokens de la sección 4.2, sin literales de color en los controles" (no RGBA literals in controls).
- R4 (Phase 1): Visual rules (§4.2): dark theme with `colFondo` background; panels `colPanel` fill, 1 px `colLinea` border, radius 14, padding 16/18; panel titles uppercase, 11.5 px, `colMorado`, wide letter spacing; font Segoe UI for text, Consolas (or similar monospace) for tool names/arguments/requests. State colors: ok→verde, blocked/egress→rojo, limited→ámbar, error→rojo, pending→apagado. The user-message color `#123a78` appears only in chat (out of scope); if needed later it must also be a named token.
- R5 (Phase 1, shown with real data in Phase 2): Static header texts in `conCabecera`, Spanish:
  - Title "¿Cuándo aterriza? · asistente-vuelos", 19 px, `colCian`.
  - Subtitle "Foundry Hosted Agent · .NET 10 · Agent Framework | tools por MCP | gobernado desde Foundry (AI Gateway)", `colApagado`.
- R6 (Phase 2): `App.OnStart` (§7.1) loads in parallel (`Concurrent`) from the connector: `colTools` ← GetTools, `varGw` ← GetState, `varGuardrail` ← GetGuardrail, `colRequests` ← GetRequests. Then initializes `varModo = "gateway"`, `varLimiteBorrador = 5`, `varTrazaIdx = 0`. Each call is wrapped in error handling: GetState failure sets `varGwError` to the error message; GetGuardrail failure sets `varGuardrail` to Blank (header shows nothing rather than inventing).
- R7 (Phase 2): All app variables of §5 exist (colTools, varGw, varGwError, varGuardrail, colRequests, varModo, colMensajes, varPreviousId, varOcupado, varProbando, varUltimoResultado, colTrazas, colPasos, varTrazaIdx, varPestana, varLimiteBorrador) with the stated types/defaults. Those for later phases are only initialized/declared (empty/default) so later phases can use them; `varPestana` default `"requests"`, `varPreviousId` blank, `varOcupado`/`varProbando` false. Only the variables for R6 are loaded from the backend.
- R8 (Phase 2): Header with real data (§6.1):
  - Guardrail pill: visible if `varGuardrail` is not blank; green "🛡 Guardrail activo" if `found && jailbreakBlocking`, else red "⚠ Sin guardrail".
  - Egress pill: visible if `found` and `egressMode` non-empty; `Enforced` → green "Egress · solo el AI Gateway"; any other value → amber "Egress · en auditoría"; tooltip lists `allowedHosts` joined by comma.
  - Agent-state pill (label + circle): green "Agente activo" if `varGw` not blank AND `varGwError` blank AND `varGw.agentConfigured`; else red "Sin conexión".
  - Mode selector: two segmented buttons "Vía AI Gateway" / "Directo al backend"; active one with `colCian` fill and dark text; sets `varModo` to `"gateway"` / `"direct"`.
  - Session label: "Sesión nueva" if `varPreviousId` blank, else "Sesión " + last 6 chars of `varPreviousId`.
  - Button "Nueva conversación": clears `colMensajes`, blanks `varPreviousId`, sets `varOcupado` false.
- R9 (Phase 2): Timers (§7.2), hidden (`Visible = false`): `tmrEstado` 5000 ms, repeat, auto-start, reloads `varGw` and `colRequests`; `tmrGuardrail` 30000 ms, repeat, reloads `varGuardrail`. They must not overwrite `varGw` while an optimistic change is in progress (relevant from phase 3; guard flag may be declared now). `tmrAuditoria` (15000 ms, only when `varPestana = "audit"`) is deferred to phase 7. Timer reload failures follow R6 error semantics (GetState failure → `varGwError`; GetGuardrail failure → Blank), and a successful reload of GetState clears `varGwError`.
- R10 (Acceptance, §8.3): Phase 1: dark skeleton visible, no errors. Phase 2: pills and "Agente activo" reflect the real backend; no formula errors.

## Actions
- A1 (R8): Operator selects "Vía AI Gateway" or "Directo al backend" → `varModo` changes and the selected button is highlighted. No backend call. No failure state.
- A2 (R8): Operator presses "Nueva conversación" → `colMensajes` cleared, `varPreviousId` blank (label becomes "Sesión nueva"), `varOcupado` false. Works also when already empty (no-op visible change).
- A3 (R6): App start → parallel load; on success header pills populate; on failure of GetState, "Sin conexión" and `varGwError` set; on failure of GetGuardrail, guardrail/egress pills hidden.
- A4 (R9): Timers fire automatically (no user actor) → refresh state/requests every 5 s and guardrail every 30 s; failures follow R6.

## Out of scope now (keep placeholders only)
Gateway toggles/stepper/hint/tester/result/tabs (§6.4, 7.3, 7.5), trace (§6.3), chat (§6.2, 7.4), audit list and `tmrAuditoria`, `colAudit`. Operations SetBlockedAll, SetBlockedTools, SetRateLimit, GetAudit, TestTool, ChatSync are not called in this scope.

## Assumptions
- AS1 (R6, R8, R9): The connector's operations return the shapes documented in §3.3, available as `GlobalDemoControl.<OperationId>()`; this is unverified until validated in the authoring session. `blockedTools` arrives as a table with field `Value`.
- AS2 (R8): Egress/guardrail pills are placed in the header as separate labels; "Agente activo" circle color follows pill color.
- AS3 (R2): Placeholders are empty containers; panel title text is not added until their phases.
- AS4 (R1): Backend reachability, Entra auth and DLP grouping (§3.1, §9) are external to the app; if calls fail, the app shows the failure states in R6.
- AS5: Spec text "Sesión " + last 6 characters; exact emoji glyphs (🛡, ⚠) are copied from the spec.

## Material questions
- None blocking. Mock data is not proposed: the connector is the real source and was stated as already added.
