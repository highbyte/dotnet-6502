// ----------------------------------------------------------------------------------------------------
// A minimal example of how to load and run a 6502 machine code program.
// This does not involve a complete computer (such as Commodore 64) but only the CPU and memory.
// ----------------------------------------------------------------------------------------------------

using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Utils;

string programFile = "calc_avg.prg";    // Adjust path if necessary

// Create memory (default 64KB)
Memory mem = new();
// Load the machine code program into it. Assume two first bytes in the .prg file is the load address.
mem.Load(programFile, out ushort loadAddress);

// ALT: Create memory (default 64KB) and load the machine code program into it. Assume the .prg file does not contain load address in first two bytes, thus the load address must be specified explicitly.
//ushort loadAddress = 0xc000;
//mem.Load(programFile, out _, out _, forceLoadAddress: loadAddress);

// Init variables in memory locations used by the program.
mem[0xd000] = 64;
mem[0xd001] = 20;
Console.WriteLine($"Input 1 (0xd000) = {mem[0xd000]}");
Console.WriteLine($"Input 2 (0xd001) = {mem[0xd001]}");

// Create the CPU and set program counter (start address).
var cpu = new CPU();
cpu.PC = loadAddress;

// Run program. The 6502 program will run until a BRK instruction is encountered.
cpu.ExecuteUntilBRK(mem);

// Inspect result of program which is stored in memory location 0xd002.
Console.WriteLine($"Output  (0xd002) = {mem[0xd002]}");

