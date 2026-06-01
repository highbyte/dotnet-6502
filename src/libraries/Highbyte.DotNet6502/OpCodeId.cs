 namespace Highbyte.DotNet6502;

public enum OpCodeId: byte
{
    // LDA
    LDA_I = 0xA9,
    LDA_ZP = 0xA5,
    LDA_ZP_X = 0xB5,
    LDA_ABS = 0xAD,
    LDA_ABS_X = 0xBD,
    LDA_ABS_Y = 0xB9,
    LDA_IX_IND = 0xA1,
    LDA_IND_IX = 0xB1,

    // LDX
    LDX_I = 0xA2,
    LDX_ZP = 0xA6,
    LDX_ZP_Y = 0xB6,
    LDX_ABS = 0xAE,
    LDX_ABS_Y = 0xBE,

    // LDY
    LDY_I = 0xA0,
    LDY_ZP = 0xA4,
    LDY_ZP_X = 0xB4,
    LDY_ABS = 0xAC,
    LDY_ABS_X = 0xBC,

    // STA        
    STA_ZP = 0x85,
    STA_ZP_X = 0x95,
    STA_ABS = 0x8D,
    STA_ABS_X = 0x9D,
    STA_ABS_Y = 0x99,
    STA_IX_IND = 0x81,
    STA_IND_IX = 0x91,

    // STX
    STX_ZP = 0x86,
    STX_ZP_Y = 0x96,
    STX_ABS = 0x8E,

    // STY
    STY_ZP = 0x84,
    STY_ZP_X = 0x94,
    STY_ABS = 0x8C,

    
    INC_ZP = 0xE6,
    INC_ZP_X = 0xF6,
    INC_ABS = 0xEE,
    INC_ABS_X = 0xFE,

    // INX
    INX = 0xE8,

    
    INY = 0xC8,

    
    DEC_ZP = 0xC6,
    DEC_ZP_X = 0xD6,
    DEC_ABS = 0xCE,
    DEC_ABS_X = 0xDE,

    // DEX
    DEX = 0xCA,

    // DEY
    DEY = 0x88,


    // JMP
    JMP_ABS = 0x4C,
    JMP_IND = 0x6C,

    // JSR
    JSR = 0x20,

    // RTS
    RTS = 0x60,

    // BEQ
    BEQ = 0xF0,

    // BNE
    BNE = 0xD0,

    // BCC
    BCC = 0x90,

    // BCS
    BCS = 0xB0,

    // BMI
    BMI = 0x30,

    // BPL
    BPL = 0x10,

    // BVC
    BVC = 0x50,

    // BVS
    BVS = 0x70,



    // ADC
    ADC_I = 0x69,
    ADC_ZP = 0x65,
    ADC_ZP_X = 0x75,
    ADC_ABS = 0x6D,
    ADC_ABS_X = 0x7D,
    ADC_ABS_Y = 0x79,
    ADC_IX_IND = 0x61,
    ADC_IND_IX = 0x71,

    // SBC
    SBC_I = 0xE9,
    SBC_ZP = 0xE5,
    SBC_ZP_X = 0xF5,
    SBC_ABS = 0xED,
    SBC_ABS_X = 0xFD,
    SBC_ABS_Y = 0xF9,
    SBC_IX_IND = 0xE1,
    SBC_IND_IX = 0xF1,

    // AND
    AND_I = 0x29,
    AND_ZP = 0x25,
    AND_ZP_X = 0x35,
    AND_ABS = 0x2D,
    AND_ABS_X = 0x3D,
    AND_ABS_Y = 0x39,
    AND_IX_IND = 0x21,
    AND_IND_IX = 0x31,

    // ORA
    ORA_I = 0x09,
    ORA_ZP = 0x05,
    ORA_ZP_X = 0x15,
    ORA_ABS = 0x0D,
    ORA_ABS_X = 0x1D,
    ORA_ABS_Y = 0x19,
    ORA_IX_IND = 0x01,
    ORA_IND_IX = 0x11,

    // EOR
    EOR_I = 0x49,
    EOR_ZP = 0x45,
    EOR_ZP_X = 0x55,
    EOR_ABS = 0x4D,
    EOR_ABS_X = 0x5D,
    EOR_ABS_Y = 0x59,
    EOR_IX_IND = 0x41,
    EOR_IND_IX = 0x51,

