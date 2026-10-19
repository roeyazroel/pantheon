using Godot;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// A Creature's body, built from primitive meshes (presentation only, ADR-010): a round torso, a
/// big head with a snout, eyes and two curved horns, thick arms and legs on pivots, and tufts on its
/// back. Its species' look (ADR-021) sets the colours, girth, height, horns and glow; the god's
/// colour tints the horns and the tufts. Sizes are for level 1 before the level scale.
/// </summary>
internal sealed class CreatureBody
{
    private const float HipHeight = 1.6f;
    private const float ShoulderHeight = 4.7f;

    // Scars drawn at most; more are not told apart.
    private const int MostScarsShown = 6;

    private readonly StandardMaterial3D _scar;
    private int _scarsShown;

    /// <summary>Builds the body under a root node.</summary>
    /// <param name="root">Node that holds the body; it is moved and turned as a whole.</param>
    /// <param name="godColor">The god's colour.</param>
    /// <param name="look">Its species' look.</param>
    public CreatureBody(Node3D root, Color godColor, Pantheon.Game.Presentation.CreatureLook look)
    {
        var hideColor = look.Hide.ToGodot();
        var hide = look.Glow > 0f ? Material(hideColor, emission: hideColor, emissionEnergy: look.Glow) : Material(hideColor);
        var belly = Material(look.Belly.ToGodot());
        var moss = Material(look.Tufts.ToGodot().Lerp(godColor, 0.25f), emission: look.Glow > 0f ? look.Tufts.ToGodot() : null, emissionEnergy: look.Glow);
        var horn = Material(godColor.Lightened(0.15f), emission: godColor, emissionEnergy: 0.35f);
        root.Scale = new Vector3(look.Width, look.Height, look.Width);
        var eyeWhite = Material(new Color(0.97f, 0.95f, 0.88f));
        var pupil = Material(new Color(0.08f, 0.06f, 0.05f));

        Torso = new Node3D { Name = "Torso", Position = new Vector3(0f, HipHeight, 0f) };
        root.AddChild(Torso);
        Add(Torso, new CapsuleMesh { Radius = 1.7f, Height = 4.6f, Material = hide }, new Vector3(0f, 1.9f, 0f), new Vector3(0.12f, 0f, 0f));
        Add(Torso, new SphereMesh { Radius = 1.25f, Height = 2.5f, Material = belly }, new Vector3(0f, 1.6f, 0.75f), Vector3.Zero, new Vector3(1f, 1.3f, 0.7f));
        for (var i = 0; i < 5; i++)
        {
            var angle = (i - 2) * 0.45f;
            Add(Torso, new SphereMesh { Radius = 0.55f, Height = 1.1f, Material = moss }, new Vector3(Mathf.Sin(angle) * 1.2f, 2.6f + (0.35f * Mathf.Cos(angle)), -1.05f), Vector3.Zero, new Vector3(1.2f, 0.7f, 1f));
        }

        Head = new Node3D { Name = "Head", Position = new Vector3(0f, 4.2f, 0.45f) };
        Torso.AddChild(Head);
        Add(Head, new SphereMesh { Radius = 1.15f, Height = 2.3f, Material = hide }, Vector3.Zero);
        Add(Head, new CapsuleMesh { Radius = 0.55f, Height = 1.6f, Material = belly }, new Vector3(0f, -0.35f, 0.95f), new Vector3(Mathf.Pi / 2f, 0f, 0f));
        foreach (var side in new[] { -1f, 1f })
        {
            Add(Head, new SphereMesh { Radius = 0.26f, Height = 0.52f, Material = eyeWhite }, new Vector3(side * 0.48f, 0.25f, 0.92f));
            Add(Head, new SphereMesh { Radius = 0.13f, Height = 0.26f, Material = pupil }, new Vector3(side * 0.5f, 0.27f, 1.14f));
            if (look.Horns > 0f)
            {
                Add(Head, new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.28f, Height = 1.5f * look.Horns, Material = horn }, new Vector3(side * 0.75f, 0.6f + (0.5f * look.Horns), -0.1f), new Vector3(-0.35f, 0f, -side * 0.55f));
            }
        }

        _scar = Material(new Color(0.22f, 0.08f, 0.07f));
        LeftArm = Limb(Torso, "LeftArm", new Vector3(-1.85f, ShoulderHeight - HipHeight, 0.2f), hide, 0.5f, 2.6f);
        RightArm = Limb(Torso, "RightArm", new Vector3(1.85f, ShoulderHeight - HipHeight, 0.2f), hide, 0.5f, 2.6f);
        LeftLeg = Limb(root, "LeftLeg", new Vector3(-0.9f, HipHeight, 0f), hide, 0.6f, 1.9f);
        RightLeg = Limb(root, "RightLeg", new Vector3(0.9f, HipHeight, 0f), hide, 0.6f, 1.9f);
        Materials = [hide, belly, moss, horn];
    }

    /// <summary>Torso pivot at the hips; it leans and bobs.</summary>
    public Node3D Torso { get; }

    /// <summary>Head pivot; it nods and turns.</summary>
    public Node3D Head { get; }

    /// <summary>Left arm pivot at the shoulder.</summary>
    public Node3D LeftArm { get; }

    /// <summary>Right arm pivot at the shoulder.</summary>
    public Node3D RightArm { get; }

    /// <summary>Left leg pivot at the hip.</summary>
    public Node3D LeftLeg { get; }

    /// <summary>Right leg pivot at the hip.</summary>
    public Node3D RightLeg { get; }

    /// <summary>The body's materials, for a flash of colour when it is slapped or praised.</summary>
    public StandardMaterial3D[] Materials { get; }

    /// <summary>Draws the Creature's scars (spec §28): dark slashes on its flanks, one per defeat, up to six.</summary>
    /// <param name="scars">How many scars it has.</param>
    public void ShowScars(int scars)
    {
        var wanted = Mathf.Clamp(scars, 0, MostScarsShown);
        for (; _scarsShown < wanted; _scarsShown++)
        {
            // Alternate flanks, rising, each slanting down towards the tail, where the god's camera sees them.
            var side = _scarsShown % 2 == 0 ? -1f : 1f;
            var height = 1.5f + (0.55f * (_scarsShown / 2));
            Add(Torso, new BoxMesh { Size = new Vector3(0.12f, 0.16f, 1.4f), Material = _scar }, new Vector3(side * 1.66f, height, 0.1f), new Vector3(0.55f, 0f, 0f));
        }
    }

    /// <summary>A limb hanging from a pivot.</summary>
    private static Node3D Limb(Node3D parent, string name, Vector3 pivot, Material material, float radius, float length)
    {
        var limb = new Node3D { Name = name, Position = pivot };
        parent.AddChild(limb);
        Add(limb, new CapsuleMesh { Radius = radius, Height = length, Material = material }, new Vector3(0f, (-length / 2f) + radius, 0f));
        return limb;
    }

    /// <summary>Adds one mesh part.</summary>
    private static void Add(Node3D parent, Mesh mesh, Vector3 position, Vector3 rotation = default, Vector3? scale = null) =>
        parent.AddChild(new MeshInstance3D { Mesh = mesh, Position = position, Rotation = rotation, Scale = scale ?? Vector3.One });

    /// <summary>A matte material, optionally glowing.</summary>
    private static StandardMaterial3D Material(Color color, Color? emission = null, float emissionEnergy = 0f) => new()
    {
        AlbedoColor = color,
        Roughness = 0.85f,
        EmissionEnabled = emission is not null,
        Emission = emission ?? Colors.Black,
        EmissionEnergyMultiplier = emissionEnergy,
    };
}
