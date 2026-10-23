// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace VarveDemo.Spec.Tests;

/// <summary>Authentication, images and the Varve pin in configuration files (ADRs 0002, 0003, 0007).</summary>
public sealed partial class ConfigurationGates
{
    private static readonly string[] s_configurationExtensions = [".yaml", ".yml", ".json", ".sh", ".bash", ".props", ".targets", ".env"];

    private static readonly string[] s_allowedRegistries = ["ghcr.io/", "public.ecr.aws/"];

    [Fact]
    public void No_configuration_file_uses_anonymous_mode()
    {
        string[] offenders = [.. ConfigurationFiles()
            .Where(static file => File.ReadAllText(file).Contains("--anonymous", StringComparison.Ordinal))
            .Select(RepositoryTree.Relative)];

        Assert.True(offenders.Length == 0, "--anonymous in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_image_comes_from_an_allowed_registry_pinned_by_digest()
    {
        List<string> offenders = [];
        foreach ((string file, string image) in ImageReferences())
        {
            bool allowed = s_allowedRegistries.Any(registry => image.StartsWith(registry, StringComparison.Ordinal));
            if (!allowed || !Digest().IsMatch(image))
            {
                offenders.Add(file + ": " + image);
            }
        }

        Assert.True(offenders.Count == 0, "Not from ghcr.io/ or public.ecr.aws/ pinned by @sha256: " + string.Join("; ", offenders));
    }

    [Fact]
    public void Compose_image_tag_matches_the_varve_pin()
    {
        Assert.SkipUnless(
            File.Exists(RepositoryTree.PathOf("compose.yaml")),
            "compose.yaml does not exist yet: slice 1 adds it, and this gate holds its Varve image tag to v$(VarveVersion) from then on (ADR 0002).");

        string? version = XDocument.Load(RepositoryTree.PathOf("Directory.Packages.props"))
            .Descendants("VarveVersion").Select(static element => element.Value.Trim()).SingleOrDefault();
        Assert.False(string.IsNullOrEmpty(version), "Directory.Packages.props has no VarveVersion property.");

        string[] tags = [.. VarveImage().Matches(RepositoryTree.Read("compose.yaml")).Select(static match => match.Groups["tag"].Value)];
        Assert.NotEmpty(tags);
        Assert.All(tags, tag => Assert.Equal("v" + version, tag));
    }

    private static IEnumerable<string> ConfigurationFiles() =>
        RepositoryTree.Files().Where(static file =>
            s_configurationExtensions.Contains(Path.GetExtension(file), StringComparer.Ordinal) || IsDockerfile(file));

    private static bool IsDockerfile(string file)
    {
        string name = Path.GetFileName(file);
        return name.StartsWith("Dockerfile", StringComparison.Ordinal) || name.EndsWith(".dockerfile", StringComparison.Ordinal) || name.StartsWith("Containerfile", StringComparison.Ordinal);
    }

    /// <summary>Every <c>image:</c> and <c>uses: docker://</c> in YAML, and every <c>FROM</c> in a Dockerfile.</summary>
    private static IEnumerable<(string File, string Image)> ImageReferences()
    {
        foreach (string file in RepositoryTree.Files())
        {
            string relative = RepositoryTree.Relative(file);
            string extension = Path.GetExtension(file);
            if (extension is ".yaml" or ".yml")
            {
                foreach (Match match in YamlImage().Matches(File.ReadAllText(file)))
                {
                    yield return (relative, match.Groups["image"].Value.Trim().Trim('"', '\''));
                }
            }
            else if (IsDockerfile(file))
            {
                HashSet<string> stages = new(StringComparer.OrdinalIgnoreCase) { "scratch" };
                foreach (Match match in DockerFrom().Matches(File.ReadAllText(file)))
                {
                    string image = match.Groups["image"].Value;
                    if (!stages.Contains(image))
                    {
                        yield return (relative, image);
                    }

                    if (match.Groups["stage"].Success)
                    {
                        stages.Add(match.Groups["stage"].Value);
                    }
                }
            }
        }
    }

    [GeneratedRegex(@"^[ \t-]*(?:image:|uses:[ \t]*docker://)[ \t]*(?<image>[^#\n]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex YamlImage();

    [GeneratedRegex(@"^[ \t]*FROM[ \t]+(?:--\S+[ \t]+)*(?<image>\S+)(?:[ \t]+AS[ \t]+(?<stage>\S+))?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DockerFrom();

    [GeneratedRegex("@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Digest();

    [GeneratedRegex(@"ghcr\.io/hafeok/varve:(?<tag>[^@\s""']+)", RegexOptions.CultureInvariant)]
    private static partial Regex VarveImage();
}
