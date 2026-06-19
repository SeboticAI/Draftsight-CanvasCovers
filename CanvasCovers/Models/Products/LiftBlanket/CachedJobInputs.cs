namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The dialog state captured after each successful Generate (round 2,
    // item 1; extended round 3): walls + options + project information.
    // Project info (company, initials, network number, order number, project
    // name, date) is cached as of round 3 so a repeat job carries its notes
    // over too — Martin's follow-up to item 1. The operator edits the per-job
    // fields (order/network number) after loading. All fields are flat
    // strings/bools; the date is pre-formatted ("yyyy-MM-dd") to keep
    // JavaScriptSerializer's timezone-hostile DateTime handling out of the
    // cache, same reasoning as SavedAt.
    public class CachedJobInputs
    {
        // Display-only "when was this saved" stamp, local time. Kept as a
        // pre-formatted string: JavaScriptSerializer's DateTime handling is
        // timezone-hostile and nothing computes with this value.
        public string SavedAt { get; set; }

        public CachedWallInputs Left { get; set; }
        public CachedWallInputs Right { get; set; }
        public CachedWallInputs Rear { get; set; }

        public bool ThroughCar { get; set; }
        public bool PlasticCover { get; set; }
        public bool BagRequired { get; set; }
        public bool GlassBehind { get; set; }

        // The fixings ComboBoxItem Tag (e.g. "Eyelet9"); null/empty = none
        // selected. Restored by tag match; an unknown tag (from an older
        // version) simply leaves the combo unselected.
        public string FixingsTag { get; set; }

        public string FixingAllowanceText { get; set; }
        public string QuiltInsetText { get; set; }
        public string QuiltingSpacingText { get; set; }
        public bool QuiltingEnabled { get; set; } = true;
        public bool ExportDxf { get; set; } = true;

        // Project information (round 3). Restored into the form by Load
        // Previous; the operator overwrites the per-job fields (order number,
        // network number) afterwards. DateText is "yyyy-MM-dd" or null.
        public string CompanyName { get; set; }
        public string CompanyInitials { get; set; }
        public string NetworkNumber { get; set; }
        public string OrderNumber { get; set; }
        public string ProjectName { get; set; }
        public string DateText { get; set; }
    }
}
