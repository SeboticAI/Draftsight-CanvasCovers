namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The bottom-row measurement boxes for one wall, mirroring the paper
    // form: an optional door-return tab at each outer corner ("place zero
    // if not needed") plus up to three interior fold/measure segments.
    // The wall's cut width is the sum of all five. They do NOT add steps
    // to the cut rectangle — the cut piece is a plain rectangle of the
    // summed width; the segments are measure/fold references that drive
    // COP horizontal positioning and (future) fold lines.
    public class WallSegments
    {
        public double DoorReturnLeft { get; set; }

        public double Seg1 { get; set; }

        public double Seg2 { get; set; }

        public double Seg3 { get; set; }

        public double DoorReturnRight { get; set; }

        public double TotalWidth =>
            DoorReturnLeft + Seg1 + Seg2 + Seg3 + DoorReturnRight;
    }
}
