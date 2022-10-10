using System;
using Highbyte.DotNet6502.Monitor;

namespace Highbyte.DotNet6502.App.Monitor
{
    public class ConsoleMonitor : MonitorBase
    {
        public ConsoleMonitor(CPU cpu, Memory mem) : base(cpu, mem)
        {
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, ushort? forceLoadAddress = null)
        {
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out int _,
                forceLoadAddress);
        }

        public override void WriteOutput(string message, MessageSeverity? severity = MessageSeverity.Information)
        {
            switch (severity)
            {
                case MessageSeverity.Information:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(message);
                    break;
                case MessageSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(message);
                    break;
                case MessageSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
