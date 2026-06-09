# Beta-Review Changes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the confirmed build-now items from Martin's beta review (items 1, 3, 4, 5, 6, 7, 8, 10+11, 12, 13, 14, 15, 16, 18, 19, 20) on the Canvas Covers DraftSight add-in.

**Architecture:** Keep the existing split — pure, headless-testable logic in `LiftBlanketCalculator` / models / `IO`, and thin WPF + SDK translators on top. Push every new rule into the testable layer; treat WPF and the generator as wiring verified live in DraftSight. Logic changes are driven by `dotnet test` (29 tests pass today); UI/SDK changes are verified by build + install + a DraftSight smoke test at each phase boundary.

**Tech Stack:** C# net48, WPF, DraftSight COM interop, MSTest (`dotnet test`), Inno Setup installer.

---

## How to run the headless tests

From the repo root:

```
dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo
```

Confirmed working on the dev machine (dotnet 10.0.300, net48 target). This is the red/green loop for every logic task below.

## How to verify UI / generator changes

The add-in DLL is COM-loaded by DraftSight and cannot be exercised headlessly. At each phase that touches WPF or the generator:

1. Close DraftSight.
2. `.\Installer\build.ps1` (rebuilds Release + compiles the installer).
3. Run the produced `Installer\Output\BesiaCAD-CanvasCovers-Setup-<ver>.exe` as admin.
4. Open DraftSight → ribbon → Canvas Covers → Lift Blanket; perform the phase's smoke test.

Each UI/generator task lists its specific smoke check under **Live check**.

---

## Open questions — defaults chosen (override before/while building)

These were flagged to Martin and are not yet answered. The plan proceeds on a stated default so it stays executable. Change the default if Martin says otherwise.

