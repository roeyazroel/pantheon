using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Population;

/// <summary>
/// Derives a settlement's representative citizens (Tier 2) from its aggregates and layout. It is a
/// pure function of the world seed, the settlement, its labour split and its site: the same
/// inputs give the same roster, and nothing is written back (ADR-008).
/// </summary>
/// <remarks>
/// Identities are stable as the settlement changes. Representative <c>i</c> keeps its hash rank:
/// the lowest ranks are builders, the next farmers and the rest villagers, so moving one person
/// between neighbouring roles changes one representative's role. Homes and fields are chosen by
/// rendezvous hashing (each representative takes the house or field with its highest hash), so a
/// new house or field draws a share of the representatives and leaves everyone else in place.
/// </remarks>
public static class RepresentativeRoster
{
    // Hash salts, so ranks, homes, fields and spots draw unrelated values.
    private const ulong RankSalt = 11;
    private const ulong HomeSalt = 12;
    private const ulong FieldSalt = 13;
    private const ulong SpotSalt = 14;

    // Fourth hash key of the per-citizen spot values.
    private const ulong DoorOutKey = 0;
    private const ulong DoorAcrossKey = 1;
    private const ulong WorkAKey = 2;
    private const ulong WorkBKey = 3;
    private const ulong WorkFacingKey = 4;
    private const ulong GatherAngleKey = 5;
    private const ulong GatherRadiusKey = 6;
    private const ulong PhaseKey = 7;

    /// <summary>Number of representatives: one per inhabitant, up to the cap.</summary>
    /// <param name="population">Inhabitants.</param>
    /// <param name="maxRepresentatives">Most representatives drawn.</param>
    /// <returns><c>min(population, maxRepresentatives)</c>, never negative.</returns>
    public static int ComputeCount(int population, int maxRepresentatives) => Math.Clamp(population, 0, Math.Max(0, maxRepresentatives));

    /// <summary>
    /// Splits <paramref name="count"/> representatives into roles in proportion to the labour split.
    /// The two role boundaries (builders, then builders and farmers) are each rounded, so every
    /// count lies within one of its exact share and the counts add up to <paramref name="count"/>.
    /// </summary>
    /// <param name="count">Representatives to split.</param>
    /// <param name="population">Inhabitants they stand for.</param>
    /// <param name="labour">The settlement's labour split.</param>
    /// <returns>Role counts; all zero without people.</returns>
    public static RoleCounts SplitRoles(int count, int population, LabourAllocation labour)
    {
        if (count <= 0 || population <= 0)
        {
            return default;
        }

        var perPerson = (double)count / population;
        var builders = RoundBoundary(labour.Builders * perPerson, count);
        var workers = Math.Max(builders, RoundBoundary(labour.Workers * perPerson, count));
        return new RoleCounts(builders, workers - builders, count - workers);
    }

    /// <summary>Builds the roster.</summary>
    /// <param name="seed">World seed; with the settlement id it keys every hash.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="labour">Its labour split (<see cref="SettlementEconomy.AllocateLabour"/>).</param>
    /// <param name="site">Its layout (<see cref="SettlementLayout.ComputeSite"/>).</param>
    /// <param name="settings">Settlement presentation settings.</param>
    /// <returns>Representatives ordered by index.</returns>
    public static IReadOnlyList<RepresentativeCitizen> Build(WorldSeed seed, SettlementState settlement, LabourAllocation labour, SettlementSite site, SettlementViewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(settings);

        var count = ComputeCount(settlement.Population, settings.Citizens.MaxRepresentatives);
        var roles = SplitRoles(count, settlement.Population, labour);
        var key = PresentationHash.Mix(seed.Value, (ulong)settlement.Id);
        var byRank = RankOrder(key, count);
        var citizens = new RepresentativeCitizen[count];
        for (var rank = 0; rank < count; rank++)
        {
            var index = byRank[rank];
            citizens[index] = CreateCitizen(key, index, RoleAtRank(rank, roles), site, settings);
        }

        return citizens;
    }

    /// <summary>Role of the representative at a hash rank: builders first, then farmers, then villagers.</summary>
    private static CitizenRole RoleAtRank(int rank, RoleCounts roles) =>
        rank < roles.Builders ? CitizenRole.Builder
        : rank < roles.Builders + roles.Farmers ? CitizenRole.Farmer
        : CitizenRole.Villager;

    /// <summary>Indices 0 .. count − 1 sorted by their rank hash (ties, which are vanishingly rare, by index).</summary>
    private static int[] RankOrder(ulong key, int count)
    {
        var indices = new int[count];
        var hashes = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
            hashes[i] = PresentationHash.Mix(key, RankSalt, (ulong)i);
        }

