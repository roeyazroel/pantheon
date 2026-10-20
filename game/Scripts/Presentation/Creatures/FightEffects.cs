using Godot;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// What shows a Creature's fight (ADR-062): a health bar over its head while it fights and a while
/// after, and sparks and dust where each of its blows lands on its rival.
/// </summary>
internal sealed class FightEffects
{
    private const string ShaderPath = "res://Shaders/health_bar.gdshader";

    // How long the bar stays after a fight, in seconds.
    private const float LingerSeconds = 4f;

    private static Shader? _shader;
    private readonly Node3D _parent;
    private readonly MeshInstance3D _bar;
    private GpuParticles3D? _impact;
    private float _linger;
    private long _blows = -1;

    /// <summary>Builds the bar over a Creature, hidden until it fights.</summary>
    /// <param name="root">The Creature's root, which the bar rides on.</param>
    /// <param name="parent">The node the impacts are drawn under, unscaled by the Creature's size.</param>
    public FightEffects(Node3D root, Node3D parent)
    {
        _parent = parent;
        _shader ??= GD.Load<Shader>(ShaderPath);
        _bar = new MeshInstance3D
        {
            Name = "Health",
            Mesh = new QuadMesh { Size = new Vector2(9f, 1f) },
            MaterialOverride = new ShaderMaterial { Shader = _shader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        root.AddChild(_bar);
    }

    /// <summary>Shows the bar and lands the blows for this frame.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="world">The world, for its rival.</param>
    /// <param name="shown">Where the Creature is drawn.</param>
    /// <param name="standing">The head's top above its feet, in body units.</param>
    /// <param name="scale">Its size.</param>
    /// <param name="clock">Seconds of its clock and its fighting pose's offset: each punch lands as the pose throws it.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Update(CreatureState creature, WorldState world, Vector3 shown, float standing, float scale, (float Seconds, float Offset) clock, float delta)
    {
        var activity = creature.Activity;
        var fighting = activity.Action == CreatureAction.Fight && activity.Phase == CreaturePhase.Performing;
        _linger = fighting ? LingerSeconds : Mathf.Max(0f, _linger - delta);
        _bar.Visible = _linger > 0f && activity.Phase != CreaturePhase.Banished;
        if (_bar.Visible)
        {
            _bar.Position = new Vector3(0f, standing * 1.06f, 0f);
            _bar.SetInstanceShaderParameter("health", Mathf.Clamp(creature.Health, 0f, 1f));
        }

        var blows = (long)Mathf.Floor(Pantheon.Game.Presentation.Animals.AnimalMotion.Blows(clock.Seconds, clock.Offset));
        if (!fighting || activity.TargetCreatureId is not { } id || world.Creatures.Find(c => c.Id == id) is not { } rival)
        {
            _blows = blows;
            return;
        }

        if (blows != _blows && _blows >= 0)
        {
            // Each blow lands between the two, at the height of the rival's chest.
            var towards = new Vector3(rival.Position.X - shown.X, 0f, rival.Position.Z - shown.Z);
            var reach = towards.Length();
            var at = shown + (reach > 0.01f ? towards / reach * Mathf.Min(reach * 0.55f, 6f * scale) : Vector3.Zero) + new Vector3(0f, standing * scale * 0.62f, 0f);
            Land(at);
        }

        _blows = blows;
    }

    /// <summary>A burst of dust where the Creature falls, defeated.</summary>
    /// <param name="at">Where it falls.</param>
    public void Fall(Vector3 at) => Land(at);

    /// <summary>Frees the impact's particles.</summary>
    public void Free() => _impact?.QueueFree();

    /// <summary>Fires the impact's sparks and dust at a point.</summary>
    private void Land(Vector3 at)
    {
        if (_impact is null)
        {
            _impact = MiracleLayers.Impact();
            _parent.AddChild(_impact);
        }

        _impact.GlobalPosition = at;
        _impact.Restart();
        foreach (var child in _impact.GetChildren())
        {
            (child as GpuParticles3D)?.Restart();
        }
    }
}