1. **Date field (item 1):** DEFAULT = keep it. (Not in Martin's field list, but harmless and on the template.)
2. **Blanket-text wall suffix (item 3):** DEFAULT = keep the ` L`/` R`/` B` suffix appended after the blanket text so walls stay distinguishable on the sheet.
3. **Self-adhesive Velcro (item 7):** DEFAULT = add it as a distinct dropdown entry backed by a new `FixingType.SelfAdhesiveVelcro` (allowance 0), separate from the existing `Velcro` (also 0).
4. **Velcro-corners return boxes (item 9):** OPTIONAL, last phase, low priority. DEFAULT = the tickbox fills `DoorReturnLeft` and `DoorReturnRight` (both outer return tabs) with 50 on the Left and Right walls only.
5. **Item 17 (general sanity checks):** EXCLUDED from this plan — Martin didn't understand it. Explain with examples and re-scope separately.

## Key geometry decision (item 10+11) — confirm before Phase 1

Today one value, `EdgeAllowanceMm` (=10), does THREE jobs at once: it boosts the cut width, it offsets the COP, and (halved) it insets the quilting. We are separating these:

- **Outline = the entered width exactly** (no boost). Operator adds their own 10mm shrinkage when typing sizes.
- **COP offset loses the half-edge term** — its left edge becomes `originX + DoorReturnLeft + Seg1` (was `+ halfEdge`).
- **Quilt inset becomes its own field** `QuiltInsetMm` (default 5), applied directly (not halved) as the quilt-line clearance from the outline.
- **Quilt vertical fill bounds** preserve the "no line on the edge" rule: a bound sits on the door-return line when that return is non-zero, otherwise it sits one inset in from the edge:
  - `leftBound  = drLeft  > 0 ? originX + drLeft               : originX + inset`
  - `rightBound = drRight > 0 ? originX + cutWidth - drRight    : originX + cutWidth - inset`

Net effect on the reference wall (DR-L 250, S1 350, S2 240, S3 1400, DR-R 0; H 2200; fixing 50): outline 2250→**2240**, COP X 605..845→**600..840**, DR-L quilt line 255→**250**, horizontals right edge 2245→**2235**. All intended. **This narrows existing-job outlines by ~10mm — note it in the release notes.**

---

## Phase 0: Branch + baseline

### Task 0: Create the working branch

**Files:** none.

- [ ] **Step 1: Confirm clean tree and green baseline**

Run:
```
git status
dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo
```
Expected: working tree clean; `Passed! - Failed: 0, Passed: 29`.

- [ ] **Step 2: Create and switch to the feature branch**

Run:
```
git checkout -b feat/beta-review-changes
```
Expected: `Switched to a new branch 'feat/beta-review-changes'`.

---

## Phase 1: Allowance refactor (items 10 + 11) — Martin's priority

This is the highest-value, highest-risk change and the shared blast radius. Do it first, fully test-driven. Items 5 and 13 build on the result.

### Task 1: Replace EdgeAllowanceMm with QuiltInsetMm on the options model

**Files:**
- Modify: `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs`
- Test: `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs` (covered indirectly via Task 2)

- [ ] **Step 1: Edit the option**

In `LiftBlanketOptions.cs`, remove the `EdgeAllowanceMm` property and its comment block, and add in its place:

```csharp
        // The mm the quilting lines are pulled inward from the outline so the
        // pen doesn't catch a cut edge. Applied directly (not halved) on the
        // left, right and bottom of the quilted region. Default 5; operator
        // may set it lower. The OUTLINE itself is always the entered width —
        // no edge allowance is added (the operator adds their own shrinkage).
        public double QuiltInsetMm { get; set; } = 5;
```

- [ ] **Step 2: Build the test project to surface every call site**

Run: `dotnet build CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: FAILS to compile — `LiftBlanketCalculatorTests` / `QuiltingTests` reference the old `CutWidth(x, y)` signature and `edgeAllowanceMm`. That list of errors is the Task 2/3 worklist.

### Task 2: Rework the calculator core (width + COP, no edge boost)

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`
- Test: `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs`

- [ ] **Step 1: Update the calculator's width/COP math and constructor**

In `LiftBlanketCalculator.cs`:

Change the fields/constructor:
```csharp
        private readonly double _fixingAllowanceMm;
        private readonly double _quiltInsetMm;

        public LiftBlanketCalculator(double fixingAllowanceMm, double quiltInsetMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
            _quiltInsetMm = quiltInsetMm;
        }
```

Replace the static `CutWidth` with an identity (outline = raw width):
```csharp
        public static double CutWidth(double summedSegments)
        {
            return summedSegments;
        }
```

In `LayoutWall`, replace the `halfEdge` / `cutWidth` lines:
```csharp
            double cutWidth = CutWidth(wall.Width);
```
(delete the `double halfEdge = _edgeAllowanceMm / 2.0;` line entirely.)

Update the COP left edge (drop the half-edge term):
```csharp
                double copX0 = originX
                    + wall.Segments.DoorReturnLeft + wall.Segments.Seg1;
```

Update the quilt call to pass the inset instead of `halfEdge`:
```csharp
            if (quiltingEnabled)
            {
                AddQuiltLines(layout, wall, originX, cutWidth, halfHeight,
                    _quiltInsetMm, verticalQuiltingSpacingMm);
            }
```

- [ ] **Step 2: Rewrite the affected calculator tests with the new numbers**

In `LiftBlanketCalculatorTests.cs`, replace the `CutWidth_Adds_The_Edge_Allowance` test with:

```csharp
        [TestMethod]
        public void CutWidth_Is_The_Raw_Segment_Sum()
        {
            Assert.AreEqual(2240.0, LiftBlanketCalculator.CutWidth(2240));
            Assert.AreEqual(0.0, LiftBlanketCalculator.CutWidth(0));
        }
```

Replace the body assertions of `Cop_Width_Is_Middle_Segment_And_Offset_From_Door_Return_Line` (construct with `quiltInsetMm: 5`, COP now at 600..840):

```csharp
        [TestMethod]
        public void Cop_Width_Is_Middle_Segment_And_Offset_From_Door_Return_Line()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.IsTrue(layout.CopRect.HasValue);
            RectSpec cop = layout.CopRect.Value;
            Assert.AreEqual(600.0, cop.X0, 0.001);
            Assert.AreEqual(840.0, cop.X1, 0.001);
            Assert.AreEqual(600.0, cop.Y0, 0.001);
            Assert.AreEqual(1900.0, cop.Y1, 0.001);
            Assert.IsTrue(cop.Y1 <= layout.FoldMidlineY);
        }
```

Replace `CutRect_Grows_By_Edge_Allowance` with:
```csharp
        [TestMethod]
        public void CutRect_Equals_Raw_Width()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.AreEqual(0.0, layout.CutRect.X0, 0.001);
            Assert.AreEqual(2240.0, layout.CutRect.X1, 0.001);
            Assert.AreEqual(4300.0, layout.CutRect.Y1, 0.001);
            Assert.AreEqual(2150.0, layout.FoldMidlineY, 0.001);
        }
```

Update the remaining three tests that construct the calculator (`No_Cop_When_Disabled`, `IdentifierLabel_Concatenates_Project_And_Suffix`, `Width_Dimension_Extension_Points_On_Bottom_Edge`, `Layout_Offsets_Cop_X_By_OriginX`) — change every `edgeAllowanceMm: 10` to `quiltInsetMm: 5`, and update the expected numbers:
- `Width_Dimension...`: `Ext2X` expected `2240.0` (was 2250).
- `Layout_Offsets_Cop_X_By_OriginX` (originX 1000): `CutRect.X1` → `3240.0`; `CopRect.X0` → `1600.0`; `CopRect.X1` → `1840.0`; Y unchanged (600 / 1900).

- [ ] **Step 3: Run the calculator tests**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: still FAILS — `QuiltingTests` not yet updated (Task 3). Calculator tests in `LiftBlanketCalculatorTests` should compile; quilting ones still reference old numbers.

### Task 3: Rework quilting clearance to the new inset + bounds rule

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs` (the `AddQuiltLines` method)
- Test: `CanvasCovers.Tests/QuiltingTests.cs`

- [ ] **Step 1: Rewrite AddQuiltLines bounds**

Replace the body of `AddQuiltLines` (keep the signature param name change `half` → `inset`):

```csharp
        private void AddQuiltLines(
            WallLayout layout, WallDimensions wall, double originX,
            double cutWidth, double foldMidline, double inset,
            double verticalSpacing)
        {
            double bottom = inset;
            double top = foldMidline;
            if (top <= bottom) return;

            // Horizontal lines: full width minus the inset clearance.
            double hLeft = originX + inset;
            double hRight = originX + cutWidth - inset;
            if (hRight > hLeft)
            {
                foreach (double y in EvenlySpaced(bottom, top, verticalSpacing))
                {
                    layout.QuiltLines.Add(new LineSpec(hLeft, y, hRight, y));
                }
            }

            // Vertical lines: a line on each non-zero door-return boundary
            // (measured from the true cut edge now there is no padding), plus
            // even-fill between the bounds. When a door return is zero the
            // bound sits one inset in from the edge so no line lands on it.
            double drLeft = wall.Segments.DoorReturnLeft;
            double drRight = wall.Segments.DoorReturnRight;
            double leftBound = drLeft > 0 ? originX + drLeft : originX + inset;
            double rightBound = drRight > 0 ? originX + cutWidth - drRight
                                            : originX + cutWidth - inset;
            if (rightBound <= leftBound) return;

            if (drLeft > 0)
                layout.QuiltLines.Add(new LineSpec(leftBound, bottom, leftBound, top));
            if (drRight > 0)
                layout.QuiltLines.Add(new LineSpec(rightBound, bottom, rightBound, top));

            foreach (double x in EvenlySpaced(leftBound, rightBound, verticalSpacing))
            {
                layout.QuiltLines.Add(new LineSpec(x, bottom, x, top));
            }
        }
```

- [ ] **Step 2: Update the quilting tests with new numbers**

In `QuiltingTests.cs`, change every `new LiftBlanketCalculator(fixingAllowanceMm: 50, edgeAllowanceMm: 10)` to `(fixingAllowanceMm: 50, quiltInsetMm: 5)`.

`Horizontal_Quilt_Lines_Span_Full_Width_Minus_Clearance` — new edges 5..2235:
```csharp
                Assert.AreEqual(5.0, h.X0, 0.001, "horizontal left edge = inset");
                Assert.AreEqual(2235.0, h.X1, 0.001, "horizontal right edge = cutWidth - inset");
                Assert.IsTrue(h.Y0 >= 5 - 0.001 && h.Y0 <= 2150 + 0.001, "horizontal Y in [inset, fold]");
```

`Vertical_Quilt_Includes_DoorReturn_Boundary_Lines` — DR-L line now at 250, no DR-R line, verticals span 5..2150:
```csharp
            Assert.IsTrue(verticals.Any(v => System.Math.Abs(v.X0 - 250.0) < 0.001),
                "expected a vertical quilt line on the DR-L boundary (250)");
            Assert.IsFalse(verticals.Any(v => System.Math.Abs(v.X0 - 2235.0) < 0.001),
                "DR-R is 0 so no DR-R boundary line should be drawn");

            foreach (LineSpec v in verticals)
            {
                Assert.AreEqual(5.0, v.Y0, 0.001, "vertical bottom = inset");
                Assert.AreEqual(2150.0, v.Y1, 0.001, "vertical top = fold midline");
            }
```

`Vertical_Quilt_Draws_Both_DoorReturn_Lines_When_Both_Present` — DR-R=100, width 2340:
```csharp
            double cutWidth = 250 + 350 + 240 + 1400 + 100;  // 2340, no edge boost
            double drLeftLine = 250;                          // true edge + DR-L
            double drRightLine = cutWidth - 100;              // 2240
```

- [ ] **Step 3: Run the full suite green**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: `Passed! - Failed: 0`. (Total may differ from 29 by the renamed tests; all green.)

### Task 4: Wire the new option through the generator + window

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs:66-67`
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` (the Edge Allowance grid)
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`
- Modify: `CanvasCovers/UI/Controls/WallBlanket.xaml.cs:104-109` (SetSharedParams signature)

- [ ] **Step 1: Generator passes the inset**

In `LiftBlanketGenerator.Generate`, change the calc construction:
```csharp
            var calc = new LiftBlanketCalculator(
                job.Options.FixingAllowanceMm, job.Options.QuiltInsetMm);
```

- [ ] **Step 2: Rename the window field in XAML**

In `LiftBlanketWindow.xaml`, change the Edge Allowance grid (the one with `EdgeAllowanceInput`):
```xml
                            <TextBlock Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Quilt Inset (mm)" />
                            <TextBox Grid.Column="1" Name="QuiltInsetInput" Text="5" TextChanged="SharedParam_Changed" />
                            <TextBlock Grid.Column="2" Style="{StaticResource MutedLabel}"
                                       Text="Distance the quilting lines are pulled in from the outline so the pen clears the cut edge. The outline is always the entered size." />
```

- [ ] **Step 3: Update the window code-behind**

In `LiftBlanketWindow.xaml.cs`:

`PushSharedParams` — change the edge line:
```csharp
            double inset = ParseOr(QuiltInsetInput.Text, 5);
```
and pass `inset` where `edge` was passed to `SetSharedParams`.

`ReadOptions` — replace the `EdgeAllowanceMm = ...` line:
```csharp
                QuiltInsetMm = ParseOr(QuiltInsetInput.Text, 5),
```

`ReadBlanket` COP overflow check — remove the edge-allowance math. Replace the block that computes `half`, `copRight`, `cutWidth`:
```csharp
                if (wall.Segments.Seg2 > 0)
                {
                    double copRight = wall.Segments.DoorReturnLeft + wall.Segments.Seg1 + wall.Segments.Seg2;
                    double cutWidth = wall.Segments.TotalWidth;
                    if (copRight > cutWidth + 0.001)
                        errors.Add(wallLabel +
                            " COP extends past the right edge — reduce segment 1 or the COP width (segment 2).");
                }
```

- [ ] **Step 4: Update SetSharedParams signature**

In `WallBlanket.xaml.cs`, rename the ignored param for clarity:
```csharp
        public void SetSharedParams(double fixingAllowance, double quiltInset,
            double quiltSpacing, bool quiltEnabled)
        {
            _fixingAllowance = fixingAllowance;
            Redraw();
        }
```

- [ ] **Step 5: Commit**

Run:
```
dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo
git add -A
git commit -m "feat: outline = entered width; quilt inset its own field (items 10+11)"
```
Expected: tests green before commit.

- [ ] **Step 6: Live check** (build + install per the steps at top)

In DraftSight, generate the reference wall. Confirm: the "Quilt Inset (mm)" field shows (default 5); the outline width equals the typed total (no +10); quilting lines sit 5mm inside the outline; COP sits correctly. No host crash.

---

## Phase 2: Quilt spacing refinement (item 13)

Martin: the first line at the returns and the return behaviour stay exactly as they are; only the internal distribution is being tidied (even, no line on the edge). The current `EvenlySpaced` + the Task 3 bounds already produce "even interior lines, none on the edge, first line ~one gap in from each bound." Treat this phase as **lock-in-with-tests**, changing code only if a specific defect is identified.

### Task 5: Pin the internal-spacing rule with tests

**Files:**
- Test: `CanvasCovers.Tests/QuiltingTests.cs`

- [ ] **Step 1: Add a test asserting no quilt line lands on an outline edge**

```csharp
        [TestMethod]
        public void No_Quilt_Line_Sits_On_An_Outline_Edge()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            double cutWidth = 2240, fold = 2150;
            foreach (LineSpec l in layout.QuiltLines)
            {
                // No vertical on the left/right outline; no horizontal on bottom/fold.
                Assert.IsFalse(System.Math.Abs(l.X0 - 0) < 0.001 && System.Math.Abs(l.X1 - 0) < 0.001);
                Assert.IsFalse(System.Math.Abs(l.X0 - cutWidth) < 0.001 && System.Math.Abs(l.X1 - cutWidth) < 0.001);
                Assert.IsFalse(System.Math.Abs(l.Y0 - 0) < 0.001 && System.Math.Abs(l.Y1 - 0) < 0.001);
            }
        }
```

- [ ] **Step 2: Add a test that interior gaps are even**

```csharp
        [TestMethod]
        public void Interior_Horizontal_Gaps_Are_Even()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            var ys = layout.QuiltLines
                .Where(l => System.Math.Abs(l.Y0 - l.Y1) < 0.001)
                .Select(l => l.Y0).OrderBy(y => y).ToList();
            for (int i = 2; i < ys.Count; i++)
                Assert.AreEqual(ys[1] - ys[0], ys[i] - ys[i - 1], 0.01, "even vertical gaps");
        }
```

- [ ] **Step 3: Run; act on result**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: PASS (confirms the current rule already satisfies item 13). If either FAILS, that is the concrete defect — fix `AddQuiltLines`/`EvenlySpaced` minimally and re-run. **If Martin later names a different specific defect, add a test for it here first.**

- [ ] **Step 4: Commit**

```
git add CanvasCovers.Tests/QuiltingTests.cs CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs
git commit -m "test: lock quilt internal-spacing rule; no line on edge (item 13)"
```

---

## Phase 3: Total-width input when COP is off (item 5)

When "Include COP" is unticked, the five segment boxes collapse to a single total width. Mapping: the total goes into `Seg1`, the other four segments are 0 (exactly how the Rear wall already works). No calculator change needed — `Width = Segments.TotalWidth` already covers it.

### Task 6: Collapse the L/R sheet to a single width when COP is off

**Files:**
- Modify: `CanvasCovers/UI/Controls/WallBlanket.xaml.cs`

- [ ] **Step 1: Drive a redraw when the COP checkbox toggles**

`IncludeCop` already raises `Input_Changed` → `Redraw()`. Confirm no change needed there.

- [ ] **Step 2: Branch the L/R draw on COP state**

In `RedrawCore`, change the non-rear branch:
```csharp
            if (_isRear) DrawRearSheet(cw, ch);
            else if (!CopEnabled) DrawSingleWidthSheet(cw, ch);
            else DrawWallSheet(cw, ch);
```

- [ ] **Step 3: Add DrawSingleWidthSheet (mirrors DrawRearSheet but keeps the COP toggle visible)**

Add this method next to `DrawRearSheet`:
```csharp
        // Left/Right wall with COP OFF: a plain rectangle with a single Width +
        // Height, identical handling to the rear wall. The total width is stored
        // in Seg1 (the other segment getters return "0"), so the calculator sees
        // a plain rectangle of the entered width.
        private void DrawSingleWidthSheet(double cw, double ch)
        {
            HideField(_drLeft); HideField(_seg2); HideField(_seg3); HideField(_drRight);
            HideField(_copHeight); HideField(_copGapBottom);

            double left = 90, right = cw - 110;
            double top = 56, bottom = ch - 96;
            if (right - left < 120 || bottom - top < 120) return;

            double availH = bottom - top;
            double maxW = availH * 1.20;
            double wWall = Math.Min(right - left, maxW);
            double cx0 = (left + right) / 2;
            left = cx0 - wWall / 2;
            right = cx0 + wWall / 2;
            double w = right - left, h = bottom - top;

            AddRect(left, top, w, h, WallStroke, 2);

            double dimY = bottom + 14;
            AddHDim(left, right, dimY);
            PlaceLabeledFieldBelow(_seg1, (left + right) / 2 - FieldW / 2, bottom + 26);

            AddVDim(right + 18, top, bottom);
            PlaceLabeledField(_measuredHeight, right + 30, top + h / 2 - FieldH / 2);
        }
```

- [ ] **Step 4: Make Seg1's label read "Width" when COP is off**

In `RedrawCore`, before the branch, keep the label correct:
```csharp
            _labels[_seg1].Text = (_isRear || !CopEnabled) ? "Width" : "S1";
```

- [ ] **Step 5: Ensure the segment getters zero-out when COP is off**

In `WallBlanket.xaml.cs`, change the L/R segment getters so an off-COP wall reports a plain rectangle (Seg1 = the single width):
```csharp
        public string DrLeftText => (_isRear || !CopEnabled) ? "0" : _drLeft.Text;
        public string Seg1Text => _seg1.Text;
        public string Seg2Text => (_isRear || !CopEnabled) ? "0" : _seg2.Text;
        public string Seg3Text => (_isRear || !CopEnabled) ? "0" : _seg3.Text;
        public string DrRightText => (_isRear || !CopEnabled) ? "0" : _drRight.Text;
```

- [ ] **Step 6: Commit + Live check**

```
git add CanvasCovers/UI/Controls/WallBlanket.xaml.cs
git commit -m "feat: single total-width input when COP is off (item 5)"
```
**Live check:** untick "Include COP cutout" on the Left wall → the five boxes collapse to one "Width" box; generate → a plain rectangle of that width with correct quilting; re-tick → segments return. No crash.

---

## Phase 4: Project fields + blanket text + filename (items 1, 2, 3)

### Task 7: Add the blanket-text builder (headless, TDD)

**Files:**
- Create: `CanvasCovers/Models/Products/LiftBlanket/BlanketText.cs`
- Modify: `CanvasCovers/Models/ProjectMetadata.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (link the new file)
- Test: `CanvasCovers.Tests/BlanketTextTests.cs` (create)

- [ ] **Step 1: Add the new metadata fields**

In `ProjectMetadata.cs`, add (keep `OrderNumber`, `CompanyName`, `NetworkNumber`, `ProjectName`, `Date`):
```csharp
        // AAC's customer+depot code, e.g. Kone Melbourne = KM. Middle section
        // of the blanket text.
        public string CompanyInitials { get; set; }
```
Remove the `SalesContact`, `MeasuredBy`, `Mobile`, and `Notes` properties.

- [ ] **Step 2: Create the builder**

`BlanketText.cs`:
```csharp
using System.Linq;

namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The text stamped on every blanket AND used as the export filename:
    // "<AAC order number> <company initials> <network number>" with single
    // spaces, empty sections dropped.
    public static class BlanketText
    {
        public static string Build(string orderNumber, string companyInitials, string networkNumber)
        {
            string[] parts = { orderNumber, companyInitials, networkNumber };
            return string.Join(" ",
                parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        public static string Build(ProjectMetadata p)
        {
            if (p == null) return string.Empty;
            return Build(p.OrderNumber, p.CompanyInitials, p.NetworkNumber);
        }
    }
}
```

- [ ] **Step 3: Link the builder into the test project**

In `CanvasCovers.Tests.csproj`, add inside the linked-files `ItemGroup`:
```xml
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\BlanketText.cs" Link="Linked\BlanketText.cs" />
```

- [ ] **Step 4: Write the tests**

`BlanketTextTests.cs`:
```csharp
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class BlanketTextTests
    {
        [TestMethod]
        public void Joins_Three_Sections_With_Spaces()
        {
            Assert.AreEqual("AAC123 KM 45678",
                BlanketText.Build("AAC123", "KM", "45678"));
        }

        [TestMethod]
        public void Drops_Empty_Sections()
        {
            Assert.AreEqual("AAC123 45678", BlanketText.Build("AAC123", "", "45678"));
            Assert.AreEqual("KM", BlanketText.Build(null, " KM ", null));
        }
    }
}
```

- [ ] **Step 5: Run**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: PASS.

### Task 8: Use the blanket text for the export filename (item 3)

**Files:**
- Modify: `CanvasCovers/IO/DxfExporter.Filename.cs`
- Test: `CanvasCovers.Tests/DxfFilenameTests.cs`

- [ ] **Step 1: Update the filename test**

Replace `Uses_Network_Number_When_Present` and `Strips_Invalid_Filename_Chars`:
```csharp
        [TestMethod]
        public void Uses_Blanket_Text_When_Present()
        {
            var meta = new ProjectMetadata { OrderNumber = "AAC123", CompanyInitials = "KM", NetworkNumber = "45678" };
            Assert.AreEqual("AAC123 KM 45678.dxf", DxfExporter.DefaultFileName(meta));
        }

        [TestMethod]
        public void Strips_Invalid_Filename_Chars()
        {
            var meta = new ProjectMetadata { OrderNumber = "AA/C1", CompanyInitials = "K:M", NetworkNumber = "4*5" };
            Assert.AreEqual("AAC1 KM 45.dxf", DxfExporter.DefaultFileName(meta));
        }
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --filter DxfFilenameTests --nologo`
Expected: FAIL (still uses network number only).

- [ ] **Step 3: Update the filename builder**

In `DxfExporter.Filename.cs`, replace the body of `DefaultFileName`:
```csharp
        public static string DefaultFileName(ProjectMetadata project)
        {
            string raw = CanvasCovers.Models.Products.LiftBlanket.BlanketText.Build(project);
            // Keep spaces between sections; strip only filesystem-illegal chars.
            string cleaned = new string(raw.Where(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ').ToArray()).Trim();

            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "CanvasCovers-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            }
            return cleaned + ".dxf";
        }
```

- [ ] **Step 4: Run green + commit**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
```
git add -A
git commit -m "feat: blanket-text builder + filename from order/initials/network (items 2,3)"
```

### Task 9: Update the metadata panel UI (item 1)

**Files:**
- Modify: `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml`
- Modify: `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml.cs`

- [ ] **Step 1: Rework the panel XAML**

Replace the grid rows so the fields are: Company Name, Company Initials, Network Number, AAC Order Number, Project Name, Date. Remove Sales Contact, Mobile, Measured By, Notes. Replace the `<Grid>...</Grid>` body of `ProjectMetadataPanel.xaml` with:
```xml
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="120" />
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="20" />
            <ColumnDefinition Width="120" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Company Name" />
        <TextBox Grid.Row="0" Grid.Column="1" Name="CompanyNameInput" />
        <TextBlock Grid.Row="0" Grid.Column="3" Style="{StaticResource FieldLabel}" Text="Company Initials" />
        <TextBox Grid.Row="0" Grid.Column="4" Name="CompanyInitialsInput" ToolTip="AAC customer+depot code, e.g. Kone Melbourne = KM" />

        <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="AAC Order Number" />
        <TextBox Grid.Row="1" Grid.Column="1" Name="OrderNumberInput" />
        <TextBlock Grid.Row="1" Grid.Column="3" Style="{StaticResource FieldLabel}" Text="Network Number" />
        <TextBox Grid.Row="1" Grid.Column="4" Name="NetworkNumberInput" />

        <TextBlock Grid.Row="2" Grid.Column="0" Style="{StaticResource FieldLabel}" Text="Project Name" />
        <TextBox Grid.Row="2" Grid.Column="1" Name="ProjectNameInput" />
        <TextBlock Grid.Row="2" Grid.Column="3" Style="{StaticResource FieldLabel}" Text="Date" />
        <DatePicker Grid.Row="2" Grid.Column="4" Name="DateInput" Height="26" Margin="0,2,0,2" />
    </Grid>
```

- [ ] **Step 2: Update Read/Apply**

In `ProjectMetadataPanel.xaml.cs`, replace `Read` and `Apply` bodies:
```csharp
        public ProjectMetadata Read()
        {
            return new ProjectMetadata
            {
                CompanyName = Trim(CompanyNameInput.Text),
                CompanyInitials = Trim(CompanyInitialsInput.Text),
                NetworkNumber = Trim(NetworkNumberInput.Text),
                OrderNumber = Trim(OrderNumberInput.Text),
                ProjectName = Trim(ProjectNameInput.Text),
                Date = DateInput.SelectedDate,
            };
        }

        public void Apply(ProjectMetadata data)
        {
            if (data == null) return;
            CompanyNameInput.Text = data.CompanyName ?? string.Empty;
            CompanyInitialsInput.Text = data.CompanyInitials ?? string.Empty;
            NetworkNumberInput.Text = data.NetworkNumber ?? string.Empty;
            OrderNumberInput.Text = data.OrderNumber ?? string.Empty;
            ProjectNameInput.Text = data.ProjectName ?? string.Empty;
            DateInput.SelectedDate = data.Date;
        }
```

- [ ] **Step 3: Update the generator's title-block lines (item 1)**

In `LiftBlanketGenerator.DrawProjectAnnotations`, replace the `lines` list initialiser through the NOTES block with:
```csharp
            List<string> lines = new List<string>
            {
                "COMPANY - " + Display(job.Project.CompanyName),
                "COMPANY INITIALS - " + Display(job.Project.CompanyInitials),
                "AAC ORDER NO - " + Display(job.Project.OrderNumber),
                "NETWORK NO - " + Display(job.Project.NetworkNumber),
                "PROJECT NAME - " + Display(job.Project.ProjectName),
                "DATE - " + dateStr,
                string.Empty,
                "FIXINGS REQUIRED - " + FixingLabel(job.Options.Fixings).ToUpperInvariant(),
                "FIXING ALLOWANCE - -" + job.Options.FixingAllowanceMm.ToString(CultureInfo.InvariantCulture),
                "THROUGH CAR - " + YesNo(job.Options.ThroughCar),
                "PLASTIC COVER ON COP - " + YesNo(job.Options.PlasticCoverOnCop),
            };
```
Delete the `string notes = ...` block that follows (Notes field is gone).

- [ ] **Step 4: Build the add-in to catch any remaining references**

The full add-in build happens via `Installer\build.ps1`, but first confirm no other code references the removed properties:
Run: `git grep -n "SalesContact\|MeasuredBy\|\.Mobile\|\.Notes"`
Expected: no hits in `CanvasCovers/` source (only this plan / docs). Fix any stragglers.

- [ ] **Step 5: Commit + Live check**

```
git add -A
git commit -m "feat: trim project fields, add Company Initials, relabel AAC Order No (item 1)"
```
**Live check:** the Project Information panel shows exactly Company Name, Company Initials, Network Number, AAC Order Number, Project Name, Date. Generate → title block shows the new lines; no removed fields.

### Task 10: Print the blanket text on each wall (item 3 content)

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` (`BuildProjectTag`)

- [ ] **Step 1: Replace BuildProjectTag with the blanket text**

In `LiftBlanketGenerator.cs`, replace `BuildProjectTag`:
```csharp
        private static string BuildProjectTag(ProjectMetadata project)
        {
            // Per-wall label is the blanket text: "<order> <initials> <network>".
            return CanvasCovers.Models.Products.LiftBlanket.BlanketText.Build(project);
        }
```
(The calculator already appends the ` L`/`R`/`B` suffix to this tag — DEFAULT keeps the suffix. To drop the suffix, change the calculator's `IdentifierLabel.Text` to omit `suffix`.)

- [ ] **Step 2: Commit**

```
git add CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs
git commit -m "feat: per-wall label uses blanket text (item 3)"
```
(Position/rotation handled in Phase 6, Task 14.)

---

## Phase 5: Fixings (items 6, 7, 8)

### Task 11: Add the Eyelet (+ self-adhesive Velcro) fixing types

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs`
- Test: `CanvasCovers.Tests/FixingAllowanceTests.cs`

- [ ] **Step 1: Write the failing tests**

In `FixingAllowanceTests.cs`, add:
```csharp
        [TestMethod]
        public void Eyelet_Default_Is_30()
        {
            Assert.AreEqual(30.0, FixingAllowance.DefaultFor(FixingType.Eyelet));
        }

        [TestMethod]
        public void SelfAdhesiveVelcro_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.SelfAdhesiveVelcro));
        }
```

- [ ] **Step 2: Run to confirm it fails to compile**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: FAIL — `FixingType.Eyelet` / `SelfAdhesiveVelcro` don't exist.

- [ ] **Step 3: Add the enum members + allowance cases**

In `FixingAllowance.cs`, extend the enum:
```csharp
    public enum FixingType
    {
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
        Eyelet,
        SelfAdhesiveVelcro,
    }
```
And add cases (remove the silent `default → 0` reliance):
```csharp
                case FixingType.PressStuds:
                    return 40.0;
                case FixingType.Eyelet:
                    return 30.0;
                case FixingType.Velcro:
                case FixingType.SelfAdhesiveVelcro:
                    return 0.0;
                default:
                    return 0.0;
```

- [ ] **Step 4: Run green + commit**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
```
git add CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs CanvasCovers.Tests/FixingAllowanceTests.cs
git commit -m "feat: add Eyelet (30) and self-adhesive Velcro (0) fixing types (items 6,7)"
```

### Task 12: Surface the new fixings + show allowance in the dropdown (items 7, 8)

**Files:**
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` (`FixingsInput`)
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` (`FixingLabel`)

- [ ] **Step 1: Update the dropdown items with allowance suffixes**

In `LiftBlanketWindow.xaml`, replace the `FixingsInput` items:
```xml
                            <ComboBoxItem Content="Velcro (0)" Tag="Velcro" />
                            <ComboBoxItem Content="Self-adhesive Velcro (0)" Tag="SelfAdhesiveVelcro" />
                            <ComboBoxItem Content="Hooks Facing In (-50)" Tag="HooksFacingIn" />
                            <ComboBoxItem Content="Hooks Facing Out (-50)" Tag="HooksFacingOut" />
                            <ComboBoxItem Content="Press Studs (-40)" Tag="PressStuds" />
                            <ComboBoxItem Content="Eyelet (-30)" Tag="Eyelet" />
```
Keep `SelectedIndex="2"` (Hooks Facing In) as the default, or set to taste.

- [ ] **Step 2: Add the Eyelet case to the generator's label helper**

In `LiftBlanketGenerator.FixingLabel`:
```csharp
                case FixingType.Velcro: return "Velcro";
                case FixingType.SelfAdhesiveVelcro: return "Self-adhesive Velcro";
                case FixingType.HooksFacingIn: return "Hooks Facing In";
                case FixingType.HooksFacingOut: return "Hooks Facing Out";
                case FixingType.PressStuds: return "Press Studs";
                case FixingType.Eyelet: return "Eyelet";
                default: return fixing.ToString();
```

- [ ] **Step 3: Commit + Live check**

```
git add -A
git commit -m "feat: show fixing allowance in dropdown text; new types selectable (item 8)"
```
**Live check:** dropdown lists all six with allowance suffixes; selecting Eyelet auto-fills the allowance box with 30; override still works.

---

## Phase 6: Drawing output (items 12, 14, 15, 18)

### Task 13: Add Angle to the label spec + a BagRequired / GlassBehind option

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs` (`LabelSpec`)
- Modify: `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs`

- [ ] **Step 1: Add Angle to LabelSpec**

In `WallLayout.cs`, extend `LabelSpec`:
```csharp
    public struct LabelSpec
    {
        public double X, Y, Height, Angle;
        public string Text;
    }
```

- [ ] **Step 2: Add the two new options**

In `LiftBlanketOptions.cs`, add:
```csharp
        // Stamp a "BAG" reminder inside the COP cutout when a storage bag is
        // required (item 14). Drawn vertical to fit the cutout.
        public bool BagRequired { get; set; }

        // Stamp a glass-behind label (item 15).
        public bool GlassBehind { get; set; }
```

- [ ] **Step 3: Commit**

```
git add -A
git commit -m "chore: LabelSpec.Angle + BagRequired/GlassBehind options (items 12,14,15)"
```

### Task 14: Move the blanket text to bottom-centre, inverted (item 12) — TDD

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` (`DrawWall`)
- Test: `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs`

- [ ] **Step 1: Write the failing test**

In `LiftBlanketCalculatorTests.cs`:
```csharp
        [TestMethod]
        public void Identifier_Is_Bottom_Centre_Inverted()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "AAC1 KM 45678", "L");
            Assert.IsTrue(layout.IdentifierLabel.HasValue);
            LabelSpec lab = layout.IdentifierLabel.Value;
            Assert.AreEqual(1120.0, lab.X, 0.001);     // cutWidth/2 = 2240/2
            Assert.AreEqual(4275.0, lab.Y, 0.001);     // cutHeight - 25
            Assert.AreEqual(180.0, lab.Angle, 0.001);
        }
