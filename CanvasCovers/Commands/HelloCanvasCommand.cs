using System;
using System.IO;
using System.Reflection;
using System.Windows;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    public class HelloCanvasCommand : CommandBase
    {
        public HelloCanvasCommand(DsApplication application, string groupName)
            : base(application, groupName)
        {
        }

        protected override string GlobalName => "_CANVASCOVERSHELLO";

        protected override string LocalName => "CANVASCOVERSHELLO";

        protected override string Description => "Shows the CanvasCovers hello-world message.";

        protected override string ItemName => "Hello CanvasCovers";

        protected override string SmallIconPath => ResolveIconPath("canvascovers_16.png");

        protected override string LargeIconPath => ResolveIconPath("canvascovers_32.png");

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

        public override void Execute()
        {
            try
            {
                CommandMessage commandLine = Application.GetCommandMessage();
                if (commandLine == null)
                {
                    return;
                }

                commandLine.PrintLine("Hello CanvasCovers");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CanvasCovers command failed: {ex.Message}\n\n{ex.StackTrace}",
                    "CanvasCovers Command Error");
            }
        }
    }
}
