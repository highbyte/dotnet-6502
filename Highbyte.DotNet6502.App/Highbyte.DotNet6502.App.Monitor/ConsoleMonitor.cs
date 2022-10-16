using System;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Monitor
{
    public class ConsoleMonitor : MonitorBase
    {
        public ConsoleMonitor(
            SystemRunner systemRunner,
            MonitorConfig monitorConfig
            ) : base(systemRunner, monitorConfig)
        {
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out fileLength,
                forceLoadAddress);
        }

        public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
        {
            BinarySaver.Save(
                Mem,
                fileName,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: addFileHeaderWithLoadAddress);
        }

        public override void WriteOutput(string message)
        {
            WriteOutput(message, MessageSeverity.Information);
        }

        public override void WriteOutput(string message, MessageSeverity severity)
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
