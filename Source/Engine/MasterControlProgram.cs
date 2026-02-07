using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal enum ProgramState
    {
        None = 0,
        Init = 2,
        TitleScreen = 5,
        MainMenu = 8,
        Game = 11
    }

    internal sealed class MasterControlProgram : IMasterControlProgram
    {
        private ProgramState _currentState = ProgramState.Init;
        private ProgramState? _wantedState;
        private bool _forceRestart;

        private readonly IGameStateHandler _game;
        private readonly IMainMenuHandler _mainMenu;
        private readonly ITitleScreenHandler _titleScreen;

        internal MasterControlProgram(IGameStateHandler game, IMainMenuHandler mainMenu, ITitleScreenHandler titleScreen)
        {
            _game = game;
            _mainMenu = mainMenu;
            _titleScreen = titleScreen;
        }

        public bool Update()
        {
            // Apply any queued state transitions first.
            ExecuteWantedStateChange();

            // Run one tick of the current state.
            // IMPORTANT: This assumes your handlers expose a Tick/Update returning bool.
            // If they are void, adjust accordingly.
            switch (_currentState)
            {
                case ProgramState.Init:
                    // Example: immediately go to TitleScreen (or whatever your init does)
                    RequestStateChange(ProgramState.TitleScreen);
                    return true;

                case ProgramState.TitleScreen:
                    return _titleScreen.Update();

                case ProgramState.MainMenu:
                    return _mainMenu.Update();

                case ProgramState.Game:
                    return _game.Update();

                default:
                    return true;
            }
        }

        internal void RequestStateChange(ProgramState newState, bool forceRestart = false)
        {
            _wantedState = newState;
            _forceRestart = forceRestart;
        }

        internal void ExecuteWantedStateChange()
        {
            if (_wantedState == null)
            {
                return;
            }

            ProgramState wanted = _wantedState.Value;
            ProgramState current = _currentState;

            bool mustChange = wanted != current || _forceRestart;

            if (mustChange)
            {
                ShutdownState(current);
                StartupState(wanted);
                _currentState = wanted;
            }

            _wantedState = null;
            _forceRestart = false;
        }

        private void ShutdownState(ProgramState state)
        {
            switch (state)
            {
                case ProgramState.Game:
                    _game.Shutdown();
                    break;

                case ProgramState.MainMenu:
                    _mainMenu.Shutdown();
                    break;

                case ProgramState.TitleScreen:
                    _titleScreen.Shutdown();
                    break;
            }
        }

        private void StartupState(ProgramState state)
        {
            switch (state)
            {
                case ProgramState.Game:
                    _game.Startup();
                    break;

                case ProgramState.MainMenu:
                    _mainMenu.Startup();
                    break;

                case ProgramState.TitleScreen:
                    _titleScreen.Startup();
                    break;
            }
        }
    }
}
