# CanvasCovers Project

Minimal guarded DraftSight add-in shell for the canvas cover demo.

Current status:
- COM-visible `DsAddin` entry class exists.
- First command is hello-world only: `Hello CanvasCovers`.
- No cover geometry or WPF feature UI has been added yet.

Development safety:
- Register with 64-bit `RegAsm` only after building.
- Use `CanvasCovers.xml` with `startup="0"` during development.
- Keep the unregister command ready before each DraftSight load test.
