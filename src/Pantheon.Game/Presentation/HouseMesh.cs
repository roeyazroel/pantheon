using System;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>How a village builds its houses (spec §61, ADR-052).</summary>
public enum HouseStyle
{
    /// <summary>A round hut of wattle and daub under a thatched cone.</summary>
    RoundHut,

    /// <summary>A timber-framed longhouse under a thatched gable.</summary>
    Longhouse,

    /// <summary>A stone cottage under slate, with a chimney.</summary>
    StoneCottage,

    /// <summary>An adobe house with a flat roof and beam ends.</summary>
    Adobe,

    /// <summary>A plank house on stilts under thatch, with a ladder.</summary>
    StiltHouse,
}

/// <summary>Which colour a building's surface takes; the building shader paints each.</summary>
public enum BuildingSlot
{
    /// <summary>The walls (the instance colour), patterned by the style.</summary>
    Wall,

    /// <summary>The roof (the instance's custom colour), patterned by the style.</summary>
    Roof,

    /// <summary>Timber: posts, beams, ladders, stakes.</summary>
    Trim,

    /// <summary>Doors.</summary>
    Door,

    /// <summary>Windows.</summary>
    Window,

    /// <summary>Stone footings and chimneys.</summary>
    Stone,
}

/// <summary>How a building's walls and roof are patterned by the building shader.</summary>
public enum SurfacePattern
{
    /// <summary>Wattle and daub, or plain plaster.</summary>
    Daub,

    /// <summary>Upright planks.</summary>
    Planks,

    /// <summary>Courses of stone.</summary>
    Stone,

    /// <summary>Thatch, in layers down the slope.</summary>
    Thatch,

    /// <summary>Slate tiles.</summary>
    Slate,
}

/// <summary>
/// A village's houses as meshes built in code (spec §61, ADR-052): five styles, one or two storeys,
/// each standing on the ground at the origin with its door to +Z and fitting the 2 m radius the
/// village's layout and paths keep for a house. Walls run 0.6 m into the ground, so slopes show no
/// gap. Pure.
/// </summary>
public static class HouseMesh
{
    /// <summary>The height a house's walls run below the ground.</summary>
    public const float Sink = 0.6f;

    /// <summary>The highest point of any house, for the building shader's rising walls.</summary>
    public const float Top = 7.2f;

    /// <summary>Whether a style can have an upper storey: round huts and stilt houses never do.</summary>
    /// <param name="style">The style.</param>
    /// <returns><c>true</c> when it can.</returns>
    public static bool HasUpperStorey(HouseStyle style) => style is not (HouseStyle.RoundHut or HouseStyle.StiltHouse);

    /// <summary>The walls' and roof's patterns of a style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The patterns.</returns>
    public static (SurfacePattern Walls, SurfacePattern Roof) PatternsOf(HouseStyle style) => style switch
    {
        HouseStyle.RoundHut => (SurfacePattern.Daub, SurfacePattern.Thatch),
        HouseStyle.Longhouse => (SurfacePattern.Planks, SurfacePattern.Thatch),
        HouseStyle.StoneCottage => (SurfacePattern.Stone, SurfacePattern.Slate),
        HouseStyle.Adobe => (SurfacePattern.Daub, SurfacePattern.Daub),
        HouseStyle.StiltHouse => (SurfacePattern.Planks, SurfacePattern.Thatch),
    };

