using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Client.Presentation.Buildings;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// A settlement's temple and walls (ADR-023, ADR-052), shown once built and laid out by
/// <see cref="CivicLayout"/>: the temple in the style of the god it is raised to, under that god's
/// colour, and the walls as a palisade of stakes with timber towers at the gates, or crenellated
/// stone with stone towers where the village builds in stone or follows a god of learning.
/// </summary>
internal sealed class CivicView
{
    // A palisade's stakes stand this far apart, in metres.
    private const float StakeSpacing = 0.36f;

    private static readonly RgbColor TempleStone = RgbColor.ParseHex("#cfc8ba");

    private readonly BuildingAssets _buildings;
    private readonly MultiMeshInstance3D _temple;
    private readonly MultiMesh _wall;
    private readonly MultiMesh _towers;
    private string _shown = string.Empty;

    /// <summary>Creates the (hidden) temple and walls under a settlement's root.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="buildings">The buildings made in code.</param>
    public CivicView(Node3D root, BuildingAssets buildings)
    {
        _buildings = buildings;
        var temple = BuildingAssets.NewMultiMesh();
        temple.InstanceCount = 1;
        _temple = new MultiMeshInstance3D { Name = "Temple", Multimesh = temple, Visible = false };
        _wall = BuildingAssets.NewMultiMesh();
        _towers = BuildingAssets.NewMultiMesh();
        root.AddChild(_temple);
        root.AddChild(new MultiMeshInstance3D { Name = "Walls", Multimesh = _wall });
        root.AddChild(new MultiMeshInstance3D { Name = "Towers", Multimesh = _towers });
    }

    /// <summary>Shows the temple and walls a settlement has built; rebuilt only when they, the layout or the look change.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="site">Its layout.</param>
    /// <param name="terrain">Terrain.</param>
    /// <param name="templeColor">The colour of the god its temple is raised to.</param>
    /// <param name="look">The village's look, for the walls' material and the temple's style.</param>
    /// <param name="templeStyle">The style of the god the temple is raised to, if strongly felt.</param>
    public void Show(SettlementState settlement, SettlementSite site, TerrainField terrain, RgbColor? templeColor, VillageLook look, StyleMotif? templeStyle)
    {
        var buildings = settlement.Society.Buildings;
        var temple = buildings.FirstOrDefault(b => b.Kind == BuildingKind.Temple);
        var walls = buildings.Any(b => b.Kind == BuildingKind.Walls);
        var key = $"{temple?.GodId}:{templeColor}:{templeStyle}:{walls}:{look}:{site.HouseDiscRadius:0.0}";
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        _temple.Visible = temple is not null;
        if (temple is not null)
        {
            var (place, yaw) = CivicLayout.Temple(site, terrain, settlement.Id);
            var multimesh = _temple.Multimesh;
            multimesh.Mesh = _buildings.Temple(templeStyle);
            multimesh.SetInstanceTransform(0, new Transform3D(new Basis(Vector3.Up, yaw), place.ToGodot()));
            var stone = templeStyle == StyleMotif.Monument ? _buildings.Settings.MonumentWall : templeStyle == StyleMotif.Lore ? _buildings.Settings.LoreWall : TempleStone;
            multimesh.SetInstanceColor(0, BuildingAssets.InstanceColor(stone));
            multimesh.SetInstanceCustomData(0, BuildingAssets.InstanceCustom(templeColor ?? RgbColor.ParseHex("#c9b066"), SurfacePattern.Stone, templeStyle == StyleMotif.Garden ? SurfacePattern.Daub : SurfacePattern.Slate));
        }

        ShowWalls(walls, site, terrain, StoneBuilt(look));
    }

    /// <summary>Whether a village walls itself in stone: where it builds in stone or adobe, or follows a god of learning.</summary>
    private static bool StoneBuilt(VillageLook look) => look.Style is HouseStyle.StoneCottage or HouseStyle.Adobe || look.Motif == StyleMotif.Lore;

    /// <summary>The palisade or stone walls, and the towers by the gates.</summary>
    private void ShowWalls(bool built, SettlementSite site, TerrainField terrain, bool stone)
    {
        var settings = _buildings.Settings;
        var posts = built ? CivicLayout.Walls(site, terrain, stone ? CivicLayout.PostSpacing : StakeSpacing) : [];
        _wall.Mesh = stone ? _buildings.WallSegment : _buildings.Stake;
        _wall.InstanceCount = posts.Count;
        var (wallColor, pattern) = stone ? (settings.Stone, SurfacePattern.Stone) : (settings.Trim, SurfacePattern.Planks);
        for (var i = 0; i < posts.Count; i++)
        {
            var post = posts[i];
            // Stakes vary a little in height and lean, as cut and driven by hand.
            var (height, lean) = stone ? (1f, 0f) : (0.85f + (0.3f * Hash(i, 1)), (Hash(i, 2) - 0.5f) * 0.12f);
            var basis = new Basis(Vector3.Up, CivicLayout.AlongRing(site, post)).Rotated(Vector3.Forward, lean).Scaled(new Vector3(1f, height, 1f));
            _wall.SetInstanceTransform(i, new Transform3D(basis, post.ToGodot()));
            _wall.SetInstanceColor(i, BuildingAssets.InstanceColor(wallColor));
            _wall.SetInstanceCustomData(i, BuildingAssets.InstanceCustom(settings.Stone, pattern, SurfacePattern.Stone));
        }

        IReadOnlyList<System.Numerics.Vector3> towers = built ? CivicLayout.Towers(site, terrain) : [];
        _towers.Mesh = _buildings.Tower(stone);
        _towers.InstanceCount = towers.Count;
        for (var i = 0; i < towers.Count; i++)
        {
            _towers.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, CivicLayout.AlongRing(site, towers[i])), towers[i].ToGodot()));
            _towers.SetInstanceColor(i, BuildingAssets.InstanceColor(stone ? settings.Stone : settings.Trim));
            _towers.SetInstanceCustomData(i, BuildingAssets.InstanceCustom(stone ? settings.Styles[HouseStyle.StoneCottage].Roofs[0] : settings.Styles[HouseStyle.Longhouse].Roofs[0], SurfacePattern.Stone, stone ? SurfacePattern.Slate : SurfacePattern.Thatch));
        }
    }

    private static float Hash(int index, ulong salt) => PresentationHash.Unit(523, (ulong)index, salt);
}
