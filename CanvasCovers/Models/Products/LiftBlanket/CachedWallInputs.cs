namespace CanvasCovers.Models.Products.LiftBlanket
{
    // One wall's raw dialog state for the job cache (round 2, item 1).
    // Strings are the operator's literal typed text — NOT parsed numbers —
    // so a restore reproduces the form exactly, including blanks.
    // Public parameterless ctor + get/set properties: required by
    // JavaScriptSerializer round-tripping.
    public class CachedWallInputs
    {
        public bool IncludeWall { get; set; } = true;
        public bool IncludeCop { get; set; }
        public string DoorReturnLeft { get; set; }
        public string Seg1 { get; set; }
        public string Seg2 { get; set; }
        public string Seg3 { get; set; }
        public string DoorReturnRight { get; set; }
        public string TotalWidth { get; set; }
        public string MeasuredHeight { get; set; }
        public string CopHeight { get; set; }
        public string CopGapBottom { get; set; }
    }
}
