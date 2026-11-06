// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using VarveDemo.Oracle;
using Xunit;

namespace VarveDemo.Steps.Tests;

/// <summary>The oracle shares nothing with what it checks (S2 criterion 3; ADR 0010 point 6).</summary>
public sealed class OracleIndependenceTests
{
    [Fact]
    public void The_oracle_assembly_references_no_varve_assembly_and_no_demo_library()
    {
        string[] referenced = [.. typeof(CatalogOracle).Assembly.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty)];

        Assert.Contains("NuGet.Versioning", referenced);
        Assert.DoesNotContain(referenced, static name => name.StartsWith("Varve", StringComparison.Ordinal));
    }

    [Fact]
    public void The_oracle_project_references_no_varve_package_and_no_project()
    {
        string project = Path.Combine(Root(), "tests", "VarveDemo.Oracle", "VarveDemo.Oracle.csproj");
        XDocument document = XDocument.Load(project);
        string[] packages = [.. document.Descendants("PackageReference").Select(static e => (string?)e.Attribute("Include") ?? string.Empty)];

        Assert.DoesNotContain(packages, static id => id.StartsWith("Varve", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(document.Descendants("ProjectReference"));
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VarveDemo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No VarveDemo.slnx above " + AppContext.BaseDirectory);
    }
}
