using System.Linq;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>A settlement's temple and walls (ADR-023): shown once built, laid out by <see cref="CivicLayout"/>.</summary>
internal sealed class CivicView
{
    private static readonly Vector3 HallSize = new(5.5f, 3f, 4f);
    private static readonly Vector3 RoofSize = new(6f, 2f, 4.6f);

    private readonly MeshInstance3D _hall;
    private readonly MeshInstance3D _roof;
    private readonly MultiMesh _posts;
    private readonly StandardMaterial3D _roofMaterial = new() { Roughness = 0.7f };
    private string _shown = string.Empty;

    /// <summary>Creates the (hidden) temple and walls under a settlement's root.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="assets">Shared meshes and materials.</param>
    public CivicView(Node3D root, SettlementAssets assets)
    {
        _hall = new MeshInstance3D { Name = "TempleHall", Mesh = new BoxMesh { Size = HallSize, Material = assets.Stone }, Visible = false };
        _roof = new MeshInstance3D { Name = "TempleRoof", Mesh = new PrismMesh { Size = RoofSize, Material = _roofMaterial }, Visible = false };
        _posts = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.22f, Height = 2.6f, RadialSegments = 6, Rings = 1, Material = assets.Timber } };
        root.AddChild(_hall);
        root.AddChild(_roof);
        root.AddChild(new MultiMeshInstance3D { Name = "Walls", Multimesh = _posts });
    }

    /// <summary>Shows the temple and walls a settlement has built; rebuilt only when they or the layout change.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="site">Its layout.</param>
    /// <param name="terrain">Terrain.</param>
    /// <param name="templeColor">The colour of the god its temple is raised to.</param>
    public void Show(SettlementState settlement, SettlementSite site, TerrainField terrain, Color? templeColor)
    {
        var buildings = settlement.Society.Buildings;
        var temple = buildings.FirstOrDefault(b => b.Kind == BuildingKind.Temple);
        var walls = buildings.Any(b => b.Kind == BuildingKind.Walls);
        var key = $"{temple?.GodId}:{walls}:{site.HouseDiscRadius:0.0}";
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        _hall.Visible = _roof.Visible = temple is not null;
        if (temple is not null)
        {
            var (place, yaw) = CivicLayout.Temple(site, terrain, settlement.Id);
            var spot = place.ToGodot();
            var turn = new Basis(Vector3.Up, yaw);
            _hall.Transform = new Transform3D(turn, spot + new Vector3(0f, (HallSize.Y * 0.5f) - 0.3f, 0f));
            _roof.Transform = new Transform3D(turn, spot + new Vector3(0f, HallSize.Y - 0.3f + (RoofSize.Y * 0.5f), 0f));
            _roofMaterial.AlbedoColor = templeColor ?? new Color(0.8f, 0.7f, 0.4f);
        }

        var posts = walls ? CivicLayout.Walls(site, terrain) : [];
        _posts.InstanceCount = posts.Count;
        for (var i = 0; i < posts.Count; i++)
        {
            _posts.SetInstanceTransform(i, new Transform3D(Basis.Identity, posts[i].ToGodot() + new Vector3(0f, 1.1f, 0f)));
        }
    }
}
