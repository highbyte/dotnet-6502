using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public static class ExecutionCommands
    {
        public static CommandLineApplication ConfigureExecution(this CommandLineApplication app, MonitorBase monitor)
        {
            app.Command("g", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Change the PC (Program Counter) to the specified address continue execution.";
                cmd.AddName("goto");

                var address = cmd.Argument("address", "Optional address (hex) to start executing code at.");
                address.Validators.Add(new MustBe16BitHexValueValidator());
                var dontStopOnBRK = cmd.Option("--no-brk|-nb", "Prevent execution stop when BRK instruction encountered.", CommandOptionType.NoValue);

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    if (string.IsNullOrEmpty(address.Value))
                        return (int)CommandResult.Continue;

                    monitor.Cpu.PC = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);
                    ExecOptions execOptions;
                    if (dontStopOnBRK.HasValue())
                    {
                        execOptions = new ExecOptions();
                        monitor.WriteOutput($"Will never stop.");
                    }
                    else
                    {
                        execOptions = new ExecOptions
                        {
                            ExecuteUntilInstruction = OpCodeId.BRK,
                        };
                        monitor.WriteOutput($"Will stop on BRK instruction.");
                    }
                    monitor.WriteOutput($"Staring executing code at {monitor.Cpu.PC.ToHex("", lowerCase: true)}");
                    // monitor.SystemRunner.Run();
                    // monitor.WriteOutput($"Stopped at                {monitor.Cpu.PC.ToHex("",lowerCase:true)}");
                    // monitor.WriteOutput($"{OutputGen.GetLastInstructionDisassembly(monitor.Cpu, monitor.Mem)}");
                    return (int)CommandResult.Continue;
                });
            });

            app.Command("z", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Single step through instructions. Optionally execute a specified number of instructions.";

                var inscount = cmd.Argument<ulong>("inscount", "Number of instructions to execute. Defaults to 1.");
                inscount.DefaultValue = 1;

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    //monitor.WriteOutput($"Executing code at {monitor.Cpu.PC.ToHex("",lowerCase:true)} for {inscount.Value} instruction(s).");
                    monitor.Cpu.Execute(monitor.Mem, LegacyExecEvaluator.InstructionCountExecEvaluator(ulong.Parse(inscount.Value)));
                    // Last instruction
                    // monitor.WriteOutput($"{OutputGen.GetLastInstructionDisassembly(monitor.Cpu, monitor.Mem)}");
                    // Next instruction
                    monitor.WriteOutput($"{OutputGen.GetNextInstructionDisassembly(monitor.Cpu, monitor.Mem)}");

                    return (int)CommandResult.Ok;
                });
            });

            return app;
        }
    }
}