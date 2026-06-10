namespace CanvasCovers.Models.Products.LiftBlanket
{
    public enum FixingType
    {
        // "Not chosen yet". There is deliberately NO default fixing (round 2,
        // item 3) — Martin forgot to set it twice and had to edit drawings by
        // hand. The UI blocks Generate until a real fixing is selected, so
        // the generator never sees None.
        None,
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
        // TG7 vs TG9 eyelets calculate identically (-30) but the label prints
        // on the COP, so the ordered size must be distinguishable (item 3).
        Eyelet7,
        Eyelet9,
        SelfAdhesiveVelcro,
    }

    public class LiftBlanketOptions
    {
        public bool ThroughCar { get; set; }

        public bool PlasticCoverOnCop { get; set; }

        public FixingType Fixings { get; set; } = FixingType.None;

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

        // Stamp a "BAG" reminder inside the COP cutout when a storage bag is
        // required (item 14). Drawn vertical to fit the cutout.
        public bool BagRequired { get; set; }

        // Stamp a glass-behind label (item 15).
        public bool GlassBehind { get; set; }
    }
}
