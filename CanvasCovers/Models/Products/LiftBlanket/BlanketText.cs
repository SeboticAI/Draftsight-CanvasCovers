using System.Linq;
using CanvasCovers.Models;

namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The text stamped on every blanket AND used as the export filename:
    // "<AAC order number> <company initials> <network number>" with single
    // spaces, empty sections dropped.
    public static class BlanketText
    {
        public static string Build(string orderNumber, string companyInitials, string networkNumber)
        {
            string[] parts = { orderNumber, companyInitials, networkNumber };
            return string.Join(" ",
                parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        public static string Build(ProjectMetadata p)
        {
            if (p == null) return string.Empty;
            return Build(p.OrderNumber, p.CompanyInitials, p.NetworkNumber);
        }
    }
}
