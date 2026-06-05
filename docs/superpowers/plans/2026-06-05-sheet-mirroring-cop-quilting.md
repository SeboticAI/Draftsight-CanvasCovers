# Sheet-Mirroring COP + Quilting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the free-number COP model with a segment-driven COP, build quilting (vertical + horizontal lines, even-divided, unified edge allowance), and turn each wall's input into a true-proportion live blanket diagram arranged in tabs.

**Architecture:** Keep the existing headless-calculator / thin-SDK-generator / linked-test split (per `docs/superpowers/specs/2026-06-05-sheet-mirroring-cop-quilting-design.md`). All geometry math lands in `LiftBlanketCalculator` and is unit-tested headlessly against reference job 12346. The generator gains quilt-line emission only (no math). The WPF UI is reworked from stacked sections + passive preview into a tabbed, interactive, embedded-field diagram — verified live in DraftSight, not by unit tests.

**Tech Stack:** C# / .NET Framework 4.8, WPF, DraftSight COM interop, MSTest (headless, net48), Inno Setup. Build via `dotnet build -c Release`; tests via `dotnet test CanvasCovers.Tests`.

---

## Reference numbers (job 12346 Left wall) used throughout

- Bottom row: `DR-L 250 / S1 350 / S2 240 / S3 1400 / DR-R 0` → segment sum **2240**
- Measured height **2200**, fixing allowance **50** → fold midline **2150**, cut height **4300**
- Edge allowance **10** → half = **5** per side
- Cut width = 2240 + 10 = **2250**; cut rect spans X `0..2250`
- COP width = S2 = **240**; COP left edge (cut-rect X) = `5 + DR-L + S1` = `5 + 250 + 350` = **605**, right = **845**
- COP bottom gap **600**, COP height **1300** → COP spans Y `600..1900`
- Auto top gap = `foldMidline − bottomGap − copHeight` = `2150 − 600 − 1300` = **250**
- Quilt zone X (segment grid): left bound = `DR-L + half` = `250 + 5` = **255**, right bound = `cutWidth − DR-R − half` = `2250 − 0 − 5` = **2245**
- Quilt zone Y: bottom = `half` = **5**, top = fold midline = **2150**

---

## Phase 0: Branch + baseline

### Task 0: Create a working branch and confirm green baseline

**Files:** none (git + verification only)

- [ ] **Step 1: Create a feature branch off main**

```bash
cd "c:\Users\sebas\Desktop\AI Programs\SeboticAI Projects\Draftsight-CanvasCovers"
git checkout -b feat/sheet-mirroring-cop-quilting
```

- [ ] **Step 2: Confirm the build is clean**

Run: `dotnet build -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Confirm tests pass (baseline 21)**

Run: `dotnet test CanvasCovers.Tests`
Expected: `Passed!  - Failed: 0, Passed: 21`

> If either fails, STOP — the baseline must be green before changes.

---

## Phase 1: Models

### Task 1: Add edge allowance + quilting spacing to options; flip quilting on

**Files:**
- Modify: `CanvasCovers\Models\Products\LiftBlanket\LiftBlanketOptions.cs`

- [ ] **Step 1: Add the two new option fields and default quilting on**

Replace the `QuiltingEnabled` block at the end of the class with:

```csharp
        // The total mm added to a wall's cut width as a manufacturing edge
        // allowance. Split evenly: half on the left edge, half on the right,
        // and the same half is the inset of quilting from the bottom + side
        // outlines. Defaults to 10 (→ 5mm each side). Operator-editable; 0
        // means the cut equals the raw segment sum.
        public double EdgeAllowanceMm { get; set; } = 10;

        // Target gap between the horizontal quilt lines (running up the
        // height). The actual count is rounded so the lines divide the
        // quilted region evenly — see LiftBlanketCalculator.QuiltLines.
        // Vertical quilt lines even-divide the bounded width to a similar gap.
        public double VerticalQuiltingSpacingMm { get; set; } = 700;

        // Quilting is now built. On by default; the operator can disable it.
        public bool QuiltingEnabled { get; set; } = true;
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs
git commit -m "feat: edge-allowance + quilting-spacing options; quilting on by default"
```

---

### Task 2: Reshape CopPlacement to drop derived fields

The COP width and horizontal offset are now derived from the segments, not
stored. Only `Enabled`, `Height` (COP height), and `GapFromBottom` (bottom
gap) remain as inputs.

**Files:**
- Modify: `CanvasCovers\Models\Products\LiftBlanket\CopPlacement.cs`
- Modify: `CanvasCovers.Tests\WallModelTests.cs` (CopPlacement defaults test still valid; no change needed but verify)

- [ ] **Step 1: Replace CopPlacement with the reduced model**

Replace the whole file body (keep the namespace) with:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // Per-wall COP (cut-out panel) VERTICAL inputs. The horizontal geometry
    // is derived from the bottom-row segments (width = Seg2, left edge =
    // edgeAllowance/2 + DoorReturnLeft + Seg1), so only the vertical numbers
    // live here. The blanket folds at the midline (measuredHeight −
    // fixingAllowance); the COP sits in the bottom (measured) half. The top
    // gap is computed (foldMidline − GapFromBottom − Height), not stored.
    public class CopPlacement
    {
        public bool Enabled { get; set; }

        // COP height (the middle vertical box on the sheet, e.g. 1300).
        public double Height { get; set; } = 1300;

        // Distance from the wall's bottom edge to the COP's bottom edge
        // (the bottom vertical box, e.g. 600).
        public double GapFromBottom { get; set; } = 600;
    }
}
```

- [ ] **Step 2: Build — expect compile errors in the calculator/generator/UI that reference the removed fields**

Run: `dotnet build -c Release`
Expected: FAIL — errors referencing `Cop.Width` and `Cop.OffsetFromLeft` in
`LiftBlanketCalculator.cs`, `LiftBlanketWindow.xaml.cs`, and the test file.
This is expected; later tasks fix each call site. Note the error list.

- [ ] **Step 3: Commit the model change alone**

```bash
git add CanvasCovers/Models/Products/LiftBlanket/CopPlacement.cs
git commit -m "refactor: COP model keeps only vertical inputs (width/offset now derived)"
```

> The build is intentionally red after this commit. Phase 2 makes it green
> again by updating the calculator and its tests together.

---

## Phase 2: Calculator — segment-driven COP + parameterised allowance (TDD)

### Task 3: Update calculator tests for the new COP + allowance model

We rewrite the tests first (they describe the new contract), watch them fail
to compile/pass, then change the calculator to satisfy them.

**Files:**
- Modify: `CanvasCovers.Tests\LiftBlanketCalculatorTests.cs`

- [ ] **Step 1: Replace the test helper and the COP/width tests**

Replace `Job12346LeftWall()` and the existing `CutWidth_Adds_Ten_Millimetres`,
`Cop_Sits_In_Bottom_Half_From_Sheet_Numbers`, `Layout_Offsets_All_X_By_OriginX`,
`Width_Dimension_Extension_Points_On_Bottom_Edge`, `Cop_Fits_*` tests with:

