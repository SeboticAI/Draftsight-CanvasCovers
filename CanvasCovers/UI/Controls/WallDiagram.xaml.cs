using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CanvasCovers.UI.Controls
{
    public enum WallContext
    {
        Left,
        Right,
        Rear,
    }

    public partial class WallDiagram : UserControl
    {
        // Dimension keys recognised by Highlight(). Each is a Tag value on the
        // matching input in LiftBlanketWindow.xaml.
        public const string KeyMainWidth = "MainWidth";
        public const string KeyMainHeight = "MainHeight";
        public const string KeyDoorReturn1 = "DoorReturn1";
        public const string KeyDoorReturn2 = "DoorReturn2";
        public const string KeyDoorReturn3 = "DoorReturn3";
        public const string KeyCopTopOffset = "CopTopOffset";
        public const string KeyCopHeight = "CopHeight";
        public const string KeyCopWidth = "CopWidth";

        private static readonly Brush WallStroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x52));
        private static readonly Brush WallFill = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF3));
        private static readonly Brush ReturnFill = new SolidColorBrush(Color.FromRgb(0xDF, 0xE5, 0xEC));
        private static readonly Brush CopStroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly Brush CopFill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        private static readonly Brush DimStroke = new SolidColorBrush(Color.FromRgb(0x99, 0xA3, 0xAD));
        private static readonly Brush LabelText = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xC7, 0x7B, 0x30));

        private readonly Dictionary<string, Callout> _callouts =
            new Dictionary<string, Callout>();
        private WallContext _context = WallContext.Left;
        private string _currentKey;

        public WallDiagram()
        {
            InitializeComponent();
            ShowWall(WallContext.Left);
        }

        // Repaints the diagram for the named wall. Preserves the current
        // highlight if the new wall has the same dimension key.
        public void ShowWall(WallContext context)
        {
            // Avoid redrawing when the cursor sweeps in and out of the same
            // section — Canvas.Children.Clear()+rebuild is cheap but not free.
            if (_context == context && DrawCanvas.Children.Count > 0)
            {
                return;
            }
            _context = context;
            switch (context)
            {
                case WallContext.Left: TitleText.Text = "LEFT WALL"; break;
                case WallContext.Right: TitleText.Text = "RIGHT WALL"; break;
                case WallContext.Rear: TitleText.Text = "REAR WALL"; break;
            }

            DrawCanvas.Children.Clear();
            _callouts.Clear();

            if (context == WallContext.Rear)
            {
                BuildRear();
            }
            else
            {
                BuildFull(mirrored: context == WallContext.Right);
            }

            // Re-apply the existing key against the freshly built shapes so
            // switching wall sections doesn't lose the visual focus.
            if (!string.IsNullOrEmpty(_currentKey) && _callouts.ContainsKey(_currentKey))
            {
                Apply(_callouts[_currentKey], highlighted: true);
            }
        }

        public void Highlight(string key)
        {
            if (!string.IsNullOrEmpty(_currentKey)
                && _callouts.TryGetValue(_currentKey, out Callout previous))
            {
                Apply(previous, highlighted: false);
            }

            _currentKey = key;

            if (!string.IsNullOrEmpty(key)
                && _callouts.TryGetValue(key, out Callout current))
            {
                Apply(current, highlighted: true);
            }
        }

        public void ClearHighlight()
        {
            Highlight(null);
        }

        private void BuildFull(bool mirrored)
        {
            // Canvas is 260 x 290. Wall outline lives inside an inset frame so
            // dimension callouts can sit outside the rectangle without being
            // clipped. The 3 door returns are fixed-width strips (the real
            // values vary wildly between jobs and this is a reference picture,
            // not a preview).
            const double wallTop = 50;
            const double wallBottom = 230;
            const double wallLeft = 30;
            const double wallRight = 230;
            const double returnWidth = 18;
            const double wallHeight = wallBottom - wallTop;

            double mainLeft, mainRight;
            double dr1Left, dr2Left, dr3Left;
            if (!mirrored)
            {
                // Order left-to-right: DR1, DR2, DR3, MAIN
                dr1Left = wallLeft;
                dr2Left = dr1Left + returnWidth;
                dr3Left = dr2Left + returnWidth;
                mainLeft = dr3Left + returnWidth;
                mainRight = wallRight;
            }
            else
            {
                // Order left-to-right: MAIN, DR1, DR2, DR3
                mainLeft = wallLeft;
                mainRight = wallRight - returnWidth * 3;
                dr1Left = mainRight;
                dr2Left = dr1Left + returnWidth;
                dr3Left = dr2Left + returnWidth;
            }
            double mainWidthPx = mainRight - mainLeft;

            // Main area rectangle (lighter fill).
            Rectangle main = AddRect(mainLeft, wallTop, mainWidthPx, wallHeight, WallStroke, WallFill);

            // Door return strips.
            Rectangle dr1 = AddRect(dr1Left, wallTop, returnWidth, wallHeight, WallStroke, ReturnFill);
            Rectangle dr2 = AddRect(dr2Left, wallTop, returnWidth, wallHeight, WallStroke, ReturnFill);
            Rectangle dr3 = AddRect(dr3Left, wallTop, returnWidth, wallHeight, WallStroke, ReturnFill);

            AddCenteredLabel(dr1Left + returnWidth / 2, (wallTop + wallBottom) / 2, "1", 10, LabelText);
            AddCenteredLabel(dr2Left + returnWidth / 2, (wallTop + wallBottom) / 2, "2", 10, LabelText);
            AddCenteredLabel(dr3Left + returnWidth / 2, (wallTop + wallBottom) / 2, "3", 10, LabelText);

            Register(KeyDoorReturn1, null, dr1);
            Register(KeyDoorReturn2, null, dr2);
            Register(KeyDoorReturn3, null, dr3);

            // COP cutout centered horizontally in the main area, top inset
            // mimics a typical CopTopOffset.
            double copWidthPx = mainWidthPx * 0.45;
            double copHeightPx = 60;
            double copTopOffsetPx = 22;
            double copLeft = mainLeft + (mainWidthPx - copWidthPx) / 2;
            double copTop = wallTop + copTopOffsetPx;

            Rectangle cop = AddRect(copLeft, copTop, copWidthPx, copHeightPx, CopStroke, CopFill);
            TextBlock copLabel = AddCenteredLabel(copLeft + copWidthPx / 2, copTop + copHeightPx / 2, "COP", 10, LabelText);

            // CopWidth: horizontal dim line above COP.
            Line[] copWidthLines = AddHDimLine(copLeft, copLeft + copWidthPx, copTop - 10, DimStroke);
            TextBlock copWidthLabel = AddCenteredLabel(copLeft + copWidthPx / 2, copTop - 20, "COP Width", 10, LabelText);

            // CopHeight: vertical dim line on outboard side of COP.
            double copHeightDimX = mirrored ? copLeft - 10 : copLeft + copWidthPx + 10;
            Line[] copHeightLines = AddVDimLine(copHeightDimX, copTop, copTop + copHeightPx, DimStroke);
            TextBlock copHeightLabel = AddRotatedLabel(copHeightDimX + (mirrored ? -10 : 10), copTop + copHeightPx / 2, "COP Height", 10, LabelText, mirrored);

            // CopTopOffset: vertical dim line between wall top and COP top,
            // placed slightly off the COP rectangle on the side opposite the
            // CopHeight callout to avoid overlap.
            double copTopOffsetX = mirrored ? copLeft + copWidthPx + 10 : copLeft - 10;
            Line[] copTopOffsetLines = AddVDimLine(copTopOffsetX, wallTop, copTop, DimStroke);
            TextBlock copTopOffsetLabel = AddRotatedLabel(copTopOffsetX + (mirrored ? 10 : -10), (wallTop + copTop) / 2, "Top Offset", 9, LabelText, !mirrored);

            Register(KeyCopWidth, copWidthLabel, Combine(cop, copWidthLines));
            Register(KeyCopHeight, copHeightLabel, Combine(cop, copHeightLines));
            Register(KeyCopTopOffset, copTopOffsetLabel, copTopOffsetLines);

            // MainWidth dim line + label below the MAIN area only.
            Line[] mainWidthLines = AddHDimLine(mainLeft, mainRight, wallBottom + 14, DimStroke);
            TextBlock mainWidthLabel = AddCenteredLabel((mainLeft + mainRight) / 2, wallBottom + 26, "Main Width", 10, LabelText);
            Register(KeyMainWidth, mainWidthLabel, Combine(main, mainWidthLines));

            // MainHeight dim line + label outside the wall, on the door-return
            // side so it sits clear of the COP-Height callout.
            double heightDimX = mirrored ? wallRight + 14 : wallLeft - 14;
            Line[] mainHeightLines = AddVDimLine(heightDimX, wallTop, wallBottom, DimStroke);
            TextBlock mainHeightLabel = AddRotatedLabel(heightDimX + (mirrored ? 10 : -10), (wallTop + wallBottom) / 2, "Main Height", 10, LabelText, !mirrored);
            Register(KeyMainHeight, mainHeightLabel, mainHeightLines);
        }

        private void BuildRear()
        {
            const double wallTop = 60;
            const double wallBottom = 220;
            const double wallLeft = 50;
            const double wallRight = 210;

            Rectangle wall = AddRect(wallLeft, wallTop, wallRight - wallLeft, wallBottom - wallTop, WallStroke, WallFill);
            AddCenteredLabel((wallLeft + wallRight) / 2, (wallTop + wallBottom) / 2 - 10, "REAR", 10, LabelText);
            AddCenteredLabel((wallLeft + wallRight) / 2, (wallTop + wallBottom) / 2 + 6, "WALL", 10, LabelText);

            Line[] widthLines = AddHDimLine(wallLeft, wallRight, wallBottom + 14, DimStroke);
            TextBlock widthLabel = AddCenteredLabel((wallLeft + wallRight) / 2, wallBottom + 26, "Width", 10, LabelText);
            Register(KeyMainWidth, widthLabel, Combine(wall, widthLines));

            Line[] heightLines = AddVDimLine(wallLeft - 14, wallTop, wallBottom, DimStroke);
            TextBlock heightLabel = AddRotatedLabel(wallLeft - 24, (wallTop + wallBottom) / 2, "Height", 10, LabelText, true);
            Register(KeyMainHeight, heightLabel, heightLines);
        }

        private Rectangle AddRect(double x, double y, double w, double h, Brush stroke, Brush fill)
        {
            Rectangle r = new Rectangle
            {
                Width = w,
                Height = h,
                Stroke = stroke,
                StrokeThickness = 1.2,
                Fill = fill,
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            DrawCanvas.Children.Add(r);
            return r;
        }

        private Line[] AddHDimLine(double x1, double x2, double y, Brush stroke)
        {
            Line main = new Line
            {
                X1 = x1, X2 = x2, Y1 = y, Y2 = y,
                Stroke = stroke, StrokeThickness = 1,
            };
            Line t1 = MakeTick(x1, y, true, stroke);
            Line t2 = MakeTick(x2, y, true, stroke);
            DrawCanvas.Children.Add(main);
            DrawCanvas.Children.Add(t1);
            DrawCanvas.Children.Add(t2);
            return new[] { main, t1, t2 };
        }

        private Line[] AddVDimLine(double x, double y1, double y2, Brush stroke)
        {
            Line main = new Line
            {
                X1 = x, X2 = x, Y1 = y1, Y2 = y2,
                Stroke = stroke, StrokeThickness = 1,
            };
            Line t1 = MakeTick(x, y1, false, stroke);
            Line t2 = MakeTick(x, y2, false, stroke);
            DrawCanvas.Children.Add(main);
            DrawCanvas.Children.Add(t1);
            DrawCanvas.Children.Add(t2);
            return new[] { main, t1, t2 };
        }

        private static Line MakeTick(double x, double y, bool horizontalLine, Brush stroke)
        {
            // A short perpendicular tick mark at the end of a dim line.
            if (horizontalLine)
            {
                return new Line { X1 = x, X2 = x, Y1 = y - 3, Y2 = y + 3, Stroke = stroke, StrokeThickness = 1 };
            }
            return new Line { X1 = x - 3, X2 = x + 3, Y1 = y, Y2 = y, Stroke = stroke, StrokeThickness = 1 };
        }

        private TextBlock AddCenteredLabel(double cx, double cy, string text, double size, Brush brush)
        {
            TextBlock t = new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = brush,
            };
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(t, cx - t.DesiredSize.Width / 2);
            Canvas.SetTop(t, cy - t.DesiredSize.Height / 2);
            DrawCanvas.Children.Add(t);
            return t;
        }

        private TextBlock AddRotatedLabel(double cx, double cy, string text, double size, Brush brush, bool rotateCounterClockwise)
        {
            TextBlock t = new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = brush,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double angle = rotateCounterClockwise ? -90 : 90;
            t.RenderTransform = new RotateTransform(angle);
            Canvas.SetLeft(t, cx - t.DesiredSize.Width / 2);
            Canvas.SetTop(t, cy - t.DesiredSize.Height / 2);
            DrawCanvas.Children.Add(t);
            return t;
        }

        private void Register(string key, TextBlock label, params Shape[] shapes)
        {
            Callout callout;
            if (!_callouts.TryGetValue(key, out callout))
            {
                callout = new Callout();
                _callouts[key] = callout;
            }

            if (shapes != null)
            {
                foreach (Shape s in shapes)
                {
                    if (s != null) callout.Shapes.Add(s);
                }
            }
            if (label != null)
            {
                callout.Labels.Add(label);
            }
        }

        private static Shape[] Combine(Shape first, Shape[] rest)
        {
            // Flatten (rect + dim-line shapes) into one array for Register's
            // params Shape[] argument.
            Shape[] result = new Shape[rest.Length + 1];
            result[0] = first;
            for (int i = 0; i < rest.Length; i++) result[i + 1] = rest[i];
            return result;
        }

        private static void Apply(Callout callout, bool highlighted)
        {
            Brush stroke = highlighted ? AccentBrush : null;
            double thickness = highlighted ? 2.0 : 1.0;

            foreach (Shape shape in callout.Shapes)
            {
                if (!callout.OriginalStroke.ContainsKey(shape))
                {
                    callout.OriginalStroke[shape] = shape.Stroke;
                    callout.OriginalThickness[shape] = shape.StrokeThickness;
                }

                shape.Stroke = highlighted ? AccentBrush : callout.OriginalStroke[shape];
                shape.StrokeThickness = highlighted ? GetHighlightThickness(shape) : callout.OriginalThickness[shape];
            }

            foreach (TextBlock label in callout.Labels)
            {
                if (!callout.OriginalForeground.ContainsKey(label))
                {
                    callout.OriginalForeground[label] = label.Foreground;
                }

                label.Foreground = highlighted ? AccentBrush : callout.OriginalForeground[label];
                label.FontWeight = highlighted ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private static double GetHighlightThickness(Shape shape)
        {
            // Lines stay thin on highlight (they're already visual accents);
            // rectangles get a bolder border so they read as the focused area.
            return shape is Line ? 1.8 : 2.2;
        }

        private class Callout
        {
            public readonly List<Shape> Shapes = new List<Shape>();
            public readonly List<TextBlock> Labels = new List<TextBlock>();
            public readonly Dictionary<Shape, Brush> OriginalStroke = new Dictionary<Shape, Brush>();
            public readonly Dictionary<Shape, double> OriginalThickness = new Dictionary<Shape, double>();
            public readonly Dictionary<TextBlock, Brush> OriginalForeground = new Dictionary<TextBlock, Brush>();
        }
    }
}
