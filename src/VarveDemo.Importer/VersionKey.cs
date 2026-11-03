// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VarveDemo.Importer;

/// <summary>
/// The sortable version key of ADR 0010 point 5: a string whose code-point
/// order is NuGet's SemVer 2.0 version order, so SPARQL can order versions
/// with <c>ORDER BY</c> and compare them with <c>&gt;</c>. Computed here, by
/// these rules; the oracle checks it against <c>NuGet.Versioning</c>.
/// </summary>
public static class VersionKey
{
    private const char Release = '~';
    private const char Prerelease = '-';
    private const char LabelSeparator = '!';

    /// <summary>The key of a NuGet version string, normalised or not.</summary>
    public static string Of(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);

        // Build metadata takes no part in the order.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        string withoutMetadata = plus >= 0 ? version[..plus] : version;
        int dash = withoutMetadata.IndexOf('-', StringComparison.Ordinal);
        string numbers = dash >= 0 ? withoutMetadata[..dash] : withoutMetadata;
        string? release = dash >= 0 ? withoutMetadata[(dash + 1)..] : null;

        string[] parts = numbers.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            throw new InvalidDataException("Not a NuGet version: " + version);
        }

        StringBuilder key = new(64);
        for (int i = 0; i < 4; i++)
        {
            int value = 0;
            if (i < parts.Length && !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidDataException("Not a NuGet version: " + version);
            }

            if (i > 0)
            {
                key.Append('.');
            }

            key.Append(value.ToString("D10", CultureInfo.InvariantCulture));
        }

        if (release is null)
        {
            return key.Append(Release).ToString();
        }

        if (release.Length == 0)
        {
            throw new InvalidDataException("Not a NuGet version: " + version);
        }

        key.Append(Prerelease);
        string[] labels = release.Split('.');
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0)
            {
                key.Append(LabelSeparator);
            }

            string label = labels[i];
            if (int.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                key.Append('0').Append(number.ToString("D10", CultureInfo.InvariantCulture));
            }
            else
            {
                key.Append('1').Append(label.ToUpperInvariant());
            }
        }

        return key.ToString();
    }
}
