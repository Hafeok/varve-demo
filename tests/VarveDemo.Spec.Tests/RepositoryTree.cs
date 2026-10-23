// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VarveDemo.Spec.Tests;

/// <summary>
/// The working tree the gates read: found by walking up from the test binary
/// to the directory holding <c>VarveDemo.slnx</c>.
/// </summary>
internal static class RepositoryTree
{
    private static readonly string[] s_skippedDirectories = [".git", "bin", "obj", "TestResults", "artifacts", ".data", "node_modules"];

    public static string Root { get; } = FindRoot();

    public static string PathOf(string relative) => Path.Combine(Root, relative);

    public static string Read(string relative) => File.ReadAllText(PathOf(relative)).ReplaceLineEndings("\n");

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>Every file in the tree, build output and git's own files excluded.</summary>
    public static IEnumerable<string> Files(string relativeDirectory = ".")
    {
        string start = PathOf(relativeDirectory);
        if (!Directory.Exists(start))
        {
            yield break;
        }

        Stack<string> pending = new();
        pending.Push(start);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                yield return file;
            }

            foreach (string child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
            {
                if (!s_skippedDirectories.Contains(Path.GetFileName(child), StringComparer.Ordinal))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VarveDemo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No VarveDemo.slnx above " + AppContext.BaseDirectory);
    }
}