```

- [ ] **Step 2: Run to fail**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --filter Identifier_Is_Bottom_Centre_Inverted --nologo`
Expected: FAIL (Y is currently cutHeight/2, Angle 0).

- [ ] **Step 3: Update the calculator's IdentifierLabel**

In `LiftBlanketCalculator.cs`, add a constant near `IdentifierTextHeight`:
```csharp
        // Blanket text sits this far down from the top edge (becomes the bottom
        // when folded), fixed so it doesn't move with width. CLAUDE.md item 12.
        private const double IdentifierTopGap = 25.0;
```
Replace the `layout.IdentifierLabel = new LabelSpec { ... }` block:
```csharp
                layout.IdentifierLabel = new LabelSpec
                {
                    X = originX + cutWidth / 2.0,
                    Y = cutHeight - IdentifierTopGap,
                    Height = IdentifierTextHeight,
                    Angle = 180.0,
                    Text = (string.IsNullOrEmpty(projectTag) ? "" : projectTag + " ") + suffix,
                };
```
(Note: `cutHeight` is already computed in `LayoutWall`; if it's only a local inside the block, hoist the existing `double cutHeight = ...` above this usage — it is computed at the top of `LayoutWall`, so it's in scope.)

- [ ] **Step 4: Pass the angle through the generator**

