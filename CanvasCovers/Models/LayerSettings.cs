using System.Collections.Generic;
using System.Linq;

namespace CanvasCovers.Models
{
    // The cutter's full layer set plus the role->layer assignment that tells
    // the generator which layer each kind of entity is drawn on.
    //
    // The six layers mirror Adelaide Annexes & Canvas's cutter setup
    // (0, the four tool-blade layers, and "5 Draw and Text"). All are created
    // in the generated DXF every time so the cutter file always carries the
    // complete layer set, even when some layers hold no geometry. (Defpoints
    // was removed per the beta review — item 19.)
    //
    // The generator draws four ROLES — the cut outline, the COP (draw/score),
    // wall labels (annotation) and the project/dimension text (title block).
    // Each role points at one of the layers by name. Defaults: the cut
    // outline on "1 Rotary Blade", COP + annotation on "5 Draw and Text",
    // title block on "0" — matching the reference DXFs.
    //
    // ACI (AutoCAD Color Index): 1=Red, 2=Yellow, 3=Green, 4=Cyan, 5=Blue,
    // 6=Magenta, 7=White/Black.
    public class LayerSettings
    {
        // The full cutter layer set (name + colour), created in every DXF.
        public List<LayerSetting> Layers { get; set; }

        // Role -> layer-name assignments. The generator looks up the layer by
        // name in Layers to find the colour to create it with.
        public string OutlineLayer { get; set; }
        public string CopLayer { get; set; }
        public string AnnotationLayer { get; set; }
        public string TitleblockLayer { get; set; }

        public LayerSettings()
        {
            Layers = DefaultLayers();
            OutlineLayer = "1 Rotary Blade";
            CopLayer = "5 Draw and Text";
            AnnotationLayer = "5 Draw and Text";
            TitleblockLayer = "0";
        }

        // The six cutter layers with their standard ACI colours.
        public static List<LayerSetting> DefaultLayers()
        {
            return new List<LayerSetting>
            {
                new LayerSetting("0", 7),
                new LayerSetting("1 Rotary Blade", 5),
                new LayerSetting("2 Drag Blade", 4),
                new LayerSetting("3 Crease Tool", 3),
                new LayerSetting("4 Drill Tool", 1),
                new LayerSetting("5 Draw and Text", 6),
            };
        }

        // The LayerSetting (name + colour) a role resolves to, or a sensible
        // fallback if the assigned name isn't in the list.
        public LayerSetting Resolve(string layerName, LayerSetting fallback)
        {
            LayerSetting match = Layers?.FirstOrDefault(l => l.Name == layerName);
            return match ?? fallback;
        }

        // Convenience accessors the generator uses — each resolves the role's
        // assigned layer name to the actual LayerSetting in the list.
        public LayerSetting Outline => Resolve(OutlineLayer, new LayerSetting("1 Rotary Blade", 5));
        public LayerSetting Cop => Resolve(CopLayer, new LayerSetting("5 Draw and Text", 6));
        public LayerSetting Annotation => Resolve(AnnotationLayer, new LayerSetting("5 Draw and Text", 6));
        public LayerSetting Titleblock => Resolve(TitleblockLayer, new LayerSetting("0", 7));
    }
}
