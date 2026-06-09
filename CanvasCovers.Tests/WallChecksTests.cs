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
