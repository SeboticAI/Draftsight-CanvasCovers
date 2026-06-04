# Lift Blanket Real Geometry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the wrong-shaped lift-blanket wall model with the geometry the client's real DXFs actually use — plain rectangles whose cut height is the auto-doubled (measured − fixing-allowance) value, with per-wall optional COP placed from the bottom and horizontally offset, plus a one-click DXF export.

**Architecture:** Split the work into a **headless, unit-testable math/derivation core** (`LiftBlanketCalculator`) and the **SDK-emission layer** (`LiftBlanketGenerator`). The calculator turns operator inputs into world-coordinate primitives (rectangles, COP rect, fold midline, dim specs, text specs); the generator just walks those primitives and calls the DraftSight SDK. This makes the rules testable without DraftSight, and makes the generator a thin, low-risk translator. The dialog is reshaped to mirror the paper measurement sheet (segment boxes + per-wall COP + editable fixing allowance). DXF export pops DraftSight's native Save-As dialog defaulting to the network number.

**Tech Stack:** C# `net48`, WPF, DraftSight COM interop (`DraftSight.Interop.dsAutomation`), MSTest for the headless core, PowerShell `reference/parse-dxf.ps1` for DXF round-trip verification.

---

## Ground truth (from the reference DXFs + measurement sheet — do not re-derive)

Verified against `reference/dxf/12345 TEST 12345.dxf` (2 walls) and
`12346 TEST 12346.dxf` (3 walls: L/R/B). Full analysis in
`reference/DXF_FINDINGS.md`. Coordinates below are job 12346 left wall.

- **Cut piece = one closed rectangle per wall** on layer `1 Rotary Blade`.
  No door-return steps in the cut path.
- **Walls are L / R / B(ack).** Through-Car OFF ⇒ B present (3 walls);
  ON ⇒ no B (2 walls).
- **Wall width = sum of the bottom-row segments.** Sheet left wall:
  `250 + 350 + 240 + 1400 + 0 = 2240` = DXF cut width 2240. The two
  outer boxes are door-return tabs ("place zero if not needed").
- **Cut height is auto-doubled:**
  `half = measuredHeight − fixingAllowance`; `cutHeight = half × 2`.
  Sheet: `2200 − 50 = 2150`, `× 2 = 4300` = DXF cut height 4300.
  Default fixing allowance −50 (hooks), operator-editable.
- **The bottom half is the real panel; the top half is its mirror.**
  Fold midline at `wallBottomY + half`. COP and (future) quilting live
  only in the bottom half.
- **COP (optional, per wall):** width + height + gap-from-bottom typed
  by the operator, placed from the wall bottom. Job 12346: bottom-gap
  600, height 1300, width 240 → DXF COP Y 1843.4–3143.4 on a
  1243.4–5543.4 piece. **COP sits entirely below the fold midline.**
- **COP horizontal:** operator enters an offset from the wall's left
  edge. Job 12346: COP left edge = 600 from wall left edge (= first two
  segments 250+350), confirming a left-edge-referenced offset.
- COP, quilt lines, and wall-label TEXT (`<net> <name> L/R/B`, height 20)
  go on `5 Draw and Text`. DIMENSIONs + reference text + project metadata
  go on `0`.
- **Quilting is OUT OF SCOPE this round** (no input on the sheet; client
  rule unconfirmed). A `QuiltingEnabled` flag is added but defaults false
  and emits nothing — wired for a later round.

---

## File structure

**New files:**
- `CanvasCovers/Models/Products/LiftBlanket/WallSegments.cs` — the
  bottom-row segment values for one wall (door returns + L/LR segments).
- `CanvasCovers/Models/Products/LiftBlanket/CopPlacement.cs` — per-wall
  COP inputs (enabled, width, height, gap-from-bottom, offset-from-left).
- `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs` —
  headless math: derives cut rectangles, fold midline, COP rect, and dim
  specs from a `LiftBlanketJob`. No SDK references.
- `CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs` — plain DTOs
  the calculator emits and the generator consumes (`WallLayout`,
  `RectSpec`, `DimSpec`, `LabelSpec`).
- `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs` — static
  lookup mapping `FixingType` → default allowance mm.
- `CanvasCovers/IO/DxfExporter.cs` — pops DraftSight's native DXF Save-As,
  default filename from the network number.
- `CanvasCovers.Tests/CanvasCovers.Tests.csproj` — MSTest project,
  references only the headless code (no interop).
- `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs` — unit tests for the
  math core.
- `CanvasCovers.Tests/FixingAllowanceTests.cs` — unit tests for the lookup.
- `CanvasCovers.Tests/DxfFilenameTests.cs` — unit tests for filename
  derivation.

**Modified files:**
- `CanvasCovers/Models/Products/LiftBlanket/WallDimensions.cs` — replace
  DoorReturn1/2/3 + Cop* fields with `Segments` + `CopPlacement` +
  `MeasuredHeight`; add derived helpers.
- `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs` — add
  `FixingAllowanceMm` (editable), keep `FixingType`, add `QuiltingEnabled`.
- `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` —
  rewrite to consume `WallLayout` from the calculator; emit rectangles +
  mirrored COP + labels on the correct layers.
- `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` + `.cs` —
  reshape wall sections to segment boxes + per-wall COP + measured height;
  add fixing-allowance field; add "Export DXF after generate" checkbox.
- `CanvasCovers/UI/Controls/WallDiagram.xaml.cs` — redraw to show the
  segment + COP model instead of door-return steps.
- `CanvasCovers/Commands/OpenCanvasCoversCommand.cs` — call `DxfExporter`
  after a successful generate when the export checkbox is ticked.
- `CanvasCovers.csproj` — no change expected (existing project already
  compiles the new files via SDK-style globbing); verify.
- A new solution file `CanvasCovers.sln` tying the add-in + test project.

---

## Task 1: Solution + headless test project scaffold

**Files:**
- Create: `CanvasCovers.sln`
- Create: `CanvasCovers.Tests/CanvasCovers.Tests.csproj`
- Create: `CanvasCovers.Tests/SmokeTest.cs`

- [ ] **Step 1: Create the test csproj**

Create `CanvasCovers.Tests/CanvasCovers.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <!-- Test project references ONLY the headless math code by file link,
         never the interop DLLs, so tests run on any machine without
         DraftSight installed. -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.1" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.1" />
  </ItemGroup>
  <ItemGroup>
    <!-- Link the headless source files directly. Added as each file is
         created in later tasks. The interop-dependent generator is NOT
         linked here. -->
    <Compile Include="..\CanvasCovers\Geometry\Products\LiftBlanket\FixingAllowance.cs" Link="Linked\FixingAllowance.cs" />
    <Compile Include="..\CanvasCovers\Geometry\Products\LiftBlanket\WallLayout.cs" Link="Linked\WallLayout.cs" />
    <Compile Include="..\CanvasCovers\Geometry\Products\LiftBlanket\LiftBlanketCalculator.cs" Link="Linked\LiftBlanketCalculator.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\WallSegments.cs" Link="Linked\WallSegments.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\CopPlacement.cs" Link="Linked\CopPlacement.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\WallDimensions.cs" Link="Linked\WallDimensions.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\LiftBlanketOptions.cs" Link="Linked\LiftBlanketOptions.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\LiftBlanketJob.cs" Link="Linked\LiftBlanketJob.cs" />
    <Compile Include="..\CanvasCovers\Models\ProjectMetadata.cs" Link="Linked\ProjectMetadata.cs" />
    <Compile Include="..\CanvasCovers\Models\LayerSetting.cs" Link="Linked\LayerSetting.cs" />
    <Compile Include="..\CanvasCovers\Models\LayerSettings.cs" Link="Linked\LayerSettings.cs" />
    <Compile Include="..\CanvasCovers\IO\DxfExporter.Filename.cs" Link="Linked\DxfExporter.Filename.cs" />
  </ItemGroup>
</Project>
```

