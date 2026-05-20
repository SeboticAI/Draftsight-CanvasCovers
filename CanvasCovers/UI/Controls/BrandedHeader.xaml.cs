using System.Windows.Controls;

namespace CanvasCovers.UI.Controls
{
    public partial class BrandedHeader : UserControl
    {
        public BrandedHeader()
        {
            InitializeComponent();
        }

        public string Subtitle
        {
            get => SubtitleText.Text;
            set => SubtitleText.Text = value ?? string.Empty;
        }
    }
}