```csharp
        private static WallDimensions Job12346LeftWall()
        {
            var wall = new WallDimensions();
            wall.Segments.DoorReturnLeft = 250;
            wall.Segments.Seg1 = 350;
            wall.Segments.Seg2 = 240;   // = COP width
            wall.Segments.Seg3 = 1400;
            wall.Segments.DoorReturnRight = 0;
            wall.MeasuredHeight = 2200;
            wall.Cop.Enabled = true;
            wall.Cop.Height = 1300;
            wall.Cop.GapFromBottom = 600;
            return wall;
        }

        [TestMethod]
        public void CutWidth_Adds_The_Edge_Allowance()
        {
            // Allowance is now a parameter (default 10), not a constant.
            Assert.AreEqual(2250.0, LiftBlanketCalculator.CutWidth(2240, 10));
            Assert.AreEqual(2240.0, LiftBlanketCalculator.CutWidth(2240, 0));
            Assert.AreEqual(2252.0, LiftBlanketCalculator.CutWidth(2240, 12));
        }

        [TestMethod]
        public void AutoTopGap_Is_Fold_Less_Bottom_Less_CopHeight()
        {
            // foldMidline 2150 − bottom 600 − copHeight 1300 = 250 (matches DXF).
            Assert.AreEqual(250.0,
                LiftBlanketCalculator.AutoTopGap(
                    measuredHeight: 2200, fixingAllowanceMm: 50,
                    copGapFromBottom: 600, copHeight: 1300),
                0.001);
        }

        [TestMethod]
        public void AutoTopGap_Goes_Negative_When_Cop_Crosses_Fold()
        {
            // bottom 600 + height 1700 = 2300 > fold 2150 → top gap −150.
            Assert.IsTrue(
                LiftBlanketCalculator.AutoTopGap(2200, 50, 600, 1700) < 0);
        }

        [TestMethod]
        public void Cop_Width_Is_Middle_Segment_And_Offset_From_Door_Return_Line()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.IsTrue(layout.CopRect.HasValue);
            RectSpec cop = layout.CopRect.Value;
            // left edge = half(5) + DR-L(250) + S1(350) = 605; width = S2 = 240.
            Assert.AreEqual(605.0, cop.X0, 0.001);
            Assert.AreEqual(845.0, cop.X1, 0.001);
            // vertical from sheet numbers: 600..1900, below fold 2150.
            Assert.AreEqual(600.0, cop.Y0, 0.001);
            Assert.AreEqual(1900.0, cop.Y1, 0.001);
            Assert.IsTrue(cop.Y1 <= layout.FoldMidlineY);
        }

        [TestMethod]
        public void CutRect_Grows_By_Edge_Allowance()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.AreEqual(0.0, layout.CutRect.X0, 0.001);
            Assert.AreEqual(2250.0, layout.CutRect.X1, 0.001);
            Assert.AreEqual(4300.0, layout.CutRect.Y1, 0.001);
            Assert.AreEqual(2150.0, layout.FoldMidlineY, 0.001);
        }

        [TestMethod]
        public void Layout_Offsets_Cop_X_By_OriginX()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 1000, "12346 TEST 12346", "L");
            Assert.AreEqual(1000.0, layout.CutRect.X0, 0.001);
            Assert.AreEqual(3250.0, layout.CutRect.X1, 0.001);
            Assert.IsTrue(layout.CopRect.HasValue);
            Assert.AreEqual(1605.0, layout.CopRect.Value.X0, 0.001);
            Assert.AreEqual(1845.0, layout.CopRect.Value.X1, 0.001);
            Assert.AreEqual(600.0, layout.CopRect.Value.Y0, 0.001);
            Assert.AreEqual(1900.0, layout.CopRect.Value.Y1, 0.001);
        }

        [TestMethod]
        public void Width_Dimension_Extension_Points_On_Bottom_Edge()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.AreEqual(1, layout.Dimensions.Count);
            DimSpec d = layout.Dimensions[0];
            Assert.AreEqual(0.0, d.Ext1Y, 0.001);
            Assert.AreEqual(0.0, d.Ext2Y, 0.001);
            Assert.AreEqual(0.0, d.LineY, 0.001);
            Assert.AreEqual(0.0, d.Ext1X, 0.001);
            Assert.AreEqual(2250.0, d.Ext2X, 0.001);
        }
```

Leave `CutHeight_Is_Measured_Less_Allowance_Doubled`,
`WallLayout_CutRect_Matches_Derived_Dims` (rename collision: delete the old
`WallLayout_CutRect_Matches_Derived_Dims` since `CutRect_Grows_By_Edge_Allowance`
replaces it), `No_Cop_When_Disabled`, and
`IdentifierLabel_Concatenates_Project_And_Suffix` in place. Update the two
constructor calls in the kept tests (`No_Cop_When_Disabled`,
`IdentifierLabel_Concatenates_Project_And_Suffix`,
`WallLayout_CutRect_Matches_Derived_Dims` if kept) to the two-arg constructor
`new LiftBlanketCalculator(fixingAllowanceMm: 50, edgeAllowanceMm: 10)`.

> To avoid duplicate method names, delete the old
> `WallLayout_CutRect_Matches_Derived_Dims` test entirely — `CutRect_Grows_By_Edge_Allowance`
> covers the same assertions.

- [ ] **Step 2: Run the tests — expect COMPILE failure**

Run: `dotnet test CanvasCovers.Tests`
Expected: FAIL to compile — `LiftBlanketCalculator` has no two-arg
constructor, no `AutoTopGap`, and `CutWidth` takes one arg. This drives Task 4.

---

### Task 4: Implement the segment-driven COP + parameterised allowance in the calculator

**Files:**
- Modify: `CanvasCovers\Geometry\Products\LiftBlanket\LiftBlanketCalculator.cs`

- [ ] **Step 1: Add the edge-allowance field + two-arg constructor**

Replace the field/constructor block:

```csharp
        private readonly double _fixingAllowanceMm;

        public LiftBlanketCalculator(double fixingAllowanceMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
        }
```

with:

```csharp
        private readonly double _fixingAllowanceMm;
        private readonly double _edgeAllowanceMm;

        public LiftBlanketCalculator(double fixingAllowanceMm, double edgeAllowanceMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
            _edgeAllowanceMm = edgeAllowanceMm;
        }
```

- [ ] **Step 2: Parameterise `CutWidth` and add `AutoTopGap`; remove `CopFitsInBottomHalf`**

Replace:

```csharp
        public static double CutWidth(double summedSegments)
        {
            return summedSegments + WidthAllowanceMm;
        }
```

with:

```csharp
        public static double CutWidth(double summedSegments, double edgeAllowanceMm)
        {
            return summedSegments + edgeAllowanceMm;
        }

        // The auto-derived top gap: from the COP top up to the fold midline.
        // The fixing allowance comes off this gap, so it references the fold
        // (measuredHeight − fixingAllowance), NOT the raw measured height.
        // Returns a negative value when the COP would cross the fold — the
        // dialog treats that as a blocking validation error.
        public static double AutoTopGap(
            double measuredHeight, double fixingAllowanceMm,
            double copGapFromBottom, double copHeight)
        {
            double fold = HalfHeight(measuredHeight, fixingAllowanceMm);
            return fold - copGapFromBottom - copHeight;
        }
```

Then delete the entire `CopFitsInBottomHalf` method (the `AutoTopGap < 0`
check supersedes it).

