namespace Highbyte.DotNet6502
{
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
    }
}
