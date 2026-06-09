using System.Collections.Generic;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Axis-aligned rectangle in world (drawing) coordinates, given by its
    // lower-left and upper-right corners.
    public struct RectSpec
    {
        public double X0;
        public double Y0;
        public double X1;
        public double Y1;

        public RectSpec(double x0, double y0, double x1, double y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }
    }

    // A dimension to emit: two extension points and a point on the dim line.
    public struct DimSpec
    {
        public double Ext1X, Ext1Y, Ext2X, Ext2Y, LineX, LineY;
    }

    // A single-line text label to emit at a baseline point. Angle is the
    // text rotation in degrees (0 = horizontal, 180 = inverted, 90 = vertical).
    public struct LabelSpec
    {
        public double X, Y, Height, Angle;
        public string Text;
    }

    // A single straight line segment to emit (used for quilt lines).
    public struct LineSpec
    {
        public double X0, Y0, X1, Y1;

        public LineSpec(double x0, double y0, double x1, double y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }
    }

    // Everything the generator needs to draw one wall.
    public class WallLayout
    {
        // The cut rectangle (full doubled height) on the cut layer.
        public RectSpec CutRect;

        // The fold midline Y (where the bottom panel mirrors). The quilt
        // layout uses it as the top bound — lines fill only the bottom half.
        public double FoldMidlineY;

        // The COP rectangle in the bottom half, or null when no COP.
        public RectSpec? CopRect;

        // Wall identifier label ("<net> <name> L"), on the draw layer.
        public LabelSpec? IdentifierLabel;

        // Width + height dimensions for this wall.
        public List<DimSpec> Dimensions = new List<DimSpec>();

        // Vertical + horizontal quilt lines (draw/score layer), bottom half
        // only. Empty when quilting is disabled.
        public List<LineSpec> QuiltLines = new List<LineSpec>();

        // Reminder labels drawn inside the COP cutout (vertical), e.g. "BAG"
        // and the fixing type. Empty when no COP or none requested.
        public List<LabelSpec> CopReminders = new List<LabelSpec>();
    }
}