Also remove the now-unused `WidthAllowanceMm` constant line:

```csharp
        private const double WidthAllowanceMm = 10.0;
```

- [ ] **Step 3: Update `LayoutWall` to use the allowance + segment-derived COP**

In `LayoutWall`, replace:

```csharp
            double cutWidth = CutWidth(wall.Width);
```

with:

```csharp
            double half = _edgeAllowanceMm / 2.0;
            double cutWidth = CutWidth(wall.Width, _edgeAllowanceMm);
```

(There is an existing `double half = HalfHeight(...)` a few lines below for
the vertical half — RENAME that one to `halfHeight` to avoid the clash. Update
its two uses: the `FoldMidlineY = half` assignment and any reference.)

Replace the height block:

```csharp
            double half = HalfHeight(wall.MeasuredHeight, _fixingAllowanceMm);
            double cutHeight = CutHeight(wall.MeasuredHeight, _fixingAllowanceMm);

            var layout = new WallLayout
            {
                CutRect = new RectSpec(originX, 0, originX + cutWidth, cutHeight),
                FoldMidlineY = half,
                Dimensions = new List<DimSpec>(),
            };
```

with:

```csharp
            double halfHeight = HalfHeight(wall.MeasuredHeight, _fixingAllowanceMm);
            double cutHeight = CutHeight(wall.MeasuredHeight, _fixingAllowanceMm);

            var layout = new WallLayout
            {
                CutRect = new RectSpec(originX, 0, originX + cutWidth, cutHeight),
                FoldMidlineY = halfHeight,
                Dimensions = new List<DimSpec>(),
            };
```

Replace the COP block:

```csharp
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
```

with:

```csharp
            if (wall.Cop.Enabled)
            {
                // Horizontal geometry is derived from the segments: the COP
                // width is the middle box (Seg2), and its left edge sits
                // half-allowance + DoorReturnLeft + Seg1 in from the cut edge
                // (measured from the door-return line, per the sheet).
                double copX0 = originX + half
                    + wall.Segments.DoorReturnLeft + wall.Segments.Seg1;
                double copWidth = wall.Segments.Seg2;
                double copY0 = wall.Cop.GapFromBottom;
                layout.CopRect = new RectSpec(
                    copX0,
                    copY0,
                    copX0 + copWidth,
                    copY0 + wall.Cop.Height);
            }
```

- [ ] **Step 4: Run the calculator tests — expect PASS**

Run: `dotnet test CanvasCovers.Tests`
Expected: still FAIL to compile, because `LiftBlanketGenerator.cs` and
`LiftBlanketWindow.xaml.cs` still call the old one-arg constructor,
`CutWidth(x)`, `CopFitsInBottomHalf`, `Cop.Width`, `Cop.OffsetFromLeft`. The
*calculator + its tests* are now consistent, but the project won't compile
until Tasks 5–6 fix the generator and UI. Proceed; do not commit yet.

> Rationale: the test project links the calculator source and references no
> interop, but it compiles the whole `CanvasCovers.Tests` assembly which does
> not include the generator/UI. Check: if `dotnet test` now compiles the test
> assembly successfully and only the main project fails, the calculator tests
> may actually run. If they run, expect PASS. If the solution build blocks
> them, continue to Task 5 and run tests at the end of Task 6.

---

### Task 5: Fix the generator to the new calculator API

The generator does no COP math but constructs the calculator and reads the
allowance. Update its constructor call and any removed-symbol references.

**Files:**
- Modify: `CanvasCovers\Geometry\Products\LiftBlanket\LiftBlanketGenerator.cs`

- [ ] **Step 1: Pass the edge allowance into the calculator**

Replace:

```csharp
            var calc = new LiftBlanketCalculator(job.Options.FixingAllowanceMm);
```

with:

```csharp
            var calc = new LiftBlanketCalculator(
                job.Options.FixingAllowanceMm, job.Options.EdgeAllowanceMm);
```

- [ ] **Step 2: Build — expect the generator to compile (UI still broken)**

Run: `dotnet build -c Release`
Expected: FAIL, but now only in `LiftBlanketWindow.xaml.cs` (references to
`Cop.Width`, `Cop.OffsetFromLeft`, `CopFitsInBottomHalf`). The generator
errors are gone. Task 6 / Phase 5 fixes the UI.

- [ ] **Step 3: Commit calculator + generator together**

```bash
git add CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs CanvasCovers.Tests/LiftBlanketCalculatorTests.cs
git commit -m "feat: segment-driven COP + parameterised edge allowance + AutoTopGap"
```

---

## Phase 3: Quilting math (TDD)

### Task 6: Add quilt-line DTOs to WallLayout

**Files:**
- Modify: `CanvasCovers\Geometry\Products\LiftBlanket\WallLayout.cs`

- [ ] **Step 1: Add a `LineSpec` struct and a `QuiltLines` list**

After the `LabelSpec` struct (before `WallLayout`), add:

```csharp
    // A single straight line segment to emit (used for quilt lines).
    public struct LineSpec
    {
        public double X0, Y0, X1, Y1;

        public LineSpec(double x0, double y0, double x1, double y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }
    }
```

Inside `WallLayout`, after the `Dimensions` field, add:

```csharp
        // Vertical + horizontal quilt lines (draw/score layer), bottom half
        // only. Empty when quilting is disabled.
        public List<LineSpec> QuiltLines = new List<LineSpec>();
```

- [ ] **Step 2: Build to confirm it compiles (UI still red — expected)**

Run: `dotnet build -c Release`
Expected: FAIL only in `LiftBlanketWindow.xaml.cs` (unchanged from Task 5).
`WallLayout.cs` itself compiles.

- [ ] **Step 3: Commit**

```bash
git add CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs
git commit -m "feat: LineSpec + QuiltLines on WallLayout"
```

---

### Task 7: Write failing tests for the even-division quilt helper

We test the pure spacing helper in isolation first (count + positions), then
the integrated `LayoutWall` quilting.

**Files:**
- Create: `CanvasCovers.Tests\QuiltingTests.cs`

- [ ] **Step 1: Write the quilting tests**

