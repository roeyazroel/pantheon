using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// The house under construction: a ring of scaffold poles and a timber pile, walls that rise with
/// the work done, then the roof. Hidden while no house is under way.
/// </summary>
internal sealed class ConstructionSiteView
{
    // The walls are up at this share of the work; the roof is thatched during the rest.
    private const float WallsDoneAt = 0.7f;
    private const int PoleCount = 6;

    private readonly Node3D _root;
    private readonly MeshInstance3D _wall;
    private readonly MeshInstance3D _roof;

    /// <summary>Creates the hidden building site under <paramref name="parent"/>.</summary>
    /// <param name="parent">The settlement's node.</param>
    /// <param name="assets">Shared meshes.</param>
    public ConstructionSiteView(Node3D parent, SettlementAssets assets)
    {
        _root = new Node3D { Name = "ConstructionSite", Visible = false };
        _wall = new MeshInstance3D { Name = "Wall", Mesh = assets.HutWall };
        _roof = new MeshInstance3D { Name = "Roof", Mesh = assets.HutRoof };
        _root.AddChild(_wall);
        _root.AddChild(_roof);
        _root.AddChild(CreateScaffold(assets));
        _root.AddChild(new MeshInstance3D { Name = "Timber", Mesh = assets.TimberPile, Position = SettlementAssets.TimberPileOffset });
        parent.AddChild(_root);
    }

    /// <summary>Shows the building site at its progress, or hides it.</summary>
    /// <param name="construction">The house under construction, or <c>null</c>.</param>
    public void Show(ConstructionPlacement? construction)
    {
        _root.Visible = construction is not null;
        if (construction is not { } site)
        {
            return;
        }

        var house = site.House;
        _root.Transform = new Transform3D(new Basis(Vector3.Up, house.Yaw).Scaled(Vector3.One * house.Scale), house.Position.ToGodot());
        _wall.Transform = SettlementAssets.PartialWall(site.Progress / WallsDoneAt);
        var roofBuilt = (site.Progress - WallsDoneAt) / (1f - WallsDoneAt);
        _roof.Visible = roofBuilt > 0f;
        _roof.Transform = SettlementAssets.PartialRoof(roofBuilt);
    }

    /// <summary>Scaffold poles evenly spaced around the walls, drawn with one multimesh.</summary>
    private static MultiMeshInstance3D CreateScaffold(SettlementAssets assets)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = assets.ScaffoldPole, InstanceCount = PoleCount };
        for (var i = 0; i < PoleCount; i++)
        {
            var angle = (i + 0.5f) * Mathf.Tau / PoleCount;
            var ring = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SettlementAssets.ScaffoldRadius;
            multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, ring + SettlementAssets.ScaffoldPoleOffset));
        }

        return new MultiMeshInstance3D { Name = "Scaffold", Multimesh = multimesh };
    }
}
