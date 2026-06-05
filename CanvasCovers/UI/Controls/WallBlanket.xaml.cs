using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CanvasCovers.Geometry.Products.LiftBlanket;

namespace CanvasCovers.UI.Controls
{
    // Per-wall input surface drawn as a FIXED schematic copy of the paper
    // measurement sheet. The blanket rectangle and every internal feature
    // (COP, segment slots, dimension lines) are drawn at FIXED positions
    // derived only from the canvas size — typed values NEVER move or rescale
    // anything. This is deliberately not a live geometric preview: an earlier
    // true-proportion version collapsed to an unusable sliver when a single
    // digit was typed mid-edit. The schematic just shows the layout; the
    // operator's numbers fill the labelled fields, which are wired to the
    // matching dimension line (and echo their value on it).
    //
    // Two modes (Configure): Left/Right show the five-segment + COP sheet;
    // Rear shows a single Width + Height. The generated DXF still doubles the
    // height ((measured - fixing) x 2) + fixing clearance — that geometry is
    // in the calculator/generator and is unaffected by anything here.
    //
    // Redraw discipline: the input TextBoxes and their external labels are
    // PERSISTENT canvas children (added once, repositioned/toggled each pass)
    // so editing keeps focus. Only the drawn schematic shapes (rect, dim
    // lines, value echoes) are torn down and rebuilt — tracked in _shapes.
    public partial class WallBlanket : UserControl
    {
        private readonly TextBox _drLeft, _seg1, _seg2, _seg3, _drRight;
        private readonly TextBox _measuredHeight, _copHeight, _copGapBottom;

        // External label per field (sits beside/above the box — not inside).
        private readonly Dictionary<TextBox, TextBlock> _labels =
            new Dictionary<TextBox, TextBlock>();

        // Transient drawn schematic shapes only. The persistent TextBoxes and
        // their labels are excluded so they survive a redraw (and keep focus).
        private readonly List<UIElement> _shapes = new List<UIElement>();

        private const double FieldW = 64;
        private const double FieldH = 24;

        private double _fixingAllowance = 50;
        private bool _isRear;

