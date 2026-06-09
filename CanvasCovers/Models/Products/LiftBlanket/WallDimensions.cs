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

        // Optional manual override of the cut width. When > 0 it replaces the
        // segment sum (the operator typed a single total instead of breaking it
        // into segments). The segment boxes stay usable and still drive COP
        // positioning if present. 0 = no override (use the segment sum).
        public double TotalWidthOverride { get; set; }

        // Cut width: the override when set, otherwise the summed segments.
        public double Width => TotalWidthOverride > 0 ? TotalWidthOverride : Segments.TotalWidth;
    }
}
