using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanvasCovers.Models;

namespace CanvasCovers.UI.Controls
{
    public partial class LayersPanel : UserControl
    {
        public LayersPanel()
        {
            InitializeComponent();
            WireSwatches();
            // Seed every row from the model defaults so LayerSettings is the
            // single source of truth — the XAML Text values are just a
            // design-time placeholder and must not drift from the model
            // (the COP-on-draw-layer default in particular).
            ResetToDefaults();
        }

        // Reads all four rows, appending each error to the supplied list and
        // continuing past failures. Returns a settings object built from
        // whatever fields parsed cleanly; the caller decides to commit only
        // if the error list is empty.
        public LayerSettings Read(List<string> errors)
        {
            return new LayerSettings
            {
                Outline = ReadRow(OutlineName, OutlineAci, "Outline", new LayerSetting("1 Rotary Blade", 5), errors),
                Cop = ReadRow(CopName, CopAci, "COP", new LayerSetting("5 Draw and Text", 6), errors),
                Annotation = ReadRow(AnnotationName, AnnotationAci, "Annotation", new LayerSetting("5 Draw and Text", 6), errors),
                Titleblock = ReadRow(TitleblockName, TitleblockAci, "Title block", new LayerSetting("0", 7), errors),
            };
        }

        public void Apply(LayerSettings settings)
        {
            if (settings == null) return;
            ApplyRow(OutlineName, OutlineAci, settings.Outline);
            ApplyRow(CopName, CopAci, settings.Cop);
            ApplyRow(AnnotationName, AnnotationAci, settings.Annotation);
            ApplyRow(TitleblockName, TitleblockAci, settings.Titleblock);
        }

        public void ResetToDefaults()
        {
            Apply(new LayerSettings());
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetToDefaults();
        }

        private static LayerSetting ReadRow(TextBox nameBox, TextBox aciBox, string label, LayerSetting fallback, List<string> errors)
        {
            string name = nameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                errors.Add(label + " layer name is required.");
                name = fallback.Name;
            }

            int aci;
            if (!int.TryParse(aciBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out aci) || aci < 0 || aci > 255)
            {
                errors.Add(label + " layer ACI must be an integer between 0 and 255.");
                aci = fallback.ColorIndex;
            }

            return new LayerSetting(name, aci);
        }

        private static void ApplyRow(TextBox nameBox, TextBox aciBox, LayerSetting setting)
        {
            if (setting == null) return;
            nameBox.Text = setting.Name ?? string.Empty;
            aciBox.Text = setting.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        private void WireSwatches()
        {
            OutlineAci.TextChanged += (s, e) => UpdateSwatch(OutlineAci, OutlineSwatch);
            CopAci.TextChanged += (s, e) => UpdateSwatch(CopAci, CopSwatch);
            AnnotationAci.TextChanged += (s, e) => UpdateSwatch(AnnotationAci, AnnotationSwatch);
            TitleblockAci.TextChanged += (s, e) => UpdateSwatch(TitleblockAci, TitleblockSwatch);

            UpdateSwatch(OutlineAci, OutlineSwatch);
            UpdateSwatch(CopAci, CopSwatch);
            UpdateSwatch(AnnotationAci, AnnotationSwatch);
            UpdateSwatch(TitleblockAci, TitleblockSwatch);
        }

        private static void UpdateSwatch(TextBox aciBox, Border swatch)
        {
            if (!int.TryParse(aciBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int aci))
            {
                swatch.Background = Brushes.LightGray;
                return;
            }
            swatch.Background = AciToBrush(aci);
        }

        private static Brush AciToBrush(int aci)
        {
            switch (aci)
            {
                case 1: return Brushes.Red;
                case 2: return Brushes.Yellow;
                case 3: return new SolidColorBrush(Color.FromRgb(0, 176, 80));
                case 4: return Brushes.Cyan;
                case 5: return Brushes.Blue;
                case 6: return Brushes.Magenta;
                case 7: return Brushes.Black;
                default: return Brushes.LightGray;
            }
        }
    }
}
