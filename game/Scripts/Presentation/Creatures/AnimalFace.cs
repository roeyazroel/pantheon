using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Animals;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// The moving parts of a Creature's face (ADR-055, ADR-061), placed by its species' face plan: eyes
/// with pupils, lids that close in sleep, brows that draw down in anger and lift in fear, cheeks that
/// push up into a smile, and a mouth on the front of the muzzle that bares, gapes and grins. None
/// casts a shadow: each would draw again in every shadow pass for nothing seen.
/// </summary>
internal sealed class AnimalFace
{
    private readonly FacePlan _plan;
    private readonly Node3D[] _brows = new Node3D[2];
    private readonly MeshInstance3D[] _lids = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _cheeks = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _pupils = new MeshInstance3D[2];
    private readonly Node3D[] _corners = new Node3D[2];
    private readonly MeshInstance3D _mouth;

    /// <summary>Builds the face on a head.</summary>
    /// <param name="head">The head's joint.</param>
    /// <param name="plan">Where the face's parts sit.</param>
    /// <param name="pupil">The pupils' material, which a cruel Creature's nature lights.</param>
    public AnimalFace(Node3D head, FacePlan plan, StandardMaterial3D pupil)
    {
        _plan = plan;
        var r = plan.EyeRadius;
        var eyeWhite = Plain(new Color(0.97f, 0.95f, 0.88f));
        var lid = Plain(new Color(0.18f, 0.15f, 0.13f));
        var mouthColor = Plain(new Color(0.2f, 0.07f, 0.06f));
        for (var i = 0; i < 2; i++)
        {
            var eye = Eye(i);
            Part(head, new SphereMesh { Radius = r, Height = r * 2f, Material = eyeWhite }, eye);
            _pupils[i] = Part(head, new SphereMesh { Radius = r * 0.5f, Height = r, Material = pupil }, eye + new Vector3(0f, 0.02f, r * 0.82f));
            _lids[i] = Part(head, new SphereMesh { Radius = r * 1.12f, Height = r * 2.24f, Material = lid }, eye, Vector3.Zero, new Vector3(1f, 0.02f, 1f));
            _cheeks[i] = Part(head, new SphereMesh { Radius = r * 1.15f, Height = r * 2.3f, Material = lid }, eye, Vector3.Zero, new Vector3(1f, 0.02f, 1f));
            _brows[i] = new Node3D { Name = "Brow", Position = eye + new Vector3(0f, r * 1.45f, r * 0.35f) };
            head.AddChild(_brows[i]);
            Part(_brows[i], new BoxMesh { Size = new Vector3(r * 2.2f, r * 0.42f, r * 0.6f), Material = lid }, Vector3.Zero);
        }

        var lips = new Node3D { Name = "Mouth", Position = plan.Mouth.ToGodot(), Rotation = new Vector3(plan.MouthTilt, 0f, 0f) };
        head.AddChild(lips);
        var w = plan.MouthWidth;
        Part(lips, new BoxMesh { Size = new Vector3(w * 0.35f, w * 0.15f, w * 0.2f), Material = mouthColor }, Vector3.Zero);
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            _corners[i] = new Node3D { Name = "MouthCorner", Position = new Vector3(side * w * 0.17f, 0f, 0f) };
            lips.AddChild(_corners[i]);
            Part(_corners[i], new BoxMesh { Size = new Vector3(w * 0.5f, w * 0.14f, w * 0.18f), Material = mouthColor }, new Vector3(side * w * 0.24f, 0f, 0f));
        }

        _mouth = Part(lips, new SphereMesh { Radius = w * 0.4f, Height = w * 0.8f, Material = mouthColor }, new Vector3(0f, -w * 0.06f, 0f), Vector3.Zero, new Vector3(1f, 0.02f, 0.5f));
    }

    /// <summary>Shows a face.</summary>
    /// <param name="face">The face.</param>
    public void Express(CreatureFace face)
    {
        var r = _plan.EyeRadius;
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? 1f : -1f;
            var eye = Eye(i);

            // Anger draws the brows down at the middle; fear and sorrow lift them there; joy raises them.
            var sorrow = Mathf.Max(0f, -face.Joy);
            _brows[i].Rotation = new Vector3(0f, 0f, -side * ((face.Anger * 0.7f) - (face.Fear * 0.5f) - (sorrow * 0.35f)));
            _brows[i].Position = eye + new Vector3(0f, r * (1.45f + (0.8f * face.Fear) + (0.35f * Mathf.Max(0f, face.Joy)) - (0.55f * face.Anger)), r * 0.35f);
            var closed = Mathf.Max(0.02f, face.Sleep);
            _lids[i].Scale = new Vector3(1f, closed, 1f);
            _lids[i].Position = eye + new Vector3(0f, r * (1.05f * (1f - closed)), r * 0.05f);

            // A smile pushes the cheeks up into the eyes.
            var cheek = Mathf.Max(0.02f, face.Joy * 0.55f);
            _cheeks[i].Scale = new Vector3(1f, cheek, 1f);
            _cheeks[i].Position = eye - new Vector3(0f, r * 1.1f * (1f - cheek), -r * 0.08f);
            _pupils[i].Scale = Vector3.One * (1f + (0.45f * face.Fear) - (0.25f * face.Anger));
            _corners[i].Rotation = new Vector3(0f, 0f, (i == 0 ? -1f : 1f) * face.Joy * 0.95f);
        }

        // It bares its teeth in anger, gapes in fear and grins in joy.
        var open = Mathf.Max(Mathf.Max(face.Anger, face.Fear), (face.Joy - 0.4f) * 1.2f) * 0.9f;
        _mouth.Scale = new Vector3(1f, Mathf.Max(0.02f, open), 0.4f);
    }

    /// <summary>An eye's centre: the plan's on the left (+X), mirrored on the right.</summary>
    private Vector3 Eye(int i) => new(i == 0 ? _plan.Eye.X : -_plan.Eye.X, _plan.Eye.Y, _plan.Eye.Z);

    /// <summary>One part of the face, casting no shadow.</summary>
    private static MeshInstance3D Part(Node3D parent, Mesh mesh, Vector3 position, Vector3 rotation = default, Vector3? scale = null)
    {
        var part = new MeshInstance3D { Mesh = mesh, Position = position, Rotation = rotation, Scale = scale ?? Vector3.One, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(part);
        return part;
    }

    private static StandardMaterial3D Plain(Color color) => new() { AlbedoColor = color, Roughness = 0.5f };
}
