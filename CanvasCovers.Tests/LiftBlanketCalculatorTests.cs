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
    }
}
