namespace CanvasCovers.Models.Products.LiftBlanket
{
    public class WallDimensions
    {
        public bool Enabled { get; set; } = true;

        public double MainWidth { get; set; } = 1400;

        public double MainHeight { get; set; } = 2150;

        public double DoorReturn1 { get; set; }

        public double DoorReturn2 { get; set; }

        public double DoorReturn3 { get; set; }

        public bool CopEnabled { get; set; }

        public double CopTopOffset { get; set; } = 150;

        public double CopHeight { get; set; } = 1300;

        public double CopWidth { get; set; } = 600;

        public double TotalLength => MainWidth + DoorReturn1 + DoorReturn2 + DoorReturn3;
    }
}