In `LiftBlanketGenerator.DrawWall`, change the identifier insert:
```csharp
                SimpleNote note = sketch.InsertSimpleNote(lab.X, lab.Y, 0, lab.Height, lab.Angle, lab.Text);
```

- [ ] **Step 5: Run green + commit + Live check**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
```
git add -A
git commit -m "feat: blanket text bottom-centre, inverted 180, fixed offset (item 12)"
```
**Live check:** generate → blanket text reads upside-down ~25mm from the top edge of each panel, centred, not shifting with width.

### Task 15: Reminders inside the COP cutout, vertical (item 14) + glass-behind (item 15) — TDD

**Files:**
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/WallLayout.cs` (add a reminder list)
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketCalculator.cs`
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs`
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` + `.xaml.cs`
- Test: `CanvasCovers.Tests/LiftBlanketCalculatorTests.cs`

- [ ] **Step 1: Add a reminder list to WallLayout**

In `WallLayout.cs`, add:
```csharp
        // Reminder labels drawn inside the COP cutout (vertical), e.g. "BAG"
        // and the fixing type. Empty when no COP or none requested.
        public List<LabelSpec> CopReminders = new List<LabelSpec>();
```

- [ ] **Step 2: Extend LayoutWall to accept reminder text and place it vertical in the COP**

