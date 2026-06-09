using System.Collections.Generic;
using System.Linq;
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
            // Span 0..1000, target 700 → 1.43 → round to 1 gap → 0 interior.
            // Span 0..1000, target 400 → 2.5 → round up to 3 gaps → interior
            // at 333.3, 666.6.
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

        [TestMethod]
        public void EvenlySpaced_Caps_The_Line_Count_For_Tiny_Spacing()
        {
            // A tiny spacing (e.g. 1mm over a 2100mm span) would otherwise yield
            // ~2099 interior lines. The MaxGaps cap (200) limits it so a typo
            // can't emit thousands of polylines and freeze the host.
            List<double> lines = LiftBlanketCalculator.EvenlySpaced(0, 2100, 1);
            Assert.IsTrue(lines.Count <= 200,
                $"expected the count to be capped, got {lines.Count}");
            Assert.AreEqual(199, lines.Count);   // 200 gaps → 199 interior lines
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
        public void Horizontal_Quilt_Lines_Span_Full_Width_Minus_Clearance()
        {
            // Horizontal lines run edge-to-edge, inset only by the quilt inset
            // (NOT bounded by the door-return segments). For the reference:
            // cut width 2240, inset 5 → X spans 5..2235.
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            var horizontals = layout.QuiltLines.Where(l => System.Math.Abs(l.Y0 - l.Y1) < 0.001).ToList();
            Assert.IsTrue(horizontals.Count > 0, "expected horizontal quilt lines");
            foreach (LineSpec h in horizontals)
            {
                Assert.AreEqual(5.0, h.X0, 0.001, "horizontal left edge = inset");
                Assert.AreEqual(2235.0, h.X1, 0.001, "horizontal right edge = cutWidth - inset");
                Assert.IsTrue(h.Y0 >= 5 - 0.001 && h.Y0 <= 2150 + 0.001, "horizontal Y in [inset, fold]");
            }
        }

        [TestMethod]
        public void Vertical_Quilt_Includes_DoorReturn_Boundary_Lines()
        {
            // A vertical line sits on the DR-L boundary (DR-L = 250).
            // DR-R is 0 here, so the DR-R boundary line is SKIPPED. Verticals
            // span the bottom region (half=5) up to the fold (2150).
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            var verticals = layout.QuiltLines.Where(l => System.Math.Abs(l.X0 - l.X1) < 0.001).ToList();
            Assert.IsTrue(verticals.Count > 0, "expected vertical quilt lines");

            // DR-L boundary line present at X = 250.
            Assert.IsTrue(verticals.Any(v => System.Math.Abs(v.X0 - 250.0) < 0.001),
                "expected a vertical quilt line on the DR-L boundary (250)");
            // DR-R is 0 → no boundary line at the right cut edge.
            Assert.IsFalse(verticals.Any(v => System.Math.Abs(v.X0 - 2235.0) < 0.001),
                "DR-R is 0 so no DR-R boundary line should be drawn");

            foreach (LineSpec v in verticals)
            {
                Assert.AreEqual(5.0, v.Y0, 0.001, "vertical bottom = inset");
                Assert.AreEqual(2150.0, v.Y1, 0.001, "vertical top = fold midline");
            }
        }

        [TestMethod]
        public void Vertical_Quilt_Draws_Both_DoorReturn_Lines_When_Both_Present()
        {
            // With both DR-L and DR-R non-zero, both boundary lines appear.
            var wall = Job12346LeftWall();
            wall.Segments.DoorReturnRight = 100;   // now 250 + 350 + 240 + 1400 + 100
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            double cutWidth = 250 + 350 + 240 + 1400 + 100;  // 2340, no edge boost
            double drLeftLine = 250;                          // true edge + DR-L
            double drRightLine = cutWidth - 100;              // 2240

            var verticals = layout.QuiltLines.Where(l => System.Math.Abs(l.X0 - l.X1) < 0.001).ToList();
            Assert.IsTrue(verticals.Any(v => System.Math.Abs(v.X0 - drLeftLine) < 0.001),
                "expected DR-L boundary line at 250");
            Assert.IsTrue(verticals.Any(v => System.Math.Abs(v.X0 - drRightLine) < 0.001),
                "expected DR-R boundary line at 2240");
        }

        [TestMethod]
        public void Quilt_Empty_When_Disabled()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: false, verticalQuiltingSpacingMm: 700);
            Assert.AreEqual(0, layout.QuiltLines.Count);
        }

        [TestMethod]
        public void No_Quilt_Line_Sits_On_An_Outline_Edge()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50, quiltInsetMm: 5);
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            double cutWidth = 2240;
            foreach (LineSpec l in layout.QuiltLines)
            {
                // No vertical on the left/right outline; no horizontal on the bottom.
                Assert.IsFalse(System.Math.Abs(l.X0 - 0) < 0.001 && System.Math.Abs(l.X1 - 0) < 0.001);
                Assert.IsFalse(System.Math.Abs(l.X0 - cutWidth) < 0.001 && System.Math.Abs(l.X1 - cutWidth) < 0.001);
                Assert.IsFalse(System.Math.Abs(l.Y0 - 0) < 0.001 && System.Math.Abs(l.Y1 - 0) < 0.001);
            }
        }

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
    }
}