        Array.Sort(hashes, indices);
        return indices;
    }

    /// <summary>A representative's home, work and gathering spots for its role.</summary>
    private static RepresentativeCitizen CreateCitizen(ulong key, int index, CitizenRole role, SettlementSite site, SettlementViewSettings settings)
    {
        var citizens = settings.Citizens;
        var center = new Vector2(site.Center.X, site.Center.Z);
        var house = PickHouse(key, index, site.Houses.Count);
        var home = house >= 0
            ? HomeSpot(key, index, site, house, settings)
            : center + GroundMath.Polar(Unit(key, index, GatherAngleKey) * MathF.Tau, citizens.GatherRadiusMax + citizens.DoorSpread);
        var (work, workYaw) = WorkSpot(key, index, role, site, home, settings);
        var gatherAngle = Unit(key, index, GatherAngleKey) * MathF.Tau;
        var gatherRadius = citizens.GatherRadiusMin + ((citizens.GatherRadiusMax - citizens.GatherRadiusMin) * MathF.Sqrt(Unit(key, index, GatherRadiusKey)));
        var gathering = center + GroundMath.Polar(gatherAngle, gatherRadius);

        return new RepresentativeCitizen(
            index,
            role,
            house,
            home,
            work,
            workYaw,
            gathering,
            GroundMath.YawTowards(gathering, center),
            Unit(key, index, PhaseKey));
    }

    /// <summary>The house with the representative's highest rendezvous hash, or −1 without houses.</summary>
    private static int PickHouse(ulong key, int index, int houses)
    {
        var best = -1;
        var bestHash = 0UL;
        for (var house = 0; house < houses; house++)
        {
            var hash = PresentationHash.Mix(key, HomeSalt, (ulong)index, (ulong)house);
            if (best < 0 || hash > bestHash)
            {
                best = house;
                bestHash = hash;
            }
        }

        return best;
    }

    /// <summary>A spot just outside the door: up to <see cref="CitizenSettings.DoorSpread"/> out from it and to either side.</summary>
    private static Vector2 HomeSpot(ulong key, int index, SettlementSite site, int house, SettlementViewSettings settings)
    {
        var spread = settings.Citizens.DoorSpread;
        var facing = SettlementSite.Facing(site.Houses[house].Yaw);
        var across = new Vector2(facing.Y, -facing.X);
        return site.DoorOf(house, settings.Layout.DoorDistance)
            + (facing * (Unit(key, index, DoorOutKey) * spread))
            + (across * ((Unit(key, index, DoorAcrossKey) - 0.5f) * 2f * spread));
    }

    /// <summary>
    /// Where the role works and which way it faces there. Builders without a building site and
    /// farmers without fields do chores near home, as villagers do.
    /// </summary>
    private static (Vector2 Spot, float Yaw) WorkSpot(ulong key, int index, CitizenRole role, SettlementSite site, Vector2 home, SettlementViewSettings settings)
    {
        var a = Unit(key, index, WorkAKey);
        var b = Unit(key, index, WorkBKey);
        var facing = Unit(key, index, WorkFacingKey);
        var chores = (home + GroundMath.Polar(a * MathF.Tau, settings.Citizens.WanderRadius * MathF.Sqrt(b)), facing * MathF.Tau);
        return role switch
        {
            CitizenRole.Builder => site.Construction is { } construction ? BuilderSpot(construction, a, settings.Citizens) : chores,
            CitizenRole.Farmer => site.Fields.Count > 0 ? FieldSpot(PickField(key, index, site.Fields), a, b, facing, settings) : chores,
            CitizenRole.Villager => chores,
        };
    }

    /// <summary>A spot on the ring around the house under construction, facing it.</summary>
    private static (Vector2 Spot, float Yaw) BuilderSpot(ConstructionPlacement construction, float angle, CitizenSettings settings)
    {
        var house = new Vector2(construction.House.Position.X, construction.House.Position.Z);
        var spot = house + GroundMath.Polar(angle * MathF.Tau, settings.BuilderRingRadius * construction.House.Scale);
        return (spot, GroundMath.YawTowards(spot, house));
    }

    /// <summary>The field with the representative's highest rendezvous hash; fields are keyed by their spiral slot, so they keep their farmers as the village grows.</summary>
    private static FieldPlacement PickField(ulong key, int index, IReadOnlyList<FieldPlacement> fields)
    {
        var best = fields[0];
        var bestHash = PresentationHash.Mix(key, FieldSalt, (ulong)index, (ulong)best.Slot);
        for (var i = 1; i < fields.Count; i++)
        {
            var hash = PresentationHash.Mix(key, FieldSalt, (ulong)index, (ulong)fields[i].Slot);
            if (hash > bestHash)
            {
                best = fields[i];
                bestHash = hash;
            }
        }

        return best;
    }

    /// <summary>A spot inside the field, facing along its rows one way or the other.</summary>
    private static (Vector2 Spot, float Yaw) FieldSpot(FieldPlacement field, float along, float across, float facing, SettlementViewSettings settings)
    {
        var extent = settings.Citizens.WorkSpread * settings.Layout.FieldSize;
        var forward = SettlementSite.Facing(field.Yaw);
        var right = new Vector2(forward.Y, -forward.X);
        var spot = new Vector2(field.Position.X, field.Position.Z)
            + (forward * ((along - 0.5f) * extent))
            + (right * ((across - 0.5f) * extent));
        return (spot, facing < 0.5f ? field.Yaw : field.Yaw + MathF.PI);
    }

    /// <summary>Rounds a role boundary and keeps it within the roster.</summary>
    private static int RoundBoundary(double value, int count) => Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, count);

    /// <summary>One of a representative's spot values, in [0, 1).</summary>
    private static float Unit(ulong key, int index, ulong value) => PresentationHash.Unit(key, SpotSalt, (ulong)index, value);
}
