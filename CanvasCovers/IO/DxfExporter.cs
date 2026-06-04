using System;
using CanvasCovers.Models;
using Microsoft.Win32;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.IO
{
    // Pops a Save-As dialog seeded with the network-number default filename
    // and a .dxf filter, then writes the active document to the chosen path
    // as AutoCAD ASCII DXF. Folder is the operator's choice (per client: no
    // fixed output folder). Best-effort: a cancelled dialog is a no-op; a
    // failed save surfaces a message but does not throw.
    public partial class DxfExporter
    {
        private readonly DsApplication _application;

        public DxfExporter(DsApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void Export(ProjectMetadata project)
        {
            Document document = _application.GetActiveDocument();
            if (document == null)
            {
                System.Windows.MessageBox.Show(
                    "No active drawing to export.", "CanvasCovers");
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = DefaultFileName(project),
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                Title = "Export Lift Blanket DXF",
                AddExtension = true,
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog() != true) return;

            dsDocumentSaveError_e errors;
            document.SaveAs(
                dialog.FileName,
                dsDocumentSaveAsOption_e.dsDocumentSaveAs_R2018_ASCII_DXF,
                out errors);

            if (errors != dsDocumentSaveError_e.dsDocumentSave_Succeeded)
            {
                System.Windows.MessageBox.Show(
                    "DXF export reported a problem: " + errors + "\n\n" +
                    "The drawing may not have been saved to:\n" + dialog.FileName,
                    "CanvasCovers");
            }
        }
    }
}
