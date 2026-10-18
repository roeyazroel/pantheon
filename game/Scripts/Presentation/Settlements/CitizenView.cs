using Godot;
using Pantheon.Game.Population;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Draws a settlement's representative citizens with one multimesh: each figure's body takes its
/// role's colour through the instance colour, and its head keeps the skin material.
/// </summary>
internal sealed class CitizenView
{
    private readonly MultiMeshInstance3D _instance;
    private readonly MultiMesh _multimesh;
    private readonly CitizenSettings _settings;
    private int? _shownVersion;

    /// <summary>Creates the empty, hidden view under <paramref name="parent"/>.</summary>
    /// <param name="parent">The settlement's node.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="settings">Citizen settings, for the role colours and the figure size.</param>
    public CitizenView(Node3D parent, SettlementAssets assets, CitizenSettings settings)
    {
        _settings = settings;
        _multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = assets.CitizenFigure };
        _instance = new MultiMeshInstance3D { Name = "Citizens", Multimesh = _multimesh, Visible = false };
        parent.AddChild(_instance);
    }

    /// <summary>Shows the crowd's current poses; after a roster change it also resizes and recolours the figures.</summary>
    /// <param name="crowd">The settlement's crowd, advanced this frame.</param>
    public void Show(SettlementCrowd crowd)
    {
        var poses = crowd.Poses;
        _instance.Visible = poses.Count > 0;
        if (crowd.RosterVersion != _shownVersion)
        {
            _shownVersion = crowd.RosterVersion;
            _multimesh.InstanceCount = poses.Count;
            for (var i = 0; i < poses.Count; i++)
            {
                _multimesh.SetInstanceColor(i, _settings.ColorOf(crowd.Roster[i].Role).ToGodot());
            }
        }

        var scale = Vector3.One * _settings.FigureScale;
        for (var i = 0; i < poses.Count; i++)
        {
            var pose = poses[i];
            var basis = (new Basis(Vector3.Up, pose.Yaw) * new Basis(Vector3.Right, pose.Lean)).Scaled(scale);
            _multimesh.SetInstanceTransform(i, new Transform3D(basis, pose.Position.ToGodot()));
        }
    }
}
