using System.Linq;
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class LayerSettingsTests
    {
        [TestMethod]
        public void DefaultLayers_Has_No_Defpoints()
        {
            Assert.IsFalse(LayerSettings.DefaultLayers().Any(l => l.Name == "Defpoints"));
        }

        [TestMethod]
        public void DefaultLayers_Keeps_The_Cutting_Tool_Layers()
        {
            var names = LayerSettings.DefaultLayers().Select(l => l.Name).ToList();
            CollectionAssert.Contains(names, "1 Rotary Blade");
            CollectionAssert.Contains(names, "2 Drag Blade");
            CollectionAssert.Contains(names, "3 Crease Tool");
            CollectionAssert.Contains(names, "5 Draw and Text");
        }
    }
}
