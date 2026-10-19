using System.Collections.Generic;
using Godot;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// A Creature's body, built from primitive meshes (presentation only, ADR-010): a round torso, a
/// big head with a snout, eyes and two curved horns, thick arms and legs on pivots, tufts on its
/// back and a tail. Its species' look (ADR-021) sets the colours, girth, height, horns and glow; the
/// god's colour tints the horns and the tufts. Its face (ADR-055) shows how it feels: brows, eyelids,
/// a mouth and pupils; its tail wags, tucks or lashes; and its nature shows in its body: a kind
/// Creature grows warmer and brighter, a cruel one darker, red-eyed and longer-horned. Sizes are for
/// level 1 before the level scale.
/// </summary>
internal sealed class CreatureBody
{
    private const float HipHeight = 1.6f;
    private const float ShoulderHeight = 4.7f;

    // Scars drawn at most; more are not told apart.
    private const int MostScarsShown = 6;

    private readonly StandardMaterial3D _scar;
    private readonly StandardMaterial3D _hide;
    private readonly StandardMaterial3D _pupil;
    private readonly Color _hideColor;
    private readonly float _hideGlow;
    private readonly Node3D[] _brows = new Node3D[2];
    private readonly MeshInstance3D[] _lids = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _cheeks = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _pupils = new MeshInstance3D[2];
    private readonly Node3D[] _corners = new Node3D[2];
    private readonly MeshInstance3D _mouth;
    private readonly List<Node3D> _horns = [];
    private readonly Node3D _tail;
    private int _scarsShown;
    private int _nature = int.MinValue;

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
        (_hide, _pupil, _hideColor, _hideGlow) = (hide, pupil, hideColor, look.Glow);
        var brow = Material(hideColor.Darkened(0.55f));
        var mouth = Material(new Color(0.18f, 0.06f, 0.05f));

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
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            Face(Head, new SphereMesh { Radius = 0.26f, Height = 0.52f, Material = eyeWhite }, new Vector3(side * 0.48f, 0.25f, 0.92f));
            _pupils[i] = Face(Head, new SphereMesh { Radius = 0.13f, Height = 0.26f, Material = pupil }, new Vector3(side * 0.5f, 0.27f, 1.14f));
            _lids[i] = Face(Head, new SphereMesh { Radius = 0.29f, Height = 0.58f, Material = hide }, new Vector3(side * 0.48f, 0.5f, 0.92f), Vector3.Zero, new Vector3(1f, 0.02f, 1f));
            _cheeks[i] = Face(Head, new SphereMesh { Radius = 0.3f, Height = 0.6f, Material = hide }, new Vector3(side * 0.48f, 0f, 0.92f), Vector3.Zero, new Vector3(1f, 0.02f, 1f));
            _brows[i] = new Node3D { Name = "Brow", Position = new Vector3(side * 0.48f, 0.62f, 1.0f) };
            Head.AddChild(_brows[i]);
            Face(_brows[i], new BoxMesh { Size = new Vector3(0.5f, 0.11f, 0.16f), Material = brow }, Vector3.Zero);
            if (look.Horns > 0f)
            {
                var hornNode = new Node3D { Name = "Horn", Position = new Vector3(side * 0.75f, 0.6f, -0.1f), Rotation = new Vector3(-0.35f, 0f, -side * 0.55f) };
                Head.AddChild(hornNode);
                Add(hornNode, new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.28f, Height = 1.5f * look.Horns, Material = horn }, new Vector3(0f, 0.75f * look.Horns, 0f));
                _horns.Add(hornNode);
            }
        }

        // The mouth sits on the front of the snout, tilted to face forward and down, where the god's camera sees it.
        var lips = new Node3D { Name = "Mouth", Position = new Vector3(0f, -0.74f, 1.6f), Rotation = new Vector3(0.75f, 0f, 0f) };
        Head.AddChild(lips);
        Face(lips, new BoxMesh { Size = new Vector3(0.16f, 0.08f, 0.1f), Material = mouth }, new Vector3(0f, 0f, 0.02f));
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            _corners[i] = new Node3D { Name = "MouthCorner", Position = new Vector3(side * 0.08f, 0f, 0.02f) };
            lips.AddChild(_corners[i]);
            Face(_corners[i], new BoxMesh { Size = new Vector3(0.3f, 0.08f, 0.1f), Material = mouth }, new Vector3(side * 0.14f, 0f, 0f));
        }

        _mouth = Face(lips, new SphereMesh { Radius = 0.22f, Height = 0.44f, Material = mouth }, new Vector3(0f, -0.04f, 0f), Vector3.Zero, new Vector3(1f, 0.02f, 0.5f));
        _tail = new Node3D { Name = "Tail", Position = new Vector3(0f, 1.1f, -1.55f) };
        Torso.AddChild(_tail);
        Add(_tail, new CapsuleMesh { Radius = 0.32f, Height = 1.6f, Material = hide }, new Vector3(0f, -0.35f, -0.55f), new Vector3(-1f, 0f, 0f));
        Add(_tail, new SphereMesh { Radius = 0.3f, Height = 0.6f, Material = moss }, new Vector3(0f, -0.75f, -1.2f));

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

    /// <summary>Shows how the Creature feels (ADR-055): brows, eyelids, mouth, pupils and tail.</summary>
    /// <param name="face">Its face.</param>
    /// <param name="clock">Seconds of its own clock, for the tail's wag.</param>
    public void Express(Pantheon.Game.Presentation.CreatureFace face, float clock)
    {
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            // Anger draws the brows down at the middle; fear and sorrow lift them there; joy raises them.
            var sorrow = Mathf.Max(0f, -face.Joy);
            _brows[i].Rotation = new Vector3(0f, 0f, side * ((face.Anger * 0.7f) - (face.Fear * 0.5f) - (sorrow * 0.35f)));
            _brows[i].Position = new Vector3(side * 0.48f, 0.62f + (0.14f * face.Fear) + (0.06f * Mathf.Max(0f, face.Joy)) - (0.09f * face.Anger), 1.0f);
            var closed = Mathf.Max(0.02f, face.Sleep);
            _lids[i].Scale = new Vector3(1f, closed, 1f);
            _lids[i].Position = new Vector3(side * 0.48f, 0.25f + (0.26f * (1f - closed)), 0.92f);

            // A smile pushes the cheeks up into the eyes.
            var cheek = Mathf.Max(0.02f, face.Joy * 0.55f);
            _cheeks[i].Scale = new Vector3(1f, cheek, 1f);
            _cheeks[i].Position = new Vector3(side * 0.48f, 0.25f - (0.27f * (1f - cheek)), 0.93f);
            _pupils[i].Scale = Vector3.One * (1f + (0.45f * face.Fear) - (0.25f * face.Anger));
            _corners[i].Rotation = new Vector3(0f, 0f, side * face.Joy * 0.95f);
        }

        // It bares its teeth in anger, gapes in fear and grins in joy.
        var open = Mathf.Max(Mathf.Max(face.Anger, face.Fear), (face.Joy - 0.4f) * 1.2f) * 0.9f;
        _mouth.Scale = new Vector3(1f, Mathf.Max(0.02f, open), 0.4f);
        var wag = Mathf.Sin(clock * 9f) * 0.7f * face.Wag;
        var lash = Mathf.Sin(clock * 5f) * 0.35f * face.Anger;
        _tail.Rotation = new Vector3(0.9f * face.Fear, wag + lash, 0f);
    }

    /// <summary>
    /// Shows the Creature's nature (ADR-055): a kind one's hide warms and glows softly, a cruel one's
    /// darkens and reddens, its eyes burn red and its horns grow. Recoloured only as its nature moves.
    /// </summary>
    /// <param name="morality">Its morality, from -1 (cruel) to 1 (kind).</param>
    public void Align(float morality)
    {
        var nature = (int)Mathf.Round(morality * 10f);
        if (nature == _nature)
        {
            return;
        }

        _nature = nature;
        var kindness = Mathf.Clamp(morality, 0f, 1f);
        var cruelty = Mathf.Clamp(-morality, 0f, 1f);
        var warm = new Color(Mathf.Min(1f, _hideColor.R + 0.12f), Mathf.Min(1f, _hideColor.G + 0.08f), _hideColor.B).Lightened(0.15f);
        var dark = new Color(_hideColor.R * 0.55f + 0.08f, _hideColor.G * 0.4f, _hideColor.B * 0.4f);
        _hide.AlbedoColor = _hideColor.Lerp(warm, kindness * 0.6f).Lerp(dark, cruelty * 0.7f);
        (_glowColor, _glowEnergy) = kindness > 0.3f ? (new Color(1f, 0.85f, 0.5f), kindness * 0.25f) : (_hideColor, _hideGlow);
        RestoreGlow();
        _pupil.EmissionEnabled = cruelty > 0.3f;
        _pupil.Emission = new Color(0.9f, 0.1f, 0.05f);
        _pupil.EmissionEnergyMultiplier = cruelty * 2f;
        _pupil.AlbedoColor = new Color(0.08f, 0.06f, 0.05f).Lerp(new Color(0.6f, 0.05f, 0.03f), cruelty);
        foreach (var horn in _horns)
        {
            horn.Scale = new Vector3(1f, 1f + (0.8f * cruelty), 1f);
        }
    }

    /// <summary>Puts the hide's own glow back after a flash.</summary>
    public void RestoreGlow()
    {
        _hide.EmissionEnabled = _glowEnergy > 0f;
        _hide.Emission = _glowColor;
        _hide.EmissionEnergyMultiplier = _glowEnergy;
    }

    private Color _glowColor;
    private float _glowEnergy;

    /// <summary>A limb hanging from a pivot.</summary>
    private static Node3D Limb(Node3D parent, string name, Vector3 pivot, Material material, float radius, float length)
    {
        var limb = new Node3D { Name = name, Position = pivot };
        parent.AddChild(limb);
        Add(limb, new CapsuleMesh { Radius = radius, Height = length, Material = material }, new Vector3(0f, (-length / 2f) + radius, 0f));
        return limb;
    }

    /// <summary>Adds one mesh part.</summary>
    private static MeshInstance3D Add(Node3D parent, Mesh mesh, Vector3 position, Vector3 rotation = default, Vector3? scale = null)
    {
        var part = new MeshInstance3D { Mesh = mesh, Position = position, Rotation = rotation, Scale = scale ?? Vector3.One };
        parent.AddChild(part);
        return part;
    }

    /// <summary>Adds one small part of the face; it casts no shadow, as it would draw again in every shadow pass for nothing seen.</summary>
    private static MeshInstance3D Face(Node3D parent, Mesh mesh, Vector3 position, Vector3 rotation = default, Vector3? scale = null)
    {
        var part = Add(parent, mesh, position, rotation, scale);
        part.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return part;
    }

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
