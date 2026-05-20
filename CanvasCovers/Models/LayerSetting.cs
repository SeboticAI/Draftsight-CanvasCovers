namespace CanvasCovers.Models
{
    public class LayerSetting
    {
        public LayerSetting() { }

        public LayerSetting(string name, int colorIndex)
        {
            Name = name;
            ColorIndex = colorIndex;
        }

        public string Name { get; set; }

        public int ColorIndex { get; set; }
    }
}