```csharp
using System.Collections.Generic;
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class QuiltingTests
    {
        [TestMethod]
        public void EvenlySpaced_Divides_Span_Into_Equal_Interior_Lines()
        {
            // Span 0..2100, target gap 700 → 3 gaps → 2 interior lines at
            // 700 and 1400 (the span ends are bounds, not lines).
            List<double> lines = LiftBlanketCalculator.EvenlySpaced(0, 2100, 700);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(700.0, lines[0], 0.001);
            Assert.AreEqual(1400.0, lines[1], 0.001);
        }

        [TestMethod]
        public void EvenlySpaced_Rounds_Count_To_Nearest_Even_Division()
        {
            // Span 0..1000, target 700 → 1000/700 = 1.43 → round to 1 gap →
            // 0 interior lines. Span 0..1000 target 400 → 2.5 → round to 2 or
            // 3 gaps; nearest is 3 (2.5 rounds up) → interior at 333.3, 666.6.
            List<double> a = LiftBlanketCalculator.EvenlySpaced(0, 1000, 700);
            Assert.AreEqual(0, a.Count);

            List<double> b = LiftBlanketCalculator.EvenlySpaced(0, 1000, 400);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(333.333, b[0], 0.01);
            Assert.AreEqual(666.667, b[1], 0.01);
        }

        [TestMethod]
        public void EvenlySpaced_Returns_Empty_For_Nonpositive_Span_Or_Spacing()
        {
            Assert.AreEqual(0, LiftBlanketCalculator.EvenlySpaced(0, 0, 700).Count);
            Assert.AreEqual(0, LiftBlanketCalculator.EvenlySpaced(0, 2100, 0).Count);
            Assert.AreEqual(0, LiftBlanketCalculator.EvenlySpaced(500, 100, 700).Count);
        }

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
            wall.Cop.Height = 1300;
            wall.Cop.GapFromBottom = 600;
            return wall;
        }

        [TestMethod]
        public void Quilt_Lines_Stay_In_Bottom_Half_And_Within_Side_Bounds()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            // Quilting params: enabled, vertical spacing 700.
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            Assert.IsTrue(layout.QuiltLines.Count > 0);

            double half = 5;                 // edge allowance 10 / 2
            double leftBound = 250 + half;   // DR-L + half = 255
            double rightBound = 2250 - 0 - half; // cutWidth − DR-R − half = 2245
            double bottomBound = half;       // 5
            double topBound = 2150;          // fold midline

            foreach (LineSpec l in layout.QuiltLines)
            {
                // every line endpoint within the quilt zone (with tolerance)
                Assert.IsTrue(l.X0 >= leftBound - 0.001 && l.X1 <= rightBound + 0.001,
                    $"line X out of bounds: {l.X0}..{l.X1}");
                Assert.IsTrue(l.Y0 >= bottomBound - 0.001 && l.Y1 <= topBound + 0.001,
                    $"line Y out of bounds: {l.Y0}..{l.Y1}");
            }
        }

        [TestMethod]
        public void Quilt_Empty_When_Disabled()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: false, verticalQuiltingSpacingMm: 700);
            Assert.AreEqual(0, layout.QuiltLines.Count);
        }
    }
}
```

- [ ] **Step 2: Run — expect compile failure**

Run: `dotnet test CanvasCovers.Tests`
Expected: FAIL to compile — `EvenlySpaced` does not exist and `LayoutWall` has
no quilting overload. Drives Task 8.

---

### Task 8: Implement `EvenlySpaced` + quilting in `LayoutWall`

**Files:**
- Modify: `CanvasCovers\Geometry\Products\LiftBlanket\LiftBlanketCalculator.cs`

- [ ] **Step 1: Add the `EvenlySpaced` helper**

Add this static method to the calculator (near the other statics):

```csharp
        // Interior line offsets that divide [start, end] into equal gaps as
        // close as possible to targetGap. The count of GAPS is round(span /
        // targetGap), clamped to at least 1; the returned list is the
        // interior boundaries (count = gaps − 1). Returns empty for a
        // nonpositive span or spacing. Even division means no remainder gap.
        public static List<double> EvenlySpaced(double start, double end, double targetGap)
        {
            var result = new List<double>();
            double span = end - start;
            if (span <= 0 || targetGap <= 0) return result;

            int gaps = (int)System.Math.Round(span / targetGap, System.MidpointRounding.AwayFromZero);
            if (gaps < 1) gaps = 1;
            double step = span / gaps;
            for (int i = 1; i < gaps; i++)
            {
                result.Add(start + i * step);
            }
            return result;
        }
```

- [ ] **Step 2: Add a quilting overload of `LayoutWall`**

Change the existing signature:

```csharp
        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix)
        {
```

to add two parameters with defaults (so existing callers/tests still compile):

```csharp
        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix,
            bool quiltingEnabled = false,
            double verticalQuiltingSpacingMm = 0)
        {
```

Then, just before `return layout;` at the end of the method, insert:

```csharp
            if (quiltingEnabled)
            {
                AddQuiltLines(layout, wall, originX, cutWidth, halfHeight,
                    half, verticalQuiltingSpacingMm);
            }
```

- [ ] **Step 3: Add the `AddQuiltLines` helper**

Add this private method to the calculator:

```csharp
        // Fills layout.QuiltLines with vertical + horizontal lines confined to
        // the bottom (measured) half. Side bounds come from the door-return
        // boxes (DR-L from the left, DR-R from the right); all four edges are
        // inset by the half-allowance. Horizontal lines are spaced by the
        // operator's target; vertical lines even-divide the bounded width to
        // a similar gap.
        private void AddQuiltLines(
            WallLayout layout, WallDimensions wall, double originX,
            double cutWidth, double foldMidline, double half,
            double verticalSpacing)
        {
            double left = originX + wall.Segments.DoorReturnLeft + half;
            double right = originX + cutWidth - wall.Segments.DoorReturnRight - half;
            double bottom = half;
            double top = foldMidline;

            if (right <= left || top <= bottom) return;

            // Horizontal lines (run left→right), spaced up the height by the
            // operator's target spacing, even-divided.
            foreach (double y in EvenlySpaced(bottom, top, verticalSpacing))
            {
                layout.QuiltLines.Add(new LineSpec(left, y, right, y));
            }

            // Vertical lines (run bottom→top), even-dividing the bounded width
            // to roughly the same target gap.
            foreach (double x in EvenlySpaced(left, right, verticalSpacing))
            {
                layout.QuiltLines.Add(new LineSpec(x, bottom, x, top));
            }
        }
```

- [ ] **Step 4: Run the quilting tests — expect PASS for QuiltingTests**

Run: `dotnet test CanvasCovers.Tests`
Expected: the `QuiltingTests` compile and pass. The full solution may still
fail to build because of the UI (`LiftBlanketWindow.xaml.cs`). If `dotnet test`
refuses to run due to the main project, note it and proceed — Phase 5 makes
the solution green, and we re-run the full suite at Task 14.

- [ ] **Step 5: Commit the quilting math**

```bash
git add CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs CanvasCovers.Tests/QuiltingTests.cs
git commit -m "feat: even-division quilt-line layout (vertical + horizontal, bottom half)"
```

---

## Phase 4: Generator emits quilt lines

### Task 9: Draw quilt lines in the generator

**Files:**
- Modify: `CanvasCovers\Geometry\Products\LiftBlanket\LiftBlanketGenerator.cs`

- [ ] **Step 1: Pass quilting params into `LayoutWall`**

In `Generate`, find the `LayoutWall` call:

```csharp
                        WallLayout layout = calc.LayoutWall(pair.Wall, cursorX, projectTag, pair.Suffix);
```

Replace with:

```csharp
                        WallLayout layout = calc.LayoutWall(
                            pair.Wall, cursorX, projectTag, pair.Suffix,
                            job.Options.QuiltingEnabled,
                            job.Options.VerticalQuiltingSpacingMm);
```

- [ ] **Step 2: Emit the quilt lines on the COP/draw layer in `DrawWall`**

In `DrawWall`, after the COP rectangle block (the
`if (layout.CopRect.HasValue) { ... }` block) and before the identifier label
block, add:

