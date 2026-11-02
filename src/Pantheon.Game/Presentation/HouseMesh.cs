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
/// A village's houses as meshes built in code (spec §61, ADR-052, ADR-077): five styles of
/// <see cref="Variants"/> variants each, one or two storeys, each standing on the ground at the origin
/// with its door to +Z, on the plot the village's plan gives it. Deep roofs with wide eaves carry the
/// look from the god's height. Walls run 0.6 m into the ground, so slopes show no gap. Pure.
/// </summary>
public static class HouseMesh
{
    /// <summary>The height a house's walls run below the ground.</summary>
    public const float Sink = 0.6f;

    /// <summary>The highest point of any house, for the building shader's rising walls.</summary>
    public const float Top = 7.2f;

    /// <summary>How many variants each style has.</summary>
    public const int Variants = 4;

    /// <summary>Whether a style can have an upper storey: round huts and stilt houses never do.</summary>
    /// <param name="style">The style.</param>
    /// <returns><c>true</c> when it can.</returns>
    public static bool HasUpperStorey(HouseStyle style) => style is not (HouseStyle.RoundHut or HouseStyle.StiltHouse);

    /// <summary>The height a house's smoke rises from (ADR-080).</summary>
    /// <param name="style">Its style.</param>
    /// <param name="twoStoreys">Whether it has an upper storey.</param>
    /// <returns>Metres above its foot.</returns>
    public static float SmokeHeight(HouseStyle style, bool twoStoreys) => (twoStoreys ? 1.8f : 0f) + style switch
    {
        HouseStyle.RoundHut => 4.9f,
        HouseStyle.Longhouse => 4.6f,
        HouseStyle.StoneCottage => 3.9f,
        HouseStyle.Adobe => 2.9f,
        HouseStyle.StiltHouse => 5.4f,
    };

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
    /// <param name="variant">Which of the style's <see cref="Variants"/> variants.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Build(HouseStyle style, bool twoStoreys, bool detailed, int variant = 0)
    {
        var b = new MeshBuilder(detailed);
        switch (style)
        {
            case HouseStyle.RoundHut:
                RoundHut(b, detailed, variant);
                break;
            case HouseStyle.Longhouse:
                Longhouse(b, twoStoreys, variant);
                break;
            case HouseStyle.StoneCottage:
                Cottage(b, twoStoreys, variant);
                break;
            case HouseStyle.Adobe:
                Adobe(b, twoStoreys, variant);
                break;
            case HouseStyle.StiltHouse:
                Stilts(b, detailed, variant);
                break;
        }

        return b.Finish();
    }

    /// <summary>Round huts: daub on a stone footing under a deep thatched cone; a smaller hut beside it, a banded cone or a porch.</summary>
    private static void RoundHut(MeshBuilder b, bool detailed, int variant)
    {
        var sides = detailed ? 14 : 8;
        var (radius, height, rise) = variant switch
        {
            1 => (1.7f, 1.9f, 2.5f),
            2 => (2f, 2f, 3.3f),
            3 => (2.1f, 2f, 2.7f),
            _ => (2f, 2.1f, 2.9f),
        };
        Hut(b, Vector3.Zero, radius, height, rise, sides);
        Door(b, radius - 0.02f, 0f);
        b.Box(new Vector3(0f, 1.78f, radius + 0.04f), new Vector3(1.2f, 0.14f, 0.22f), 0, (int)BuildingSlot.Trim);
        if (variant == 1)
        {
            Hut(b, new Vector3(-2.3f, 0f, -1f), 1.05f, 1.5f, 1.8f, sides);
        }
        else if (variant == 2)
        {
            var eave = height + (rise * 0.42f);
            b.Tube(new Vector3(0f, eave, 0f), new Vector3(0f, eave + 0.14f, 0f), 1.72f, 1.6f, 1f, sides, 0, (int)BuildingSlot.Trim);
        }
        else if (variant == 3)
        {
            Porch(b, radius - 0.15f, 1.75f, 2.2f, 1.2f);
        }
    }

