using System;
using System.Runtime.InteropServices;
using System.Windows;
using CanvasCovers.Commands;
using DraftSight.Interop.dsAddin;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers
{
    [Guid("aa497758-3d06-46b7-9f75-7a8f2fffed7c")]
    [ComVisible(true)]
    public class App : DsAddin
    {
        private DsApplication _application;
        private readonly string _addinGuid;
        private OpenCanvasCoversCommand _openCommand;
        private LayerTestCommand _layerTestCommand;
        private bool _uiRegistered;

        public App()
        {
            _addinGuid = GetType().GUID.ToString();
        }

        public bool ConnectToDraftSight(object draftSightApplication, int cookie)
        {
            try
            {
                _application = draftSightApplication as DsApplication;
                if (_application == null)
                {
                    MessageBox.Show(
                        "CanvasCovers failed to load: DraftSight application object was not available.",
                        "CanvasCovers Add-in Error");
                    return false;
                }

                _openCommand = new OpenCanvasCoversCommand(_application, _addinGuid);
                _openCommand.RegisterCommand();
                _openCommand.CreateUserCommand();

                _layerTestCommand = new LayerTestCommand(_application, _addinGuid);
                _layerTestCommand.RegisterCommand();

                RegisterRibbon();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CanvasCovers failed to load: {ex.Message}\n\n{ex.StackTrace}",
                    "CanvasCovers Add-in Error");
                return false;
            }
        }

        public bool DisconnectFromDraftSight()
        {
            try
            {
                if (_uiRegistered && _application != null)
                {
                    _application.RemoveUserInterface(_addinGuid);
                    _uiRegistered = false;
                }

                _application = null;
                _openCommand = null;
                _layerTestCommand = null;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CanvasCovers failed to unload: {ex.Message}\n\n{ex.StackTrace}",
                    "CanvasCovers Add-in Error");
                return false;
            }
        }

        private void RegisterRibbon()
        {
            WorkSpace workspace = GetTargetWorkspace();
            if (workspace == null)
            {
                MessageBox.Show(
                    "CanvasCovers could not find an active workspace for its ribbon tab. The CANVASCOVERSOPEN command is still available from the command line.",
                    "CanvasCovers Add-in Warning");
                return;
            }

            // Crash-recovery: remove any orphan tabs left over from a previous
            // session that didn't disconnect cleanly. Without this, repeated
            // crash-reload cycles accumulate duplicate CanvasCovers tabs.
            RemoveOrphanRibbonItems(workspace);

            int tabPosition = CountExistingTabs(workspace) + 1;

            RibbonTab tab = workspace.AddRibbonTab(
                _addinGuid,
                tabPosition,
                "CanvasCovers",
                "CanvasCovers");

            if (tab == null)
            {
                return;
            }

            RibbonPanel panel = tab.InsertRibbonPanel(_addinGuid, 1, "Tools", "Tools");
            if (panel == null)
            {
                return;
            }

            RibbonRow row = panel.InsertRibbonRow(_addinGuid, "ToolsRow");
            if (row == null)
            {
                return;
            }

            row.InsertRibbonCommandButton(
                _addinGuid,
                dsRibbonButtonStyle_e.dsRibbonButtonStyle_LargeWithText,
                "Canvas Covers",
                _openCommand.UserCommandId);

            _uiRegistered = true;
        }

        private WorkSpace GetTargetWorkspace()
        {
            // Prefer the user's currently active workspace so we don't hijack
            // them onto something different.
            WorkSpace active = _application.GetActiveWorkspace();
            if (active != null)
            {
                return active;
            }

            // Defensive fallback if no active workspace is reported.
            WorkSpace named = _application.GetWorkspace("CAD General")
                ?? _application.GetWorkspace("Drafting and Annotation");
            if (named != null)
            {
                return named;
            }

            WorkSpace created = _application.AddWorkspace("CanvasCovers");
            if (created == null)
            {
                created = _application.GetWorkspace("CanvasCovers");
            }
            if (created != null)
            {
                created.Activate();
            }
            return created;
        }

        private void RemoveOrphanRibbonItems(WorkSpace workspace)
        {
            object tabsObj = workspace.GetRibbonTabs();
            object[] tabs = tabsObj as object[];
            if (tabs == null) return;

            foreach (object tabObj in tabs)
            {
                RibbonTab tab = tabObj as RibbonTab;
                if (tab == null) continue;

                string apiId = SafeGetApiId(tab);
                if (!string.Equals(apiId, _addinGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object panelsObj = tab.GetRibbonPanels();
                object[] panels = panelsObj as object[];
                if (panels != null)
                {
                    foreach (object panelObj in panels)
                    {
                        RibbonPanel panel = panelObj as RibbonPanel;
                        if (panel != null)
                        {
                            try { panel.Remove(); } catch { /* ignore — orphan cleanup is best-effort */ }
                        }
                    }
                }

                try { tab.Remove(); } catch { /* ignore — orphan cleanup is best-effort */ }
            }
        }

        private static string SafeGetApiId(RibbonTab tab)
        {
            try { return tab.GetApiID(); } catch { return null; }
        }

        private static int CountExistingTabs(WorkSpace workspace)
        {
            object tabsObj = workspace.GetRibbonTabs();
            object[] tabs = tabsObj as object[];
            return tabs?.Length ?? 0;
        }
    }
}