> NOTE: `LiftBlanketJob.cs` references `ProjectMetadata` and `LayerSettings`
> only as plain POCOs (no interop), so linking them is safe. If a linked
> file turns out to pull an interop type, extract the pure part instead.
> `DxfExporter.Filename.cs` (Task 9) is the interop-free half of the
> exporter — only the filename logic is linked.

- [ ] **Step 2: Create the solution and add both projects**

Run:
```powershell
dotnet new sln -n CanvasCovers
dotnet sln CanvasCovers.sln add CanvasCovers\CanvasCovers.csproj
dotnet sln CanvasCovers.sln add CanvasCovers.Tests\CanvasCovers.Tests.csproj
```
Expected: "Project ... added to the solution" twice.

- [ ] **Step 3: Add a smoke test**

Create `CanvasCovers.Tests/SmokeTest.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class SmokeTest
    {
        [TestMethod]
        public void Arithmetic_Works()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
```

- [ ] **Step 4: Comment out not-yet-existing Compile links**

The linked files in Step 1 don't exist yet. Temporarily comment out every
`<Compile Include="..\CanvasCovers\...">` line in the test csproj so the
project builds with only `SmokeTest.cs`. Uncomment each line in the task
that creates the corresponding file.

- [ ] **Step 5: Run the smoke test**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 1 passed.

- [ ] **Step 6: Commit**

```powershell
git add CanvasCovers.sln CanvasCovers.Tests
git commit -m "test: scaffold headless MSTest project + solution"
```

---

## Task 2: FixingAllowance lookup (headless)

**Files:**
- Create: `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs`
- Test: `CanvasCovers.Tests/FixingAllowanceTests.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (uncomment the
  `FixingAllowance.cs` + `LiftBlanketOptions.cs` Compile links)

- [ ] **Step 1: Write the failing test**

Create `CanvasCovers.Tests/FixingAllowanceTests.cs`:

```csharp
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class FixingAllowanceTests
    {
        [TestMethod]
        public void Hooks_Default_Is_50()
        {
            Assert.AreEqual(50.0, FixingAllowance.DefaultFor(FixingType.HooksFacingOut));
            Assert.AreEqual(50.0, FixingAllowance.DefaultFor(FixingType.HooksFacingIn));
        }

        [TestMethod]
        public void PressStuds_Default_Is_40()
        {
            Assert.AreEqual(40.0, FixingAllowance.DefaultFor(FixingType.PressStuds));
        }

        [TestMethod]
        public void Velcro_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.Velcro));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: compile error — `FixingAllowance` does not exist.

- [ ] **Step 3: Implement**

Create `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs`:

```csharp
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Default hook/stud/eyelet allowance subtracted from the measured
    // height before doubling. Values from the FIXINGS table stamped on
    // every client drawing: HOOKS -50, PRESS STUDS -40, EYELET -30,
    // VELCRO 0. The operator can override the actual value used per job
    // (see LiftBlanketOptions.FixingAllowanceMm); this is just the
    // sensible default for each fixing type.
    public static class FixingAllowance
    {
        public static double DefaultFor(FixingType fixing)
        {
            switch (fixing)
            {
                case FixingType.HooksFacingIn:
                case FixingType.HooksFacingOut:
                    return 50.0;
                case FixingType.PressStuds:
                    return 40.0;
                case FixingType.Velcro:
                    return 0.0;
                default:
                    return 0.0;
            }
        }
    }
}
```

- [ ] **Step 4: Uncomment the Compile links**

In `CanvasCovers.Tests\CanvasCovers.Tests.csproj`, uncomment the
`FixingAllowance.cs` and `LiftBlanketOptions.cs` Compile lines.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 4 passed (3 + smoke).

- [ ] **Step 6: Commit**

```powershell
git add CanvasCovers
git commit -m "feat: fixing-allowance lookup with unit tests"
```

---

## Task 3: New model types — WallSegments, CopPlacement, options

