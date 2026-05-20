using System;
using System.Collections.Generic;
using System.Globalization;
using CanvasCovers.Models;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

// Layer assignment is intentionally NOT performed here yet. The earlier
// attempt to use LayerManager.CreateLayer + EntityHelper.SetLayer crashed
// DraftSight (likely a COM out-param marshalling mismatch). Once the
// geometry pipeline is verified stable, layers will be reintroduced with
// each call tested individually. For now, everything is drawn on the
// active layer and the user can move entities post-hoc if needed.

namespace CanvasCovers.Geometry
{
    public class LiftBlanketGenerator
    {
        private const double WallGap = 300;
        private const double TitleBlockGap = 500;
        private const double TitleBlockWidth = 1400;
        private const double TitleBlockRowHeight = 80;
        private const double LabelOffset = 120;

        private readonly DsApplication _application;

        public LiftBlanketGenerator(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void Generate(LiftBlanketJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

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

            double cursorX = 0;
            double maxRight = 0;

            if (job.LeftWall != null && job.LeftWall.Enabled)
            {
                double width = DrawWall(sketch, job.LeftWall, cursorX, 0, "LEFT WALL", doorReturnsOnLeft: true);
                cursorX += width + WallGap;
                maxRight = cursorX - WallGap;
            }

            if (job.RightWall != null && job.RightWall.Enabled)
            {
                double width = DrawWall(sketch, job.RightWall, cursorX, 0, "RIGHT WALL", doorReturnsOnLeft: false);
                cursorX += width + WallGap;
                maxRight = cursorX - WallGap;
            }

            if (job.RearWall != null && job.RearWall.Enabled && !job.Options.ThroughCar)
            {
                double width = DrawWall(sketch, job.RearWall, cursorX, 0, "REAR WALL", doorReturnsOnLeft: true);
                cursorX += width + WallGap;
                maxRight = cursorX - WallGap;
            }

            DrawTitleBlock(sketch, job, 0, -TitleBlockGap, maxRight);
        }

        private double DrawWall(
            SketchManager sketch,
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

            sketch.InsertPolyline2D(
                new[]
                {
                    originX,              originY,
                    originX + totalWidth, originY,
                    originX + totalWidth, originY + height,
                    originX,              originY + height,
                },
                true);

            DrawFoldLines(sketch, originX, originY, height, returns, mainWidth, doorReturnsOnLeft);

            if (wall.CopEnabled)
            {
                DrawCop(sketch, wall, mainX0, originY, height);
            }

            DrawWallLabel(sketch, wallLabel, originX, originY, totalWidth);

            return totalWidth;
        }

        private void DrawFoldLines(
            SketchManager sketch,
            double originX,
            double originY,
            double height,
            double[] returns,
            double mainWidth,
            bool doorReturnsOnLeft)
        {
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
            WallDimensions wall,
            double mainX0,
            double originY,
            double height)
        {
            double copWidth = wall.CopWidth;
            double copHeight = wall.CopHeight;
            double copCx = mainX0 + wall.MainWidth / 2.0;
            double copCy = originY + height - wall.CopTopOffset - copHeight / 2.0;

            sketch.InsertPolyline2D(
                new[]
                {
                    copCx - copWidth / 2.0, copCy - copHeight / 2.0,
                    copCx + copWidth / 2.0, copCy - copHeight / 2.0,
                    copCx + copWidth / 2.0, copCy + copHeight / 2.0,
                    copCx - copWidth / 2.0, copCy + copHeight / 2.0,
                },
                true);

            sketch.InsertNote(
                copCx - copWidth / 4.0, copCy - 30, 0,
                copCx + copWidth / 4.0, copCy + 30, 0,
                new[] { "COP" });
        }

        private void DrawWallLabel(
            SketchManager sketch,
            string label,
            double originX,
            double originY,
            double totalWidth)
        {
            double labelHalfWidth = Math.Max(totalWidth * 0.3, 200);
            double centerX = originX + totalWidth / 2.0;

            sketch.InsertNote(
                centerX - labelHalfWidth, originY - LabelOffset - 60, 0,
                centerX + labelHalfWidth, originY - LabelOffset, 0,
                new[] { label });
        }

        private void DrawTitleBlock(
            SketchManager sketch,
            LiftBlanketJob job,
            double originX,
            double topY,
            double availableWidth)
        {
            double width = Math.Max(availableWidth, TitleBlockWidth);
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
                        ? job.Project.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : "—"),
                "Fixings:         " + FixingLabel(job.Options.Fixings)
                    + "        Through Car:    " + YesNo(job.Options.ThroughCar)
                    + "        Plastic Cover on COP: " + YesNo(job.Options.PlasticCoverOnCop),
            };

            double totalHeight = rowH * rows.Count;
            double bottomY = topY - totalHeight;

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
