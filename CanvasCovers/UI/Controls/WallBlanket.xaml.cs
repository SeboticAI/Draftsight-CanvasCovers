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
        private bool _mirrored;   // Right wall: COP + its fields on the right (mirror of Left)

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

        public void Configure(bool isRear, bool mirrored)
        {
            _isRear = isRear;
            _mirrored = mirrored;
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
            // Fixed wall rectangle. SYMMETRIC side margins: the height dim +
            // field sit on the right for the Left wall and on the LEFT for the
            // mirrored Right wall, so both sides need room. The shape NEVER
            // changes with typed values.
            double sideMargin = 100;
            double left = sideMargin, right = cw - sideMargin;
            double top = 56, bottom = ch - 96;
            if (right - left < 120 || bottom - top < 120) return;

            // Landscape, like the test sheet. Fill most of the band; stay wide
            // enough for the 5 segment fields, never wider than the band.
            double availW = right - left, availH = bottom - top;
            double minFieldsW = 5 * (FieldW + 12);    // 5 fields + breathing room
            double targetW = availH * 1.50;           // sheet-like landscape aspect
            double wWall = Math.Max(minFieldsW, targetW);
            wWall = Math.Min(wWall, availW);
            double cx0 = (left + right) / 2;
            left = cx0 - wWall / 2;
            right = cx0 + wWall / 2;
            double w = right - left, h = bottom - top;

            AddRect(left, top, w, h, WallStroke, 2);

            // Segment X boundaries: DR-L | S1 | S2(COP) | S3 | DR-R. The base
            // layout puts the COP (S2) LEFT-of-centre (big S1 gap to its right,
            // small S3 to its left). The RIGHT wall MIRRORS this — the whole
            // layout is reflected so the COP sits right-of-centre — because the
            // two walls face each other on the sheet. Field labels keep their
            // DR-L | S1 | S2 | S3 | DR-R order; only the proportions + COP side
            // flip.
            double[] frac = { 0.0, 0.10, 0.34, 0.48, 0.90, 1.0 };
            if (_mirrored)
            {
                double[] r = new double[6];
                for (int i = 0; i < 6; i++) r[i] = 1.0 - frac[5 - i];
                frac = r;
            }
            double[] bx = new double[6];
            for (int i = 0; i < 6; i++) bx[i] = left + w * frac[i];

            // Dashed vertical dividers at each internal segment boundary.
            for (int i = 1; i < 5; i++)
                AddDashedV(bx[i], top, bottom, GuideStroke);

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

                // COP dim lines + fields go OUTBOARD of the COP: to its left on
                // the Left wall, to its right on the mirrored Right wall.
                double stackX = _mirrored ? copRight + 20 : copLeft - 20;
                AddVDim(stackX, top, copTop);        // top gap (auto)
                AddVDim(stackX, copTop, copBottom);  // COP height
                AddVDim(stackX, copBottom, bottom);  // from bottom

                double fieldX = _mirrored ? stackX + 8 : stackX - FieldW - 8;
                double copHFieldY = (copTop + copBottom) / 2 - FieldH / 2;
                double gapFieldY = (copBottom + bottom) / 2 - FieldH / 2;
                PlaceLabeledField(_copHeight, fieldX, copHFieldY);
                PlaceLabeledField(_copGapBottom, fieldX, gapFieldY);

                AddTopGapReadout(_mirrored ? stackX + 6 : stackX - 6,
                    (top + copTop) / 2, _mirrored);
            }
            else
            {
                HideField(_copHeight);
                HideField(_copGapBottom);
            }

            // Height dimension + field: outer side AWAY from the COP — right
            // edge on the Left wall, left edge on the mirrored Right wall.
            if (_mirrored)
            {
                AddVDim(left - 18, top, bottom);
                PlaceLabeledField(_measuredHeight, left - 30 - FieldW, top + h / 2 - FieldH / 2);
            }
            else
            {
                AddVDim(right + 18, top, bottom);
                PlaceLabeledField(_measuredHeight, right + 30, top + h / 2 - FieldH / 2);
            }

            // Five segment dimension spans + fields below the wall (labels keep
            // DR-L | S1 | S2 | S3 | DR-R order; the spans already mirror via bx).
            // Fields are centred on their span, but a narrow span can be less
            // than FieldW wide, so clamp each field's left edge to not overlap
            // the previous field — guarantees a clean, clickable row at any
            // window width.
            var fields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            double dimY = bottom + 14;
            double fieldY = bottom + 26;
            double prevRight = double.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                AddHDim(bx[i], bx[i + 1], dimY);
                double cx = (bx[i] + bx[i + 1]) / 2;
                double fx = cx - FieldW / 2;
                if (fx < prevRight + 2) fx = prevRight + 2;   // no overlap
                PlaceLabeledFieldBelow(fields[i], fx, fieldY);
                prevRight = fx + FieldW;
            }
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
        // shows a hint until both are present. On a mirrored wall the text
        // reads to the RIGHT of the anchor, else to the left.
        private void AddTopGapReadout(double anchorX, double yCenter, bool mirrored)
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
            if (mirrored) AddLeftText(text, anchorX, yCenter, 10, brush);
            else AddRightText(text, anchorX, yCenter, 10, brush);
        }

        // Left-aligned text starting at x (mirror of AddRightText).
        private void AddLeftText(string text, double x, double cy, double size, Brush brush)
        {
            var tb = new TextBlock { Text = text, FontSize = size, Foreground = brush, IsHitTestVisible = false };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, cy - tb.DesiredSize.Height / 2);
            DrawCanvas.Children.Add(tb); _shapes.Add(tb);
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
            // Keep input fields above every drawn shape so a line/divider can
            // never render over a field and swallow its clicks (a drawn shape
            // added later in the redraw would otherwise sit on top).
            Panel.SetZIndex(tb, 20);
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