**Files:**
- Create: `CanvasCovers/Models/Products/LiftBlanket/WallSegments.cs`
- Create: `CanvasCovers/Models/Products/LiftBlanket/CopPlacement.cs`
- Modify: `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs`
- Test: `CanvasCovers.Tests/WallModelTests.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (uncomment
  `WallSegments.cs`, `CopPlacement.cs` links)

- [ ] **Step 1: Write the failing test**

Create `CanvasCovers.Tests/WallModelTests.cs`:

```csharp
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class WallModelTests
    {
        [TestMethod]
        public void WallSegments_TotalWidth_Sums_All_Boxes()
        {
            // Sheet job 12346 left wall bottom row: 250 350 240 1400 0
            var s = new WallSegments
            {
                DoorReturnLeft = 250,
                Seg1 = 350,
                Seg2 = 240,
                Seg3 = 1400,
                DoorReturnRight = 0,
            };
            Assert.AreEqual(2240.0, s.TotalWidth);
        }

        [TestMethod]
        public void CopPlacement_Defaults_Are_Off()
        {
            var cop = new CopPlacement();
            Assert.IsFalse(cop.Enabled);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: compile error — `WallSegments` / `CopPlacement` do not exist.

- [ ] **Step 3: Implement WallSegments**

Create `CanvasCovers/Models/Products/LiftBlanket/WallSegments.cs`:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The bottom-row measurement boxes for one wall, mirroring the paper
    // form: an optional door-return tab at each outer corner ("place zero
    // if not needed") plus up to three interior fold/measure segments.
    // The wall's cut width is the sum of all five. They do NOT add steps
    // to the cut rectangle — the cut piece is a plain rectangle of the
    // summed width; the segments are measure/fold references that drive
    // COP horizontal positioning and (future) fold lines.
    public class WallSegments
    {
        public double DoorReturnLeft { get; set; }

        public double Seg1 { get; set; }

        public double Seg2 { get; set; }

        public double Seg3 { get; set; }

        public double DoorReturnRight { get; set; }

        public double TotalWidth =>
            DoorReturnLeft + Seg1 + Seg2 + Seg3 + DoorReturnRight;
    }
}
```

- [ ] **Step 4: Implement CopPlacement**

Create `CanvasCovers/Models/Products/LiftBlanket/CopPlacement.cs`:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // Per-wall COP (cut-out panel) inputs, taken straight off the
    // measurement sheet. The operator types width, height, the gap from
    // the wall bottom to the COP bottom, and a horizontal offset from the
    // wall's left edge. The COP is placed in the bottom (measured) half of
    // the doubled cut piece — the calculator handles the fold.
    public class CopPlacement
    {
        public bool Enabled { get; set; }

        public double Width { get; set; } = 240;

        public double Height { get; set; } = 1300;

        // Distance from the wall's bottom edge to the COP's bottom edge.
        public double GapFromBottom { get; set; } = 600;

        // Distance from the wall's left edge to the COP's left edge.
        public double OffsetFromLeft { get; set; } = 600;
    }
}
```

- [ ] **Step 5: Extend LiftBlanketOptions**

Modify `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs` to:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    public enum FixingType
    {
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
    }

    public class LiftBlanketOptions
    {
        public bool ThroughCar { get; set; }

        public bool PlasticCoverOnCop { get; set; }

        public FixingType Fixings { get; set; } = FixingType.HooksFacingOut;

        // The mm subtracted from each wall's measured height before the
        // ×2 doubling. Defaults to the fixing type's standard allowance
        // (see FixingAllowance) but the operator can override per job —
        // the sheet's "height from top of hook ... to bottom of blanket"
        // note means this is a real per-job value.
        public double FixingAllowanceMm { get; set; } = 50;

        // Reserved for a later round. The reference DXFs carry quilt lines
        // but the measurement sheet has no quilt input and the spacing rule
        // is unconfirmed, so generation is gated off for now.
        public bool QuiltingEnabled { get; set; }
    }
}
```

- [ ] **Step 6: Uncomment Compile links, run tests**

Uncomment `WallSegments.cs` and `CopPlacement.cs` in the test csproj.
Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 6 passed.

- [ ] **Step 7: Commit**

```powershell
git add CanvasCovers
git commit -m "feat: segment + COP-placement models, editable fixing allowance"
```

---

## Task 4: Rewrite WallDimensions onto the new model

**Files:**
- Modify: `CanvasCovers/Models/Products/LiftBlanket/WallDimensions.cs`
- Test: `CanvasCovers.Tests/WallModelTests.cs` (extend)

WallDimensions currently exposes `MainWidth`, `MainHeight`, `DoorReturn1/2/3`,
`Cop*`. Replace with the new composition. The generator and dialog (later
tasks) are updated to match; this task just changes the model + its tests.

- [ ] **Step 1: Add failing test for the new shape**

Append to `CanvasCovers.Tests/WallModelTests.cs` inside the class:

```csharp
        [TestMethod]
        public void WallDimensions_Width_Comes_From_Segments()
        {
            var wall = new WallDimensions();
            wall.Segments.DoorReturnLeft = 250;
            wall.Segments.Seg1 = 350;
            wall.Segments.Seg2 = 240;
            wall.Segments.Seg3 = 1400;
            wall.MeasuredHeight = 2200;
            Assert.AreEqual(2240.0, wall.Width);
            Assert.AreEqual(2200.0, wall.MeasuredHeight);
            Assert.IsTrue(wall.Enabled);
            Assert.IsFalse(wall.Cop.Enabled);
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: compile error — `Segments`, `Width`, `MeasuredHeight`, `Cop` not
on `WallDimensions`.

- [ ] **Step 3: Rewrite WallDimensions**

Replace the entire contents of
`CanvasCovers/Models/Products/LiftBlanket/WallDimensions.cs` with:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // One lift-blanket wall as the operator measures it on the paper form.
    // Width is the sum of the bottom-row segments; MeasuredHeight is the
    // raw "top of hook to bottom of blanket" number BEFORE the fixing
    // allowance is subtracted and BEFORE the ×2 doubling — the calculator
    // applies both. COP is optional and per-wall.
    public class WallDimensions
    {
        public bool Enabled { get; set; } = true;

        public WallSegments Segments { get; set; } = new WallSegments();

        public double MeasuredHeight { get; set; } = 2200;

        public CopPlacement Cop { get; set; } = new CopPlacement();

        // Cut width equals the summed segments (no allowance on width here;
        // the +10mm "WIDTH - ADD 10mm" rule is applied by the calculator).
        public double Width => Segments.TotalWidth;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 7 passed.

> The add-in project (`CanvasCovers.csproj`) will NOT compile yet — the
> generator and dialog still reference the old fields. That's fixed in
> Tasks 6–8. Do not build the add-in project in this task.

- [ ] **Step 5: Commit**

```powershell
git add CanvasCovers
git commit -m "refactor: WallDimensions onto segments + measured height + COP"
```

---

## Task 5: LiftBlanketCalculator — the math core (headless)

**Files:**
- Create: `CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs`
- Create: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`
- Test: `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (uncomment
  `WallLayout.cs`, `LiftBlanketCalculator.cs` links)

The calculator converts a `LiftBlanketJob` into world-coordinate primitives.
It is the single source of the doubling/fold/COP-placement rules and is
fully unit-tested. The generator (Task 6) only walks its output.

- [ ] **Step 1: Write the DTOs**

Create `CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs`:

```csharp
using System.Collections.Generic;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Axis-aligned rectangle in world (drawing) coordinates, given by its
    // lower-left and upper-right corners.
    public struct RectSpec
    {
        public double X0;
        public double Y0;
        public double X1;
        public double Y1;

        public RectSpec(double x0, double y0, double x1, double y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }
    }

    // A dimension to emit: two extension points and a point on the dim line.
    public struct DimSpec
    {
        public double Ext1X, Ext1Y, Ext2X, Ext2Y, LineX, LineY;
    }

    // A single-line text label to emit at a baseline point.
    public struct LabelSpec
    {
        public double X, Y, Height;
        public string Text;
    }

    // Everything the generator needs to draw one wall.
    public class WallLayout
    {
        // The cut rectangle (full doubled height) on the cut layer.
        public RectSpec CutRect;

        // The fold midline Y (where the bottom panel mirrors). Informational
        // for now; quilting will use it later.
        public double FoldMidlineY;

        // The COP rectangle in the bottom half, or null when no COP.
        public RectSpec? CopRect;

        // Wall identifier label ("<net> <name> L"), on the draw layer.
        public LabelSpec? IdentifierLabel;

        // Width + height dimensions for this wall.
        public List<DimSpec> Dimensions = new List<DimSpec>();
    }
}
```

- [ ] **Step 2: Write failing tests**

Create `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs`:

```csharp
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class LiftBlanketCalculatorTests
    {
        private static WallDimensions Job12346LeftWall()
        {
            var wall = new WallDimensions();
            wall.Segments.DoorReturnLeft = 250;
            wall.Segments.Seg1 = 350;
            wall.Segments.Seg2 = 240;
            wall.Segments.Seg3 = 1400;
            wall.Segments.DoorReturnRight = 0;
            wall.MeasuredHeight = 2200;
            wall.Cop.Enabled = true;
            wall.Cop.Width = 240;
            wall.Cop.Height = 1300;
            wall.Cop.GapFromBottom = 600;
            wall.Cop.OffsetFromLeft = 600;
            return wall;
        }

        [TestMethod]
        public void CutHeight_Is_Measured_Less_Allowance_Doubled()
        {
            // (2200 - 50) * 2 = 4300, matching the reference DXF.
            double half = LiftBlanketCalculator.HalfHeight(2200, 50);
            Assert.AreEqual(2150.0, half);
            Assert.AreEqual(4300.0, LiftBlanketCalculator.CutHeight(2200, 50));
        }

        [TestMethod]
        public void CutWidth_Adds_Ten_Millimetres()
        {
            // "WIDTH - ADD 10mm" — summed segments 2240 -> cut width 2250.
            Assert.AreEqual(2250.0, LiftBlanketCalculator.CutWidth(2240));
        }

        [TestMethod]
        public void WallLayout_CutRect_Matches_Derived_Dims()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.AreEqual(0.0, layout.CutRect.X0, 0.001);
            Assert.AreEqual(0.0, layout.CutRect.Y0, 0.001);
            Assert.AreEqual(2250.0, layout.CutRect.X1, 0.001);   // 2240 + 10
            Assert.AreEqual(4300.0, layout.CutRect.Y1, 0.001);   // (2200-50)*2
            Assert.AreEqual(2150.0, layout.FoldMidlineY, 0.001); // bottom half
        }

        [TestMethod]
        public void Cop_Sits_In_Bottom_Half_From_Sheet_Numbers()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.IsTrue(layout.CopRect.HasValue);
            RectSpec cop = layout.CopRect.Value;
            Assert.AreEqual(600.0, cop.X0, 0.001);    // offset from left
            Assert.AreEqual(840.0, cop.X1, 0.001);    // +240 width
            Assert.AreEqual(600.0, cop.Y0, 0.001);    // gap from bottom
            Assert.AreEqual(1900.0, cop.Y1, 0.001);   // +1300 height
            // entirely below the fold midline (2150)
            Assert.IsTrue(cop.Y1 <= layout.FoldMidlineY);
        }

        [TestMethod]
        public void No_Cop_When_Disabled()
        {
            var wall = Job12346LeftWall();
            wall.Cop.Enabled = false;
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.IsFalse(layout.CopRect.HasValue);
        }

        [TestMethod]
        public void IdentifierLabel_Concatenates_Project_And_Suffix()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.IsTrue(layout.IdentifierLabel.HasValue);
            Assert.AreEqual("12346 TEST 12346 L", layout.IdentifierLabel.Value.Text);
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: compile error — `LiftBlanketCalculator` does not exist.

- [ ] **Step 4: Implement the calculator**

Create `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`:

```csharp
using System.Collections.Generic;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Pure, SDK-free geometry math for a lift blanket. Encapsulates the
    // three rules recovered from the client's reference DXFs:
    //   1. cutHeight = (measuredHeight - fixingAllowance) * 2
    //   2. cutWidth  = summedSegments + 10   ("WIDTH - ADD 10mm")
    //   3. COP is placed in the bottom (measured) half from the operator's
    //      gap-from-bottom / offset-from-left / size numbers; the top half
    //      is a mirror, so no COP geometry is needed above the fold midline.
    // No DraftSight types here so it can be unit-tested headlessly.
    public class LiftBlanketCalculator
    {
        private const double WidthAllowanceMm = 10.0;
        private const double IdentifierTextHeight = 20.0;

        private readonly double _fixingAllowanceMm;

        public LiftBlanketCalculator(double fixingAllowanceMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
        }

        public static double HalfHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return measuredHeight - fixingAllowanceMm;
        }

        public static double CutHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return HalfHeight(measuredHeight, fixingAllowanceMm) * 2.0;
        }

        public static double CutWidth(double summedSegments)
        {
            return summedSegments + WidthAllowanceMm;
        }

        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix)
        {
            double cutWidth = CutWidth(wall.Width);
            double half = HalfHeight(wall.MeasuredHeight, _fixingAllowanceMm);
            double cutHeight = half * 2.0;

            var layout = new WallLayout
            {
                CutRect = new RectSpec(originX, 0, originX + cutWidth, cutHeight),
                FoldMidlineY = half,
                Dimensions = new List<DimSpec>(),
            };

            if (wall.Cop.Enabled)
            {
                double copX0 = originX + wall.Cop.OffsetFromLeft;
                double copY0 = wall.Cop.GapFromBottom;
                layout.CopRect = new RectSpec(
                    copX0,
                    copY0,
                    copX0 + wall.Cop.Width,
                    copY0 + wall.Cop.Height);
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                layout.IdentifierLabel = new LabelSpec
                {
                    X = originX + cutWidth / 2.0,
                    Y = cutHeight / 2.0,
                    Height = IdentifierTextHeight,
                    Text = (string.IsNullOrEmpty(projectTag) ? "" : projectTag + " ") + suffix,
                };
            }

            // Width dim below the wall; height dim added by the generator for
            // the leftmost wall only (it owns cross-wall layout). We emit the
            // per-wall width dim here so the spec stays self-contained.
            layout.Dimensions.Add(new DimSpec
            {
                Ext1X = originX, Ext1Y = 0,
                Ext2X = originX + cutWidth, Ext2Y = 0,
                LineX = originX + cutWidth / 2.0, LineY = -300,
            });

            return layout;
        }
    }
}
```

- [ ] **Step 5: Uncomment Compile links, run tests**

Uncomment `WallLayout.cs` and `LiftBlanketCalculator.cs` in the test csproj.
Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 13 passed.

- [ ] **Step 6: Commit**

```powershell
git add CanvasCovers
git commit -m "feat: headless LiftBlanketCalculator (doubling, fold, COP) + tests"
```

---

## Task 6: Rewrite LiftBlanketGenerator onto the calculator

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs`

