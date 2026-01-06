using System;
using System.IO;

namespace EsfLibrary
{
    public sealed class AbcbFileCodec : EsfCodec
    {
        public AbcbFileCodec()
            : base(0xABCB)
        {
        }

        public override EsfHeader ReadHeader(BinaryReader reader)
        {
            // We know magic exists, but the full header layout is not identified yet.
            // Returning a minimal header and leaving the stream position at the start
            // would break the base Parse() flow, so we fail with guidance instead.
            uint magic = reader.ReadUInt32();
            throw new NotSupportedException(
                "0xABCB detected (Total War: Warhammer 3 v7+). " +
                "Header/node-name-offset layout is not implemented yet. " +
                "Run the enhanced analyzer to locate node name table offset and header size.");
        }

        public override void WriteHeader(BinaryWriter writer)
        {
            throw new NotSupportedException("Encoding 0xABCB is not implemented.");
        }
    }
}