using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

            // Reset any prior run state so repeated saves don't leak IDs.
            _utf16ById = new Dictionary<int, string>();
            _asciiById = new Dictionary<int, string>();
            _utf16IdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
            _asciiIdByValue = new Dictionary<string, int>(StringComparer.Ordinal);

            WriteHeader(writer);

            if (writer.BaseStream.Position != HeaderSize)
            {
                throw new InvalidDataException("ABCB header size mismatch.");
            }

            Encode(writer, rootNode);

            long nodeNamePosition = writer.BaseStream.Position;
            WriteNodeNames(writer);

            long restore = writer.BaseStream.Position;
            writer.BaseStream.Seek(0x0C, SeekOrigin.Begin);
            writer.Write((uint)nodeNamePosition);
            writer.BaseStream.Seek(restore, SeekOrigin.Begin);
        }

        // ABCB: use id->string lookup for references
        private Dictionary<int, string> _utf16ById = new Dictionary<int, string>();
        private Dictionary<int, string> _asciiById = new Dictionary<int, string>();

        // For writing: value->id reverse maps (ensures stable ID per string value within one encode)
        private Dictionary<string, int> _utf16IdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, int> _asciiIdByValue = new Dictionary<string, int>(StringComparer.Ordinal);

        protected override void ReadNodeNames(BinaryReader reader)
        {
            int count = reader.ReadInt16();
            nodeNames = new SortedList<int, string>(count);
            for (ushort i = 0; i < count; i++)
            {
                nodeNames.Add(i, ReadAscii(reader));
            }

            ReadAbcbStringTables(reader);
        }

        private void ReadAbcbStringTables(BinaryReader reader)
        {
            _utf16ById = ReadAbcbUtf16StringTableById(reader);
            _asciiById = ReadAbcbAsciiStringTableById(reader);

            _utf16IdByValue = new Dictionary<string, int>(_utf16ById.Count, StringComparer.Ordinal);
            foreach (var kvp in _utf16ById)
            {
                if (!_utf16IdByValue.ContainsKey(kvp.Value))
                {
                    _utf16IdByValue.Add(kvp.Value, kvp.Key);
                }
            }

            _asciiIdByValue = new Dictionary<string, int>(_asciiById.Count, StringComparer.Ordinal);
            foreach (var kvp in _asciiById)
            {
                if (!_asciiIdByValue.ContainsKey(kvp.Value))
                {
                    _asciiIdByValue.Add(kvp.Value, kvp.Key);
                }
            }
        }

        private static Dictionary<int, string> ReadAbcbUtf16StringTableById(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("Invalid ABCB string table count (negative).");
            }

            Dictionary<int, string> result = new Dictionary<int, string>(count);

            for (int i = 0; i < count; i++)
            {
                int charLen = reader.ReadInt32();
                if (charLen < 0)
                {
                    throw new InvalidDataException("Invalid ABCB string length (negative).");
                }

                byte[] bytes = reader.ReadBytes(charLen * 2);
                if (bytes.Length != charLen * 2)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading ABCB UTF-16 string.");
                }

                string s = Encoding.Unicode.GetString(bytes);
                int id = reader.ReadInt32();

                if (!result.ContainsKey(id))
                {
                    result.Add(id, s);
                }
            }

            return result;
        }

        private static Dictionary<int, string> ReadAbcbAsciiStringTableById(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("Invalid ABCB string table count (negative).");
            }

            Dictionary<int, string> result = new Dictionary<int, string>(count);

            for (int i = 0; i < count; i++)
            {
                int byteLen = reader.ReadInt32();
                if (byteLen < 0)
                {
                    throw new InvalidDataException("Invalid ABCB string length (negative).");
                }

                byte[] bytes = reader.ReadBytes(byteLen);
                if (bytes.Length != byteLen)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading ABCB ASCII string.");
                }

                string s = Encoding.ASCII.GetString(bytes);
                int id = reader.ReadInt32();

                if (!result.ContainsKey(id))
                {
                    result.Add(id, s);
                }
            }

            return result;
        }

        public override EsfNode CreateValueNode(EsfType typeCode, bool optimize = false)
        {
            EsfNode result;
            switch (typeCode)
            {
                case EsfType.UTF16:
                    result = new StringNode(ReadUtf16String, WriteUtf16Reference);
                    break;
                case EsfType.ASCII:
                case EsfType.ASCII_W21:
                case EsfType.ASCII_W25:
                    result = new StringNode(ReadAsciiString, WriteAsciiReference);
                    break;
                default:
                    return base.CreateValueNode(typeCode, optimize);
            }

            result.TypeCode = typeCode;
            return result;
        }

        protected override string ReadUtf16String(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            string value;
            if (!_utf16ById.TryGetValue(id, out value))
            {
                value = null;
            }

            return value;
        }

        protected override string ReadAsciiString(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            string value;
            if (!_asciiById.TryGetValue(id, out value))
            {
                value = null;
            }

            return value;
        }

        private int GetOrCreateUtf16Id(string value)
        {
            if (value == null)
            {
                // Prefer a stable null/empty representation; ABCB files often use 0/5/etc,
                // but without a spec we emit 0 for null and do not store it in the table.
                return 0;
            }

            int id;
            if (_utf16IdByValue.TryGetValue(value, out id))
            {
                return id;
            }

            // Allocate next free id (not necessarily contiguous if source file had holes).
            id = 0;
            while (_utf16ById.ContainsKey(id))
            {
                id++;
            }

            _utf16ById.Add(id, value);
            _utf16IdByValue.Add(value, id);
            return id;
        }

        private int GetOrCreateAsciiId(string value)
        {
            if (value == null)
            {
                return 0;
            }

            int id;
            if (_asciiIdByValue.TryGetValue(value, out id))
            {
                return id;
            }

            id = 0;
            while (_asciiById.ContainsKey(id))
            {
                id++;
            }

            _asciiById.Add(id, value);
            _asciiIdByValue.Add(value, id);
            return id;
        }

        private new void WriteUtf16Reference(BinaryWriter writer, string value)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int id = GetOrCreateUtf16Id(value);
            writer.Write(id);
        }

        private new void WriteAsciiReference(BinaryWriter writer, string value)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int id = GetOrCreateAsciiId(value);
            writer.Write(id);
        }

        protected override void WriteNodeNames(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.Write((short)nodeNames.Count);
            for (int i = 0; i < nodeNames.Count; i++)
            {
                WriteAscii(writer, nodeNames[i]);
            }

            WriteAbcbUtf16StringTableById(writer, _utf16ById);
            WriteAbcbAsciiStringTableById(writer, _asciiById);
        }

        private static void WriteAbcbUtf16StringTableById(BinaryWriter writer, Dictionary<int, string> byId)
        {
            writer.Write(byId.Count);

            foreach (var kvp in byId)
            {
                string value = kvp.Value ?? string.Empty;
                int id = kvp.Key;

                int charLen = value.Length;
                writer.Write(charLen);

                if (charLen != 0)
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(value);
                    writer.Write(bytes);
                }

                writer.Write(id);
            }
        }

        private static void WriteAbcbAsciiStringTableById(BinaryWriter writer, Dictionary<int, string> byId)
        {
            writer.Write(byId.Count);

            foreach (var kvp in byId)
            {
                string value = kvp.Value ?? string.Empty;
                int id = kvp.Key;

                byte[] bytes = Encoding.ASCII.GetBytes(value);
                writer.Write(bytes.Length);

                if (bytes.Length != 0)
                {
                    writer.Write(bytes);
                }

                writer.Write(id);
            }
        }
    }
}