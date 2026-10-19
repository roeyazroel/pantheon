using System;
using Godot;
using Pantheon.Domain.HolySites;

namespace Pantheon.Client.Presentation.HolySites;

/// <summary>
/// One holy site in the world (spec §51, ADR-021): a shape for its kind (an obelisk on the peak, a
/// great tree, a pool, a ring of stones) around a glowing crystal in its keeper's colour, white while
/// nobody keeps it, that flickers towards the claimant's colour while it is being taken; and a label.
/// </summary>
internal sealed partial class HolySiteView : Node3D
{
    private static readonly Color Stone = new(0.55f, 0.53f, 0.5f);
    private static readonly Color Unclaimed = new(0.92f, 0.92f, 0.86f);

    private readonly StandardMaterial3D _crystal;
    private readonly OmniLight3D _light;
    private readonly DeclutteredLabel _label;
    private float _age;
    private Color _keeper = Unclaimed;
    private Color? _claimant;

    private HolySiteView(float crystalHeight)
    {
        _crystal = new StandardMaterial3D { AlbedoColor = Unclaimed, EmissionEnabled = true, Emission = Unclaimed, EmissionEnergyMultiplier = 1.2f, Roughness = 0.2f };
        AddChild(new MeshInstance3D { Name = "Crystal", Mesh = new SphereMesh { Radius = 2f, Height = 5.2f, RadialSegments = 6, Rings = 3, Material = _crystal }, Position = new Vector3(0f, crystalHeight, 0f) });
        _light = new OmniLight3D { LightColor = Unclaimed, LightEnergy = 1.5f, OmniRange = 30f, Position = new Vector3(0f, crystalHeight, 0f) };
        AddChild(_light);
        _label = new DeclutteredLabel
        {
            // Holy sites are few and matter to every god: they take their place before any village.
            Priority = float.MaxValue,
            Name = "Label",
            Position = new Vector3(0f, crystalHeight + 5f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.0011f,
            FontSize = 22,
            OutlineSize = 8,
            OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Modulate = new Color(1f, 0.92f, 0.7f),
        };
        AddChild(_label);
    }

    /// <summary>Creates a site's look.</summary>
    /// <param name="kind">Its kind.</param>
    /// <param name="ground">Its ground point.</param>
    /// <returns>The view, ready to be added to the tree.</returns>
    public static HolySiteView Create(HolySiteKind kind, Vector3 ground)
    {
        var stone = new StandardMaterial3D { AlbedoColor = Stone, Roughness = 0.9f };
        var view = kind switch
        {
            HolySiteKind.SacredPeak => new HolySiteView(15f),
            HolySiteKind.AncientGrove => new HolySiteView(25f),
            HolySiteKind.HolySpring => new HolySiteView(4f),
            HolySiteKind.StandingStones => new HolySiteView(6f),
        };
        view.Name = "HolySite_" + kind;
        view.Position = ground;
        view.Scale = new Vector3(1.5f, 1.5f, 1.5f);
        switch (kind)
        {
            case HolySiteKind.SacredPeak:
                view.AddPart(new CylinderMesh { TopRadius = 0.4f, BottomRadius = 2.2f, Height = 12f, RadialSegments = 4, Material = stone }, new Vector3(0f, 6f, 0f));
                break;
            case HolySiteKind.AncientGrove:
                var bark = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.25f, 0.16f), Roughness = 0.95f };
                var leaves = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.45f, 0.2f), Roughness = 0.9f };
                view.AddPart(new CylinderMesh { TopRadius = 1.4f, BottomRadius = 2.4f, Height = 12f, Material = bark }, new Vector3(0f, 6f, 0f));
                view.AddPart(new SphereMesh { Radius = 8f, Height = 11f, Material = leaves }, new Vector3(0f, 15f, 0f));
                break;
            case HolySiteKind.HolySpring:
                var water = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.65f, 0.9f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.2f, 0.45f, 0.7f), EmissionEnergyMultiplier = 0.6f };
                view.AddPart(new CylinderMesh { TopRadius = 7f, BottomRadius = 7f, Height = 0.4f, Material = water }, new Vector3(0f, 0.4f, 0f));
                view.AddRing(stone, 9, 8f, 1.4f);
                break;
            case HolySiteKind.StandingStones:
                view.AddRing(stone, 9, 9f, 5f);
                break;
        }

        return view;
    }

    /// <summary>Shows who keeps the site and who is taking it.</summary>
    /// <param name="text">The label.</param>
    /// <param name="keeper">The keeper's colour, or <c>null</c> when nobody keeps it.</param>
    /// <param name="claimant">The claimant's colour, or <c>null</c> when nobody is taking it.</param>
    public void Show(string text, Color? keeper, Color? claimant)
    {
        _label.SetTexts(text, text.Split('\n')[0]);
        _keeper = keeper ?? Unclaimed;
        _claimant = claimant;
    }

    /// <summary>Glows in the keeper's colour, flickering towards the claimant's.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _age += (float)delta;
        var color = _claimant is { } claimant ? _keeper.Lerp(claimant, 0.5f + (0.5f * MathF.Sin(_age * 4f))) : _keeper;
        _crystal.AlbedoColor = color;
        _crystal.Emission = color;
        _light.LightColor = color;
        _light.LightEnergy = 1.5f + (0.5f * MathF.Sin(_age * 1.5f));
    }

    /// <summary>Adds a static mesh.</summary>
    private void AddPart(Mesh mesh, Vector3 position) => AddChild(new MeshInstance3D { Mesh = mesh, Position = position });

    /// <summary>A ring of standing stones.</summary>
    private void AddRing(StandardMaterial3D stone, int count, float radius, float height)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count;
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.4f, height, 0.8f), Material = stone },
                Position = new Vector3(MathF.Cos(angle) * radius, height / 2f, MathF.Sin(angle) * radius),
                Rotation = new Vector3(0f, -angle, 0f),
            });
        }
    }
}
