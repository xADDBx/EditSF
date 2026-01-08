using EsfLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EsfTest {
    public class Tester {
        public static string FILENAME="testfiles.txt";
        public static TextWriter logWriter;        
        public static TextWriter addressLogWriter;
        public static void Main(string[] args) {
            if (args != null && args.Length > 0)
                if (string.Equals(args[0], "compare", StringComparison.OrdinalIgnoreCase)) {
                    if (args.Length < 3) {
                        Console.Error.WriteLine("Usage: EsfTest compare <fileA> <fileB>");
                        return;
                    }

                    Environment.ExitCode = CompareEsf.Run(args[1], args[2]);
                    return;
                } else if (string.Equals(args[0], "probe", StringComparison.OrdinalIgnoreCase)) {
                    if (args.Length < 3) {
                        Console.Error.WriteLine("Usage: EsfTest probe <input> <output>");
                        return;
                    }
                    var sw = Stopwatch.StartNew();

                    Console.WriteLine($"t={sw.Elapsed} load");
                    EsfFile file = EsfCodecUtil.LoadEsfFile(args[1]);

                    Console.WriteLine($"t={sw.Elapsed} force-decode");
                    ForceDecode(file.RootNode);

                    Console.WriteLine($"t={sw.Elapsed} touch-one-node");
                    // Touch something minimal: mark root modified (or modify a known value node if you have a path helper)
                    file.RootNode.Modified = true;

                    Console.WriteLine($"t={sw.Elapsed} write");
                    EsfCodecUtil.WriteEsfFile(args[2], file);

                    Console.WriteLine($"t={sw.Elapsed} done");
                    return;
                } else if (string.Equals(args[0], "probe3", StringComparison.OrdinalIgnoreCase)) {
                    if (args.Length < 3) {
                        Console.Error.WriteLine("Usage: EsfTest probe3 <input> <output> [--path=/A/B/C]");
                        return;
                    }

                    string path = "COMPRESSED_DATA/CAMPAIGN_ENV/CAMPAIGN_MODEL/WORLD/FACTION_ARRAY/FACTION_ARRAY - 93/FACTION/FACTION_ECONOMICS";

                    for (int i = 3; i < args.Length; i++) {
                        if (args[i].StartsWith("--path=", StringComparison.OrdinalIgnoreCase)) {
                            path = args[i].Substring("--path=".Length);
                        }
                    }

                    var sw = Stopwatch.StartNew();

                    Console.WriteLine($"t={sw.Elapsed} load");
                    EsfFile file = EsfCodecUtil.LoadEsfFile(args[1]);

                    Console.WriteLine($"t={sw.Elapsed} locate {path}");
                    ParentNode target = FindRecordPath(file.RootNode as ParentNode, path);
                    if (target == null) {
                        Console.WriteLine($"t={sw.Elapsed} target not found");
                        return;
                    }

                    Console.WriteLine($"t={sw.Elapsed} mutate int");
                    bool mutated = Mutate(target, 666);
                    Console.WriteLine($"t={sw.Elapsed} mutated={mutated}");

                    Console.WriteLine($"t={sw.Elapsed} write");
                    EsfCodecUtil.WriteEsfFile(args[2], file);

                    Console.WriteLine($"t={sw.Elapsed} done");
                    if (CompareEsf.Run(args[1], args[2], path) != 3) {
                        Console.WriteLine("New file has unexpected difference");
                    } else {
                        Console.WriteLine("New file matches original except for intended mutation");
                    }

                    return;
                }
        }
        private static ParentNode FindRecordPath(ParentNode root, string path) {
            if (root == null) return null;

            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            ParentNode current = root;

            foreach (string part in parts) {
                ParentNode next = null;
                foreach (ParentNode child in current.Children) {
                    if (string.Equals(child.Name, part, StringComparison.Ordinal)) {
                        next = child;
                        break;
                    }
                }

                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        private static bool Mutate(ParentNode record, int newVal) {
            foreach (EsfNode n in record.Values) {
                if (n is OptimizedIntNode a) {
                    a.Value = newVal;
                    return true;
                }
            }

            return false;
        }

        private static void ForceDecode(EsfNode node) {
            if (node is ParentNode parent) {
                foreach (EsfNode child in parent.AllNodes) {
                    ForceDecode(child);
                }
            }
        }

        public static void runTests() {
            //new CodecTest().run();
        }
        public static void testFiles() {
            foreach (string file in File.ReadAllLines(FILENAME, Encoding.Default)) {
                if (file.StartsWith("#") || string.IsNullOrEmpty(file)) {
                    continue;
                }
                //testOld (file);
                testNew (file);
                //Console.ReadKey();
            }
        }
        
        static void testNew (string filename) {
            using (FileStream 
                   logStream = File.Create(filename + "_log.txt"), 
                   addressStream = File.Create(filename + "_address.txt")) {
                logWriter = new StreamWriter(logStream);
                addressLogWriter = new StreamWriter(addressStream);
                EsfFile file = null;
                DateTime start = DateTime.Now;
                try {
                    Console.WriteLine("reading {0}", filename);
                    using (FileStream stream = File.OpenRead(filename)) {
                        EsfCodec codec = EsfCodecUtil.GetCodec(stream);
                        TicToc timer = new TicToc();
                        codec.NodeReadStarting += timer.Tic;
                        codec.NodeReadFinished += timer.Toc;
                        // codec.NodeReadFinished += OutputNodeEnd;
                        file = EsfCodecUtil.LoadEsfFile(filename);
                        forceDecode(file.RootNode);
                        //file = new EsfFile(stream, codec);
                        
                        timer.DumpAll();
                    }
                    Console.WriteLine("{0} read in {1} seconds", file, (DateTime.Now.Ticks - start.Ticks) / 10000000);
                    Console.WriteLine("Reading finished, saving now");
                } catch (Exception e) {
                    Console.WriteLine("Read failed: {0}, {1}", filename, e);
                }
                try {
                    string saveFile = filename + "_save";
                    if (file != null) {
                        EsfCodecUtil.WriteEsfFile(saveFile, file);
                    }
                    //File.Delete(saveFile);
                } catch (Exception e) {
                    Console.WriteLine("Write {0} failed: {1}", filename, e);
                }
                logWriter.Flush();
                addressLogWriter.Flush();
            }
        }
        static void forceDecode(EsfNode node) {
            if (node is ParentNode) {
                (node as ParentNode).AllNodes.ForEach(n => forceDecode(n));
            }
        }
        
        static void OutputNodeEnd(EsfNode node, long position) {
            if (logWriter != null) {
                logWriter.WriteLine("{0} / {1:x}", node, node.TypeCode);
                addressLogWriter.WriteLine("{2:x}: {0} / {1:x}", node, node.TypeCode, position);
                logWriter.Flush();
                addressLogWriter.Flush();
            }
        }

/*        static void testOld(string filename) {
            EsfFile file = new EsfFile (filename);
            foreach (IEsfNode rootNode in file.RootNodes) {
                rootNode.ParseDeep ();
            }
        }
  */      
//        static void parseNode(IEsfNode node) {
//            node.Parse ();
//            node.
//        }
    }
    
    public class TicToc {
        Dictionary<byte, long> codeToTime = new Dictionary<byte, long>();
        Dictionary<byte, int> codeToCount = new Dictionary<byte, int>();
        
        private long startTime;
        public void Tic(byte typeCode, long readerPosition) {
            startTime = DateTime.Now.Ticks;
        }
        public void Toc(EsfNode node, long readerPosition) {
            long endTime = DateTime.Now.Ticks;
            long total;
            int count;
            if (codeToTime.TryGetValue((byte)node.TypeCode, out total)) {
                count = codeToCount[(byte)node.TypeCode] + 1;
            } else {
                total = 0;
                count = 1;
            }
            total += endTime - startTime;
            codeToTime[(byte) node.TypeCode] = total;
            codeToCount[(byte) node.TypeCode] = count;
        }
        public void DumpAll() {
            Dictionary<long, byte> otherWay = new Dictionary<long, byte>();
            foreach(byte code in codeToTime.Keys) {
                otherWay.Add(codeToTime[code], code);
                Console.WriteLine("{0:x}: {1}", code, codeToTime[code]);
            }
            List<long> sorted = new List<long>(otherWay.Keys);
            sorted.Sort();
            sorted.ForEach(i => Console.WriteLine("{1:x} ({2}): {0}", i, otherWay[i], codeToCount[otherWay[i]]));
        }
    }
}
