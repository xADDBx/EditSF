using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EsfLibrary
{
    public sealed class AbcbFileCodec : AbcaFileCodec
    {
        public AbcbFileCodec() : base(0xABCB) { }
        protected override string ReadUtf16NodeNames(BinaryReader reader) {
            // I assume the format serializes as uint; as int for length does not make much sense
            // In practice the infrastructure does not support values exceeding int.MaxValue anyway
            int strLength = (int)reader.ReadUInt32();
            return Encoding.Unicode.GetString(reader.ReadBytes(strLength * 2));
        }
        protected override void WriteUtf16NodeNames(BinaryWriter writer, string toWrite) {
            writer.Write(toWrite.Length);
            writer.Write(Encoding.Unicode.GetBytes(toWrite));
        }
        protected override string ReadAsciiNodeNames(BinaryReader reader) {
            int strLength = (int)reader.ReadUInt32();
            return Encoding.ASCII.GetString(reader.ReadBytes(strLength));
        }
        protected override void WriteAsciiNodeNames(BinaryWriter writer, string toWrite) {
            writer.Write(toWrite.Length);
            writer.Write(Encoding.ASCII.GetBytes(toWrite));
        }
    }
}