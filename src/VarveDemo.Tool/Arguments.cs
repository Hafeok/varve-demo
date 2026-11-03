// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace VarveDemo.Tool;

/// <summary>A refused command line: the message is the usage error.</summary>
internal sealed class UsageException : Exception
{
    public UsageException()
    {
    }

    public UsageException(string message)
        : base(message)
    {
    }

    public UsageException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// <c>--name value</c> options (ADR 0012 point 3). An option not given falls
/// back to the environment variable <c>VARVE_DEMO_NAME</c>, upper case with
/// dashes as underscores, so compose configures the importer by environment.
/// </summary>
internal sealed class Arguments
{
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public Arguments(IReadOnlyList<string> args, int skip)
    {
        for (int i = skip; i < args.Count; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Count)
            {
                throw new UsageException("Expected --name value, got " + arg + ".");
            }

            _options[arg[2..]] = args[++i];
        }
    }

    public string? Optional(string name)
    {
        _used.Add(name);
        if (_options.TryGetValue(name, out string? value))
        {
            return value;
        }

        string variable = "VARVE_DEMO_" + name.ToUpperInvariant().Replace('-', '_');
        return Environment.GetEnvironmentVariable(variable) is { Length: > 0 } fromEnvironment ? fromEnvironment : null;
    }

    public string Required(string name) =>
        Optional(name) ?? throw new UsageException("--" + name + " (or VARVE_DEMO_" + name.ToUpperInvariant().Replace('-', '_') + ") is required.");

    public string OrDefault(string name, string fallback) => Optional(name) ?? fallback;

    public long Long(string name, long fallback) =>
        Optional(name) is { } text ? long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture) : fallback;

    public Uri Uri(string name) => new(Required(name), UriKind.Absolute);

    /// <summary>Refuses options no command read.</summary>
    public void RefuseUnknown()
    {
        foreach (string name in _options.Keys)
        {
            if (!_used.Contains(name))
            {
                throw new UsageException("Unknown option --" + name + ".");
            }
        }
    }
}
