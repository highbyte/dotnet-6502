using System.Collections.Generic;

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

        private IExecEvaluator? _customExecEvaluator;
        public IExecEvaluator? CustomExecEvaluator => _customExecEvaluator;

        public SystemRunner(ISystem system)
        {
            _system = system;
        }

        /// <summary>
        /// Set a ExecEvaluator that is used for when executing the CPU instructions. 
        /// This will be used in addition to what "normally" is used (running for x cycles or instructions).
        /// Useful for setting breakpoints.
        /// </summary>
        /// <param name="execEvaluator"></param>
        public void SetCustomExecEvaluator(IExecEvaluator execEvaluator)
        {
            _customExecEvaluator = execEvaluator;
        }
        public void ClearCustomExecEvaluator()
        {
            _customExecEvaluator = null;
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

        public bool RunOneInstruction()
        {
            bool executeOk = _system.ExecuteOneInstruction();
            if (!executeOk)
                return false;
            return true;
        }

        public void ProcessInput()
        {
            if (_inputHandler != null)
                _inputHandler.ProcessInput(_system);
        }

        public bool RunEmulatorOneFrame()
        {
            bool shouldContinue = _system.ExecuteOneFrame(_customExecEvaluator);
            if (!shouldContinue)
                return false;
            return true;
        }

        public void Draw()
        {
            if (_renderer != null)
                _renderer.Draw(_system);
        }
    }
}