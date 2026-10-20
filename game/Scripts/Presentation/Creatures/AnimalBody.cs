using System.Collections.Generic;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Animals;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// A Creature's body (ADR-061): an upright animal on a skeleton of joints (hips, knees and ankles;
/// shoulders, elbows and wrists; neck and head; a tail of three segments), each part a smooth mesh of
/// its species. It takes an <see cref="BodyPose"/> each frame. Its face shows how it feels (ADR-055):
/// eyes with pupils, lids, brows, cheeks and a mouth on its muzzle; a collar in its god's colour marks
/// whose it is; its nature tints it warm or dark and lights a cruel one's eyes red; scars show on its
/// flanks. Sizes are in body units, before its level's scale.
/// </summary>
internal sealed class AnimalBody
{
    // Scars drawn at most; more are not told apart.
    private const int MostScarsShown = 6;
    private const int TailSegments = 3;

    private readonly AnimalPlan _plan;
    private readonly StandardMaterial3D _skin;
    private readonly StandardMaterial3D _pupil;
    private readonly StandardMaterial3D _scar;
    private readonly float _glow;
    private readonly Color _glowColor;
    private readonly Node3D _hips;
    private readonly Node3D _torso;
    private readonly Node3D _head;
    private readonly (Node3D Hip, Node3D Knee, Node3D Ankle)[] _legs = new (Node3D, Node3D, Node3D)[2];
    private readonly (Node3D Shoulder, Node3D Upper, Node3D Elbow, Node3D Wrist)[] _arms = new (Node3D, Node3D, Node3D, Node3D)[2];
    private readonly List<Node3D> _tail = [];
    private readonly AnimalFace _face;
    private int _scarsShown;
    private int _nature = int.MinValue;

    /// <summary>Builds the body under a root node.</summary>
    /// <param name="root">Node that holds the body; its origin is the ground under the Creature.</param>
    /// <param name="meshes">The species' meshes.</param>
    /// <param name="godColor">Its god's colour, for its collar.</param>
    /// <param name="glow">How much its hide glows (0..1).</param>
    public AnimalBody(Node3D root, AnimalMeshes meshes, Color godColor, float glow)
    {
        _plan = meshes.Plan;
        var b = _plan.Body;
        _glow = glow;
        _glowColor = new Color(1f, 0.6f, 0.35f);
        _skin = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 0.9f,
            RimEnabled = true,
            Rim = 0.35f,
            RimTint = 0.6f,
            EmissionEnabled = glow > 0f,
            Emission = _glowColor,
            EmissionEnergyMultiplier = glow,
        };
        _pupil = Plain(new Color(0.06f, 0.05f, 0.04f));
        _scar = Plain(new Color(0.24f, 0.08f, 0.07f));

        _hips = Joint(root, "Hips", new Vector3(0f, b.HipHeight, 0f));
        _torso = Joint(_hips, "Torso", Vector3.Zero);
        Part(_torso, meshes, "Torso");
        _head = Joint(_torso, "Head", new Vector3(0f, b.Torso + b.Neck, b.NeckForward));
        Part(_head, meshes, "Head");
        _face = new AnimalFace(_head, _plan.Face, _pupil);
        Collar(godColor, b);

        // The +X side is the Creature's left, as it faces +Z.
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? 1f : -1f;
            var mirror = new Vector3(side, 1f, 1f);
            var hip = Joint(_hips, "Hip", new Vector3(side * b.HipWidth, 0f, 0f));
            Part(hip, meshes, "Thigh");
            var knee = Joint(hip, "Knee", new Vector3(0f, -b.Thigh, 0f));
            Part(knee, meshes, "Shin");
            var ankle = Joint(knee, "Ankle", new Vector3(0f, -b.Shin, 0f));
            Part(ankle, meshes, "Foot").Scale = mirror;
            _legs[i] = (hip, knee, ankle);

