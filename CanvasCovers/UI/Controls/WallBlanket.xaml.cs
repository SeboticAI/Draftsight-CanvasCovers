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
            // Fixed wall rectangle: a landscape box, centred, leaving margins
            // for the left field column, the right height field, the top stack
            // labels and the bottom segment fields.
            double left = 150, right = cw - 110;
            double top = 70, bottom = ch - 110;
            if (right - left < 120 || bottom - top < 120) return;
            double w = right - left, h = bottom - top;

            // Wall outline.
            AddRect(left, top, w, h, WallStroke, 2);

            // Door-return guide verticals (the two outer "place zero" columns).
            double drW = w * 0.10;
            AddLine(left + drW, top, left + drW, bottom, GuideStroke, 1);
            AddLine(right - drW, top, right - drW, bottom, GuideStroke, 1);

            // COP rectangle, fixed in the middle-left third.
            double copW = w * 0.14, copH = h * 0.42;
            double copLeft = left + drW + (w - 2 * drW) * 0.22;
            double copTop = top + h * 0.30;
            double copBottom = copTop + copH;
            AddRect(copLeft, copTop, copW, copH, CopStroke, 1.5);
            AddCenteredText("COP", copLeft + copW / 2, copTop + copH / 2, 12, CopStroke);

            // Vertical COP stack dimension lines to the LEFT of the COP:
            // top gap (wall top -> COP top), COP height (COP top -> COP bottom),
            // from-bottom (COP bottom -> wall bottom). Real dimension symbols.
            double stackX = copLeft - 22;
            AddVDim(stackX, top, copTop);
            AddVDim(stackX, copTop, copBottom);
            AddVDim(stackX, copBottom, bottom);

            // COP fields: a fixed column on the LEFT, each with a label above
            // and a connector line to its stack segment. Echo value on the dim.
            double colX = 14;
            PlaceLabeledField(_copHeight, colX, top + h * 0.20);
            ConnectAndEcho(colX + FieldW, top + h * 0.20 + FieldH / 2, stackX, (copTop + copBottom) / 2, _copHeight.Text);

            PlaceLabeledField(_copGapBottom, colX, top + h * 0.55);
            ConnectAndEcho(colX + FieldW, top + h * 0.55 + FieldH / 2, stackX, (copBottom + bottom) / 2, _copGapBottom.Text);

            // top gap (auto) readout near the top-gap dim.
            AddTopGapReadout(stackX - 4, (top + copTop) / 2);

            // Height dimension on the RIGHT + field further right.
            double htDimX = right + 20;
            AddVDim(htDimX, top, bottom);
            PlaceLabeledField(_measuredHeight, right + 34, top + h / 2 - FieldH / 2);
            EchoOnVDim(htDimX, (top + bottom) / 2, _measuredHeight.Text);

            // Five segment slots along the bottom: fixed horizontal dim spans +
            // fields below, each wired to its span and echoing its value.
            // Spans: DR-L (drW), S1, COP W (under the COP), S3, DR-R (drW).
            double inner = w - 2 * drW;
            double[] frac = { 0.0, 0.18, 0.42, 0.82, 1.0 };  // S1|S2|S3 boundaries within inner
            double x0 = left, x1 = left + drW;
            double x2 = left + drW + inner * frac[1];
            double x3 = left + drW + inner * frac[2];
            double x4 = left + drW + inner * frac[3];
            double x5 = right;
            double[] bx0 = { x0, x1, x2, x3, x4 };
            double[] bx1 = { x1, x2, x3, x4, x5 };
            var fields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            string[] texts = { _drLeft.Text, _seg1.Text, _seg2.Text, _seg3.Text, _drRight.Text };
            double dimY = bottom + 16;
            double fieldY = bottom + 30;
            for (int i = 0; i < 5; i++)
            {
                AddHDim(bx0[i], bx1[i], dimY);
                double cx = (bx0[i] + bx1[i]) / 2;
                if (!string.IsNullOrEmpty(texts[i]))
                    AddCenteredText(texts[i], cx, dimY - 8, 10, DimStroke);
                PlaceLabeledFieldBelow(fields[i], cx - FieldW / 2, fieldY);
            }
        }

        // ---- Rear: a plain rectangle with Width + Height ----
        private void DrawRearSheet(double cw, double ch)
        {
            // Hide the L/R-only fields.
            HideField(_drLeft); HideField(_seg2); HideField(_seg3); HideField(_drRight);
            HideField(_copHeight); HideField(_copGapBottom);

            double left = 150, right = cw - 110;
            double top = 70, bottom = ch - 90;
            if (right - left < 120 || bottom - top < 120) return;
            double w = right - left, h = bottom - top;

            AddRect(left, top, w, h, WallStroke, 2);
            AddCenteredText("REAR WALL", (left + right) / 2, top + h / 2, 12, LabelText);

            // Width dim along the bottom + field below.
            double dimY = bottom + 16;
            AddHDim(left, right, dimY);
            double cx = (left + right) / 2;
            if (!string.IsNullOrEmpty(_seg1.Text))
                AddCenteredText(_seg1.Text, cx, dimY - 8, 10, DimStroke);
            PlaceLabeledFieldBelow(_seg1, cx - FieldW / 2, bottom + 30);

            // Height dim on the right + field.
            double htDimX = right + 20;
            AddVDim(htDimX, top, bottom);
            PlaceLabeledField(_measuredHeight, right + 34, top + h / 2 - FieldH / 2);
            EchoOnVDim(htDimX, (top + bottom) / 2, _measuredHeight.Text);
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

        // A dashed connector from a field edge to a dim line, plus the echoed
        // value drawn on the dim (centred at the dim midpoint).
        private void ConnectAndEcho(double fx, double fy, double dx, double dy, string value)
        {
            var dash = new Line
            {
                X1 = fx, Y1 = fy, X2 = dx, Y2 = dy,
                Stroke = GuideStroke, StrokeThickness = 0.7,
                StrokeDashArray = new DoubleCollection { 2, 2 },
            };
            DrawCanvas.Children.Add(dash); _shapes.Add(dash);
            if (!string.IsNullOrEmpty(value))
                AddRightText(value, dx - 4, dy, 10, DimStroke);
        }

        private void EchoOnVDim(double x, double yCenter, string value)
        {
            if (!string.IsNullOrEmpty(value))
                AddCenteredText(value, x + 12, yCenter, 10, DimStroke);
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
