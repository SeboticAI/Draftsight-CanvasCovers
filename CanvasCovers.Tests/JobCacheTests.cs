using System;
using System.Globalization;
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
        public void Save_Then_TryLoad_RoundTrips_ProjectInfo()
        {
            // Round 3: the project notes carry over too, including the per-job
            // order/network fields (the operator edits those after loading).
            var data = new CachedJobInputs
            {
                SavedAt = "2026-06-19 09:15",
                CompanyName = "Kone Melbourne",
                CompanyInitials = "KM",
                NetworkNumber = "NET-4471",
                OrderNumber = "AAC-10293",
                ProjectName = "Lift 3 reline",
                DateText = "2026-06-19",
            };

            JobCache.Save(data, _path);
            CachedJobInputs loaded = JobCache.TryLoad(_path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Kone Melbourne", loaded.CompanyName);
            Assert.AreEqual("KM", loaded.CompanyInitials);
            Assert.AreEqual("NET-4471", loaded.NetworkNumber);
            Assert.AreEqual("AAC-10293", loaded.OrderNumber);
            Assert.AreEqual("Lift 3 reline", loaded.ProjectName);
            Assert.AreEqual("2026-06-19", loaded.DateText);
        }

        [TestMethod]
        public void TryLoad_PreRound3_Cache_Has_Null_ProjectInfo()
        {
            // A cache written before round 3 has no project fields; they must
            // deserialize as null (restored as blank), never throw.
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{\"SavedAt\":\"2026-06-01 10:00\",\"ThroughCar\":true}");
            CachedJobInputs loaded = JobCache.TryLoad(_path);

            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.ThroughCar);
            Assert.IsNull(loaded.CompanyName);
            Assert.IsNull(loaded.DateText);
        }

        [TestMethod]
        public void Date_RoundTrips_Through_The_Format_Parse_Contract()
        {
            // Pins the round-3 date contract: SaveJobCache writes
            // Date?.ToString("yyyy-MM-dd", Invariant) and LoadPrevious reads it
            // back with TryParseExact(..., "yyyy-MM-dd", Invariant). The two
            // format literals must stay identical or the operator's date
            // silently drops on every load. Also pins graceful degradation:
            // a null (pre-round-3) or garbage DateText yields "no date", never
            // throws.
            var original = new DateTime(2026, 6, 19);
            var data = new CachedJobInputs
            {
                DateText = original.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };

            JobCache.Save(data, _path);
            CachedJobInputs loaded = JobCache.TryLoad(_path);

            Assert.IsNotNull(loaded);
            Assert.IsTrue(DateTime.TryParseExact(loaded.DateText, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed));
            Assert.AreEqual(original.Date, parsed.Date);

            Assert.IsFalse(DateTime.TryParseExact(null, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
            Assert.IsFalse(DateTime.TryParseExact("not-a-date", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
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
