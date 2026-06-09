using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class BlanketTextTests
    {
        [TestMethod]
        public void Joins_Three_Sections_With_Spaces()
        {
            Assert.AreEqual("AAC123 KM 45678",
                BlanketText.Build("AAC123", "KM", "45678"));
        }

        [TestMethod]
        public void Drops_Empty_Sections()
        {
            Assert.AreEqual("AAC123 45678", BlanketText.Build("AAC123", "", "45678"));
            Assert.AreEqual("KM", BlanketText.Build(null, " KM ", null));
        }
    }
}
