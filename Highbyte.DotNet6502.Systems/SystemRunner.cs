namespace Highbyte.DotNet6502.Systems
{
    public class SystemRunner
    {
        private readonly ISystem _system;
        private IRenderer _renderer;
        private IInputHandler _inputHandler;

        public ISystem System => _system;
        public IRenderer Renderer { get => _renderer; set => _renderer = value; }
        public IInputHandler InputHandler { get => _inputHandler; set => _inputHandler = value; }

        public SystemRunner(ISystem system)
        {
            _system = system;
        }

        public SystemRunner(ISystem system, IRenderer renderer, IInputHandler inputHandler)
        {
            _system = system;
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
                _inputHandler.ProcessInput(_system);
        }

        public bool RunEmulatorOneFrame()
        {
            bool executeOk = _system.ExecuteOneFrame();
            if (!executeOk)
                return false;
            return true;
        }

        public void Draw()
        {
            if (_renderer != null)
                _renderer.Draw(_system);
        }
    }

    public class SystemRunner<TSystem, TRenderContext> : SystemRunner
        where TSystem : ISystem
        where TRenderContext: IRenderContext
    {
        public SystemRunner(
            TSystem system) : base(system)
        {
        }
        public SystemRunner(
            TSystem system,
            IRenderer<TSystem, TRenderContext> renderer,
            IInputHandler<TSystem> inputHandler
            ) : base(system, renderer, inputHandler)
        {
        }
    }
}