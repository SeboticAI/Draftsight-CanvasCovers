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
        private HelloCanvasCommand _helloCommand;
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

                _helloCommand = new HelloCanvasCommand(_application, _addinGuid);
                _helloCommand.RegisterCommand();
                _helloCommand.CreateUserCommand();

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
                _helloCommand = null;
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
                    "CanvasCovers could not find or create a workspace for its ribbon tab. The command CANVASCOVERSHELLO is still available from the command line.",
                    "CanvasCovers Add-in Warning");
                return;
            }

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
                "Hello",
                _helloCommand.UserCommandId);

            _uiRegistered = true;
        }

        private WorkSpace GetTargetWorkspace()
        {
            string[] candidateNames =
            {
                "CAD General",
                "Drafting and Annotation",
                "3D Modeling"
            };

            foreach (string name in candidateNames)
            {
                WorkSpace existing = _application.GetWorkspace(name);
                if (existing != null)
                {
                    return existing;
                }
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

        private static int CountExistingTabs(WorkSpace workspace)
        {
            object tabsObj = workspace.GetRibbonTabs();
            object[] tabs = tabsObj as object[];
            return tabs?.Length ?? 0;
        }
    }
}
