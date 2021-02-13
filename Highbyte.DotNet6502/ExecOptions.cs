using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    public class ExecOptions
    {
        public ulong? CyclesRequested {get; set;}
        public ulong? MaxNumberOfInstructions {get; set;}
        public ushort? ExecuteUntilPC {get; set;}
        public ushort? ExecuteUntilExecutedInstructionAtPC {get; set;}
        
        public OpCodeId? ExecuteUntilInstruction {get; set;}
        public List<byte> ExecuteUntilInstructions {get; set;}
        public bool UnknownInstructionThrowsException {get; set;}

        public ExecOptions()
        {
            CyclesRequested = null;
            MaxNumberOfInstructions = null;
            ExecuteUntilPC = null;
            ExecuteUntilExecutedInstructionAtPC = null;
            UnknownInstructionThrowsException = false;
            ExecuteUntilInstruction = null;
            ExecuteUntilInstructions = new();
        }

        public ExecOptions Clone()
        {
            return new ExecOptions
            {
                CyclesRequested = this.CyclesRequested,
                MaxNumberOfInstructions = this.MaxNumberOfInstructions,
                ExecuteUntilPC = this.ExecuteUntilPC,
                ExecuteUntilExecutedInstructionAtPC = this.ExecuteUntilExecutedInstructionAtPC,
                UnknownInstructionThrowsException = this.UnknownInstructionThrowsException,
                ExecuteUntilInstruction = this.ExecuteUntilInstruction,
                ExecuteUntilInstructions = this.ExecuteUntilInstructions
            };
        }
    }
}
