using System;
using System.IO;
using System.Reflection;
using System.Windows;
using CanvasCovers.Geometry;
using CanvasCovers.UI;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    public class GenerateCoverCommand : CommandBase
    {
        public GenerateCoverCommand(DsApplication application, string groupName)
            : base(application, groupName)
        {
        }

        protected override string GlobalName => "_CANVASCOVERSGENERATE";

        protected override string LocalName => "CANVASCOVERSGENERATE";

        protected override string Description => "Open the Canvas Cover dialog and generate a placeholder rectangle.";

        protected override string ItemName => "Generate Canvas Cover";

        protected override string SmallIconPath => ResolveIconPath("canvascovers_16.png");

        protected override string LargeIconPath => ResolveIconPath("canvascovers_32.png");

        public override void Execute()
        {
            try
            {
                CanvasCoverWindow window = new CanvasCoverWindow();
                bool? result = window.ShowDialog();
                if (result != true || window.Job == null)
                {
                    return;
                }

                LiftBlanketGenerator generator = new LiftBlanketGenerator(Application);
                generator.Generate(window.Job);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CanvasCovers could not generate geometry: {ex.Message}\n\n{ex.StackTrace}",
                    "CanvasCovers Generation Error");
            }
        }

        private static string ResolveIconPath(string fileName)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                return string.Empty;
            }

            string candidate = Path.Combine(assemblyDir, "Resources", fileName);
            return File.Exists(candidate) ? candidate : string.Empty;
        }
    }
}