```csharp
            // Quilt lines on the draw/score layer (same layer as COP), never
            // the cut layer. Each is a 2-point open polyline.
            if (layout.QuiltLines.Count > 0)
            {
                layers.Activate(_layerSettings.Cop.Name);
                foreach (LineSpec q in layout.QuiltLines)
                {
                    sketch.InsertPolyline2D(
                        new[] { q.X0, q.Y0, q.X1, q.Y1 }, false);
                }
            }
```

> Note: `InsertPolyline2D(..., false)` = open polyline (not closed). The COP
> and cut rectangles use `true` (closed). Verify `InsertPolyline2D`'s second
> arg is the "closed" boolean in the interop before relying on this — it is
> used that way already at the top of `DrawWall`.

- [ ] **Step 3: Build — still expect UI errors only**

Run: `dotnet build -c Release`
Expected: FAIL only in `LiftBlanketWindow.xaml.cs`. The generator compiles.

- [ ] **Step 4: Commit**

```bash
git add CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs
git commit -m "feat: generator emits quilt lines on the draw layer"
```

---

## Phase 5: UI rework — tabbed, interactive, true-proportion blanket

This phase is verified live in DraftSight (WPF + COM cannot be unit-tested).
Each task ends with a `dotnet build -c Release` gate; the final live check is
Task 14.

### Task 10: Add an interactive WallBlanket control (true-proportion live diagram)

This new control replaces the passive `WallDiagram` as the per-wall input
surface: a `Canvas` that reshapes to the real height:width ratio, hosts the
embedded `TextBox`es, and redraws COP + segment dividers + quilt preview +
dimension leaders live on every edit.

**Files:**
- Create: `CanvasCovers\UI\Controls\WallBlanket.xaml`
- Create: `CanvasCovers\UI\Controls\WallBlanket.xaml.cs`

- [ ] **Step 1: Create the XAML shell**

```xml
<UserControl x:Class="CanvasCovers.UI.Controls.WallBlanket"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
            <CheckBox x:Name="IncludeWall" Content="Include this wall" IsChecked="True"
                      Margin="0,0,16,0" Checked="Input_Changed" Unchecked="Input_Changed" />
            <CheckBox x:Name="IncludeCop" Content="Include COP cutout" IsChecked="True"
                      Checked="Input_Changed" Unchecked="Input_Changed" />
        </DockPanel>
        <Canvas x:Name="DrawCanvas" Background="#FFFFFF"
                MinHeight="380" ClipToBounds="True"
                SizeChanged="DrawCanvas_SizeChanged" />
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create the code-behind with the public input surface + live redraw**

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.UI.Controls
{
    // Interactive per-wall input surface: a blanket drawn to true proportion
    // (clamped) with embedded measurement fields. Redraws live on edit. The
    // hosting window reads values via the public accessors and the embedded
    // TextBoxes. Rear-wall mode hides the COP fields.
    public partial class WallBlanket : UserControl
    {
        private readonly TextBox _drLeft, _seg1, _seg2, _seg3, _drRight;
        private readonly TextBox _measuredHeight, _copHeight, _copGapBottom;
        private readonly TextBlock _topGapAuto;

        // Allowance + fixing + quilting come from the shared Options panel;
        // the host pushes them in so the live preview can compute fold + COP.
        private double _fixingAllowance = 50;
        private double _edgeAllowance = 10;
        private double _quiltSpacing = 700;
        private bool _quiltEnabled = true;
        private bool _isRear;

        public WallBlanket()
        {
            InitializeComponent();
            _drLeft = MakeField("0");
            _seg1 = MakeField("0");
            _seg2 = MakeField("240");
            _seg3 = MakeField("1400");
            _drRight = MakeField("0");
            _measuredHeight = MakeField("2200");
            _copHeight = MakeField("1300");
            _copGapBottom = MakeField("600");
            _topGapAuto = new TextBlock { Foreground = Brushes.Gray, FontSize = 11 };
        }

        private TextBox MakeField(string value)
        {
            var tb = new TextBox { Text = value, Width = 56, TextAlignment = TextAlignment.Center };
            tb.TextChanged += Input_Changed;
            return tb;
        }

        // Configure the control for a wall. isRear hides COP fields.
        public void Configure(bool isRear)
        {
            _isRear = isRear;
            IncludeCop.Visibility = isRear ? Visibility.Collapsed : Visibility.Visible;
            Redraw();
        }

        // Pushed from the host whenever the Options panel changes.
        public void SetSharedParams(double fixingAllowance, double edgeAllowance,
            double quiltSpacing, bool quiltEnabled)
        {
            _fixingAllowance = fixingAllowance;
            _edgeAllowance = edgeAllowance;
            _quiltSpacing = quiltSpacing;
            _quiltEnabled = quiltEnabled;
            Redraw();
        }

        public bool WallEnabled => IncludeWall.IsChecked == true;
        public bool CopEnabled => !_isRear && IncludeCop.IsChecked == true;
        public void SetWallEnabled(bool v) => IncludeWall.IsChecked = v;
        public void SetWallEnabledInteractive(bool enabled) => IncludeWall.IsEnabled = enabled;

        public string DrLeftText => _drLeft.Text;
        public string Seg1Text => _seg1.Text;
        public string Seg2Text => _seg2.Text;
        public string Seg3Text => _seg3.Text;
        public string DrRightText => _drRight.Text;
        public string MeasuredHeightText => _measuredHeight.Text;
        public string CopHeightText => _copHeight.Text;
        public string CopGapBottomText => _copGapBottom.Text;

        // Set initial values (used to seed Left/Right/Rear differently).
        public void Seed(string drL, string s1, string s2, string s3, string drR, string measuredH)
        {
            _drLeft.Text = drL; _seg1.Text = s1; _seg2.Text = s2;
            _seg3.Text = s3; _drRight.Text = drR; _measuredHeight.Text = measuredH;
        }

        private void Input_Changed(object sender, RoutedEventArgs e) => Redraw();
        private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        private static double ParseOr(string s, double fallback)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        // Rebuilds the canvas: outer box at the true (clamped) aspect ratio,
        // segment dividers, COP, quilt preview, and the embedded fields.
        private void Redraw()
        {
            if (DrawCanvas == null) return;
            DrawCanvas.Children.Clear();

            double cw = DrawCanvas.ActualWidth;
            double ch = DrawCanvas.ActualHeight;
            if (cw < 20 || ch < 20) return;

            double segSum = ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0)
                + ParseOr(_seg2.Text, 0) + ParseOr(_seg3.Text, 0) + ParseOr(_drRight.Text, 0);
            double cutWidth = segSum + _edgeAllowance;
            double measuredH = ParseOr(_measuredHeight.Text, 2200);
            double cutHeight = LiftBlanketCalculator.CutHeight(measuredH, _fixingAllowance);
            if (cutWidth <= 0 || cutHeight <= 0) return;

            // True aspect ratio (height:width), clamped so extreme/typo inputs
            // can't collapse the layout (per spec §3.3).
            double aspect = cutHeight / cutWidth;
            aspect = Math.Max(0.33, Math.Min(3.0, aspect));

            // Fit a box of that aspect into the canvas with padding.
            double padX = 90, padY = 50;
            double availW = cw - 2 * padX, availH = ch - 2 * padY;
            double boxW = availW, boxH = availW * aspect;
            if (boxH > availH) { boxH = availH; boxW = availH / aspect; }
            double boxLeft = (cw - boxW) / 2;
            double boxTop = (ch - boxH) / 2;

            // Helpers mapping world (mm) → canvas px within the box.
            Func<double, double> px = mmX => boxLeft + (mmX / cutWidth) * boxW;
            Func<double, double> py = mmY => boxTop + boxH - (mmY / cutHeight) * boxH; // y up

            // Outer cut box.
            AddRect(boxLeft, boxTop, boxW, boxH, Brushes.SteelBlue, 2);

            // Fold midline.
            double foldY = LiftBlanketCalculator.HalfHeight(measuredH, _fixingAllowance);
            AddDashed(boxLeft, py(foldY), boxLeft + boxW, py(foldY), Brushes.HotPink);

            double half = _edgeAllowance / 2.0;

            // COP + vertical stack fields.
            if (CopEnabled)
            {
                double copH = ParseOr(_copHeight.Text, 1300);
                double gap = ParseOr(_copGapBottom.Text, 600);
                double copX0 = half + ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0);
                double copW = ParseOr(_seg2.Text, 0);
                AddRect(px(copX0), py(gap + copH), (copW / cutWidth) * boxW,
                    ((copH) / cutHeight) * boxH, Brushes.Purple, 1.5);

                double topGap = LiftBlanketCalculator.AutoTopGap(measuredH, _fixingAllowance, gap, copH);
                _topGapAuto.Text = "top gap (auto): " +
                    topGap.ToString("0", CultureInfo.InvariantCulture) +
                    (topGap < 0 ? "  ⚠ crosses fold" : "");
                _topGapAuto.Foreground = topGap < 0 ? Brushes.Red : Brushes.Gray;

                PlaceField(_copHeight, px(copX0 + copW) + 6, py(gap + copH));
                PlaceField(_copGapBottom, px(copX0 + copW) + 6, py(gap) - 12);
                Place(_topGapAuto, px(copX0 + copW) + 6, py(gap + copH) - 26);

                // Quilt preview lines (light), live.
                if (_quiltEnabled)
                {
                    var calc = new LiftBlanketCalculator(_fixingAllowance, _edgeAllowance);
                    var wall = ReadWallModel();
                    WallLayout layout = calc.LayoutWall(wall, 0, "", "",
                        _quiltEnabled, _quiltSpacing);
                    foreach (LineSpec q in layout.QuiltLines)
                    {
                        AddLine(px(q.X0), py(q.Y0), px(q.X1), py(q.Y1),
                            new SolidColorBrush(Color.FromRgb(0xD8, 0xB0, 0xE8)), 0.8);
                    }
                }
            }
            else
            {
                _topGapAuto.Text = "";
            }

            // Segment dividers + the 5 bottom fields.
            double accum = 0;
            var segVals = new[] { ParseOr(_drLeft.Text, 0), ParseOr(_seg1.Text, 0),
                ParseOr(_seg2.Text, 0), ParseOr(_seg3.Text, 0), ParseOr(_drRight.Text, 0) };
            var segFields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            for (int i = 0; i < 5; i++)
            {
                double startMm = half + accum;
                if (i > 0 && startMm > half + 0.001)
                    AddLine(px(startMm), boxTop, px(startMm), boxTop + boxH, Brushes.LightGray, 0.6);
                PlaceField(segFields[i], px(startMm + segVals[i] / 2.0) - 28, boxTop + boxH + 8);
                accum += segVals[i];
            }

            // Measured height field on the outer left.
            PlaceField(_measuredHeight, boxLeft - 78, boxTop + boxH / 2 - 12);
        }

        private WallDimensions ReadWallModel()
        {
            var w = new WallDimensions();
            w.Segments.DoorReturnLeft = ParseOr(_drLeft.Text, 0);
            w.Segments.Seg1 = ParseOr(_seg1.Text, 0);
            w.Segments.Seg2 = ParseOr(_seg2.Text, 0);
            w.Segments.Seg3 = ParseOr(_seg3.Text, 0);
            w.Segments.DoorReturnRight = ParseOr(_drRight.Text, 0);
            w.MeasuredHeight = ParseOr(_measuredHeight.Text, 2200);
            w.Cop.Enabled = CopEnabled;
            w.Cop.Height = ParseOr(_copHeight.Text, 1300);
            w.Cop.GapFromBottom = ParseOr(_copGapBottom.Text, 600);
            return w;
        }

        private void AddRect(double x, double y, double w, double h, Brush stroke, double thick)
        {
            var r = new Rectangle { Width = w, Height = h, Stroke = stroke, StrokeThickness = thick, Fill = Brushes.Transparent };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y); DrawCanvas.Children.Add(r);
        }
        private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double thick)
        {
            DrawCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thick });
        }
        private void AddDashed(double x1, double y1, double x2, double y2, Brush stroke)
        {
            DrawCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 } });
        }
        private void PlaceField(TextBox tb, double x, double y)
        {
            if (!DrawCanvas.Children.Contains(tb)) DrawCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
        }
        private void Place(UIElement el, double x, double y)
        {
            if (!DrawCanvas.Children.Contains(el)) DrawCanvas.Children.Add(el);
            Canvas.SetLeft(el, x); Canvas.SetTop(el, y);
        }
    }
}
```

