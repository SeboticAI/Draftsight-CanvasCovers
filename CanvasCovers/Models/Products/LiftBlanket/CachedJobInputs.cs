namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The dialog state captured after each successful Generate (round 2,
    // item 1): walls + options ONLY. Project information (order number,
    // network number, company, project name, date) is deliberately NOT
    // cached — repeat jobs reuse measurements, never job identity, so those
    // fields must start blank and be typed fresh per job.
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
    }
}
