using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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
        // named elements PushSharedParams reads (QuiltInsetInput,
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
            LeftBlanket.Configure(isRear: false, mirrored: false);
            RightBlanket.Configure(isRear: false, mirrored: true);
            RearBlanket.Configure(isRear: true, mirrored: false);

            // Seed the right + rear height from the left wall (placeholder only;
            // a wall stops mirroring once the operator types its own height).
            // Item 4.
            LeftBlanket.HeightChanged += (s, ev) =>
            {
                RightBlanket.MirrorHeight(LeftBlanket.HeightText);
                RearBlanket.MirrorHeight(LeftBlanket.HeightText);
            };

            // Live left/right width-match check (item 16): re-evaluate whenever
            // either side changes. Non-blocking — just a visible reminder.
            LeftBlanket.InputChanged += (s, ev) => UpdateWidthWarning();
            RightBlanket.InputChanged += (s, ev) => UpdateWidthWarning();

            PushSharedParams();
            UpdateWidthWarning();

            // Customer drop-down (round 2, item 6). Best-effort: any IO
            // problem just leaves the combo empty-but-typable.
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string seedPath = string.IsNullOrEmpty(assemblyDir)
                    ? null
                    : Path.Combine(assemblyDir, "Resources", "customers.csv");
                MetadataPanel.SetCustomers(
                    CustomerDirectory.LoadOrSeed(CustomerDirectory.DefaultUserPath, seedPath));
            }
            catch { /* drop-down is optional — never block the dialog */ }
        }

        // Pushes the Options-panel values (allowances, quilting) into every
        // blanket so each live preview computes the same fold + COP geometry.
        private void PushSharedParams()
        {
            double fixing = ParseOr(FixingAllowanceInput.Text, 50);
            double inset = ParseOr(QuiltInsetInput.Text, 5);
            double quilt = ParseOr(QuiltingSpacingInput.Text, 700);
            bool quiltOn = QuiltingOption.IsChecked == true;
            LeftBlanket.SetSharedParams(fixing, inset, quilt, quiltOn);
            RightBlanket.SetSharedParams(fixing, inset, quilt, quiltOn);
            RearBlanket.SetSharedParams(fixing, inset, quilt, quiltOn);
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
            // Reject non-finite parses ("NaN"/"Infinity" parse as true) so a
            // typo in Edge/Quilting/Fixing can't push a non-finite value into
            // the previews — consistent with WallBlanket.ParseOr.
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                       CultureInfo.InvariantCulture, out double v)
                   && !double.IsNaN(v) && !double.IsInfinity(v)
                ? v : fallback;
        }

        public LiftBlanketJob Job { get; private set; }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            List<string> errors = new List<string>();

            ProjectMetadata project = MetadataPanel.Read();
            LiftBlanketOptions options = ReadOptions(errors);

            WallDimensions left = ReadBlanket(LeftBlanket, "Left wall", options, errors);
            WallDimensions right = ReadBlanket(RightBlanket, "Right wall", options, errors);
            WallDimensions rear = ReadBlanket(RearBlanket, "Rear wall", options, errors);

            // At least one wall must be generated, else Generate would draw an
            // empty document with no feedback.
            bool anyWall = left.Enabled || right.Enabled
                || (rear.Enabled && !options.ThroughCar);
            if (!anyWall)
                errors.Add("Enable at least one wall (tick \"Include this wall\").");

            // Guard quilting spacing so a tiny value can't emit thousands of
            // lines and freeze the host.
            if (options.QuiltingEnabled && options.VerticalQuiltingSpacingMm < 50)
                errors.Add("Quilting spacing must be at least 50mm (a smaller value would draw an excessive number of lines).");

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

        private LiftBlanketOptions ReadOptions(List<string> errors)
        {
            // No default fixing (round 2, item 3): the operator must choose,
            // because TG7 vs TG9 prints on the COP and a silently-defaulted
            // value already cost two hand-edited drawings.
            FixingType fixing = FixingType.None;
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            string tag = selected?.Tag as string;
            if (string.IsNullOrEmpty(tag) || !Enum.TryParse(tag, out fixing) || fixing == FixingType.None)
                errors.Add("Select the fixings required (there is no default).");

            double allowance = ReadNonNegative(
                FixingAllowanceInput.Text, "Fixing allowance", errors);
            double quiltInset = ReadNonNegative(
                QuiltInsetInput.Text, "Quilt inset", errors);
            double quiltSpacing = ReadPositive(
                QuiltingSpacingInput.Text, "Quilting spacing", errors);

            return new LiftBlanketOptions
            {
                ThroughCar = ThroughCarOption.IsChecked == true,
                PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
                BagRequired = BagRequiredOption.IsChecked == true,
                GlassBehind = GlassBehindOption.IsChecked == true,
                Fixings = fixing,
                FixingAllowanceMm = allowance,
                // The quilt-inset, quilting-spacing and quilting-on inputs
                // drive both the live preview (via SetSharedParams) AND the
                // generated drawing — read them here so operator changes reach
                // the generator, not just the preview.
                QuiltInsetMm = quiltInset,
                VerticalQuiltingSpacingMm = quiltSpacing,
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
            CanvasCovers.UI.Controls.WallBlanket blanket, string wallLabel,
            LiftBlanketOptions options, List<string> errors)
        {
            var wall = new WallDimensions { Enabled = blanket.WallEnabled };
            if (!wall.Enabled) return wall;

            // The rear wall has a single "Width" field (stored as Seg1); the
            // L/R walls have the five segment boxes. Use the right noun.
            bool isRear = blanket == RearBlanket;
            string widthLabel = isRear ? wallLabel + " width" : wallLabel + " segment 1";

            // Optional total-width override (L/R only). When set, the operator
            // may leave the segment boxes blank — they're treated as 0 instead
            // of erroring, so a single total width is enough. Item 5.
            double overrideWidth = isRear
                ? 0
                : ReadOptionalNonNegative(blanket.TotalWidthText, wallLabel + " total width", errors);
            wall.TotalWidthOverride = overrideWidth;
            bool blankOk = overrideWidth > 0;

            wall.Segments.DoorReturnLeft  = ReadSegment(blanket.DrLeftText,  wallLabel + " door return (left)",  blankOk, errors);
            wall.Segments.Seg1            = ReadSegment(blanket.Seg1Text,    widthLabel,                          blankOk, errors);
            wall.Segments.Seg2            = ReadSegment(blanket.Seg2Text,    wallLabel + " segment 2 (COP width)", blankOk, errors);
            wall.Segments.Seg3            = ReadSegment(blanket.Seg3Text,    wallLabel + " segment 3",            blankOk, errors);
            wall.Segments.DoorReturnRight = ReadSegment(blanket.DrRightText, wallLabel + " door return (right)", blankOk, errors);
            wall.MeasuredHeight           = ReadPositive(blanket.MeasuredHeightText, wallLabel + " measured height", errors);

            if (wall.Width <= 0)
                errors.Add(isRear
                    ? wallLabel + " needs a width."
                    : wallLabel + " needs a total width or at least one non-zero segment.");

            // The measured height must exceed the fixing allowance, else the
            // doubled cut height ((measured − fixing) × 2) goes zero/negative
            // and the cut rectangle inverts.
            if (wall.MeasuredHeight > 0 && wall.MeasuredHeight <= options.FixingAllowanceMm)
                errors.Add(wallLabel + " measured height (" +
                    wall.MeasuredHeight.ToString(CultureInfo.InvariantCulture) +
                    ") must be greater than the fixing allowance (" +
                    options.FixingAllowanceMm.ToString(CultureInfo.InvariantCulture) + ").");

            wall.Cop.Enabled = blanket.CopEnabled;
            if (wall.Cop.Enabled)
            {
                wall.Cop.Height        = ReadPositive(blanket.CopHeightText,    wallLabel + " COP height", errors);
                wall.Cop.GapFromBottom = ReadNonNegative(blanket.CopGapBottomText, wallLabel + " COP gap from bottom", errors);

                if (wall.Segments.Seg2 <= 0)
                    errors.Add(wallLabel + " COP width (segment 2) must be greater than zero when COP is enabled.");

                // COP must fit horizontally within the cut piece: its left edge
                // (half-allowance + DR-L + S1) plus its width (S2) must not pass
                // the right cut edge.
                if (wall.Segments.Seg2 > 0)
                {
                    double copRight = wall.Segments.DoorReturnLeft + wall.Segments.Seg1 + wall.Segments.Seg2;
                    double cutWidth = wall.Width;
                    if (copRight > cutWidth + 0.001)
                        errors.Add(wallLabel +
                            " COP extends past the right edge — reduce segment 1 or the COP width (segment 2).");
                }

                // Vertical: the COP must not cross the fold line.
                if (wall.MeasuredHeight > 0 && wall.Cop.Height > 0 &&
                    Geometry.Products.LiftBlanket.LiftBlanketCalculator.AutoTopGap(
                        wall.MeasuredHeight, options.FixingAllowanceMm, wall.Cop.GapFromBottom, wall.Cop.Height) < 0)
                {
                    errors.Add(wallLabel +
                        " COP gap-from-bottom + height crosses the fold line (must fit within the measured half = measured height − fixing allowance).");
                }
            }
            return wall;
        }

        private static double ReadPositive(string text, string label, List<string> errors)
        {
            // double.TryParse accepts "NaN"/"Infinity" (and NaN<=0 is false), so
            // guard them explicitly — otherwise a non-finite value would slip
            // past into the generated geometry.
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                errors.Add(label + " must be a number greater than zero.");
                return 0;
            }
            return value;
        }

        // A segment value. When blankAllowed (the operator gave a total-width
        // override) an empty box means 0 rather than an error.
        private static double ReadSegment(string text, string label, bool blankAllowed, List<string> errors)
        {
            if (blankAllowed && string.IsNullOrWhiteSpace(text)) return 0;
            return ReadNonNegative(text, label, errors);
        }

        // An optional non-negative value: blank means "not set" (0), not an error.
        private static double ReadOptionalNonNegative(string text, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                errors.Add(label + " must be zero or a positive number.");
                return 0;
            }
            return value;
        }

        private static double ReadNonNegative(string text, string label, List<string> errors)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value) || value < 0)
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

        // Live, non-blocking reminder when the left and right wall widths differ.
        // Cars are square, so a mismatch is usually leftover data — but an
        // angled-COP lift legitimately differs, so it never blocks. Item 16.
        private void UpdateWidthWarning()
        {
            if (WidthWarningText == null || LeftBlanket == null || RightBlanket == null) return;

            bool show = CanvasCovers.Models.Products.LiftBlanket.WallChecks.WidthsMismatch(
                LeftBlanket.WallEnabled, LeftBlanket.CurrentTotalWidth,
                RightBlanket.WallEnabled, RightBlanket.CurrentTotalWidth);

            if (show)
            {
                WidthWarningText.Text =
                    "⚠ Left and right wall widths differ ("
                    + LeftBlanket.CurrentTotalWidth.ToString("0", CultureInfo.InvariantCulture)
                    + " vs " + RightBlanket.CurrentTotalWidth.ToString("0", CultureInfo.InvariantCulture)
                    + "). Cars are usually square — check this isn't leftover data (OK if this lift has an angled COP).";
                WidthWarningText.Visibility = Visibility.Visible;
            }
            else
            {
                WidthWarningText.Visibility = Visibility.Collapsed;
            }
        }
    }
}
