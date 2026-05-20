using System;
using System.IO;
using System.Windows;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    // Diagnostic command. The previous run proved EntityHelper.SetLayer
    // crashes the host when called on a freshly-inserted entity. This run
    // tests the alternative pattern from the SDK Boolean sample: change the
    // active layer BEFORE inserting, so the new entity is created on the
    // target layer directly, then restore the previous active layer. No
    // SetLayer call is needed.
    //
    // Logs to %LocalAppData%\CanvasCovers\layertest.log, MessageBox before
    // each step. Invoke from the DraftSight command line:
    //     CANVASCOVERSLAYERTEST
    public class LayerTestCommand : CommandBase
    {
        private const string TestLayerName = "CC-LayerTest";

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanvasCovers",
            "layertest.log");

        public LayerTestCommand(DsApplication application, string groupName)
            : base(application, groupName)
        {
        }

        protected override string GlobalName => "_CANVASCOVERSLAYERTEST";

        protected override string LocalName => "CANVASCOVERSLAYERTEST";

        protected override string Description => "Diagnostic: tests activate-based layer assignment.";

        protected override string ItemName => "CanvasCovers Layer Test";

        public override void Execute()
        {
            ResetLog();
            Log("=== LayerTest run started (activate-based) ===");

            try
            {
                if (!Prompt("Step 1: Application.GetActiveDocument().")) { Log("cancelled at 1."); return; }
                Log("Step 1: calling GetActiveDocument ...");
                Document document = Application.GetActiveDocument();
                Log("Step 1: returned " + (document == null ? "null" : "non-null"));
                if (document == null) { Fail("No active document. Open a drawing first."); return; }

                if (!Prompt("Step 2: Document.GetLayerManager().")) { Log("cancelled at 2."); return; }
                Log("Step 2: calling GetLayerManager ...");
                LayerManager layerManager = document.GetLayerManager();
                Log("Step 2: returned " + (layerManager == null ? "null" : "non-null"));
                if (layerManager == null) { Fail("GetLayerManager returned null."); return; }

                if (!Prompt("Step 3: LayerManager.GetActiveLayer() to remember current layer.")) { Log("cancelled at 3."); return; }
                Log("Step 3: calling GetActiveLayer ...");
                Layer originalActive = layerManager.GetActiveLayer();
                string originalName = originalActive == null ? "<null>" : originalActive.Name;
                Log("Step 3: returned " + (originalActive == null ? "null" : "layer '" + originalName + "'"));

                if (!Prompt("Step 4: LayerManager.CreateLayer(\"" + TestLayerName + "\").")) { Log("cancelled at 4."); return; }
                Log("Step 4: calling CreateLayer ...");
                Layer newLayer;
                dsCreateObjectResult_e createResult;
                layerManager.CreateLayer(TestLayerName, out newLayer, out createResult);
                Log("Step 4: CreateLayer result=" + createResult + ", layer=" + (newLayer == null ? "null" : "non-null"));

                // If the layer already existed, fetch it explicitly.
                if (newLayer == null)
                {
                    Log("Step 4b: layer was null, calling GetLayer ...");
                    newLayer = layerManager.GetLayer(TestLayerName);
                    Log("Step 4b: GetLayer returned " + (newLayer == null ? "null" : "non-null"));
                    if (newLayer == null) { Fail("Could not obtain layer after CreateLayer."); return; }
                }

                if (!Prompt("Step 5: Document.GetModel() and Model.GetSketchManager().")) { Log("cancelled at 5."); return; }
                Log("Step 5: calling GetModel + GetSketchManager ...");
                Model model = document.GetModel();
                if (model == null) { Fail("GetModel returned null."); return; }
                SketchManager sketch = model.GetSketchManager();
                if (sketch == null) { Fail("GetSketchManager returned null."); return; }
                Log("Step 5: both returned non-null");

                if (!Prompt("Step 6: newLayer.Activate()  --  this is the call we're testing.")) { Log("cancelled at 6."); return; }
                Log("Step 6: calling newLayer.Activate ...");
                bool activatedNew = newLayer.Activate();
                Log("Step 6: returned " + activatedNew);

                if (!Prompt("Step 7: InsertPolyline2D (should land on " + TestLayerName + ").")) { Log("cancelled at 7."); return; }
                Log("Step 7: calling InsertPolyline2D ...");
                PolyLine polyline = sketch.InsertPolyline2D(
                    new[] { 0.0, 0.0, 50.0, 0.0, 50.0, 30.0, 0.0, 30.0 },
                    true);
                Log("Step 7: returned " + (polyline == null ? "null" : "non-null"));
                if (polyline == null) { Fail("InsertPolyline2D returned null."); return; }

                if (!Prompt("Step 8: restore original active layer ('" + originalName + "').")) { Log("cancelled at 8."); return; }
                Log("Step 8: restoring original active layer ...");
                if (originalActive != null)
                {
                    bool restored = originalActive.Activate();
                    Log("Step 8: originalActive.Activate returned " + restored);
                }
                else
                {
                    Log("Step 8: no original active layer to restore.");
                }

                // NOTE: we intentionally do NOT call EntityHelper.GetLayer
                // here. It crashes the host when called on a freshly-inserted
                // entity (same failure mode as EntityHelper.SetLayer). See
                // CLAUDE.md §9. Verify layer assignment visually by opening
                // the Layer Manager and confirming the polyline sits on
                // 'CC-LayerTest'.

                Log("=== SUCCESS — activate-based layer assignment works. ===");
                MessageBox.Show(
                    "Layer test SUCCESS. The polyline at the world origin should be on '" + TestLayerName + "'.\n\n"
                    + "Verify by opening Layer Manager (type LAYER) — the layer should exist, and the small rectangle at the world origin should be on it.\n\nLog: "
                    + LogPath,
                    "CanvasCovers Layer Test");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex.GetType().FullName + ": " + ex.Message);
                Log(ex.StackTrace ?? "(no stack trace)");
                MessageBox.Show(
                    "Layer test threw an exception:\n\n" + ex.Message + "\n\nLog: " + LogPath,
                    "CanvasCovers Layer Test");
            }
        }

        private static bool Prompt(string message)
        {
            MessageBoxResult result = MessageBox.Show(
                message + "\n\nClick OK to proceed, Cancel to abort.",
                "CanvasCovers Layer Test",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            return result == MessageBoxResult.OK;
        }

        private static void Fail(string reason)
        {
            Log("FAIL: " + reason);
            MessageBox.Show("Layer test failed: " + reason + "\n\nLog: " + LogPath, "CanvasCovers Layer Test");
        }

        private static void ResetLog()
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(LogPath, string.Empty);
            }
            catch
            {
                // best effort
            }
        }

        private static void Log(string line)
        {
            try
            {
                string stamped = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine;
                File.AppendAllText(LogPath, stamped);
            }
            catch
            {
                // best effort
            }
        }
    }
}
