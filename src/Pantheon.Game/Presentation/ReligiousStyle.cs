using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>What a god's worship looks like in a village (spec §61, ADR-042).</summary>
public enum StyleMotif
{
    /// <summary>A benevolent, living faith: gardens, bushes and flowers.</summary>
    Garden,

    /// <summary>A fearful, warlike faith: dark obelisks and braziers burning by the shrine.</summary>
    Monument,

    /// <summary>A faith of order and learning: a ring of white columns about the shrine.</summary>
    Lore,
}

/// <summary>A thing a village raises to its god's style.</summary>
public enum StylePieceKind
{
    /// <summary>A bed of flowers.</summary>
    FlowerBed,

    /// <summary>A round green bush.</summary>
    Bush,

    /// <summary>A dark obelisk.</summary>
    Obelisk,

    /// <summary>A burning brazier.</summary>
    Brazier,

    /// <summary>A white column.</summary>
    Column,
}

/// <summary>One piece of a village's religious style, placed.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Position">Its foot on the ground.</param>
/// <param name="Yaw">Its turn about the vertical axis.</param>
/// <param name="Scale">Its size, around 1.</param>
/// <param name="Variant">A value in 0..1 for its colour or shape.</param>
public readonly record struct StylePiece(StylePieceKind Kind, Vector3 Position, float Yaw, float Scale, float Variant);

/// <summary>A village's religious style: its motif, how strongly it shows and why.</summary>
/// <param name="Motif">The motif.</param>
/// <param name="Strength">How strongly it shows (0..1): its score times the god's share of the village.</param>
/// <param name="GodId">The god it is raised to.</param>
/// <param name="Reason">What gave it, for the debug view.</param>
public sealed record ReligiousStyle(StyleMotif Motif, float Strength, int GodId, string Reason);

/// <summary>
/// Religion's look on villages (spec §61, ADR-042), derived from the world and never saved. The god
/// most of a village follows gives it a motif by its domains and traits, the village's doctrines,
/// and whether its people love or fear it: a benevolent nature god's villages grow gardens, a
/// fearful war god's raise dark monuments, and a god of order and learning's stand in a ring of
/// columns. Pieces are placed from the seed and ids, so showing them changes nothing.
/// </summary>
public static class ReligiousStyles
{
    private static readonly GodDomain[][] Domains =
    [
        [GodDomain.Nature, GodDomain.Life, GodDomain.Prosperity, GodDomain.Ocean],
        [GodDomain.War, GodDomain.Death, GodDomain.Fire, GodDomain.Storm, GodDomain.Chaos],
        [GodDomain.Knowledge, GodDomain.Justice, GodDomain.Protection, GodDomain.Trickery],
    ];

    private static readonly GodTrait[][] Traits =
    [
        [GodTrait.Compassionate, GodTrait.Merciful, GodTrait.Generous],
        [GodTrait.Cruel, GodTrait.Vengeful, GodTrait.Ambitious, GodTrait.Chaotic],
        [GodTrait.Honest, GodTrait.Deceptive],
    ];

    private static readonly Doctrine[][] Doctrines =
    [
        [Doctrine.NatureWorship, Doctrine.Pacifism, Doctrine.Charity],
        [Doctrine.Militarism, Doctrine.Sacrifice, Doctrine.Expansionism],
        [Doctrine.KnowledgeSeeking, Doctrine.Isolationism],
    ];

    private const ulong PieceSalt = 61;

    /// <summary>The style a village shows, if its god's worship is strong enough to be seen.</summary>
    /// <param name="settlement">The village.</param>
    /// <param name="world">The world, for its god.</param>
    /// <param name="settings">Style settings.</param>
    /// <returns>The style, or <c>null</c> when no god holds most of the village or no motif is strong enough.</returns>
    public static ReligiousStyle? Of(SettlementState settlement, WorldState world, StyleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        var main = SettlementLabel.MainBelief(settlement);
        if (main is not { Adherence: >= 0.5f } belief || world.Gods.Find(g => g.Id == belief.GodId) is not { } god)
        {
            return null;
        }

        var best = (Motif: StyleMotif.Garden, Score: -1f, Reason: string.Empty);
        foreach (var motif in Enum.GetValues<StyleMotif>())
        {
            var index = (int)motif;
            var domains = 0;
            foreach (var domain in god.Domains)
            {
                domains += Array.IndexOf(Domains[index], domain) >= 0 ? 1 : 0;
            }

            var doctrine = 0f;
            foreach (var each in Doctrines[index])
            {
                doctrine = Math.Max(doctrine, settlement.Culture.Get(each));
            }

            var traits = god.Traits.Exists(t => Array.IndexOf(Traits[index], t) >= 0) ? 1f : 0f;
            var feeling = motif switch
            {
                StyleMotif.Garden => belief.Love,
                StyleMotif.Monument => belief.Fear,
                StyleMotif.Lore => belief.Faith * 0.5f,
            };
            var score = (settings.DomainWeight * domains / Math.Max(1, god.Domains.Count)) + (settings.DoctrineWeight * doctrine) + (settings.TraitWeight * traits) + (settings.FeelingWeight * feeling);
            if (score > best.Score)
            {
                best = (motif, score, string.Format(CultureInfo.InvariantCulture, "domains {0}, doctrine {1:0.00}, traits {2:0}, feeling {3:0.00}", domains, doctrine, traits, feeling));
            }
        }

        var strength = Math.Clamp(best.Score * belief.Adherence, 0f, 1f);
        return strength >= settings.Threshold ? new ReligiousStyle(best.Motif, strength, god.Id, best.Reason) : null;
    }