Add an optional parameter to `LayoutWall`:
```csharp
        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix,
            bool quiltingEnabled = false,
            double verticalQuiltingSpacingMm = 0,
            System.Collections.Generic.IEnumerable<string> copReminders = null)
```
After the COP rect is built (inside `if (wall.Cop.Enabled)`), append:
```csharp
                if (copReminders != null)
                {
                    double rx = copX0 + copWidth / 2.0;
                    double ry = copY0 + wall.Cop.Height / 2.0;
                    foreach (string text in copReminders)
                    {
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        layout.CopReminders.Add(new LabelSpec
                        {
                            X = rx, Y = ry, Height = IdentifierTextHeight,
                            Angle = 90.0, Text = text,
                        });
                        rx += IdentifierTextHeight * 1.4; // stack columns across the cutout
                    }
                }
```

- [ ] **Step 3: Write the test**

```csharp
        [TestMethod]
        public void Cop_Reminders_Are_Vertical_Inside_The_Cop()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "AAC1 KM 45678", "L",
                copReminders: new[] { "BAG", "EYELET" });
            Assert.AreEqual(2, layout.CopReminders.Count);
            foreach (LabelSpec r in layout.CopReminders)
            {
                Assert.AreEqual(90.0, r.Angle, 0.001);
                Assert.IsTrue(r.X >= 600 && r.X <= 840, "reminder X inside COP");
                Assert.IsTrue(r.Y >= 600 && r.Y <= 1900, "reminder Y inside COP");
            }
        }
```

