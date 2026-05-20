using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.UI.Products.LiftBlanket
{
    public partial class LiftBlanketWindow : Window
    {
        public LiftBlanketWindow()
        {
            InitializeComponent();
            Header.Subtitle = "Lift Blanket Generator";
        }

        public LiftBlanketJob Job { get; private set; }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            List<string> errors = new List<string>();

            ProjectMetadata project = MetadataPanel.Read();
            LiftBlanketOptions options = ReadOptions();

            WallDimensions left = ReadWall(
                LeftWallEnabled, LeftMainWidth, LeftMainHeight,
                LeftDoorReturn1, LeftDoorReturn2, LeftDoorReturn3,
                LeftCopEnabled, LeftCopTopOffset, LeftCopHeight, LeftCopWidth,
                "Left wall", errors);

            WallDimensions right = ReadWall(
                RightWallEnabled, RightMainWidth, RightMainHeight,
                RightDoorReturn1, RightDoorReturn2, RightDoorReturn3,
                RightCopEnabled, RightCopTopOffset, RightCopHeight, RightCopWidth,
                "Right wall", errors);

            WallDimensions rear = ReadRearWall(RearWallEnabled, RearWidth, RearHeight, errors);

            LayerSettings layers = LayersControl.Read(errors);

            if (errors.Count > 0)
            {
                ShowError(string.Join(Environment.NewLine, errors));
                return;
            }

            Job = new LiftBlanketJob
            {
                Project = project,
                Options = options,
                LeftWall = left,
                RightWall = right,
                RearWall = rear,
                Layers = layers,
            };
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ThroughCarOption_Changed(object sender, RoutedEventArgs e)
        {
            // Through Car means no rear wall — disable the checkbox so the
            // operator isn't tempted to fill values in only to have them
            // ignored at generation time. Don't force the checkbox back ON
            // when Through Car is unticked — the user may have intentionally
            // disabled the rear wall for an unrelated reason.
            if (RearWallEnabled == null) return; // event can fire during XAML init

            bool through = ThroughCarOption.IsChecked == true;
            if (through)
            {
                RearWallEnabled.IsChecked = false;
                RearWallEnabled.IsEnabled = false;
            }
            else
            {
                RearWallEnabled.IsEnabled = true;
            }
        }

        private LiftBlanketOptions ReadOptions()
        {
            FixingType fixing = FixingType.HooksFacingOut;
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            string tag = selected?.Tag as string;
            if (!string.IsNullOrEmpty(tag) && Enum.TryParse(tag, out FixingType parsed))
            {
                fixing = parsed;
            }

            return new LiftBlanketOptions
            {
                ThroughCar = ThroughCarOption.IsChecked == true,
                PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
                Fixings = fixing,
            };
        }

        private WallDimensions ReadWall(
            CheckBox enabledBox,
            TextBox widthBox, TextBox heightBox,
            TextBox dr1, TextBox dr2, TextBox dr3,
            CheckBox copBox, TextBox copTop, TextBox copHeight, TextBox copWidth,
            string wallLabel,
            List<string> errors)
        {
            WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };

            if (!wall.Enabled)
            {
                return wall;
            }

            wall.MainWidth = ReadPositive(widthBox.Text, wallLabel + " main width", errors);
            wall.MainHeight = ReadPositive(heightBox.Text, wallLabel + " main height", errors);
            wall.DoorReturn1 = ReadNonNegative(dr1.Text, wallLabel + " door return 1", errors);
            wall.DoorReturn2 = ReadNonNegative(dr2.Text, wallLabel + " door return 2", errors);
            wall.DoorReturn3 = ReadNonNegative(dr3.Text, wallLabel + " door return 3", errors);
            wall.CopEnabled = copBox.IsChecked == true;

            if (wall.CopEnabled)
            {
                wall.CopTopOffset = ReadNonNegative(copTop.Text, wallLabel + " COP top offset", errors);
                wall.CopHeight = ReadPositive(copHeight.Text, wallLabel + " COP height", errors);
                wall.CopWidth = ReadPositive(copWidth.Text, wallLabel + " COP width", errors);

                if (wall.MainHeight > 0 && wall.CopHeight > 0 && wall.CopTopOffset + wall.CopHeight > wall.MainHeight)
                {
                    errors.Add(wallLabel + " COP top offset + COP height exceeds main height.");
                }
                if (wall.MainWidth > 0 && wall.CopWidth > 0 && wall.CopWidth > wall.MainWidth)
                {
                    errors.Add(wallLabel + " COP width exceeds main width.");
                }
            }

            return wall;
        }

        private WallDimensions ReadRearWall(CheckBox enabledBox, TextBox widthBox, TextBox heightBox, List<string> errors)
        {
            WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };
            if (!wall.Enabled)
            {
                return wall;
            }

            wall.MainWidth = ReadPositive(widthBox.Text, "Rear wall width", errors);
            wall.MainHeight = ReadPositive(heightBox.Text, "Rear wall height", errors);
            wall.CopEnabled = false;
            return wall;
        }

        private static double ReadPositive(string text, string label, List<string> errors)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value <= 0)
            {
                errors.Add(label + " must be a number greater than zero.");
                return 0;
            }
            return value;
        }

        private static double ReadNonNegative(string text, string label, List<string> errors)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
            {
                errors.Add(label + " must be zero or a positive number.");
                return 0;
            }
            return value;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void ClearError()
        {
            ErrorText.Text = string.Empty;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