        private static readonly Brush WallStroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        private static readonly Brush CopStroke = new SolidColorBrush(Color.FromRgb(0x93, 0x33, 0xEA));
        private static readonly Brush DimStroke = new SolidColorBrush(Color.FromRgb(0xC7, 0x7B, 0x30));
        private static readonly Brush GuideStroke = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB));
        private static readonly Brush LabelText = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

        public WallBlanket()
        {
            InitializeComponent();
            _drLeft = MakeField("DR-L");
            _seg1 = MakeField("S1");
            _seg2 = MakeField("COP W (S2)");
            _seg3 = MakeField("S3");
            _drRight = MakeField("DR-R");
            _measuredHeight = MakeField("Height");
            _copHeight = MakeField("COP H");
            _copGapBottom = MakeField("From bottom");
        }

        private TextBox MakeField(string label)
        {
            var tb = new TextBox
            {
                Text = string.Empty,
                Width = FieldW,
                Height = FieldH,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
            tb.TextChanged += Input_Changed;
            _labels[tb] = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = LabelText,
                IsHitTestVisible = false,
            };
            return tb;
        }

        public void Configure(bool isRear)
        {
            _isRear = isRear;
            IncludeCop.Visibility = isRear ? Visibility.Collapsed : Visibility.Visible;
            _labels[_seg1].Text = isRear ? "Width" : "S1";
            Redraw();
        }

        // The window pushes the fixing allowance (used only for the auto
        // top-gap readout). Edge allowance / quilting don't affect this fixed
        // schematic, so they're accepted and ignored here.
        public void SetSharedParams(double fixingAllowance, double edgeAllowance,
            double quiltSpacing, bool quiltEnabled)
        {
            _fixingAllowance = fixingAllowance;
            Redraw();
        }

        public bool WallEnabled => IncludeWall.IsChecked == true;
        public bool CopEnabled => !_isRear && IncludeCop.IsChecked == true;
        public void SetWallEnabled(bool v) => IncludeWall.IsChecked = v;
        public void SetWallEnabledInteractive(bool enabled) => IncludeWall.IsEnabled = enabled;

        public string DrLeftText => _isRear ? "0" : _drLeft.Text;
        public string Seg1Text => _seg1.Text;
        public string Seg2Text => _isRear ? "0" : _seg2.Text;
        public string Seg3Text => _isRear ? "0" : _seg3.Text;
        public string DrRightText => _isRear ? "0" : _drRight.Text;
        public string MeasuredHeightText => _measuredHeight.Text;
        public string CopHeightText => _copHeight.Text;
        public string CopGapBottomText => _copGapBottom.Text;

        private void Input_Changed(object sender, RoutedEventArgs e) => Redraw();
        private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        private void Redraw()
        {
            // Backstop: the schematic uses only fixed canvas-derived geometry
            // (no typed values feed sizes), so a throw is very unlikely, but a
            // bad frame must never crash the host UI thread.
            try { RedrawCore(); }
            catch { /* skip this frame */ }
        }

        private void RedrawCore()
        {
            if (DrawCanvas == null || _drLeft == null) return;

            foreach (var s in _shapes) DrawCanvas.Children.Remove(s);
            _shapes.Clear();

            double cw = DrawCanvas.ActualWidth;
            double ch = DrawCanvas.ActualHeight;
            if (cw < 360 || ch < 300) return;   // need room for the fixed sheet

            if (_isRear) DrawRearSheet(cw, ch);
            else DrawWallSheet(cw, ch);
        }

        // ---- Left / Right: the full measurement-sheet schematic ----
        private void DrawWallSheet(double cw, double ch)
        {
            // Fixed wall rectangle, centred in a band that leaves room for the
            // right-side height dim + field and the bottom segment fields. The
            // COP fields now sit INSIDE the wall band (beside the COP dim
            // lines), so the left margin no longer reserves a field column.
            // The shape NEVER changes with typed values.
            double left = 90, right = cw - 110;
            double top = 56, bottom = ch - 96;
            if (right - left < 120 || bottom - top < 120) return;

            // Landscape, like the test sheet (wider than tall, but not a thin
            // strip). Target width ≈ 1.4 × height for the sheet look, but never
            // narrower than the 5 segment fields need to read clearly, and
            // never wider than the available band. Re-centre within the band.
            double availW = right - left, availH = bottom - top;
            double minFieldsW = 5 * (FieldW + 12);    // 5 fields + breathing room
            double targetW = availH * 1.40;           // sheet-like landscape aspect
            double wWall = Math.Max(minFieldsW, targetW);
            wWall = Math.Min(wWall, availW);
            double cx0 = (left + right) / 2;
            left = cx0 - wWall / 2;
            right = cx0 + wWall / 2;
            double w = right - left, h = bottom - top;

            AddRect(left, top, w, h, WallStroke, 2);

            // Segment X boundaries: DR-L | S1 | S2(COP) | S3 | DR-R. Fixed
            // fractions of the wall width. The COP column (x2..x3) is where the
            // purple COP sits, so the COP lines up directly over its S2 field.
            double[] frac = { 0.0, 0.12, 0.40, 0.60, 0.88, 1.0 };
            double[] bx = new double[6];
            for (int i = 0; i < 6; i++) bx[i] = left + w * frac[i];

            // Dashed vertical dividers at each internal segment boundary.
            for (int i = 1; i < 5; i++)
                AddDashedV(bx[i], top, bottom, GuideStroke);

            // Illustrative quilt guides (not to the real spacing — the DXF
            // uses the even-division math). Mirrors the real rule's shape:
            // horizontal lines span the FULL width (minus a small inset),
            // vertical lines sit on the DR-L/DR-R boundaries (when non-zero)
            // plus a couple of even-fill lines between them. Confined to the
            // lower portion to suggest the bottom (measured) half.
            DrawQuiltGuides(bx, left, right, top, bottom);

            // COP block — only when "Include COP cutout" is ticked.
            if (CopEnabled)
            {
                double copInset = (bx[3] - bx[2]) * 0.10;
                double copLeft = bx[2] + copInset;
                double copRight = bx[3] - copInset;
                double copTop = top + h * 0.28;
                double copBottom = top + h * 0.72;
                AddRect(copLeft, copTop, copRight - copLeft, copBottom - copTop, CopStroke, 1.5);
                AddCenteredText("COP", (copLeft + copRight) / 2, (copTop + copBottom) / 2, 12, CopStroke);

                // Vertical COP-stack dimension lines just left of the COP.
                double stackX = copLeft - 20;
                AddVDim(stackX, top, copTop);        // top gap (auto)
                AddVDim(stackX, copTop, copBottom);  // COP height
                AddVDim(stackX, copBottom, bottom);  // from bottom

                // COP fields sit RIGHT NEXT TO their dimension line (no leader
                // lines): each field just left of stackX, vertically centred on
                // the dim segment it drives, with its label directly above.
                double fieldX = stackX - FieldW - 8;
                double copHFieldY = (copTop + copBottom) / 2 - FieldH / 2;
                double gapFieldY = (copBottom + bottom) / 2 - FieldH / 2;
                PlaceLabeledField(_copHeight, fieldX, copHFieldY);
                PlaceLabeledField(_copGapBottom, fieldX, gapFieldY);

                // Auto top-gap readout centred on the top-gap dim segment.
                AddTopGapReadout(stackX - 6, (top + copTop) / 2);
            }
            else
            {
                HideField(_copHeight);
                HideField(_copGapBottom);
            }

            // Height dimension on the RIGHT + field further right.
            double htDimX = right + 18;
            AddVDim(htDimX, top, bottom);
            PlaceLabeledField(_measuredHeight, right + 30, top + h / 2 - FieldH / 2);

            // Five segment dimension spans + fields below the wall (no value
            // echoed on the dim line — the value lives only in the field).
            var fields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            double dimY = bottom + 14;
            double fieldY = bottom + 26;
            for (int i = 0; i < 5; i++)
            {
                AddHDim(bx[i], bx[i + 1], dimY);
                double cx = (bx[i] + bx[i + 1]) / 2;
                PlaceLabeledFieldBelow(fields[i], cx - FieldW / 2, fieldY);
            }
        }

        // Illustrative quilt guides for the preview. Outer lines sit at the
        // DR-L boundary (bx[1]) and DR-R boundary (bx[4]) — skipped if that DR
        // segment reads 0. Internal lines evenly fill between whatever outer
        // bounds exist. Drawn faint; purely a visual cue, not the real spacing.
        private void DrawQuiltGuides(double[] bx, double left, double right, double top, double bottom)
        {
            Brush quilt = new SolidColorBrush(Color.FromRgb(0xD8, 0xB0, 0xE8));

            // The quilt region is the lower portion of the schematic (the
            // bottom/measured half) — from the fold-ish line down to the
            // bottom, inset slightly from the edges.
            double qTop = top + (bottom - top) * 0.42;   // ~ the fold area
            double qBottom = bottom - 4;
            double inset = 4;
            double qLeft = left + inset;
            double qRight = right - inset;

            // Horizontal lines span the FULL width minus the inset (matching the
            // DXF, where horizontals are edge-to-edge, not bounded by the DRs).
            const int hLines = 3;
            for (int i = 1; i <= hLines; i++)
            {
                double y = qTop + (qBottom - qTop) * i / (hLines + 1);
                AddLine(qLeft, y, qRight, y, quilt, 0.9);
            }

            // Vertical lines on the DR-L (bx[1]) and DR-R (bx[4]) boundaries,
            // each only when that door return is non-zero, plus a couple of
            // even-fill lines between the boundaries.
            double vLeft = IsZeroOrBlank(_drLeft.Text) ? qLeft : bx[1];
            double vRight = IsZeroOrBlank(_drRight.Text) ? qRight : bx[4];
            if (!IsZeroOrBlank(_drLeft.Text))
                AddLine(bx[1], qTop, bx[1], qBottom, quilt, 0.9);
            if (!IsZeroOrBlank(_drRight.Text))
                AddLine(bx[4], qTop, bx[4], qBottom, quilt, 0.9);

            const int vFill = 2;
            for (int i = 1; i <= vFill; i++)
            {
                double x = vLeft + (vRight - vLeft) * i / (vFill + 1);
                AddLine(x, qTop, x, qBottom, quilt, 0.9);
            }
        }

        // True when the field is blank or parses to zero — used to decide
        // whether a door-return quilt line is drawn.
        private static bool IsZeroOrBlank(string s)
        {
            return string.IsNullOrWhiteSpace(s) || ParseOr(s) == 0;
        }

        private void AddDashedV(double x, double y0, double y1, Brush stroke)
        {
            var line = new Line
            {
                X1 = x, Y1 = y0, X2 = x, Y2 = y1,
                Stroke = stroke, StrokeThickness = 0.8,
                StrokeDashArray = new DoubleCollection { 4, 3 },
            };
            DrawCanvas.Children.Add(line); _shapes.Add(line);
        }

        // ---- Rear: a plain rectangle with Width + Height ----
        private void DrawRearSheet(double cw, double ch)
        {
            // Hide the L/R-only fields.
            HideField(_drLeft); HideField(_seg2); HideField(_seg3); HideField(_drRight);
            HideField(_copHeight); HideField(_copGapBottom);

            double left = 90, right = cw - 110;
            double top = 56, bottom = ch - 96;
            if (right - left < 120 || bottom - top < 120) return;

            // Portrait-ish, matching the L/R walls.
            double availH = bottom - top;
            double maxW = availH * 0.80;
            double wWall = Math.Min(right - left, maxW);
            double cx0 = (left + right) / 2;
            left = cx0 - wWall / 2;
            right = cx0 + wWall / 2;
            double w = right - left, h = bottom - top;

            AddRect(left, top, w, h, WallStroke, 2);
            AddCenteredText("REAR WALL", (left + right) / 2, top + h / 2, 12, LabelText);

            // Width dim along the bottom + field below (no value on the dim).
            double dimY = bottom + 14;
            AddHDim(left, right, dimY);
            PlaceLabeledFieldBelow(_seg1, (left + right) / 2 - FieldW / 2, bottom + 26);

            // Height dim on the right + field (no value on the dim).
            double htDimX = right + 18;
            AddVDim(htDimX, top, bottom);
            PlaceLabeledField(_measuredHeight, right + 30, top + h / 2 - FieldH / 2);
        }

        // The auto top-gap readout (computed from measured height + COP inputs);
        // shows a hint until both are present.
        private void AddTopGapReadout(double xRight, double yCenter)
        {
            string text;
            Brush brush = LabelText;
            double measured = ParseOr(_measuredHeight.Text);
            double copH = ParseOr(_copHeight.Text);
            double gap = ParseOr(_copGapBottom.Text);
            if (measured > 0 && copH > 0)
            {
                double topGap = LiftBlanketCalculator.AutoTopGap(measured, _fixingAllowance, gap, copH);
                text = topGap.ToString("0", CultureInfo.InvariantCulture);
                if (topGap < 0) { text += " ⚠"; brush = Brushes.Red; }
                else brush = DimStroke;
            }
            else
            {
                text = "top gap";
            }
            AddRightText(text, xRight, yCenter, 10, brush);
        }

        // ---- dimension-symbol primitives (real dim lines with end arrows) ----

        private void AddHDim(double x0, double x1, double y)
        {
            if (x1 - x0 < 6) return;
            AddLine(x0, y, x1, y, DimStroke, 1);
            AddArrow(x0, y, +1, true);   // left end, pointing right
            AddArrow(x1, y, -1, true);   // right end, pointing left
        }

        private void AddVDim(double x, double y0, double y1)
        {
            if (y1 - y0 < 6) return;
            AddLine(x, y0, x, y1, DimStroke, 1);
            AddArrow(x, y0, +1, false);  // top end, pointing down
            AddArrow(x, y1, -1, false);  // bottom end, pointing up
        }

        // A small filled arrowhead at (x,y). horizontal: arrow along X;
        // dir = +1 points toward increasing axis, -1 toward decreasing.
        private void AddArrow(double x, double y, double dir, bool horizontal)
        {
            const double len = 6, wid = 3;
            Point tip, b1, b2;
            if (horizontal)
            {
                tip = new Point(x, y);
                b1 = new Point(x + dir * len, y - wid);
                b2 = new Point(x + dir * len, y + wid);
            }
            else
            {
                tip = new Point(x, y);
                b1 = new Point(x - wid, y + dir * len);
                b2 = new Point(x + wid, y + dir * len);
            }
            var poly = new Polygon
            {
                Points = new PointCollection { tip, b1, b2 },
                Fill = DimStroke,
            };
            DrawCanvas.Children.Add(poly);
            _shapes.Add(poly);
        }

        // ---- field + label placement (label OUTSIDE the box) ----

        // Label to the LEFT-above; field below the label. Used for the left
        // column (COP fields) and the right height field.
        private void PlaceLabeledField(TextBox tb, double x, double y)
        {
            ShowField(tb, x, y);
            TextBlock lbl = _labels[tb];
            EnsureChild(lbl);
            lbl.Visibility = Visibility.Visible;
            Canvas.SetLeft(lbl, x);
            Canvas.SetTop(lbl, y - 14);
        }

        // Label BELOW the field. Used for the bottom segment row.
        private void PlaceLabeledFieldBelow(TextBox tb, double x, double y)
        {
            ShowField(tb, x, y);
            TextBlock lbl = _labels[tb];
            EnsureChild(lbl);
            lbl.Visibility = Visibility.Visible;
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lbl, x + FieldW / 2 - lbl.DesiredSize.Width / 2);
            Canvas.SetTop(lbl, y + FieldH + 2);
        }

        private void ShowField(TextBox tb, double x, double y)
        {
            tb.Visibility = Visibility.Visible;
            EnsureChild(tb);
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
        }

        private void HideField(TextBox tb)
        {
            tb.Visibility = Visibility.Collapsed;
            if (_labels.TryGetValue(tb, out TextBlock lbl)) lbl.Visibility = Visibility.Collapsed;
        }

        private void EnsureChild(UIElement el)
        {
            if (!DrawCanvas.Children.Contains(el)) DrawCanvas.Children.Add(el);
        }

        // ---- drawing primitives (transient shapes go in _shapes) ----

        private void AddRect(double x, double y, double w, double h, Brush stroke, double thick)
        {
            if (!(w > 0) || !(h > 0)) return;
            var r = new Rectangle { Width = w, Height = h, Stroke = stroke, StrokeThickness = thick, Fill = Brushes.Transparent };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            DrawCanvas.Children.Add(r); _shapes.Add(r);
        }

        private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double thick)
        {
            var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thick };
            DrawCanvas.Children.Add(line); _shapes.Add(line);
        }

        private void AddCenteredText(string text, double cx, double cy, double size, Brush brush)
        {
            var tb = new TextBlock { Text = text, FontSize = size, Foreground = brush, IsHitTestVisible = false };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, cy - tb.DesiredSize.Height / 2);
            DrawCanvas.Children.Add(tb); _shapes.Add(tb);
        }

        private void AddRightText(string text, double xRight, double cy, double size, Brush brush)
        {
            var tb = new TextBlock { Text = text, FontSize = size, Foreground = brush, IsHitTestVisible = false };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, xRight - tb.DesiredSize.Width);
            Canvas.SetTop(tb, cy - tb.DesiredSize.Height / 2);
            DrawCanvas.Children.Add(tb); _shapes.Add(tb);
        }

        private static double ParseOr(string s)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                   && !double.IsNaN(v) && !double.IsInfinity(v)
                ? v : 0;
        }
    }
}
