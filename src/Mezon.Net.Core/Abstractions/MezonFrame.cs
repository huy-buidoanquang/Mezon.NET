using System;

namespace Mezon.Net.Core.Abstractions
{
    /// <summary>
    /// A decoded network frame without tuple boxing on the hot path.
    /// </summary>
    public readonly struct MezonFrame
    {
        public MezonMessageType Type { get; }
        public int Cid { get; }
        public int Code { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public MezonFrame(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> payload)
        {
            Type = type;
            Cid = cid;
            Code = code;
            Payload = payload;
        }

        public void Deconstruct(out MezonMessageType type, out int cid, out int code, out ReadOnlyMemory<byte> payload)
        {
            type = Type;
            cid = Cid;
            code = Code;
            payload = Payload;
        }
    }
}
