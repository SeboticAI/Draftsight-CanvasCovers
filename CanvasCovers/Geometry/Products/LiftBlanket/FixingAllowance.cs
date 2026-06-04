using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Default hook/stud/eyelet allowance subtracted from the measured
    // height before doubling. Values from the FIXINGS table stamped on
    // every client drawing: HOOKS -50, PRESS STUDS -40, EYELET TG9/TG7 -30,
    // VELCRO 0.
    //
    // NOTE: FixingType has no Eyelet member yet (the TG9 vs TG7 distinction
    // is an open question for the client). The default branch returns 0,
    // which is only safe while the enum stays exhaustive — if an Eyelet
    // value is added, give it its own case returning 30.0, or it will
    // silently fall through to 0.
    //
    // The operator can override the actual value used per job via
    // LiftBlanketOptions.FixingAllowanceMm (added in a later step); this is
    // just the sensible default for each fixing type.
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
