using System;
using System.Globalization;
using System.Linq;
using CanvasCovers.Models;

namespace CanvasCovers.IO
{
    // Filename derivation, kept SDK-free so it can be unit-tested. The full
    // naming convention will be confirmed with the client later; for now the
    // network number is the filename. Falls back to a timestamp when blank.
    public partial class DxfExporter
    {
        public static string DefaultFileName(ProjectMetadata project)
        {
            string net = project?.NetworkNumber ?? string.Empty;
            string cleaned = new string(net.Where(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "CanvasCovers-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            }
            return cleaned + ".dxf";
        }
    }
}
