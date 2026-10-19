using System.Linq;
using Godot;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.People;

/// <summary>
/// The person figure (spec §61, §80, ADR-049): the detailed and the simple mesh from
/// <see cref="PersonMesh"/>, with the person shader that animates and paints them. Built once and
/// shared by the crowds, followed villagers and migrants.
/// </summary>
public sealed class PersonAssets
{
    private static PersonAssets? _shared;

    private PersonAssets(SettlementViewSettings view)
    {
        var settings = view.People;
        (View, Settings, StrideLength) = (view, settings, view.Citizens.StrideLength);
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/person.gdshader") };
        Material.SetShaderParameter("skin_tones", settings.SkinTones.Select(Linear).ToArray());
        Material.SetShaderParameter("hair_colors", Enumerable.Range(0, 8).Select(i => Linear(settings.HairColors[i % settings.HairColors.Count])).ToArray());
        Material.SetShaderParameter("trousers", Linear(settings.Trousers));
        Material.SetShaderParameter("straw", Linear(settings.Straw));
        Material.SetShaderParameter("leather", Linear(settings.Leather));
        Material.SetShaderParameter("wood", Linear(settings.Wood));
        Material.SetShaderParameter("leg_swing", settings.LegSwing);
        Material.SetShaderParameter("arm_swing", settings.ArmSwing);
        Near = Build(PersonMesh.Build(detailed: true), Material);
        Far = Build(PersonMesh.Build(detailed: false), Material);
    }

    /// <summary>The settlement view settings the figures were built from.</summary>
    public SettlementViewSettings View { get; }

    /// <summary>The settings the figures were built with.</summary>
    public PeopleSettings Settings { get; }

    /// <summary>A step's length at life size, in metres.</summary>
    public float StrideLength { get; }

    /// <summary>The person shader's material.</summary>
    public ShaderMaterial Material { get; }

    /// <summary>The detailed figure, for people near the camera.</summary>
    public ArrayMesh Near { get; }

    /// <summary>The simple figure, for people far off.</summary>
    public ArrayMesh Far { get; }

    /// <summary>The shared figures, built on first use.</summary>
    /// <param name="view">Settlement view settings: how people look, and their stride.</param>
    /// <returns>The assets.</returns>
    public static PersonAssets For(SettlementViewSettings view) =>
        _shared is { } shared && ReferenceEquals(shared.View, view) ? shared : _shared = new PersonAssets(view);

    /// <summary>A multimesh of people, with a colour and custom data for each.</summary>
    /// <param name="detailed">Whether to start with the detailed figure.</param>
    /// <returns>The multimesh.</returns>
    internal MultiMesh NewMultiMesh(bool detailed = true) => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true,
        UseCustomData = true,
        Mesh = detailed ? Near : Far,
    };

    /// <summary>The colour a person's instance carries: their clothes (linear) and their skin tone in alpha.</summary>
    /// <param name="look">The person's look.</param>
    /// <returns>The instance colour.</returns>
    internal static Color InstanceColor(PersonLook look)
    {
        var clothes = look.Clothes.ToGodot().SrgbToLinear();
        return new Color(clothes.R, clothes.G, clothes.B, look.Skin);
    }

    /// <summary>The custom data a person's instance carries: their motion's cycle, the motion, their lean and their look.</summary>
    /// <param name="motion">What they are doing.</param>
    /// <param name="cycle">How far the motion has gone, in cycles.</param>
    /// <param name="lean">Forward lean in radians.</param>
    /// <param name="look">Their look.</param>
    /// <returns>The custom data.</returns>
    internal static Color InstanceCustom(PersonMotion motion, float cycle, float lean, PersonLook look) =>
        new(cycle - Mathf.Floor(cycle), (float)motion, lean, look.Packed);

    /// <summary>An opaque colour as a linear vector, as the shader expects.</summary>
    private static Vector3 Linear(RgbColor color)
    {
        var linear = color.ToGodot().SrgbToLinear();
        return new Vector3(linear.R, linear.G, linear.B);
    }

    /// <summary>The figure's triangles as a Godot mesh.</summary>
    private static ArrayMesh Build(PersonMeshData data, Material material)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Tags.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        // The shader raises arms above the head and sits the body down, beyond the mesh's own bounds.
        var mesh = new ArrayMesh { CustomAabb = new Aabb(new Vector3(-0.9f, -0.9f, -0.9f), new Vector3(1.8f, 3.1f, 1.8f)) };
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }
}
