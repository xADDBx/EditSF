using System;
using System.Collections.Generic;
using System.IO;

namespace EsfLibrary
{
    public sealed class AbcbFileCodec : AbcaFileCodec
    {
        private const int HeaderSize = 16;

        public AbcbFileCodec()
            : base(0xABCB)
        {
        }

        public sealed class AbcbHeader : EsfHeader
        {
            public uint Unknown4 { get; set; }
            public uint TimestampRaw { get; set; }
            public uint NodeNameOffset { get; set; }

            public DateTime EditTimeUtc
            {
                get { return AbceCodec.GetTime(TimestampRaw); }
            }
        }

        public override EsfHeader ReadHeader(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            return new AbcbHeader
            {
                ID = reader.ReadUInt32(),
                Unknown4 = reader.ReadUInt32(),
                TimestampRaw = reader.ReadUInt32(),
                NodeNameOffset = reader.ReadUInt32(),
            };
        }

        public override void WriteHeader(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.Write(0xABCBu);
            writer.Write(0u);
            writer.Write(AbceCodec.GetTimestamp(DateTime.UtcNow));
            writer.Write(0u); // NodeNameOffset patched in EncodeRootNode
        }

        public override EsfNode Parse(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            Header = ReadHeader(reader);

            var abcbHeader = Header as AbcbHeader;
            if (abcbHeader == null)
            {
                throw new InvalidDataException("Unexpected header type for ABCB codec.");
            }

            uint nodeNameOffset = abcbHeader.NodeNameOffset;
            if (nodeNameOffset == 0 || nodeNameOffset >= reader.BaseStream.Length)
            {
                throw new InvalidDataException(string.Format("Invalid node name offset 0x{0:X}.", nodeNameOffset));
            }

            long restorePosition = reader.BaseStream.Position; // should be 0x10
            reader.BaseStream.Seek(nodeNameOffset, SeekOrigin.Begin);
            ReadNodeNames(reader);
            reader.BaseStream.Seek(restorePosition, SeekOrigin.Begin);

            EsfNode result = Decode(reader);
            result.Codec = this;
            return result;
        }

        public override void EncodeRootNode(BinaryWriter writer, EsfNode rootNode)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));

            WriteHeader(writer);

            if (writer.BaseStream.Position != HeaderSize)
            {
                throw new InvalidDataException("ABCB header size mismatch.");
            }

            // Use ABCA record encoding rules
            Encode(writer, rootNode);

            long nodeNamePosition = writer.BaseStream.Position;
            WriteNodeNames(writer);

            long restore = writer.BaseStream.Position;
            writer.BaseStream.Seek(0x0C, SeekOrigin.Begin);
            writer.Write((uint)nodeNamePosition);
            writer.BaseStream.Seek(restore, SeekOrigin.Begin);
        }
        protected override void ReadNodeNames(BinaryReader reader) {
            int count = reader.ReadInt16();
            nodeNames = new SortedList<int, string>(count);
            for (ushort i = 0; i < count; i++) {
                nodeNames.Add(i, ReadAscii(reader));
            }
        }

        protected override void WriteNodeNames(BinaryWriter writer) {
            writer.Write((short)nodeNames.Count);
            for (int i = 0; i < nodeNames.Count; i++) {
                WriteAscii(writer, nodeNames[i]);
            }
        }
    }
}