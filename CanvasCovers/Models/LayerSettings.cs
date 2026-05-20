namespace CanvasCovers.Models
{
    // Default layer name + ACI color pairs that map each entity-purpose
    // to a layer the downstream CAM software understands. Defaults are
    // baked in here and surfaced in the UI so the operator can override
    // them if the machine they're cutting on uses a different convention.
    //
    // ACI (AutoCAD Color Index) reference: 1=Red, 2=Yellow, 3=Green,
    // 4=Cyan, 5=Blue, 6=Magenta, 7=White/Black.
    public class LayerSettings
    {
        public LayerSetting Outline { get; set; } = new LayerSetting("CC-Outline", 1);

        public LayerSetting Cop { get; set; } = new LayerSetting("CC-COP", 5);

        public LayerSetting Annotation { get; set; } = new LayerSetting("CC-Annotation", 3);

        public LayerSetting Titleblock { get; set; } = new LayerSetting("CC-Titleblock", 2);
    }
}
