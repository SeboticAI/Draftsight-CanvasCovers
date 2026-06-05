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
            WallLayout layout = calc.LayoutWall(
                wall, originX: 0, "12346 TEST 12346", "L",
                quiltingEnabled: true, verticalQuiltingSpacingMm: 700);

            Assert.IsTrue(layout.QuiltLines.Count > 0);

            double half = 5;
            double leftBound = 250 + half;
            double rightBound = 2250 - 0 - half;
            double bottomBound = half;
            double topBound = 2150;

            foreach (LineSpec l in layout.QuiltLines)
            {
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
