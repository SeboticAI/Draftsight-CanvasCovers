# DraftSight CanvasCovers

A multi-product DraftSight add-in that automates canvas-product CAD
drafting. Operator enters job parameters into a branded WPF form, the
add-in draws the parts on layered, colour-coded, machine-ready output.

**Built for:** Adelaide Annexes & Canvas. The owner currently spends
5+ hours per week manually drafting canvas products in DraftSight; this
tool gives that time back.

**Status:** lift blanket flow working end-to-end. Caravan annexe slot
exists in the product picker; awaiting measurement spec from the client.

**Target host:** DraftSight 2026 Professional or higher (SDK access
required).
**Stack:** C# / .NET Framework 4.8, WPF, DraftSight COM interop.

---

## If you're picking this up after a break

Read in this order:

1. **[docs/STATUS.md](docs/STATUS.md)** — what works right now and what
   doesn't. Five minutes; orients you to the current state.
2. **[docs/ROADMAP.md](docs/ROADMAP.md)** — what's queued next and why.
3. **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — code structure,
   key types, how to add a new product.
4. **[docs/BUSINESS_CONTEXT.md](docs/BUSINESS_CONTEXT.md)** — client,
   ROI math, pricing thinking, productisation strategy.
5. **[CLAUDE.md](CLAUDE.md)** — the DraftSight add-in playbook
   itself. Read § 9 specifically — the four gotchas that crash the
   host. Don't re-learn them.

For runtime testing, [docs/LOAD_TEST.md](docs/LOAD_TEST.md) walks
through the deploy + click-through path.

For first-time setup of the SDK and dev environment,
[docs/HELLO_DRAFTSIGHT.md](docs/HELLO_DRAFTSIGHT.md) (still useful
on a fresh machine).

For historical context on the original brief and demo scope (before
the project pivoted to lift blanket + multi-product), see
[docs/PROJECT_BRIEF.md](docs/PROJECT_BRIEF.md) and
[docs/DEMO_SCOPE.md](docs/DEMO_SCOPE.md). These are kept as historical
artifacts — current intent is in STATUS.md / ROADMAP.md.

---

## Quick start (for new dev machines)

1. DraftSight 2026 Professional+ installed at the default path with
   the APISDK folder present
2. Visual Studio 2022 (or `dotnet` CLI) for .NET Framework 4.8
3. Open `CanvasCovers/CanvasCovers.csproj` and build Release
4. Run `.\scripts\deploy-canvascovers.ps1` in an Administrator
   PowerShell
5. Open DraftSight, tick **CanvasCovers** in the Add-Ins manager
6. Ribbon → CanvasCovers tab → "Canvas Covers" button → Lift Blanket
   tile → fill form → Generate

For details on each step, deploy / rollback semantics, and recovery
from crashes, see [docs/LOAD_TEST.md](docs/LOAD_TEST.md).

---

## Project layout

```
CanvasCovers/           the add-in DLL project
docs/                   reference documentation (this directory)
scripts/                deploy / rollback PowerShell
Installer/              empty (Inno Setup script lives here when added)
CLAUDE.md               DraftSight add-in playbook
README.md               this file
.gitignore
```

For a detailed code tour, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## What this is not (yet)

- Not a generic canvas-product tool — branded specifically for
  Adelaide Annexes & Canvas. Generalisation is a productisation step,
  not a code task. See [docs/BUSINESS_CONTEXT.md](docs/BUSINESS_CONTEXT.md).
- Not multi-version — targets DraftSight 2026 only at this stage.
- Not signed — shows "Unknown Publisher" on add-in load.
- Not licence-gated — runs unconditionally.
- Not bundled into an installer — deploy via the PowerShell script.

All of these are tracked in [docs/ROADMAP.md](docs/ROADMAP.md).
