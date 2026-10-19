using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation;

/// <summary>
/// Measures every <see cref="DeclutteredLabel"/> on screen and shows each as <see cref="LabelDeclutter"/>
/// decides (spec §84, ADR-027), so a large world seen from afar is not buried in names.
/// </summary>
internal static class LabelDeclutterer
{
    /// <summary>Fits the labels to the screen for this frame.</summary>
    /// <param name="tree">The scene tree.</param>
    /// <param name="camera">The camera they are seen through.</param>
    public static void Apply(SceneTree tree, Camera3D camera)
    {
        var height = camera.GetViewport().GetVisibleRect().Size.Y;

        // A fixed-size label's font pixel covers the same share of the screen at any distance.
        var perFontPixel = height / (2f * MathF.Tan(Mathf.DegToRad(camera.Fov) / 2f));
        var labels = new List<DeclutteredLabel>();
        var onScreen = new List<ScreenLabel>();
        foreach (var node in tree.GetNodesInGroup(DeclutteredLabel.Group))
        {
            if (node is not DeclutteredLabel label || label.GetParentNode3D() is { } parent && !parent.IsVisibleInTree())
            {
                continue;
            }

            var position = label.GlobalPosition;
            if (camera.IsPositionBehind(position))
            {
                label.ShowFit(LabelFit.Hidden);
                continue;
            }

            // A label faded out by its distance takes no place on screen.
            if (label.VisibilityRangeEnd > 0f && camera.GlobalPosition.DistanceTo(position) > label.VisibilityRangeEnd)
            {
                continue;
            }

            var anchor = camera.UnprojectPosition(position);
            var scale = label.PixelSize * label.GlobalBasis.Scale.Y * perFontPixel;
            var (full, brief) = (label.FullSize * scale, label.ShortSize * scale);
            labels.Add(label);
            onScreen.Add(new ScreenLabel(anchor.X, anchor.Y, full.X, full.Y, brief.X, brief.Y, label.Priority));
        }

        var fits = LabelDeclutter.Fit(onScreen);
        for (var i = 0; i < labels.Count; i++)
        {
            labels[i].ShowFit(fits[i]);
        }
    }
}
