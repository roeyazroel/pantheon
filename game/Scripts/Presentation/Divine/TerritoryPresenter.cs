using System;
using System.Threading.Tasks;
using Godot;
using Pantheon.Game;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>
/// The gods' territories on the land (ADR-069): <see cref="TerritoryImage"/> laid over the island as a
/// decal, so it hugs the ground of either renderer and never the houses. It is painted again off the
/// main thread every few seconds when the gods' influence has moved, or when the camera has risen or
/// fallen a zoom band (the borders widen with height), and the old picture stays until the new one is
/// ready.
/// </summary>
internal sealed class TerritoryPresenter
{
    private const float Depth = 600f;

    // How often the influence is looked at again, in real seconds.
    private const double RefreshSeconds = 2.5;

    private readonly Decal _decal;
    private readonly DivineViewSettings _view;
    private ImageTexture? _texture;
    private Task<(byte[] Rgba, int Side)>? _painting;
    private double _since = RefreshSeconds;
    private int _shown;
    private int _band = int.MinValue;

    /// <summary>Creates the territories' decal under a parent.</summary>
    /// <param name="parent">Node that holds it.</param>
    /// <param name="view">The gods' colours.</param>
    public TerritoryPresenter(Node3D parent, DivineViewSettings view)
    {
        _view = view;
        _decal = new Decal { Name = "Territories", Visible = false, UpperFade = 0f, LowerFade = 0f, CullMask = DeclutteredLabel.GroundOnly, SortingOffset = 1f };
        parent.AddChild(_decal);
    }

    /// <summary>Paints the territories again soon, for a new or loaded world.</summary>
    public void Reset()
    {
        _shown = 0;
        _since = RefreshSeconds;
    }

    /// <summary>Takes a finished picture, and starts a new one when it is time and the influence has moved.</summary>
    /// <param name="session">The session.</param>
    /// <param name="distance">The camera's distance, in metres.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(GameSession session, float distance, double delta)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_painting is { IsCompleted: true } done)
        {
            _painting = null;
            if (done.IsCompletedSuccessfully)
            {
                Show(done.Result.Rgba, done.Result.Side);
            }
        }

        _since += delta;
        var band = TerritoryLook.Band(distance);
        if (_painting is not null || (_since < RefreshSeconds && band == _band))
        {
            return;
        }

        _since = 0;
        var world = session.World;
        var fields = Territories.Fields(session.Content.DivineRules, world);
        var key = Key(fields, session.LocalGodId);
        if (key == _shown && band == _band)
        {
            return;
        }

        (_shown, _band) = (key, band);
        var extent = world.Regions.Columns * world.Regions.RegionSize;
        var me = session.LocalGodId;
        var terrain = session.Terrain;
        _decal.Position = new Vector3(extent / 2f, Depth / 4f, extent / 2f);
        _decal.Size = new Vector3(extent, Depth, extent);
        _painting = Task.Run(() =>
        {
            var side = TerritoryImage.SideFor(extent);
            var territories = Territories.Build(fields, extent);
            var look = TerritoryLook.For(extent, TerritoryLook.DistanceOf(band));
            return (TerritoryImage.Render(territories, side, _view.GodColor, me, look, (x, z) => terrain.SampleHeight(x, z) > 0.3f), side);
        });
    }

    /// <summary>Lays a picture over the land.</summary>
    private void Show(byte[] rgba, int side)
    {
        var image = Image.CreateFromData(side, side, false, Image.Format.Rgba8, rgba);
        if (_texture is not null && _texture.GetWidth() == side)
        {
            _texture.Update(image);
        }
        else
        {
            _texture = ImageTexture.CreateFromImage(image);
            _decal.TextureAlbedo = _texture;
        }

        _decal.Visible = true;
    }

    /// <summary>A key of the gods' influence, its places rounded to a few metres, so a picture is painted again only when it would differ.</summary>
    private static int Key(System.Collections.Generic.IReadOnlyList<Pantheon.Simulation.Belief.InfluenceField> fields, int? me)
    {
        var hash = new HashCode();
        hash.Add(me);
        foreach (var field in fields)
        {
            hash.Add(field.GodId);
            foreach (var source in field.Sources)
            {
                hash.Add((int)(source.Position.X / 6f));
                hash.Add((int)(source.Position.Z / 6f));
                hash.Add((int)(source.Strength * 100f));
                hash.Add((int)(source.Reach / 6f));
            }
        }

        return hash.ToHashCode();
    }
}
