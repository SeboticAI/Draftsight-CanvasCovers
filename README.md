# DraftSight CanvasCovers

A multi-product DraftSight add-in that automates canvas-product CAD
drafting. Operator enters job parameters into a branded WPF form, the
add-in draws the parts on layered, colour-coded, machine-ready output.

**Built for:** Adelaide Annexes & Canvas. The lift-blanket flow is
feature-complete and matches the client's real measurement sheet:
segment-driven COP, doubled-height math, built quilting, the fixed
sheet-style preview (mirrored Left/Right walls), the 6-layer cutter
panel, DXF export, and full input validation. **v1.7.0 is the current
production build** (beta-review + round-2 + round-3 change lists).

**Target host:** DraftSight 2026 Professional or higher (SDK access
required).
**Stack:** C# / .NET Framework 4.8, WPF, DraftSight COM interop.

**Current version:** v1.7.0 — installer EXE in
`Installer\Output\BesiaCAD-CanvasCovers-Setup-1.7.0.exe`.

---

## If you're picking this up after a break

Read in this order:

1. **[docs/STATUS.md](docs/STATUS.md)** — what works right now, the
   calculation model, and the known gotchas. Read this first; it's the
   canonical current-state doc (kept current to v1.7.0).
2. **[docs/ROADMAP.md](docs/ROADMAP.md)** — what's queued next. The §0
   item is the **customer handoff** (v1.7.0 is the production build); the
   working principle is "change only what's needed, don't touch stable
   old code."
3. **[docs/CLIENT_QUESTIONS.md](docs/CLIENT_QUESTIONS.md)** — remaining
   open questions (the big one — the exact quilting spacing rule — is
   the only unconfirmed geometry rule left).
4. **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — code structure,
   key types, how to add a new product.
5. **[docs/BUSINESS_CONTEXT.md](docs/BUSINESS_CONTEXT.md)** — client,
   ROI math, pricing thinking, productisation strategy.
6. **[CLAUDE.md](CLAUDE.md)** — the DraftSight add-in playbook. Read
   §9 specifically — gotchas that crash the host or silently fail.
   Don't re-learn them.

For runtime testing, [docs/LOAD_TEST.md](docs/LOAD_TEST.md) walks
through the install + click-through path.

For first-time setup of the SDK + dev environment on a fresh
machine, [docs/HELLO_DRAFTSIGHT.md](docs/HELLO_DRAFTSIGHT.md).

For the original commercial brief and high-level strategy,
[docs/PROJECT_BRIEF.md](docs/PROJECT_BRIEF.md).

---

## Quick start (for a new dev machine)

1. DraftSight 2026 Professional+ installed at the default path
   with the APISDK folder present
2. Visual Studio 2022 (or `dotnet` CLI) for .NET Framework 4.8
3. Inno Setup 6 (`https://jrsoftware.org/isdl.php`) for installer
   builds
4. Open `CanvasCovers/CanvasCovers.csproj` and build Release
5. **Close DraftSight**, then run `.\Installer\build.ps1` from the
   repo root. Produces a versioned EXE in `Installer\Output\`
6. Double-click the EXE — UAC prompts for admin; silent install
   lays down payload, registers COM, drops the XML in ProgramData
7. Open DraftSight, tick **CanvasCovers** in the Add-Ins manager
8. Ribbon → CanvasCovers tab → "Canvas Covers" button → Lift
   Blanket tile → fill form → Generate

For details on each step, the install / uninstall semantics, and
recovery from a startup crash, see
[docs/LOAD_TEST.md](docs/LOAD_TEST.md).

To uninstall: Settings → Apps → *BesiaCAD Canvas Covers* →
Uninstall.

---

## Project layout

```
CanvasCovers/           the add-in DLL project
docs/                   reference documentation
scripts/                rollback PowerShell (for startup-crash
                        recovery only; install path is the installer)
Installer/              Inno Setup script + build pipeline
reference/              (gitignored) client-supplied DXFs + sheets
CLAUDE.md               DraftSight add-in playbook
README.md               this file
.gitignore
```

For a detailed code tour, see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## What this is not (yet)

- Not a generic canvas-product tool — branded specifically for
  Adelaide Annexes & Canvas. Generalisation is a productisation
  step. See [docs/BUSINESS_CONTEXT.md](docs/BUSINESS_CONTEXT.md).
- Not multi-version — targets DraftSight 2026 only at this stage.
- Not signed — shows "Unknown Publisher" on add-in load.
- Not licence-gated — runs unconditionally.
- Only the lift-blanket product is built; the caravan annexe is a
  disabled picker tile pending a sample DXF.
- The quilting *spacing rule* is an operator-entered target pending the
  client's real formula (the geometry shape itself matches the sheet).

All of these are tracked in [docs/ROADMAP.md](docs/ROADMAP.md).
