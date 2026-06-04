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
            double half = LiftBlanketCalculator.HalfHeight(2200, 50);
            Assert.AreEqual(2150.0, half);
            Assert.AreEqual(4300.0, LiftBlanketCalculator.CutHeight(2200, 50));
        }

        [TestMethod]
        public void CutWidth_Adds_Ten_Millimetres()
        {
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
            Assert.AreEqual(2250.0, layout.CutRect.X1, 0.001);
            Assert.AreEqual(4300.0, layout.CutRect.Y1, 0.001);
            Assert.AreEqual(2150.0, layout.FoldMidlineY, 0.001);
        }

        [TestMethod]
        public void Cop_Sits_In_Bottom_Half_From_Sheet_Numbers()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.IsTrue(layout.CopRect.HasValue);
            RectSpec cop = layout.CopRect.Value;
            Assert.AreEqual(600.0, cop.X0, 0.001);
            Assert.AreEqual(840.0, cop.X1, 0.001);
            Assert.AreEqual(600.0, cop.Y0, 0.001);
            Assert.AreEqual(1900.0, cop.Y1, 0.001);
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

        [TestMethod]
        public void Layout_Offsets_All_X_By_OriginX()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 1000, "12346 TEST 12346", "L");

            // Cut rect shifts right by 1000; width unchanged at 2250.
            Assert.AreEqual(1000.0, layout.CutRect.X0, 0.001);
            Assert.AreEqual(3250.0, layout.CutRect.X1, 0.001);
            // COP X shifts too: 1000 + 600 offset = 1600..1840.
            Assert.IsTrue(layout.CopRect.HasValue);
            Assert.AreEqual(1600.0, layout.CopRect.Value.X0, 0.001);
            Assert.AreEqual(1840.0, layout.CopRect.Value.X1, 0.001);
            // Y is unaffected by originX.
            Assert.AreEqual(600.0, layout.CopRect.Value.Y0, 0.001);
            Assert.AreEqual(1900.0, layout.CopRect.Value.Y1, 0.001);
        }

        [TestMethod]
        public void Width_Dimension_Extension_Points_On_Bottom_Edge()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(fixingAllowanceMm: 50);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");

            Assert.AreEqual(1, layout.Dimensions.Count);
            DimSpec d = layout.Dimensions[0];
            // Extension points + dim line all on the bottom edge (Y=0). The
            // generator owns the downward offset, so the calculator must NOT
            // pre-offset the dim line — locking that contract here because
            // the generator that depends on it can only be tested in-host.
            Assert.AreEqual(0.0, d.Ext1Y, 0.001);
            Assert.AreEqual(0.0, d.Ext2Y, 0.001);
            Assert.AreEqual(0.0, d.LineY, 0.001);
            // Spans the full cut width (0 .. 2250).
            Assert.AreEqual(0.0, d.Ext1X, 0.001);
            Assert.AreEqual(2250.0, d.Ext2X, 0.001);
        }

        [TestMethod]
        public void Cop_Fits_When_Top_Edge_Below_Midline()
        {
            // Job 12346: gap 600 + height 1300 = 1900 <= half 2150 → fits.
            Assert.IsTrue(LiftBlanketCalculator.CopFitsInBottomHalf(
                copGapFromBottom: 600, copHeight: 1300,
                measuredHeight: 2200, fixingAllowanceMm: 50));
        }

        [TestMethod]
        public void Cop_Does_Not_Fit_When_It_Crosses_The_Fold()
        {
            // gap 600 + height 1700 = 2300 > half 2150 → crosses the fold.
            Assert.IsFalse(LiftBlanketCalculator.CopFitsInBottomHalf(
                copGapFromBottom: 600, copHeight: 1700,
                measuredHeight: 2200, fixingAllowanceMm: 50));
        }
    }
}
