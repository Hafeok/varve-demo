// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace VarveDemo.Steps;

/// <summary>One measured number, with its unit and, when the specification sets one, its target.</summary>
/// <param name="Name">A stable name, camel case: what the evidence page keys on.</param>
/// <param name="Value">The number measured.</param>
/// <param name="Unit">Its unit: <c>s</c>, <c>ms</c>, <c>count</c>, <c>per-s</c>, <c>KiB</c>.</param>
/// <param name="Target">The target it is held to, in the same unit, or null.</param>
/// <param name="Comparison"><c>max</c> when the value must be at most the target, <c>equal</c> when it must equal it, null with no target.</param>
public sealed record Measurement(string Name, double Value, string Unit, double? Target = null, string? Comparison = null)
{
    /// <summary>Whether the value meets its target; true with no target.</summary>
    public bool Met => Target is not double target || Comparison switch
    {
        "max" => Value <= target,
        "equal" => Value.Equals(target),
        _ => true,
    };
}

/// <summary>
/// A step's measurements, as the JSON a CI artifact carries and the evidence
/// page will read: one object per check run, with the Varve version, the
/// snapshot's hash, where it ran, and a flat list of named numbers with units
/// and targets, so the page renders any step without knowing its shape.
/// </summary>
public sealed class MeasurementReport
{
    /// <summary>The form of the report.</summary>
    public const int Format = 1;

    private readonly List<Measurement> _measurements = [];
    private readonly SortedDictionary<string, string> _facts = new(StringComparer.Ordinal);

    /// <summary>A report for the check named <paramref name="check"/>.</summary>
    public MeasurementReport(string check)
    {
        ArgumentException.ThrowIfNullOrEmpty(check);
        Check = check;
    }

    /// <summary>The check, as the specification names it.</summary>
    public string Check { get; }

    /// <summary>The measurements so far.</summary>
    public IReadOnlyList<Measurement> Measurements => _measurements;

    /// <summary>Adds a measurement.</summary>
    public MeasurementReport Add(Measurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        _measurements.Add(measurement);
        return this;
    }

    /// <summary>Adds a fact about the run: a version, a hash, a runner.</summary>
    public MeasurementReport Fact(string name, string value)
    {
        _facts[name] = value;
        return this;
    }

    /// <summary>Writes the report to <paramref name="path"/>.</summary>
    public void Write(string path, DateTimeOffset at)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream file = File.Create(path);
        using Utf8JsonWriter json = new(file, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteNumber("format", Format);
        json.WriteString("check", Check);
        json.WriteString("at", at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        json.WriteStartObject("facts");
        foreach ((string name, string value) in _facts)
        {
            json.WriteString(name, value);
        }

        json.WriteEndObject();
        json.WriteStartArray("measurements");
        foreach (Measurement measurement in _measurements)
        {
            json.WriteStartObject();
            json.WriteString("name", measurement.Name);
            json.WriteNumber("value", measurement.Value);
            json.WriteString("unit", measurement.Unit);
            if (measurement.Target is double target)
            {
                json.WriteNumber("target", target);
                json.WriteString("comparison", measurement.Comparison);
                json.WriteBoolean("met", measurement.Met);
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }
}