    /// <summary>A hut's walls, footing and cone, standing at a point.</summary>
    private static void Hut(MeshBuilder b, Vector3 at, float radius, float height, float rise, int sides)
    {
        const float eaves = 0.7f;
        b.Tube(at + new Vector3(0f, -Sink, 0f), at + new Vector3(0f, height, 0f), radius, radius * 0.97f, 1f, sides, 0, (int)BuildingSlot.Wall);
        b.Tube(at + new Vector3(0f, -Sink, 0f), at + new Vector3(0f, 0.25f, 0f), radius + 0.08f, radius + 0.06f, 1f, sides, 0, (int)BuildingSlot.Stone);
        b.Tube(at + new Vector3(0f, height - 0.3f, 0f), at + new Vector3(0f, height + rise, 0f), radius + eaves, 0.05f, 1f, sides, 0, (int)BuildingSlot.Roof, capped: true);
        b.Tube(at + new Vector3(0f, height + rise - 0.1f, 0f), at + new Vector3(0f, height + rise + 0.45f, 0f), 0.12f, 0.02f, 1f, 5, 0, (int)BuildingSlot.Trim);
    }

    /// <summary>Timber houses: a long gable, one with a lean-to at its end, a hipped roof, or a porch.</summary>
    private static void Longhouse(MeshBuilder b, bool twoStoreys, int variant)
    {
        var height = twoStoreys ? 4.1f : 2.2f;
        switch (variant)
        {
            case 1:
                Framed(b, 3.8f, 3.2f, height, 2.1f, twoStoreys, posts: true);
                LeanTo(b, -1.9f, 1.5f, 2.6f, 2f, posts: true);
                break;
            case 2:
                Framed(b, 4.2f, 3.2f, height, 2f, twoStoreys, posts: true, hipped: true);
                break;
            case 3:
                Framed(b, 4f, 3f, height, 2.2f, twoStoreys, posts: true);
                Porch(b, 1.5f, 1.85f, 3.2f, 1.3f);
                break;
            default:
                Framed(b, 4.6f, 3.2f, height, 2.2f, twoStoreys, posts: true);
                break;
        }
    }

    /// <summary>Stone cottages: a chimney at one end or the other, a lean-to, a hipped roof or a porch.</summary>
    private static void Cottage(MeshBuilder b, bool twoStoreys, int variant)
    {
        var height = twoStoreys ? 4f : 2.2f;
        switch (variant)
        {
            case 1:
                Framed(b, 3.6f, 3.2f, height, 2.2f, twoStoreys, posts: false);
                LeanTo(b, 1.8f, 1.4f, 2.6f, 2f, posts: false);
                Chimney(b, -1.3f, height + 1.5f);
                break;
            case 2:
                Framed(b, 3.8f, 3.4f, height, 1.9f, twoStoreys, posts: false, hipped: true);
                Chimney(b, 0.8f, height + 2.1f);
                break;
            case 3:
                Framed(b, 4f, 3f, height, 2.2f, twoStoreys, posts: false);
                Porch(b, 1.5f, 1.85f, 2.4f, 1.2f);
                Chimney(b, -1.45f, height + 1.6f);
                break;
            default:
                Framed(b, 4.2f, 3.2f, height, 2.2f, twoStoreys, posts: false);
                Chimney(b, 1.55f, height + 1.6f);
                break;
        }
    }

    /// <summary>A chimney of stone, from the ground to its top, at a place along the ridge.</summary>
    private static void Chimney(MeshBuilder b, float x, float top)
    {
        b.Box(new Vector3(x, (top - Sink) / 2f, 0.45f), new Vector3(0.55f, top + Sink, 0.55f), 0, (int)BuildingSlot.Stone);
        b.Box(new Vector3(x, top + 0.06f, 0.45f), new Vector3(0.7f, 0.12f, 0.7f), 0, (int)BuildingSlot.Stone);
    }