    // CMP
    CMP_I = 0xC9,
    CMP_ZP = 0xC5,
    CMP_ZP_X = 0xD5,
    CMP_ABS = 0xCD,
    CMP_ABS_X = 0xDD,
    CMP_ABS_Y = 0xD9,
    CMP_IX_IND = 0xC1,
    CMP_IND_IX = 0xD1,

    // CPX
    CPX_I = 0xE0,
    CPX_ZP = 0xE4,
    CPX_ABS = 0xEC,

    // CPY
    CPY_I = 0xC0,
    CPY_ZP = 0xC4,
    CPY_ABS = 0xCC,        

    // ASL
    ASL_ACC = 0x0A,
    ASL_ZP = 0x06,
    ASL_ZP_X = 0x16,
    ASL_ABS = 0x0E,
    ASL_ABS_X = 0x1E,

    // LSR
    LSR_ACC = 0x4A,
    LSR_ZP = 0x46,
    LSR_ZP_X = 0x56,
    LSR_ABS = 0x4E,
    LSR_ABS_X = 0x5E,

    // ROL
    ROL_ACC = 0x2A,
    ROL_ZP = 0x26,
    ROL_ZP_X = 0x36,
    ROL_ABS = 0x2E,
    ROL_ABS_X = 0x3E,

    // ROR
    ROR_ACC = 0x6A,
    ROR_ZP = 0x66,
    ROR_ZP_X = 0x76,
    ROR_ABS = 0x6E,
    ROR_ABS_X = 0x7E,

    // BIT
    BIT_ZP = 0x24,
    BIT_ABS = 0x2C,

    // TAX
    TAX = 0xAA,

    // TAY
    TAY = 0xA8,

    // TXA
    TXA = 0x8A,

    // TYA
    TYA = 0x98,

    // TSX
    TSX = 0xBA,

    // TXS
    TXS = 0x9A,

    // PHA
    PHA = 0x48,

    // PHP
    PHP = 0x08,

    // PLA
    PLA = 0x68,

    // PLP
    PLP = 0x28,

    // CLC
    CLC = 0x18,

    // CLD
    CLD = 0xD8,

    // CLI
    CLI = 0x58,

    // CLV
    CLV = 0xB8,

    // SEC
    SEC = 0x38,

    // SED
    SED = 0xF8,

    // SEI
    SEI = 0x78,        

    // Misc system instructions
    BRK = 0x00,
    RTI = 0x40,
    NOP = 0xEA,

    // ── Illegal / undocumented opcodes ──────────────────────────────────────

    // JAM / KIL / HLT — halts the CPU until reset
    JAM_02 = 0x02,
    JAM_12 = 0x12,
    JAM_22 = 0x22,
    JAM_32 = 0x32,
    JAM_42 = 0x42,
    JAM_52 = 0x52,
    JAM_62 = 0x62,
    JAM_72 = 0x72,
    JAM_92 = 0x92,
    JAM_B2 = 0xB2,
    JAM_D2 = 0xD2,
    JAM_F2 = 0xF2,

    // Extra implied NOPs (do nothing, 2 cycles each)
    NOP_ILL_1A = 0x1A,
    NOP_ILL_3A = 0x3A,
    NOP_ILL_5A = 0x5A,
    NOP_ILL_7A = 0x7A,
    NOP_ILL_DA = 0xDA,
    NOP_ILL_FA = 0xFA,

    // NOPs that read (and discard) a byte — immediate
    NOP_ILL_IMM_80 = 0x80,
    NOP_ILL_IMM_82 = 0x82,
    NOP_ILL_IMM_89 = 0x89,
    NOP_ILL_IMM_C2 = 0xC2,
    NOP_ILL_IMM_E2 = 0xE2,

    // NOPs that read a zero-page byte
    NOP_ILL_ZP_04 = 0x04,
    NOP_ILL_ZP_44 = 0x44,
    NOP_ILL_ZP_64 = 0x64,

