using Godot;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Procedural meshes shared by every settlement: a round hut (wall, thatched cone roof, door) and
/// the central wayshrine. Offsets place each part relative to a point on the ground.
/// </summary>
internal sealed class SettlementAssets
{
    private const string MaterialDirectory = "res://Materials/";
    private const int RadialSegments = 12;

    // Hut walls reach this far below the ground point so huts on slopes never float.
    private const float WallSink = 0.6f;
    private const float WallRadius = 2f;
    private const float WallHeight = 3f;
    private const float RoofRadius = 2.8f;
    private const float RoofHeight = 2.4f;
    private const float RoofOverlap = 0.3f;
    private static readonly Vector3 DoorSize = new(0.9f, 1.7f, 0.3f);

    private const float ShrineSink = 0.6f;
    private const float ShrineBaseHeight = 1.6f;
    private const float ShrinePillarHeight = 7f;
    private const float ShrineCrownRadius = 1f;

    /// <summary>Loads the materials and builds the meshes.</summary>
    public SettlementAssets()
    {
        var wall = LoadMaterial("hut_wall");
        var stone = LoadMaterial("shrine_stone");
        HutWall = new CylinderMesh { TopRadius = WallRadius, BottomRadius = WallRadius * 1.05f, Height = WallHeight, RadialSegments = RadialSegments, Rings = 1, Material = wall };
        HutRoof = new CylinderMesh { TopRadius = 0f, BottomRadius = RoofRadius, Height = RoofHeight, RadialSegments = RadialSegments, Rings = 1, Material = LoadMaterial("hut_roof") };
        HutDoor = new BoxMesh { Size = DoorSize, Material = LoadMaterial("hut_door") };
        ShrineBase = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.8f, Height = ShrineBaseHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrinePillar = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.9f, Height = ShrinePillarHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrineCrown = new SphereMesh { Radius = ShrineCrownRadius, Height = 2f * ShrineCrownRadius, Material = LoadMaterial("shrine_crown") };
    }

    /// <summary>Hut wall cylinder.</summary>
    public Mesh HutWall { get; }

    /// <summary>Hut roof cone.</summary>
    public Mesh HutRoof { get; }

    /// <summary>Hut door panel.</summary>
    public Mesh HutDoor { get; }

    /// <summary>Wayshrine plinth.</summary>
    public Mesh ShrineBase { get; }

    /// <summary>Wayshrine pillar.</summary>
    public Mesh ShrinePillar { get; }

    /// <summary>Glowing orb on top of the pillar.</summary>
    public Mesh ShrineCrown { get; }

    /// <summary>Centre of the wall mesh relative to the hut's ground point.</summary>
    public static Vector3 WallOffset => new(0f, (WallHeight * 0.5f) - WallSink, 0f);

    /// <summary>Centre of the roof cone; it overlaps the wall top slightly.</summary>
    public static Vector3 RoofOffset => new(0f, WallHeight - WallSink - RoofOverlap + (RoofHeight * 0.5f), 0f);

    /// <summary>Centre of the door on the hut's local +Z side, protruding a little from the wall.</summary>
    public static Vector3 DoorOffset => new(0f, DoorSize.Y * 0.5f, WallRadius);

    /// <summary>Centre of the plinth relative to the shrine's ground point.</summary>
    public static Vector3 ShrineBaseOffset => new(0f, (ShrineBaseHeight * 0.5f) - ShrineSink, 0f);

    /// <summary>Centre of the pillar, standing on the plinth.</summary>
    public static Vector3 ShrinePillarOffset => new(0f, ShrineBaseHeight - ShrineSink + (ShrinePillarHeight * 0.5f), 0f);

    /// <summary>Centre of the crown orb, resting on the pillar.</summary>
    public static Vector3 ShrineCrownOffset => new(0f, ShrineBaseHeight - ShrineSink + ShrinePillarHeight + (ShrineCrownRadius * 0.6f), 0f);

    /// <summary>Loads a material from <c>res://Materials</c>.</summary>
    private static Material LoadMaterial(string name) => GD.Load<Material>($"{MaterialDirectory}{name}.tres");
}
