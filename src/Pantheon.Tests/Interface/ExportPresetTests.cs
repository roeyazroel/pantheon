using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>The export presets cover spec §62's platforms and carry the game's data (ADR-031).</summary>
public sealed class ExportPresetTests
{
    [Fact]
    public void ThePresets_ExportWindowsMacOsAndLinux_WithTheirData_OutsideTheRepository()
    {
        var text = File.ReadAllText(RepositoryRoot.Combine("game", "export_presets.cfg"));
        var presets = Regex.Matches(text, @"\[preset\.\d+\]\s*\n\s*name=""(?<name>[^""]+)""\s*\nplatform=""(?<platform>[^""]+)""").Select(m => m.Groups["platform"].Value).ToList();

        Assert.Equal(["Windows Desktop", "macOS", "Linux"], presets);
        Assert.Equal(3, Regex.Matches(text, @"include_filter=""Data/\*\.json""").Count);
        Assert.All(Regex.Matches(text, @"export_path=""(?<path>[^""]+)""").Select(m => m.Groups["path"].Value), path => Assert.StartsWith("../build/", path, StringComparison.Ordinal));
        Assert.Contains("binary_format/architecture=\"universal\"", text, StringComparison.Ordinal);
        Assert.Contains("textures/vram_compression/import_etc2_astc=true", File.ReadAllText(RepositoryRoot.Combine("game", "project.godot")), StringComparison.Ordinal);
    }
}