    /// <summary>Builds a house.</summary>
    /// <param name="style">Its style.</param>
    /// <param name="twoStoreys">Whether it has an upper storey, where its style can (<see cref="HasUpperStorey"/>).</param>
    /// <param name="detailed">The detailed house for villages near the camera, or the simple one.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Build(HouseStyle style, bool twoStoreys, bool detailed)
    {
        var b = new MeshBuilder(detailed);
        switch (style)
        {
            case HouseStyle.RoundHut:
                RoundHut(b, detailed);
                break;
            case HouseStyle.Longhouse:
                Framed(b, 4.2f, 3.2f, twoStoreys ? 4.1f : 2.3f, 1.7f, twoStoreys, posts: true);
                break;
            case HouseStyle.StoneCottage:
                Framed(b, 3.8f, 3.2f, twoStoreys ? 4.0f : 2.2f, 2f, twoStoreys, posts: false);
                b.Box(new Vector3(1.45f, (twoStoreys ? 4.0f : 2.2f) + 1.1f, 0.4f), new Vector3(0.5f, 2.6f, 0.5f), 0, (int)BuildingSlot.Stone);
                break;
            case HouseStyle.Adobe:
                Adobe(b, twoStoreys);
                break;
            case HouseStyle.StiltHouse:
                Stilts(b, detailed);
                break;
        }

        return b.Finish();
    }

    /// <summary>A round hut: daub walls on a stone footing, a thatched cone with a finial, a door under a lintel.</summary>
    private static void RoundHut(MeshBuilder b, bool detailed)
    {
        var sides = detailed ? 14 : 8;
        b.Tube(new Vector3(0f, -Sink, 0f), new Vector3(0f, 2.1f, 0f), 2f, 1.95f, 1f, sides, 0, (int)BuildingSlot.Wall);
        b.Tube(new Vector3(0f, -Sink, 0f), new Vector3(0f, 0.25f, 0f), 2.08f, 2.06f, 1f, sides, 0, (int)BuildingSlot.Stone);
        b.Tube(new Vector3(0f, 1.85f, 0f), new Vector3(0f, 4.5f, 0f), 2.75f, 0.05f, 1f, sides, 0, (int)BuildingSlot.Roof, capped: true);
        b.Tube(new Vector3(0f, 4.4f, 0f), new Vector3(0f, 4.95f, 0f), 0.12f, 0.02f, 1f, 5, 0, (int)BuildingSlot.Trim);
        Door(b, 1.98f, 0f);
        b.Box(new Vector3(0f, 1.78f, 2.02f), new Vector3(1.2f, 0.14f, 0.22f), 0, (int)BuildingSlot.Trim);
    }

