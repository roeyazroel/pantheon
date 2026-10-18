using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// The built part of a settlement: houses (one multimesh per hut part), the building site and the
/// fields (one multimesh tinted per crop). Instances are only rewritten when the layout changes.
/// </summary>
internal sealed class SiteView
{
    private readonly MultiMesh _walls;
    private readonly MultiMesh _roofs;
    private readonly MultiMesh _doors;
    private readonly MultiMesh _fields;
    private readonly MultiMesh _rubble;
    private readonly ConstructionSiteView _construction;
    private readonly float _fieldSize;
    private readonly Color[] _cropColors;
    private IReadOnlyList<HutPlacement> _shownHouses = [];
    private IReadOnlyList<FieldPlacement> _shownFields = [];
    private IReadOnlyList<HutPlacement> _shownRuins = [];

    /// <summary>Creates the empty houses, fields and building site under <paramref name="root"/>.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="layout">Layout settings, for the field size and crop colours.</param>
    public SiteView(Node3D root, SettlementAssets assets, SettlementLayoutSettings layout)
    {
        _walls = AddMultiMesh(root, "HutWalls", assets.HutWall, useColors: false);
        _roofs = AddMultiMesh(root, "HutRoofs", assets.HutRoof, useColors: false);
        _doors = AddMultiMesh(root, "HutDoors", assets.HutDoor, useColors: false);
        _fields = AddMultiMesh(root, "Fields", assets.FieldPlot, useColors: true);
        _rubble = AddMultiMesh(root, "Ruins", assets.Rubble, useColors: false);
        _construction = new ConstructionSiteView(root, assets);
        _fieldSize = layout.FieldSize;
        _cropColors = layout.CropColors.Select(color => color.ToGodot()).ToArray();
    }

    /// <summary>Shows a layout: houses and fields when they changed, and the building site's progress.</summary>
    /// <param name="site">Current layout.</param>
    public void Show(SettlementSite site)
    {
        if (!site.Houses.SequenceEqual(_shownHouses))
        {
            PlaceHouses(site.Houses);
            _shownHouses = site.Houses;
        }

        if (!site.Fields.SequenceEqual(_shownFields))
        {
            PlaceFields(site.Fields);
            _shownFields = site.Fields;
        }

        if (!site.Ruins.SequenceEqual(_shownRuins))
        {
            PlaceRuins(site.Ruins);
            _shownRuins = site.Ruins;
        }

        _construction.Show(site.Construction);
    }

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

    /// <summary>Writes one transform per house into each part's multimesh.</summary>
    private void PlaceHouses(IReadOnlyList<HutPlacement> houses)
    {
        _walls.InstanceCount = houses.Count;
        _roofs.InstanceCount = houses.Count;
        _doors.InstanceCount = houses.Count;
        for (var i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            var basis = new Basis(Vector3.Up, house.Yaw).Scaled(Vector3.One * house.Scale);
            var origin = house.Position.ToGodot();
            _walls.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.WallOffset)));
            _roofs.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.RoofOffset)));
            _doors.SetInstanceTransform(i, new Transform3D(basis, origin + (basis * SettlementAssets.DoorOffset)));
        }
    }

    /// <summary>Writes one tilted, crop-coloured slab per field.</summary>
    private void PlaceFields(IReadOnlyList<FieldPlacement> fields)
    {
        _fields.InstanceCount = fields.Count;
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var up = field.Normal.ToGodot();
            var right = up.Cross(new Vector3(Mathf.Sin(field.Yaw), 0f, Mathf.Cos(field.Yaw))).Normalized();
            var forward = right.Cross(up);
            var basis = new Basis(right * _fieldSize, up, forward * _fieldSize);
            _fields.SetInstanceTransform(i, new Transform3D(basis, field.Position.ToGodot() + (up * SettlementAssets.FieldCenterLift)));
            _fields.SetInstanceColor(i, CropColor(field.Variant));
        }
    }

    /// <summary>The crop colour a field's variant picks.</summary>
    private Color CropColor(float variant) => _cropColors[Math.Clamp((int)(variant * _cropColors.Length), 0, _cropColors.Length - 1)];

    /// <summary>Adds an empty multimesh.</summary>
    private static MultiMesh AddMultiMesh(Node3D root, string name, Mesh mesh, bool useColors)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = useColors, Mesh = mesh };
        root.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh });
        return multimesh;
    }
}
