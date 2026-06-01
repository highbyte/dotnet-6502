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

    public static InstructionList GetAllInstructions(CpuCompatibilityProfile compatibilityProfile = CpuCompatibilityProfile.ExperimentalUnofficial)
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

            // Illegal / undocumented opcodes
            new JAM(),
            new NOP_Illegal(),
            new LAX(),
            new SAX(),
            new DCP(),
            new ISC(),
            new SLO(),
            new SRE(),
            new RLA(),
            new RRA(),
            new ANC(),
            new ALR(),
            new ARR(),
            new AXS(),
            new LAS(),
        };

        var opCodeDictionary = new Dictionary<byte, OpCode>();
        var instructionDictionary = new Dictionary<byte, Instruction>();
        foreach (var instruction in insList)
        {
            foreach (var opCode in instruction.OpCodes)
            {
                if (!ShouldIncludeOpCode(opCode.Code, compatibilityProfile))
                    continue;

                opCodeDictionary.Add(opCode.Code.ToByte(), opCode);
                instructionDictionary.Add(opCode.CodeRaw, instruction);
            }
        }

        var instructionList = new InstructionList(opCodeDictionary, instructionDictionary);

        // Run verification to ensure the manual list above matches dynamically discovered instructions (won't work when published in AOT release mode)
#if DEBUG
        if (compatibilityProfile == CpuCompatibilityProfile.FullUnofficial)
        {
            var insListVerification = GetAllInstructionDynamcic();
            VerifyInstructionListsMatch(instructionList, insListVerification);
        }
#endif

        return instructionList;
    }

    private static bool ShouldIncludeOpCode(OpCodeId opCodeId, CpuCompatibilityProfile compatibilityProfile)
        => compatibilityProfile >= GetMinimumCompatibilityProfile(opCodeId);

    private static CpuCompatibilityProfile GetMinimumCompatibilityProfile(OpCodeId opCodeId)
        => opCodeId switch
        {
            // Stable undocumented opcodes commonly used on NMOS 6502/6510 machines.
            OpCodeId.NOP_ILL_1A or
            OpCodeId.NOP_ILL_3A or
            OpCodeId.NOP_ILL_5A or
            OpCodeId.NOP_ILL_7A or
            OpCodeId.NOP_ILL_DA or
            OpCodeId.NOP_ILL_FA or
            OpCodeId.NOP_ILL_IMM_80 or
            OpCodeId.NOP_ILL_IMM_82 or
            OpCodeId.NOP_ILL_IMM_89 or
            OpCodeId.NOP_ILL_IMM_C2 or
            OpCodeId.NOP_ILL_IMM_E2 or
            OpCodeId.NOP_ILL_ZP_04 or
            OpCodeId.NOP_ILL_ZP_44 or
            OpCodeId.NOP_ILL_ZP_64 or
            OpCodeId.NOP_ILL_ZP_X_14 or
            OpCodeId.NOP_ILL_ZP_X_34 or
            OpCodeId.NOP_ILL_ZP_X_54 or
            OpCodeId.NOP_ILL_ZP_X_74 or
            OpCodeId.NOP_ILL_ZP_X_D4 or
            OpCodeId.NOP_ILL_ZP_X_F4 or
            OpCodeId.NOP_ILL_ABS or
            OpCodeId.NOP_ILL_ABS_X_1C or
            OpCodeId.NOP_ILL_ABS_X_3C or
            OpCodeId.NOP_ILL_ABS_X_5C or
            OpCodeId.NOP_ILL_ABS_X_7C or
            OpCodeId.NOP_ILL_ABS_X_DC or
            OpCodeId.NOP_ILL_ABS_X_FC or
            OpCodeId.LAX_IX_IND or
            OpCodeId.LAX_ZP or
            OpCodeId.LAX_ABS or
            OpCodeId.LAX_IND_IX or
            OpCodeId.LAX_ZP_Y or
            OpCodeId.LAX_ABS_Y or
            OpCodeId.SAX_IX_IND or
            OpCodeId.SAX_ZP or
            OpCodeId.SAX_ABS or
            OpCodeId.SAX_ZP_Y or
            OpCodeId.DCP_IX_IND or
            OpCodeId.DCP_ZP or
            OpCodeId.DCP_ABS or
            OpCodeId.DCP_IND_IX or
            OpCodeId.DCP_ZP_X or
            OpCodeId.DCP_ABS_Y or
            OpCodeId.DCP_ABS_X or
            OpCodeId.ISC_IX_IND or
            OpCodeId.ISC_ZP or
            OpCodeId.ISC_ABS or
            OpCodeId.ISC_IND_IX or
            OpCodeId.ISC_ZP_X or
            OpCodeId.ISC_ABS_Y or
            OpCodeId.ISC_ABS_X or
            OpCodeId.SLO_IX_IND or
            OpCodeId.SLO_ZP or
            OpCodeId.SLO_ABS or
            OpCodeId.SLO_IND_IX or
            OpCodeId.SLO_ZP_X or
            OpCodeId.SLO_ABS_Y or
            OpCodeId.SLO_ABS_X or
            OpCodeId.SRE_IX_IND or
            OpCodeId.SRE_ZP or
            OpCodeId.SRE_ABS or
            OpCodeId.SRE_IND_IX or
            OpCodeId.SRE_ZP_X or
            OpCodeId.SRE_ABS_Y or
            OpCodeId.SRE_ABS_X or
            OpCodeId.RLA_IX_IND or
            OpCodeId.RLA_ZP or
            OpCodeId.RLA_ABS or
            OpCodeId.RLA_IND_IX or
            OpCodeId.RLA_ZP_X or
            OpCodeId.RLA_ABS_Y or
            OpCodeId.RLA_ABS_X or
            OpCodeId.RRA_IX_IND or
            OpCodeId.RRA_ZP or
            OpCodeId.RRA_ABS or
            OpCodeId.RRA_IND_IX or
            OpCodeId.RRA_ZP_X or
            OpCodeId.RRA_ABS_Y or
            OpCodeId.RRA_ABS_X or
            OpCodeId.ANC_I_0B or
            OpCodeId.ANC_I_2B or
            OpCodeId.ALR_I or
            OpCodeId.AXS_I or
            OpCodeId.SBC_I_EB => CpuCompatibilityProfile.StableUnofficial,

            // Known less reliable but still executable opcodes that are useful for targeted compatibility/testing.
            OpCodeId.ARR_I or
            OpCodeId.LAS_ABS_Y => CpuCompatibilityProfile.ExperimentalUnofficial,

            // Halt-style unofficial opcodes that intentionally jam the CPU until reset.
            OpCodeId.JAM_02 or
            OpCodeId.JAM_12 or
            OpCodeId.JAM_22 or
            OpCodeId.JAM_32 or
            OpCodeId.JAM_42 or
            OpCodeId.JAM_52 or
            OpCodeId.JAM_62 or
            OpCodeId.JAM_72 or
            OpCodeId.JAM_92 or
            OpCodeId.JAM_B2 or
            OpCodeId.JAM_D2 or
            OpCodeId.JAM_F2 => CpuCompatibilityProfile.FullUnofficial,

            _ => CpuCompatibilityProfile.OfficialOnly,
        };

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
                throw new DotNet6502Exception($"Failed to create instance of Instruction type '{instructionType.FullName}'.");
            }
        }
        return new InstructionList(instructions);
    }
}
