using System;
using System.IO;
using CanvasCovers.IO;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class JobCacheTests
    {
        private string _path;

        [TestInitialize]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(),
                "canvascovers-test-" + Guid.NewGuid().ToString("N"), "last-job.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            string dir = Path.GetDirectoryName(_path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [TestMethod]
        public void Save_Then_TryLoad_RoundTrips()
        {
            var data = new CachedJobInputs
            {
                SavedAt = "2026-06-10 14:30",
                ThroughCar = true,
                FixingsTag = "Eyelet9",
                FixingAllowanceText = "35",
                QuiltInsetText = "5",
                QuiltingSpacingText = "650",
                QuiltingEnabled = false,
                ExportDxf = false,
                Left = new CachedWallInputs
                {
                    IncludeWall = true,
                    IncludeCop = true,
                    Seg1 = "812.5",
                    MeasuredHeight = "2200",
                    CopHeight = "1000",
                    CopGapBottom = "150",
                    TotalWidth = "",
                },
                Rear = new CachedWallInputs { IncludeWall = false, Seg1 = "1400" },
            };

            JobCache.Save(data, _path);
            CachedJobInputs loaded = JobCache.TryLoad(_path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("2026-06-10 14:30", loaded.SavedAt);
            Assert.IsTrue(loaded.ThroughCar);
            Assert.AreEqual("Eyelet9", loaded.FixingsTag);
            Assert.AreEqual("35", loaded.FixingAllowanceText);
            Assert.IsFalse(loaded.QuiltingEnabled);
            Assert.IsFalse(loaded.ExportDxf);
            Assert.AreEqual("812.5", loaded.Left.Seg1);
            Assert.AreEqual("2200", loaded.Left.MeasuredHeight);
            Assert.IsTrue(loaded.Left.IncludeCop);
            Assert.IsFalse(loaded.Rear.IncludeWall);
            Assert.IsNull(loaded.Right);   // never set — stays null, applied as no-op
        }

        [TestMethod]
        public void TryLoad_Missing_File_Returns_Null()
        {
            Assert.IsNull(JobCache.TryLoad(_path));
        }

        [TestMethod]
        public void TryLoad_Corrupt_File_Returns_Null()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{not valid json!!");
            Assert.IsNull(JobCache.TryLoad(_path));
        }
    }
}
