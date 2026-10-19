using System.Collections.Generic;
using Godot;
using Pantheon.Client.Presentation.People;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game.Population;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Draws a settlement's representative citizens as people (ADR-049) with one multimesh: each
/// figure's instance carries its look and its motion, and the person shader animates it. Beyond
/// <see cref="PeopleSettings.DetailDistance"/> the village's people use the simpler figure.
/// </summary>
internal sealed class CitizenView
{
    private readonly MultiMeshInstance3D _instance;
    private readonly MultiMesh _multimesh;
    private readonly CitizenSettings _settings;
    private readonly PersonAssets _people;
    private readonly WorldSeed _seed;
    private PersonLook[] _looks = [];
    private int? _shownVersion;

    /// <summary>Creates the empty, hidden view under <paramref name="parent"/>.</summary>
    /// <param name="parent">The settlement's node.</param>
    /// <param name="people">The person figures.</param>
    /// <param name="settings">Citizen settings, for the figure size.</param>
    /// <param name="seed">The world seed, for each person's look.</param>
    public CitizenView(Node3D parent, PersonAssets people, CitizenSettings settings, WorldSeed seed)
    {
        (_settings, _people, _seed) = (settings, people, seed);
        _multimesh = people.NewMultiMesh();
        _instance = new MultiMeshInstance3D { Name = "Citizens", Multimesh = _multimesh, Visible = false };
        parent.AddChild(_instance);
    }

    /// <summary>Shows the crowd's current poses; after a roster change it also resizes the figures and dresses them.</summary>
    /// <param name="crowd">The settlement's crowd, advanced this frame.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="followed">The villagers the gods follow: their figures hide, as they are shown in full (ADR-043).</param>
    /// <param name="cameraDistance">The camera's distance from the village, which picks the figure's detail.</param>
    public void Show(SettlementCrowd crowd, int settlementId, IReadOnlyList<VillagerState> followed, float cameraDistance)
    {
        var poses = crowd.Poses;
        _instance.Visible = poses.Count > 0;
        var mesh = cameraDistance > _people.Settings.DetailDistance ? _people.Far : _people.Near;
        if (_multimesh.Mesh != mesh)
        {
            _multimesh.Mesh = mesh;
        }

        if (crowd.RosterVersion != _shownVersion)
        {
            _shownVersion = crowd.RosterVersion;
            _multimesh.InstanceCount = poses.Count;
            _looks = new PersonLook[poses.Count];
            for (var i = 0; i < poses.Count; i++)
            {
                _looks[i] = PersonLooks.Of(_people.Settings, _seed.Value, settlementId, crowd.Roster[i].Index, crowd.Roster[i].Role);
                _multimesh.SetInstanceColor(i, PersonAssets.InstanceColor(_looks[i]));
            }
        }

        for (var i = 0; i < poses.Count; i++)
        {
            var pose = poses[i];
            var index = i;
            var hidden = followed.Count > 0 && System.Linq.Enumerable.Any(followed, v => v.SettlementId == settlementId && v.Slot == index);
            var scale = hidden ? 0f : _settings.FigureScale * _looks[i].Height;
            _multimesh.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, pose.Yaw).Scaled(Vector3.One * scale), pose.Position.ToGodot()));
            var motion = PersonMotions.Of(pose.Activity, crowd.Roster[i].Role);
            _multimesh.SetInstanceCustomData(i, PersonAssets.InstanceCustom(motion, pose.Cycle, pose.Lean, _looks[i]));
        }
    }
}
