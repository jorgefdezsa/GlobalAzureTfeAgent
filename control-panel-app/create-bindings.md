Revision: 1

Inputs: functionality r1, screens r1, design r2, scrPanel.composition r2, discovery r2 (Approved).

## Screens
- S1 scrPanel → key `Screen1`, /Users/jorgefdezsa/Sources/NetCoreConf/control-panel-app/Screen1.pa.yaml (key kept as Screen1 per CREATE rule), composition r2. StartScreen `=Screen1`.

## Shared data/state (App.OnStart / App.Formulas)
- Tokens: colFondo, colPanel, colPanel2, colLinea, colTexto, colApagado, colCian, colMorado, colVerde, colRojo, colAmbar.
- Globals: varGw (GetState record), varGwError "", varGuardrail (record or Blank), varModo "gateway", varPestana "requests", varLimiteBorrador 5, varTrazaIdx 0, varOcupado/varProbando/varCambiando false, varPreviousId "".
- Collections: colTools, colRequests (connector), colMensajes/colTrazas/colPasos (typed empty).
- Not yet defined (later phases): varUltimoResultado, colAudit, tmrAuditoria.

## Dataset bounds
colTools / colRequests: unknown, not rendered in this scope.

## Initialization
Coordinator-owned App.OnStart; refresh by tmrEstado (5 s, skipped while varCambiando) and tmrGuardrail (30 s).

## Build status
S1: Written; compile clean. Runtime unverified: hidden timers, pill collapse, connector responses, App.Formulas rendering.
Compromises: Courier New (no Consolas); selected mode = fill + Tooltip (no selected-state output); narrow (<1024) reflow not implemented; letter spacing absent.
