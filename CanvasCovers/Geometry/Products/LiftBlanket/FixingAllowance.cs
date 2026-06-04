using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Default hook/stud/eyelet allowance subtracted from the measured
    // height before doubling. Values from the FIXINGS table stamped on
    // every client drawing: HOOKS -50, PRESS STUDS -40, EYELET -30,
    // VELCRO 0. The operator can override the actual value used per job
    // (see LiftBlanketOptions.FixingAllowanceMm); this is just the
    // sensible default for each fixing type.
    public static class FixingAllowance
    {
        public static double DefaultFor(FixingType fixing)
        {
            switch (fixing)
            {
                case FixingType.HooksFacingIn:
                case FixingType.HooksFacingOut:
                    return 50.0;
                case FixingType.PressStuds:
                    return 40.0;
                case FixingType.Velcro:
                    return 0.0;
                default:
                    return 0.0;
            }
        }
    }
}
