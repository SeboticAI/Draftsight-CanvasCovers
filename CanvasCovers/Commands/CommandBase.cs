using System;
using DraftSight.Interop.dsAutomation;
using DsApplication = DraftSight.Interop.dsAutomation.Application;

namespace CanvasCovers.Commands
{
    public abstract class CommandBase
    {
        private readonly DsApplication _application;
        private readonly string _groupName;
        private Command _command;
        private UserCommand _userCommand;
        private string _userCommandId = string.Empty;

        protected CommandBase(DsApplication application, string groupName)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _groupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
        }

        protected DsApplication Application => _application;

        public string UserCommandId => _userCommandId;

        protected abstract string GlobalName { get; }

        protected abstract string LocalName { get; }

        protected abstract string Description { get; }

        protected abstract string ItemName { get; }

        protected virtual string SmallIconPath => string.Empty;

        protected virtual string LargeIconPath => string.Empty;

        public void RegisterCommand()
        {
            dsCreateCommandError_e error;
            _command = _application.CreateCommand2(
                _groupName,
                GlobalName,
                LocalName,
                out error);

            if (error != dsCreateCommandError_e.dsCreateCommand_Succeeded || _command == null)
            {
                throw new InvalidOperationException($"DraftSight command registration failed: {error}");
            }

            _command.ExecuteNotify += Execute;
        }

        public void CreateUserCommand()
        {
            dsCreateCommandError_e error;
            _userCommand = _application.CreateUserCommand(
                _groupName,
                GlobalName,
                "^C^C" + GlobalName,
                Description,
                SmallIconPath,
                LargeIconPath,
                dsUIState_e.dsUIState_Document,
                out error);

            if (error != dsCreateCommandError_e.dsCreateCommand_Succeeded || _userCommand == null)
            {
                throw new InvalidOperationException($"DraftSight user command registration failed: {error}");
            }

            _userCommandId = _userCommand.GetID();
        }

        public void InsertInto(MenuItem menu, int position)
        {
            if (menu == null)
            {
                throw new InvalidOperationException("DraftSight did not return a menu for CanvasCovers.");
            }

            if (string.IsNullOrEmpty(_userCommandId))
            {
                throw new InvalidOperationException(
                    "CanvasCovers user command must be created before inserting into a menu.");
            }

            menu.InsertMenuItem(
                _groupName,
                dsMenuItemType_e.dsMenuItemType_UserCommand,
                position,
                ItemName,
                _userCommandId);
        }

        public abstract void Execute();
    }
}
