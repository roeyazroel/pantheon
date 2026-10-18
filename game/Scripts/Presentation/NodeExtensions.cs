using Godot;

namespace Pantheon.Client.Presentation;

/// <summary>Scene-tree helpers for presenters.</summary>
internal static class NodeExtensions
{
    /// <summary>
    /// Removes and frees every child. Removal is immediate, so old visuals never render in the
    /// same frame as the ones that replace them.
    /// </summary>
    /// <param name="node">Parent node.</param>
    public static void FreeChildren(this Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Creates an empty, named child container.</summary>
    /// <param name="parent">Parent node.</param>
    /// <param name="name">Child name.</param>
    /// <returns>The new child.</returns>
    public static Node3D AddLayer(this Node3D parent, string name)
    {
        var layer = new Node3D { Name = name };
        parent.AddChild(layer);
        return layer;
    }
}
