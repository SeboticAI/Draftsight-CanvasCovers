using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class SmokeTest
    {
        [TestMethod]
        public void Arithmetic_Works()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
