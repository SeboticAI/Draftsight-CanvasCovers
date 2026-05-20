using System;
using System.IO;
using System.Reflection;
using System.Windows;
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models;
using CanvasCovers.UI;
using CanvasCovers.UI.Products.LiftBlanket;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    // Ribbon entry point. Opens the product picker, then dispatches to the
    // appropriate product window + generator based on what the user chose.
    public class OpenCanvasCoversCommand : CommandBase
    {
        public OpenCanvasCoversCommand(DsApplication application, string groupName)
            : base(application, groupName)
        {
        }

        protected override string GlobalName => "_CANVASCOVERSOPEN";

        protected override string LocalName => "CANVASCOVERSOPEN";

        protected override string Description => "Open the Canvas Covers product picker.";

        protected override string ItemName => "Canvas Covers";

        protected override string SmallIconPath => ResolveIconPath("canvascovers_16.png");

        protected override string LargeIconPath => ResolveIconPath("canvascovers_32.png");

        public override void Execute()
        {
            try
            {
                ProductPickerWindow picker = new ProductPickerWindow();
                if (picker.ShowDialog() != true || picker.SelectedProduct == null)
                {
                    return;
                }

                switch (picker.SelectedProduct.Value)
                {
                    case ProductKind.LiftBlanket:
                        RunLiftBlanket();
                        break;
                    case ProductKind.CaravanAnnexe:
                        // The tile is disabled in the picker, but guard
                        // defensively in case it's ever enabled before the
                        // generator exists.
                        MessageBox.Show(
                            "Caravan Annexe support is coming soon.",
                            "CanvasCovers");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "CanvasCovers failed: " + ex.Message + "\n\n" + ex.StackTrace,
                    "CanvasCovers Error");
            }
        }

        private void RunLiftBlanket()
        {
            LiftBlanketWindow window = new LiftBlanketWindow();
            if (window.ShowDialog() != true || window.Job == null)
            {
                return;
            }

            LiftBlanketGenerator generator = new LiftBlanketGenerator(Application);
            generator.Generate(window.Job);
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
