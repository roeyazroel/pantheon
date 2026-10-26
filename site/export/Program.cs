using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Game.Minds;

namespace Pantheon.SiteExport;

/// <summary>
/// Writes the website's data (ADR-076) from the game's own code and content:
/// <list type="bullet">
/// <item><c>data/</c>: every content file of <c>game/Data</c>, as strict JSON;</item>
/// <item><c>islands/</c>: the islands the world generator makes for a few seeds;</item>
/// <item><c>fixtures/</c>: what the scribes and the angel answer, for the JavaScript ports' tests.</item>
/// </list>
/// </summary>
internal static class Program
{
    // The islands the site shows: the README's seed first.
    private static readonly string[] DefaultIslands = ["1234", "7", "first light"];

    /// <summary>Runs the export.</summary>
    /// <param name="args"><c>--data &lt;dir&gt; --out &lt;dir&gt; [--islands &lt;seed&gt;,...]</c>.</param>
    /// <returns>0 when written, 2 for a usage error.</returns>
    private static int Main(string[] args)
    {
        if (!TryRead(args, out var dataDirectory, out var outDirectory, out var islands))
        {
            Console.Error.WriteLine("Usage: Pantheon.SiteExport --data <game/Data> --out <directory> [--islands <seed>,<seed>,...]");
            return 2;
        }

        string Read(string name) => File.ReadAllText(Path.Combine(dataDirectory, name));
        var content = GameContent.Parse(GameContentFiles.Read(Read));
        var minds = MindContent.Parse(Read(MindContent.FileName));
        var angel = AngelContent.Parse(Read(AngelContent.FileName));

        var written = ContentExport.Write(dataDirectory, Path.Combine(outDirectory, "data"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Content: {written} files as strict JSON"));

        var islandDirectory = Path.Combine(outDirectory, "islands");
        Directory.CreateDirectory(islandDirectory);
        var index = new List<IslandExport.Summary>();
        foreach (var text in islands)
        {
            var summary = IslandExport.Write(content, minds, WorldSeed.Parse(text), text, islandDirectory);
            index.Add(summary);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Island: seed {text}, {summary.Settlements} villages, {summary.Gods} gods"));
        }

        IslandExport.WriteIndex(index, islandDirectory);

        var fixtureDirectory = Path.Combine(outDirectory, "fixtures");
        Directory.CreateDirectory(fixtureDirectory);
        var cases = FixtureExport.WriteScribes(content, minds, angel, Path.Combine(fixtureDirectory, "scribes.json"));
        var looks = FixtureExport.WriteAngel(content, angel, Path.Combine(fixtureDirectory, "angel.json"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Fixtures: {cases} pantheons, {looks} angels"));
        return 0;
    }

    /// <summary>Reads the options.</summary>
    private static bool TryRead(string[] args, out string data, out string output, out IReadOnlyList<string> islands)
    {
        data = string.Empty;
        output = string.Empty;
        islands = DefaultIslands;
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--data":
                    data = args[i + 1];
                    break;
                case "--out":
                    output = args[i + 1];
                    break;
                case "--islands":
                    islands = args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                default:
                    return false;
            }
        }

        return args.Length % 2 == 0 && data.Length > 0 && output.Length > 0 && islands.Count > 0 && Directory.Exists(data);
    }
}
