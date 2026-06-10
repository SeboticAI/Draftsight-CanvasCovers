using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class CustomerDirectoryTests
    {
        [TestMethod]
        public void Parse_Reads_Name_And_Initials()
        {
            var list = CustomerDirectory.Parse(new[] { "Kone Melbourne,KM", "LiftCorp,L" });
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("Kone Melbourne", list[0].Name);
            Assert.AreEqual("KM", list[0].Initials);
            Assert.AreEqual("LiftCorp", list[1].Name);
            Assert.AreEqual("L", list[1].Initials);
        }

        [TestMethod]
        public void Parse_Trims_Whitespace()
        {
            var list = CustomerDirectory.Parse(new[] { "  Kone Perth ,  KP  " });
            Assert.AreEqual("Kone Perth", list[0].Name);
            Assert.AreEqual("KP", list[0].Initials);
        }

        [TestMethod]
        public void Parse_Skips_Blanks_Comments_And_Malformed_Lines()
        {
            var list = CustomerDirectory.Parse(new[]
            {
                "", "   ", "# comment line", "NoCommaHere", ",InitialsOnly",
                "Schindler Sydney,SS",
            });
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("Schindler Sydney", list[0].Name);
        }

        [TestMethod]
        public void Parse_Null_Returns_Empty()
        {
            Assert.AreEqual(0, CustomerDirectory.Parse(null).Count);
        }

        [TestMethod]
        public void Parse_Keeps_Entry_With_Empty_Initials()
        {
            // A trailing comma means "no initials yet" — still list the name.
            var list = CustomerDirectory.Parse(new[] { "New Customer," });
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("", list[0].Initials);
        }
    }
}
