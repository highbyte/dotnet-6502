namespace Highbyte.DotNet6502.Systems
{
    public class SystemRunner
    {
        public readonly ISystem System;
        private readonly IRenderer _renderer;
        private readonly IInputHandler _inputHandler;

        public SystemRunner(ISystem system)
        {
            System = system;
        }

        public SystemRunner(ISystem system, IRenderer renderer, IInputHandler inputHandler)
        {
            System = system;
            _renderer = renderer;
            _inputHandler = inputHandler;
        }

        public void Run()
        {
            bool quit = false;
            while (!quit)
            {
                bool executeOk = RunOneFrame();
                if (!executeOk)
                    quit = true;
            }
        }

        public bool RunOneFrame()
        {
            ProcessInput();

            bool executeOk = RunEmulatorOneFrame();
            if (!executeOk)
                return false;

            Draw();

            return true;
        }

        public void ProcessInput()
        {
            if (_inputHandler != null)
                _inputHandler.ProcessInput(System);
        }

        public bool RunEmulatorOneFrame()
        {
            bool executeOk = System.ExecuteOneFrame();
            if (!executeOk)
                return false;
            return true;
        }

        public void Draw()
        {
            if (_renderer != null)
                _renderer.Draw(System);
        }
    }

    public class SystemRunner<TSystem> : SystemRunner where TSystem : ISystem
    {
        public SystemRunner(
            TSystem system) : base(system)
        {
        }
        public SystemRunner(
            TSystem system,
            IRenderer<TSystem> renderer,
            IInputHandler<TSystem> inputHandler
            ) : base(system, renderer, inputHandler)
        {
        }
    }
}