// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;
using VarveDemo.Snapshot;

namespace VarveDemo.Tool;

/// <summary><c>varve-demo snapshot …</c>: the subset, the snapshot, its manifest (ADR 0008).</summary>
internal static class SnapshotCommands
{
    public static async Task<int> TopAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        int n = (int)arguments.Long("n", 3000);
        string output = arguments.OrDefault("out", "snapshot");
        arguments.RefuseUnknown();
        using CatalogHttp http = new();
        IReadOnlyList<string> ids = await TopPackages.WriteAsync(http, n, output, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync("snapshot top: " + ids.Count.ToString(CultureInfo.InvariantCulture) + " ids written to " + Path.Combine(output, "ids.txt") + ", the query to " + Path.Combine(output, "query.json")).ConfigureAwait(false);
        return 0;
    }

    public static async Task<int> BuildAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        string inputsDirectory = arguments.OrDefault("inputs", "snapshot");
        string output = arguments.Required("out");
        string? manifestPath = arguments.Optional("manifest");
        string? check = arguments.Optional("check");
        arguments.RefuseUnknown();

        SnapshotInputs inputs = SnapshotInputs.Read(inputsDirectory);
        using CatalogHttp http = new();
        SnapshotBuilder builder = new(http, Console.Out);
        SnapshotDraft draft = await builder.BuildAsync(inputs, output, cancellationToken).ConfigureAwait(false);
        SnapshotManifest manifest = await ManifestWriter.CompleteAsync(draft, inputs, output, ToolVersion.Text, cancellationToken).ConfigureAwait(false);
        byte[] json = manifest.ToJson();
        await File.WriteAllBytesAsync(Path.Combine(output, "manifest.json"), json, cancellationToken).ConfigureAwait(false);
        if (manifestPath is not null)
        {
            await File.WriteAllBytesAsync(manifestPath, json, cancellationToken).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("snapshot: " + manifest.Counts.CatalogCommitsRetained.ToString(CultureInfo.InvariantCulture) + " catalog commits retained, "
            + manifest.Counts.CatalogCommitsSkipped.ToString(CultureInfo.InvariantCulture) + " skipped, " + manifest.Counts.Leaves.ToString(CultureInfo.InvariantCulture) + " leaves, "
            + manifest.Counts.Quads.ToString(CultureInfo.InvariantCulture) + " quads; sha256 " + manifest.Sha256).ConfigureAwait(false);

        if (check is null)
        {
            return 0;
        }

        SnapshotManifest committed = SnapshotManifest.Read(await File.ReadAllBytesAsync(check, cancellationToken).ConfigureAwait(false));
        IReadOnlyList<string> differences = ManifestWriter.Differences(committed, manifest);
        if (differences.Count == 0)
        {
            await Console.Out.WriteLineAsync("snapshot: rebuilt from nuget.org, identical to " + check + " (sha256 " + committed.Sha256 + ")").ConfigureAwait(false);
            return 0;
        }

        await Console.Error.WriteLineAsync("snapshot: the rebuild differs from " + check + ":").ConfigureAwait(false);
        foreach (string difference in differences)
        {
            await Console.Error.WriteLineAsync("  " + difference).ConfigureAwait(false);
        }

        return 1;
    }

    public static async Task<int> VerifyAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        string directory = arguments.Required("snapshot");
        string? manifestPath = arguments.Optional("manifest");
        arguments.RefuseUnknown();
        SnapshotManifest manifest = manifestPath is null ? PinnedSnapshot.Manifest() : SnapshotManifest.Read(await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        SnapshotReader reader = await SnapshotReader.OpenAsync(directory, manifest, cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync("snapshot: " + directory + " matches its manifest, sha256 " + reader.Manifest.Sha256).ConfigureAwait(false);
        return 0;
    }

    public static async Task<int> PackAsync(Arguments arguments, CancellationToken cancellationToken)
    {
        string directory = arguments.Required("snapshot");
        string output = arguments.Required("out");
        arguments.RefuseUnknown();
        FileStream file = File.Create(output);
        await using (file.ConfigureAwait(false))
        {
            GZipStream gzip = new(file, CompressionLevel.SmallestSize);
            await using (gzip.ConfigureAwait(false))
            {
                await TarFile.CreateFromDirectoryAsync(directory, gzip, includeBaseDirectory: false, cancellationToken).ConfigureAwait(false);
            }
        }

        await Console.Out.WriteLineAsync("snapshot: packed " + directory + " into " + output + ", sha256 " + await Sha256Text.OfFileAsync(output, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        return 0;
    }
}

/// <summary>The snapshot this build of the tool pins: the committed manifest, embedded (ADR 0008).</summary>
internal static class PinnedSnapshot
{
    public static SnapshotManifest Manifest()
    {
        using Stream stream = typeof(PinnedSnapshot).Assembly.GetManifestResourceStream("snapshot/manifest.json")
            ?? throw new InvalidDataException("This build of varve-demo carries no snapshot manifest; build it after snapshot/manifest.json is committed.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return SnapshotManifest.Read(bytes.ToArray());
    }

    /// <summary>
    /// A snapshot directory: <paramref name="source"/> itself when it is a
    /// directory, or the release asset at a URL, downloaded into
    /// <paramref name="cache"/> and unpacked. Either is verified against the
    /// pinned manifest before it is used.
    /// </summary>
    public static async Task<SnapshotReader> OpenAsync(string source, string cache, TextWriter log, CancellationToken cancellationToken)
    {
        SnapshotManifest manifest = Manifest();
        string directory = source;
        if (source.StartsWith("https://", StringComparison.Ordinal) || source.StartsWith("http://", StringComparison.Ordinal))
        {
            directory = Path.Combine(cache, manifest.Sha256);
            if (!File.Exists(Path.Combine(directory, "commits.ndjson")))
            {
                await log.WriteLineAsync("snapshot: downloading " + source).ConfigureAwait(false);
                string partial = directory + ".partial";
                if (Directory.Exists(partial))
                {
                    Directory.Delete(partial, recursive: true);
                }

                Directory.CreateDirectory(partial);
                using HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
                using HttpResponseMessage response = await http.GetAsync(new Uri(source), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    GZipStream gzip = new(body, CompressionMode.Decompress);
                    await using (gzip.ConfigureAwait(false))
                    {
                        await TarFile.ExtractToDirectoryAsync(gzip, partial, overwriteFiles: false, cancellationToken).ConfigureAwait(false);
                    }
                }

                File.Delete(Path.Combine(partial, "manifest.json"));
                Directory.Move(partial, directory);
            }
        }

        await log.WriteLineAsync("snapshot: verifying " + directory + " against the pinned manifest, sha256 " + manifest.Sha256).ConfigureAwait(false);
        return await SnapshotReader.OpenAsync(directory, manifest, cancellationToken).ConfigureAwait(false);
    }
}
