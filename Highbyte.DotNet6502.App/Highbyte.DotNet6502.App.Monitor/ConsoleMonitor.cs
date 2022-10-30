using System;
using System.IO;
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

        public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            if (!File.Exists(fileName))
            {
                WriteOutput($"File not found: {fileName}", MessageSeverity.Error);
                loadedAtAddress = 0;
                fileLength = 0;
                return false;
            }

            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out fileLength,
                forceLoadAddress);
            return true;
        }

        public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            WriteOutput($"Loading file via file picker dialoag not implemented.", MessageSeverity.Warning);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
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
