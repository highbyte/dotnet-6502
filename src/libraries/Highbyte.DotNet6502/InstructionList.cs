using System.Reflection;

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
        // var insList = new List<Instruction>
        // {
        //     new ADC(),
        //     new AND(),
        // };

        // var asmNames = DependencyContext.Default.GetDefaultAssemblyNames();
        // var assemblysToSearch =  asmNames.Select(Assembly.Load);
        // var typesToSearch = assemblysToSearch.SelectMany(t => t.GetTypes());

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
