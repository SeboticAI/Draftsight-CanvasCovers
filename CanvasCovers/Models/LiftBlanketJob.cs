namespace CanvasCovers.Models
{
    public class LiftBlanketJob
    {
        public ProjectMetadata Project { get; set; } = new ProjectMetadata();

        public WallDimensions LeftWall { get; set; } = new WallDimensions();

        public WallDimensions RightWall { get; set; } = new WallDimensions();

        public WallDimensions RearWall { get; set; } = new WallDimensions();

        public JobOptions Options { get; set; } = new JobOptions();

        public LayerSettings Layers { get; set; } = new LayerSettings();
    }
}
