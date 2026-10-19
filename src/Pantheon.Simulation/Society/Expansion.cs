using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Society;

/// <summary>
/// Settlers founding new villages (spec §29's "expand territory", §6's 20–50 settlements, ADR-023):
/// a settlement big enough, under an Expansion priority, sends some of its people with food to the
/// best free land in reach. The new village shares its founders' beliefs and some of their knowledge.
/// </summary>
internal sealed class Expansion
{
    // Lowest ground and fertility a new village is founded on.
    private const float MinimumElevation = 1.5f;
    private const float MinimumFertility = 0.2f;

    // Share of their home's technology the settlers take with them.
    private const float CarriedKnowledge = 0.8f;

    private readonly SocietyRules _rules;

    /// <summary>Creates the founding over validated rules.</summary>
    public Expansion(SocietyRules rules) => _rules = rules;

    /// <summary>Founds a village from a settlement when its priority, size (times <paramref name="sooner"/>: expansionists go sooner) and time allow and there is land for it.</summary>
    public void TryFound(WorldState world, SettlementState home, float sooner, List<WorldEvent> events)
    {
        var expansion = _rules.Expansion;
        var society = home.Society;
        var cooldown = (long)MathF.Round(expansion.CooldownSeconds * SimulationTime.TicksPerSecond);
        if (society.Priority != SettlementPriority.Expansion || home.Population < expansion.MinPopulation * sooner
            || (society.LastFoundingTick > 0 && world.Tick - society.LastFoundingTick < cooldown) || Site(world, home) is not { } site)
        {
            return;
        }

        var settlers = Math.Min(expansion.Settlers, home.Population - 1);
        var food = Math.Min(home.Food, settlers * expansion.FoodPerSettler);
        home.Population -= settlers;
        home.Food -= food;
        society.LastFoundingTick = Math.Max(1, world.Tick);
        var village = new SettlementState
        {
            Id = NextId(world),
            Name = NameFor(world, home),
            RegionId = site,
            Position = world.Regions.GetRegionCenter(site),
            Population = settlers,
            Food = food,
            Water = settlers,
            Housing = settlers,
            Health = home.Health,
            ConstructionProgress = 0f,
            Ruins = 0,
            ShieldedUntilTick = 0,
            Emigration = 0f,
            OldFaith = null,
            SanctuaryGodId = null,
            SanctuaryUntilTick = 0,
            Society = Founded(home.Society),
            Culture = Inherited(home.Culture),
            Beliefs = home.Beliefs.ConvertAll(b => new GodBelief { GodId = b.GodId, Adherence = b.Adherence, Faith = b.Faith, Love = b.Love, Fear = b.Fear, Doubt = b.Doubt, Dependency = b.Dependency, Resentment = b.Resentment }),
        };
        world.Settlements.Add(village);
        events.Add(new SettlementFounded { Tick = world.Tick, SettlementId = village.Id, FromSettlementId = home.Id, Settlers = settlers });
    }

    /// <summary>The best free land within range: fertile, dry, above the sea and far enough from every settlement; nearer is better.</summary>
    private int? Site(WorldState world, SettlementState home)
    {
        var expansion = _rules.Expansion;
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(home.RegionId);
        int? best = null;
        var bestScore = float.MinValue;
        for (var r = Math.Max(0, row - expansion.Range); r <= Math.Min(grid.Rows - 1, row + expansion.Range); r++)
        {
            for (var c = Math.Max(0, column - expansion.Range); c <= Math.Min(grid.Columns - 1, column + expansion.Range); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (region.Biome == Biome.Sea || region.Fertility < MinimumFertility || region.Elevation < MinimumElevation || region.FreshWater > 0.9f || !Free(world, c, r))
                {
                    continue;
                }

                var distance = Math.Max(Math.Abs(c - column), Math.Abs(r - row));
                var score = region.Fertility + (0.3f * region.FreshWater) - (0.02f * distance);
                if (score > bestScore)
                {
                    (best, bestScore) = (region.Id, score);
                }
            }
        }

        return best;
    }

    /// <summary>Whether a region lies at least the spacing from every settlement, fallen ones included.</summary>
    private bool Free(WorldState world, int column, int row)
    {
        foreach (var settlement in world.Settlements)
        {
            var (c, r) = world.Regions.GetCoordinates(settlement.RegionId);
            if (Math.Max(Math.Abs(c - column), Math.Abs(r - row)) < _rules.Expansion.Spacing)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The next settlement id: one above the highest.</summary>
    private static int NextId(WorldState world)
    {
        var highest = 0;
        foreach (var settlement in world.Settlements)
        {
            highest = Math.Max(highest, settlement.Id);
        }

        return highest + 1;
    }

    /// <summary>The first name of the world's pool not yet taken, or one after the founders' home.</summary>
    private static string NameFor(WorldState world, SettlementState home)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var settlement in world.Settlements)
        {
            taken.Add(settlement.Name);
        }

        foreach (var name in world.Generation.StartingSettlement.Names)
        {
            if (!taken.Contains(name))
            {
                return name;
            }
        }

        for (var n = 2; ; n++)
        {
            var name = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"New {home.Name} {n}");
            if (!taken.Contains(name))
            {
                return name;
            }
        }
    }

    /// <summary>A new village's culture: its founders' doctrines and outlook (ADR-024).</summary>
    private static Domain.Culture.CultureState Inherited(Domain.Culture.CultureState home)
    {
        var culture = Domain.Culture.CultureState.Founded();
        foreach (var doctrine in Enum.GetValues<Domain.Culture.Doctrine>())
        {
            culture.Set(doctrine, home.Get(doctrine));
        }

        culture.Secularism = home.Secularism;
        return culture;
    }

    /// <summary>A new village's society: founded fresh, with most of its founders' knowledge.</summary>
    private static SocietyState Founded(SocietyState home)
    {
        var society = SocietyState.Founded();
        foreach (var field in Enum.GetValues<TechnologyField>())
        {
            society.Technology.Set(field, home.Technology.Get(field) * CarriedKnowledge);
        }

        society.Education = home.Education * CarriedKnowledge;
        return society;
    }
}
