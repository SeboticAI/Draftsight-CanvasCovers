using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanvasCovers.Models;

namespace CanvasCovers.UI.Controls
{
    // The cutter's full layer set as editable rows: each layer has a name, an
    // ACI colour (swatch dropdown), and four role CHECKBOXES (Cut outline /
    // COP / Annotation / Title block) marking which entity kinds draw on it.
    // A layer may carry several roles (e.g. "5 Draw and Text" is both COP and
    // Annotation by default); a role belongs to exactly one layer, so ticking
    // it on one row unticks it on any other.
    public partial class LayersPanel : UserControl
    {
        private enum Role { Outline, Cop, Annotation, Titleblock }

        private static readonly (Role Role, string Label)[] RoleColumns =
        {
            (Role.Outline,    "Cut"),
            (Role.Cop,        "COP"),
            (Role.Annotation, "Annot."),
            (Role.Titleblock, "Title"),
        };

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

        public void Apply(LayerSettings settings)
        {
            if (settings == null) settings = new LayerSettings();
            BuildRows(settings.Layers ?? LayerSettings.DefaultLayers());

            SetRoleLayer(Role.Outline, settings.OutlineLayer);
            SetRoleLayer(Role.Cop, settings.CopLayer);
            SetRoleLayer(Role.Annotation, settings.AnnotationLayer);
            SetRoleLayer(Role.Titleblock, settings.TitleblockLayer);
        }

        public void ResetToDefaults() => Apply(new LayerSettings());

        public LayerSettings Read(List<string> errors)
        {
            var settings = new LayerSettings
            {
                Layers = _rows.Select(r => new LayerSetting(r.LayerName, r.SelectedAci())).ToList(),
                OutlineLayer = LayerForRole(Role.Outline),
                CopLayer = LayerForRole(Role.Cop),
                AnnotationLayer = LayerForRole(Role.Annotation),
                TitleblockLayer = LayerForRole(Role.Titleblock),
            };

            if (settings.OutlineLayer == null) errors.Add("Tick a layer's 'Cut' box (the cut outline needs a layer).");
            if (settings.CopLayer == null) errors.Add("Tick a layer's 'COP' box.");
            if (settings.AnnotationLayer == null) errors.Add("Tick a layer's 'Annot.' box.");
            if (settings.TitleblockLayer == null) errors.Add("Tick a layer's 'Title' box.");

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
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // colour
                for (int c = 0; c < RoleColumns.Length; c++)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

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

                for (int c = 0; c < RoleColumns.Length; c++)
                {
                    Role role = RoleColumns[c].Role;
                    var cb = new CheckBox
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = role,
                    };
                    cb.Checked += RoleCheck_Changed;
                    row.RoleChecks[role] = cb;
                    Grid.SetColumn(cb, 2 + c);
                    grid.Children.Add(cb);
                }

                RowsPanel.Children.Add(grid);
                _rows.Add(row);
            }
        }

        private static ComboBox BuildColorCombo(int aci)
        {
            var combo = new ComboBox { Height = 24, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var opt in AciOptions) combo.Items.Add(MakeColorItem(opt.Aci, opt.Label));
            SelectAci(combo, aci);
            return combo;
        }

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

        // ---- role syncing (a role belongs to exactly one layer) ----

        private void RoleCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressRoleSync) return;
            var changed = sender as CheckBox;
            if (changed?.Tag is not Role role) return;

            // Untick that role on every OTHER row.
            _suppressRoleSync = true;
            foreach (LayerRow row in _rows)
            {
                if (row.RoleChecks.TryGetValue(role, out CheckBox cb) && cb != changed)
                    cb.IsChecked = false;
            }
            _suppressRoleSync = false;
        }

        private void SetRoleLayer(Role role, string layerName)
        {
            _suppressRoleSync = true;
            foreach (LayerRow row in _rows)
            {
                if (row.RoleChecks.TryGetValue(role, out CheckBox cb))
                    cb.IsChecked = row.LayerName == layerName;
            }
            _suppressRoleSync = false;
        }

        private string LayerForRole(Role role)
        {
            LayerRow row = _rows.FirstOrDefault(r =>
                r.RoleChecks.TryGetValue(role, out CheckBox cb) && cb.IsChecked == true);
            return row?.LayerName;
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

        private sealed class LayerRow
        {
            public LayerRow(string layerName) { LayerName = layerName; }
            public string LayerName { get; }
            public ComboBox ColorCombo { get; set; }
            public readonly Dictionary<Role, CheckBox> RoleChecks = new Dictionary<Role, CheckBox>();

            public int SelectedAci()
            {
                return (ColorCombo?.SelectedItem as ComboBoxItem)?.Tag is int a ? a : 7;
            }
        }
    }
}
