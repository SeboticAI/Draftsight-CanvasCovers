using System.Windows;
using CanvasCovers.Models;

namespace CanvasCovers.UI
{
    public partial class ProductPickerWindow : Window
    {
        public ProductPickerWindow()
        {
            InitializeComponent();
            Header.Subtitle = "Pick a product to generate";
        }

        public ProductKind? SelectedProduct { get; private set; }

        private void LiftBlanketTile_Click(object sender, RoutedEventArgs e)
        {
            SelectedProduct = ProductKind.LiftBlanket;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
