using System;
using System.IO;
using System.Web.Script.Serialization;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.IO
{
    // Persists the last successfully generated job's dialog state to a
    // per-user JSON file (round 2, item 1 — Martin's 12-identical-blankets
    // workflow). JavaScriptSerializer (System.Web.Extensions) is the
    // zero-dependency net48 JSON option; the DTOs are flat strings/bools so
    // its limitations don't bite.
    public static class JobCache
    {
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BesiaCAD", "CanvasCovers", "last-job.json");

        // Throws on IO problems — callers on the UI thread must wrap this
        // (no dispatcher exception handler in-host, CLAUDE.md §9).
        public static void Save(CachedJobInputs data, string path)
        {
            if (data == null || string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(data));
        }

        // Null when there is no usable cache (missing, corrupt, unreadable).
        // Never throws: a broken cache file must read as "no previous job",
        // not crash the dialog.
        public static CachedJobInputs TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                return new JavaScriptSerializer()
                    .Deserialize<CachedJobInputs>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
