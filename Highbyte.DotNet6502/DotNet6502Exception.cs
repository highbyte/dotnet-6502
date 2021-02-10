using System;
using System.Runtime.Serialization;

namespace Highbyte.DotNet6502
{
    [Serializable]
    public class DotNet6502Exception: Exception
    {
        public DotNet6502Exception(string message): base(message)
        {
        }
        protected DotNet6502Exception(SerializationInfo info,
            StreamingContext context): base(info, context)
        {
        }
        public override void GetObjectData(SerializationInfo info,
            StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}
