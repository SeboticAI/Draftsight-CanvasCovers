using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.UI.Controls
{
    // Interactive per-wall input surface: a blanket drawn with embedded
    // measurement fields. Redraws live on edit. The hosting window reads
    // values via the public accessors and the embedded TextBoxes.
    //
    // Two modes (set via Configure):
    //   - Left/Right: five bottom-row segment fields (DR-L / S1 / S2 / S3 /
    //     DR-R) plus measured height plus an optional COP (height +
    //     from-bottom). The five segments are placed in five EQUAL columns by
    //     slot index — never by their mm value — so empty/zero fields can't
    //     stack on top of one another.
    //   - Rear: a single Width field + Height field, no segments, no COP.
    //     The width is stored internally into one segment so the calculator
    //     and generator need no rear-specific branch.
    //
    // Empty-state: all fields start blank with a greyed in-box placeholder.
    // The preview still draws a roughly-square default schematic (with the
    // segment slots + COP slot as a guide) so the operator sees the layout
    // before typing. Once real values are entered the blanket morphs to the
    // true height:width proportion (clamped).
    //
    // Redraw discipline: the embedded TextBoxes (and their placeholder labels
    // + the auto top-gap label) are PERSISTENT children of the canvas — added
    // once and only repositioned/toggled each redraw. Only the drawn shapes
    // (rectangles, lines) are torn down and rebuilt. Clearing the whole canvas
    // each keystroke would yank a TextBox out of the visual tree mid-edit and
    // destroy keyboard focus, so we never do that.
    public partial class WallBlanket : UserControl
    {
        private readonly TextBox _drLeft, _seg1, _seg2, _seg3, _drRight;
        private readonly TextBox _measuredHeight, _copHeight, _copGapBottom;
        private readonly TextBlock _topGapAuto;

        // One greyed placeholder label per field, shown only while the field
        // is empty (WPF TextBox has no native placeholder).
        private readonly Dictionary<TextBox, TextBlock> _placeholders =
            new Dictionary<TextBox, TextBlock>();

        // Only the transient drawn shapes go in here. Persistent TextBoxes,
        // their placeholders and the _topGapAuto label are excluded so they
        // survive a redraw (and keep focus).
        private readonly List<UIElement> _shapes = new List<UIElement>();

        // Nominal mm used to draw the default schematic when a field is blank.
        // Chosen so Left/Right default to a roughly-square, wide blanket.
        private const double DefaultWidthMm = 2200;
        private const double DefaultHeightMm = 2200;
        private const double DefaultCopWidthMm = 240;
        private const double DefaultCopHeightMm = 1300;
        private const double DefaultCopGapMm = 600;

        private double _fixingAllowance = 50;
        private double _edgeAllowance = 10;
        private double _quiltSpacing = 700;
        private bool _quiltEnabled = true;
        private bool _isRear;

        public WallBlanket()
        {
            InitializeComponent();
            // All fields start EMPTY; the hint argument is the greyed
            // placeholder shown inside the box until the operator types.
            _drLeft = MakeField("DR-L");
            _seg1 = MakeField("S1");
            _seg2 = MakeField("COP W");
            _seg3 = MakeField("S3");
            _drRight = MakeField("DR-R");
            _measuredHeight = MakeField("height");
            _copHeight = MakeField("COP H");
            _copGapBottom = MakeField("from btm");
            _topGapAuto = new TextBlock { Foreground = Brushes.Gray, FontSize = 11 };
        }

        // Uniform field width, comfortably fits a 5-digit number plus a little.
        private const double FieldW = 64;
        private const double FieldH = 24;

        private TextBox MakeField(string hint)
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
            tb.GotFocus += Field_FocusChanged;
            tb.LostFocus += Field_FocusChanged;
            _placeholders[tb] = new TextBlock
            {
                Text = hint,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8)),
                IsHitTestVisible = false,   // clicks pass through to the TextBox
                TextAlignment = TextAlignment.Center,
                Width = FieldW,
            };
            return tb;
        }

        private void Field_FocusChanged(object sender, RoutedEventArgs e) => Redraw();

        public void Configure(bool isRear)
        {
            _isRear = isRear;
            IncludeCop.Visibility = isRear ? Visibility.Collapsed : Visibility.Visible;
            // In rear mode the single "width" field reuses the _seg1 TextBox;
            // relabel its placeholder so it reads "width", not "S1".
            _placeholders[_seg1].Text = isRear ? "width" : "S1";
            Redraw();
        }

        public void SetSharedParams(double fixingAllowance, double edgeAllowance,
            double quiltSpacing, bool quiltEnabled)
        {
            _fixingAllowance = fixingAllowance;
            _edgeAllowance = edgeAllowance;
            _quiltSpacing = quiltSpacing;
            _quiltEnabled = quiltEnabled;
            Redraw();
        }

        public bool WallEnabled => IncludeWall.IsChecked == true;
        public bool CopEnabled => !_isRear && IncludeCop.IsChecked == true;
        public void SetWallEnabled(bool v) => IncludeWall.IsChecked = v;
        public void SetWallEnabledInteractive(bool enabled) => IncludeWall.IsEnabled = enabled;

        // Segment accessors. In rear mode only Seg1 (= the single Width field)
        // carries a value; DR-L/S2/S3/DR-R stay empty and read as 0 upstream.
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

        private static double ParseOr(string s, double fallback)
        {
            // double.TryParse accepts "NaN"/"Infinity" and returns true with a
            // non-finite value; those would later reach WPF Rectangle/Line
            // setters, which throw on NaN/Infinity. Treat non-finite as the
            // fallback so a mid-type entry can never produce a non-finite size.
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                   && !double.IsNaN(v) && !double.IsInfinity(v)
                ? v : fallback;
        }

        private static bool IsFinitePositive(double d)
        {
            return !double.IsNaN(d) && !double.IsInfinity(d) && d > 0;
        }

        private void Redraw()
        {
            // The preview redraws on every keystroke from operator-typed values.
            // Despite the finite/positive guards below, wrap the whole rebuild
            // in a swallow-all backstop: a transient bad mid-type value must
            // skip that frame, never throw on the host UI thread (an unhandled
            // exception here would crash DraftSight — there is no dispatcher
            // exception handler).
            try { RedrawCore(); }
            catch { /* skip this frame's preview; the next keystroke retries */ }
        }

        // Fixed layout zones (px) reserved around the drawn wall.
        private const double LeftZone = 78;    // height field on the left
        private const double RightZone = 86;   // COP fields column on the right
        private const double TopZone = 24;      // breathing room above the wall
        private const double BottomZone = 56;   // segment fields + labels below

        private void RedrawCore()
        {
            // _drLeft guards against the checkbox Checked events firing during
            // InitializeComponent (before the constructor builds the fields),
            // independent of XAML element-declaration order.
            if (DrawCanvas == null || _drLeft == null) return;

            // Tear down ONLY the transient drawn shapes. The TextBoxes,
            // placeholders and the auto top-gap label stay in the visual tree.
            foreach (var s in _shapes) DrawCanvas.Children.Remove(s);
            _shapes.Clear();

            double cw = DrawCanvas.ActualWidth;
            double ch = DrawCanvas.ActualHeight;
            // Need room for the wall box plus the reserved field zones, else the
            // box size goes non-positive and a Rectangle setter throws.
            double availW = cw - LeftZone - RightZone;
            double availH = ch - TopZone - BottomZone;
            if (availW < 40 || availH < 40) return;

            // The preview draws the MEASURED wall (landscape, like the paper
            // sheet) — width = segment sum + edge allowance, height = measured
            // height. NB: this is display only. The generated DXF still doubles
            // the height ((measured − fixing) × 2) and applies the fixing
            // clearance — that geometry lives in the calculator/generator and
            // is unchanged here.
            double drawWidth, drawHeight;
            if (_isRear)
            {
                drawWidth = PositiveOr(ParseOr(_seg1.Text, 0), DefaultWidthMm) + _edgeAllowance;
            }
            else
            {
                double segSum = ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0)
                    + ParseOr(_seg2.Text, 0) + ParseOr(_seg3.Text, 0) + ParseOr(_drRight.Text, 0);
                drawWidth = PositiveOr(segSum, DefaultWidthMm) + _edgeAllowance;
            }
            drawHeight = PositiveOr(ParseOr(_measuredHeight.Text, 0), DefaultHeightMm);
            if (!IsFinitePositive(drawWidth) || !IsFinitePositive(drawHeight)) return;

            // Fit the true-proportion wall into the available area.
            double aspect = drawHeight / drawWidth;   // height : width
            double boxW = availW, boxH = availW * aspect;
            if (boxH > availH) { boxH = availH; boxW = availH / aspect; }
            if (!IsFinitePositive(boxW) || !IsFinitePositive(boxH)) return;
            double boxLeft = LeftZone + (availW - boxW) / 2.0;
            double boxTop = TopZone + (availH - boxH) / 2.0;

            // mm → px within the wall box (y up).
            Func<double, double> px = mmX => boxLeft + (mmX / drawWidth) * boxW;
            Func<double, double> py = mmY => boxTop + boxH - (mmY / drawHeight) * boxH;

            AddRect(boxLeft, boxTop, boxW, boxH, Brushes.SteelBlue, 2);
            // Faint fold line at the very top of the measured wall (the panel
            // mirrors/doubles upward from here at Generate time).
            AddDashed(boxLeft, boxTop, boxLeft + boxW, boxTop, Brushes.HotPink);

            if (_isRear)
            {
                DrawRear(px, boxLeft, boxTop, boxW, boxH, drawWidth);
            }
            else
            {
                DrawSegments(px, boxLeft, boxTop, boxW, boxH, drawWidth);
                DrawCop(px, py, boxLeft, boxTop, boxW, boxH, drawWidth, drawHeight);
            }

            // Height field: left of the wall, label above (placed by PlaceField
            // hint); centre it vertically on the wall.
            PlaceField(_measuredHeight, boxLeft - LeftZone + 4, boxTop + boxH / 2.0 - FieldH / 2.0);
        }

        // Left/Right: five segment fields in a stable row below the wall (five
        // equal columns by index — never positioned by value, so blank/zero
        // fields never overlap). Inside the wall, a horizontal dimension line
        // per section with the typed value echoed above it (blank until typed).
        private void DrawSegments(Func<double, double> px, double boxLeft, double boxTop,
            double boxW, double boxH, double drawWidth)
        {
            var fields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            var texts = new[] { _drLeft.Text, _seg1.Text, _seg2.Text, _seg3.Text, _drRight.Text };
            double colW = boxW / 5.0;
            double dimY = boxTop + boxH - 14;   // a little above the bottom edge

            // Section boundaries follow the real cumulative widths when the row
            // has values, else fall back to five equal slots so the guide reads
            // cleanly on a blank form.
            double sum = 0; for (int i = 0; i < 5; i++) sum += ParseOr(texts[i], 0);
            bool haveValues = sum > 0;

            double accumMm = 0;
            for (int i = 0; i < 5; i++)
            {
                // Field: stable equal-column slot below the wall.
                double colCenter = boxLeft + (i + 0.5) * colW;
                PlaceField(fields[i], colCenter - FieldW / 2.0, boxTop + boxH + 8);

                // Section span in px: by real widths if present, else equal slot.
                double segMm = ParseOr(texts[i], 0);
                double x0Px, x1Px;
                if (haveValues)
                {
                    x0Px = px(accumMm);
                    x1Px = px(accumMm + segMm);
                    accumMm += segMm;
                }
                else
                {
                    x0Px = boxLeft + i * colW;
                    x1Px = boxLeft + (i + 1) * colW;
                }

                // Divider between sections.
                if (i > 0)
                {
                    double xDiv = haveValues ? x0Px : boxLeft + i * colW;
                    AddLine(xDiv, boxTop, xDiv, boxTop + boxH, Brushes.LightGray, 0.6);
                }

                // Horizontal dimension line for this section + value echo.
                if (x1Px - x0Px > 4)
                {
                    AddDim(x0Px, dimY, x1Px, dimY, horizontal: true);
                    if (!string.IsNullOrEmpty(texts[i]))
                        AddDimLabel(texts[i], (x0Px + x1Px) / 2.0, dimY - 14, center: true);
                }
            }
        }

        // Rear: single Width field centred below the wall, no COP, no segments.
        private void DrawRear(Func<double, double> px, double boxLeft, double boxTop,
            double boxW, double boxH, double drawWidth)
        {
            HideField(_drLeft); HideField(_seg2); HideField(_seg3); HideField(_drRight);
            HideField(_copHeight); HideField(_copGapBottom);
            _topGapAuto.Visibility = Visibility.Collapsed;

            PlaceField(_seg1, boxLeft + boxW / 2.0 - FieldW / 2.0, boxTop + boxH + 8);

            // Width dimension line along the wall bottom + value echo.
            double dimY = boxTop + boxH - 14;
            AddDim(boxLeft + 4, dimY, boxLeft + boxW - 4, dimY, horizontal: true);
            if (!string.IsNullOrEmpty(_seg1.Text))
                AddDimLabel(_seg1.Text, boxLeft + boxW / 2.0, dimY - 14, center: true);
        }

        private void DrawCop(Func<double, double> px, Func<double, double> py,
            double boxLeft, double boxTop, double boxW, double boxH,
            double drawWidth, double drawHeight)
        {
            bool copOn = CopEnabled;
            _copHeight.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;
            _copGapBottom.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;
            _topGapAuto.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;
            if (!copOn) return;

            // COP geometry uses values when present, else a default schematic
            // slot, so the COP shows as a guide even on a blank form.
            double half = _edgeAllowance / 2.0;
            double copW = PositiveOr(ParseOr(_seg2.Text, 0), DefaultCopWidthMm);
            double copH = PositiveOr(ParseOr(_copHeight.Text, 0), DefaultCopHeightMm);
            double gap = NonNegOr(ParseOr(_copGapBottom.Text, -1), DefaultCopGapMm);
            double offsetMm = ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0);
            double copX0 = (offsetMm > 0) ? half + offsetMm : half + (drawWidth - copW) / 2.0;

            double copPxW = (copW / drawWidth) * boxW;
            double copPxH = (copH / drawHeight) * boxH;
            if (copPxW > 0 && copPxH > 0)
                AddRect(px(copX0), py(gap + copH), copPxW, copPxH, Brushes.Purple, 1.5);

            // Vertical dimension lines for the COP stack, just left of the COP:
            // from-bottom (0..gap), COP height (gap..gap+copH), top gap
            // (gap+copH..measured). Value echoes only when the field is typed.
            double dimX = px(copX0) - 10;
            double yBottom = py(0), yGapTop = py(gap), yCopTop = py(gap + copH), yWallTop = boxTop;
            AddDim(dimX, yBottom, dimX, yGapTop, horizontal: false);
            AddDim(dimX, yGapTop, dimX, yCopTop, horizontal: false);
            AddDim(dimX, yCopTop, dimX, yWallTop, horizontal: false);
            if (!string.IsNullOrEmpty(_copGapBottom.Text))
                AddDimLabel(_copGapBottom.Text, dimX - 4, (yBottom + yGapTop) / 2.0, center: false);
            if (!string.IsNullOrEmpty(_copHeight.Text))
                AddDimLabel(_copHeight.Text, dimX - 4, (yGapTop + yCopTop) / 2.0, center: false);

            // COP input fields: fixed RIGHT column, clear of the drawing, so
            // they're never covered and always clickable. Labels above (hints).
            double colX = boxLeft + boxW + 8;
            PlaceField(_copHeight, colX, boxTop + boxH * 0.30);
            PlaceField(_copGapBottom, colX, boxTop + boxH * 0.55);

            // Auto top-gap readout below the two COP fields (computed only when
            // the real measured height + COP height are present).
            double measured = ParseOr(_measuredHeight.Text, 0);
            if (measured > 0 && IsFinitePositive(ParseOr(_copHeight.Text, 0)) && ParseOr(_copHeight.Text, 0) > 0)
            {
                double topGap = LiftBlanketCalculator.AutoTopGap(measured, _fixingAllowance,
                    ParseOr(_copGapBottom.Text, 0), ParseOr(_copHeight.Text, 0));
                _topGapAuto.Text = "top gap: " + topGap.ToString("0", CultureInfo.InvariantCulture) +
                    (topGap < 0 ? " ⚠" : "");
                _topGapAuto.Foreground = topGap < 0 ? Brushes.Red : Brushes.Gray;
            }
            else
            {
                _topGapAuto.Text = "top gap (auto)";
                _topGapAuto.Foreground = Brushes.Gray;
            }
            Place(_topGapAuto, colX, boxTop + boxH * 0.55 + FieldH + 6);

            // Quilt preview only with real values (no schematic quilting).
            if (_quiltEnabled)
            {
                var calc = new LiftBlanketCalculator(_fixingAllowance, _edgeAllowance);
                var wall = ReadWallModel();
                if (wall.Width > 0 && wall.MeasuredHeight > 0)
                {
                    // The quilt math works in CUT (doubled) coordinates; the
                    // preview is in measured coords, so only the bottom-half
                    // quilt lines (Y ≤ measured) map 1:1 — which is exactly the
                    // region quilting fills. Clip anything above the wall.
                    WallLayout layout = calc.LayoutWall(wall, 0, "", "", _quiltEnabled, _quiltSpacing);
                    foreach (LineSpec q in layout.QuiltLines)
                    {
                        if (q.Y0 > drawHeight + 0.5 || q.Y1 > drawHeight + 0.5) continue;
                        AddLine(px(q.X0), py(q.Y0), px(q.X1), py(q.Y1),
                            new SolidColorBrush(Color.FromRgb(0xD8, 0xB0, 0xE8)), 0.8);
                    }
                }
            }
        }

        // A thin dimension line with small end ticks. Transient (in _shapes).
        private void AddDim(double x1, double y1, double x2, double y2, bool horizontal)
        {
            Brush b = new SolidColorBrush(Color.FromRgb(0xC7, 0x7B, 0x30));
            AddLine(x1, y1, x2, y2, b, 0.8);
            if (horizontal)
            {
                AddLine(x1, y1 - 3, x1, y1 + 3, b, 0.8);
                AddLine(x2, y2 - 3, x2, y2 + 3, b, 0.8);
            }
            else
            {
                AddLine(x1 - 3, y1, x1 + 3, y1, b, 0.8);
                AddLine(x2 - 3, y2, x2 + 3, y2, b, 0.8);
            }
        }

        // A transient value-echo label drawn on a dimension line. center=true
        // horizontally centres on x; false right-aligns to x (for vertical dims
        // sitting to the left of the COP).
        private void AddDimLabel(string text, double x, double y, bool center)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC7, 0x7B, 0x30)),
                IsHitTestVisible = false,
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = tb.DesiredSize.Width;
            Canvas.SetLeft(tb, center ? x - w / 2.0 : x - w);
            Canvas.SetTop(tb, y - tb.DesiredSize.Height / 2.0);
            DrawCanvas.Children.Add(tb);
            _shapes.Add(tb);
        }

        private static double PositiveOr(double v, double fallback) => v > 0 ? v : fallback;
        private static double NonNegOr(double v, double fallback) => v >= 0 ? v : fallback;

        private WallDimensions ReadWallModel()
        {
            var w = new WallDimensions();
            w.Segments.DoorReturnLeft = ParseOr(DrLeftText, 0);
            w.Segments.Seg1 = ParseOr(Seg1Text, 0);
            w.Segments.Seg2 = ParseOr(Seg2Text, 0);
            w.Segments.Seg3 = ParseOr(Seg3Text, 0);
            w.Segments.DoorReturnRight = ParseOr(DrRightText, 0);
            w.MeasuredHeight = ParseOr(_measuredHeight.Text, 0);
            w.Cop.Enabled = CopEnabled;
            w.Cop.Height = ParseOr(_copHeight.Text, 0);
            w.Cop.GapFromBottom = ParseOr(_copGapBottom.Text, 0);
            return w;
        }

        private void AddRect(double x, double y, double w, double h, Brush stroke, double thick)
        {
            var r = new Rectangle { Width = w, Height = h, Stroke = stroke, StrokeThickness = thick, Fill = Brushes.Transparent };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            DrawCanvas.Children.Add(r);
            _shapes.Add(r);
        }
        private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double thick)
        {
            var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thick };
            DrawCanvas.Children.Add(line);
            _shapes.Add(line);
        }
        private void AddDashed(double x1, double y1, double x2, double y2, Brush stroke)
        {
            var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 } };
            DrawCanvas.Children.Add(line);
            _shapes.Add(line);
        }

        // Adds the field (and its placeholder) to the canvas once, then
        // repositions. The placeholder sits at the same spot and shows only
        // while the field is empty (and the field isn't focused).
        private void PlaceField(TextBox tb, double x, double y)
        {
            tb.Visibility = Visibility.Visible;
            if (!DrawCanvas.Children.Contains(tb)) DrawCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);

            TextBlock ph = _placeholders[tb];
            if (!DrawCanvas.Children.Contains(ph)) DrawCanvas.Children.Add(ph);
            bool showHint = string.IsNullOrEmpty(tb.Text) && !tb.IsFocused;
            ph.Visibility = showHint ? Visibility.Visible : Visibility.Collapsed;
            // Sit the hint over the field (TextBox content padding ~3px).
            Canvas.SetLeft(ph, x);
            Canvas.SetTop(ph, y + 3);
            Panel.SetZIndex(ph, 10);
        }

        private void HideField(TextBox tb)
        {
            tb.Visibility = Visibility.Collapsed;
            if (_placeholders.TryGetValue(tb, out TextBlock ph)) ph.Visibility = Visibility.Collapsed;
        }

        private void Place(UIElement el, double x, double y)
        {
            if (!DrawCanvas.Children.Contains(el)) DrawCanvas.Children.Add(el);
            Canvas.SetLeft(el, x); Canvas.SetTop(el, y);
        }
    }
}
