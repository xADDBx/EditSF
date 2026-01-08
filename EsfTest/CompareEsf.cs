using EsfLibrary;
using System;
using System.Collections.Generic;
using System.IO;

namespace EsfTest
{
    internal static class CompareEsf
    {
        public static int Run(string fileA, string fileB, string allowedMissmatch = null)
        {
            if (string.IsNullOrWhiteSpace(fileA) || string.IsNullOrWhiteSpace(fileB))
            {
                Console.Error.WriteLine("Usage: compare <fileA> <fileB>");
                return 2;
            }

            if (!File.Exists(fileA))
            {
                Console.Error.WriteLine("File not found: {0}", fileA);
                return 2;
            }

            if (!File.Exists(fileB))
            {
                Console.Error.WriteLine("File not found: {0}", fileB);
                return 2;
            }

            EsfFile a = EsfCodecUtil.LoadEsfFile(fileA);
            EsfFile b = EsfCodecUtil.LoadEsfFile(fileB);

            ForceDecode(a.RootNode);
            ForceDecode(b.RootNode);

            if (a.Codec.ID != b.Codec.ID)
            {
                Console.WriteLine("Different codec IDs: 0x{0:X} vs 0x{1:X}", a.Codec.ID, b.Codec.ID);
            }

            /*
            if (Equals(a, b))
            {
                Console.WriteLine("Files are equal (per EsfFile.Equals). ");
                return 0;
            }
            */

            if (a.RootNode == null || b.RootNode == null)
            {
                Console.WriteLine("One of the root nodes is null.");
                return 1;
            }

            var mismatch = FindFirstMismatch(a.RootNode, b.RootNode, new List<string>(), allowedMissmatch);
            if (mismatch != null)
            {
                Console.WriteLine("First mismatch at path: {0}", mismatch.Path);
                Console.WriteLine("A: {0}", mismatch.LeftSummary);
                Console.WriteLine("B: {0}", mismatch.RightSummary);
                return 1;
            }
            if (allowedMissmatch == null) {
                Console.WriteLine("Files differ, but no structural mismatch was found.");
            }
            return 3;
        }

        private static void ForceDecode(EsfNode node)
        {
            if (node is ParentNode parent)
            {
                foreach (var child in parent.AllNodes)
                {
                    ForceDecode(child);
                }
            }
        }

        private sealed class Mismatch
        {
            public string Path { get; set; }
            public string LeftSummary { get; set; }
            public string RightSummary { get; set; }
        }

        private static Mismatch FindFirstMismatch(EsfNode left, EsfNode right, List<string> path, string allowedMissmatch = null)
        {
            if (ReferenceEquals(left, right))
            {
                return null;
            }

            if (left == null || right == null)
            {
                return new Mismatch
                {
                    Path = "/" + string.Join("/", path),
                    LeftSummary = left == null ? "<null>" : Summarize(left),
                    RightSummary = right == null ? "<null>" : Summarize(right),
                };
            }

            if (left.TypeCode != right.TypeCode)
            {
                if (!string.Join("/", path).EndsWith(allowedMissmatch)) {
                    return new Mismatch {
                        Path = "/" + string.Join("/", path),
                        LeftSummary = Summarize(left),
                        RightSummary = Summarize(right),
                    };
                }
            }

            // value nodes
            if (left is ParentNode leftParent && right is ParentNode rightParent)
            {
                string leftName = (leftParent as INamedNode)?.GetName() ?? leftParent.Name;
                string rightName = (rightParent as INamedNode)?.GetName() ?? rightParent.Name;
                if (!string.Equals(leftName, rightName, StringComparison.Ordinal))
                {
                    return new Mismatch
                    {
                        Path = "/" + string.Join("/", path),
                        LeftSummary = Summarize(left),
                        RightSummary = Summarize(right),
                    };
                }

                var leftChildren = leftParent.AllNodes;
                var rightChildren = rightParent.AllNodes;

                if (leftChildren.Count != rightChildren.Count)
                {
                    return new Mismatch
                    {
                        Path = "/" + string.Join("/", path) + "/" + leftName,
                        LeftSummary = $"{Summarize(left)} (Children={leftChildren.Count})",
                        RightSummary = $"{Summarize(right)} (Children={rightChildren.Count})",
                    };
                }

                path.Add(leftName);
                try
                {
                    for (int i = 0; i < leftChildren.Count; i++)
                    {
                        var mismatch = FindFirstMismatch(leftChildren[i], rightChildren[i], path, allowedMissmatch);
                        if (mismatch != null)
                        {
                            // Add index context if multiple siblings share the same name.
                            mismatch.Path = mismatch.Path + $" [index {i}]";
                            return mismatch;
                        }
                    }
                }
                finally
                {
                    path.RemoveAt(path.Count - 1);
                }

                return null;
            }

            // non-parent nodes: fall back to Equals
            if (!left.Equals(right)) {
                if (!string.Join("/", path).EndsWith(allowedMissmatch)) {
                    return new Mismatch {
                        Path = "/" + string.Join("/", path),
                        LeftSummary = Summarize(left),
                        RightSummary = Summarize(right),
                    };
                }
            }

            return null;
        }

        private static string Summarize(EsfNode node)
        {
            if (node == null)
            {
                return "<null>";
            }

            if (node is ParentNode parent)
            {
                string name = (parent as INamedNode)?.GetName() ?? parent.Name;
                return $"{node.TypeCode} \"{name}\"";
            }

            try
            {
                return $"{node.TypeCode} {node}";
            }
            catch
            {
                return $"{node.TypeCode} <unprintable>";
            }
        }
    }
}
