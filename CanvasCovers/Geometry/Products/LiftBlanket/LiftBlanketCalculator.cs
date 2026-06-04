using System.Collections.Generic;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Pure, SDK-free geometry math for a lift blanket. Encapsulates the
    // three rules recovered from the client's reference DXFs:
    //   1. cutHeight = (measuredHeight - fixingAllowance) * 2
    //   2. cutWidth  = summedSegments + 10   ("WIDTH - ADD 10mm")
    //   3. COP is placed in the bottom (measured) half from the operator's
    //      gap-from-bottom / offset-from-left / size numbers; the top half
    //      is a mirror, so no COP geometry is needed above the fold midline.
    // No DraftSight types here so it can be unit-tested headlessly.
    public class LiftBlanketCalculator
    {
        private const double WidthAllowanceMm = 10.0;

        // Wall identifier labels ("<net> <name> L/R/B") are text height 20
        // in the client's reference DXFs — match that.
        private const double IdentifierTextHeight = 20.0;

        private readonly double _fixingAllowanceMm;

        public LiftBlanketCalculator(double fixingAllowanceMm)
        {
            _fixingAllowanceMm = fixingAllowanceMm;
        }

        public static double HalfHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return measuredHeight - fixingAllowanceMm;
        }

        public static double CutHeight(double measuredHeight, double fixingAllowanceMm)
        {
            return HalfHeight(measuredHeight, fixingAllowanceMm) * 2.0;
        }

        public static double CutWidth(double summedSegments)
        {
            return summedSegments + WidthAllowanceMm;
        }

        // True if the COP fits entirely within the bottom (measured) half of
        // the doubled cut piece. The blanket folds at the midline (= half =
        // measuredHeight - fixingAllowance); a COP whose top edge
        // (GapFromBottom + Height) exceeds that would bleed into the mirrored
        // top panel, which is geometrically wrong.
        public static bool CopFitsInBottomHalf(
            double copGapFromBottom, double copHeight,
            double measuredHeight, double fixingAllowanceMm)
        {
            double half = HalfHeight(measuredHeight, fixingAllowanceMm);
            return copGapFromBottom + copHeight <= half + 0.001;
        }

        public WallLayout LayoutWall(
            WallDimensions wall,
            double originX,
            string projectTag,
            string suffix)
        {
            double cutWidth = CutWidth(wall.Width);

            // Assumes upstream validation guarantees MeasuredHeight > the
            // fixing allowance; otherwise half goes negative and the cut
            // rect inverts. The dialog enforces a positive measured height.
            double half = HalfHeight(wall.MeasuredHeight, _fixingAllowanceMm);
            double cutHeight = CutHeight(wall.MeasuredHeight, _fixingAllowanceMm);

            var layout = new WallLayout
            {
                CutRect = new RectSpec(originX, 0, originX + cutWidth, cutHeight),
                FoldMidlineY = half,
                Dimensions = new List<DimSpec>(),
            };

            if (wall.Cop.Enabled)
            {
                double copX0 = originX + wall.Cop.OffsetFromLeft;
                double copY0 = wall.Cop.GapFromBottom;
                layout.CopRect = new RectSpec(
                    copX0,
                    copY0,
                    copX0 + wall.Cop.Width,
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
