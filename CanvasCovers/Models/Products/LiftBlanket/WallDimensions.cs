namespace CanvasCovers.Models.Products.LiftBlanket
{
    // One lift-blanket wall as the operator measures it on the paper form.
    // Width is the sum of the bottom-row segments; MeasuredHeight is the
    // raw "top of hook to bottom of blanket" number BEFORE the fixing
    // allowance is subtracted and BEFORE the ×2 doubling — the calculator
    // applies both. COP is optional and per-wall.
    public class WallDimensions
    {
        public bool Enabled { get; set; } = true;

        public WallSegments Segments { get; set; } = new WallSegments();

        public double MeasuredHeight { get; set; } = 2200;

        public CopPlacement Cop { get; set; } = new CopPlacement();

        // Cut width equals the summed segments (no allowance on width here;
        // the +10mm "WIDTH - ADD 10mm" rule is applied by the calculator).
        public double Width => Segments.TotalWidth;
    }
}
