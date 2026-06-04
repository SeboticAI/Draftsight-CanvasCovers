namespace CanvasCovers.Models
{
    // Default layer name + ACI colour pairs that map each entity-purpose
    // to a layer the downstream CAM software understands. The defaults
    // mirror the layer convention found in Adelaide Annexes & Canvas's
    // existing cutter-ready DXFs (reference/dxf/*.dxf): the cutter cuts
    // whatever is on "1 Rotary Blade"; "5 Draw and Text" is for the
    // title block and labels and is never cut. Operator can override
    // per-job if a different machine expects different names.
    //
    // ACI (AutoCAD Color Index) reference: 1=Red, 2=Yellow, 3=Green,
    // 4=Cyan, 5=Blue, 6=Magenta, 7=White/Black.
    public class LayerSettings
    {
        public LayerSetting Outline { get; set; } = new LayerSetting("1 Rotary Blade", 5);

        // COP rectangle is a draw/score feature in the client's reference
        // DXFs — it lives on "5 Draw and Text", NOT the cut layer.
        public LayerSetting Cop { get; set; } = new LayerSetting("5 Draw and Text", 6);

        // Wall identifier label per wall ("<network> <project> L/R/REAR").
        // The client's reference DXFs put this on the magenta "5 Draw and
        // Text" layer.
        public LayerSetting Annotation { get; set; } = new LayerSetting("5 Draw and Text", 6);

        // Project info + dimensions + worksheet reference blocks. Layer "0"
        // is DraftSight's built-in default and renders white on dark themes /
        // black on light themes — matches the worksheet text scattered
        // around the client's reference DXFs.
        public LayerSetting Titleblock { get; set; } = new LayerSetting("0", 7);
    }
}
