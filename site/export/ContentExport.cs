using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pantheon.SiteExport;

/// <summary>
/// The game's content files as the website reads them: the same values, as strict JSON without the
/// files' comments, so a browser can parse them with <c>JSON.parse</c>.
/// </summary>
internal static class ContentExport
{
    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Writes every content file.</summary>
    /// <param name="dataDirectory"><c>game/Data</c>.</param>
    /// <param name="outDirectory">Where the strict copies go.</param>
    /// <returns>How many files were written.</returns>
    public static int Write(string dataDirectory, string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var files = Directory.GetFiles(dataDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
        foreach (var file in files)
        {
            var node = JsonNode.Parse(File.ReadAllText(file), documentOptions: Lenient)
                ?? throw new InvalidDataException($"{file} is empty.");
            File.WriteAllText(Path.Combine(outDirectory, Path.GetFileName(file)), node.ToJsonString());
        }

        return files.Count;
    }
}
