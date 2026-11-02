using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Client.Presentation.Buildings;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// The built part of a settlement: houses in the village's style (ADR-052, ADR-077, one multimesh per
/// variant), its lanes, the building site and the fields (one multimesh tinted per crop). Instances
/// are only rewritten when the layout or the village's look changes.
/// </summary>
internal sealed class SiteView
{
    private readonly MultiMesh[] _houses = new MultiMesh[HouseMesh.Variants];
    private readonly LaneView _lanes;
    private readonly HearthView _hearths;
    private readonly BuildingAssets _buildings;
    private readonly int _settlementId;
    private VillageLook? _look;
    private readonly FieldView _fields;
    private readonly MultiMesh _rubble;
    private readonly ConstructionSiteView _construction;
    private IReadOnlyList<HutPlacement> _shownHouses = [];
    private IReadOnlyList<FieldPlacement> _shownFields = [];
    private IReadOnlyList<HutPlacement> _shownRuins = [];

    /// <summary>Creates the empty houses, fields and building site under <paramref name="root"/>.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="layout">Layout settings, for the field size and crop colours.</param>
    /// <param name="buildings">The buildings made in code.</param>
    /// <param name="settlementId">The settlement, for each house's colours.</param>
    /// <param name="terrain">Terrain, which its lanes lie on.</param>
    /// <param name="center">The village's middle.</param>
    public SiteView(Node3D root, SettlementAssets assets, SettlementLayoutSettings layout, BuildingAssets buildings, int settlementId, Pantheon.Simulation.Terrain.TerrainField terrain, Vector3 center)
    {
        (_buildings, _settlementId) = (buildings, settlementId);
        for (var variant = 0; variant < _houses.Length; variant++)
        {
            _houses[variant] = BuildingAssets.NewMultiMesh();
            root.AddChild(new MultiMeshInstance3D { Name = "Houses" + variant, Multimesh = _houses[variant] });
        }

        _lanes = new LaneView(root, layout, terrain);
        _hearths = new HearthView(root, center);
        _fields = new FieldView(root, layout, terrain);
        _rubble = AddMultiMesh(root, "Ruins", assets.Rubble, useColors: false);
        _construction = new ConstructionSiteView(root, assets, buildings);
    }

    /// <summary>Dresses the houses and the building site in the village's look, when it changed.</summary>
    /// <param name="look">The village's look.</param>
    public void Dress(VillageLook look)
    {
        if (look == _look)
        {
            return;
        }

        _look = look;
        for (var variant = 0; variant < _houses.Length; variant++)
        {
            _houses[variant].Mesh = _buildings.House(look.Style, look.TwoStoreys, variant);
        }

        PlaceHouses(_shownHouses);
        _construction.Dress(look);
    }

    /// <summary>Shows a layout: houses and fields when they changed, and the building site's progress.</summary>
    /// <param name="site">Current layout.</param>
    public void Show(SettlementSite site)
    {
        if (!site.Houses.SequenceEqual(_shownHouses))
        {
            PlaceHouses(site.Houses);
            _shownHouses = site.Houses;
            _lanes.Show(site);
        }

        if (!site.Fields.SequenceEqual(_shownFields))
        {
            _fields.Show(site.Fields);
            _shownFields = site.Fields;
        }

        if (!site.Ruins.SequenceEqual(_shownRuins))
        {
            PlaceRuins(site.Ruins);
            _shownRuins = site.Ruins;
        }

        _construction.Show(site.Construction);
    }

    /// <summary>Thickens or thins the village's smoke for the hour.</summary>
    /// <param name="camera">The camera's place.</param>
    public void Breathe(Vector3 camera) => _hearths.Update(camera);

    /// <summary>Writes one low, skewed heap per ruined house.</summary>
    private void PlaceRuins(IReadOnlyList<HutPlacement> ruins)
    {
        _rubble.InstanceCount = ruins.Count;
        for (var i = 0; i < ruins.Count; i++)
        {
            var ruin = ruins[i];
            var basis = new Basis(Vector3.Up, ruin.Yaw).Rotated(Vector3.Right, 0.12f).Scaled(Vector3.One * ruin.Scale);
            _rubble.SetInstanceTransform(i, new Transform3D(basis, ruin.Position.ToGodot() + (Vector3.Up * 0.3f)));
        }
    }

    /// <summary>Writes each house's place, its walls' and roof's colours in its own shade, and its patterns.</summary>
    private void PlaceHouses(IReadOnlyList<HutPlacement> houses)
    {
        if (_look is not { } look)
        {
            return;
        }

        _hearths.Show(houses, HouseMesh.SmokeHeight(look.Style, look.TwoStoreys));
        var (walls, roof) = HouseMesh.PatternsOf(look.Style);
        var written = new int[_houses.Length];
        for (var variant = 0; variant < _houses.Length; variant++)
        {
            _houses[variant].InstanceCount = houses.Count(house => house.Variant == variant);
        }

        for (var i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            var multimesh = _houses[house.Variant];
            var at = written[house.Variant]++;
            var shade = VillageArchitecture.ShadeOf(_buildings.Settings, _settlementId, i);
            var (wallColor, roofColor) = VillageArchitecture.ColorsOf(look, _buildings.Settings, _settlementId, i);
            multimesh.SetInstanceTransform(at, new Transform3D(new Basis(Vector3.Up, house.Yaw).Scaled(Vector3.One * house.Scale), house.Position.ToGodot()));
            multimesh.SetInstanceColor(at, BuildingAssets.InstanceColor(wallColor, shade));
            multimesh.SetInstanceCustomData(at, BuildingAssets.InstanceCustom(roofColor, walls, roof, 2f - shade));
        }
    }

    /// <summary>Adds an empty multimesh.</summary>
    private static MultiMesh AddMultiMesh(Node3D root, string name, Mesh mesh, bool useColors)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = useColors, Mesh = mesh };
        root.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh });
        return multimesh;
    }
}
