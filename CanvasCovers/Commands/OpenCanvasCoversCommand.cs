using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using CanvasCovers.Geometry.Products.LiftBlanket;
using CanvasCovers.Models;
using CanvasCovers.Models.Products.LiftBlanket;
using CanvasCovers.UI;
using CanvasCovers.UI.Products.LiftBlanket;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    // Ribbon entry point. Opens the product picker (modal — brief), then
    // launches the chosen product's dialog non-modally so the operator can
    // pan and zoom in DraftSight while the form is open. The dialog raises
    // JobReady when the operator clicks Generate; this command handles that
    // event and runs the matching generator.
    public class OpenCanvasCoversCommand : CommandBase
    {
        // Re-entry guard: if the operator clicks the ribbon button while a
        // dialog is already open, focus the existing window instead of
        // stacking a new one. Static because there's only ever one ribbon
        // command instance per add-in load.
        private static LiftBlanketWindow _openLiftBlanketWindow;

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
                        OpenLiftBlanket();
                        break;
                    case ProductKind.CaravanAnnexe:
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

        private void OpenLiftBlanket()
        {
            // If a dialog is already open from a previous click, bring it
            // forward instead of opening a second one. Two stacked dialogs
            // would each generate a drawing on Generate, which is confusing
            // and wastes the operator's typing.
            if (_openLiftBlanketWindow != null)
            {
                _openLiftBlanketWindow.Activate();
                return;
            }

            LiftBlanketWindow window = new LiftBlanketWindow();
            _openLiftBlanketWindow = window;

            window.GenerateRequested += LiftBlanketWindow_GenerateRequested;
            window.Closed += LiftBlanketWindow_Closed;

            // Parent the dialog to DraftSight's main HWND so it stays z-order
            // associated with the host app (alt-tab cycles them together,
            // dialog stays above DraftSight when both are foregrounded, etc.).
            // Process.MainWindowHandle is the DraftSight main window since
            // this add-in runs in-process.
            IntPtr hostHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (hostHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = hostHandle;
            }

            window.Show();
        }

        private void LiftBlanketWindow_GenerateRequested(object sender, GenerateRequestedEventArgs e)
        {
            // Runs on the dialog's UI thread (= the add-in's COM callback
            // thread = DraftSight's main UI thread, since WPF dialog and
            // host live on the same STA). Safe to call into the SDK.
            try
            {
                LiftBlanketGenerator generator = new LiftBlanketGenerator(Application);
                generator.Generate(e.Job);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "CanvasCovers failed to generate the drawing: " + ex.Message
                        + "\n\n" + ex.StackTrace,
                    "CanvasCovers Error");
                // Leave the dialog open so the operator can adjust inputs
                // (e.g. open a drawing first) and click Generate again.
                e.Cancel = true;
            }
        }

        private void LiftBlanketWindow_Closed(object sender, EventArgs e)
        {
            // Allow the next ribbon click to open a fresh dialog. Unsubscribe
            // so the closed-then-GC'd window doesn't keep this command alive.
            LiftBlanketWindow window = sender as LiftBlanketWindow;
            if (window != null)
            {
                window.GenerateRequested -= LiftBlanketWindow_GenerateRequested;
                window.Closed -= LiftBlanketWindow_Closed;
            }
            if (ReferenceEquals(_openLiftBlanketWindow, window))
            {
                _openLiftBlanketWindow = null;
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