The generator becomes a thin walker over `WallLayout`. It keeps the existing
SDK-safe patterns (activate-based layers, undo record, `InsertAlignedDimension`,
`InsertSimpleNote`, DIMSCALE bump — all per CLAUDE.md §9). No headless logic
lives here; it only translates DTOs into SDK calls.

- [ ] **Step 1: Rewrite the generator**

Replace the contents of
`CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` with the
implementation below. Key changes from the old file:
- Walls drawn L → R → B(ack), each as a single cut rectangle.
- COP rectangle + identifier label on the **draw** layer (`5 Draw and Text`),
  not the cut layer.
- Per-wall width dims + a single leftmost height dim.
- Reuse `DrawProjectAnnotations` / `DrawTopLegend` from the old file
  verbatim (they already match the reference DXFs); only the wall-drawing
  half changes.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Walks the WallLayout DTOs produced by LiftBlanketCalculator and emits
    // DraftSight entities. All geometry math lives in the calculator; this
    // class only translates specs into SDK calls, keeping the SDK-risky
    // surface minimal. SDK gotchas (activate-based layers, aligned dims,
    // simple notes, DIMSCALE) follow CLAUDE.md §9.
    public class LiftBlanketGenerator
    {
        private const double WallGap = 715;             // gap between walls (matches reference DXFs)
        private const double DimGap = 300;
        private const double TopLegendGap = 600;
        private const double ProjectInfoLeftGap = 800;
        private const double ProjectRowH = 200;
        private const double ProjectTextHeight = 160;
        private const double TopLegendTextHeight = 180;
        private const double IdentifierTextHeight = 20;
        private const string DimScaleValue = "30";

        private readonly DsApplication _application;
        private LayerSettings _layerSettings;

        public LiftBlanketGenerator(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void Generate(LiftBlanketJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            _layerSettings = job.Layers ?? new LayerSettings();

            Document document = _application.GetActiveDocument();
            if (document == null)
            {
                throw new InvalidOperationException(
                    "No active drawing is open. Create or open a drawing before generating.");
            }

            Model model = document.GetModel();
            if (model == null) throw new InvalidOperationException("DraftSight did not return a model.");
            SketchManager sketch = model.GetSketchManager();
            if (sketch == null) throw new InvalidOperationException("DraftSight did not return a sketch manager.");

            string projectTag = BuildProjectTag(job.Project);
            var calc = new LiftBlanketCalculator(job.Options.FixingAllowanceMm);

            using (LayerHelper layers = new LayerHelper(document))
            {
                layers.EnsureLayer(_layerSettings.Outline.Name, _layerSettings.Outline.ColorIndex);
                layers.EnsureLayer(_layerSettings.Cop.Name, _layerSettings.Cop.ColorIndex);
                layers.EnsureLayer(_layerSettings.Annotation.Name, _layerSettings.Annotation.ColorIndex);
                layers.EnsureLayer(_layerSettings.Titleblock.Name, _layerSettings.Titleblock.ColorIndex);

                try { _application.RunCommand("DIMSCALE\n" + DimScaleValue + "\n", true); }
                catch { /* non-fatal */ }

                sketch.StartUndoRecord();
                try
                {
                    double cursorX = 0;
                    double maxRight = 0;
                    double maxTop = 0;
                    bool isFirstWall = true;

                    foreach (var (wall, suffix) in EnumerateWalls(job))
                    {
                        WallLayout layout = calc.LayoutWall(wall, cursorX, projectTag, suffix);
                        DrawWall(sketch, layers, layout, isFirstWall);
                        double cutWidth = layout.CutRect.X1 - layout.CutRect.X0;
                        double cutHeight = layout.CutRect.Y1 - layout.CutRect.Y0;
                        cursorX += cutWidth + WallGap;
                        maxRight = cursorX - WallGap;
                        if (cutHeight > maxTop) maxTop = cutHeight;
                        isFirstWall = false;
                    }

                    if (maxRight > 0)
                    {
                        DrawTopLegend(sketch, layers, 0, maxTop + TopLegendGap);
                        DrawProjectAnnotations(sketch, layers, job, maxRight + ProjectInfoLeftGap, maxTop);
                    }
                }
                finally
                {
                    sketch.StopUndoRecord();
                }
            }
        }

        // L, then R, then B (back) unless Through Car omits the back wall.
        private static IEnumerable<(WallDimensions wall, string suffix)> EnumerateWalls(LiftBlanketJob job)
        {
            if (job.LeftWall != null && job.LeftWall.Enabled)
                yield return (job.LeftWall, "L");
            if (job.RightWall != null && job.RightWall.Enabled)
                yield return (job.RightWall, "R");
            if (job.RearWall != null && job.RearWall.Enabled && !job.Options.ThroughCar)
                yield return (job.RearWall, "B");
        }

        private void DrawWall(SketchManager sketch, LayerHelper layers, WallLayout layout, bool isLeftmost)
        {
            // Cut rectangle on the cut/outline layer.
            layers.Activate(_layerSettings.Outline.Name);
            RectSpec r = layout.CutRect;
            sketch.InsertPolyline2D(
                new[] { r.X0, r.Y0, r.X1, r.Y0, r.X1, r.Y1, r.X0, r.Y1 }, true);

            // COP rectangle on the draw layer (5 Draw and Text), not the cut
            // layer — it's a draw/score feature, per the reference DXFs.
            if (layout.CopRect.HasValue)
            {
                layers.Activate(_layerSettings.Cop.Name);
                RectSpec c = layout.CopRect.Value;
                sketch.InsertPolyline2D(
                    new[] { c.X0, c.Y0, c.X1, c.Y0, c.X1, c.Y1, c.X0, c.Y1 }, true);
            }

            // Wall identifier label on the annotation (draw) layer.
            if (layout.IdentifierLabel.HasValue)
            {
                layers.Activate(_layerSettings.Annotation.Name);
                LabelSpec lab = layout.IdentifierLabel.Value;
                SimpleNote note = sketch.InsertSimpleNote(lab.X, lab.Y, 0, lab.Height, 0.0, lab.Text);
                if (note != null) note.Justify = dsTextJustification_e.dsTextJustification_Middle;
            }

            // Dimensions on the titleblock layer.
            layers.Activate(_layerSettings.Titleblock.Name);
            foreach (DimSpec d in layout.Dimensions)
            {
                InsertDim(sketch, d);
            }
            if (isLeftmost)
            {
                // Height dim on the leftmost wall's outer (left) side.
                InsertDim(sketch, new DimSpec
                {
                    Ext1X = r.X0, Ext1Y = r.Y0,
                    Ext2X = r.X0, Ext2Y = r.Y1,
                    LineX = r.X0 - DimGap, LineY = (r.Y0 + r.Y1) / 2.0,
                });
            }
        }

        private static void InsertDim(SketchManager sketch, DimSpec d)
        {
            double dx = d.Ext2X - d.Ext1X;
            double dy = d.Ext2Y - d.Ext1Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;
            sketch.InsertAlignedDimension(
                new[] { d.Ext1X, d.Ext1Y, 0.0 },
                new[] { d.Ext2X, d.Ext2Y, 0.0 },
                new[] { d.LineX, d.LineY, 0.0 },
                string.Empty);
        }

        private static string BuildProjectTag(ProjectMetadata project)
        {
            if (project == null) return string.Empty;
            string net = (project.NetworkNumber ?? "").Trim();
            string name = (project.ProjectName ?? "").Trim();
            // Reference DXFs label walls "<net> <name>" e.g. "12346 TEST 12346".
            string combined = (net + " " + name).Trim();
            return combined;
        }

        // ---- Annotation/legend text: copied verbatim from the prior generator
        //      (already matches the reference DXFs). ----

        private void DrawTopLegend(SketchManager sketch, LayerHelper layers, double originX, double baselineY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);
            sketch.InsertSimpleNote(originX, baselineY, 0, TopLegendTextHeight, 0.0,
                "HEIGHT / WIDTH / RETURNS / V QUILT / H QUILT / COP / TEXT / INFO / STENCIL / SCALE / OTHER");
        }

        private void DrawProjectAnnotations(SketchManager sketch, LayerHelper layers, LiftBlanketJob job, double originX, double topY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);

            string dateStr = job.Project.Date.HasValue
                ? job.Project.Date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "—";

            List<string> lines = new List<string>
            {
                "COMPANY - " + Display(job.Project.CompanyName),
                "PROJECT NAME - " + Display(job.Project.ProjectName),
                "NETWORK NO - " + Display(job.Project.NetworkNumber),
                "O / NO - " + Display(job.Project.OrderNumber),
                "SALES CONTACT - " + Display(job.Project.SalesContact),
                "MOBILE - " + Display(job.Project.Mobile),
                "MEASURED BY - " + Display(job.Project.MeasuredBy),
                "DATE - " + dateStr,
                string.Empty,
                "FIXINGS REQUIRED - " + FixingLabel(job.Options.Fixings).ToUpperInvariant(),
                "FIXING ALLOWANCE - -" + job.Options.FixingAllowanceMm.ToString(CultureInfo.InvariantCulture),
                "THROUGH CAR - " + YesNo(job.Options.ThroughCar),
                "PLASTIC COVER ON COP - " + YesNo(job.Options.PlasticCoverOnCop),
            };

            string notes = (job.Project.Notes ?? string.Empty).Trim();
            if (notes.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("NOTES");
                lines.Add(notes);
            }

            lines.Add(string.Empty);
            lines.Add("FIXINGS");
            lines.Add("HOOKS       = -50");
            lines.Add("PRESS STUDS = -40");
            lines.Add("EYELET TG9  = -30");
            lines.Add("EYELET TG7  = -30");
            lines.Add("VELCRO      =   0");
            lines.Add(string.Empty);
            lines.Add("WIDTH  - ADD 10mm");
            lines.Add("HEIGHT = LESS FIXING THEN x2");

            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i];
                if (string.IsNullOrEmpty(text)) continue;
                double rowTopY = topY - i * ProjectRowH;
                double baseline = rowTopY - ProjectRowH + (ProjectRowH - ProjectTextHeight) / 2.0;
                sketch.InsertSimpleNote(originX, baseline, 0, ProjectTextHeight, 0.0, text);
            }
        }

        private static string Display(string value) => string.IsNullOrEmpty(value) ? "—" : value;
        private static string YesNo(bool flag) => flag ? "YES" : "NO";

        private static string FixingLabel(FixingType fixing)
        {
            switch (fixing)
            {
                case FixingType.Velcro: return "Velcro";
                case FixingType.HooksFacingIn: return "Hooks Facing In";
                case FixingType.HooksFacingOut: return "Hooks Facing Out";
                case FixingType.PressStuds: return "Press Studs";
                default: return fixing.ToString();
            }
        }
    }
}
```

> Removed: the vertical-quilting-spacing reference block (it was guesswork
> and quilting is now explicitly deferred). The FIXINGS table + formula
> reminders stay — they match the reference DXFs.

- [ ] **Step 2: Verify the add-in compiles**

The dialog still references old field names, so a full build fails here.
Compile just the geometry/models to catch generator errors early:

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: errors ONLY in `LiftBlanketWindow.xaml.cs` / `WallDiagram.xaml.cs`
(old field references). No errors inside `LiftBlanketGenerator.cs`,
`LiftBlanketCalculator.cs`, or the models. If the generator itself has
errors, fix them now.

- [ ] **Step 3: Commit**

```powershell
git add CanvasCovers
git commit -m "refactor: generator walks WallLayout; COP on draw layer; L/R/B walls"
```

---

## Task 7: Reshape the dialog to the measurement sheet

**Files:**
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml`
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`

Replace each wall's "Main Width / Main Height / DoorReturn1-3 / COP top
offset" grid with: a **segment row** (DR-left, Seg1, Seg2, Seg3, DR-right),
a **Measured Height** field, and a **COP** sub-grid (enabled checkbox +
width + height + gap-from-bottom + offset-from-left). Add a **Fixing
Allowance (mm)** field to OPTIONS and an **"Export DXF after generate"**
checkbox.

- [ ] **Step 1: Replace the Left Wall section grid in the XAML**

In `LiftBlanketWindow.xaml`, replace the inner `<Grid>` of the Left Wall
`Border` (currently lines ~127–176, the one with `LeftMainWidth` etc.) with:

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <!-- Bottom-row segments: DR-left | Seg1 | Seg2 | Seg3 | DR-right -->
    <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Segments (mm)" />
    <TextBox Grid.Row="0" Grid.Column="1" Name="LeftDrLeft"  Text="0"    Tag="DrLeft"  GotFocus="DimField_GotFocus" />
    <TextBox Grid.Row="0" Grid.Column="2" Name="LeftSeg1"    Text="0"    Tag="Seg1"    GotFocus="DimField_GotFocus" />
    <TextBox Grid.Row="0" Grid.Column="3" Name="LeftSeg2"    Text="0"    Tag="Seg2"    GotFocus="DimField_GotFocus" />
    <TextBox Grid.Row="0" Grid.Column="4" Name="LeftSeg3"    Text="1400" Tag="Seg3"    GotFocus="DimField_GotFocus" />
    <TextBox Grid.Row="0" Grid.Column="5" Name="LeftDrRight" Text="0"    Tag="DrRight" GotFocus="DimField_GotFocus" />

    <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Measured Height" />
    <TextBox Grid.Row="1" Grid.Column="1" Name="LeftMeasuredHeight" Text="2200" Tag="MeasuredHeight" GotFocus="DimField_GotFocus" />
    <CheckBox Grid.Row="1" Grid.Column="3" Grid.ColumnSpan="4" Name="LeftCopEnabled" Content="Include COP cutout" />

    <Grid Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="7" Margin="0,8,0,0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="80" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="80" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="80" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="80" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0" Style="{StaticResource FieldLabel}" Text="COP W" />
        <TextBox Grid.Column="1" Name="LeftCopWidth"  Text="240"  Tag="CopWidth"  GotFocus="DimField_GotFocus" />
        <TextBlock Grid.Column="2" Style="{StaticResource FieldLabel}" Text="COP H" />
        <TextBox Grid.Column="3" Name="LeftCopHeight" Text="1300" Tag="CopHeight" GotFocus="DimField_GotFocus" />
        <TextBlock Grid.Column="4" Style="{StaticResource FieldLabel}" Text="From Bottom" />
        <TextBox Grid.Column="5" Name="LeftCopGapBottom" Text="600" Tag="CopGapBottom" GotFocus="DimField_GotFocus" />
        <TextBlock Grid.Column="6" Style="{StaticResource FieldLabel}" Text="From Left" />
        <TextBox Grid.Column="7" Name="LeftCopOffsetLeft" Text="600" Tag="CopOffsetLeft" GotFocus="DimField_GotFocus" />
    </Grid>
</Grid>
```

