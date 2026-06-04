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
        public const string KeyDrLeft = "DrLeft";
        public const string KeySeg1 = "Seg1";
        public const string KeySeg2 = "Seg2";
        public const string KeySeg3 = "Seg3";
        public const string KeyDrRight = "DrRight";
        public const string KeyMeasuredHeight = "MeasuredHeight";
        public const string KeyCopWidth = "CopWidth";
        public const string KeyCopHeight = "CopHeight";
        public const string KeyCopGapBottom = "CopGapBottom";
        public const string KeyCopOffsetLeft = "CopOffsetLeft";

        private static readonly Brush WallStroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x52));
        private static readonly Brush WallFill = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF3));
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
            // Canvas 260 x 290. Illustrative only — not to scale. The wall is a
            // plain rectangle; the bottom edge carries five segment callouts
            // (DrLeft | Seg1 | Seg2 | Seg3 | DrRight), and an optional COP sits
            // in the lower portion positioned from the wall bottom + left edge.
            const double wallTop = 50;
            const double wallBottom = 215;
            const double wallLeft = 40;
            const double wallRight = 225;
            double wallWidth = wallRight - wallLeft;

            // Wall outline.
            Rectangle wall = AddRect(wallLeft, wallTop, wallWidth, wallBottom - wallTop, WallStroke, WallFill);

            // Five bottom-edge segments. Illustrative proportions (DrLeft and
            // DrRight are thin tabs; the three interior segments share the rest).
            // For a mirrored (right) wall, the order flips so DrRight reads on
            // the left, matching how the right wall is drawn in the real DXF.
            double tab = wallWidth * 0.10;
            double interior = (wallWidth - 2 * tab) / 3.0;

            string[] order = mirrored
                ? new[] { KeyDrRight, KeySeg3, KeySeg2, KeySeg1, KeyDrLeft }
                : new[] { KeyDrLeft, KeySeg1, KeySeg2, KeySeg3, KeyDrRight };
            // Symmetric, so mirroring the order doesn't change the widths.
            double[] widths = { tab, interior, interior, interior, tab };
            string[] captions = mirrored
                ? new[] { "DR-R", "S3", "S2", "S1", "DR-L" }
                : new[] { "DR-L", "S1", "S2", "S3", "DR-R" };

            double segDimY = wallBottom + 14;
            double x = wallLeft;
            for (int i = 0; i < order.Length; i++)
            {
                double segW = widths[i];
                // Vertical separator inside the wall for each boundary (skip the
                // outer edges, which are the wall outline itself).
                if (i > 0)
                {
                    Line sep = new Line
                    {
                        X1 = x, X2 = x, Y1 = wallTop, Y2 = wallBottom,
                        Stroke = DimStroke, StrokeThickness = 0.6,
                    };
                    DrawCanvas.Children.Add(sep);
                }
                Line[] segLines = AddHDimLine(x, x + segW, segDimY, DimStroke);
                TextBlock segLabel = AddCenteredLabel(x + segW / 2, segDimY + 10, captions[i], 9, LabelText);
                Register(order[i], segLabel, segLines);
                x += segW;
            }

            // Measured Height dim on the outer side (left for a normal wall,
            // right for a mirrored one).
            double heightDimX = mirrored ? wallRight + 16 : wallLeft - 16;
            Line[] heightLines = AddVDimLine(heightDimX, wallTop, wallBottom, DimStroke);
            TextBlock heightLabel = AddRotatedLabel(
                heightDimX + (mirrored ? 10 : -10), (wallTop + wallBottom) / 2,
                "Meas. Height", 9, LabelText, !mirrored);
            Register(KeyMeasuredHeight, heightLabel, Combine(wall, heightLines));

            // COP rectangle in the lower portion.
            double copWidthPx = wallWidth * 0.22;
            double copHeightPx = 70;
            double copGapBottomPx = 26;                 // gap from wall bottom up to COP bottom
            double copOffsetLeftPx = wallWidth * 0.30;  // gap from wall left to COP left
            double copLeft = wallLeft + copOffsetLeftPx;
            double copBottom = wallBottom - copGapBottomPx;
            double copTop = copBottom - copHeightPx;

            Rectangle cop = AddRect(copLeft, copTop, copWidthPx, copHeightPx, CopStroke, CopFill);
            AddCenteredLabel(copLeft + copWidthPx / 2, copTop + copHeightPx / 2, "COP", 10, LabelText);

            // CopWidth: horizontal dim above the COP.
            Line[] copWidthLines = AddHDimLine(copLeft, copLeft + copWidthPx, copTop - 8, DimStroke);
            TextBlock copWidthLabel = AddCenteredLabel(copLeft + copWidthPx / 2, copTop - 17, "COP W", 9, LabelText);
            Register(KeyCopWidth, copWidthLabel, Combine(cop, copWidthLines));

            // CopHeight: vertical dim on the inboard side of the COP.
            double copHeightDimX = copLeft + copWidthPx + 10;
            Line[] copHeightLines = AddVDimLine(copHeightDimX, copTop, copBottom, DimStroke);
            TextBlock copHeightLabel = AddRotatedLabel(copHeightDimX + 10, (copTop + copBottom) / 2, "COP H", 9, LabelText, false);
            Register(KeyCopHeight, copHeightLabel, Combine(cop, copHeightLines));

            // CopGapBottom: vertical dim from the COP bottom down to the wall
            // bottom, on the same inboard side.
            Line[] copGapLines = AddVDimLine(copHeightDimX, copBottom, wallBottom, DimStroke);
            TextBlock copGapLabel = AddRotatedLabel(copHeightDimX + 10, (copBottom + wallBottom) / 2, "From Bot.", 8, LabelText, false);
            Register(KeyCopGapBottom, copGapLabel, copGapLines);

            // CopOffsetLeft: horizontal dim from the wall left edge to the COP
            // left edge, drawn just below the COP.
            double offsetDimY = copBottom + 6;
            Line[] copOffsetLines = AddHDimLine(wallLeft, copLeft, offsetDimY, DimStroke);
            TextBlock copOffsetLabel = AddCenteredLabel((wallLeft + copLeft) / 2, offsetDimY + 9, "From Left", 8, LabelText);
            Register(KeyCopOffsetLeft, copOffsetLabel, copOffsetLines);
        }

        private void BuildRear()
        {
            const double wallTop = 60;
            const double wallBottom = 210;
            const double wallLeft = 45;
            const double wallRight = 215;
            double wallWidth = wallRight - wallLeft;

            Rectangle wall = AddRect(wallLeft, wallTop, wallWidth, wallBottom - wallTop, WallStroke, WallFill);
            AddCenteredLabel((wallLeft + wallRight) / 2, (wallTop + wallBottom) / 2 - 8, "REAR", 10, LabelText);
            AddCenteredLabel((wallLeft + wallRight) / 2, (wallTop + wallBottom) / 2 + 8, "WALL", 10, LabelText);

            // Five bottom-edge segments (no COP on a rear wall).
            double tab = wallWidth * 0.10;
            double interior = (wallWidth - 2 * tab) / 3.0;
            string[] order = { KeyDrLeft, KeySeg1, KeySeg2, KeySeg3, KeyDrRight };
            double[] widths = { tab, interior, interior, interior, tab };
            string[] captions = { "DR-L", "S1", "S2", "S3", "DR-R" };

            double segDimY = wallBottom + 14;
            double x = wallLeft;
            for (int i = 0; i < order.Length; i++)
            {
                double segW = widths[i];
                if (i > 0)
                {
                    Line sep = new Line
                    {
                        X1 = x, X2 = x, Y1 = wallTop, Y2 = wallBottom,
                        Stroke = DimStroke, StrokeThickness = 0.6,
                    };
                    DrawCanvas.Children.Add(sep);
                }
                Line[] segLines = AddHDimLine(x, x + segW, segDimY, DimStroke);
                TextBlock segLabel = AddCenteredLabel(x + segW / 2, segDimY + 10, captions[i], 9, LabelText);
                Register(order[i], segLabel, segLines);
                x += segW;
            }

            Line[] heightLines = AddVDimLine(wallLeft - 16, wallTop, wallBottom, DimStroke);
            TextBlock heightLabel = AddRotatedLabel(wallLeft - 26, (wallTop + wallBottom) / 2, "Meas. Height", 9, LabelText, true);
            Register(KeyMeasuredHeight, heightLabel, Combine(wall, heightLines));
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
