namespace CanvasCovers.Models.Products.LiftBlanket
{
    public enum FixingType
    {
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
    }

    public class LiftBlanketOptions
    {
        public bool ThroughCar { get; set; }

        public bool PlasticCoverOnCop { get; set; }

        public FixingType Fixings { get; set; } = FixingType.HooksFacingOut;

        // The mm subtracted from each wall's measured height before the
        // ×2 doubling. Defaults to the fixing type's standard allowance
        // (see FixingAllowance) but the operator can override per job —
        // the sheet's "height from top of hook ... to bottom of blanket"
        // note means this is a real per-job value.
        public double FixingAllowanceMm { get; set; } = 50;

        // The mm the quilting lines are pulled inward from the outline so the
        // pen doesn't catch a cut edge. Applied directly (not halved) on the
        // left, right and bottom of the quilted region. Default 5; operator
        // may set it lower. The OUTLINE itself is always the entered width —
        // no edge allowance is added (the operator adds their own shrinkage).
        public double QuiltInsetMm { get; set; } = 5;

        // Target gap between the horizontal quilt lines (running up the
        // height). The actual count is rounded so the lines divide the
        // quilted region evenly — see LiftBlanketCalculator.QuiltLines.
        // Vertical quilt lines even-divide the bounded width to a similar gap.
        public double VerticalQuiltingSpacingMm { get; set; } = 700;

        // Quilting is now built. On by default; the operator can disable it.
        public bool QuiltingEnabled { get; set; } = true;
    }
}