- [ ] **Step 3: Build — expect WallBlanket to compile; window still red**

Run: `dotnet build -c Release`
Expected: FAIL only in `LiftBlanketWindow.xaml.cs`. `WallBlanket` compiles.

- [ ] **Step 4: Commit the new control**

```bash
git add CanvasCovers/UI/Controls/WallBlanket.xaml CanvasCovers/UI/Controls/WallBlanket.xaml.cs
git commit -m "feat: interactive WallBlanket control (true-proportion live diagram)"
```

---

### Task 11: Replace the window's wall sections with tabs hosting WallBlanket

**Files:**
- Modify: `CanvasCovers\UI\Products\LiftBlanket\LiftBlanketWindow.xaml`

- [ ] **Step 1: Remove the old right-side WallDiagram border**

Delete the entire `<Border DockPanel.Dock="Right" ...>` block (the one wrapping
`<controls:WallDiagram x:Name="Diagram" .../>`). The blanket is now inline per
tab, not a side panel.

- [ ] **Step 2: Replace the three wall `<Border>` sections with a TabControl**

Replace the three wall sections (the `Tag="Left"`, `Tag="Right"`, `Tag="Rear"`
borders, from `<Border Style="{StaticResource SectionPanel}" Tag="Left"...>`
through the closing `</Border>` of the Rear section) with:

```xml
                <Border Style="{StaticResource SectionPanel}">
                    <StackPanel>
                        <TextBlock Style="{StaticResource SectionTitle}" Text="WALLS" />
                        <TextBlock Style="{StaticResource MutedLabel}"
                                   Text="One tab per wall. Type the measurements where they sit on the sheet; the blanket redraws live. Rear tab is disabled when Through Car is ticked." />
                        <TabControl x:Name="WallTabs" MinHeight="460" BorderThickness="1">
                            <TabItem Header="Left Wall">
                                <controls:WallBlanket x:Name="LeftBlanket" Margin="8" />
                            </TabItem>
                            <TabItem Header="Right Wall">
                                <controls:WallBlanket x:Name="RightBlanket" Margin="8" />
                            </TabItem>
                            <TabItem x:Name="RearTab" Header="Rear Wall">
                                <controls:WallBlanket x:Name="RearBlanket" Margin="8" />
                            </TabItem>
                        </TabControl>
                    </StackPanel>
                </Border>
```