- [ ] **Step 2: Replace the Right Wall section grid identically**

Repeat Step 1's grid for the Right Wall `Border`, renaming every control
`Left*` → `Right*` (`RightDrLeft`, `RightSeg1`, `RightSeg2`, `RightSeg3`,
`RightDrRight`, `RightMeasuredHeight`, `RightCopEnabled`, `RightCopWidth`,
`RightCopHeight`, `RightCopGapBottom`, `RightCopOffsetLeft`). Keep the same
column/row layout and Tags.

- [ ] **Step 3: Update the Rear Wall section**

Replace the Rear Wall grid (currently `RearWidth` / `RearHeight`) with a
segment + measured-height row (rear walls have no COP per the sheet):

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="80" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Segments (mm)" />
    <TextBox Grid.Row="0" Grid.Column="1" Name="RearDrLeft"  Text="0"    Tag="DrLeft" />
    <TextBox Grid.Row="0" Grid.Column="2" Name="RearSeg1"    Text="0"    Tag="Seg1" />
    <TextBox Grid.Row="0" Grid.Column="3" Name="RearSeg2"    Text="0"    Tag="Seg2" />
    <TextBox Grid.Row="0" Grid.Column="4" Name="RearSeg3"    Text="1400" Tag="Seg3" />
    <TextBox Grid.Row="0" Grid.Column="5" Name="RearDrRight" Text="0"    Tag="DrRight" />
    <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Measured Height" />
    <TextBox Grid.Row="1" Grid.Column="1" Name="RearMeasuredHeight" Text="2200" Tag="MeasuredHeight" />
