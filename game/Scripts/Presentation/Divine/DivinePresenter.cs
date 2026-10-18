using Godot;
using Pantheon.Domain.Events;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>Visuals of divine acts: the targeting ring, the map overlay and the effects of cast miracles.</summary>
public sealed class DivinePresenter
{
    private readonly Node3D _effects;
    private readonly DivineViewSettings _view;
    private readonly TargetRing _ring;
    private readonly MapOverlay _overlay;
    private TerrainField? _terrain;

    /// <summary>Creates the presenter's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the divine visuals.</param>
    /// <param name="view">God colours, overlay opacity and miracle looks.</param>
    public DivinePresenter(Node3D parent, DivineViewSettings view)
    {
        _view = view;
        _effects = parent.AddLayer("Effects");
        _ring = new TargetRing(parent);
        _overlay = new MapOverlay(parent);
    }

    /// <summary>Uses a new world's terrain and clears effects of the old world.</summary>
    /// <param name="terrain">Terrain of the current world.</param>
    /// <param name="worldSize">Side of the square world in metres (the region grid's extent).</param>
    public void Rebuild(TerrainField terrain, float worldSize)
    {
        _terrain = terrain;
        _effects.FreeChildren();
        _overlay.Fit(worldSize);
        _ring.Hide();
    }

    /// <summary>Plays the effect of a cast miracle.</summary>
    /// <param name="cast">The cast.</param>
    /// <param name="radius">The miracle's radius in metres.</param>
    public void Play(MiracleCast cast, float radius)
    {
        var effect = MiracleEffect.Create(_view.EffectFor(cast.MiracleId), Ground(cast.Target.X, cast.Target.Z), radius);
        _effects.AddChild(effect);
    }

    /// <summary>Shows the targeting ring at a ground point.</summary>
    /// <param name="point">Ground point under the cursor.</param>
    /// <param name="radius">Radius of the selected miracle.</param>
    /// <param name="valid">Whether it can be cast there.</param>
    public void ShowTarget(System.Numerics.Vector3 point, float radius, bool valid) => _ring.Show(point.ToGodot(), radius, valid);

    /// <summary>Hides the targeting ring.</summary>
    public void HideTarget() => _ring.Hide();

    /// <summary>Shows a map overlay, or hides it.</summary>
    /// <param name="image">Overlay image, or <c>null</c>.</param>
    public void ShowOverlay(OverlayImage? image) => _overlay.Show(image);

    /// <summary>The ground point (or sea surface) below a position.</summary>
    private Vector3 Ground(float x, float z) => new(x, _terrain is null ? 0f : Mathf.Max(_terrain.SampleHeight(x, z), 0f), z);
}