- [ ] **Step 4: Run to fail, then it passes once Step 2 compiles**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --filter Cop_Reminders_Are_Vertical_Inside_The_Cop --nologo`
Expected: PASS after Steps 1-2 compile (write the test first; if you do Step 3 before Step 2, it fails to compile — that's the red).

- [ ] **Step 5: Build the reminder list in the generator and draw it**

In `LiftBlanketGenerator.Generate`, where `LayoutWall` is called, build and pass the reminders:
```csharp
                        var reminders = new List<string>();
                        if (job.Options.BagRequired) reminders.Add("BAG");
                        reminders.Add(FixingLabel(job.Options.Fixings).ToUpperInvariant());
                        if (job.Options.GlassBehind) reminders.Add("GLASS BEHIND");

                        WallLayout layout = calc.LayoutWall(
                            pair.Wall, cursorX, projectTag, pair.Suffix,
                            job.Options.QuiltingEnabled,
                            job.Options.VerticalQuiltingSpacingMm,
                            reminders);
```
In `DrawWall`, after the COP rectangle block, draw the reminders on the draw layer:
```csharp
            if (layout.CopReminders.Count > 0)
            {
                layers.Activate(_layerSettings.Cop.Name);
                foreach (LabelSpec rem in layout.CopReminders)
                {
                    SimpleNote n = sketch.InsertSimpleNote(rem.X, rem.Y, 0, rem.Height, rem.Angle, rem.Text);
                    if (n != null) n.Justify = dsTextJustification_e.dsTextJustification_Middle;
                    else FailedInsertCount++;
                }
            }
