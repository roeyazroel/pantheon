using Godot;
using Pantheon.Domain.Hand;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>What the Divine Hand holds, drawn (presentation only, ADR-030): a rock, a tree or a few villagers, and their flight when thrown.</summary>
internal sealed partial class HeldThing : Node3D
{
    private const float FlightSeconds = 0.7f;

    private static readonly Color[] Clothes = [new(0.75f, 0.32f, 0.25f), new(0.3f, 0.45f, 0.7f), new(0.85f, 0.7f, 0.35f), new(0.4f, 0.6f, 0.35f)];

    private Vector3 _from;
    private Vector3 _to;
    private float _age = -1f;

    /// <summary>A held thing, sized for a hand of size 1.</summary>
    /// <param name="kind">What it is.</param>
    /// <returns>The node; its name is the kind.</returns>
    public static HeldThing Create(HeldKind kind)
    {
        var thing = new HeldThing { Name = kind.ToString() };
        switch (kind)
        {
            case HeldKind.Rock:
                thing.AddChild(Mesh(new SphereMesh { Radius = 0.42f, Height = 0.7f, RadialSegments = 7, Rings = 4 }, new Color(0.45f, 0.42f, 0.4f), Vector3.Zero));
                break;
            case HeldKind.Tree:
                thing.AddChild(Mesh(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.09f, Height = 0.6f }, new Color(0.4f, 0.27f, 0.15f), new Vector3(0f, -0.3f, 0f)));
                thing.AddChild(Mesh(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.38f, Height = 0.95f }, new Color(0.18f, 0.42f, 0.2f), new Vector3(0f, 0.35f, 0f)));
                break;
            case HeldKind.Villagers:
                for (var i = 0; i < 4; i++)
                {
                    thing.AddChild(Mesh(new CapsuleMesh { Radius = 0.08f, Height = 0.36f }, Clothes[i], new Vector3(-0.27f + (i * 0.18f), 0f, (i % 2) * 0.12f)));
                }

                break;
            case HeldKind.Creature:
                break;
        }

        return thing;
    }

    /// <summary>A thrown thing flying from the hand to where it lands, then gone.</summary>
    /// <param name="kind">What it is.</param>
    /// <param name="from">The hand's position.</param>
    /// <param name="to">Where it lands.</param>
    /// <param name="size">The hand's size.</param>
    /// <returns>The node, ready to be added to the world.</returns>
    public static HeldThing Flight(HeldKind kind, Vector3 from, Vector3 to, float size)
    {
        var thing = Create(kind);
        (thing._from, thing._to, thing._age, thing.Position, thing.Scale) = (from, to, 0f, from, new Vector3(size, size, size));
        return thing;
    }

    /// <summary>Flies along the arc, and is gone when it lands.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        if (_age < 0f)
        {
            return;
        }

        _age += (float)delta;
        var t = Mathf.Clamp(_age / FlightSeconds, 0f, 1f);
        var height = 0.3f * _from.DistanceTo(_to);
        Position = _from.Lerp(_to, t) + new Vector3(0f, height * 4f * t * (1f - t), 0f);
        Rotation = new Vector3(t * 9f, t * 5f, 0f);
        if (t >= 1f)
        {
            QueueFree();
        }
    }

    /// <summary>A plain coloured mesh.</summary>
    private static MeshInstance3D Mesh(Mesh mesh, Color color, Vector3 position) => new()
    {
        Mesh = mesh,
        Position = position,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };
}