    /// <summary>Where a village's style pieces stand: by the shrine for monuments and columns, among the houses for gardens.</summary>
    /// <param name="style">The style.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="site">Its layout.</param>
    /// <param name="terrain">Terrain.</param>
    /// <param name="settings">Style settings.</param>
    /// <returns>The pieces; the same for the same inputs.</returns>
    public static IReadOnlyList<StylePiece> Layout(ReligiousStyle style, SettlementState settlement, SettlementSite site, TerrainField terrain, StyleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        var count = Math.Max(2, (int)MathF.Round(settings.MaxPieces * style.Strength * Math.Min(1f, (site.Houses.Count + 2) / 14f)));
        var (templeAt, _) = CivicLayout.Temple(site, terrain, settlement.Id);
        var templeAngle = MathF.Atan2(templeAt.Z - site.Center.Z, templeAt.X - site.Center.X);
        return style.Motif == StyleMotif.Garden
            ? Gardens(settlement.Id, site, terrain, settings, count, templeAt)
            : Ring(style.Motif, settlement.Id, site, terrain, settings, style.Motif == StyleMotif.Lore ? Math.Max(5, count) : count, templeAngle);
    }

    /// <summary>Obelisks and braziers by turns, or columns, spread evenly round the shrine on the arc the temple's side leaves free.</summary>
    private static List<StylePiece> Ring(StyleMotif motif, int settlementId, SettlementSite site, TerrainField terrain, StyleSettings settings, int count, float templeAngle)
    {
        const float templeGap = 0.45f;
        var pieces = new List<StylePiece>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = templeAngle + templeGap + ((MathF.Tau - (2f * templeGap)) * (i + 0.5f) / count);
            var x = site.Center.X + (MathF.Cos(angle) * settings.ShrineRingRadius);
            var z = site.Center.Z + (MathF.Sin(angle) * settings.ShrineRingRadius);
            if (Dry(terrain, x, z))
            {
                var kind = motif == StyleMotif.Lore ? StylePieceKind.Column : pieces.Count % 2 == 0 ? StylePieceKind.Obelisk : StylePieceKind.Brazier;
                var variant = PresentationHash.Unit(PieceSalt, (ulong)settlementId, (ulong)(i + 1));
                pieces.Add(new StylePiece(kind, new Vector3(x, terrain.SampleHeight(x, z), z), angle, 0.85f + (0.3f * variant), variant));
            }
        }

        return pieces;
    }

    /// <summary>Flower beds and bushes in the gaps among the houses, clear of houses, the temple and water.</summary>
    private static List<StylePiece> Gardens(int settlementId, SettlementSite site, TerrainField terrain, StyleSettings settings, int count, Vector3 temple)
    {
        var pieces = new List<StylePiece>(count);
        for (var slot = 0; pieces.Count < count && slot < count * 8; slot++)
        {
            var radius = settings.ShrineRingRadius + (settings.GardenSpacing * MathF.Sqrt(slot + 0.5f));
            var angle = (slot * 2.39996323f) + 1.2f;
            var x = site.Center.X + (MathF.Cos(angle) * radius);
            var z = site.Center.Z + (MathF.Sin(angle) * radius);
            if (!Dry(terrain, x, z) || TooNear(site.Houses, x, z, settings.HouseClearance) || Near(temple, x, z, settings.HouseClearance + 2f))
            {
                continue;
            }

            var variant = PresentationHash.Unit(PieceSalt, (ulong)settlementId, (ulong)(slot + 100));
            var kind = variant < 0.6f ? StylePieceKind.FlowerBed : StylePieceKind.Bush;
            pieces.Add(new StylePiece(kind, new Vector3(x, terrain.SampleHeight(x, z), z), variant * MathF.Tau, 0.8f + (0.4f * variant), variant));
        }

        return pieces;
    }

    /// <summary>Whether a point is dry land.</summary>
    private static bool Dry(TerrainField terrain, float x, float z) => terrain.SampleHeight(x, z) >= 0.4f && !terrain.IsUnderFreshWater(x, z);

    /// <summary>Whether a point lies closer than a distance to any house.</summary>
    private static bool TooNear(IReadOnlyList<HutPlacement> houses, float x, float z, float distance)
    {
        foreach (var house in houses)
        {
            if (Near(house.Position, x, z, distance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a point lies closer than a distance to another, on the ground.</summary>
    private static bool Near(Vector3 point, float x, float z, float distance) => ((point.X - x) * (point.X - x)) + ((point.Z - z) * (point.Z - z)) < distance * distance;
}
