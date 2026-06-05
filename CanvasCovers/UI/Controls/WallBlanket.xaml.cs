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
    // Interactive per-wall input surface: a blanket drawn to true proportion
    // (clamped) with embedded measurement fields. Redraws live on edit. The
    // hosting window reads values via the public accessors and the embedded
    // TextBoxes. Rear-wall mode hides the COP fields.
    //
    // Redraw discipline: the embedded TextBoxes (and the auto top-gap label)
    // are persistent children of the canvas — they are added ONCE and only
    // repositioned on each redraw. Only the drawn shapes (rectangles, lines)
    // are torn down and rebuilt each pass. Clearing the whole canvas on every
    // keystroke would yank a TextBox out of the visual tree mid-edit and
    // destroy keyboard focus, so we never do that.
    public partial class WallBlanket : UserControl
    {
        private readonly TextBox _drLeft, _seg1, _seg2, _seg3, _drRight;
        private readonly TextBox _measuredHeight, _copHeight, _copGapBottom;
        private readonly TextBlock _topGapAuto;

        // Only the transient drawn shapes go in here. Persistent TextBoxes and
        // the _topGapAuto label are deliberately excluded so they survive a
        // redraw (and keep focus).
        private readonly List<UIElement> _shapes = new List<UIElement>();

        private double _fixingAllowance = 50;
        private double _edgeAllowance = 10;
        private double _quiltSpacing = 700;
        private bool _quiltEnabled = true;
        private bool _isRear;

        public WallBlanket()
        {
            InitializeComponent();
            _drLeft = MakeField("0");
            _seg1 = MakeField("0");
            _seg2 = MakeField("240");
            _seg3 = MakeField("1400");
            _drRight = MakeField("0");
            _measuredHeight = MakeField("2200");
            _copHeight = MakeField("1300");
            _copGapBottom = MakeField("600");
            _topGapAuto = new TextBlock { Foreground = Brushes.Gray, FontSize = 11 };
        }

        private TextBox MakeField(string value)
        {
            var tb = new TextBox { Text = value, Width = 56, TextAlignment = TextAlignment.Center };
            tb.TextChanged += Input_Changed;
            return tb;
        }

        public void Configure(bool isRear)
        {
            _isRear = isRear;
            IncludeCop.Visibility = isRear ? Visibility.Collapsed : Visibility.Visible;
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

        public string DrLeftText => _drLeft.Text;
        public string Seg1Text => _seg1.Text;
        public string Seg2Text => _seg2.Text;
        public string Seg3Text => _seg3.Text;
        public string DrRightText => _drRight.Text;
        public string MeasuredHeightText => _measuredHeight.Text;
        public string CopHeightText => _copHeight.Text;
        public string CopGapBottomText => _copGapBottom.Text;

        public void Seed(string drL, string s1, string s2, string s3, string drR, string measuredH)
        {
            _drLeft.Text = drL; _seg1.Text = s1; _seg2.Text = s2;
            _seg3.Text = s3; _drRight.Text = drR; _measuredHeight.Text = measuredH;
        }

        private void Input_Changed(object sender, RoutedEventArgs e) => Redraw();
        private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        private static double ParseOr(string s, double fallback)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        private void Redraw()
        {
            // _drLeft guards against the checkbox Checked events firing during
            // InitializeComponent (before the constructor builds the fields),
            // independent of XAML element-declaration order.
            if (DrawCanvas == null || _drLeft == null) return;

            // Tear down ONLY the transient drawn shapes. The TextBoxes and the
            // auto top-gap label stay in the visual tree so editing keeps focus.
            foreach (var s in _shapes) DrawCanvas.Children.Remove(s);
            _shapes.Clear();

            double cw = DrawCanvas.ActualWidth;
            double ch = DrawCanvas.ActualHeight;
            if (cw < 20 || ch < 20) return;

            double segSum = ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0)
                + ParseOr(_seg2.Text, 0) + ParseOr(_seg3.Text, 0) + ParseOr(_drRight.Text, 0);
            double cutWidth = segSum + _edgeAllowance;
            double measuredH = ParseOr(_measuredHeight.Text, 2200);
            double cutHeight = LiftBlanketCalculator.CutHeight(measuredH, _fixingAllowance);
            if (cutWidth <= 0 || cutHeight <= 0) return;

            double aspect = cutHeight / cutWidth;
            aspect = Math.Max(0.33, Math.Min(3.0, aspect));

            double padX = 90, padY = 50;
            double availW = cw - 2 * padX, availH = ch - 2 * padY;
            double boxW = availW, boxH = availW * aspect;
            if (boxH > availH) { boxH = availH; boxW = availH / aspect; }
            double boxLeft = (cw - boxW) / 2;
            double boxTop = (ch - boxH) / 2;

            Func<double, double> px = mmX => boxLeft + (mmX / cutWidth) * boxW;
            Func<double, double> py = mmY => boxTop + boxH - (mmY / cutHeight) * boxH;

            AddRect(boxLeft, boxTop, boxW, boxH, Brushes.SteelBlue, 2);

            double foldY = LiftBlanketCalculator.HalfHeight(measuredH, _fixingAllowance);
            AddDashed(boxLeft, py(foldY), boxLeft + boxW, py(foldY), Brushes.HotPink);

            double half = _edgeAllowance / 2.0;

            bool copOn = CopEnabled;
            // Persistent COP fields + auto label: toggle visibility rather than
            // add/remove so they never float over a COP-less blanket and never
            // get duplicated.
            _copHeight.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;
            _copGapBottom.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;
            _topGapAuto.Visibility = copOn ? Visibility.Visible : Visibility.Collapsed;

            if (copOn)
            {
                double copH = ParseOr(_copHeight.Text, 1300);
                double gap = ParseOr(_copGapBottom.Text, 600);
                double copX0 = half + ParseOr(_drLeft.Text, 0) + ParseOr(_seg1.Text, 0);
                double copW = ParseOr(_seg2.Text, 0);
                AddRect(px(copX0), py(gap + copH), (copW / cutWidth) * boxW,
                    ((copH) / cutHeight) * boxH, Brushes.Purple, 1.5);

                double topGap = LiftBlanketCalculator.AutoTopGap(measuredH, _fixingAllowance, gap, copH);
                _topGapAuto.Text = "top gap (auto): " +
                    topGap.ToString("0", CultureInfo.InvariantCulture) +
                    (topGap < 0 ? "  crosses fold" : "");
                _topGapAuto.Foreground = topGap < 0 ? Brushes.Red : Brushes.Gray;

                PlaceField(_copHeight, px(copX0 + copW) + 6, py(gap + copH));
                PlaceField(_copGapBottom, px(copX0 + copW) + 6, py(gap) - 12);
                Place(_topGapAuto, px(copX0 + copW) + 6, py(gap + copH) - 26);

                if (_quiltEnabled)
                {
                    var calc = new LiftBlanketCalculator(_fixingAllowance, _edgeAllowance);
                    var wall = ReadWallModel();
                    WallLayout layout = calc.LayoutWall(wall, 0, "", "",
                        _quiltEnabled, _quiltSpacing);
                    foreach (LineSpec q in layout.QuiltLines)
                    {
                        AddLine(px(q.X0), py(q.Y0), px(q.X1), py(q.Y1),
                            new SolidColorBrush(Color.FromRgb(0xD8, 0xB0, 0xE8)), 0.8);
                    }
                }
            }
            else
            {
                _topGapAuto.Text = "";
            }

            double accum = 0;
            var segVals = new[] { ParseOr(_drLeft.Text, 0), ParseOr(_seg1.Text, 0),
                ParseOr(_seg2.Text, 0), ParseOr(_seg3.Text, 0), ParseOr(_drRight.Text, 0) };
            var segFields = new[] { _drLeft, _seg1, _seg2, _seg3, _drRight };
            for (int i = 0; i < 5; i++)
            {
                double startMm = half + accum;
                if (i > 0 && startMm > half + 0.001)
                    AddLine(px(startMm), boxTop, px(startMm), boxTop + boxH, Brushes.LightGray, 0.6);
                PlaceField(segFields[i], px(startMm + segVals[i] / 2.0) - 28, boxTop + boxH + 8);
                accum += segVals[i];
            }

            PlaceField(_measuredHeight, boxLeft - 78, boxTop + boxH / 2 - 12);
        }

        private WallDimensions ReadWallModel()
        {
            var w = new WallDimensions();
            w.Segments.DoorReturnLeft = ParseOr(_drLeft.Text, 0);
            w.Segments.Seg1 = ParseOr(_seg1.Text, 0);
            w.Segments.Seg2 = ParseOr(_seg2.Text, 0);
            w.Segments.Seg3 = ParseOr(_seg3.Text, 0);
            w.Segments.DoorReturnRight = ParseOr(_drRight.Text, 0);
            w.MeasuredHeight = ParseOr(_measuredHeight.Text, 2200);
            w.Cop.Enabled = CopEnabled;
            w.Cop.Height = ParseOr(_copHeight.Text, 1300);
            w.Cop.GapFromBottom = ParseOr(_copGapBottom.Text, 600);
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
        private void PlaceField(TextBox tb, double x, double y)
        {
            if (!DrawCanvas.Children.Contains(tb)) DrawCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
        }
        private void Place(UIElement el, double x, double y)
        {
            if (!DrawCanvas.Children.Contains(el)) DrawCanvas.Children.Add(el);
            Canvas.SetLeft(el, x); Canvas.SetTop(el, y);
        }
    }
}
