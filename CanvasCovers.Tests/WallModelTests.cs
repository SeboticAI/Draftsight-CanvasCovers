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
    }
}
