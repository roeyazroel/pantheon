using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// The wildlife near the camera (spec §61, ADR-054): the herds of deer and flocks of birds
/// <see cref="Wildlife"/> places each frame, in one multimesh for each kind, animated by the animal
/// shader. Presentation only.
/// </summary>
public sealed class WildlifePresenter
{
    private const double BareSeconds = 5.0;

    private readonly SettlementViewSettings _view;
    private readonly SettlementRules _rules;
    private readonly MultiMesh _deer;
    private readonly MultiMesh _birds;
    private readonly List<AnimalPose> _poses = [];
    private List<Clearing> _bare = [];
    private readonly List<Clearing> _near = [];
    private TerrainField? _terrain;
    private double _sinceBare = double.MaxValue;

    /// <summary>Creates the (empty) herds and flocks under a parent.</summary>
    /// <param name="parent">Node that holds the wildlife.</param>
    /// <param name="view">Settlement view settings: the wildlife and the villages' layout.</param>
    /// <param name="rules">Settlement rules, for the villages' bare ground.</param>
    public WildlifePresenter(Node3D parent, SettlementViewSettings view, SettlementRules rules)
    {
        (_view, _rules) = (view, rules);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/animal.gdshader") };
        material.SetShaderParameter("light_color", Linear(new Color(0.9f, 0.85f, 0.75f)));
        material.SetShaderParameter("dark_color", Linear(new Color(0.12f, 0.1f, 0.09f)));
        _deer = New(parent, "Deer", Meshes.ToArrayMesh(AnimalMesh.Build(AnimalKind.Deer), material));
        _birds = New(parent, "Birds", Meshes.ToArrayMesh(AnimalMesh.Build(AnimalKind.Bird), material));
    }

    /// <summary>Deer and birds drawn now, for the debug panel.</summary>
    public (int Deer, int Birds) Counts => (_deer.InstanceCount, _birds.InstanceCount);

    /// <summary>Clears the wildlife for a new or loaded world.</summary>
    /// <param name="terrain">Its terrain.</param>
    public void Rebuild(TerrainField terrain)
    {
        _terrain = terrain;
        _sinceBare = double.MaxValue;
        _deer.InstanceCount = 0;
        _birds.InstanceCount = 0;
    }

    /// <summary>Places the herds and flocks for the crowds' time.</summary>
    /// <param name="world">The world.</param>
    /// <param name="camera">The camera's place.</param>
    /// <param name="seconds">The crowds' time.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(WorldState world, Vector3 camera, double seconds, double delta)
    {
        if (_terrain is null)
        {
            return;
        }

        _sinceBare += delta;
        if (_sinceBare >= BareSeconds)
        {
            _sinceBare = 0.0;
            _bare = [];
            foreach (var settlement in world.Settlements.Where(s => s.Population > 0 || s.Housing > 0))
            {
                GroundCoverLayout.BareGround(SettlementLayout.ComputeSite(settlement, _rules, _terrain, _view.Layout), _view.Layout.FieldSize, _view.Layout.ShrineClearance, _bare);
            }
        }

        // Only the bare ground within reach matters; a big world's villages hold tens of thousands of patches.
        var at = new System.Numerics.Vector2(camera.X, camera.Z);
        var reach = _view.Wildlife.Radius + 120f;
        _near.Clear();
        _near.AddRange(_bare.Where(patch => System.Numerics.Vector2.DistanceSquared(patch.Center, at) < reach * reach));
        _poses.Clear();
        Wildlife.Pose(world, _terrain, at, seconds, _near, _view.Wildlife, _poses);
        Fill(_deer, AnimalKind.Deer, _view.Wildlife.Coats, world.Seed.Value);
        Fill(_birds, AnimalKind.Bird, _view.Wildlife.Plumage, world.Seed.Value);
    }

    /// <summary>Writes one kind's animals into its multimesh.</summary>
    private void Fill(MultiMesh mesh, AnimalKind kind, IReadOnlyList<RgbColor> colors, ulong seed)
    {
        var animals = _poses.Where(p => p.Kind == kind).ToList();
        mesh.InstanceCount = animals.Count;
        for (var i = 0; i < animals.Count; i++)
        {
            var animal = animals[i];
            var basis = new Basis(Vector3.Up, animal.Yaw).Scaled(Vector3.One * animal.Size);
            mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(animal.Position.X, animal.Position.Y, animal.Position.Z)));
            var color = colors[(int)(PresentationHash.Mix(seed, (ulong)i, (ulong)kind) % (ulong)colors.Count)].ToGodot().SrgbToLinear();
            mesh.SetInstanceColor(i, color);
            mesh.SetInstanceCustomData(i, new Color(animal.Cycle - Mathf.Floor(animal.Cycle), (float)animal.Motion, animal.Antlers ? 1f : 0f, 0f));
        }
    }

    private static MultiMesh New(Node3D parent, string name, Mesh mesh)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh };
        parent.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh });
        return multimesh;
    }

    private static Vector3 Linear(Color color)
    {
        var linear = color.SrgbToLinear();
        return new Vector3(linear.R, linear.G, linear.B);
    }
}