    /// <summary>A rectangular house under a deep roof: walls, a sill, posts and beams for a framed house, a door and windows.</summary>
    private static void Framed(MeshBuilder b, float width, float depth, float height, float rise, bool twoStoreys, bool posts, bool hipped = false)
    {
        const float eaves = 0.55f;
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

        var eaveAt = new Vector3(0f, height - 0.25f, 0f);
        if (hipped)
        {
            b.Hip(eaveAt, width + (2f * eaves), depth + (2f * eaves), rise + 0.25f, width * 0.45f, (int)BuildingSlot.Roof);
        }
        else
        {
            b.Gable(eaveAt, width + (2f * eaves), depth + (2f * eaves), rise + 0.25f, 0, (int)BuildingSlot.Roof, (int)BuildingSlot.Wall, walls: new Vector2(width, depth));
            b.Box(eaveAt + new Vector3(0f, rise + 0.3f, 0f), new Vector3(width + (2f * eaves) + 0.1f, 0.12f, 0.2f), 0, (int)BuildingSlot.Trim);
        }

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

    /// <summary>A low room against an end of the house under a single slope.</summary>
    private static void LeanTo(MeshBuilder b, float wall, float reach, float depth, float height, bool posts)
    {
        var side = MathF.Sign(wall);
        var far = wall + (side * reach);
        var middle = (wall + far) / 2f;
        b.Box(new Vector3(middle, (height - 0.5f - Sink) / 2f, 0f), new Vector3(reach, height - 0.5f + Sink, depth), 0, (int)BuildingSlot.Wall);
        var (eave, top) = (height - 0.75f, height + 0.35f);
        var overhang = far + (side * 0.3f);
        var half = (depth / 2f) + 0.3f;
        b.Roof(new Vector3(overhang, eave, -half), new Vector3(overhang, eave, half), new Vector3(wall, top, half), new Vector3(wall, top, -half), (int)BuildingSlot.Roof);
        if (posts)
        {
            b.Box(new Vector3(far, (height - 0.5f - Sink) / 2f, depth / 2f), new Vector3(0.18f, height - 0.5f + Sink, 0.18f), 0, (int)BuildingSlot.Trim);
        }
    }

    /// <summary>A roof on two posts over the door, sloping out from the wall.</summary>
    private static void Porch(MeshBuilder b, float wall, float height, float width, float reach)
    {
        var front = wall + reach;
        foreach (var x in new[] { -1f, 1f })
        {
            b.Box(new Vector3(x * ((width / 2f) - 0.15f), (height - Sink) / 2f, front - 0.1f), new Vector3(0.16f, height + Sink, 0.16f), 0, (int)BuildingSlot.Trim);
        }

        b.Roof(new Vector3(-width / 2f, height, front + 0.15f), new Vector3(width / 2f, height, front + 0.15f), new Vector3(width / 2f, height + 0.5f, wall), new Vector3(-width / 2f, height + 0.5f, wall), (int)BuildingSlot.Roof);
    }

    /// <summary>Adobe houses: thick plastered walls, a flat roof with a lip and beam ends; a lower room beside it, a raised parapet or a walled yard.</summary>
    private static void Adobe(MeshBuilder b, bool twoStoreys, int variant)
    {
        const float height = 2.6f;
        b.Box(new Vector3(0f, (height - Sink) / 2f, 0f), new Vector3(3.6f, height + Sink, 3.4f), 0, (int)BuildingSlot.Wall);
        b.Box(new Vector3(0f, height + 0.1f, 0f), new Vector3(3.8f, 0.24f, 3.6f), 0, (int)BuildingSlot.Roof);
        foreach (var x in new[] { -1.3f, -0.45f, 0.45f, 1.3f })
        {
            b.Tube(new Vector3(x, height - 0.3f, 1.6f), new Vector3(x, height - 0.3f, 2.05f), 0.08f, 0.08f, 1f, 5, 0, (int)BuildingSlot.Trim, capped: true);
        }

        if (variant == 1)
        {
            b.Box(new Vector3(2.5f, 0.625f, -0.2f), new Vector3(1.6f, 2.45f, 2.6f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(2.5f, 1.95f, -0.2f), new Vector3(1.8f, 0.2f, 2.8f), 0, (int)BuildingSlot.Roof);
        }
        else if (variant == 2)
        {
            foreach (var (at, size) in new[] { (new Vector3(0f, height + 0.4f, 1.72f), new Vector3(3.6f, 0.5f, 0.2f)), (new Vector3(0f, height + 0.4f, -1.72f), new Vector3(3.6f, 0.5f, 0.2f)), (new Vector3(1.82f, height + 0.4f, 0f), new Vector3(0.2f, 0.5f, 3.3f)), (new Vector3(-1.82f, height + 0.4f, 0f), new Vector3(0.2f, 0.5f, 3.3f)) })
            {
                b.Box(at, size, 0, (int)BuildingSlot.Wall);
            }
        }
        else if (variant == 3)
        {
            b.Box(new Vector3(-2.7f, 0.25f, 0f), new Vector3(0.3f, 1.7f, 3.4f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(-2.3f, 0.25f, -1.6f), new Vector3(1f, 1.7f, 0.3f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(-2.3f, 0.25f, 1.6f), new Vector3(1f, 1.7f, 0.3f), 0, (int)BuildingSlot.Wall);
        }

        if (twoStoreys)
        {
            b.Box(new Vector3(-0.5f, height + 1.2f, -0.4f), new Vector3(2.4f, 2.2f, 2.4f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(-0.5f, height + 2.4f, -0.4f), new Vector3(2.6f, 0.22f, 2.6f), 0, (int)BuildingSlot.Roof);
            Window(b, new Vector3(-0.5f, height + 1.3f, 0.8f));
        }

        Door(b, 1.7f, 0.6f);
        Window(b, new Vector3(-0.9f, 1.5f, 1.7f));
    }

    /// <summary>Plank houses on stilts: posts into the ground, a floor, a deep roof and a ladder; a veranda, a hipped roof or a room beside it.</summary>
    private static void Stilts(MeshBuilder b, bool detailed, int variant)
    {
        const float floor = 1.4f;
        var sides = detailed ? 6 : 4;
        foreach (var (x, z) in new[] { (1.4f, 1.2f), (1.4f, -1.2f), (-1.4f, 1.2f), (-1.4f, -1.2f) })
        {
            b.Tube(new Vector3(x, -Sink, z), new Vector3(x, floor, z), 0.13f, 0.11f, 1f, sides, 0, (int)BuildingSlot.Trim);
        }

        var deck = variant == 1 ? 4.2f : 3.2f;
        b.Box(new Vector3(0f, floor + 0.08f, (deck - 3.2f) / 2f), new Vector3(3.6f, 0.18f, deck), 0, (int)BuildingSlot.Trim);
        b.Box(new Vector3(0f, floor + 1.15f, 0f), new Vector3(3.2f, 2f, 2.8f), 0, (int)BuildingSlot.Wall);
        var eaves = new Vector3(0f, floor + 1.95f, 0f);
        if (variant == 2)
        {
            b.Hip(eaves, 4.3f, 4f, 2.2f, 1.4f, (int)BuildingSlot.Roof);
        }
        else
        {
            b.Gable(eaves, 4.3f, 4f, 2.1f, 0, (int)BuildingSlot.Roof, (int)BuildingSlot.Wall, walls: new Vector2(3.2f, 2.8f));
        }

        if (variant == 3)
        {
            b.Box(new Vector3(-2.3f, floor + 0.85f, -0.3f), new Vector3(1.5f, 1.4f, 1.9f), 0, (int)BuildingSlot.Wall);
            b.Box(new Vector3(-2.3f, floor + 0.08f, -0.3f), new Vector3(1.7f, 0.18f, 2.1f), 0, (int)BuildingSlot.Trim);
            b.Roof(new Vector3(-3.3f, floor + 1.4f, -1.5f), new Vector3(-3.3f, floor + 1.4f, 0.9f), new Vector3(-1.6f, floor + 2.1f, 0.9f), new Vector3(-1.6f, floor + 2.1f, -1.5f), (int)BuildingSlot.Roof);
            b.Tube(new Vector3(-3f, -Sink, -0.3f), new Vector3(-3f, floor, -0.3f), 0.11f, 0.1f, 1f, sides, 0, (int)BuildingSlot.Trim);
        }

        b.Box(new Vector3(0f, floor + 1.0f, 1.42f), new Vector3(0.85f, 1.6f, 0.1f), 0, (int)BuildingSlot.Door);
        var foot = 2.3f + (deck - 3.2f);
        var head = 1.65f + (deck - 3.2f);
        foreach (var x in new[] { -0.3f, 0.3f })
        {
            b.Tube(new Vector3(x, 0f, foot), new Vector3(x, floor + 0.1f, head), 0.05f, 0.05f, 1f, 4, 0, (int)BuildingSlot.Trim);
        }

        for (var rung = 1; rung <= 4; rung++)
        {
            var t = rung / 5f;
            b.Box(new Vector3(0f, t * (floor + 0.1f), foot - (t * 0.65f)), new Vector3(0.6f, 0.05f, 0.08f), 0, (int)BuildingSlot.Trim);
        }
    }

    private static void Door(MeshBuilder b, float front, float x) =>
        b.Box(new Vector3(x, 0.85f, front + 0.02f), new Vector3(0.9f, 1.7f, 0.12f), 0, (int)BuildingSlot.Door);

    private static void Window(MeshBuilder b, Vector3 at) =>
        b.Box(at + new Vector3(0f, 0f, 0.02f), new Vector3(0.55f, 0.5f, 0.1f), 0, (int)BuildingSlot.Window);
}
