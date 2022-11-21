using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using McMaster.Extensions.CommandLineUtils;
using Highbyte.DotNet6502.App.ConsoleMonitor;
using Highbyte.DotNet6502;

NativeConsoleMonitor Monitor;

var mem = new Memory();

var computerBuilder = new GenericComputerBuilder();
computerBuilder
    .WithCPU()
    //.WithStartAddress()
    .WithMemory(mem);
// .WithInstructionExecutedEventHandler(
//     (s, e) => Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)));
// .WithExecOptions(options =>
// {
// });
var computer = computerBuilder.Build();

var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, NullRenderContext, NullInputHandlerContext>(computer);

var systemRunner = systemRunnerBuilder.Build();

var monitorConfig = new MonitorConfig
{
    DefaultDirectory = Environment.CurrentDirectory
};

Monitor = new NativeConsoleMonitor(systemRunner, monitorConfig);

Monitor.ShowDescription();
Monitor.WriteOutput("");
Monitor.ShowHelp();

bool cont = true;
bool startMonitor = true;
while (cont)
{
    if (startMonitor)
    {
        var input = PromptInput();
        var commandResult = Monitor.SendCommand(input);
        if (commandResult == CommandResult.Quit)
            cont = false;
        if (commandResult == CommandResult.Continue)
            startMonitor = false;
    }
    else
    {
        bool runOk = systemRunner.RunOneInstruction();
        if (runOk != true || systemRunner.System.CPU.ExecState.LastInstructionExecResult.OpCodeByte == (byte)OpCodeId.BRK)
        {
            Monitor.WriteOutput("Execution stopped.");
            startMonitor = true;
        }
        else
        {
            Thread.Sleep(1);
        }
    }
}

string PromptInput()
{
    return Prompt.GetString(">",
        promptColor: ConsoleColor.Gray,
        promptBgColor: ConsoleColor.DarkBlue);

    // Console.Write(">");
    // return Console.ReadLine();
}
