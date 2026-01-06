using System;
using System.Collections.Generic;
using System.IO;
using SevenZip.Compression;
using SevenZip;

using LzmaDecoder = SevenZip.Compression.LZMA.Decoder;
using LzmaEncoder = SevenZip.Compression.LZMA.Encoder;

namespace EsfLibrary {
    public class CompressedNode : DelegatingNode {
        public CompressedNode(EsfCodec codec, RecordNode rootNode) : base(codec) {
            Name = TAG_NAME;
            compressedNode = rootNode;
        }

        private RecordNode compressedNode;

        public static readonly string TAG_NAME = "COMPRESSED_DATA";
        public static readonly string INFO_TAG = "COMPRESSED_DATA_INFO";
        private static char GetHexNibble(int v) {
            return (char)(v < 10 ? ('0' + v) : ('A' + (v - 10)));
        }

        // unzip contained 7zip node
        protected override RecordNode DecodeDelegate() {
#if DEBUG
            Console.WriteLine("decompressing");
#endif
            List<EsfNode> values = compressedNode.Values;
            byte[] data = (values[0] as EsfValueNode<byte[]>).Value;
            ParentNode infoNode = compressedNode.Children[0];
            uint size = (infoNode.Values[0] as EsfValueNode<uint>).Value;
            byte[] decodeProperties = (infoNode.Values[1] as EsfValueNode<byte[]>).Value;

            LzmaDecoder decoder = new LzmaDecoder();
            decoder.SetDecoderProperties(decodeProperties);

            byte[] outData = new byte[size];
            using (MemoryStream inStream = new MemoryStream(data, false), outStream = new MemoryStream(outData)) {
                decoder.Code(inStream, outStream, data.Length, size, null);
                outData = outStream.ToArray();
            }
            EsfCodec codec;
            using (var ms = new MemoryStream(outData, writable: false)) {
                codec = EsfCodecUtil.GetCodec(ms);
            }
            if (codec == null) {
                codec = new AbcaFileCodec();
            }

            EsfNode result;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(outData))) {
                result = codec.Parse(reader);
            }
            return result as RecordNode;
        }

        //re-compress node
        public override void Encode(BinaryWriter writer) {
            // encode the node into bytes
            byte[] data;
            MemoryStream uncompressedStream = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(uncompressedStream)) {
                // use the node's own codec or we'll mess up the string lists
                Decoded.Codec.EncodeRootNode(w, Decoded);
                data = uncompressedStream.ToArray();
            }
            uint uncompressedSize = (uint)data.LongLength;

            // compress the encoded data
#if DEBUG
            Console.WriteLine("compressing...");
#endif

            MemoryStream outStream = new MemoryStream();
            LzmaEncoder encoder = new LzmaEncoder();
            using (uncompressedStream = new MemoryStream(data)) {
                encoder.Code(uncompressedStream, outStream, data.Length, long.MaxValue, null);
                data = outStream.ToArray();
            }
#if DEBUG
            Console.WriteLine("ok, compression done");
#endif

            // prepare decoding information
            List<EsfNode> infoItems = new List<EsfNode>();
            infoItems.Add(new UIntNode { Value = uncompressedSize, TypeCode = EsfType.UINT32, Codec = Codec });
            using (MemoryStream propertyStream = new MemoryStream()) {
                encoder.WriteCoderProperties(propertyStream);
                infoItems.Add(new RawDataNode(Codec) {
                    Value = propertyStream.ToArray()
                });
            }

            List<EsfNode> dataItems = new List<EsfNode>();
            dataItems.Add(new RawDataNode(Codec) {
                Value = data
            });
            dataItems.Add(new RecordNode(Codec) { Name = CompressedNode.INFO_TAG, Value = infoItems });
            RecordNode compressedNode = new RecordNode(Codec) { Name = CompressedNode.TAG_NAME, Value = dataItems };

            compressedNode.Encode(writer);
        }
    }
}

