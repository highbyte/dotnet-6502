using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.App.ConsoleMonitor;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic.Config;

NativeConsoleMonitor Monitor;

var mem = new Memory();

var computerBuilder = new GenericComputerBuilder(new GenericComputerConfig { WaitForHostToAcknowledgeFrame = false });
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

var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>(computer);

var systemRunner = systemRunnerBuilder.Build();

var monitorConfig = new MonitorConfig
{
    StopAfterBRKInstruction = true,
    StopAfterUnknownInstruction = true,
    DefaultDirectory = Environment.CurrentDirectory
};

Monitor = new NativeConsoleMonitor(systemRunner, monitorConfig);

Monitor.ShowDescription();
Monitor.WriteOutput("");
Monitor.ShowHelp();

Monitor.WriteOutput("Stop manually by pressing ESC.");
Monitor.WriteOutput("");

bool cont = true;
bool startMonitor = true;
ExecEvaluatorTriggerResult? lastExecEvaluatorTriggerResult = null;
while (cont)
{
    if (startMonitor)
    {
        if (lastExecEvaluatorTriggerResult != null)
            Monitor.ShowInfoAfterBreakTriggerEnabled(lastExecEvaluatorTriggerResult);

        lastExecEvaluatorTriggerResult = null;

        var input = PromptInput();
        if (!string.IsNullOrEmpty(input))
        {
            var commandResult = Monitor.SendCommand(input);
            if (commandResult == CommandResult.Quit)
                cont = false;
            if (commandResult == CommandResult.Continue)
                startMonitor = false;
        }
    }
    else
    {
        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        {
            lastExecEvaluatorTriggerResult = ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.Other, "Manually stopped");
            startMonitor = true;
        }
        else
        {
            lastExecEvaluatorTriggerResult = systemRunner.RunEmulatorOneFrame(out _);
            if (lastExecEvaluatorTriggerResult.Triggered)
            {
                startMonitor = true;
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
}

string? PromptInput()
{
    return Prompt.GetString(">",
        promptColor: ConsoleColor.Gray,
        promptBgColor: ConsoleColor.DarkBlue);

    // Console.Write(">");
    // return Console.ReadLine();
}
