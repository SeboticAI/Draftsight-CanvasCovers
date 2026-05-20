namespace CanvasCovers.Models.Products.LiftBlanket
{
    public class LiftBlanketJob
    {
        public ProjectMetadata Project { get; set; } = new ProjectMetadata();

        public LayerSettings Layers { get; set; } = new LayerSettings();

        public WallDimensions LeftWall { get; set; } = new WallDimensions();

        public WallDimensions RightWall { get; set; } = new WallDimensions();

        public WallDimensions RearWall { get; set; } = new WallDimensions();

        public LiftBlanketOptions Options { get; set; } = new LiftBlanketOptions();
    }
}
