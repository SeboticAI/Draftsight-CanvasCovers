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
            // Put the cursor straight into the first measurement so the
            // operator can start typing without clicking. Also pre-target the
            // wall diagram to the left wall (it defaults there anyway but
            // belt-and-braces in case ShowWall got skipped on initial paint).
            LeftSeg3.Focus();
            LeftSeg3.SelectAll();
            Diagram.ShowWall(WallContext.Left);
        }

        // The Border around each wall section sets Tag="Left|Right|Rear".
        // Fires on either MouseEnter or GotKeyboardFocus so the diagram
        // retargets the moment the user shows interest in the section, not
        // only when a field is clicked.
        private void WallSection_Activated(object sender, RoutedEventArgs e)
        {
            FrameworkElement section = sender as FrameworkElement;
            string tag = section?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            switch (tag)
            {
                case "Left":  Diagram.ShowWall(WallContext.Left); break;
                case "Right": Diagram.ShowWall(WallContext.Right); break;
                case "Rear":  Diagram.ShowWall(WallContext.Rear); break;
            }
        }

        // Each dimension input carries Tag="<DimensionKey>" matching a constant
        // on WallDiagram. Focus the field → that part of the diagram lights up.
        private void DimField_GotFocus(object sender, RoutedEventArgs e)
        {
            FrameworkElement field = sender as FrameworkElement;
            string key = field?.Tag as string;
            if (string.IsNullOrEmpty(key)) return;
            Diagram.Highlight(key);
        }

        public LiftBlanketJob Job { get; private set; }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            List<string> errors = new List<string>();

            ProjectMetadata project = MetadataPanel.Read();
            LiftBlanketOptions options = ReadOptions();

            WallDimensions left = ReadWall(
                LeftWallEnabled, LeftDrLeft, LeftSeg1, LeftSeg2, LeftSeg3, LeftDrRight,
                LeftMeasuredHeight,
                LeftCopEnabled, LeftCopWidth, LeftCopHeight, LeftCopGapBottom, LeftCopOffsetLeft,
                "Left wall", errors);

            WallDimensions right = ReadWall(
                RightWallEnabled, RightDrLeft, RightSeg1, RightSeg2, RightSeg3, RightDrRight,
                RightMeasuredHeight,
                RightCopEnabled, RightCopWidth, RightCopHeight, RightCopGapBottom, RightCopOffsetLeft,
                "Right wall", errors);

            WallDimensions rear = ReadRearWall(
                RearWallEnabled, RearDrLeft, RearSeg1, RearSeg2, RearSeg3, RearDrRight,
                RearMeasuredHeight, errors);

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
        }

        private WallDimensions ReadWall(
            CheckBox enabledBox,
            TextBox drLeft, TextBox seg1, TextBox seg2, TextBox seg3, TextBox drRight,
            TextBox measuredHeight,
            CheckBox copBox, TextBox copWidth, TextBox copHeight, TextBox copGapBottom, TextBox copOffsetLeft,
            string wallLabel,
            List<string> errors)
        {
            WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };
            if (!wall.Enabled) return wall;

            wall.Segments.DoorReturnLeft  = ReadNonNegative(drLeft.Text,  wallLabel + " door return (left)",  errors);
            wall.Segments.Seg1            = ReadNonNegative(seg1.Text,    wallLabel + " segment 1",            errors);
            wall.Segments.Seg2            = ReadNonNegative(seg2.Text,    wallLabel + " segment 2",            errors);
            wall.Segments.Seg3            = ReadNonNegative(seg3.Text,    wallLabel + " segment 3",            errors);
            wall.Segments.DoorReturnRight = ReadNonNegative(drRight.Text, wallLabel + " door return (right)", errors);
            wall.MeasuredHeight           = ReadPositive(measuredHeight.Text, wallLabel + " measured height", errors);

            if (wall.Segments.TotalWidth <= 0)
                errors.Add(wallLabel + " needs at least one non-zero segment.");

            wall.Cop.Enabled = copBox.IsChecked == true;
            if (wall.Cop.Enabled)
            {
                wall.Cop.Width         = ReadPositive(copWidth.Text,    wallLabel + " COP width",  errors);
                wall.Cop.Height        = ReadPositive(copHeight.Text,   wallLabel + " COP height", errors);
                wall.Cop.GapFromBottom = ReadNonNegative(copGapBottom.Text,  wallLabel + " COP gap from bottom", errors);
                wall.Cop.OffsetFromLeft= ReadNonNegative(copOffsetLeft.Text, wallLabel + " COP offset from left", errors);

                if (wall.Width > 0 && wall.Cop.Width > 0 && wall.Cop.OffsetFromLeft + wall.Cop.Width > wall.Width)
                    errors.Add(wallLabel + " COP offset + width exceeds the wall width.");
            }
            return wall;
        }

        private WallDimensions ReadRearWall(
            CheckBox enabledBox,
            TextBox drLeft, TextBox seg1, TextBox seg2, TextBox seg3, TextBox drRight,
            TextBox measuredHeight,
            List<string> errors)
        {
            WallDimensions wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };
            if (!wall.Enabled) return wall;
            wall.Segments.DoorReturnLeft  = ReadNonNegative(drLeft.Text,  "Rear door return (left)",  errors);
            wall.Segments.Seg1            = ReadNonNegative(seg1.Text,    "Rear segment 1",            errors);
            wall.Segments.Seg2            = ReadNonNegative(seg2.Text,    "Rear segment 2",            errors);
            wall.Segments.Seg3            = ReadNonNegative(seg3.Text,    "Rear segment 3",            errors);
            wall.Segments.DoorReturnRight = ReadNonNegative(drRight.Text, "Rear door return (right)", errors);
            wall.MeasuredHeight           = ReadPositive(measuredHeight.Text, "Rear measured height", errors);
            wall.Cop.Enabled = false;
            if (wall.Segments.TotalWidth <= 0)
                errors.Add("Rear wall needs at least one non-zero segment.");
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
