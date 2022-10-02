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
            if (_inputHandler != null)
                _inputHandler.ProcessInput(System);

            bool executeOk = System.ExecuteOneFrame();
            if (!executeOk)
                return false;

            if (_renderer != null)
                _renderer.Draw(System);
            return true;
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