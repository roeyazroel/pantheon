using System.Collections.Generic;

namespace Pantheon.Game.Diagnostics;

/// <summary>Formatted debug overlay content.</summary>
/// <param name="Lines">Lines of text, top to bottom.</param>
public sealed record DebugPanelModel(IReadOnlyList<string> Lines)
{
    /// <summary>Joins the lines with newlines.</summary>
    /// <returns>Overlay text.</returns>
    public string ToText() => string.Join('\n', Lines);
}
