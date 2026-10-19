using Godot;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Procedural meshes shared by every settlement: a round hut (wall, thatched cone roof, door), the
/// central wayshrine, the scaffolding of a building site, a field plot and a citizen figure.
/// Offsets place each part relative to a point on the ground.
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

    // Citizen figure at life size (CitizenSettings.FigureScale enlarges it): a robed body and a head.
    private const float BodyHeight = 1.15f;
    private const float BodyTopRadius = 0.17f;
    private const float BodyBottomRadius = 0.3f;
    private const float HeadRadius = 0.2f;
    private const int FigureSegments = 8;

    // A ruined hut: a low heap of charred timber and earth.
    private static readonly Vector3 RubbleSize = new(3.4f, 0.7f, 2.8f);

    /// <summary>Loads the materials and builds the meshes.</summary>
    public SettlementAssets()
    {
        var wall = LoadMaterial("hut_wall");
        var stone = LoadMaterial("shrine_stone");
        var timber = LoadMaterial("scaffold");
        HutWall = new CylinderMesh { TopRadius = WallRadius, BottomRadius = WallRadius * 1.05f, Height = WallHeight, RadialSegments = RadialSegments, Rings = 1, Material = wall };
        HutRoof = new CylinderMesh { TopRadius = 0f, BottomRadius = RoofRadius, Height = RoofHeight, RadialSegments = RadialSegments, Rings = 1, Material = LoadMaterial("hut_roof") };
        HutDoor = new BoxMesh { Size = DoorSize, Material = LoadMaterial("hut_door") };
        Stone = stone;
        Timber = timber;
        ShrineBase = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.8f, Height = ShrineBaseHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrinePillar = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.9f, Height = ShrinePillarHeight, RadialSegments = RadialSegments, Rings = 1, Material = stone };
        ShrineCrown = new SphereMesh { Radius = ShrineCrownRadius, Height = 2f * ShrineCrownRadius, Material = LoadMaterial("shrine_crown") };
        ScaffoldPole = new CylinderMesh { TopRadius = ScaffoldPoleRadius, BottomRadius = ScaffoldPoleRadius, Height = ScaffoldPoleHeight, RadialSegments = 6, Rings = 1, Material = timber };
        TimberPile = new BoxMesh { Size = TimberPileSize, Material = timber };
        Rubble = new BoxMesh { Size = RubbleSize, Material = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.2f, 0.17f), Roughness = 1f } };
        FieldPlot = new BoxMesh { Size = new Vector3(1f, FieldThickness, 1f), Material = CreateFieldMaterial() };
        CitizenFigure = CreateCitizenFigure(LoadMaterial("citizen_body"), LoadMaterial("citizen_head"));
    }

    /// <summary>Height of a scaffold pole in metres; it rises above a finished hut's walls.</summary>
    public static float ScaffoldPoleHeight => 4.4f;

    /// <summary>Radius of the ring of scaffold poles around a building site.</summary>
    public static float ScaffoldRadius => 2.6f;

    /// <summary>The shrines' stone, for temples too (ADR-023).</summary>
    public Material Stone { get; }

    /// <summary>The scaffolds' timber, for walls too (ADR-023).</summary>
    public Material Timber { get; }

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

    /// <summary>One scaffold pole of a building site.</summary>
    public Mesh ScaffoldPole { get; }

    /// <summary>Stack of timber beside a building site.</summary>
    public Mesh TimberPile { get; }

    /// <summary>A ruined house's heap (ADR-014).</summary>
    public Mesh Rubble { get; }

    /// <summary>Unit-sized field slab; scale it by the field size and tint it per instance with the crop colour.</summary>
    public Mesh FieldPlot { get; }

    /// <summary>Citizen figure with its feet at the origin, facing +Z: the body surface takes the instance colour, the head surface does not.</summary>
    public Mesh CitizenFigure { get; }

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

    /// <summary>Centre of a scaffold pole relative to the ground below it.</summary>
    public static Vector3 ScaffoldPoleOffset => new(0f, (ScaffoldPoleHeight * 0.5f) - ScaffoldPoleSink, 0f);

    /// <summary>Centre of the timber pile, lying on the ground beside the site (local -X side).</summary>
    public static Vector3 TimberPileOffset => new(-(ScaffoldRadius + 1.6f), TimberPileSize.Y * 0.4f, 0.8f);

    /// <summary>Height of a field slab's centre above the ground point.</summary>
    public static float FieldCenterLift => FieldLift;

    /// <summary>
    /// Transform of a wall that is <paramref name="built"/> of the way up: the wall keeps its
    /// sunk foot and its visible part grows from the ground.
    /// </summary>
    /// <param name="built">Share of the wall built, 0..1.</param>
    /// <returns>Scale and offset relative to the hut's ground point (before the hut's own rotation).</returns>
    public static Transform3D PartialWall(float built)
    {
        var visible = (WallHeight - WallSink) * Mathf.Clamp(built, 0f, 1f);
        var total = WallSink + visible;
        var scale = new Vector3(1f, total / WallHeight, 1f);
        return new Transform3D(Basis.FromScale(scale), new Vector3(0f, (visible - WallSink) * 0.5f, 0f));
    }

    /// <summary>
    /// Transform of a roof that is <paramref name="built"/> of the way done: the thatch rises
    /// from the wall top to its full height.
    /// </summary>
    /// <param name="built">Share of the roof built, 0..1.</param>
    /// <returns>Scale and offset relative to the hut's ground point (before the hut's own rotation).</returns>
    public static Transform3D PartialRoof(float built)
    {
        var height = RoofHeight * Mathf.Clamp(built, 0.05f, 1f);
        var scale = new Vector3(1f, height / RoofHeight, 1f);
        return new Transform3D(Basis.FromScale(scale), new Vector3(0f, WallHeight - WallSink - RoofOverlap + (height * 0.5f), 0f));
    }

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

    /// <summary>A tapered body and a head merged into one mesh with a surface (and material) each.</summary>
    private static ArrayMesh CreateCitizenFigure(Material body, Material head)
    {
        var figure = new ArrayMesh();
        var bodyMesh = new CylinderMesh { TopRadius = BodyTopRadius, BottomRadius = BodyBottomRadius, Height = BodyHeight, RadialSegments = FigureSegments, Rings = 1 };
        var headMesh = new SphereMesh { Radius = HeadRadius, Height = 2f * HeadRadius, RadialSegments = FigureSegments, Rings = FigureSegments / 2 };
        AddSurface(figure, bodyMesh, new Vector3(0f, BodyHeight * 0.5f, 0f), body);
        AddSurface(figure, headMesh, new Vector3(0f, BodyHeight + (HeadRadius * 0.8f), 0f), head);
        return figure;
    }

    /// <summary>Appends a primitive mesh, moved by <paramref name="offset"/>, to <paramref name="target"/> as a new surface.</summary>
    private static void AddSurface(ArrayMesh target, PrimitiveMesh part, Vector3 offset, Material material)
    {
        var arrays = part.GetMeshArrays();
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] += offset;
        }

        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        target.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        target.SurfaceSetMaterial(target.GetSurfaceCount() - 1, material);
    }
}
