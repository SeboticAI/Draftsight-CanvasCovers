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

        [TestMethod]
        public void WallDimensions_Width_Uses_Override_When_Set()
        {
            var wall = new WallDimensions();
            wall.Segments.Seg1 = 1450;          // segments present
            wall.TotalWidthOverride = 1990;     // but an override is given
            Assert.AreEqual(1990.0, wall.Width);

            wall.TotalWidthOverride = 0;        // 0 = no override → back to segments
            Assert.AreEqual(1450.0, wall.Width);
        }
    }
}
