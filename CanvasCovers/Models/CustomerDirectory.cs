using System.Collections.Generic;
using System.IO;

namespace CanvasCovers.Models
{
    public class CustomerEntry
    {
        public CustomerEntry(string name, string initials)
        {
            Name = name;
            Initials = initials;
        }

        public string Name { get; }
        public string Initials { get; }
    }

    // Customer name -> AAC initials list feeding the Company Name drop-down
    // (round 2, item 6). Read from a per-user CSV the operator edits in
    // Notepad; seeded on first use from the read-only copy shipped in the
    // install dir's Resources folder. One "Name,Initials" pair per line;
    // blank lines and #-comments are ignored. WPF-free so the parser links
    // into the headless test project.
    public static class CustomerDirectory
    {
        public static string DefaultUserPath => UserDataPaths.CustomersCsv;

        public static List<CustomerEntry> Parse(IEnumerable<string> lines)
        {
            var result = new List<CustomerEntry>();
            if (lines == null) return result;
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string line = raw.Trim();
                if (line.StartsWith("#")) continue;
                int comma = line.IndexOf(',');
                if (comma <= 0) continue;   // no comma, or empty name part
                string name = line.Substring(0, comma).Trim();
                string initials = line.Substring(comma + 1).Trim();
                if (name.Length == 0) continue;
                result.Add(new CustomerEntry(name, initials));
            }
            return result;
        }

        // Reads the operator's editable copy, creating it from the shipped
        // seed on first use. Any IO failure returns an empty list — the
        // drop-down is convenience sugar and must never block the dialog
        // (no dispatcher exception handler in-host, CLAUDE.md §9).
        public static List<CustomerEntry> LoadOrSeed(string userPath, string seedPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(userPath) && !File.Exists(userPath)
                    && !string.IsNullOrEmpty(seedPath) && File.Exists(seedPath))
                {
                    UserDataPaths.EnsureParentDir(userPath);
                    File.Copy(seedPath, userPath);
                }
                if (!string.IsNullOrEmpty(userPath) && File.Exists(userPath))
                    return Parse(File.ReadAllLines(userPath));
            }
            catch { /* fall through */ }
            return new List<CustomerEntry>();
        }
    }
}
