Revision: 1

Inputs:
- /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/create-functionality.md (Revision 1)
- Spec: /Users/jorgefdezsa/Sources/NetCoreConf/docs/especificacion-canvas-app.md (§4, 5, 6.1, 7.1, 7.2)

## Screen set

Explicit user constraint (R1, verbatim): "One screen named `scrPanel`, tablet landscape 16:9 (1366×768). The existing blank Screen1 is renamed/reused as this screen; no other screen."

The app is one screen. All operator tasks are glanceable status plus two local controls, so no second screen or navigation is warranted. Later-phase content (chat, trace, gateway controls, audit) lives in regions of this same screen, not in separate screens.

Landing screen: **S1**.

## S1 — scrPanel (landing screen)

**Purpose:** Single operator panel for "¿Cuándo aterriza?". In scope now, it shows at a glance whether the agent is connected and whether guardrail and egress control are active. It also lets the operator choose the call mode and see or reset the session.

**Owns:** R1–R10; A1, A2, A3, A4.

**Layout regions (logical, from R2):**
- Header region: title and subtitle (R5), status pills, mode selector, session label and "Nueva conversación" (R8). Owns R5, R8, A1, A2.
- Chat region (48 % width, left): empty placeholder for now. Panel styling applies (R2, R4); no content (AS3). Later phase.
- Right column (52 % width), stacked: trace placeholder over gateway placeholder. Both are empty with panel styling (R2, AS3). Later phases.
- App-level behaviours with no visible region: startup load (R6, A3), timed refresh (R9, A4), design tokens and visual rules (R3, R4) and variable declarations (R7). They are tied to S1 because it is the only screen. Timers are hidden.

**Information needed for tasks:**
- Agent status: gateway state, its error indicator and the "agent configured" flag.
- Guardrail pill: guardrail found and jailbreak blocking.
- Egress pill: egress mode and the allowed-hosts list (tooltip).
- Mode selector: current mode (gateway or direct).
- Session label: current session id (last 6 characters) or blank.
- Data comes only from the real `GlobalDemoControl` connector. No mock data.

**Entry:** App start, which lands directly on S1. There is no other entry path.
**Exit:** None. S1 has no navigation to or from any other screen.

### In-screen states

| State | Trigger | What the operator sees |
|---|---|---|
| Loading (first load) | App start before the loads finish (R6) | Skeleton with the static title and subtitle. State-driven pills show their not-yet-known state: guardrail and egress hidden, agent pill per R8 rule (red "Sin conexión" while state is blank). Mode defaults to "Vía AI Gateway" and the session label to "Sesión nueva". |
| Connected | GetState succeeds, error blank, agent configured | Green "Agente activo" |
| Disconnected / failure | GetState fails, or the agent is not configured | Red "Sin conexión". The error message is held in `varGwError`. |
| Recovered | A later GetState succeeds | `varGwError` cleared; the pill returns to green if the agent is configured. |
| Guardrail unreadable | GetGuardrail fails, or the result is blank | Guardrail and egress pills hidden. Nothing is invented. |
| Guardrail active / absent | Guardrail found and jailbreak blocking, or not | Green "🛡 Guardrail activo" or red "⚠ Sin guardrail" |
| Egress shown | Guardrail found and egress mode non-empty | Green "Egress · solo el AI Gateway" if `Enforced`, otherwise amber "Egress · en auditoría". Tooltip lists the allowed hosts, comma-joined. |
| Mode selected | A1 | The selected segment is highlighted. No backend call and no failure state. |
| Session empty / active | Session id blank vs set | "Sesión nueva" vs "Sesión " + last 6 characters |
| Reset conversation | A2 | Messages cleared, session blank, busy false. When already empty, no visible change (no-op). |
| Confirmation / cancel | None | "Nueva conversación" is immediate and has no confirmation, because the functionality defines none. This is not a design gap. |
| Empty (data) | Empty tools or request lists | Not shown in this phase. The lists are loaded for later regions and no region displays them yet. |

Background refresh (A4) updates the state and requests every 5 s and the guardrail every 30 s. It does not change the mode, the session or the messages, and it shows no loading indicator. The R9 guard against overwriting state during an optimistic change is declared now and used from phase 3.

## Requirement and action ownership

| IDs | Home |
|---|---|
| R1, R2, R3, R4 | S1 (structure, tokens and visual rules apply screen-wide) |
| R5, R8 | S1 header region |
| R6, R7, R9 | S1 app-level behaviour (startup, variables, timers) |
| R10 | S1 acceptance |
| A1, A2 | S1 header region |
| A3, A4 | S1 app-level behaviour; the outcomes show in the S1 header |

No action has a cross-screen outcome. The later-phase placeholders (chat, trace, gateway) are in S1 and will keep their owner in S1.

## Round trips and context

There is no cross-screen navigation, so there is no Back path to preserve.

- **Mode choice across refreshes:** The operator picks "Directo al backend", and the 5 s and 30 s refreshes then run. The selected mode must survive and must not reset to gateway. Only app start sets the default "gateway".
- **Session across refreshes and mode changes:** The session label and its id must survive timer refreshes and mode switches. Only "Nueva conversación" resets the session to "Sesión nueva". Messages and busy flag are cleared with it.
- **Connection loss and recovery:** The operator watches "Agente activo". A failed refresh turns the pill red. A later successful refresh restores it to green, with no restart and no change to mode or session.
- **Intentional resets:** Only A2 (messages, session, busy flag) and app start (mode, draft limit, trace index) reset anything. Neither the timers nor the mode selector reset the session.
- **Future phases:** Chat, trace and gateway controls will share S1's mode and session context. When they arrive, they must respect the same survival rules.
