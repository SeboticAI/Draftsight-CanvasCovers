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

        // Reserved for a later round. The reference DXFs carry quilt lines
        // but the measurement sheet has no quilt input and the spacing rule
        // is unconfirmed, so generation is gated off for now.
        public bool QuiltingEnabled { get; set; }
    }
}
