using Godot;
using Pantheon.Client.Presentation.Land;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// A village's worn ground (ADR-077): its green, the lanes its houses stand along and the paths from
/// their doors, drawn as the roads are, in one mesh that follows the houses.
/// </summary>
internal sealed class LaneView
{
    // A path to a door is this share of a lane's width.
    private const float PathShare = 0.6f;

    // A lane gives out where the ground falls this far across it, as a share of its width.
    private const float SteepestCamber = 0.7f;

    private readonly SettlementLayoutSettings _layout;
    private readonly TerrainField _terrain;
    private readonly MeshInstance3D _node;

    /// <summary>Creates the empty lanes under <paramref name="root"/>.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="layout">Layout settings.</param>
    /// <param name="terrain">Terrain they lie on.</param>
    public LaneView(Node3D root, SettlementLayoutSettings layout, TerrainField terrain)
    {
        (_layout, _terrain) = (layout, terrain);
        _node = new MeshInstance3D { Name = "Lanes", MaterialOverride = RoadMesh.Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(_node);
    }

    /// <summary>Draws the village's lanes as they stand now.</summary>
    /// <param name="site">The village's layout.</param>
    public void Show(SettlementSite site)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetNormal(Vector3.Up);
        var vertices = 0;
        RoadMesh.Disc(tool, new System.Numerics.Vector2(site.Center.X, site.Center.Z), _layout.GreenRadius, (x, z) => Mathf.Max(_terrain.SampleHeight(x, z), 0f), ref vertices);
        var seed = 0;
        foreach (var lane in VillagePlan.Lanes(site, _layout))
        {
            RoadMesh.Add(tool, RoadLayout.Strip(lane, _layout.LaneWidth, seed++, _terrain), ref vertices, Unfit);
        }

        foreach (var path in VillagePlan.Paths(site, _layout))
        {
            RoadMesh.Add(tool, RoadLayout.Strip(path, _layout.LaneWidth * PathShare, seed++, _terrain), ref vertices, Unfit);
        }

        _node.Mesh = tool.Commit();
    }

    /// <summary>Whether a lane gives out on a stretch: in water, or across a slope too steep to wear a track into.</summary>
    private bool Unfit(RoadSection a, RoadSection b) =>
        BridgeLayout.IsWet(_terrain, a.Centre) || BridgeLayout.IsWet(_terrain, b.Centre) || Mathf.Abs(a.Left.Y - a.Right.Y) > _layout.LaneWidth * SteepestCamber;
}