- [ ] **Step 3: Add Edge Allowance + Vertical Quilting Spacing to the Options section**

In the OPTIONS `<Border>`, after the Fixing Allowance `<Grid>` (the one with
`FixingAllowanceInput`) and before the `ExportDxfOption` checkbox, add:

```xml
                        <Grid Margin="0,8,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="160" />
                                <ColumnDefinition Width="100" />
                                <ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Edge Allowance (mm)" />
                            <TextBox Grid.Column="1" Name="EdgeAllowanceInput" Text="10" TextChanged="SharedParam_Changed" />
                            <TextBlock Grid.Column="2" Style="{StaticResource MutedLabel}"
                                       Text="Added to cut width and split evenly (half each side + bottom inset for quilting)." />
                        </Grid>
                        <Grid Margin="0,8,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="160" />
                                <ColumnDefinition Width="100" />
                                <ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Quilting Spacing (mm)" />
                            <TextBox Grid.Column="1" Name="QuiltingSpacingInput" Text="700" TextChanged="SharedParam_Changed" />
                            <TextBlock Grid.Column="2" Style="{StaticResource MutedLabel}"
                                       Text="Target gap between horizontal quilt lines; lines even-divide so spacing lands cleanly." />
                        </Grid>
                        <CheckBox Name="QuiltingOption" Margin="0,8,0,0" IsChecked="True"
                                  Checked="SharedParam_Changed" Unchecked="SharedParam_Changed"
                                  Content="Draw quilting lines" />
```

Also add `TextChanged="SharedParam_Changed"` to the existing
`FixingAllowanceInput` TextBox, and `SelectionChanged` on `FixingsInput`
already fires `FixingsInput_SelectionChanged` (keep it; we extend that handler
in Task 12 to also push shared params).

- [ ] **Step 4: Build — expect window code-behind errors (handlers missing)**

Run: `dotnet build -c Release`
Expected: FAIL — `SharedParam_Changed` not defined, plus the still-pending
COP-field reference errors. Task 12 rewrites the code-behind.

> Do not commit yet — the XAML and code-behind must land together (Task 12).

---

### Task 12: Rewrite the window code-behind for tabs + shared params + new validation

**Files:**
- Modify: `CanvasCovers\UI\Products\LiftBlanket\LiftBlanketWindow.xaml.cs`

- [ ] **Step 1: Replace the Loaded handler + remove diagram/highlight code**

Replace `LiftBlanketWindow_Loaded`, `WallSection_Activated`, and
`DimField_GotFocus` with:

```csharp
        private void LiftBlanketWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Seed the three blankets. Left/Right default with Seg3 1400 (a
            // meaningful default); Rear is COP-less.
            LeftBlanket.Configure(isRear: false);
            LeftBlanket.Seed("0", "0", "240", "1400", "0", "2200");
            RightBlanket.Configure(isRear: false);
            RightBlanket.Seed("0", "0", "240", "1400", "0", "2200");
            RearBlanket.Configure(isRear: true);
            RearBlanket.Seed("0", "0", "0", "1400", "0", "2200");
            PushSharedParams();
        }

        // Pushes the Options-panel values (allowances, quilting) into every
        // blanket so each live preview computes the same fold + COP geometry.
        private void PushSharedParams()
        {
            double fixing = ParseOr(FixingAllowanceInput.Text, 50);
            double edge = ParseOr(EdgeAllowanceInput.Text, 10);
            double quilt = ParseOr(QuiltingSpacingInput.Text, 700);
            bool quiltOn = QuiltingOption.IsChecked == true;
            LeftBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
            RightBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
            RearBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
        }

        private void SharedParam_Changed(object sender, RoutedEventArgs e)
        {
            if (LeftBlanket == null) return; // fires during XAML init
            PushSharedParams();
        }

        private static double ParseOr(string s, double fallback)
        {
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }
```

- [ ] **Step 2: Rewrite `GenerateButton_Click` wall reads to use the blankets**

Replace the three `ReadWall`/`ReadRearWall` calls in `GenerateButton_Click`
with:

```csharp
            WallDimensions left = ReadBlanket(LeftBlanket, "Left wall", errors);
            WallDimensions right = ReadBlanket(RightBlanket, "Right wall", errors);
            WallDimensions rear = ReadBlanket(RearBlanket, "Rear wall", errors);
```

- [ ] **Step 3: Replace `ReadWall` + `ReadRearWall` with a single `ReadBlanket`**

Delete both `ReadWall` and `ReadRearWall` methods. Add:

```csharp
        private WallDimensions ReadBlanket(
            CanvasCovers.UI.Controls.WallBlanket blanket, string wallLabel, List<string> errors)
        {
            var wall = new WallDimensions { Enabled = blanket.WallEnabled };
            if (!wall.Enabled) return wall;

            wall.Segments.DoorReturnLeft  = ReadNonNegative(blanket.DrLeftText,  wallLabel + " door return (left)",  errors);
            wall.Segments.Seg1            = ReadNonNegative(blanket.Seg1Text,    wallLabel + " segment 1",            errors);
            wall.Segments.Seg2            = ReadNonNegative(blanket.Seg2Text,    wallLabel + " segment 2 (COP width)", errors);
            wall.Segments.Seg3            = ReadNonNegative(blanket.Seg3Text,    wallLabel + " segment 3",            errors);
            wall.Segments.DoorReturnRight = ReadNonNegative(blanket.DrRightText, wallLabel + " door return (right)", errors);
            wall.MeasuredHeight           = ReadPositive(blanket.MeasuredHeightText, wallLabel + " measured height", errors);

            if (wall.Segments.TotalWidth <= 0)
                errors.Add(wallLabel + " needs at least one non-zero segment.");

            wall.Cop.Enabled = blanket.CopEnabled;
            if (wall.Cop.Enabled)
            {
                wall.Cop.Height        = ReadPositive(blanket.CopHeightText,    wallLabel + " COP height", errors);
                wall.Cop.GapFromBottom = ReadNonNegative(blanket.CopGapBottomText, wallLabel + " COP gap from bottom", errors);

                if (wall.Segments.Seg2 <= 0)
                    errors.Add(wallLabel + " COP width (segment 2) must be greater than zero when COP is enabled.");

                double fixingForCop = ParseOr(FixingAllowanceInput.Text, 50);
                if (wall.MeasuredHeight > 0 && wall.Cop.Height > 0 &&
                    Geometry.Products.LiftBlanket.LiftBlanketCalculator.AutoTopGap(
                        wall.MeasuredHeight, fixingForCop, wall.Cop.GapFromBottom, wall.Cop.Height) < 0)
                {
                    errors.Add(wallLabel +
                        " COP gap-from-bottom + height crosses the fold line (must fit within the measured half = measured height − fixing allowance).");
                }
            }
            return wall;
        }
```

- [ ] **Step 4: Update `ThroughCarOption_Changed` + Fixing handler for tabs**

Replace `ThroughCarOption_Changed` with a version that disables the Rear tab +
blanket:

