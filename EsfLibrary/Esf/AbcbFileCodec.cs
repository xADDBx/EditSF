using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EsfLibrary
{
    public sealed class AbcbFileCodec : AbcaFileCodec
    {

        private int _nextUtf16Id;
        private int _nextAsciiId;

        private Dictionary<int, string> _utf16ById = new Dictionary<int, string>();
        private Dictionary<int, string> _asciiById = new Dictionary<int, string>();

        private Dictionary<string, int> _utf16IdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, int> _asciiIdByValue = new Dictionary<string, int>(StringComparer.Ordinal);

        public AbcbFileCodec() : base(0xABCB) { }

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

            _nextUtf16Id = ComputeNextId(_utf16ById);
            _nextAsciiId = ComputeNextId(_asciiById);
        }

        private static int ComputeNextId(Dictionary<int, string> byId)
        {
            if (byId == null || byId.Count == 0)
            {
                return 0;
            }

            int max = -1;
            foreach (int id in byId.Keys)
            {
                if (id > max)
                {
                    max = id;
                }
            }

            return max + 1;
        }

        private static Dictionary<int, string> ReadAbcbUtf16StringTableById(BinaryReader reader) {
            int count = reader.ReadInt32();

            Dictionary<int, string> result = new Dictionary<int, string>(count);

            for (int i = 0; i < count; i++) {
                int charLen = reader.ReadInt32();

                byte[] bytes = reader.ReadBytes(charLen * 2);
                if (bytes.Length != charLen * 2) {
                    throw new EndOfStreamException("Unexpected end of stream while reading ABCB UTF-16 string.");
                }

                string s = Encoding.Unicode.GetString(bytes);
                int id = reader.ReadInt32();

                if (!result.TryAdd(id, s)) {
                    Console.WriteLine($"Duplicate Id: {s}. Old string: {result[id]}, New string: {s}");
                }
            }

            return result;
        }

        private static Dictionary<int, string> ReadAbcbAsciiStringTableById(BinaryReader reader) {
            int count = reader.ReadInt32();

            Dictionary<int, string> result = new Dictionary<int, string>(count);

            for (int i = 0; i < count; i++) {
                int byteLen = reader.ReadInt32();

                byte[] bytes = reader.ReadBytes(byteLen);
                if (bytes.Length != byteLen) {
                    throw new EndOfStreamException("Unexpected end of stream while reading ABCB ASCII string.");
                }

                string s = Encoding.ASCII.GetString(bytes);
                int id = reader.ReadInt32();

                if (!result.TryAdd(id, s)) {
                    Console.WriteLine($"Duplicate Id: {s}. Old string: {result[id]}, New string: {s}");
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
                    result = new StringNode(ReadUtf16String, WriteUtf16ReferenceAbcb);
                    break;
                case EsfType.ASCII:
                case EsfType.ASCII_W21:
                case EsfType.ASCII_W25:
                    result = new StringNode(ReadAsciiString, WriteAsciiReferenceAbcb);
                    break;
                default:
                    return base.CreateValueNode(typeCode, optimize);
            }

            result.TypeCode = typeCode;
            return result;
        }

        public override string ReadUtf16String(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            string value;
            if (!_utf16ById.TryGetValue(id, out value))
            {
                value = null;
            }

            return value;
        }

        public override string ReadAsciiString(BinaryReader reader)
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
                return 0;
            }

            int id;
            if (_utf16IdByValue.TryGetValue(value, out id))
            {
                return id;
            }

            id = _nextUtf16Id;
            while (_utf16ById.ContainsKey(id))
            {
                id++;
            }

            _nextUtf16Id = id + 1;
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

            id = _nextAsciiId;
            while (_asciiById.ContainsKey(id))
            {
                id++;
            }

            _nextAsciiId = id + 1;
            _asciiById.Add(id, value);
            _asciiIdByValue.Add(value, id);
            return id;
        }

        private void WriteUtf16ReferenceAbcb(BinaryWriter writer, string value)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int id = GetOrCreateUtf16Id(value);
            writer.Write(id);
        }

        private void WriteAsciiReferenceAbcb(BinaryWriter writer, string value)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int id = GetOrCreateAsciiId(value);
            writer.Write(id);
        }

        protected override void WriteNodeNames(BinaryWriter writer) {
            writer.Write((short)nodeNames.Count);
            for (int i = 0; i < nodeNames.Count; i++) {
                WriteAscii(writer, nodeNames[i]);
            }

            WriteAbcbUtf16StringTableById(writer, _utf16ById);
            WriteAbcbAsciiStringTableById(writer, _asciiById);
        }

        private static void WriteAbcbUtf16StringTableById(BinaryWriter writer, Dictionary<int, string> byId)
        {
            writer.Write(byId.Count);

            List<int> ids = new List<int>(byId.Keys);
            ids.Sort();

            foreach (int id in ids)
            {
                string value = byId[id] ?? string.Empty;

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

            List<int> ids = new List<int>(byId.Keys);
            ids.Sort();

            foreach (int id in ids)
            {
                string value = byId[id] ?? string.Empty;

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