using System.Reflection;
using Highbyte.DotNet6502.Instructions;

namespace Highbyte.DotNet6502;

/// <summary>
/// </summary>
public class InstructionList
{
    public Dictionary<byte, OpCode> OpCodeDictionary { get; private set; }
    public Dictionary<byte, Instruction> InstructionDictionary { get; private set; }

    public InstructionList(Dictionary<byte, OpCode> opCodeDictionary, Dictionary<byte, Instruction> instructionDictionary)
    {
        OpCodeDictionary = opCodeDictionary;
        InstructionDictionary = instructionDictionary;
    }

    public InstructionList(List<Instruction> insList)
    {
        InstructionDictionary = new Dictionary<byte, Instruction>();
        OpCodeDictionary = new Dictionary<byte, OpCode>();
        foreach (var instruction in insList)
        {
            foreach (var opCode in instruction.OpCodes)
            {
                OpCodeDictionary.Add(opCode.Code.ToByte(), opCode);
                InstructionDictionary.Add(opCode.CodeRaw, instruction);
            }
        }
    }

    public OpCode GetOpCode(byte opCode)
    {
        return OpCodeDictionary[opCode];
    }

    public Instruction GetInstruction(OpCode opCodeObject)
    {
        return InstructionDictionary[opCodeObject.CodeRaw];
    }

    public InstructionList Clone()
    {
        return new InstructionList(this.OpCodeDictionary, this.InstructionDictionary);
    }

    public static InstructionList GetAllInstructions()
    {
        // Manual (AOT & trimming safe) list of all instruction implementations.
        // Add new instruction types here when introduced.
        var insList = new List<Instruction>
        {
            new ADC(),
            new AND(),
            new ASL(),
            new BCC(),
            new BCS(),
            new BEQ(),
            new BIT(),
            new BMI(),
            new BNE(),
            new BPL(),
            new BRK(),
            new BVC(),
            new BVS(),
            new CLC(),
            new CLD(),
            new CLI(),
            new CLV(),
            new CMP(),
            new CPX(),
            new CPY(),
            new DEC(),
            new DEX(),
            new DEY(),
            new EOR(),
            new INC(),
            new INX(),
            new INY(),
            new JMP(),
            new JSR(),
            new LDA(),
            new LDX(),
            new LDY(),
            new LSR(),
            new NOP(),
            new ORA(),
            new PHA(),
            new PHP(),
            new PLA(),
            new PLP(),
            new ROL(),
            new ROR(),
            new RTI(),
            new RTS(),
            new SBC(),
            new SEC(),
            new SED(),
            new SEI(),
            new STA(),
            new STX(),
            new STY(),
            new TAX(),
            new TAY(),
            new TSX(),
            new TXA(),
            new TXS(),
            new TYA(),
        };

        var instructionList = new InstructionList(insList);

        // Run verification to ensure the manual list above matches dynamically discovered instructions (won't work when published in AOT release mode)
#if DEBUG
        var insListVerification = GetAllInstructionDynamcic();
        VerifyInstructionListsMatch(instructionList, insListVerification);
#endif

        return instructionList;
    }

#if DEBUG
    /// <summary>
    /// Verifies that two instruction lists have the same count of instructions.
    /// Used internally to ensure the manual instruction list matches dynamic discovery.
    /// </summary>
    internal static void VerifyInstructionListsMatch(InstructionList manualList, InstructionList dynamicList)
    {
        if (manualList.InstructionDictionary.Count != dynamicList.InstructionDictionary.Count)
            throw new DotNet6502Exception($"Instruction list count mismatch: manual list has {manualList.InstructionDictionary.Count} instructions, but dynamic discovery found {dynamicList.InstructionDictionary.Count} instructions. The manual list in GetAllInstructions() must be updated.");
    }
#endif

    private static InstructionList GetAllInstructionDynamcic()
    {
        var typesToSearch = typeof(InstructionList).Assembly.GetTypes();

        var instructionTypes = typesToSearch.Where(p =>
            p.GetTypeInfo().IsSubclassOf(typeof(Instruction))
            && !p.GetTypeInfo().IsAbstract
        );

        var instructions = new List<Instruction>();
        foreach (var instructionType in instructionTypes)
        {
            Instruction? instruction = (Instruction?)Activator.CreateInstance(instructionType);
            if (instruction != null)
            {
                instructions.Add(instruction);
            }
            else
            {
                throw new Exception($"Failed to create instance of Instruction type '{instructionType.FullName}'.");
            }
        }
        return new InstructionList(instructions);
    }
}
