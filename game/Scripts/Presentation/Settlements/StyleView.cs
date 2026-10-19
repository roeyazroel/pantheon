using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// A village's religious style, drawn (spec §61, ADR-042): flower beds and bushes, dark obelisks and
/// burning braziers, or white columns, laid out by <see cref="ReligiousStyles"/>. Rebuilt only when
/// the style or the layout changes.
/// </summary>
internal sealed class StyleView
{
    private readonly StyleSettings _settings;
    private readonly Dictionary<StylePieceKind, MultiMesh> _pieces = [];
    private readonly MultiMesh _flames;
    private string _shown = string.Empty;

    /// <summary>Creates the (empty) pieces under a settlement's root.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="settings">Style settings, for the colours.</param>
    public StyleView(Node3D root, StyleSettings settings)
    {
        _settings = settings;
        var tinted = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f };
        Add(root, StylePieceKind.FlowerBed, new CylinderMesh { TopRadius = 1.4f, BottomRadius = 1.5f, Height = 0.3f, RadialSegments = 8, Rings = 1, Material = tinted }, colored: true);
        Add(root, StylePieceKind.Bush, new SphereMesh { Radius = 1.1f, Height = 1.6f, RadialSegments = 8, Rings = 4, Material = tinted }, colored: true);
        Add(root, StylePieceKind.Obelisk, new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.65f, Height = 5.5f, RadialSegments = 4, Rings = 1, Material = Plain(settings.Obelisk, 0.6f) }, colored: false);
        Add(root, StylePieceKind.Brazier, new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.35f, Height = 1.3f, RadialSegments = 8, Rings = 1, Material = Plain(settings.Obelisk, 0.7f) }, colored: false);
        Add(root, StylePieceKind.Column, new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.45f, Height = 4.2f, RadialSegments = 10, Rings = 1, Material = Plain(settings.Column, 0.5f) }, colored: false);
        var fire = new StandardMaterial3D { AlbedoColor = settings.Flame.ToGodot(), EmissionEnabled = true, Emission = settings.Flame.ToGodot(), EmissionEnergyMultiplier = 2.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        _flames = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f, RadialSegments = 6, Rings = 3, Material = fire } };
        root.AddChild(new MultiMeshInstance3D { Name = "StyleFlames", Multimesh = _flames, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Shows the village's pieces, rebuilt only when they change.</summary>
    /// <param name="style">Its style, or <c>null</c> for none.</param>
    /// <param name="pieces">Its pieces.</param>
    /// <param name="key">What the pieces depend on.</param>
    public void Show(ReligiousStyle? style, IReadOnlyList<StylePiece> pieces, string key)
    {
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        var byKind = new Dictionary<StylePieceKind, List<StylePiece>>();
        foreach (var piece in style is null ? [] : pieces)
        {
            if (!byKind.TryGetValue(piece.Kind, out var list))
            {
                byKind[piece.Kind] = list = [];
            }

            list.Add(piece);
        }

        var flames = 0;
        foreach (var (kind, mesh) in _pieces)
        {
            var list = byKind.GetValueOrDefault(kind) ?? [];
            mesh.InstanceCount = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                var piece = list[i];
                var lift = kind switch
                {
                    StylePieceKind.FlowerBed => 0.08f,
                    StylePieceKind.Bush => 0.6f,
                    StylePieceKind.Obelisk => 2.6f,
                    StylePieceKind.Brazier => 0.6f,
                    StylePieceKind.Column => 2f,
                } * piece.Scale;
                var basis = new Basis(Vector3.Up, piece.Yaw).Scaled(new Vector3(piece.Scale, piece.Scale, piece.Scale));
                mesh.SetInstanceTransform(i, new Transform3D(basis, piece.Position.ToGodot() + new Vector3(0f, lift, 0f)));
                if (mesh.UseColors)
                {
                    mesh.SetInstanceColor(i, kind == StylePieceKind.Bush ? _settings.Bush.ToGodot() : Flower(piece.Variant));
                }

                flames += kind == StylePieceKind.Brazier ? 1 : 0;
            }
        }

        var braziers = byKind.GetValueOrDefault(StylePieceKind.Brazier) ?? [];
        _flames.InstanceCount = flames;
        for (var i = 0; i < braziers.Count; i++)
        {
            _flames.SetInstanceTransform(i, new Transform3D(Basis.Identity, braziers[i].Position.ToGodot() + new Vector3(0f, (1.3f * braziers[i].Scale) + 0.35f, 0f)));
        }
    }

    /// <summary>A key for what a village's pieces depend on.</summary>
    /// <param name="style">Its style.</param>
    /// <param name="pieces">Its pieces.</param>
    /// <param name="discRadius">Its houses' reach.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(ReligiousStyle? style, int pieces, float discRadius) =>
        style is null ? "none" : string.Create(CultureInfo.InvariantCulture, $"{style.Motif}:{style.GodId}:{pieces}:{discRadius:0.0}");

    /// <summary>A flower bed's colour by its variant.</summary>
    private Color Flower(float variant)
    {
        var colors = _settings.FlowerColors;
        return colors[Mathf.Clamp((int)(variant * colors.Count), 0, colors.Count - 1)].ToGodot();
    }

    /// <summary>Adds one kind of piece.</summary>
    private void Add(Node3D root, StylePieceKind kind, Mesh mesh, bool colored)
    {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colored, Mesh = mesh };
        _pieces[kind] = multi;
        root.AddChild(new MultiMeshInstance3D { Name = "Style" + kind, Multimesh = multi });
    }

    /// <summary>A plain material of a colour.</summary>
    private static StandardMaterial3D Plain(RgbColor color, float roughness) => new() { AlbedoColor = color.ToGodot(), Roughness = roughness };
}
