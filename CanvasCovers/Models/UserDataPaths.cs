using System;
using System.IO;

namespace CanvasCovers.Models
{
    // Single source of truth for the per-user data folder
    // (%AppData%\BesiaCAD\CanvasCovers) and the files that live in it.
    // JobCache and CustomerDirectory both build paths from here so the
    // folder name can never drift between features. WPF-free; linked into
    // the headless test project.
    public static class UserDataPaths
    {
        public static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BesiaCAD", "CanvasCovers");

        // The operator-editable customer list (round 2, item 6).
        public static string CustomersCsv => Path.Combine(Root, "customers.csv");

        // The last-generated-job cache (round 2, item 1).
        public static string LastJobJson => Path.Combine(Root, "last-job.json");

        // Creates the parent folder of a file path if needed.
        public static void EnsureParentDir(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
}