    /// <summary>A rectangular house under a gable: walls, a sill, posts and beams for a framed house, a door and windows.</summary>
    private static void Framed(MeshBuilder b, float width, float depth, float height, float rise, bool twoStoreys, bool posts)
    {
        b.Box(new Vector3(0f, (height - Sink) / 2f, 0f), new Vector3(width, height + Sink, depth), 0, (int)BuildingSlot.Wall);
        b.Box(new Vector3(0f, 0.15f, 0f), new Vector3(width + 0.08f, 0.3f, depth + 0.08f), 0, (int)BuildingSlot.Stone);
        if (posts)
        {
            foreach (var (x, z) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
            {
                b.Box(new Vector3(x * width / 2f, (height - Sink) / 2f, z * depth / 2f), new Vector3(0.22f, height + Sink, 0.22f), 0, (int)BuildingSlot.Trim);
            }

            b.Box(new Vector3(0f, height - 0.1f, 0f), new Vector3(width + 0.06f, 0.2f, depth + 0.06f), 0, (int)BuildingSlot.Trim);
        }

        if (twoStoreys)
        {
            b.Box(new Vector3(0f, 2.15f, 0f), new Vector3(width + 0.06f, 0.18f, depth + 0.06f), 0, (int)BuildingSlot.Trim);
        }

        b.Gable(new Vector3(0f, height, 0f), width + 0.7f, depth + 0.8f, rise, 0, (int)BuildingSlot.Roof, (int)BuildingSlot.Wall, walls: new Vector2(width, depth));
        Door(b, depth / 2f, 0f);
        foreach (var x in new[] { -1.25f, 1.25f })
        {
            Window(b, new Vector3(x * width / 4.2f, 1.35f, depth / 2f));
            if (twoStoreys)
            {
                Window(b, new Vector3(x * width / 4.2f, 3.2f, depth / 2f));
            }
        }
    }

    /// <summary>An adobe house: thick plastered walls, a flat roof with a lip, beam ends, a door and small windows; an upper block set back.</summary>
    private static void Adobe(MeshBuilder b, bool twoStoreys)
    {
        const float height = 2.6f;
        b.Box(new Vector3(0f, (height - Sink) / 2f, 0f), new Vector3(3.6f, height + Sink, 3.4f), 0, (int)BuildingSlot.Wall);
        b.Box(new Vector3(0f, height + 0.1f, 0f), new Vector3(3.75f, 0.22f, 3.55f), 0, (int)BuildingSlot.Roof);
        foreach (var x in new[] { -1.3f, -0.45f, 0.45f, 1.3f })
        {
            b.Tube(new Vector3(x, height - 0.3f, 1.6f), new Vector3(x, height - 0.3f, 2.05f), 0.08f, 0.08f, 1f, 5, 0, (int)BuildingSlot.Trim, capped: true);
        }

        if (twoStoreys)
        {
            b.Box(new Vector3(-0.5f, height + 1.2f, -0.4f), new Vector3(2.4f, 2.2f, 2.4f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(-0.5f, height + 2.4f, -0.4f), new Vector3(2.55f, 0.2f, 2.55f), 0, (int)BuildingSlot.Roof);
            Window(b, new Vector3(-0.5f, height + 1.3f, 0.8f));
        }

        Door(b, 1.7f, 0.6f);
        Window(b, new Vector3(-0.9f, 1.5f, 1.7f));
    }

    /// <summary>A plank house on stilts: posts into the ground, a floor, walls, a thatched gable and a ladder up to the door.</summary>
    private static void Stilts(MeshBuilder b, bool detailed)
    {
        const float floor = 1.4f;
        var sides = detailed ? 6 : 4;
        foreach (var (x, z) in new[] { (1.4f, 1.2f), (1.4f, -1.2f), (-1.4f, 1.2f), (-1.4f, -1.2f) })
        {
            b.Tube(new Vector3(x, -Sink, z), new Vector3(x, floor, z), 0.13f, 0.11f, 1f, sides, 0, (int)BuildingSlot.Trim);
        }

        b.Box(new Vector3(0f, floor + 0.08f, 0f), new Vector3(3.6f, 0.18f, 3.2f), 0, (int)BuildingSlot.Trim);
        b.Box(new Vector3(0f, floor + 1.15f, 0f), new Vector3(3.2f, 2f, 2.8f), 0, (int)BuildingSlot.Wall);
        b.Gable(new Vector3(0f, floor + 2.15f, 0f), 3.9f, 3.6f, 1.8f, 0, (int)BuildingSlot.Roof, (int)BuildingSlot.Wall, walls: new Vector2(3.2f, 2.8f));
        b.Box(new Vector3(0f, floor + 1.0f, 1.42f), new Vector3(0.85f, 1.6f, 0.1f), 0, (int)BuildingSlot.Door);
        foreach (var x in new[] { -0.3f, 0.3f })
        {
            b.Tube(new Vector3(x, 0f, 2.3f), new Vector3(x, floor + 0.1f, 1.65f), 0.05f, 0.05f, 1f, 4, 0, (int)BuildingSlot.Trim);
        }

        for (var rung = 1; rung <= 4; rung++)
        {
            var t = rung / 5f;
            b.Box(new Vector3(0f, t * (floor + 0.1f), 2.3f - (t * 0.65f)), new Vector3(0.6f, 0.05f, 0.08f), 0, (int)BuildingSlot.Trim);
        }
    }

    private static void Door(MeshBuilder b, float front, float x) =>
        b.Box(new Vector3(x, 0.85f, front + 0.02f), new Vector3(0.9f, 1.7f, 0.12f), 0, (int)BuildingSlot.Door);

    private static void Window(MeshBuilder b, Vector3 at) =>
        b.Box(at + new Vector3(0f, 0f, 0.02f), new Vector3(0.55f, 0.5f, 0.1f), 0, (int)BuildingSlot.Window);
}