```

- [ ] **Step 6: Add the two checkboxes to the OPTIONS panel**

In `LiftBlanketWindow.xaml`, after `PlasticCoverOption`:
```xml
                            <CheckBox Name="BagRequiredOption" Content="Storage bag required (BAG)" />
                            <CheckBox Name="GlassBehindOption" Content="Glass behind" />
```
In `ReadOptions`, add:
```csharp
                BagRequired = BagRequiredOption.IsChecked == true,
                GlassBehind = GlassBehindOption.IsChecked == true,
```

- [ ] **Step 7: Run green + commit + Live check**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
```
git add -A
git commit -m "feat: vertical COP-cutout reminders (BAG/fixing) + glass-behind (items 14,15)"
```
**Live check:** tick "Storage bag required" → "BAG", the fixing name (and "GLASS BEHIND" if ticked) appear vertical inside the COP cutout, fitting within it.

### Task 16: Confirm dimensions match the template (item 18)

**Files:** verification only (no code unless a gap is found).

- [ ] **Step 1: Compare emitted dims to the template drawing**

The generator emits a width dim per wall and one height dim on the leftmost wall ([LiftBlanketGenerator.cs DrawWall]). Open a client template DXF and list its dimensions.
- If the template shows ONLY width + height → no change; item 18 is satisfied.
- If it shows more (e.g. COP height/gap) → add those as `DimSpec`s in `LayoutWall` with a test mirroring `Width_Dimension_Extension_Points_On_Bottom_Edge`, then draw them in `DrawWall`.

- [ ] **Step 2: Commit only if changed**

```
git add -A
git commit -m "feat: add template COP dimensions (item 18)"
```

---

## Phase 7: Conveniences (items 4, 16)

### Task 17: Seed right/rear height from the left wall (item 4)

**Files:**
- Modify: `CanvasCovers/UI/Controls/WallBlanket.xaml.cs`
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`

- [ ] **Step 1: Expose a height-changed signal + a seed method on WallBlanket**

In `WallBlanket.xaml.cs`, add:
```csharp
        // Raised when the measured-height field changes (used to seed the other
        // walls from the left wall — item 4).
        public event System.EventHandler HeightChanged;

        public string HeightText => _measuredHeight.Text;

        // Seed this wall's height ONLY if its field is currently empty, so a
        // value the operator already typed is never overwritten.
        public void SeedHeightIfEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(_measuredHeight.Text))
                _measuredHeight.Text = value;
        }
```
In the `MakeField` wiring, the height box already calls `Input_Changed`. Add a raise: in `Input_Changed`, after `Redraw()`:
```csharp
        private void Input_Changed(object sender, RoutedEventArgs e)
        {
            Redraw();
            if (sender == _measuredHeight) HeightChanged?.Invoke(this, System.EventArgs.Empty);
        }
```

- [ ] **Step 2: Wire left → right/rear in the window**

In `LiftBlanketWindow_Loaded` (after `Configure` calls), subscribe:
```csharp
            LeftBlanket.HeightChanged += (s, e) =>
            {
                RightBlanket.SeedHeightIfEmpty(LeftBlanket.HeightText);
                RearBlanket.SeedHeightIfEmpty(LeftBlanket.HeightText);
            };
```

- [ ] **Step 3: Commit + Live check**

```
git add -A
git commit -m "feat: seed right/rear height from left wall as placeholder (item 4)"
```
**Live check:** type a Left height with Right/Rear empty → both fill with the same value; edit Right → Left unaffected; change Left again → Right (now non-empty) is NOT overwritten.

### Task 18: Warn on left/right width mismatch (item 16) — TDD helper + UI

**Files:**
- Create: `CanvasCovers/Models/Products/LiftBlanket/WallChecks.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (link it)
- Test: `CanvasCovers.Tests/WallChecksTests.cs`
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`

- [ ] **Step 1: Create the testable helper**

`WallChecks.cs`:
```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    public static class WallChecks
    {
        // True when both walls are present and their total widths differ by more
        // than tolerance. Cars are square, so a mismatch is usually a data error
        // — but it is only a WARNING (an angled-COP lift legitimately differs).
        public static bool WidthsMismatch(bool leftEnabled, double leftWidth,
            bool rightEnabled, double rightWidth, double toleranceMm = 1.0)
        {
            if (!leftEnabled || !rightEnabled) return false;
            if (leftWidth <= 0 || rightWidth <= 0) return false;
            return System.Math.Abs(leftWidth - rightWidth) > toleranceMm;
        }
    }
}
```

- [ ] **Step 2: Link it + write the test**

In `CanvasCovers.Tests.csproj` linked group:
```xml
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\WallChecks.cs" Link="Linked\WallChecks.cs" />
```
`WallChecksTests.cs`:
```csharp
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class WallChecksTests
    {
        [TestMethod]
        public void Mismatch_When_Widths_Differ()
        {
            Assert.IsTrue(WallChecks.WidthsMismatch(true, 2240, true, 2200));
        }

        [TestMethod]
        public void No_Mismatch_When_Equal_Or_A_Wall_Disabled()
        {
            Assert.IsFalse(WallChecks.WidthsMismatch(true, 2240, true, 2240));
            Assert.IsFalse(WallChecks.WidthsMismatch(false, 2240, true, 2200));
            Assert.IsFalse(WallChecks.WidthsMismatch(true, 0, true, 2200));
        }
    }
}
```

- [ ] **Step 3: Run**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: PASS.

- [ ] **Step 4: Show a non-blocking warning in the window**

