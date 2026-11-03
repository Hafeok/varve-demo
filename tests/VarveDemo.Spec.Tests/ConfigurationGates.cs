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

    /// <summary>Where our images' Dockerfiles live, the one place MCR may be named (ADR 0011 point 3).</summary>
    private const string OurImages = "deploy/images/";

    /// <summary>The one MCR image allowed there: Varve's ADR 0111 exception.</summary>
    private const string OurImageBase = "mcr.microsoft.com/dotnet/runtime-deps:";

    [Fact]
    public void No_configuration_file_uses_anonymous_mode()
    {
        string[] offenders = [.. ConfigurationFiles()
            .Where(static file => AnonymousMode().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryTree.Relative)];

        Assert.True(offenders.Length == 0, "Anonymous mode (--anonymous, or Auth:Mode Anonymous) in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_image_comes_from_an_allowed_registry_pinned_by_digest()
    {
        List<string> offenders = Offenders(ImageReferences());

        Assert.True(offenders.Count == 0, "Not from ghcr.io/ or public.ecr.aws/ pinned by @sha256, nor our image base in deploy/images/: " + string.Join("; ", offenders));
    }

    [Theory]
    [InlineData("compose.yaml")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData("deploy/images/importer/notes.yaml")]
    [InlineData("deploy/Dockerfile")]
    [InlineData("tests/fixtures/Dockerfile")]
    public void A_stray_mcr_reference_outside_our_image_dockerfiles_still_fails(string file)
    {
        string image = "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:" + new string('0', 64);

        Assert.Equal([file + ": " + image], Offenders([(file, image)]));
    }

    [Fact]
    public void Our_image_dockerfiles_may_name_only_the_runtime_deps_base_from_mcr_pinned_by_digest()
    {
        string digest = "@sha256:" + new string('0', 64);
        const string Dockerfile = "deploy/images/importer/Dockerfile";

        Assert.Empty(Offenders([(Dockerfile, "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled" + digest)]));
        Assert.Single(Offenders([(Dockerfile, "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled")]));
        Assert.Single(Offenders([(Dockerfile, "mcr.microsoft.com/dotnet/sdk:10.0" + digest)]));
        Assert.Single(Offenders([(Dockerfile, "docker.io/library/ubuntu:24.04" + digest)]));
    }

    /// <summary>
    /// The images that break the rule (ADR 0003 point 8, ADR 0011 point 3):
    /// anything not from ghcr.io or public.ecr.aws pinned by digest, except
    /// the runtime-deps base from MCR, pinned by digest, in a Dockerfile under
    /// <c>deploy/images/</c>.
    /// </summary>
    private static List<string> Offenders(IEnumerable<(string File, string Image)> references)
    {
        List<string> offenders = [];
        foreach ((string file, string image) in references)
        {
            bool allowed = s_allowedRegistries.Any(registry => image.StartsWith(registry, StringComparison.Ordinal))
                || (file.StartsWith(OurImages, StringComparison.Ordinal) && IsDockerfile(file) && image.StartsWith(OurImageBase, StringComparison.Ordinal));
            if (!allowed || !Digest().IsMatch(image))
            {
                offenders.Add(file + ": " + image);
            }
        }

        return offenders;
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

    /// <summary>
    /// <c>--anonymous</c>, and the same mode by its configuration names: <c>--auth-mode Anonymous</c>,
    /// <c>Varve:Auth:Mode</c> or <c>VARVE__AUTH__MODE</c> set to <c>Anonymous</c>, and a <c>"Mode": "Anonymous"</c> member.
    /// </summary>
    [GeneratedRegex(@"--anonymous\b|--auth-mode[ \t=]+[""']?anonymous|auth(?::|__)mode[""']?[ \t]*[:=][ \t]*[""']?anonymous|""mode""[ \t]*:[ \t]*""anonymous""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnonymousMode();

    [GeneratedRegex(@"^[ \t-]*(?:image:|uses:[ \t]*docker://)[ \t]*(?<image>[^#\n]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex YamlImage();

    [GeneratedRegex(@"^[ \t]*FROM[ \t]+(?:--\S+[ \t]+)*(?<image>\S+)(?:[ \t]+AS[ \t]+(?<stage>\S+))?", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DockerFrom();

    [GeneratedRegex("@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Digest();

    [GeneratedRegex(@"ghcr\.io/hafeok/varve:(?<tag>[^@\s""']+)", RegexOptions.CultureInvariant)]
    private static partial Regex VarveImage();
}
