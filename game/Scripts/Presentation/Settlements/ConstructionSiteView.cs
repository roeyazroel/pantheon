using Godot;
using Pantheon.Client.Presentation.Buildings;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// The house under construction (ADR-052): a ring of scaffold poles and a timber pile, and the
/// village's house rising from the ground with the work done. Hidden while no house is under way.
/// </summary>
internal sealed class ConstructionSiteView
{
    private const int PoleCount = 6;

    private readonly Node3D _root;
    private readonly MultiMeshInstance3D _house;
    private readonly BuildingAssets _buildings;

    /// <summary>Creates the hidden building site under <paramref name="parent"/>.</summary>
    /// <param name="parent">The settlement's node.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="buildings">The buildings made in code.</param>
    public ConstructionSiteView(Node3D parent, SettlementAssets assets, BuildingAssets buildings)
    {
        _buildings = buildings;
        _root = new Node3D { Name = "ConstructionSite", Visible = false };
        var multimesh = BuildingAssets.NewMultiMesh();
        multimesh.InstanceCount = 1;
        _house = new MultiMeshInstance3D { Name = "House", Multimesh = multimesh };
        _root.AddChild(_house);
        _root.AddChild(CreateScaffold(assets));
        _root.AddChild(new MeshInstance3D { Name = "Timber", Mesh = assets.TimberPile, Position = SettlementAssets.TimberPileOffset });
        parent.AddChild(_root);
    }

    /// <summary>Builds the house in the village's look.</summary>
    /// <param name="look">The village's look.</param>
    public void Dress(VillageLook look)
    {
        var multimesh = _house.Multimesh;
        multimesh.Mesh = _buildings.House(look.Style, look.TwoStoreys);
        var (walls, roof) = HouseMesh.PatternsOf(look.Style);
        multimesh.SetInstanceTransform(0, Transform3D.Identity);
        multimesh.SetInstanceColor(0, BuildingAssets.InstanceColor(look.Wall));
        multimesh.SetInstanceCustomData(0, BuildingAssets.InstanceCustom(look.Roof, walls, roof));
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
        _house.SetInstanceShaderParameter("built", Mathf.Clamp(site.Progress, 0.02f, 0.999f));
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
