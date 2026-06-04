namespace CanvasCovers.Models.Products.LiftBlanket
{
    // Per-wall COP (cut-out panel) inputs, taken straight off the
    // measurement sheet. The operator types width, height, the gap from
    // the wall bottom to the COP bottom, and a horizontal offset from the
    // wall's left edge. The COP is placed in the bottom (measured) half of
    // the doubled cut piece — the calculator handles the fold.
    public class CopPlacement
    {
        public bool Enabled { get; set; }

        public double Width { get; set; } = 240;

        public double Height { get; set; } = 1300;

        // Distance from the wall's bottom edge to the COP's bottom edge.
        public double GapFromBottom { get; set; } = 600;

        // Distance from the wall's left edge to the COP's left edge.
        public double OffsetFromLeft { get; set; } = 600;
    }
}
