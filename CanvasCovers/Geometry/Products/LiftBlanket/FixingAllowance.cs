using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Default hook/stud/eyelet allowance subtracted from the measured
    // height before doubling. Values from the FIXINGS table stamped on
    // every client drawing: HOOKS -50, PRESS STUDS -40, EYELET TG9/TG7 -30,
    // VELCRO 0.
    //
    // Eyelet is 30 (TG9 and TG7 are display labels only, same value).
    // Self-adhesive Velcro is a distinct type but also 0. Each FixingType has
    // its own case; the default→0 branch is only a backstop for any future
    // member added without a case.
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
                case FixingType.Eyelet:
                    return 30.0;
                case FixingType.Velcro:
                case FixingType.SelfAdhesiveVelcro:
                    return 0.0;
                default:
                    return 0.0;
            }
        }
    }
}
