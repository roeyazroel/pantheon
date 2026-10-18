using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Architecture;

/// <summary>
/// The owner's coding rules that can be checked mechanically: core code never references Godot,
/// references point down the layers only, core code has no unseeded randomness or clock, and
/// native dependencies ship libraries for every desktop platform.
/// </summary>
public sealed class ArchitectureRulesTests
{
    private const string CoreProjectSdk = "Microsoft.NET.Sdk";
    private const string ResourcePrefix = "res://";
    private const string LibrariesSection = "libraries";

    private static readonly string[] DesktopLibraryFeatures =
    [
        "windows.debug.x86_64",
        "windows.release.x86_64",
        "linux.debug.x86_64",
        "linux.release.x86_64",
        "macos.debug",
        "macos.release",
    ];

    // Every use of these types is nondeterministic.
    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.Random",
        "System.Diagnostics.Stopwatch",
        "System.Security.Cryptography.RandomNumberGenerator",
        "System.TimeProvider",
    };

    private static readonly HashSet<string> ForbiddenMembers = new(StringComparer.Ordinal)
    {
        "System.DateTime.get_Now",
        "System.DateTime.get_UtcNow",
        "System.DateTime.get_Today",
        "System.DateTimeOffset.get_Now",
        "System.DateTimeOffset.get_UtcNow",
        "System.Environment.get_TickCount",
        "System.Environment.get_TickCount64",
        "System.Guid.NewGuid",
    };

    [Theory]
    [InlineData("Pantheon.Domain")]
    [InlineData("Pantheon.Simulation", "Pantheon.Domain")]
    [InlineData("Pantheon.Persistence", "Pantheon.Domain")]
    [InlineData("Pantheon.Game", "Pantheon.Domain", "Pantheon.Simulation", "Pantheon.Persistence")]
    [InlineData("Pantheon.Networking")]
    public void CoreProject_ReferencesOnlyLowerLayersAndNeverGodot(string project, params string[] allowedProjects)
    {
        var projectFile = XDocument.Load(RepositoryRoot.Combine("src", project, project + ".csproj"));

        Assert.Equal(CoreProjectSdk, (string?)projectFile.Root?.Attribute("Sdk"));
        Assert.All(
            IncludesOf(projectFile, "ProjectReference").Select(GetProjectName),
            reference => Assert.Contains(reference, allowedProjects));
        Assert.DoesNotContain(IncludesOf(projectFile, "PackageReference").Concat(IncludesOf(projectFile, "Reference")), IsGodot);
    }

    [Theory]
    [InlineData("Pantheon.Domain")]
    [InlineData("Pantheon.Simulation")]
    [InlineData("Pantheon.Persistence")]
    [InlineData("Pantheon.Game")]
    public void CoreAssembly_HasNoGodotClockOrUnseededRandomness(string assemblyName)
    {
        var references = ReadReferences(Assembly.Load(assemblyName));

        Assert.DoesNotContain(references.Assemblies, IsGodot);
        Assert.Empty(references.Forbidden);
    }

    [Fact]
    public void ReferenceScan_FindsForbiddenTypesAndMembers()
    {
        Assert.InRange(new Random(1).Next(10), 0, 9);
        Assert.NotEqual(Guid.Empty, Guid.NewGuid());

        var references = ReadReferences(typeof(ArchitectureRulesTests).Assembly);

        Assert.Contains("System.Random", references.Forbidden);
        Assert.Contains("System.Guid.NewGuid", references.Forbidden);
    }

    [Fact]
    public void GdExtensions_ShipDebugAndReleaseLibrariesForEveryDesktopPlatform()
    {
        var gameDirectory = RepositoryRoot.Combine("game");
        var manifests = Directory.GetFiles(Path.Combine(gameDirectory, "addons"), "*.gdextension", SearchOption.AllDirectories);

        Assert.NotEmpty(manifests);
        foreach (var manifest in manifests)
        {
            var libraries = ReadSection(manifest, LibrariesSection);
            foreach (var feature in DesktopLibraryFeatures)
            {
                Assert.True(libraries.TryGetValue(feature, out var resourcePath), $"{manifest} has no '{feature}' library.");
                Assert.True(LibraryExists(gameDirectory, resourcePath!), $"{manifest}: '{feature}' points at missing {resourcePath}.");
            }
        }
    }

    /// <summary>Assembly names, and forbidden type and member references, read from an assembly's metadata.</summary>
    /// <param name="Assemblies">Names of the referenced assemblies.</param>
    /// <param name="Forbidden">Forbidden types (<c>Namespace.Type</c>) and members (<c>Namespace.Type.Member</c>) it references.</param>
    private sealed record AssemblyReferences(IReadOnlyList<string> Assemblies, IReadOnlyList<string> Forbidden);

    /// <summary>Reads what an assembly references from its metadata, without running any of its code.</summary>
    /// <param name="assembly">Assembly to inspect.</param>
    /// <returns>Its assembly references and forbidden references.</returns>
    private static AssemblyReferences ReadReferences(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        var assemblies = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToList();
        var types = metadata.TypeReferences
            .Select(handle => GetFullName(metadata, handle))
            .Where(ForbiddenTypes.Contains);
        var members = metadata.MemberReferences
            .Select(metadata.GetMemberReference)
            .Where(member => member.Parent.Kind == HandleKind.TypeReference)
            .Select(member => GetFullName(metadata, (TypeReferenceHandle)member.Parent) + "." + metadata.GetString(member.Name))
            .Where(ForbiddenMembers.Contains);
        return new AssemblyReferences(assemblies, types.Concat(members).ToList());
    }

    /// <summary>Namespace-qualified name of a referenced type.</summary>
    /// <param name="metadata">Metadata of the referencing assembly.</param>
    /// <param name="handle">The type reference.</param>
    /// <returns>For example <c>System.Random</c>.</returns>
    private static string GetFullName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var type = metadata.GetTypeReference(handle);
        var name = metadata.GetString(type.Name);
        return type.Namespace.IsNil ? name : metadata.GetString(type.Namespace) + "." + name;
    }

    /// <summary>The <c>Include</c> values of every item of one kind in a project file.</summary>
    /// <param name="projectFile">Parsed project file.</param>
    /// <param name="itemName">Item kind, for example <c>ProjectReference</c>.</param>
    /// <returns>The included paths or package names.</returns>
    private static IEnumerable<string> IncludesOf(XDocument projectFile, string itemName) =>
        projectFile.Descendants(itemName).Select(item => (string?)item.Attribute("Include") ?? string.Empty);

    /// <summary>Project name of a <c>ProjectReference</c> path, which may use either directory separator.</summary>
    /// <param name="include">Referenced project path, for example <c>..\Pantheon.Domain\Pantheon.Domain.csproj</c>.</param>
    /// <returns>For example <c>Pantheon.Domain</c>.</returns>
    private static string GetProjectName(string include) => Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));

    /// <summary>Whether a package or assembly name belongs to Godot's C# API.</summary>
    /// <param name="name">Package, file or assembly name.</param>
    /// <returns><c>true</c> for Godot names such as <c>GodotSharp</c>.</returns>
    private static bool IsGodot(string name) => name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the <c>key = "value"</c> entries of one section of a Godot config file.</summary>
    /// <param name="path">Config file, for example a <c>.gdextension</c> manifest.</param>
    /// <param name="section">Section name without brackets.</param>
    /// <returns>The section's entries, with quotes removed from the values.</returns>
    private static Dictionary<string, string> ReadSection(string path, string section)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentSection = null;
        foreach (var line in File.ReadLines(path).Select(text => text.Trim()))
        {
            if (line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1];
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (currentSection == section && separator > 0)
            {
                entries[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
            }
        }

        return entries;
    }

    /// <summary>
    /// Whether a <c>res://</c> library path exists in the Godot project. macOS libraries are
    /// <c>.framework</c> directories, which must contain the binary.
    /// </summary>
    /// <param name="gameDirectory">The Godot project directory (<c>res://</c>).</param>
    /// <param name="resourcePath">Library path from the manifest.</param>
    /// <returns><c>true</c> when the library file or a non-empty framework directory exists.</returns>
    private static bool LibraryExists(string gameDirectory, string resourcePath)
    {
        if (!resourcePath.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var path = Path.Combine(gameDirectory, resourcePath[ResourcePrefix.Length..]);
        return File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFiles(path).Any());
    }
}
