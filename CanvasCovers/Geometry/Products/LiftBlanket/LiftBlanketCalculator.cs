using System.Collections.Generic;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Pure, SDK-free geometry math for a lift blanket. Encapsulates the
    // rules recovered from the client's reference DXFs:
    //   1. cutHeight = (measuredHeight - fixingAllowance) * 2
    //   2. cutWidth  = summedSegments + edgeAllowance   ("WIDTH - ADD ...mm")
    //   3. COP horizontal geometry is derived from the bottom-row segments:
    //      width = Seg2, left edge = edgeAllowance/2 + DoorReturnLeft + Seg1.
    //      Vertically the COP sits in the bottom (measured) half from the
    //      operator's gap-from-bottom / height numbers; the top half is a
    //      mirror, so no COP geometry is needed above the fold midline.
    // No DraftSight types here so it can be unit-tested headlessly.
    public class LiftBlanketCalculator
    {
        // Wall identifier labels ("<net> <name> L/R/B") are text height 20
        // in the client's reference DXFs — match that.
        private const double IdentifierTextHeight = 20.0;

        private readonly double _fixingAllowanceMm;
        private readonly double _edgeAllowanceMm;

        public LiftBlanketCalculator(double fixingAllowanceMm, double edgeAllowanceMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
            _edgeAllowanceMm = edgeAllowanceMm;
        }

        public static double HalfHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return measuredHeight - fixingAllowanceMm;
        }

        public static double CutHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return HalfHeight(measuredHeight, fixingAllowanceMm) * 2.0;
        }

        public static double CutWidth(double summedSegments, double edgeAllowanceMm)
        {
            return summedSegments + edgeAllowanceMm;
        }

        // The auto-derived top gap: from the COP top up to the fold midline.
        // The fixing allowance comes off this gap, so it references the fold
        // (measuredHeight − fixingAllowance), NOT the raw measured height.
        // Returns a negative value when the COP would cross the fold — the
        // dialog treats that as a blocking validation error.
        public static double AutoTopGap(
            double measuredHeight, double fixingAllowanceMm,
            double copGapFromBottom, double copHeight)
        {
            double foldMidline = HalfHeight(measuredHeight, fixingAllowanceMm);
            return foldMidline - copGapFromBottom - copHeight;
        }

        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix)
        {
            double halfEdge = _edgeAllowanceMm / 2.0;
            double cutWidth = CutWidth(wall.Width, _edgeAllowanceMm);

            // Assumes upstream validation guarantees MeasuredHeight > the
            // fixing allowance; otherwise halfHeight goes negative and the cut
            // rect inverts. The dialog enforces a positive measured height.
            double halfHeight = HalfHeight(wall.MeasuredHeight, _fixingAllowanceMm);
            double cutHeight = CutHeight(wall.MeasuredHeight, _fixingAllowanceMm);

            var layout = new WallLayout
            {
                CutRect = new RectSpec(originX, 0, originX + cutWidth, cutHeight),
                FoldMidlineY = halfHeight,
                Dimensions = new List<DimSpec>(),
            };

            if (wall.Cop.Enabled)
            {
                // Horizontal geometry is derived from the segments: the COP
                // width is the middle box (Seg2), and its left edge sits
                // half-allowance + DoorReturnLeft + Seg1 in from the cut edge
                // (measured from the door-return line, per the sheet).
                double copX0 = originX + halfEdge
                    + wall.Segments.DoorReturnLeft + wall.Segments.Seg1;
                double copWidth = wall.Segments.Seg2;
                double copY0 = wall.Cop.GapFromBottom;
                layout.CopRect = new RectSpec(
                    copX0,
                    copY0,
                    copX0 + copWidth,
                    copY0 + wall.Cop.Height);
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                layout.IdentifierLabel = new LabelSpec
                {
                    X = originX + cutWidth / 2.0,
                    Y = cutHeight / 2.0,
                    Height = IdentifierTextHeight,
                    Text = (string.IsNullOrEmpty(projectTag) ? "" : projectTag + " ") + suffix,
                };
            }

            // Width dim along the wall bottom edge (extension points only).
            // The generator applies its own offset (DimGap) when it places
            // the dim line — keeping layout decisions out of the math core.
            layout.Dimensions.Add(new DimSpec
            {
                Ext1X = originX, Ext1Y = 0,
                Ext2X = originX + cutWidth, Ext2Y = 0,
                LineX = originX + cutWidth / 2.0, LineY = 0,
            });

            return layout;
        }
    }
}
