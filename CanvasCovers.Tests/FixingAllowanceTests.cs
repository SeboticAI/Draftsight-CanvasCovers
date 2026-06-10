using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class FixingAllowanceTests
    {
        [TestMethod]
        public void Hooks_Default_Is_50()
        {
            Assert.AreEqual(50.0, FixingAllowance.DefaultFor(FixingType.HooksFacingOut));
            Assert.AreEqual(50.0, FixingAllowance.DefaultFor(FixingType.HooksFacingIn));
        }

        [TestMethod]
        public void PressStuds_Default_Is_40()
        {
            Assert.AreEqual(40.0, FixingAllowance.DefaultFor(FixingType.PressStuds));
        }

        [TestMethod]
        public void Velcro_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.Velcro));
        }

        [TestMethod]
        public void Eyelet_TG7_And_TG9_Default_Is_30()
        {
            Assert.AreEqual(30.0, FixingAllowance.DefaultFor(FixingType.Eyelet7));
            Assert.AreEqual(30.0, FixingAllowance.DefaultFor(FixingType.Eyelet9));
        }

        [TestMethod]
        public void None_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.None));
        }

        [TestMethod]
        public void SelfAdhesiveVelcro_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.SelfAdhesiveVelcro));
        }
    }
}