</Grid>
```

- [ ] **Step 4: Add fixing-allowance + DXF-export controls to OPTIONS**

In the OPTIONS `Border`, after the `FixingsInput` ComboBox grid, add:

```xml
<Grid Margin="0,8,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="160" />
        <ColumnDefinition Width="100" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Fixing Allowance (mm)" />
    <TextBox Grid.Column="1" Name="FixingAllowanceInput" Text="50" />
    <TextBlock Grid.Column="2" Style="{StaticResource MutedLabel}"
               Text="Subtracted from each wall's measured height before the height is doubled." />
</Grid>
<CheckBox Name="ExportDxfOption" Margin="0,8,0,0" IsChecked="True"
          Content="Open DXF export dialog after generating" />
```

- [ ] **Step 5: Update FixingsInput to default the allowance**

In `LiftBlanketWindow.xaml`, add `SelectionChanged="FixingsInput_SelectionChanged"`
to the `FixingsInput` ComboBox so picking a fixing type repopulates the
allowance default.

- [ ] **Step 6: Rewrite the code-behind read logic**

In `LiftBlanketWindow.xaml.cs`:

(a) Replace `ReadWall(...)` with:

```csharp
private WallDimensions ReadWall(
    CheckBox enabledBox,
    TextBox drLeft, TextBox seg1, TextBox seg2, TextBox seg3, TextBox drRight,
    TextBox measuredHeight,
    CheckBox copBox, TextBox copWidth, TextBox copHeight, TextBox copGapBottom, TextBox copOffsetLeft,
    string wallLabel,
    List<string> errors)
{
    WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };
    if (!wall.Enabled) return wall;

    wall.Segments.DoorReturnLeft  = ReadNonNegative(drLeft.Text,  wallLabel + " door return (left)",  errors);
    wall.Segments.Seg1            = ReadNonNegative(seg1.Text,    wallLabel + " segment 1",            errors);
    wall.Segments.Seg2            = ReadNonNegative(seg2.Text,    wallLabel + " segment 2",            errors);
    wall.Segments.Seg3            = ReadNonNegative(seg3.Text,    wallLabel + " segment 3",            errors);
    wall.Segments.DoorReturnRight = ReadNonNegative(drRight.Text, wallLabel + " door return (right)", errors);
    wall.MeasuredHeight           = ReadPositive(measuredHeight.Text, wallLabel + " measured height", errors);

    if (wall.Segments.TotalWidth <= 0)
        errors.Add(wallLabel + " needs at least one non-zero segment.");

    wall.Cop.Enabled = copBox.IsChecked == true;
    if (wall.Cop.Enabled)
    {
        wall.Cop.Width         = ReadPositive(copWidth.Text,    wallLabel + " COP width",  errors);
        wall.Cop.Height        = ReadPositive(copHeight.Text,   wallLabel + " COP height", errors);
        wall.Cop.GapFromBottom = ReadNonNegative(copGapBottom.Text,  wallLabel + " COP gap from bottom", errors);
        wall.Cop.OffsetFromLeft= ReadNonNegative(copOffsetLeft.Text, wallLabel + " COP offset from left", errors);

        if (wall.Width > 0 && wall.Cop.Width > 0 && wall.Cop.OffsetFromLeft + wall.Cop.Width > wall.Width)
            errors.Add(wallLabel + " COP offset + width exceeds the wall width.");
    }
    return wall;
}
```

(b) Replace `ReadRearWall(...)` with:

```csharp
private WallDimensions ReadRearWall(
    CheckBox enabledBox,
    TextBox drLeft, TextBox seg1, TextBox seg2, TextBox seg3, TextBox drRight,
    TextBox measuredHeight,
    List<string> errors)
{
    WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };
    if (!wall.Enabled) return wall;
    wall.Segments.DoorReturnLeft  = ReadNonNegative(drLeft.Text,  "Rear door return (left)",  errors);
    wall.Segments.Seg1            = ReadNonNegative(seg1.Text,    "Rear segment 1",            errors);
    wall.Segments.Seg2            = ReadNonNegative(seg2.Text,    "Rear segment 2",            errors);
    wall.Segments.Seg3            = ReadNonNegative(seg3.Text,    "Rear segment 3",            errors);
    wall.Segments.DoorReturnRight = ReadNonNegative(drRight.Text, "Rear door return (right)", errors);
    wall.MeasuredHeight           = ReadPositive(measuredHeight.Text, "Rear measured height", errors);
    wall.Cop.Enabled = false;
    if (wall.Segments.TotalWidth <= 0)
        errors.Add("Rear wall needs at least one non-zero segment.");
    return wall;
}
```

(c) Update the two `ReadWall` call sites + the `ReadRearWall` call site in
`GenerateButton_Click` to pass the new controls:

```csharp
WallDimensions left = ReadWall(
    LeftWallEnabled, LeftDrLeft, LeftSeg1, LeftSeg2, LeftSeg3, LeftDrRight,
    LeftMeasuredHeight,
    LeftCopEnabled, LeftCopWidth, LeftCopHeight, LeftCopGapBottom, LeftCopOffsetLeft,
    "Left wall", errors);

