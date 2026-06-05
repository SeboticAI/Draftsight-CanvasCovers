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

        // Interior line offsets that divide [start, end] into equal gaps as
        // close as possible to targetGap. The count of GAPS is round(span /
        // targetGap), clamped to at least 1; the returned list is the
        // interior boundaries (count = gaps − 1). Returns empty for a
        // nonpositive span or spacing. Even division means no remainder gap.
        public static List<double> EvenlySpaced(double start, double end, double targetGap)
        {
            var result = new List<double>();
            double span = end - start;
            if (span <= 0 || targetGap <= 0) return result;

            int gaps = (int)System.Math.Round(span / targetGap, System.MidpointRounding.AwayFromZero);
            if (gaps < 1) gaps = 1;
            double step = span / gaps;
            for (int i = 1; i < gaps; i++)
            {
                result.Add(start + i * step);
            }
            return result;
        }

        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix,
            bool quiltingEnabled = false,
            double verticalQuiltingSpacingMm = 0)
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

            if (quiltingEnabled)
            {
                AddQuiltLines(layout, wall, originX, cutWidth, halfHeight,
                    halfEdge, verticalQuiltingSpacingMm);
            }

            return layout;
        }

        // Fills layout.QuiltLines, confined to the bottom (measured) half
        // (Y from the bottom clearance up to the fold midline). Two line sets:
        //
        //  - HORIZONTAL lines run the FULL width minus the edge clearance
        //    (X: half .. cutWidth-half) — edge-to-edge, NOT bounded by the
        //    door-return segments — spaced up the height by the operator's
        //    vertical spacing, even-divided. (Matches the client's DXF, where
        //    the horizontals span the full wall width.)
        //
        //  - VERTICAL lines: a line on the DR-L boundary (half + DoorReturnLeft)
        //    and on the DR-R boundary (cutWidth - half - DoorReturnRight), each
        //    drawn only when that door-return segment is non-zero, PLUS interior
        //    lines that even-divide the span between those two boundaries.
        private void AddQuiltLines(
            WallLayout layout, WallDimensions wall, double originX,
            double cutWidth, double foldMidline, double half,
            double verticalSpacing)
        {
            double bottom = half;
            double top = foldMidline;
            if (top <= bottom) return;

            // Horizontal lines: full width minus the edge clearance.
            double hLeft = originX + half;
            double hRight = originX + cutWidth - half;
            if (hRight > hLeft)
            {
                foreach (double y in EvenlySpaced(bottom, top, verticalSpacing))
                {
                    layout.QuiltLines.Add(new LineSpec(hLeft, y, hRight, y));
                }
            }

            // Vertical lines: DR-L / DR-R boundary lines (skipped when that
            // door return is zero) + even-fill between them.
            double drLeft = wall.Segments.DoorReturnLeft;
            double drRight = wall.Segments.DoorReturnRight;
            double leftBound = originX + half + drLeft;
            double rightBound = originX + cutWidth - half - drRight;
            if (rightBound <= leftBound) return;

            if (drLeft > 0)
                layout.QuiltLines.Add(new LineSpec(leftBound, bottom, leftBound, top));
            if (drRight > 0)
                layout.QuiltLines.Add(new LineSpec(rightBound, bottom, rightBound, top));

            foreach (double x in EvenlySpaced(leftBound, rightBound, verticalSpacing))
            {
                layout.QuiltLines.Add(new LineSpec(x, bottom, x, top));
            }
        }
    }
}
