# DraftSight Canvas Cover Generator

A DraftSight add-in that turns manual canvas cover drafting into a guided,
form-driven workflow. Operator enters dimensions and options → add-in
generates a layered DXF ready for production.

**Current status:** demo / proof-of-concept for an initial client.
**Target host:** DraftSight 2026 Professional or higher.
**Language:** C# (.NET Framework 4.8), WPF UI.

---

## Quick orientation

- **`CLAUDE.md`** — the playbook. Read this first. Covers project structure,
  the COM/CLSID loading mechanics, dev loop, common gotchas. Single source of
  truth for *how* we build DraftSight add-ins.
- **`docs/PROJECT_BRIEF.md`** — what this client needs and why.
- **`docs/DEMO_SCOPE.md`** — what the demo build will and won't do. Read
  before adding features.
- **`docs/HELLO_DRAFTSIGHT.md`** — first-time setup walkthrough. Get the SDK
  sample loading before writing any new code.
- **`docs/CURSOR_PROMPT.md`** — the canonical prompt for kicking off work in
  Claude Code / Cursor.
- **`CanvasCovers/`** — the actual add-in project (created during build).
- **`Installer/`** — Inno Setup script (added later, post-demo).

## Working with this repo

1. Read `CLAUDE.md` end to end before touching code.
2. Confirm DraftSight 2026 Professional (or higher) is installed and the
   SDK exists at `C:\Program Files\Dassault Systemes\DraftSight\APISDK\`.
3. Follow `docs/HELLO_DRAFTSIGHT.md` to get the SDK's hello-world sample
   loading. Do not skip this — it isolates environment issues from code
   issues.
4. Only after hello-world succeeds, scaffold the `CanvasCovers/` project.
5. Implement against `docs/DEMO_SCOPE.md`. Anything outside that scope is
   explicitly deferred.

## What this is *not*

- Not a production-ready, multi-tenant product. It's a demo to win the deal.
- Not licensed or DRM-gated yet. License integration happens post-demo.
- Not multi-version. Targets DraftSight 2026 only at this stage.
- Not signed. Will show "Unknown Publisher" until first commercial release.

## Out of scope (for the demo)

- More than one cover type
- Persistence beyond the current DraftSight session
- Multi-language UI
- ERP / cutting-table integration
- Auto-update mechanism