WallDimensions right = ReadWall(
    RightWallEnabled, RightDrLeft, RightSeg1, RightSeg2, RightSeg3, RightDrRight,
    RightMeasuredHeight,
    RightCopEnabled, RightCopWidth, RightCopHeight, RightCopGapBottom, RightCopOffsetLeft,
    "Right wall", errors);

WallDimensions rear = ReadRearWall(
    RearWallEnabled, RearDrLeft, RearSeg1, RearSeg2, RearSeg3, RearDrRight,
    RearMeasuredHeight, errors);
```

(d) Update `ReadOptions()` to read the allowance + export flag:

```csharp
private LiftBlanketOptions ReadOptions()
{
    FixingType fixing = FixingType.HooksFacingOut;
    ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
    string tag = selected?.Tag as string;
    if (!string.IsNullOrEmpty(tag) && Enum.TryParse(tag, out FixingType parsed)) fixing = parsed;

    double allowance = 50;
    double.TryParse(FixingAllowanceInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out allowance);

    return new LiftBlanketOptions
    {
        ThroughCar = ThroughCarOption.IsChecked == true,
        PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
        Fixings = fixing,
        FixingAllowanceMm = allowance,
    };
}
```

(e) Add the export-requested surface + fixing default handler:

```csharp
// True when the operator wants the DXF export dialog after a successful
// generate. Read by OpenCanvasCoversCommand.
public bool ExportDxfRequested => ExportDxfOption.IsChecked == true;

private void FixingsInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (FixingAllowanceInput == null) return; // fires during XAML init
    ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
    string tag = selected?.Tag as string;
    if (!string.IsNullOrEmpty(tag) && Enum.TryParse(tag, out FixingType parsed))
    {
        FixingAllowanceInput.Text =
            Geometry.Products.LiftBlanket.FixingAllowance
                .DefaultFor(parsed).ToString(CultureInfo.InvariantCulture);
    }
}
```

(f) Fix `LiftBlanketWindow_Loaded` — `LeftMainWidth` no longer exists.
Change the focus target to `LeftSeg3` (the main panel segment):

```csharp
LeftSeg3.Focus();
LeftSeg3.SelectAll();
Diagram.ShowWall(WallContext.Left);
```

Add `using System.Windows.Controls;` is already present; ensure
`SelectionChangedEventArgs` resolves (it's in `System.Windows.Controls`).

- [ ] **Step 7: Build the add-in**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: build succeeds, 0 errors. (WallDiagram may still reference old
keys — if it errors, fix in Task 8; if it only warns, proceed.)

If `WallDiagram.Highlight(key)` is called with keys that no longer exist,
that's fine at compile time (it takes a string); the visual just won't
highlight until Task 8.

- [ ] **Step 8: Commit**

```powershell
git add CanvasCovers
git commit -m "feat: dialog reshaped to measurement sheet (segments + per-wall COP + allowance + export toggle)"
```

---

## Task 8: Update WallDiagram to the new model

**Files:**
- Modify: `CanvasCovers/UI/Controls/WallDiagram.xaml.cs`

The diagram currently draws door-return steps. Update it to draw a plain
rectangle with a COP box and the new dimension highlight keys
(`DrLeft`, `Seg1`, `Seg2`, `Seg3`, `DrRight`, `MeasuredHeight`, `CopWidth`,
`CopHeight`, `CopGapBottom`, `CopOffsetLeft`).

- [ ] **Step 1: Read the current WallDiagram code-behind**

Run: open `CanvasCovers/UI/Controls/WallDiagram.xaml.cs` and identify the
`ShowWall` paint method and the `Highlight(string key)` method and the
dimension-key constants it uses.

- [ ] **Step 2: Replace the wall painting with the rectangle + COP model**

Rewrite `ShowWall` so it draws:
- A single outer rectangle (the wall outline).
- Tick marks / a segment strip along the bottom edge split into DR-left,
  Seg1, Seg2, Seg3, DR-right proportions (use placeholder proportions
  250/350/240/1400/0 normalised, purely illustrative).
- A COP rectangle inset from the bottom-left by illustrative proportions.
- Labelled dimension lines whose `Tag`/name matches the new keys so
  `Highlight(key)` can tint them.

Keep the existing canvas-sizing and brand-accent highlight mechanism;
only the shapes + keys change. (Diagram is illustrative, not to scale —
no need to feed it real values.)

- [ ] **Step 3: Update Highlight key constants**

Ensure `Highlight(key)` recognises the ten new keys above and tints the
matching shape; unknown keys are a no-op (current behaviour).

- [ ] **Step 4: Build**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add CanvasCovers
git commit -m "feat: WallDiagram shows rectangle + COP model with new highlight keys"
```

---

## Task 9: DXF export — native Save-As, default to network number

