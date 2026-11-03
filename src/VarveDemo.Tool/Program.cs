// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VarveDemo.Importer;

namespace VarveDemo.Tool;

/// <summary>The <c>varve-demo</c> command (ADR 0012).</summary>
internal static class Program
{
    private const string Usage = """
        varve-demo: the Varve demo's one command.

          varve-demo snapshot top     --n 3000 --out snapshot
          varve-demo snapshot build   --inputs snapshot --out <empty dir> [--manifest <path to write>] [--check snapshot/manifest.json]
          varve-demo snapshot verify  --snapshot <dir> [--manifest <path>]
          varve-demo snapshot pack    --snapshot <dir> --out <file.tar.gz>
          varve-demo import           --snapshot <dir or URL> --server <url> --issuer <url> [--issuer-address <url>] [--results <file>]
          varve-demo count            --server <url> --issuer <url> [--issuer-address <url>]
          varve-demo deps             --lock <packages.lock.json> --server <url> --issuer <url> [--issuer-address <url>] [--results <file>]

        Options not given are read from VARVE_DEMO_<NAME> (upper case, dashes as
        underscores). Every request to Varve carries a token from the issuer, by
        client credentials (ADR 0007).
        """;

    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancel = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };

        try
        {
            return await RunAsync(args, cancel.Token).ConfigureAwait(false);
        }
        catch (UsageException error)
        {
            await Console.Error.WriteLineAsync("varve-demo: " + error.Message).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }
        catch (Exception error) when (error is ImportRefusedException or InvalidDataException or HttpRequestException or IOException or TimeoutException or LeafRefusedException)
        {
            await Console.Error.WriteLineAsync("varve-demo: " + error.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        string command = args.Length > 0 ? args[0] : string.Empty;
        string sub = args.Length > 1 ? args[1] : string.Empty;
        return (command, sub) switch
        {
            ("snapshot", "top") => SnapshotCommands.TopAsync(new Arguments(args, 2), cancellationToken),
            ("snapshot", "build") => SnapshotCommands.BuildAsync(new Arguments(args, 2), cancellationToken),
            ("snapshot", "verify") => SnapshotCommands.VerifyAsync(new Arguments(args, 2), cancellationToken),
            ("snapshot", "pack") => SnapshotCommands.PackAsync(new Arguments(args, 2), cancellationToken),
            ("import", _) => ImportCommand.RunAsync(new Arguments(args, 1), cancellationToken),
            ("count", _) => StepCommands.CountAsync(new Arguments(args, 1), cancellationToken),
            ("deps", _) => StepCommands.DepsAsync(new Arguments(args, 1), cancellationToken),
            ("--version", _) => Version(),
            _ => throw new UsageException(command.Length == 0 ? "No command." : "Unknown command " + string.Join(' ', args[..Math.Min(2, args.Length)]) + "."),
        };
    }

    private static async Task<int> Version()
    {
        await Console.Out.WriteLineAsync(ToolVersion.Text).ConfigureAwait(false);
        return 0;
    }
}

/// <summary>The tool's version, with the mapping's: part of a snapshot's identity (ADR 0008).</summary>
internal static class ToolVersion
{
    public static string Text { get; } = "varve-demo "
        + (typeof(ToolVersion).Assembly.GetName().Version is { } v ? v.ToString(3) : "0.0.0")
        + " (mapping " + CatalogUpdate.MappingVersion + ")";
}
