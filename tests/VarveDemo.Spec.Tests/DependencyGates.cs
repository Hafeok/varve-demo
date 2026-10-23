// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>The register of package dependencies, <c>Directory.Packages.props</c> (ADRs 0001, 0003).</summary>
public sealed class DependencyGates
{
    [Fact]
    public void Every_package_version_cites_an_existing_adr()
    {
        XDocument register = XDocument.Load(RepositoryTree.PathOf("Directory.Packages.props"));
        HashSet<string> adrs = [.. AdrFiles.Numbers().Select(static number => number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture))];
        List<XElement> versions = [.. register.Descendants("PackageVersion")];

        Assert.NotEmpty(versions);
        Assert.All(versions, version =>
        {
            string id = (string?)version.Attribute("Include") ?? "?";
            string? adr = (string?)version.Attribute("Adr");
            Assert.False(string.IsNullOrWhiteSpace(adr), id + " has no Adr attribute.");
            Assert.True(adrs.Contains(adr!), id + " cites ADR " + adr + ", which does not exist in docs/adr/.");
        });
    }

    [Fact]
    public void No_project_pins_its_own_version()
    {
        string[] projects = [.. RepositoryTree.Files()
            .Where(static file => file.EndsWith(".csproj", StringComparison.Ordinal) || file.EndsWith(".props", StringComparison.Ordinal) || file.EndsWith(".targets", StringComparison.Ordinal))
            .Where(static file => !string.Equals(Path.GetFileName(file), "Directory.Packages.props", StringComparison.Ordinal))];

        Assert.All(projects, static project =>
        {
            XDocument document = XDocument.Load(project);
            IEnumerable<XElement> pinned = document.Descendants()
                .Where(static element => element.Name.LocalName is "PackageReference" or "GlobalPackageReference" or "PackageVersion")
                .Where(static element => element.Attribute("Version") is not null || element.Attribute("VersionOverride") is not null || element.Element("Version") is not null);

            Assert.True(!pinned.Any(), RepositoryTree.Relative(project) + " pins its own version: " + string.Join(", ", pinned.Select(static element => (string?)element.Attribute("Include"))));
        });
    }
}
