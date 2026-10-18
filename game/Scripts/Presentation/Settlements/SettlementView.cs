using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Nodes of one settlement: the wayshrine, its huts (one multimesh per hut part) and a floating
/// name label. <see cref="Sync"/> follows the population, so growth shows as new huts.
/// </summary>
internal sealed class SettlementView
{
    // The label floats above the shrine's crown.
    private const float LabelHeight = 11f;
    private const int LabelFontSize = 30;
    private const int LabelOutlineSize = 10;

    // With FixedSize the label keeps its on-screen size; this gives about 30 px per line at 900 px height.
    private const float LabelPixelSize = 0.001f;

    private readonly TerrainField _field;
    private readonly MultiMesh _walls;
    private readonly MultiMesh _roofs;
    private readonly MultiMesh _doors;
    private readonly Label3D _label;
    private int _hutCount = -1;

    /// <summary>Creates the settlement's nodes under <paramref name="parent"/> and shows its current state.</summary>
    /// <param name="parent">Node that holds all settlements.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="settlement">Settlement to show.</param>
    /// <param name="field">Terrain the settlement stands on.</param>
    public SettlementView(Node3D parent, SettlementAssets assets, SettlementState settlement, TerrainField field)
    {
        _field = field;
        var root = parent.AddLayer(string.Create(CultureInfo.InvariantCulture, $"Settlement_{settlement.Id}"));
        var center = new Vector3(settlement.Position.X, field.SampleHeight(settlement.Position.X, settlement.Position.Z), settlement.Position.Z);

        AddPart(root, "ShrineBase", assets.ShrineBase, center + SettlementAssets.ShrineBaseOffset);
        AddPart(root, "ShrinePillar", assets.ShrinePillar, center + SettlementAssets.ShrinePillarOffset);
        AddPart(root, "ShrineCrown", assets.ShrineCrown, center + SettlementAssets.ShrineCrownOffset);
        _walls = AddMultiMesh(root, "HutWalls", assets.HutWall);
        _roofs = AddMultiMesh(root, "HutRoofs", assets.HutRoof);
        _doors = AddMultiMesh(root, "HutDoors", assets.HutDoor);
        _label = CreateLabel(center + (Vector3.Up * LabelHeight));
        root.AddChild(_label);

        Sync(settlement);
    }

    /// <summary>Updates the label and, when the hut count changes, re-places the huts.</summary>
    /// <param name="settlement">Current settlement state.</param>
    public void Sync(SettlementState settlement)
    {
        var text = SettlementLabel.Format(settlement);
        if (_label.Text != text)
        {
            _label.Text = text;
        }

        var hutCount = SettlementLayout.ComputeHutCount(settlement);
        if (hutCount == _hutCount)
        {
            return;
        }

        _hutCount = hutCount;
        PlaceHuts(SettlementLayout.ComputeHuts(settlement, _field, hutCount));
    }

    /// <summary>Writes one transform per hut into each part's multimesh.</summary>
    private void PlaceHuts(IReadOnlyList<HutPlacement> huts)
    {
        _walls.InstanceCount = huts.Count;
        _roofs.InstanceCount = huts.Count;
        _doors.InstanceCount = huts.Count;
        for (var i = 0; i < huts.Count; i++)
        {
            var hut = huts[i];
            var basis = new Basis(Vector3.Up, hut.Yaw).Scaled(Vector3.One * hut.Scale);
            var origin = hut.Position.ToGodot();
            _walls.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.WallOffset)));
            _roofs.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.RoofOffset)));
            _doors.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.DoorOffset)));
        }
    }

    /// <summary>Adds a single static mesh.</summary>
    private static void AddPart(Node3D root, string name, Mesh mesh, Vector3 position) =>
        root.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Position = position });

    /// <summary>Adds an empty multimesh for one hut part.</summary>
    private static MultiMesh AddMultiMesh(Node3D root, string name, Mesh mesh)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
        root.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh });
        return multimesh;
    }

    /// <summary>Billboard label with a constant screen size that draws over the terrain.</summary>
    private static Label3D CreateLabel(Vector3 position) => new()
    {
        Name = "Label",
        Position = position,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FixedSize = true,
        PixelSize = LabelPixelSize,
        FontSize = LabelFontSize,
        OutlineSize = LabelOutlineSize,
        OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
        NoDepthTest = true,
        VerticalAlignment = VerticalAlignment.Bottom,
    };
}
