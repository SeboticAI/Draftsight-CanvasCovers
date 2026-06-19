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

        [TestMethod]
        public void Tolerance_Boundary_Guards_The_Strict_Comparison()
        {
            // A difference exactly at the 1.0mm default tolerance must NOT warn
            // (the comparison is strict '>'); just over it must warn. Locks the
            // '>' vs '>=' choice so a future retune can't silently flip the
            // operator-facing width warning.
            Assert.IsFalse(WallChecks.WidthsMismatch(true, 2240, true, 2239));
            Assert.IsTrue(WallChecks.WidthsMismatch(true, 2240, true, 2238.5));
        }
    }
}
