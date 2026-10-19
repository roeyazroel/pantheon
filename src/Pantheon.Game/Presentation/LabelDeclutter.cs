using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation;

/// <summary>How much of a floating label is shown.</summary>
public enum LabelFit
{
    /// <summary>All of it.</summary>
    Full,

    /// <summary>Its short text, such as a village's name alone.</summary>
    Short,

    /// <summary>None of it: a more important label holds its place.</summary>
    Hidden,
}

/// <summary>A floating label on screen: the middle of its bottom edge, its sizes in full and short, in pixels, and how much it matters.</summary>
/// <param name="X">Anchor, from the left.</param>
/// <param name="Y">Anchor, from the top: the label stands above it.</param>
/// <param name="FullWidth">Width of all of it.</param>
/// <param name="FullHeight">Height of all of it.</param>
/// <param name="ShortWidth">Width of its short text.</param>
/// <param name="ShortHeight">Height of its short text.</param>
/// <param name="Priority">The larger, the sooner it is placed.</param>
public readonly record struct ScreenLabel(float X, float Y, float FullWidth, float FullHeight, float ShortWidth, float ShortHeight, float Priority);

/// <summary>
/// Keeps the floating labels of a large world from piling on one another (spec §84, ADR-027): the
/// most important is placed first, each in full where it overlaps none placed before, by its short
/// text where only that fits, and otherwise not at all. Pure: the client measures, this decides.
/// </summary>
public static class LabelDeclutter
{
    /// <summary>Chooses how much of each label to show.</summary>
    /// <param name="labels">The labels on screen.</param>
    /// <returns>Each label's fit, in the same order.</returns>
    public static LabelFit[] Fit(IReadOnlyList<ScreenLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var order = new int[labels.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => labels[b].Priority != labels[a].Priority ? labels[b].Priority.CompareTo(labels[a].Priority) : a.CompareTo(b));
        var placed = new List<Box>(labels.Count);
        var fits = new LabelFit[labels.Count];
        foreach (var i in order)
        {
            var label = labels[i];
            var full = Box.Above(label.X, label.Y, label.FullWidth, label.FullHeight);
            var brief = Box.Above(label.X, label.Y, label.ShortWidth, label.ShortHeight);
            fits[i] = Free(full, placed) ? LabelFit.Full : Free(brief, placed) ? LabelFit.Short : LabelFit.Hidden;
            if (fits[i] != LabelFit.Hidden)
            {
                placed.Add(fits[i] == LabelFit.Full ? full : brief);
            }
        }

        return fits;
    }

    /// <summary>Whether a box overlaps none placed.</summary>
    private static bool Free(Box box, List<Box> placed)
    {
        foreach (var other in placed)
        {
            if (box.Left < other.Right && other.Left < box.Right && box.Top < other.Bottom && other.Top < box.Bottom)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A screen rectangle.</summary>
    private readonly record struct Box(float Left, float Top, float Right, float Bottom)
    {
        /// <summary>A box of a size standing centred on an anchor.</summary>
        public static Box Above(float x, float y, float width, float height) => new(x - (width / 2f), y - height, x + (width / 2f), y);
    }
}
