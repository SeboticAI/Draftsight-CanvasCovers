using CanvasCovers.IO;
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class DxfFilenameTests
    {
        [TestMethod]
        public void Uses_Blanket_Text_When_Present()
        {
            var meta = new ProjectMetadata { OrderNumber = "AAC123", CompanyInitials = "KM", NetworkNumber = "45678" };
            Assert.AreEqual("AAC123 KM 45678.dxf", DxfExporter.DefaultFileName(meta));
        }

        [TestMethod]
        public void Falls_Back_When_Blanket_Text_Blank()
        {
            var meta = new ProjectMetadata();
            string name = DxfExporter.DefaultFileName(meta);
            StringAssert.StartsWith(name, "CanvasCovers-");
            StringAssert.EndsWith(name, ".dxf");
        }

        [TestMethod]
        public void Strips_Invalid_Filename_Chars()
        {
            var meta = new ProjectMetadata { OrderNumber = "AA/C1", CompanyInitials = "K:M", NetworkNumber = "4*5" };
            Assert.AreEqual("AAC1 KM 45.dxf", DxfExporter.DefaultFileName(meta));
        }

        [TestMethod]
        public void Null_Metadata_Falls_Back()
        {
            string name = DxfExporter.DefaultFileName(null);
            StringAssert.StartsWith(name, "CanvasCovers-");
            StringAssert.EndsWith(name, ".dxf");
        }
    }
}
