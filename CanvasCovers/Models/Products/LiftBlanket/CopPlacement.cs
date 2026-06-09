namespace CanvasCovers.Models.Products.LiftBlanket
{
    // Per-wall COP (cut-out panel) VERTICAL inputs. The horizontal geometry
    // is derived from the bottom-row segments (width = Seg2, left edge =
    // DoorReturnLeft + Seg1), so only the vertical numbers
    // live here. The blanket folds at the midline (measuredHeight −
    // fixingAllowance); the COP sits in the bottom (measured) half. The top
    // gap is computed (foldMidline − GapFromBottom − Height), not stored.
    public class CopPlacement
    {
        public bool Enabled { get; set; }

        // COP height (the middle vertical box on the sheet, e.g. 1300).
        public double Height { get; set; } = 1300;

        // Distance from the wall's bottom edge to the COP's bottom edge
        // (the bottom vertical box, e.g. 600).
        public double GapFromBottom { get; set; } = 600;
    }
}
