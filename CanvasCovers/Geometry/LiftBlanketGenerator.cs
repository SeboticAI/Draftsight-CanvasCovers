using System;
using System.Collections.Generic;
using System.Globalization;
using CanvasCovers.Models;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Geometry
{
    // Lays out three lift-blanket walls horizontally with a title block
    // underneath. Each entity is placed on a named layer via the activate
    // pattern (LayerHelper) so the CAM software downstream can pick which
    // lines to cut, score, or ignore by layer. Layer names and colours come
    // from job.Layers (LayerSettings) so the operator can override them
    // from the dialog if their machine expects a different convention.
    public class LiftBlanketGenerator
    {
        private const double WallGap = 300;
        private const double TitleBlockGap = 500;
        private const double TitleBlockMinWidth = 1400;
        private const double TitleBlockMaxWidth = 2400;
        private const double TitleBlockRowHeight = 80;
        private const double LabelOffset = 120;

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

            // Layer creation is outside the undo record on purpose: layers are
            // a document-level resource we want to persist across Ctrl+Z. The
            // entity inserts go inside the undo record so one Ctrl+Z reverts
            // them as a single group.
            using (LayerHelper layers = new LayerHelper(document))
            {
                layers.EnsureLayer(_layerSettings.Outline.Name, _layerSettings.Outline.ColorIndex);
                layers.EnsureLayer(_layerSettings.Cop.Name, _layerSettings.Cop.ColorIndex);
                layers.EnsureLayer(_layerSettings.Annotation.Name, _layerSettings.Annotation.ColorIndex);
                layers.EnsureLayer(_layerSettings.Titleblock.Name, _layerSettings.Titleblock.ColorIndex);

                sketch.StartUndoRecord();
                try
                {
                    double cursorX = 0;
                    double maxRight = 0;

                    if (job.LeftWall != null && job.LeftWall.Enabled)
                    {
                        double width = DrawWall(sketch, layers, job.LeftWall, cursorX, 0, "LEFT WALL", doorReturnsOnLeft: true);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                    }

                    if (job.RightWall != null && job.RightWall.Enabled)
                    {
                        double width = DrawWall(sketch, layers, job.RightWall, cursorX, 0, "RIGHT WALL", doorReturnsOnLeft: false);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                    }

                    if (job.RearWall != null && job.RearWall.Enabled && !job.Options.ThroughCar)
                    {
                        double width = DrawWall(sketch, layers, job.RearWall, cursorX, 0, "REAR WALL", doorReturnsOnLeft: true);
                        cursorX += width + WallGap;
                        maxRight = cursorX - WallGap;
                    }

                    DrawTitleBlock(sketch, layers, job, 0, -TitleBlockGap, maxRight);
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
            string wallLabel,
            bool doorReturnsOnLeft)
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

            DrawWallLabel(sketch, layers, wallLabel, originX, originY, totalWidth);

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
            sketch.InsertNote(
                copCx - copWidth / 4.0, copCy - 30, 0,
                copCx + copWidth / 4.0, copCy + 30, 0,
                new[] { "COP" });
        }

        private void DrawWallLabel(
            SketchManager sketch,
            LayerHelper layers,
            string label,
            double originX,
            double originY,
            double totalWidth)
        {
            double labelHalfWidth = Math.Max(totalWidth * 0.3, 200);
            double centerX = originX + totalWidth / 2.0;

            layers.Activate(_layerSettings.Annotation.Name);
            sketch.InsertNote(
                centerX - labelHalfWidth, originY - LabelOffset - 60, 0,
                centerX + labelHalfWidth, originY - LabelOffset, 0,
                new[] { label });
        }

        private void DrawTitleBlock(
            SketchManager sketch,
            LayerHelper layers,
            LiftBlanketJob job,
            double originX,
            double topY,
            double availableWidth)
        {
            double width = Math.Min(Math.Max(availableWidth, TitleBlockMinWidth), TitleBlockMaxWidth);
            double rowH = TitleBlockRowHeight;

            List<string> rows = new List<string>
            {
                "ADELAIDE ANNEXES & CANVAS  -  LIFT BLANKET",
                "Company Name:    " + Display(job.Project.CompanyName)
                    + "        Network Number: " + Display(job.Project.NetworkNumber),
                "Project Name:    " + Display(job.Project.ProjectName)
                    + "        O/Number:       " + Display(job.Project.OrderNumber),
                "Sales Contact:   " + Display(job.Project.SalesContact)
                    + "        Mobile:         " + Display(job.Project.Mobile),
                "Measured By:     " + Display(job.Project.MeasuredBy)
                    + "        Date:           " + (job.Project.Date.HasValue
                        ? job.Project.Date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                        : "—"),
                "Fixings:         " + FixingLabel(job.Options.Fixings)
                    + "        Through Car:    " + YesNo(job.Options.ThroughCar)
                    + "        Plastic Cover on COP: " + YesNo(job.Options.PlasticCoverOnCop),
            };

            double totalHeight = rowH * rows.Count;
            double bottomY = topY - totalHeight;

            layers.Activate(_layerSettings.Titleblock.Name);

            sketch.InsertPolyline2D(
                new[]
                {
                    originX,         bottomY,
                    originX + width, bottomY,
                    originX + width, topY,
                    originX,         topY,
                },
                true);

            for (int i = 0; i < rows.Count; i++)
            {
                double rowTop = topY - i * rowH;
                double rowBottom = rowTop - rowH;

                if (i < rows.Count - 1)
                {
                    sketch.InsertLine(originX, rowBottom, 0, originX + width, rowBottom, 0);
                }

                sketch.InsertNote(
                    originX + 20, rowBottom + 12, 0,
                    originX + width - 20, rowTop - 12, 0,
                    new[] { rows[i] });
            }
        }

        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "—" : value;
        }

        private static string YesNo(bool flag)
        {
            return flag ? "Yes" : "No";
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
