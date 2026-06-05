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

        // False until the constructor finishes. The Options TextBoxes carry
        // TextChanged="SharedParam_Changed", which WPF fires WHILE parsing the
        // XAML (as each Text="..." is applied) — before the later-declared
        // named elements PushSharedParams reads (EdgeAllowanceInput,
        // QuiltingSpacingInput, QuiltingOption) have been constructed. Guarding
        // on this flag (set last, below) is order-independent: a XAML reorder
        // cannot silently reintroduce the init-time NRE.
        private bool _initialized;

        public LiftBlanketWindow()
        {
            InitializeComponent();
            Header.Subtitle = "Lift Blanket Generator";
            Loaded += LiftBlanketWindow_Loaded;
            _initialized = true;
        }

        private void LiftBlanketWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fields start EMPTY (greyed in-box placeholders prompt for input);
            // no values are pre-seeded. Configure sets each tab's mode: Left/
            // Right get the 5-segment + COP layout, Rear gets width + height.
            LeftBlanket.Configure(isRear: false);
            RightBlanket.Configure(isRear: false);
            RearBlanket.Configure(isRear: true);
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
            // Ignore the TextChanged/Checked events WPF raises while still
            // parsing the XAML — see _initialized. The old guard checked
            // LeftBlanket (declared BEFORE the Options inputs in XAML, so
            // always non-null by the time they fire) and so never caught this.
            if (!_initialized) return;
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
                // Re-enable the tab but deliberately do NOT re-tick the wall's
                // "Include" box — the operator may have unticked it for an
                // unrelated reason, so preserve their intent.
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

            // ParseOr (not raw TryParse) so a non-numeric entry falls back to
            // 50 — matching the preview + COP validation, which also use
            // ParseOr(..., 50). Raw TryParse would zero it on bad input and
            // make the generated job disagree with what the operator saw.
            double allowance = ParseOr(FixingAllowanceInput.Text, 50);

            return new LiftBlanketOptions
            {
                ThroughCar = ThroughCarOption.IsChecked == true,
                PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
                Fixings = fixing,
                FixingAllowanceMm = allowance,
                // The edge-allowance, quilting-spacing and quilting-on inputs
                // drive both the live preview (via SetSharedParams) AND the
                // generated drawing — read them here so operator changes reach
                // the generator, not just the preview.
                EdgeAllowanceMm = ParseOr(EdgeAllowanceInput.Text, 10),
                VerticalQuiltingSpacingMm = ParseOr(QuiltingSpacingInput.Text, 700),
                QuiltingEnabled = QuiltingOption.IsChecked == true,
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
            if (_initialized) PushSharedParams();
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
