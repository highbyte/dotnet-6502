using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic
{
    public class C64AspNetInputHandler : IInputHandler<C64, AspNetInputHandlerContext>, IInputHandler
    {
        private AspNetInputHandlerContext _inputHandlerContext;

        public C64AspNetInputHandler()
        {
        }

        public void Init(C64 system, AspNetInputHandlerContext inputHandlerContext)
        {
            _inputHandlerContext = inputHandlerContext;
            _inputHandlerContext.Init();
        }

        public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
        {
            Init((C64)system, (AspNetInputHandlerContext)inputHandlerContext);
        }

        public void ProcessInput(C64 c64)
        {
            CaptureKeyboard(c64);

            _inputHandlerContext.ClearKeys();   // Clear our captured keys so far
        }

        public void ProcessInput(ISystem system)
        {
            ProcessInput((C64)system);
        }

        private void CaptureKeyboard(C64 c64)
        {
        }
    }
}
