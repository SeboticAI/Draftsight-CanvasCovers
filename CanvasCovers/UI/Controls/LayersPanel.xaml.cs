using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanvasCovers.Models;

namespace CanvasCovers.UI.Controls
{
    // The cutter's full layer set as editable rows: each layer has a name, an
    // ACI colour (swatch dropdown) and a "used for" role dropdown. The four
    // roles the generator draws (outline / COP / annotation / title block) are
    // assigned to layers here; a role maps to exactly one layer, so selecting
    // it on one row clears it from any other.
    public partial class LayersPanel : UserControl
    {
        // Role identity for the "used for" dropdown.
        private enum Role { None, Outline, Cop, Annotation, Titleblock }

        private static readonly (Role Role, string Label)[] RoleOptions =
        {
            (Role.None,       "—"),
            (Role.Outline,    "Cut outline"),
            (Role.Cop,        "COP (draw)"),
            (Role.Annotation, "Annotation"),
            (Role.Titleblock, "Title block"),
        };

        // Standard ACI palette offered in the colour dropdown.
        private static readonly (int Aci, string Label)[] AciOptions =
        {
            (7, "White"), (1, "Red"), (2, "Yellow"), (3, "Green"),
            (4, "Cyan"),  (5, "Blue"), (6, "Magenta"),
        };

        private readonly List<LayerRow> _rows = new List<LayerRow>();
        private bool _suppressRoleSync;

        public LayersPanel()
        {
            InitializeComponent();
            ResetToDefaults();
        }

        // Builds whatever rows are needed for the given settings, then fills
        // colours + role selections from them.
        public void Apply(LayerSettings settings)
        {
            if (settings == null) settings = new LayerSettings();
            BuildRows(settings.Layers ?? LayerSettings.DefaultLayers());

            SetRoleOnLayer(settings.OutlineLayer, Role.Outline);
            SetRoleOnLayer(settings.CopLayer, Role.Cop);
            SetRoleOnLayer(settings.AnnotationLayer, Role.Annotation);
            SetRoleOnLayer(settings.TitleblockLayer, Role.Titleblock);
        }

        public void ResetToDefaults()
        {
            Apply(new LayerSettings());
        }

        // Reads the rows back into a LayerSettings. Every role must be assigned
        // to exactly one layer; a missing assignment is an error (the generator
        // needs a destination for each role).
        public LayerSettings Read(List<string> errors)
        {
            var settings = new LayerSettings
            {
                Layers = _rows.Select(r => new LayerSetting(r.LayerName, r.SelectedAci())).ToList(),
            };

            settings.OutlineLayer = LayerForRole(Role.Outline);
            settings.CopLayer = LayerForRole(Role.Cop);
            settings.AnnotationLayer = LayerForRole(Role.Annotation);
            settings.TitleblockLayer = LayerForRole(Role.Titleblock);

            if (settings.OutlineLayer == null) errors.Add("Assign a layer to 'Cut outline'.");
            if (settings.CopLayer == null) errors.Add("Assign a layer to 'COP (draw)'.");
            if (settings.AnnotationLayer == null) errors.Add("Assign a layer to 'Annotation'.");
            if (settings.TitleblockLayer == null) errors.Add("Assign a layer to 'Title block'.");

            return settings;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e) => ResetToDefaults();

        // ---- row construction ----

        private void BuildRows(IEnumerable<LayerSetting> layers)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();

            foreach (LayerSetting layer in layers)
            {
                var row = new LayerRow(layer.Name);

                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var name = new TextBlock
                {
                    Text = layer.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    Margin = new Thickness(0, 0, 8, 0),
                };
                Grid.SetColumn(name, 0);
                grid.Children.Add(name);

                row.ColorCombo = BuildColorCombo(layer.ColorIndex);
                Grid.SetColumn(row.ColorCombo, 1);
                grid.Children.Add(row.ColorCombo);

                row.RoleCombo = BuildRoleCombo();
                row.RoleCombo.SelectionChanged += RoleCombo_SelectionChanged;
                Grid.SetColumn(row.RoleCombo, 2);
                grid.Children.Add(row.RoleCombo);

                RowsPanel.Children.Add(grid);
                _rows.Add(row);
            }
        }

        private static ComboBox BuildColorCombo(int aci)
        {
            var combo = new ComboBox { Height = 24, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var opt in AciOptions)
            {
                combo.Items.Add(MakeColorItem(opt.Aci, opt.Label));
            }
            SelectAci(combo, aci);
            return combo;
        }

        // A colour combo item: a swatch + the colour name, tagged with the ACI.
        private static ComboBoxItem MakeColorItem(int aci, string label)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new Border
            {
                Width = 22, Height = 14,
                Background = AciToBrush(aci),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            return new ComboBoxItem { Content = panel, Tag = aci };
        }

        private ComboBox BuildRoleCombo()
        {
            var combo = new ComboBox { Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var opt in RoleOptions)
            {
                combo.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Role });
            }
            combo.SelectedIndex = 0; // None
            return combo;
        }

        // ---- role syncing (a role belongs to exactly one layer) ----

        private void RoleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressRoleSync) return;
            var changed = sender as ComboBox;
            Role role = RoleOf(changed);
            if (role == Role.None) return;

            // Clear that role from every OTHER row so it's assigned once only.
            _suppressRoleSync = true;
            foreach (LayerRow row in _rows)
            {
                if (row.RoleCombo != changed && RoleOf(row.RoleCombo) == role)
                    SelectRole(row.RoleCombo, Role.None);
            }
            _suppressRoleSync = false;
        }

        private void SetRoleOnLayer(string layerName, Role role)
        {
            LayerRow row = _rows.FirstOrDefault(r => r.LayerName == layerName);
            if (row == null) return;
            _suppressRoleSync = true;
            SelectRole(row.RoleCombo, role);
            _suppressRoleSync = false;
        }

        private string LayerForRole(Role role)
        {
            LayerRow row = _rows.FirstOrDefault(r => RoleOf(r.RoleCombo) == role);
            return row?.LayerName;
        }

        private static Role RoleOf(ComboBox combo)
        {
            return (combo?.SelectedItem as ComboBoxItem)?.Tag is Role r ? r : Role.None;
        }

        private static void SelectRole(ComboBox combo, Role role)
        {
            foreach (object item in combo.Items)
            {
                if (item is ComboBoxItem ci && ci.Tag is Role r && r == role)
                {
                    combo.SelectedItem = ci;
                    return;
                }
            }
        }

        private static void SelectAci(ComboBox combo, int aci)
        {
            foreach (object item in combo.Items)
            {
                if (item is ComboBoxItem ci && ci.Tag is int a && a == aci)
                {
                    combo.SelectedItem = ci;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
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
                case 7: return Brushes.White;
                default: return Brushes.LightGray;
            }
        }

        // Holds the controls for one layer row.
        private sealed class LayerRow
        {
            public LayerRow(string layerName) { LayerName = layerName; }
            public string LayerName { get; }
            public ComboBox ColorCombo { get; set; }
            public ComboBox RoleCombo { get; set; }

            public int SelectedAci()
            {
                return (ColorCombo?.SelectedItem as ComboBoxItem)?.Tag is int a ? a : 7;
            }
        }
    }
}
