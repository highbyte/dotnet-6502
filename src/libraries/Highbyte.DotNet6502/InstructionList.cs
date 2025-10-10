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

        var instrucionList = new InstructionList(insList);

        // Run verification to ensure the manual list above (won't work when publised in AOT release mode)
#if DEBUG
        var insListVerification = GetAllInstructionDynamcic();
        if (instrucionList.InstructionDictionary.Count != insListVerification.InstructionDictionary.Count)
            throw new Exception("Instruction list in InstructionList.GetAllInstructions() is not up to date. It must include all Instruction implementations.");
#endif

        return instrucionList;
    }

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
        }
        return new InstructionList(instructions);
    }
}
