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

        // The total mm added to a wall's cut width as a manufacturing edge
        // allowance. Split evenly: half on the left edge, half on the right,
        // and the same half is the inset of quilting from the bottom + side
        // outlines. Defaults to 10 (→ 5mm each side). Operator-editable; 0
        // means the cut equals the raw segment sum.
        public double EdgeAllowanceMm { get; set; } = 10;

        // Target gap between the horizontal quilt lines (running up the
        // height). The actual count is rounded so the lines divide the
        // quilted region evenly — see LiftBlanketCalculator.QuiltLines.
        // Vertical quilt lines even-divide the bounded width to a similar gap.
        public double VerticalQuiltingSpacingMm { get; set; } = 700;

        // Quilting is now built. On by default; the operator can disable it.
        public bool QuiltingEnabled { get; set; } = true;
    }
}