```csharp
        private void ThroughCarOption_Changed(object sender, RoutedEventArgs e)
        {
            if (RearBlanket == null || RearTab == null) return; // during init
            bool through = ThroughCarOption.IsChecked == true;
            if (through)
            {
                RearBlanket.SetWallEnabled(false);
                RearBlanket.SetWallEnabledInteractive(false);
                RearTab.IsEnabled = false;
            }
            else
            {
                RearBlanket.SetWallEnabledInteractive(true);
                RearTab.IsEnabled = true;
            }
        }
```

In `FixingsInput_SelectionChanged`, after it sets `FixingAllowanceInput.Text`,
add a `PushSharedParams();` call at the end of the method (guard: only if
`LeftBlanket != null`).

- [ ] **Step 5: Build — expect SUCCESS**

Run: `dotnet build -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. The project compiles for
the first time since Task 2.

- [ ] **Step 6: Commit XAML + code-behind together**

```bash
git add CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs
git commit -m "feat: tabbed wall blankets + shared params + fold-line validation"
```

---

### Task 13: Remove the dead WallDiagram control + tests-green checkpoint

**Files:**
- Delete: `CanvasCovers\UI\Controls\WallDiagram.xaml`
- Delete: `CanvasCovers\UI\Controls\WallDiagram.xaml.cs`
- Modify: `CanvasCovers\CanvasCovers.csproj` (only if the files are explicitly
  listed; SDK-style globbing usually means no edit is needed)

- [ ] **Step 1: Confirm nothing else references WallDiagram**

Run: `git grep -n "WallDiagram"` (or Grep tool)
Expected: only the two files being deleted. If the window XAML still
references it, you missed a deletion in Task 11 — fix that first.

- [ ] **Step 2: Delete the files**

```bash
git rm CanvasCovers/UI/Controls/WallDiagram.xaml CanvasCovers/UI/Controls/WallDiagram.xaml.cs
```

- [ ] **Step 3: Build — expect SUCCESS**

Run: `dotnet build -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Run the FULL test suite — expect all green**

Run: `dotnet test CanvasCovers.Tests`
Expected: `Passed!` with the new count (original 21 minus the 2 removed
`Cop_Fits_*`/old width tests, plus the new AutoTopGap/edge-allowance/quilting
tests). Confirm 0 failures. If any fail, STOP and fix before committing.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: remove dead WallDiagram; full headless suite green"
```

---

## Phase 6: Live verification + docs

### Task 14: Build the installer and verify live in DraftSight

**Files:** none (manual verification, then docs)

- [ ] **Step 1: Bump the installer version**

Edit `Installer\CanvasCovers.iss`: change `#define MyAppVersion "1.2.0"` to
`"1.3.0"`. Also bump `AssemblyVersion`/`AssemblyFileVersion` in
`CanvasCovers\Properties\AssemblyInfo.cs` to `1.3.0.0`.

- [ ] **Step 2: Build the installer (DraftSight must be closed)**

Run: `.\Installer\build.ps1`
Expected: `Installer built: ...BesiaCAD-CanvasCovers-Setup-1.3.0.exe`

- [ ] **Step 3: Install + live-test against the LOAD_TEST walkthrough**

Run the EXE as admin, activate the add-in, open the picker → Lift Blanket.
Verify against `docs/LOAD_TEST.md` §6a (reproduce job 12346 Left wall) PLUS
the new behaviour:
- Tabs: Left / Right / Rear; Rear disables under Through Car.
- Type the Left segments `250 / 350 / 240 / 1400 / 0`, measured 2200, COP
  height 1300, from-bottom 600 → the blanket redraws live; top-gap shows 250.
- Generate → cut rect 2250 × 4300, COP 240 wide at X 605..845, quilt lines on
  the draw layer filling the bottom half up to 2150, inset 5 from the edges.
- Make `from-bottom + COP height` exceed the fold → Generate is blocked with
  the fold-line error; dialog stays open.

- [ ] **Step 4: Export DXF + parse to confirm coordinates**

Save the DXF (default filename = network number), then:
Run: `.\reference\parse-dxf.ps1 -Path <saved.dxf>`
Expected: cut rect width 2250, COP left edge 605 ± rounding, quilt lines
present on the draw layer. Compare against `reference/DXF_FINDINGS.md`.

- [ ] **Step 5: Whole-implementation end-to-end review (mandatory)**

Trace ONE job dialog → `ReadBlanket` → `LiftBlanketJob` → `LiftBlanketGenerator`
→ SDK, and confirm the `LayersPanel` defaults still agree with `LayerSettings`
(the COP/quilt land on `5 Draw and Text`, not the cut layer). This is the
class of cross-file data-flow bug that per-task review misses (see
`project_liftblanket_calculator_split` memory).

### Task 15: Update docs

**Files:**
- Modify: `docs\STATUS.md`
- Modify: `docs\LOAD_TEST.md`
- Modify: `CLAUDE.md` (add any new SDK gotcha discovered during live test, e.g.
  open-polyline quilt lines, embedded-TextBox-on-Canvas quirks)

- [ ] **Step 1: Update STATUS.md headline + "Recently completed" for v1.3.0**

Document: segment-driven COP, tabbed live blankets, built quilting, edge
allowance. Move quilting out of "deliberately not yet built".

- [ ] **Step 2: Update LOAD_TEST.md click-through for the tabbed UI + quilting**

Replace the stacked-section description (§2) and the COP-fields description
with the tabbed blanket + Options edge-allowance/quilting fields, and add a
quilting check to §6.

- [ ] **Step 3: Commit docs**

```bash
git add docs/STATUS.md docs/LOAD_TEST.md CLAUDE.md CanvasCovers/Properties/AssemblyInfo.cs Installer/CanvasCovers.iss
git commit -m "docs: v1.3.0 — sheet-mirroring COP, tabbed blankets, quilting"
```

### Task 16: Finish the branch

- [ ] **Step 1: Invoke the finishing-a-development-branch skill**

Use `superpowers:finishing-a-development-branch` to choose merge / PR / cleanup.

---

## Self-review notes (addressed)

- **Spec coverage:** §2.1 width/edge-allowance → Tasks 1,4; §2.2 height
  (unchanged) → preserved; §2.3 COP vertical + AutoTopGap + blocking
  validation → Tasks 3,4,12; §2.4 quilting → Tasks 6,7,8,9; §3 tabbed
  true-proportion live UI → Tasks 10,11,12,13; §4 architecture preserved;
  §6 end-to-end review → Task 14 Step 5.
- **Removed-symbol consistency:** `Cop.Width`/`Cop.OffsetFromLeft` removed in
  Task 2, all call sites updated in Tasks 4 (calc), 12 (UI); `CopFitsInBottomHalf`
  removed in Task 4, replaced by `AutoTopGap` everywhere it was used (tests
  Task 3, UI Task 12). `CutWidth` one-arg → two-arg updated in Task 4 (def),
  Tasks 3 (tests), and `LayoutWall` callers in Tasks 9 (generator), 10 (control).
- **Build stays red Tasks 2–11 by design**; first green is Task 12 Step 5,
  full suite green at Task 13 Step 4. This is called out at each gate so an
  executor doesn't mistake it for a mistake.