            var shoulder = Joint(_torso, "Shoulder", new Vector3(side * b.ShoulderWidth, b.Torso * 0.95f, 0f));
            var upper = Joint(shoulder, "UpperArm", Vector3.Zero);
            Part(upper, meshes, "UpperArm");
            var elbow = Joint(upper, "Elbow", new Vector3(0f, -b.UpperArm, 0f));
            Part(elbow, meshes, "Forearm");
            var wrist = Joint(elbow, "Wrist", new Vector3(0f, -b.Forearm, 0f));
            Part(wrist, meshes, "Hand").Scale = mirror;
            _arms[i] = (shoulder, upper, elbow, wrist);
        }

        if (meshes.Parts.ContainsKey("Tail"))
        {
            var parent = Joint(_hips, "Tail", new Vector3(0f, 0.15f, -0.75f));
            for (var i = 0; i < TailSegments; i++)
            {
                var segment = i == 0 ? parent : Joint(_tail[^1], "Tail", new Vector3(0f, 0f, -b.TailLength));
                Part(segment, meshes, "Tail").Scale = Vector3.One * (1f - (0.12f * i));
                _tail.Add(segment);
            }

            if (meshes.Parts.ContainsKey("TailTip"))
            {
                Part(Joint(_tail[^1], "TailTip", new Vector3(0f, 0f, -b.TailLength)), meshes, "TailTip");
            }
        }

        Materials = [_skin];
    }

    /// <summary>The body's plan.</summary>
    public AnimalPlan Plan => _plan;

    /// <summary>The materials a flash of colour lights, when it is slapped or praised.</summary>
    public StandardMaterial3D[] Materials { get; }

    /// <summary>The head's top above the ground when standing, in body units.</summary>
    public float StandingHeight => _plan.Body.HipHeight + _plan.Body.Torso + _plan.Body.Neck + 0.9f;

    /// <summary>Puts the body in a pose.</summary>
    /// <param name="pose">The pose.</param>
    public void Apply(BodyPose pose)
    {
        _hips.Position = new Vector3(0f, pose.HipHeight, 0f);
        // A positive turn about X tilts up towards +Z (a lean forward) and a hanging limb towards -Z.
        _torso.Rotation = new Vector3(pose.Lean, pose.Twist, pose.Roll);
        _head.Rotation = new Vector3(pose.HeadPitch - pose.Lean, pose.HeadYaw, 0f);
        Leg(0, pose.LeftLeg);
        Leg(1, pose.RightLeg);
        Arm(0, pose.LeftArm, pose.ArmsOut.Left);
        Arm(1, pose.RightArm, -pose.ArmsOut.Right);
        for (var i = 0; i < _tail.Count; i++)
        {
            // The tail droops from its root and curls up along its length as it is lifted.
            var lift = i == 0 ? pose.Tail.Pitch - 0.6f : (pose.Tail.Pitch * 0.3f) - 0.15f;
            _tail[i].Rotation = new Vector3(lift + (i == 0 ? _tailMood.Tuck : 0f), (pose.Tail.Yaw + _tailMood.Swing) * (0.6f + (0.3f * i)), 0f);
        }
    }

    /// <summary>Shows how the Creature feels (ADR-055): its face, and its tail wagging in joy, lashing in anger and tucked in fear.</summary>
    /// <param name="face">Its face.</param>
    /// <param name="clock">Seconds of its own clock, for the tail.</param>
    public void Express(CreatureFace face, float clock = 0f)
    {
        _face.Express(face);
        _tailMood = ((Mathf.Sin(clock * 9f) * 0.7f * face.Wag) + (Mathf.Sin(clock * 5f) * 0.35f * face.Anger), -0.8f * face.Fear);
    }

    private (float Swing, float Tuck) _tailMood;

    /// <summary>
    /// Shows its nature (ADR-055): a kind Creature's hide warms and glows softly, a cruel one's darkens
    /// and reddens and its eyes burn red. Recoloured only as its nature moves.
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
        _skin.AlbedoColor = Colors.White.Lerp(new Color(1.08f, 1.02f, 0.9f), kindness * 0.6f).Lerp(new Color(0.55f, 0.38f, 0.36f), cruelty * 0.7f);
        RestoreGlow();
        _pupil.EmissionEnabled = cruelty > 0.3f;
        _pupil.Emission = new Color(0.9f, 0.1f, 0.05f);
        _pupil.EmissionEnergyMultiplier = cruelty * 2.2f;
    }

    /// <summary>Puts the hide's own glow back after a flash: a soft gold on a kind Creature.</summary>
    public void RestoreGlow()
    {
        var kindness = Mathf.Clamp(_nature / 10f, 0f, 1f);
        var energy = kindness > 0.3f ? kindness * 0.18f : _glow;
        _skin.EmissionEnabled = energy > 0f;
        _skin.Emission = kindness > 0.3f ? new Color(1f, 0.85f, 0.5f) : _glowColor;
        _skin.EmissionEnergyMultiplier = energy;
    }

    /// <summary>Draws the Creature's scars (spec §28): dark slashes on its flanks, one per defeat, up to six.</summary>
    /// <param name="scars">How many it has.</param>
    public void ShowScars(int scars)
    {
        var wanted = Mathf.Clamp(scars, 0, MostScarsShown);
        for (; _scarsShown < wanted; _scarsShown++)
        {
            var side = _scarsShown % 2 == 0 ? -1f : 1f;
            var height = _plan.Body.Torso * (0.4f + (0.12f * (_scarsShown / 2)));
            var scar = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.12f, 0.9f), Material = _scar },
                Position = new Vector3(side * _plan.Body.ShoulderWidth * 0.95f, height, 0.15f),
                Rotation = new Vector3(0.55f, side * 0.25f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _torso.AddChild(scar);
        }
    }

    /// <summary>One leg's angles: hip, knee and ankle, swinging forward as the pose's are positive.</summary>
    private void Leg(int i, LimbAngles angles)
    {
        var (hip, knee, ankle) = _legs[i];
        hip.Rotation = new Vector3(-angles.Upper, 0f, 0f);
        knee.Rotation = new Vector3(-angles.Lower, 0f, 0f);
        ankle.Rotation = new Vector3(-angles.End, 0f, 0f);
    }

    /// <summary>One arm's angles, raised to the side by <paramref name="outward"/>.</summary>
    private void Arm(int i, LimbAngles angles, float outward)
    {
        var (shoulder, upper, elbow, wrist) = _arms[i];
        shoulder.Rotation = new Vector3(0f, 0f, outward);
        upper.Rotation = new Vector3(-angles.Upper, 0f, 0f);
        elbow.Rotation = new Vector3(-angles.Lower, 0f, 0f);
        wrist.Rotation = new Vector3(-angles.End, 0f, 0f);
    }

    /// <summary>A collar of the god's colour about the neck's root.</summary>
    private void Collar(Color godColor, AnimalProportions b)
    {
        var ring = new MeshInstance3D
        {
            Name = "Collar",
            Mesh = new TorusMesh { InnerRadius = b.ArmGirth * 1.05f, OuterRadius = b.ArmGirth * 1.32f, Rings = 24, RingSegments = 10 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = godColor, Roughness = 0.45f, Metallic = 0.3f, EmissionEnabled = true, Emission = godColor, EmissionEnergyMultiplier = 0.35f },
            Position = new Vector3(0f, b.Torso + (b.Neck * 0.15f), b.NeckForward * 0.15f),
            Rotation = new Vector3(-0.15f, 0f, 0f),
        };
        _torso.AddChild(ring);
    }

    /// <summary>A joint.</summary>
    private static Node3D Joint(Node3D parent, string name, Vector3 position)
    {
        var joint = new Node3D { Name = name, Position = position };
        parent.AddChild(joint);
        return joint;
    }

    /// <summary>A part's mesh on its joint.</summary>
    private MeshInstance3D Part(Node3D joint, AnimalMeshes meshes, string name)
    {
        var part = new MeshInstance3D { Name = name + "Mesh", Mesh = meshes.Parts[name], MaterialOverride = _skin };
        joint.AddChild(part);
        return part;
    }

    /// <summary>A matte material.</summary>
    private static StandardMaterial3D Plain(Color color) => new() { AlbedoColor = color, Roughness = 0.6f };
}