**Files:**
- Create: `CanvasCovers/IO/DxfExporter.Filename.cs` (headless filename logic)
- Create: `CanvasCovers/IO/DxfExporter.cs` (SDK call, partial class)
- Test: `CanvasCovers.Tests/DxfFilenameTests.cs`
- Modify: `CanvasCovers/Commands/OpenCanvasCoversCommand.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (uncomment
  `DxfExporter.Filename.cs` link)

Split the exporter so the filename rule is unit-testable without the SDK.
The `.cs` file holds the SDK call; the `.Filename.cs` partial holds the
pure default-filename derivation.

- [ ] **Step 1: Write the failing filename test**

Create `CanvasCovers.Tests/DxfFilenameTests.cs`:

```csharp
using CanvasCovers.IO;
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class DxfFilenameTests
    {
        [TestMethod]
        public void Uses_Network_Number_When_Present()
        {
            var meta = new ProjectMetadata { NetworkNumber = "12346" };
            Assert.AreEqual("12346.dxf", DxfExporter.DefaultFileName(meta));
        }

        [TestMethod]
        public void Falls_Back_When_Network_Number_Blank()
        {
            var meta = new ProjectMetadata { NetworkNumber = "" };
            string name = DxfExporter.DefaultFileName(meta);
            StringAssert.StartsWith(name, "CanvasCovers-");
            StringAssert.EndsWith(name, ".dxf");
        }

        [TestMethod]
        public void Strips_Invalid_Filename_Chars()
        {
            var meta = new ProjectMetadata { NetworkNumber = "12/3:46*" };
            Assert.AreEqual("12346.dxf", DxfExporter.DefaultFileName(meta));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: compile error — `DxfExporter` not found.

- [ ] **Step 3: Implement the headless filename half**

Create `CanvasCovers/IO/DxfExporter.Filename.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using CanvasCovers.Models;

namespace CanvasCovers.IO
{
    // Filename derivation, kept SDK-free so it can be unit-tested. The full
    // convention will be confirmed with the client later; for now the
    // network number is the filename. Falls back to a timestamp when blank.
    public partial class DxfExporter
    {
        public static string DefaultFileName(ProjectMetadata project)
        {
            string net = project?.NetworkNumber ?? string.Empty;
            string cleaned = new string(net.Where(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "CanvasCovers-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            }
            return cleaned + ".dxf";
        }
    }
}
```

- [ ] **Step 4: Uncomment the link, run tests**

Uncomment the `DxfExporter.Filename.cs` line in the test csproj.
Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: 16 passed.

- [ ] **Step 5: Verify the SDK SaveAs/Export signature**

Before writing the SDK half, confirm the actual export API on the interop.
Run:
```powershell
$asm = [Reflection.Assembly]::LoadFrom("C:\Program Files\Dassault Systemes\DraftSight\APISDK\tlb\DraftSight.Interop.dsAutomation.dll")
$asm.GetTypes() | Where-Object { $_.Name -eq 'Document' } | ForEach-Object {
  $_.GetMethods() | Where-Object { $_.Name -match 'Save|Export' } | ForEach-Object {
    $p = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
    "$($_.ReturnType.Name) $($_.Name)($p)"
  }
}
```
Expected: a `SaveAs` (and/or `ExportToFile`) overload. Note the exact name
and parameters; use them in Step 6. If only a path-based `SaveAs(string)`
exists (no native dialog), Step 6 falls back to a WPF `SaveFileDialog`
seeded with the default filename, then calls `SaveAs(chosenPath)`.

- [ ] **Step 6: Implement the SDK export half**

Create `CanvasCovers/IO/DxfExporter.cs` (adjust the SaveAs call to the
signature confirmed in Step 5):

```csharp
using System;
using CanvasCovers.Models;
using Microsoft.Win32;
using DsApplication = DraftSight.Interop.dsAutomation.Application;
using DraftSight.Interop.dsAutomation;

namespace CanvasCovers.IO
{
    // Pops a Save-As dialog seeded with the network-number default filename
    // and a .dxf filter, then asks DraftSight to write the active document
    // to the chosen path. Folder is the operator's choice (per client: no
    // fixed output folder). Best-effort: a cancelled dialog is a no-op.
    public partial class DxfExporter
    {
        private readonly DsApplication _application;

        public DxfExporter(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void Export(ProjectMetadata project)
        {
            Document document = _application.GetActiveDocument();
            if (document == null) return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = DefaultFileName(project),
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                Title = "Export Lift Blanket DXF",
                AddExtension = true,
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog() != true) return;

            // SaveAs signature confirmed via reflection in Step 5. The common
            // shape is SaveAs(string fileName) which infers DXF from the
            // extension. Adjust if the reflected overload differs.
            document.SaveAs(dialog.FileName);
        }
    }
}
```

- [ ] **Step 7: Wire export into the command**

In `CanvasCovers/Commands/OpenCanvasCoversCommand.cs`,
`LiftBlanketWindow_GenerateRequested`, after `generator.Generate(e.Job)`
succeeds, add (inside the `try`, after Generate):

```csharp
LiftBlanketWindow window = sender as LiftBlanketWindow;
if (window != null && window.ExportDxfRequested)
{
    new CanvasCovers.IO.DxfExporter(Application).Export(e.Job.Project);
}
```

- [ ] **Step 8: Build the add-in**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```powershell
git add CanvasCovers CanvasCovers.Tests
git commit -m "feat: DXF export via native Save-As, default filename = network number"
```

---

## Task 10: In-DraftSight verification + DXF round-trip

This is the real verification for the SDK-emission layer (no headless test
can cover it). Per CLAUDE.md §7 the install path is the Inno installer.

- [ ] **Step 1: Build the installer**

Close DraftSight, then run: `.\Installer\build.ps1`
Expected: `Installer\Output\BesiaCAD-CanvasCovers-Setup-<version>.exe` built.

- [ ] **Step 2: Install + load**

Run the EXE as admin. Open DraftSight → new drawing → Tools → Add-Ins →
tick CanvasCovers → ribbon "CanvasCovers" → "Canvas Covers" → "Lift Blanket".

- [ ] **Step 3: Reproduce job 12346**

Enter the sheet values:
- Left wall: segments `250 / 350 / 240 / 1400 / 0`, measured height `2200`,
  COP enabled W`240` H`1300` from-bottom`600` from-left`600`.
- Right wall: segments `0 / 350 / 240 / 1400 / 250` (mirror), height `2200`,
  COP off.
- Through Car OFF; rear wall segments to taste, height `2200`.
- Fixing: Hooks Facing Out (allowance auto-fills 50).
- Generate.

Expected: three blue cut rectangles ~4300 tall; left COP box on the draw
layer at the lower-left; dims legible; project text column on the right;
Ctrl+Z reverts in one step.

- [ ] **Step 4: Export + re-parse**

Tick "Open DXF export dialog after generating" (default on). Save as
`reference\dxf\_generated-12346.dxf`. Then:

Run: `.\reference\parse-dxf.ps1 -Path "reference\dxf\_generated-12346.dxf"`
Expected: left cut rect width ≈ 2250 (2240 + 10), height ≈ 4300; COP rect
on `5 Draw and Text` width 240 / height 1300, bottom 600 above the cut
bottom. Compare against the reference 12346 numbers in `DXF_FINDINGS.md`.

- [ ] **Step 5: Record results**

If the generated rectangle/COP coordinates match the reference within a few
mm, the geometry is correct. Note any deltas. (Exact X-origins differ — the
reference uses absolute offsets; ours starts at 0 — only the relative
sizes/COP placement must match.)

- [ ] **Step 6: Update docs**

Update `docs/STATUS.md`: move "wall geometry redesign" + "fixing-allowance
×2 math" + "DXF auto-export" out of "deliberately not yet built" into
"what works end-to-end". Note quilting is still pending a client rule.
Update `docs/ARCHITECTURE.md` to mention `LiftBlanketCalculator` +
`DxfExporter` + the test project. Bump `MyAppVersion` in
`Installer\CanvasCovers.iss` and `AssemblyInfo.cs`.

- [ ] **Step 7: Commit**

```powershell
git add docs Installer CanvasCovers
git commit -m "docs: lift-blanket geometry/math/export now working; version bump"
```

---

## Self-review notes

- **Spec coverage:** correct cut geometry (Tasks 4–6), auto fixing/×2 math
  (Tasks 2,5), DXF export (Task 9), quilting deferred behind a flag (Task 3,
  `QuiltingEnabled`) — all four user goals accounted for.
- **Type consistency:** `WallDimensions.Width`, `.MeasuredHeight`,
  `.Segments`, `.Cop` used identically across calculator, generator, dialog;
  `CopPlacement` fields (`Width/Height/GapFromBottom/OffsetFromLeft`) match
  between model, calculator, dialog read logic, and tests; `WallLayout`
  consumed only by the generator. `FixingAllowance.DefaultFor` used by tests
  + dialog. `DxfExporter.DefaultFileName` used by tests + the SDK half.
- **Headless boundary:** the test csproj links only POCO + math files; the
  generator + exporter SDK half are never linked into tests.
- **Risk flag:** Task 9 Step 5/6 — the exact SaveAs signature is verified by
  reflection before use, per CLAUDE.md §12 ("verify any DraftSight API
  signature before using it").
```
