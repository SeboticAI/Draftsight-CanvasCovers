using System;
using System.Collections.Generic;
using System.Globalization;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Lays out three lift-blanket walls horizontally with free-floating
    // text around the outside, mirroring Adelaide Annexe's reference DXF
    // layout:
    //   - Top: a single horizontal worksheet legend on layer 0 (white).
    //   - Right of the walls: project metadata + static worksheet
    //     reference blocks (FIXINGS table, WIDTH/HEIGHT formula, vertical
    //     quilting spacing lookup), all on layer 0.
    //   - Below the walls: width dimensions (one per wall).
    //   - Left of the leftmost wall: a single overall height dimension
    //     (the other walls share the same height, so duplicating would
    //     just stack labels in the gap between walls).
    //   - Above each wall: door-return widths (when non-zero).
    //   - Around the COP cutout: top-offset / height / width dims.
    //
    // Dimensions use SDK-native InsertAlignedDimension so DraftSight
    // treats them as real DIMENSION entities (selectable, editable, ext
    // lines auto-drawn). Aligned (not Linear) — Linear defaults to
    // rotation=0 and projects vertical extension points to zero length.
    //
    // Text is inserted via InsertSimpleNote (single-line TEXT entity)
    // because InsertNoteWithParameters silently produced no visible
    // output in our testing.
    //
    // DIMSCALE is bumped via RunCommand at the start of Generate so the
    // dim text + arrows are readable at lift-blanket scale (drawings
    // span thousands of mm; the default DIMSTYLE ships with text sized
    // for millimetre-scale drawings).
    public class LiftBlanketGenerator
    {
        private const double WallGap = 600;             // gap between adjacent walls
        private const double DimGap = 300;              // gap from wall edge to its dim line
        private const double DimReturnGap = 300;        // gap from wall top to door-return dim line
        private const double DimCopGap = 150;           // gap from COP edge to its dim line
        private const double TopLegendGap = 600;        // gap from tallest wall to top legend
        private const double ProjectInfoLeftGap = 800;  // gap from rightmost wall to info column
        private const double ProjectColWidth = 3000;
        private const double ProjectRowH = 200;         // row height between project info lines

        // Drawing-units text heights. All in mm (drawing units). Tuned so
        // the text is legible at a typical full-wall zoom in DraftSight.
        private const double ProjectTextHeight = 160;
        private const double TopLegendTextHeight = 180;
        private const double CopLabelTextHeight = 100;

        // Multiplier applied to the active dimension style so dim text +
        // arrows aren't unreadably small at lift-blanket scale. Applied via
        // RunCommand at the start of Generate; failure is non-fatal.
        private const string DimScaleValue = "30";

        private readonly DsApplication _application;
        private LayerSettings _layerSettings;

        public LiftBlanketGenerator(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void Generate(LiftBlanketJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            _layerSettings = job.Layers ?? new LayerSettings();

            Document document = _application.GetActiveDocument();
            if (document == null)
            {
                throw new InvalidOperationException(
                    "No active drawing is open. Create or open a drawing before generating.");
            }

            Model model = document.GetModel();
            if (model == null)
            {
                throw new InvalidOperationException("DraftSight did not return a model for the active document.");
            }

            SketchManager sketch = model.GetSketchManager();
            if (sketch == null)
            {
                throw new InvalidOperationException("DraftSight did not return a sketch manager.");
            }

            using (LayerHelper layers = new LayerHelper(document))
            {
                layers.EnsureLayer(_layerSettings.Outline.Name, _layerSettings.Outline.ColorIndex);
                layers.EnsureLayer(_layerSettings.Cop.Name, _layerSettings.Cop.ColorIndex);
                layers.EnsureLayer(_layerSettings.Annotation.Name, _layerSettings.Annotation.ColorIndex);
                layers.EnsureLayer(_layerSettings.Titleblock.Name, _layerSettings.Titleblock.ColorIndex);

                // Scale up the active dimension style so dim text and arrows
                // are legible at lift-blanket scale (drawings span thousands
                // of mm; the default DIMSTYLE ships with millimetre-scale
                // text). Best-effort — if RunCommand isn't available or the
                // command fails, dims still render with their default style.
                try
                {
                    _application.RunCommand(
                        "DIMSCALE\n" + DimScaleValue + "\n",
                        true);
                }
                catch
                {
                    // non-fatal — dim text will be small but everything else
                    // still works
                }

                sketch.StartUndoRecord();
                try
                {
                    double cursorX = 0;
                    double maxRight = 0;
                    double maxTop = 0;

                    // The first wall drawn gets the height dim on its outer
                    // (left) side; subsequent walls skip it to avoid stacking
                    // duplicate "2150" callouts in the gap between walls.
                    bool isFirstWall = true;

                    if (job.LeftWall != null && job.LeftWall.Enabled)
                    {
                        double width = DrawWall(sketch, layers, job.LeftWall, cursorX, 0, doorReturnsOnLeft: true, isLeftmost: isFirstWall);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                        if (job.LeftWall.MainHeight > maxTop) maxTop = job.LeftWall.MainHeight;
                        isFirstWall = false;
                    }

                    if (job.RightWall != null && job.RightWall.Enabled)
                    {
                        double width = DrawWall(sketch, layers, job.RightWall, cursorX, 0, doorReturnsOnLeft: false, isLeftmost: isFirstWall);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                        if (job.RightWall.MainHeight > maxTop) maxTop = job.RightWall.MainHeight;
                        isFirstWall = false;
                    }

                    if (job.RearWall != null && job.RearWall.Enabled && !job.Options.ThroughCar)
                    {
                        double width = DrawWall(sketch, layers, job.RearWall, cursorX, 0, doorReturnsOnLeft: true, isLeftmost: isFirstWall);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                        if (job.RearWall.MainHeight > maxTop) maxTop = job.RearWall.MainHeight;
                        // isFirstWall is no longer read after this block — rear is the last wall.
                    }

                    if (maxRight > 0)
                    {
                        DrawTopLegend(sketch, layers, 0, maxTop + TopLegendGap);
                        double infoX = maxRight + ProjectInfoLeftGap;
                        DrawProjectAnnotations(sketch, layers, job, infoX, maxTop);
                    }
                }
                finally
                {
                    sketch.StopUndoRecord();
                }
            }
        }

        private double DrawWall(
            SketchManager sketch,
            LayerHelper layers,
            WallDimensions wall,
            double originX,
            double originY,
            bool doorReturnsOnLeft,
            bool isLeftmost)
        {
            double mainWidth = wall.MainWidth;
            double height = wall.MainHeight;
            double[] returns = { wall.DoorReturn1, wall.DoorReturn2, wall.DoorReturn3 };
            double totalReturns = wall.DoorReturn1 + wall.DoorReturn2 + wall.DoorReturn3;
            double totalWidth = mainWidth + totalReturns;

            double mainX0 = doorReturnsOnLeft ? originX + totalReturns : originX;

            layers.Activate(_layerSettings.Outline.Name);

            sketch.InsertPolyline2D(
                new[]
                {
                    originX,              originY,
                    originX + totalWidth, originY,
                    originX + totalWidth, originY + height,
                    originX,              originY + height,
                },
                true);

            DrawFoldLines(sketch, layers, originX, originY, height, returns, mainWidth, doorReturnsOnLeft);

            if (wall.CopEnabled)
            {
                DrawCop(sketch, layers, wall, mainX0, originY, height);
            }

            DrawWallDimensions(sketch, layers, wall, originX, originY, mainX0, mainWidth, totalWidth, returns, doorReturnsOnLeft, isLeftmost);

            return totalWidth;
        }

        private void DrawFoldLines(
            SketchManager sketch,
            LayerHelper layers,
            double originX,
            double originY,
            double height,
            double[] returns,
            double mainWidth,
            bool doorReturnsOnLeft)
        {
            layers.Activate(_layerSettings.Outline.Name);

            double topY = originY + height;
            double x = originX;

            if (doorReturnsOnLeft)
            {
                for (int i = 0; i < returns.Length; i++)
                {
                    if (returns[i] > 0)
                    {
                        x += returns[i];
                        sketch.InsertLine(x, originY, 0, x, topY, 0);
                    }
                }
            }
            else
            {
                x = originX + mainWidth;
                if (returns[0] > 0)
                {
                    sketch.InsertLine(x, originY, 0, x, topY, 0);
                    x += returns[0];
                }
                if (returns[1] > 0)
                {
                    sketch.InsertLine(x, originY, 0, x, topY, 0);
                    x += returns[1];
                }
                if (returns[2] > 0)
                {
                    sketch.InsertLine(x, originY, 0, x, topY, 0);
                }
            }
        }

        private void DrawCop(
            SketchManager sketch,
            LayerHelper layers,
            WallDimensions wall,
            double mainX0,
            double originY,
            double height)
        {
            double copWidth = wall.CopWidth;
            double copHeight = wall.CopHeight;
            double copCx = mainX0 + wall.MainWidth / 2.0;
            double copCy = originY + height - wall.CopTopOffset - copHeight / 2.0;

            layers.Activate(_layerSettings.Cop.Name);
            sketch.InsertPolyline2D(
                new[]
                {
                    copCx - copWidth / 2.0, copCy - copHeight / 2.0,
                    copCx + copWidth / 2.0, copCy - copHeight / 2.0,
                    copCx + copWidth / 2.0, copCy + copHeight / 2.0,
                    copCx - copWidth / 2.0, copCy + copHeight / 2.0,
                },
                true);

            layers.Activate(_layerSettings.Annotation.Name);
            SimpleNote copLabel = sketch.InsertSimpleNote(
                copCx, copCy, 0,
                CopLabelTextHeight, 0.0,
                "COP");
            if (copLabel != null)
            {
                // Anchor at text middle/center so (copCx, copCy) ends up
                // visually at the centre of the COP rectangle.
                copLabel.Justify = dsTextJustification_e.dsTextJustification_Middle;
            }
        }

        // Native-dimension wrapper: emits proper DIMENSION entities via
        // InsertLinearDimension so DraftSight treats them as real dims
        // (selectable, editable, ext lines auto-drawn). The orientation
        // (horizontal vs vertical) is inferred from the extension-line
        // coordinates; the dim line point sets the offset distance.
        private void DrawWallDimensions(
            SketchManager sketch,
            LayerHelper layers,
            WallDimensions wall,
            double originX,
            double originY,
            double mainX0,
            double mainWidth,
            double totalWidth,
            double[] returns,
            bool doorReturnsOnLeft,
            bool isLeftmost)
        {
            layers.Activate(_layerSettings.Titleblock.Name);
            double topY = originY + wall.MainHeight;

            // Main width: horizontal dim below the MAIN portion only.
            InsertHDim(sketch, mainX0, mainX0 + mainWidth, originY, originY - DimGap);

            // Main height: only on the leftmost wall, placed on its left
            // (outer) side. Other walls share the same height, and putting
            // each wall's height dim in the gap between adjacent walls just
            // stacks duplicate labels on top of each other.
            if (isLeftmost)
            {
                InsertVDim(sketch,
                    originX,
                    originY, topY,
                    originX - DimGap);
            }

            // Door return widths: one horizontal dim per non-zero return,
            // placed above the wall.
            double rx = doorReturnsOnLeft ? originX : originX + mainWidth;
            for (int i = 0; i < returns.Length; i++)
            {
                if (returns[i] > 0)
                {
                    InsertHDim(sketch, rx, rx + returns[i], topY, topY + DimReturnGap);
                    rx += returns[i];
                }
            }

            // COP dims: around the COP cutout when enabled.
            if (wall.CopEnabled)
            {
                double copCx = mainX0 + mainWidth / 2.0;
                double copLeft = copCx - wall.CopWidth / 2.0;
                double copRight = copCx + wall.CopWidth / 2.0;
                double copTop = topY - wall.CopTopOffset;
                double copBot = copTop - wall.CopHeight;

                // COP width: above the COP rect, inside the wall.
                InsertHDim(sketch, copLeft, copRight, copTop, copTop + DimCopGap);

                // COP height: vertical, on the inboard side.
                double copHeightDimX = doorReturnsOnLeft
                    ? copLeft - DimCopGap
                    : copRight + DimCopGap;
                InsertVDim(sketch,
                    doorReturnsOnLeft ? copLeft : copRight,
                    copBot, copTop, copHeightDimX);

                // COP top offset: vertical, between wall-top and COP-top,
                // opposite side from CopHeight to avoid overlap.
                double copTopOffsetX = doorReturnsOnLeft
                    ? copRight + DimCopGap
                    : copLeft - DimCopGap;
                InsertVDim(sketch,
                    doorReturnsOnLeft ? copRight : copLeft,
                    copTop, topY, copTopOffsetX);
            }
        }

        // Horizontal dim between (x1, y) and (x2, y); dim line sits at y=dimY.
        // Uses InsertAlignedDimension (not InsertLinearDimension), which
        // orients itself along the line between ext1 and ext2 — works
        // identically for horizontal and vertical cases. InsertLinearDimension
        // defaults to rotation=0 and projects both points onto a horizontal
        // axis, so it returns 0 for vertically-stacked extension points.
        private static void InsertHDim(SketchManager sketch, double x1, double x2, double y, double dimY)
        {
            if (Math.Abs(x2 - x1) < 0.5) return;
            double cx = (x1 + x2) / 2.0;
            sketch.InsertAlignedDimension(
                new[] { x1, y, 0.0 },
                new[] { x2, y, 0.0 },
                new[] { cx, dimY, 0.0 },
                string.Empty);
        }

        // Vertical dim between (x, y1) and (x, y2); dim line sits at x=dimX.
        private static void InsertVDim(SketchManager sketch, double x, double y1, double y2, double dimX)
        {
            if (Math.Abs(y2 - y1) < 0.5) return;
            double cy = (y1 + y2) / 2.0;
            sketch.InsertAlignedDimension(
                new[] { x, y1, 0.0 },
                new[] { x, y2, 0.0 },
                new[] { dimX, cy, 0.0 },
                string.Empty);
        }

        // Horizontal legend across the top of the drawing — mirrors the
        // single-line worksheet legend the client's reference DXFs carry
        // above the walls. Static text, same on every drawing.
        private void DrawTopLegend(
            SketchManager sketch,
            LayerHelper layers,
            double originX,
            double baselineY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);
            sketch.InsertSimpleNote(
                originX, baselineY, 0,
                TopLegendTextHeight, 0.0,
                "HEIGHT / WIDTH / RETURNS / V QUILT / H QUILT / COP / TEXT / INFO / STENCIL / SCALE / OTHER");
        }

        // Project metadata + worksheet reference text, placed to the right
        // of the walls. Free-floating single-column lines on the Titleblock
        // layer (defaults to "0" = white), matching the worksheet text
        // scattered around the right side of the client's reference DXFs.
        private void DrawProjectAnnotations(
            SketchManager sketch,
            LayerHelper layers,
            LiftBlanketJob job,
            double originX,
            double topY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);

            string dateStr = job.Project.Date.HasValue
                ? job.Project.Date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "—";

            List<string> lines = new List<string>();

            // Dynamic project info — one "LABEL - VALUE" per line, dash
            // separator like the client's worksheet notes.
            lines.Add("COMPANY - " + Display(job.Project.CompanyName));
            lines.Add("PROJECT NAME - " + Display(job.Project.ProjectName));
            lines.Add("NETWORK NO - " + Display(job.Project.NetworkNumber));
            lines.Add("O / NO - " + Display(job.Project.OrderNumber));
            lines.Add("SALES CONTACT - " + Display(job.Project.SalesContact));
            lines.Add("MOBILE - " + Display(job.Project.Mobile));
            lines.Add("MEASURED BY - " + Display(job.Project.MeasuredBy));
            lines.Add("DATE - " + dateStr);
            lines.Add(string.Empty);

            lines.Add("FIXINGS REQUIRED - " + FixingLabel(job.Options.Fixings).ToUpperInvariant());
            lines.Add("THROUGH CAR - " + YesNo(job.Options.ThroughCar));
            lines.Add("PLASTIC COVER ON COP - " + YesNo(job.Options.PlasticCoverOnCop));

            string notes = (job.Project.Notes ?? string.Empty).Trim();
            if (notes.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("NOTES");
                lines.Add(notes);
            }

            // Static worksheet reference blocks — these are reference text
            // the client bakes into every drawing. They do NOT auto-apply
            // any rules yet (e.g. fixing-allowance math is still manual);
            // they're here to match the client's visual convention.
            lines.Add(string.Empty);
            lines.Add("FIXINGS");
            lines.Add("HOOKS       = -50");
            lines.Add("PRESS STUDS = -40");
            lines.Add("EYELET TG9  = -30");
            lines.Add("EYELET TG7  = -30");
            lines.Add("VELCRO      =   0");

            lines.Add(string.Empty);
            lines.Add("WIDTH  - ADD 10mm");
            lines.Add("HEIGHT = LESS FIXING THEN x2");

            lines.Add(string.Empty);
            lines.Add("VERTICAL QUILTING SPACING");
            lines.Add("4700 = 783");
            lines.Add("4500 = 750");
            lines.Add("4300 = 716");
            lines.Add("4100 = 683");

            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i];
                if (string.IsNullOrEmpty(text)) continue;

                double rowTopY = topY - i * ProjectRowH;
                // Baseline = a small margin above the row bottom. Text extends
                // upward from baseline by ProjectTextHeight, fitting inside
                // ProjectRowH with room to spare.
                double baseline = rowTopY - ProjectRowH + (ProjectRowH - ProjectTextHeight) / 2.0;
                sketch.InsertSimpleNote(
                    originX, baseline, 0,
                    ProjectTextHeight, 0.0,
                    text);
            }
        }

        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "—" : value;
        }

        private static string YesNo(bool flag)
        {
            return flag ? "YES" : "NO";
        }

        private static string FixingLabel(FixingType fixing)
        {
            switch (fixing)
            {
                case FixingType.Velcro: return "Velcro";
                case FixingType.HooksFacingIn: return "Hooks Facing In";
                case FixingType.HooksFacingOut: return "Hooks Facing Out";
                case FixingType.PressStuds: return "Press Studs";
                default: return fixing.ToString();
            }
        }
    }
}