    // NOPs that read a zero-page,X byte
    NOP_ILL_ZP_X_14 = 0x14,
    NOP_ILL_ZP_X_34 = 0x34,
    NOP_ILL_ZP_X_54 = 0x54,
    NOP_ILL_ZP_X_74 = 0x74,
    NOP_ILL_ZP_X_D4 = 0xD4,
    NOP_ILL_ZP_X_F4 = 0xF4,

    // NOP that reads an absolute byte
    NOP_ILL_ABS = 0x0C,

    // NOPs that read an absolute,X byte (+1 cycle on page cross)
    NOP_ILL_ABS_X_1C = 0x1C,
    NOP_ILL_ABS_X_3C = 0x3C,
    NOP_ILL_ABS_X_5C = 0x5C,
    NOP_ILL_ABS_X_7C = 0x7C,
    NOP_ILL_ABS_X_DC = 0xDC,
    NOP_ILL_ABS_X_FC = 0xFC,

    // LAX — load A and X from same address
    LAX_IX_IND = 0xA3,
    LAX_ZP     = 0xA7,
    LAX_ABS    = 0xAF,
    LAX_IND_IX = 0xB3,
    LAX_ZP_Y   = 0xB7,
    LAX_ABS_Y  = 0xBF,

    // SAX — store A AND X
    SAX_IX_IND = 0x83,
    SAX_ZP     = 0x87,
    SAX_ABS    = 0x8F,
    SAX_ZP_Y   = 0x97,

    // DCP — decrement memory then CMP with A
    DCP_IX_IND = 0xC3,
    DCP_ZP     = 0xC7,
    DCP_ABS    = 0xCF,
    DCP_IND_IX = 0xD3,
    DCP_ZP_X   = 0xD7,
    DCP_ABS_Y  = 0xDB,
    DCP_ABS_X  = 0xDF,

    // ISC — increment memory then SBC from A
    ISC_IX_IND = 0xE3,
    ISC_ZP     = 0xE7,
    ISC_ABS    = 0xEF,
    ISC_IND_IX = 0xF3,
    ISC_ZP_X   = 0xF7,
    ISC_ABS_Y  = 0xFB,
    ISC_ABS_X  = 0xFF,

    // SLO — ASL memory then ORA into A
    SLO_IX_IND = 0x03,
    SLO_ZP     = 0x07,
    SLO_ABS    = 0x0F,
    SLO_IND_IX = 0x13,
    SLO_ZP_X   = 0x17,
    SLO_ABS_Y  = 0x1B,
    SLO_ABS_X  = 0x1F,

    // SRE — LSR memory then EOR into A
    SRE_IX_IND = 0x43,
    SRE_ZP     = 0x47,
    SRE_ABS    = 0x4F,
    SRE_IND_IX = 0x53,
    SRE_ZP_X   = 0x57,
    SRE_ABS_Y  = 0x5B,
    SRE_ABS_X  = 0x5F,

    // RLA — ROL memory then AND into A
    RLA_IX_IND = 0x23,
    RLA_ZP     = 0x27,
    RLA_ABS    = 0x2F,
    RLA_IND_IX = 0x33,
    RLA_ZP_X   = 0x37,
    RLA_ABS_Y  = 0x3B,
    RLA_ABS_X  = 0x3F,

    // RRA — ROR memory then ADC into A
    RRA_IX_IND = 0x63,
    RRA_ZP     = 0x67,
    RRA_ABS    = 0x6F,
    RRA_IND_IX = 0x73,
    RRA_ZP_X   = 0x77,
    RRA_ABS_Y  = 0x7B,
    RRA_ABS_X  = 0x7F,

    // ANC — AND immediate, bit 7 of result → Carry
    ANC_I_0B = 0x0B,
    ANC_I_2B = 0x2B,

    // ALR — AND immediate then LSR accumulator
    ALR_I = 0x4B,

    // ARR — AND immediate then ROR accumulator (unusual flags)
    ARR_I = 0x6B,

    // AXS — X = (A & X) − immediate  (CMP-style flags, no borrow input)
    AXS_I = 0xCB,

    // LAS — A = X = SP = memory & SP
    LAS_ABS_Y = 0xBB,

    // SBC duplicate (identical to SBC_I 0xE9)
    SBC_I_EB = 0xEB,
}
