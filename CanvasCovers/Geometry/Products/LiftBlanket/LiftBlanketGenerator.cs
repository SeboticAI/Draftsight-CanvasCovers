using System;
using System.Collections.Generic;
using System.Globalization;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Geometry.Products.LiftBlanket
{
    // Walks the WallLayout DTOs produced by LiftBlanketCalculator and emits
    // DraftSight entities. All geometry math lives in the calculator; this
    // class only translates specs into SDK calls, keeping the SDK-risky
    // surface minimal. SDK gotchas (activate-based layers, aligned dims,
    // simple notes, DIMSCALE) follow CLAUDE.md §9.
    public class LiftBlanketGenerator
    {
        private const double WallGap = 715;             // gap between walls (matches reference DXFs)
        private const double DimGap = 300;              // gap from wall edge to its dim line
        private const double TopLegendGap = 600;
        private const double ProjectInfoLeftGap = 800;
        private const double ProjectRowH = 200;
        private const double ProjectTextHeight = 160;
        private const double TopLegendTextHeight = 180;
        private const string DimScaleValue = "30";

        private readonly DsApplication _application;
        private LayerSettings _layerSettings;

        // Counts SDK inserts that returned null (the entity silently failed to
        // draw). After a Generate the caller reads this to warn the operator
        // that some geometry may be missing, rather than shipping a silently
        // incomplete drawing. Reset at the start of each Generate.
        public int FailedInsertCount { get; private set; }

        public LiftBlanketGenerator(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        // Inserts a polyline and records a failure if the SDK returns null.
        private void InsertPolyline(SketchManager sketch, double[] coords, bool closed)
        {
            if (sketch.InsertPolyline2D(coords, closed) == null) FailedInsertCount++;
        }

        public void Generate(LiftBlanketJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            _layerSettings = job.Layers ?? new LayerSettings();
            FailedInsertCount = 0;

            Document document = _application.GetActiveDocument();
            if (document == null)
            {
                throw new InvalidOperationException(
                    "No active drawing is open. Create or open a drawing before generating.");
            }

            Model model = document.GetModel();
            if (model == null) throw new InvalidOperationException("DraftSight did not return a model.");
            SketchManager sketch = model.GetSketchManager();
            if (sketch == null) throw new InvalidOperationException("DraftSight did not return a sketch manager.");

            string projectTag = BuildProjectTag(job.Project);
            var calc = new LiftBlanketCalculator(
                job.Options.FixingAllowanceMm, job.Options.EdgeAllowanceMm);

            using (LayerHelper layers = new LayerHelper(document))
            {
                // Create the cutter's full layer set (all seven) so the DXF
                // always carries the complete set, even layers with no geometry.
                if (_layerSettings.Layers != null)
                {
                    foreach (LayerSetting layer in _layerSettings.Layers)
                    {
                        if (!string.IsNullOrWhiteSpace(layer.Name))
                            layers.EnsureLayer(layer.Name, layer.ColorIndex);
                    }
                }
                // Belt-and-braces: ensure each role's resolved layer exists even
                // if it wasn't in the list (Resolve falls back to a default).
                layers.EnsureLayer(_layerSettings.Outline.Name, _layerSettings.Outline.ColorIndex);
                layers.EnsureLayer(_layerSettings.Cop.Name, _layerSettings.Cop.ColorIndex);
                layers.EnsureLayer(_layerSettings.Annotation.Name, _layerSettings.Annotation.ColorIndex);
                layers.EnsureLayer(_layerSettings.Titleblock.Name, _layerSettings.Titleblock.ColorIndex);

                try { _application.RunCommand("DIMSCALE\n" + DimScaleValue + "\n", true); }
                catch { /* non-fatal */ }

                sketch.StartUndoRecord();
                try
                {
                    double cursorX = 0;
                    double maxRight = 0;
                    double maxTop = 0;
                    bool isFirstWall = true;

                    foreach (var pair in EnumerateWalls(job))
                    {
                        WallLayout layout = calc.LayoutWall(
                            pair.Wall, cursorX, projectTag, pair.Suffix,
                            job.Options.QuiltingEnabled,
                            job.Options.VerticalQuiltingSpacingMm);
                        DrawWall(sketch, layers, layout, isFirstWall);
                        double cutWidth = layout.CutRect.X1 - layout.CutRect.X0;
                        double cutHeight = layout.CutRect.Y1 - layout.CutRect.Y0;
                        cursorX += cutWidth + WallGap;
                        maxRight = cursorX - WallGap;
                        if (cutHeight > maxTop) maxTop = cutHeight;
                        isFirstWall = false;
                    }

                    if (maxRight > 0)
                    {
                        DrawTopLegend(sketch, layers, 0, maxTop + TopLegendGap);
                        DrawProjectAnnotations(sketch, layers, job, maxRight + ProjectInfoLeftGap, maxTop);
                    }
                }
                finally
                {
                    sketch.StopUndoRecord();
                }
            }
        }

        // Draw order across the page: Left, then Rear (B) in the MIDDLE, then
        // Right — so the back wall sits between the two side walls, matching
        // the physical layout. Rear is omitted when Through Car is ticked.
        private struct WallEntry { public WallDimensions Wall; public string Suffix; }

        private static IEnumerable<WallEntry> EnumerateWalls(LiftBlanketJob job)
        {
            if (job.LeftWall != null && job.LeftWall.Enabled)
                yield return new WallEntry { Wall = job.LeftWall, Suffix = "L" };
            if (job.RearWall != null && job.RearWall.Enabled && !job.Options.ThroughCar)
                yield return new WallEntry { Wall = job.RearWall, Suffix = "B" };
            if (job.RightWall != null && job.RightWall.Enabled)
                yield return new WallEntry { Wall = job.RightWall, Suffix = "R" };
        }

        private void DrawWall(SketchManager sketch, LayerHelper layers, WallLayout layout, bool isLeftmost)
        {
            RectSpec r = layout.CutRect;

            // Cut rectangle on the cut/outline layer.
            layers.Activate(_layerSettings.Outline.Name);
            InsertPolyline(sketch,
                new[] { r.X0, r.Y0, r.X1, r.Y0, r.X1, r.Y1, r.X0, r.Y1 }, true);

            // COP rectangle on the draw layer (5 Draw and Text), not the cut
            // layer — it's a draw/score feature, per the reference DXFs.
            if (layout.CopRect.HasValue)
            {
                layers.Activate(_layerSettings.Cop.Name);
                RectSpec c = layout.CopRect.Value;
                InsertPolyline(sketch,
                    new[] { c.X0, c.Y0, c.X1, c.Y0, c.X1, c.Y1, c.X0, c.Y1 }, true);
            }

            // Quilt lines on the draw/score layer (same layer as COP), never
            // the cut layer. Each is a 2-point open polyline.
            if (layout.QuiltLines.Count > 0)
            {
                layers.Activate(_layerSettings.Cop.Name);
                foreach (LineSpec q in layout.QuiltLines)
                {
                    InsertPolyline(sketch, new[] { q.X0, q.Y0, q.X1, q.Y1 }, false);
                }
            }

            // Wall identifier label on the annotation (draw) layer.
            if (layout.IdentifierLabel.HasValue)
            {
                layers.Activate(_layerSettings.Annotation.Name);
                LabelSpec lab = layout.IdentifierLabel.Value;
                SimpleNote note = sketch.InsertSimpleNote(lab.X, lab.Y, 0, lab.Height, 0.0, lab.Text);
                if (note != null) note.Justify = dsTextJustification_e.dsTextJustification_Middle;
                else FailedInsertCount++;
            }

            // Dimensions on the titleblock layer.
            layers.Activate(_layerSettings.Titleblock.Name);

            // Width dim: the calculator emits extension points on the wall
            // bottom edge (LineY = 0). The generator owns the offset, so push
            // the dim line DimGap below the wall.
            foreach (DimSpec d in layout.Dimensions)
            {
                InsertDim(sketch, d.Ext1X, d.Ext1Y, d.Ext2X, d.Ext2Y, d.LineX, d.LineY - DimGap);
            }

            // Height dim on the leftmost wall's outer (left) side only. Other
            // walls share the same height; duplicating would just stack labels
            // in the gap between walls.
            if (isLeftmost)
            {
                InsertDim(sketch, r.X0, r.Y0, r.X0, r.Y1, r.X0 - DimGap, (r.Y0 + r.Y1) / 2.0);
            }
        }

        // Aligned dim between (x1,y1) and (x2,y2); dim line passes through
        // (lineX, lineY). Aligned (not Linear) works for both axes.
        private static void InsertDim(SketchManager sketch, double x1, double y1, double x2, double y2, double lineX, double lineY)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;
            sketch.InsertAlignedDimension(
                new[] { x1, y1, 0.0 },
                new[] { x2, y2, 0.0 },
                new[] { lineX, lineY, 0.0 },
                string.Empty);
        }

        private static string BuildProjectTag(ProjectMetadata project)
        {
            if (project == null) return string.Empty;
            string net = (project.NetworkNumber ?? "").Trim();
            string name = (project.ProjectName ?? "").Trim();
            // Reference DXFs label walls "<net> <name>" e.g. "12346 TEST 12346".
            return (net + " " + name).Trim();
        }

        // ---- Annotation/legend text (free-floating, layer 0), mirroring the
        //      client's reference DXF layout. ----

        private void DrawTopLegend(SketchManager sketch, LayerHelper layers, double originX, double baselineY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);
            sketch.InsertSimpleNote(originX, baselineY, 0, TopLegendTextHeight, 0.0,
                "HEIGHT / WIDTH / RETURNS / V QUILT / H QUILT / COP / TEXT / INFO / STENCIL / SCALE / OTHER");
        }

        private void DrawProjectAnnotations(SketchManager sketch, LayerHelper layers, LiftBlanketJob job, double originX, double topY)
        {
            layers.Activate(_layerSettings.Titleblock.Name);

            string dateStr = job.Project.Date.HasValue
                ? job.Project.Date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "—";

            List<string> lines = new List<string>
            {
                "COMPANY - " + Display(job.Project.CompanyName),
                "PROJECT NAME - " + Display(job.Project.ProjectName),
                "NETWORK NO - " + Display(job.Project.NetworkNumber),
                "O / NO - " + Display(job.Project.OrderNumber),
                "SALES CONTACT - " + Display(job.Project.SalesContact),
                "MOBILE - " + Display(job.Project.Mobile),
                "MEASURED BY - " + Display(job.Project.MeasuredBy),
                "DATE - " + dateStr,
                string.Empty,
                "FIXINGS REQUIRED - " + FixingLabel(job.Options.Fixings).ToUpperInvariant(),
                "FIXING ALLOWANCE - -" + job.Options.FixingAllowanceMm.ToString(CultureInfo.InvariantCulture),
                "THROUGH CAR - " + YesNo(job.Options.ThroughCar),
                "PLASTIC COVER ON COP - " + YesNo(job.Options.PlasticCoverOnCop),
            };

            string notes = (job.Project.Notes ?? string.Empty).Trim();
            if (notes.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("NOTES");
                lines.Add(notes);
            }

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

            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i];
                if (string.IsNullOrEmpty(text)) continue;
                double rowTopY = topY - i * ProjectRowH;
                double baseline = rowTopY - ProjectRowH + (ProjectRowH - ProjectTextHeight) / 2.0;
                sketch.InsertSimpleNote(originX, baseline, 0, ProjectTextHeight, 0.0, text);
            }
        }

        private static string Display(string value) => string.IsNullOrEmpty(value) ? "—" : value;
        private static string YesNo(bool flag) => flag ? "YES" : "NO";

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
