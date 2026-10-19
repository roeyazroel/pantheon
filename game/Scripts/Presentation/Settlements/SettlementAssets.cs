using Godot;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Procedural meshes shared by every settlement: the central wayshrine, the scaffolding of a
/// building site, a field plot and a ruin's heap. Offsets place each part relative to a point on
/// the ground. Houses, temples and walls are <c>Buildings/BuildingAssets</c> (ADR-052), people
/// <c>People/PersonAssets</c> (ADR-049).
/// </summary>
internal sealed class SettlementAssets
{
    private const string MaterialDirectory = "res://Materials/";
    private const int RadialSegments = 12;

    private const float ShrineSink = 0.6f;
    private const float ShrineBaseHeight = 1.6f;
    private const float ShrinePillarHeight = 7f;
    private const float ShrineCrownRadius = 1f;

    private const float ScaffoldPoleRadius = 0.09f;
    private const float ScaffoldPoleSink = 0.3f;
    private static readonly Vector3 TimberPileSize = new(1.8f, 0.45f, 0.9f);

    // Field plots are thin slabs, sunk a little so small bumps of the ground stay covered.
    private const float FieldThickness = 0.4f;
    private const float FieldLift = 0.08f;

    // Crop rows: a grey-and-white striped texture that the per-field crop colour tints. A box face
    // spans half the texture's height, so each field shows half of its stripes.
    private const int RowTextureSize = 64;
    private const int RowWidthPixels = 4;
    private const float FurrowShade = 0.7f;

    // A ruined hut: a low heap of charred timber and earth.
    private static readonly Vector3 RubbleSize = new(3.4f, 0.7f, 2.8f);

    /// <summary>Loads the materials and builds the meshes.</summary>
    public SettlementAssets()
    {
        var stone = LoadMaterial("shrine_stone");
        var timber = LoadMaterial("scaffold");
        ShrineBase = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.8f, Height = ShrineBaseHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrinePillar = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.9f, Height = ShrinePillarHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrineCrown = new SphereMesh { Radius = ShrineCrownRadius, Height = 2f * ShrineCrownRadius, Material = LoadMaterial("shrine_crown") };
        ScaffoldPole = new CylinderMesh { TopRadius = ScaffoldPoleRadius, BottomRadius = ScaffoldPoleRadius, Height = ScaffoldPoleHeight, RadialSegments = 6, Rings = 1, Material = timber };
        TimberPile = new BoxMesh { Size = TimberPileSize, Material = timber };
        Rubble = new BoxMesh { Size = RubbleSize, Material = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.2f, 0.17f), Roughness = 1f } };
        FieldPlot = new BoxMesh { Size = new Vector3(1f, FieldThickness, 1f), Material = CreateFieldMaterial() };
    }

    /// <summary>Height of a scaffold pole in metres; it rises above a finished hut's walls.</summary>
    public static float ScaffoldPoleHeight => 4.4f;

    /// <summary>Radius of the ring of scaffold poles around a building site.</summary>
    public static float ScaffoldRadius => 2.6f;

    /// <summary>Wayshrine plinth.</summary>
    public Mesh ShrineBase { get; }

    /// <summary>Wayshrine pillar.</summary>
    public Mesh ShrinePillar { get; }

    /// <summary>Glowing orb on top of the pillar.</summary>
    public Mesh ShrineCrown { get; }

    /// <summary>One scaffold pole of a building site.</summary>
    public Mesh ScaffoldPole { get; }

    /// <summary>Stack of timber beside a building site.</summary>
    public Mesh TimberPile { get; }

    /// <summary>A ruined house's heap (ADR-014).</summary>
    public Mesh Rubble { get; }

    /// <summary>Unit-sized field slab; scale it by the field size and tint it per instance with the crop colour.</summary>
    public Mesh FieldPlot { get; }

    /// <summary>Centre of the plinth relative to the shrine's ground point.</summary>
    public static Vector3 ShrineBaseOffset => new(0f, (ShrineBaseHeight * 0.5f) - ShrineSink, 0f);

    /// <summary>Centre of the pillar, standing on the plinth.</summary>
    public static Vector3 ShrinePillarOffset => new(0f, ShrineBaseHeight - ShrineSink + (ShrinePillarHeight * 0.5f), 0f);

    /// <summary>Centre of the crown orb, resting on the pillar.</summary>
    public static Vector3 ShrineCrownOffset => new(0f, ShrineBaseHeight - ShrineSink + ShrinePillarHeight + (ShrineCrownRadius * 0.6f), 0f);

    /// <summary>Centre of a scaffold pole relative to the ground below it.</summary>
    public static Vector3 ScaffoldPoleOffset => new(0f, (ScaffoldPoleHeight * 0.5f) - ScaffoldPoleSink, 0f);

    /// <summary>Centre of the timber pile, lying on the ground beside the site (local -X side).</summary>
    public static Vector3 TimberPileOffset => new(-(ScaffoldRadius + 1.6f), TimberPileSize.Y * 0.4f, 0.8f);

    /// <summary>Height of a field slab's centre above the ground point.</summary>
    public static float FieldCenterLift => FieldLift;

    /// <summary>Loads a material from <c>res://Materials</c>.</summary>
    private static Material LoadMaterial(string name) => GD.Load<Material>($"{MaterialDirectory}{name}.tres");

    /// <summary>The field material with crop rows added as its albedo texture.</summary>
    private static Material CreateFieldMaterial()
    {
        var material = (StandardMaterial3D)LoadMaterial("field").Duplicate();
        material.AlbedoTexture = CreateRowTexture();
        return material;
    }

    /// <summary>Alternating light rows and darker furrows, with mipmaps so distant fields blend instead of shimmering.</summary>
    private static ImageTexture CreateRowTexture()
    {
        var image = Image.CreateEmpty(RowTextureSize, RowTextureSize, false, Image.Format.Rgb8);
        for (var y = 0; y < RowTextureSize; y++)
        {
            var shade = (y / RowWidthPixels) % 2 == 0 ? 1f : FurrowShade;
            for (var x = 0; x < RowTextureSize; x++)
            {
                image.SetPixel(x, y, new Color(shade, shade, shade));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
