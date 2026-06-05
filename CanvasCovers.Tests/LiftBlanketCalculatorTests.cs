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
        public void CutHeight_Is_Measured_Less_Allowance_Doubled()
        {
            double half = LiftBlanketCalculator.HalfHeight(2200, 50);
            Assert.AreEqual(2150.0, half);
            Assert.AreEqual(4300.0, LiftBlanketCalculator.CutHeight(2200, 50));
        }

        [TestMethod]
        public void CutWidth_Adds_The_Edge_Allowance()
        {
            Assert.AreEqual(2250.0, LiftBlanketCalculator.CutWidth(2240, 10));
            Assert.AreEqual(2240.0, LiftBlanketCalculator.CutWidth(2240, 0));
            Assert.AreEqual(2252.0, LiftBlanketCalculator.CutWidth(2240, 12));
        }

        [TestMethod]
        public void AutoTopGap_Is_Fold_Less_Bottom_Less_CopHeight()
        {
            Assert.AreEqual(250.0,
                LiftBlanketCalculator.AutoTopGap(
                    measuredHeight: 2200, fixingAllowanceMm: 50,
                    copGapFromBottom: 600, copHeight: 1300),
                0.001);
        }

        [TestMethod]
        public void AutoTopGap_Goes_Negative_When_Cop_Crosses_Fold()
        {
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
            Assert.AreEqual(605.0, cop.X0, 0.001);
            Assert.AreEqual(845.0, cop.X1, 0.001);
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
        public void No_Cop_When_Disabled()
        {
            var wall = Job12346LeftWall();
            wall.Cop.Enabled = false;
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.IsFalse(layout.CopRect.HasValue);
        }

        [TestMethod]
        public void IdentifierLabel_Concatenates_Project_And_Suffix()
        {
            var wall = Job12346LeftWall();
            var calc = new LiftBlanketCalculator(
                fixingAllowanceMm: 50, edgeAllowanceMm: 10);
            WallLayout layout = calc.LayoutWall(wall, originX: 0, "12346 TEST 12346", "L");
            Assert.IsTrue(layout.IdentifierLabel.HasValue);
            Assert.AreEqual("12346 TEST 12346 L", layout.IdentifierLabel.Value.Text);
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
    }
}
