using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Events;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Disasters;

/// <summary>
/// Visual debris (spec §94, §55, ADR-048): when the simulation brings a house down, or a thrown thing
/// or a falling star strikes, planks, stones and thatch fly off as Jolt rigid bodies, tumble onto a
/// patch of the terrain's shape and fade. Presentation only: the simulation decided what fell.
/// </summary>
public sealed class DebrisPresenter
{
    private readonly Node3D _parent;
    private readonly DebrisSettings _settings;
    private readonly Func<TerrainField> _terrain;
    private readonly List<(RigidBody3D Body, MeshInstance3D Mesh, double Age, StaticBody3D Ground)> _pieces = [];
    private readonly Dictionary<StaticBody3D, int> _grounds = [];
    private readonly Dictionary<DebrisShape, StandardMaterial3D> _materials;

    /// <summary>Creates the presenter.</summary>
    /// <param name="parent">The node that holds the debris.</param>
    /// <param name="settings">Debris settings.</param>
    /// <param name="terrain">The current terrain, for the ground the pieces land on.</param>
    public DebrisPresenter(Node3D parent, DebrisSettings settings, Func<TerrainField> terrain)
    {
        (_parent, _settings, _terrain) = (parent, settings, terrain);
        _materials = new Dictionary<DebrisShape, StandardMaterial3D>
        {
            [DebrisShape.Plank] = new() { AlbedoColor = settings.Wood.ToGodot(), Roughness = 0.95f },
            [DebrisShape.Stone] = new() { AlbedoColor = settings.Stone.ToGodot(), Roughness = 1f },
            [DebrisShape.Shard] = new() { AlbedoColor = settings.Thatch.ToGodot(), Roughness = 1f },
        };
    }

    /// <summary>Pieces flying or lying now.</summary>
    public int Count => _pieces.Count;

    /// <summary>Pieces still moving: the rest have come to rest and sleep.</summary>
    public int Moving => _pieces.Count(p => !p.Body.Sleeping);

    /// <summary>Throws up debris where houses fell within reach of the camera.</summary>
    /// <param name="houses">The fallen houses.</param>
    /// <param name="camera">The camera's place.</param>
    public void Houses(IReadOnlyList<HutPlacement> houses, Vector3 camera)
    {
        foreach (var house in houses)
        {
            Burst(DebrisCause.House, house.Position.ToGodot(), house.Scale, camera);
        }
    }

    /// <summary>Throws up stones where the hand's thrown things or falling stars struck, and lands the thrown thing itself.</summary>
    /// <param name="events">The events handled this frame.</param>
    /// <param name="camera">The camera's place.</param>
    public void React(IReadOnlyList<WorldEvent> events, Vector3 camera)
    {
        foreach (var worldEvent in events)
        {
            var at = worldEvent switch
            {
                HandStruck struck => struck.Position,
                MeteorStruck meteor => meteor.Position,
                _ => (Domain.World.GroundPosition?)null,
            };
            if (at is { } point)
            {
                var ground = new Vector3(point.X, _terrain().SampleHeight(point.X, point.Z), point.Z);
                var patch = Burst(DebrisCause.Strike, ground, 1f, camera);
                if (worldEvent is HandStruck hand && ground.DistanceTo(camera) <= _settings.Reach
                    && DebrisLayout.Thrown(_settings, hand.Kind, new System.Numerics.Vector2(ground.X - camera.X, ground.Z - camera.Z)) is { } thrown)
                {
                    Add(thrown, ground, patch ?? Ground(ground));
                }
            }
        }
    }

    /// <summary>Ages the pieces: they fade at the end of their time and go, and their ground with the last of them.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(double delta)
    {
        for (var i = _pieces.Count - 1; i >= 0; i--)
        {
            var (body, mesh, age, ground) = _pieces[i];
            age += delta;
            var left = _settings.Lifetime - age;
            if (left <= 0.0)
            {
                Remove(i);
                continue;
            }

            if (left < _settings.FadeSeconds)
            {
                mesh.Scale = Vector3.One * (float)Math.Max(0.05, left / _settings.FadeSeconds);
            }

            _pieces[i] = (body, mesh, age, ground);
        }
    }

    /// <summary>Removes every piece, for a new or loaded world.</summary>
    public void Clear()
    {
        while (_pieces.Count > 0)
        {
            Remove(_pieces.Count - 1);
        }
    }

    /// <summary>One burst: its pieces and the ground they land on, which it returns; none beyond reach.</summary>
    private StaticBody3D? Burst(DebrisCause cause, Vector3 at, float scale, Vector3 camera)
    {
        if (at.DistanceTo(camera) > _settings.Reach)
        {
            return null;
        }

        var pieces = DebrisLayout.Burst(_settings, cause, DebrisLayout.KeyOf(at.X, at.Z), scale);
        if (pieces.Count == 0)
        {
            return null;
        }

        var ground = Ground(at);
        foreach (var piece in pieces)
        {
            Add(piece, at, ground);
        }

        return ground;
    }

    /// <summary>One piece as a rigid body, making room for it when there are too many.</summary>
    private void Add(DebrisPiece piece, Vector3 at, StaticBody3D ground)
    {
        while (_pieces.Count >= _settings.MaxBodies)
        {
            Remove(0);
        }

        var size = piece.Size.ToGodot();
        var body = new RigidBody3D
        {
            Name = "Debris",
            Position = at + piece.Offset.ToGodot(),
            LinearVelocity = piece.Velocity.ToGodot(),
            AngularVelocity = piece.Spin.ToGodot(),
            Mass = Math.Max(0.5f, size.X * size.Y * size.Z * 400f),
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = _materials[piece.Shape] };
        body.AddChild(mesh);
        _parent.AddChild(body);
        _pieces.Add((body, mesh, 0.0, ground));
        _grounds[ground] = _grounds.GetValueOrDefault(ground) + 1;
    }

    /// <summary>A patch of the terrain's shape under a burst, for its pieces to land on.</summary>
    private StaticBody3D Ground(Vector3 at)
    {
        var terrain = _terrain();
        var side = (int)MathF.Ceiling(_settings.GroundPatch) + 1;
        var data = new float[side * side];
        var half = (side - 1) / 2f;
        for (var z = 0; z < side; z++)
        {
            for (var x = 0; x < side; x++)
            {
                data[(z * side) + x] = MathF.Max(terrain.SampleHeight(at.X + x - half, at.Z + z - half), 0f);
            }
        }

        var ground = new StaticBody3D { Name = "DebrisGround", Position = new Vector3(at.X, 0f, at.Z) };
        ground.AddChild(new CollisionShape3D { Shape = new HeightMapShape3D { MapWidth = side, MapDepth = side, MapData = data } });
        _parent.AddChild(ground);
        return ground;
    }

    /// <summary>Removes a piece, and its ground when it was the last on it.</summary>
    private void Remove(int index)
    {
        var (body, _, _, ground) = _pieces[index];
        _pieces.RemoveAt(index);
        body.QueueFree();
        if (_grounds.TryGetValue(ground, out var left) && --left <= 0)
        {
            _grounds.Remove(ground);
            ground.QueueFree();
        }
        else
        {
            _grounds[ground] = left;
        }
    }
}
