namespace CanvasCovers.Models
{
    public enum FixingType
    {
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
    }

    public class JobOptions
    {
        public bool ThroughCar { get; set; }

        public bool PlasticCoverOnCop { get; set; }

        public FixingType Fixings { get; set; } = FixingType.HooksFacingOut;
    }
}