In `LiftBlanketWindow.xaml.cs` `GenerateButton_Click`, after `right`/`left` are read and BEFORE the `errors.Count > 0` check, add a non-blocking notice that does NOT prevent generation. Append to the existing `ErrorText` only as a prefix warning when there are no hard errors:
```csharp
            bool widthWarn = CanvasCovers.Models.Products.LiftBlanket.WallChecks.WidthsMismatch(
                left.Enabled, left.Segments.TotalWidth, right.Enabled, right.Segments.TotalWidth);
```
After the hard-error block (which returns on errors), and before building `Job`, surface the warning without blocking:
```csharp
            if (widthWarn)
            {
                System.Windows.MessageBox.Show(
                    "Left and right wall widths differ. Cars are usually square, so check this isn't leftover data — but proceed if this lift has an angled COP.",
                    "CanvasCovers — width check", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
```
(MessageBox here is acceptable — it's on the dialog's own button click, not a COM event callback.)

- [ ] **Step 5: Commit + Live check**

```
git add -A
git commit -m "feat: non-blocking left/right width-mismatch warning (item 16)"
```
**Live check:** enter different L/R widths → a warning appears on Generate but the drawing still generates after OK; equal widths → no warning.

---

## Phase 8: Layers (items 19, 20)

### Task 19: Remove the Defpoints layer (item 19) — TDD

**Files:**
- Modify: `CanvasCovers/Models/LayerSettings.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (link LayerSetting + LayerSettings)
- Test: `CanvasCovers.Tests/LayerSettingsTests.cs` (create)

- [ ] **Step 1: Link the layer models into the test project**

In `CanvasCovers.Tests.csproj`, uncomment / add:
```xml
    <Compile Include="..\CanvasCovers\Models\LayerSetting.cs" Link="Linked\LayerSetting.cs" />
    <Compile Include="..\CanvasCovers\Models\LayerSettings.cs" Link="Linked\LayerSettings.cs" />
```

- [ ] **Step 2: Write the failing test**

`LayerSettingsTests.cs`:
```csharp
using System.Linq;
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class LayerSettingsTests
    {
        [TestMethod]
        public void DefaultLayers_Has_No_Defpoints()
        {
            Assert.IsFalse(LayerSettings.DefaultLayers().Any(l => l.Name == "Defpoints"));
        }

        [TestMethod]
        public void DefaultLayers_Keeps_The_Cutting_Tool_Layers()
        {
            var names = LayerSettings.DefaultLayers().Select(l => l.Name).ToList();
            CollectionAssert.Contains(names, "1 Rotary Blade");
            CollectionAssert.Contains(names, "2 Drag Blade");
            CollectionAssert.Contains(names, "3 Crease Tool");
            CollectionAssert.Contains(names, "5 Draw and Text");
        }
    }
}
```

- [ ] **Step 3: Run to fail**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --filter LayerSettingsTests --nologo`
Expected: FAIL on `DefaultLayers_Has_No_Defpoints`.

- [ ] **Step 4: Remove Defpoints**

In `LayerSettings.cs` `DefaultLayers()`, delete the line:
```csharp
                new LayerSetting("Defpoints", 7),
```

- [ ] **Step 5: Run green + commit**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
```
git add -A
git commit -m "feat: drop Defpoints from the default layer set (item 19)"
```

### Task 20: Confirm layer tickboxes + colours still work (item 20)

**Files:** verification only.

- [ ] **Step 1: Live check**

In DraftSight, open the Layers panel: confirm every remaining cutting-tool layer has a colour dropdown and the Cut/COP/Annot./Title role tickboxes, and that assignments persist into the generated DXF. No code change expected (already built).

---

## Phase 9 (OPTIONAL, low priority): Velcro corners tickbox (item 9)

Only build if Martin wants it now; he's happy to enter manually. DEFAULT behaviour per open question 4.

### Task 21: Add a "50mm velcro corner returns" tickbox

**Files:**
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` + `.xaml.cs`
- Modify: `CanvasCovers/UI/Controls/WallBlanket.xaml.cs` (a setter for the return boxes)

- [ ] **Step 1: Add a method to set both return boxes on a wall**

In `WallBlanket.xaml.cs`:
```csharp
        public void SetCornerReturns(string value)
        {
            if (_isRear || !CopEnabled) return;
            _drLeft.Text = value;
            _drRight.Text = value;
        }
```

- [ ] **Step 2: Add the checkbox + wiring**

In `LiftBlanketWindow.xaml` OPTIONS panel:
```xml
                            <CheckBox Name="VelcroCornersOption" Content="50mm velcro corner returns (L &amp; R)"
                                      Checked="VelcroCornersOption_Changed" />
```
In `LiftBlanketWindow.xaml.cs`:
```csharp
        private void VelcroCornersOption_Changed(object sender, RoutedEventArgs e)
        {
            if (VelcroCornersOption.IsChecked == true)
            {
                LeftBlanket.SetCornerReturns("50");
                RightBlanket.SetCornerReturns("50");
            }
        }
```

- [ ] **Step 3: Commit + Live check**

```
git add -A
git commit -m "feat: optional 50mm velcro corner returns tickbox (item 9)"
```
**Live check:** tick it → DR-L and DR-R on Left and Right walls fill with 50; widths/quilting update through the existing return handling; Rear unaffected.

---

## Wrap-up

- [ ] **Run the full suite one final time**

Run: `dotnet test CanvasCovers.Tests/CanvasCovers.Tests.csproj --nologo`
Expected: all green.

- [ ] **Bump the installer version**

Edit `MyAppVersion` in `Installer/CanvasCovers.iss` (e.g. 1.4.5 → 1.5.0). Never change `AppId`.

- [ ] **Full live regression** in DraftSight: generate a full L/Rear/R job with COP + quilting; generate a COP-off single-width wall; export DXF and confirm the filename = blanket text.

- [ ] **Update docs**: mark the built items in `docs/CHANGE_REQUESTS_BETA_REVIEW.md` as Done; note the ~10mm outline narrowing in the release notes / `STATUS.md`.

- [ ] **Push + open PR (only when asked):**
```
git push -u origin feat/beta-review-changes
```

---

## Self-review notes (coverage map)

- Item 1 → Tasks 7 (model), 9 (UI + generator title block)
- Item 2 → folded into 7/9 (Company Initials + AAC Order No replace Canvas/Production)
- Item 3 → Tasks 7 (builder), 8 (filename), 10 (per-wall label), 14 (placement)
- Item 4 → Task 17
- Item 5 → Task 6
- Items 6,7 → Task 11; surfaced in Task 12
- Item 8 → Task 12
- Items 10+11 → Tasks 1-4
- Item 12 → Task 14
- Item 13 → Task 5
- Item 14 → Tasks 13, 15
- Item 15 → Tasks 13, 15
- Item 16 → Task 18
- Item 18 → Task 16 (verify)
- Item 19 → Task 19
- Item 20 → Task 20 (verify)
- Item 9 → Task 21 (optional)
- Item 17 → excluded (needs explanation to Martin)

**Not in this plan (confirmed elsewhere):** 21 (parked), 22/23/24/25 (DraftSight profile/settings, not add-in), 26 (name OK), 27/28 (manual for now / future quote).
