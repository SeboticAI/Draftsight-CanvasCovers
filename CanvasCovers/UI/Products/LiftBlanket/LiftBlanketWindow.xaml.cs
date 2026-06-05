using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;
using CanvasCovers.UI.Controls;

namespace CanvasCovers.UI.Products.LiftBlanket
{
    public partial class LiftBlanketWindow : Window
    {
        // Fired once the operator has clicked Generate and validation passed.
        // The window is shown non-modally so the operator can pan and zoom
        // inside DraftSight while the form is open — the caller subscribes
        // and runs the generator from this event handler. If generation
        // fails the handler should set e.Cancel = true so the dialog stays
        // open and the operator can fix inputs and try again.
        public event EventHandler<GenerateRequestedEventArgs> GenerateRequested;

        public LiftBlanketWindow()
        {
            InitializeComponent();
            Header.Subtitle = "Lift Blanket Generator";
            Loaded += LiftBlanketWindow_Loaded;
        }

        private void LiftBlanketWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Seed the three blankets. Left/Right default with Seg3 1400 (a
            // meaningful default); Rear is COP-less.
            LeftBlanket.Configure(isRear: false);
            LeftBlanket.Seed("0", "0", "240", "1400", "0", "2200");
            RightBlanket.Configure(isRear: false);
            RightBlanket.Seed("0", "0", "240", "1400", "0", "2200");
            RearBlanket.Configure(isRear: true);
            RearBlanket.Seed("0", "0", "0", "1400", "0", "2200");
            PushSharedParams();
        }

        // Pushes the Options-panel values (allowances, quilting) into every
        // blanket so each live preview computes the same fold + COP geometry.
        private void PushSharedParams()
        {
            double fixing = ParseOr(FixingAllowanceInput.Text, 50);
            double edge = ParseOr(EdgeAllowanceInput.Text, 10);
            double quilt = ParseOr(QuiltingSpacingInput.Text, 700);
            bool quiltOn = QuiltingOption.IsChecked == true;
            LeftBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
            RightBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
            RearBlanket.SetSharedParams(fixing, edge, quilt, quiltOn);
        }

        private void SharedParam_Changed(object sender, RoutedEventArgs e)
        {
            if (LeftBlanket == null) return; // fires during XAML init
            PushSharedParams();
        }

        private static double ParseOr(string s, double fallback)
        {
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        public LiftBlanketJob Job { get; private set; }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            List<string> errors = new List<string>();

            ProjectMetadata project = MetadataPanel.Read();
            LiftBlanketOptions options = ReadOptions();

            WallDimensions left = ReadBlanket(LeftBlanket, "Left wall", errors);
            WallDimensions right = ReadBlanket(RightBlanket, "Right wall", errors);
            WallDimensions rear = ReadBlanket(RearBlanket, "Rear wall", errors);

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

            // Hand the validated job to the subscriber (typically the
            // OpenCanvasCoversCommand). The subscriber runs the generator
            // synchronously; if anything goes wrong (no active drawing,
            // SDK exception, etc.) the subscriber should set e.Cancel = true
            // so the dialog stays open for the operator to retry.
            GenerateRequestedEventArgs args = new GenerateRequestedEventArgs(Job);
            GenerateRequested?.Invoke(this, args);
            if (!args.Cancel)
            {
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void ThroughCarOption_Changed(object sender, RoutedEventArgs e)
        {
            if (RearBlanket == null || RearTab == null) return; // during init
            bool through = ThroughCarOption.IsChecked == true;
            if (through)
            {
                RearBlanket.SetWallEnabled(false);
                RearBlanket.SetWallEnabledInteractive(false);
                RearTab.IsEnabled = false;
            }
            else
            {
                RearBlanket.SetWallEnabledInteractive(true);
                RearTab.IsEnabled = true;
            }
        }

        private LiftBlanketOptions ReadOptions()
        {
            FixingType fixing = FixingType.HooksFacingOut;
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            string tag = selected?.Tag as string;
            if (!string.IsNullOrEmpty(tag) && Enum.TryParse(tag, out FixingType parsed)) fixing = parsed;

            double allowance = 50;
            double.TryParse(FixingAllowanceInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out allowance);

            return new LiftBlanketOptions
            {
                ThroughCar = ThroughCarOption.IsChecked == true,
                PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
                Fixings = fixing,
                FixingAllowanceMm = allowance,
            };
        }

        // True when the operator wants the DXF export dialog after a successful
        // generate. Read by OpenCanvasCoversCommand.
        public bool ExportDxfRequested => ExportDxfOption.IsChecked == true;

        private void FixingsInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FixingAllowanceInput == null) return; // fires during XAML init
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            string tag = selected?.Tag as string;
            if (!string.IsNullOrEmpty(tag) && Enum.TryParse(tag, out FixingType parsed))
            {
                FixingAllowanceInput.Text =
                    Geometry.Products.LiftBlanket.FixingAllowance
                        .DefaultFor(parsed).ToString(CultureInfo.InvariantCulture);
            }
            if (LeftBlanket != null) PushSharedParams();
        }

        private WallDimensions ReadBlanket(
            CanvasCovers.UI.Controls.WallBlanket blanket, string wallLabel, List<string> errors)
        {
            var wall = new WallDimensions { Enabled = blanket.WallEnabled };
            if (!wall.Enabled) return wall;

            wall.Segments.DoorReturnLeft  = ReadNonNegative(blanket.DrLeftText,  wallLabel + " door return (left)",  errors);
            wall.Segments.Seg1            = ReadNonNegative(blanket.Seg1Text,    wallLabel + " segment 1",            errors);
            wall.Segments.Seg2            = ReadNonNegative(blanket.Seg2Text,    wallLabel + " segment 2 (COP width)", errors);
            wall.Segments.Seg3            = ReadNonNegative(blanket.Seg3Text,    wallLabel + " segment 3",            errors);
            wall.Segments.DoorReturnRight = ReadNonNegative(blanket.DrRightText, wallLabel + " door return (right)", errors);
            wall.MeasuredHeight           = ReadPositive(blanket.MeasuredHeightText, wallLabel + " measured height", errors);

            if (wall.Segments.TotalWidth <= 0)
                errors.Add(wallLabel + " needs at least one non-zero segment.");

            wall.Cop.Enabled = blanket.CopEnabled;
            if (wall.Cop.Enabled)
            {
                wall.Cop.Height        = ReadPositive(blanket.CopHeightText,    wallLabel + " COP height", errors);
                wall.Cop.GapFromBottom = ReadNonNegative(blanket.CopGapBottomText, wallLabel + " COP gap from bottom", errors);

                if (wall.Segments.Seg2 <= 0)
                    errors.Add(wallLabel + " COP width (segment 2) must be greater than zero when COP is enabled.");

                double fixingForCop = ParseOr(FixingAllowanceInput.Text, 50);
                if (wall.MeasuredHeight > 0 && wall.Cop.Height > 0 &&
                    Geometry.Products.LiftBlanket.LiftBlanketCalculator.AutoTopGap(
                        wall.MeasuredHeight, fixingForCop, wall.Cop.GapFromBottom, wall.Cop.Height) < 0)
                {
                    errors.Add(wallLabel +
                        " COP gap-from-bottom + height crosses the fold line (must fit within the measured half = measured height − fixing allowance).");
                }
            }
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
