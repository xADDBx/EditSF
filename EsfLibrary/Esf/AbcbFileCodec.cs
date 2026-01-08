using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EsfLibrary
{
    public sealed class AbcbFileCodec : AbcaFileCodec
    {
        private const int HeaderSize = 16;

        private int _nextUtf16Id;
        private int _nextAsciiId;

        private Dictionary<int, string> _utf16ById = new Dictionary<int, string>();
        private Dictionary<int, string> _asciiById = new Dictionary<int, string>();

        private Dictionary<string, int> _utf16IdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, int> _asciiIdByValue = new Dictionary<string, int>(StringComparer.Ordinal);

        public AbcbFileCodec() : base(0xABCB) { }
        public override void EncodeRootNode(BinaryWriter writer, EsfNode rootNode) {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));

            // Preserve original IDs when saving an existing file; only add new IDs for new strings.
            PrepareStringTablesForEncode();

            EnsureNodeNamesPresent(rootNode);

            byte[] encodedRoot;
            using (var bufferStream = new MemoryStream())
            using (var bufferWriter = new BinaryWriter(bufferStream, Encoding.UTF8, leaveOpen: true)) {
                Encode(bufferWriter, rootNode);
                bufferWriter.Flush();
                encodedRoot = bufferStream.ToArray();
            }

            WriteHeader(writer);

            if (writer.BaseStream.Position != HeaderSize) {
                throw new InvalidDataException("ABCB header size mismatch.");
            }

            writer.Write(encodedRoot);

            long nodeNamePosition = writer.BaseStream.Position;
            WriteNodeNames(writer, _utf16ById, _asciiById);

            long restore = writer.BaseStream.Position;
            writer.BaseStream.Seek(0x0C, SeekOrigin.Begin);
            writer.Write((uint)nodeNamePosition);
            writer.BaseStream.Seek(restore, SeekOrigin.Begin);
        }

        private void PrepareStringTablesForEncode() {
            if (_utf16ById == null) {
                _utf16ById = new Dictionary<int, string>();
            }

            if (_asciiById == null) {
                _asciiById = new Dictionary<int, string>();
            }

            if (_utf16IdByValue == null) {
                _utf16IdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            if (_asciiIdByValue == null) {
                _asciiIdByValue = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            if (_utf16IdByValue.Count == 0 && _utf16ById.Count != 0) {
                foreach (var kvp in _utf16ById) {
                    // If duplicates exist, preserve first-seen mapping.
                    if (!_utf16IdByValue.ContainsKey(kvp.Value)) {
                        _utf16IdByValue.Add(kvp.Value, kvp.Key);
                    }
                }
            }

            if (_asciiIdByValue.Count == 0 && _asciiById.Count != 0) {
                foreach (var kvp in _asciiById) {
                    if (!_asciiIdByValue.ContainsKey(kvp.Value)) {
                        _asciiIdByValue.Add(kvp.Value, kvp.Key);
                    }
                }
            }

            _nextUtf16Id = ComputeNextId(_utf16ById);
            _nextAsciiId = ComputeNextId(_asciiById);
        }

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

        protected override void WriteNodeNames(BinaryWriter writer)
        {
            // Kept for compatibility; EncodeRootNode uses the overload below.
            WriteNodeNames(writer, _utf16ById, _asciiById);
        }

        private void WriteNodeNames(BinaryWriter writer, Dictionary<int, string> utf16ByIdToWrite, Dictionary<int, string> asciiByIdToWrite)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.Write((short)nodeNames.Count);
            for (int i = 0; i < nodeNames.Count; i++)
            {
                WriteAscii(writer, nodeNames[i]);
            }

            WriteAbcbUtf16StringTableById(writer, utf16ByIdToWrite);
            WriteAbcbAsciiStringTableById(writer, asciiByIdToWrite);
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

        private void EnsureNodeNamesPresent(EsfNode rootNode)
        {
            if (nodeNames != null && nodeNames.Count > 0)
            {
                return;
            }

            nodeNames = new SortedList<int, string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            CollectNodeNames(rootNode, seen);
        }

        private void CollectNodeNames(EsfNode node, HashSet<string> seen)
        {
            RecordNode record = node as RecordNode;
            if (record != null)
            {
                string name = record.Name;
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                {
                    nodeNames.Add(nodeNames.Count, name);
                }
            }

            ParentNode parent = node as ParentNode;
            if (parent != null)
            {
                foreach (EsfNode child in parent.AllNodes)
                {
                    CollectNodeNames(child, seen);
                }
            }
        }

        private void CollectStrings(EsfNode node)
        {
            EsfValueNode<string> stringNode = node as EsfValueNode<string>;
            if (stringNode != null)
            {
                if (node.TypeCode == EsfType.UTF16)
                {
                    GetOrCreateUtf16Id(stringNode.Value);
                }
                else if (node.TypeCode == EsfType.ASCII || node.TypeCode == EsfType.ASCII_W21 || node.TypeCode == EsfType.ASCII_W25)
                {
                    GetOrCreateAsciiId(stringNode.Value);
                }
            }

            ParentNode parent = node as ParentNode;
            if (parent != null)
            {
                foreach (EsfNode child in parent.AllNodes)
                {
                    CollectStrings(child);
                }
            }
        }

        private void CollectReferencedStringIds(EsfNode node, HashSet<int> utf16Ids, HashSet<int> asciiIds)
        {
            EsfValueNode<string> stringNode = node as EsfValueNode<string>;
            if (stringNode != null)
            {
                string value = stringNode.Value;

                if (node.TypeCode == EsfType.UTF16)
                {
                    int id;
                    if (value != null && _utf16IdByValue.TryGetValue(value, out id))
                    {
                        utf16Ids.Add(id);
                    }
                }
                else if (node.TypeCode == EsfType.ASCII || node.TypeCode == EsfType.ASCII_W21 || node.TypeCode == EsfType.ASCII_W25)
                {
                    int id;
                    if (value != null && _asciiIdByValue.TryGetValue(value, out id))
                    {
                        asciiIds.Add(id);
                    }
                }
            }

            ParentNode parent = node as ParentNode;
            if (parent != null)
            {
                foreach (EsfNode child in parent.AllNodes)
                {
                    CollectReferencedStringIds(child, utf16Ids, asciiIds);
                }
            }
        }
    }
}