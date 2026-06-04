using CanvasCovers.IO;
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class DxfFilenameTests
    {
        [TestMethod]
        public void Uses_Network_Number_When_Present()
        {
            var meta = new ProjectMetadata { NetworkNumber = "12346" };
            Assert.AreEqual("12346.dxf", DxfExporter.DefaultFileName(meta));
        }

        [TestMethod]
        public void Falls_Back_When_Network_Number_Blank()
        {
            var meta = new ProjectMetadata { NetworkNumber = "" };
            string name = DxfExporter.DefaultFileName(meta);
            StringAssert.StartsWith(name, "CanvasCovers-");
            StringAssert.EndsWith(name, ".dxf");
        }

        [TestMethod]
        public void Strips_Invalid_Filename_Chars()
        {
            var meta = new ProjectMetadata { NetworkNumber = "12/3:46*" };
            Assert.AreEqual("12346.dxf", DxfExporter.DefaultFileName(meta));
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
