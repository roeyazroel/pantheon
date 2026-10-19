using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Hand;

namespace Pantheon.Game.Presentation;

/// <summary>The shape of a piece of debris.</summary>
public enum DebrisShape
{
    /// <summary>A wall plank.</summary>
    Plank,

    /// <summary>A stone.</summary>
    Stone,

    /// <summary>A shard of thatch from a roof.</summary>
    Shard,
}

/// <summary>What threw debris up.</summary>
public enum DebrisCause
{
    /// <summary>A house fell.</summary>
    House,

    /// <summary>A thrown thing or a falling star struck the ground.</summary>
    Strike,
}

/// <summary>One piece of debris as it sets off.</summary>
/// <param name="Shape">Its shape.</param>
/// <param name="Size">Its box, in metres.</param>
/// <param name="Offset">Where it starts, from the burst's point.</param>
/// <param name="Velocity">How it flies off, in metres per second.</param>
/// <param name="Spin">How it turns, in radians per second.</param>
public readonly record struct DebrisPiece(DebrisShape Shape, Vector3 Size, Vector3 Offset, Vector3 Velocity, Vector3 Spin);

/// <summary>
/// Where the pieces of a burst of debris start and how they fly (spec §94, §55, ADR-048): the
/// simulation decides what fell, and this only dresses it. Varied by <see cref="PresentationHash"/>
/// of the burst's key, so the same fall throws the same debris. Pure.
/// </summary>
public static class DebrisLayout
{
    private const ulong Salt = 94;

    /// <summary>The pieces of a burst.</summary>
    /// <param name="settings">Debris settings.</param>
    /// <param name="cause">A fallen house or a strike.</param>
    /// <param name="key">What makes this burst its own: the house's place, or the strike's.</param>
    /// <param name="scale">The house's scale, or 1.</param>
    /// <returns>The pieces.</returns>
    public static IReadOnlyList<DebrisPiece> Burst(DebrisSettings settings, DebrisCause cause, ulong key, float scale)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var pieces = new List<DebrisPiece>();
        var i = 0UL;
        void Add(DebrisShape shape, int count, float reach, float height, Vector3 size)
        {
            for (var n = 0; n < count; n++, i++)
            {
                float U(ulong k) => PresentationHash.Unit(Salt, key, i, k);
                var angle = U(0) * MathF.Tau;
                var (dx, dz) = (MathF.Cos(angle), MathF.Sin(angle));
                var radius = reach * (0.3f + (0.7f * U(1)));
                var outward = settings.Impulse * (0.4f + (0.6f * U(2)));
                pieces.Add(new DebrisPiece(
                    shape,
                    size * scale * (0.75f + (0.5f * U(3))),
                    new Vector3(dx * radius, height * (0.4f + (0.6f * U(4))), dz * radius),
                    new Vector3(dx * outward, settings.Lift * (0.5f + (0.5f * U(5))), dz * outward),
                    new Vector3(U(6) - 0.5f, U(7) - 0.5f, U(8) - 0.5f) * (2f * settings.Spin)));
            }
        }

        if (cause == DebrisCause.House)
        {
            Add(DebrisShape.Plank, settings.HousePlanks, 2f * scale, 2f * scale, new Vector3(1.6f, 0.12f, 0.3f));
            Add(DebrisShape.Stone, settings.HouseStones, 2f * scale, 0.6f * scale, new Vector3(0.45f, 0.4f, 0.45f));
            Add(DebrisShape.Shard, settings.HouseShards, 1.6f * scale, 3f * scale, new Vector3(1.3f, 0.1f, 0.9f));
        }
        else
        {
            Add(DebrisShape.Stone, settings.StrikeStones, 0.8f, 0.4f, new Vector3(0.4f, 0.35f, 0.4f));
        }

        return pieces;
    }

    /// <summary>
    /// The thrown thing itself, landing where it struck (spec §55): a boulder or a trunk coming in on
    /// the line of the throw and down, to tumble to rest. None for villagers, the Creature or an
    /// offering, which are not left lying.
    /// </summary>
    /// <param name="settings">Debris settings, for the speeds.</param>
    /// <param name="kind">What was thrown.</param>
    /// <param name="away">The throw's direction on the ground: from the camera to the strike.</param>
    /// <returns>The piece, if any.</returns>
    public static DebrisPiece? Thrown(DebrisSettings settings, HeldKind kind, Vector2 away)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Vector3? size = kind switch
        {
            HeldKind.Rock => new Vector3(1.3f, 1.1f, 1.2f),
            HeldKind.Tree => new Vector3(0.5f, 0.5f, 5f),
            HeldKind.Villagers => null,
            HeldKind.Creature => null,
            HeldKind.Offering => null,
        };
        if (size is not { } box)
        {
            return null;
        }

        var line = away.LengthSquared() > 0f ? Vector2.Normalize(away) : Vector2.UnitX;
        var (forward, roll) = (new Vector3(line.X, 0f, line.Y), new Vector3(line.Y, 0f, -line.X));
        return new DebrisPiece(
            kind == HeldKind.Rock ? DebrisShape.Stone : DebrisShape.Plank,
            box,
            (forward * -2f) + new Vector3(0f, 2.5f, 0f),
            (forward * (settings.Impulse * 1.6f)) - new Vector3(0f, settings.Lift, 0f),
            roll * settings.Spin);
    }

    /// <summary>
    /// The houses that fell between two layouts of a village: as many as its ruins grew by, taken from
    /// the houses that stood before and stand no longer. None when no ruins were added.
    /// </summary>
    /// <param name="before">The layout before.</param>
    /// <param name="after">The layout now.</param>
    /// <returns>The fallen houses.</returns>
    public static IReadOnlyList<HutPlacement> Fallen(SettlementSite before, SettlementSite after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var fell = after.Ruins.Count - before.Ruins.Count;
        if (fell <= 0)
        {
            return [];
        }

        var standing = new HashSet<Vector3>(after.Houses.Select(h => h.Position));
        return before.Houses.Where(h => !standing.Contains(h.Position)).Reverse().Take(fell).ToList();
    }

    /// <summary>A burst's key from where it is, so a burst at the same place is the same.</summary>
    /// <param name="x">East-west, in metres.</param>
    /// <param name="z">North-south, in metres.</param>
    /// <returns>The key.</returns>
    public static ulong KeyOf(float x, float z) => ((ulong)unchecked((uint)(int)MathF.Round(x * 10f)) << 32) | unchecked((uint)(int)MathF.Round(z * 10f));
}
