using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Domains;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>What keeps the AI gods affordable in a large world (ADR-027): the influence cache and their turns.</summary>
public sealed class AiCostTests
{
    [Fact]
    public void TheInfluenceCache_BuildsEachGodsFieldOnce_AsInfluenceFieldDoes()
    {
        var world = PantheonWorlds.Create(0.6f, 0.4f, 0.5f, 0.3f);
        var cache = new InfluenceCache(DivineWorlds.Rules, world);

        Assert.Same(world, cache.World);
        foreach (var god in world.Gods)
        {
            var field = cache.For(god.Id);
            var fresh = InfluenceField.Build(DivineWorlds.Rules, world, god.Id);

            Assert.Same(field, cache.For(god.Id));
            Assert.Equal(world.Settlements.Select(s => fresh.At(s.Position)), world.Settlements.Select(s => field.At(s.Position)));
        }
    }

    [Fact]
    public void ACastChecked_WithTheCache_IsCheckedAsWithout()
    {
        var world = PantheonWorlds.Create(0.6f, 0.4f, 0.5f, 0.3f);
        var caster = DomainTests.Caster();
        var cache = new InfluenceCache(DivineWorlds.Rules, world);

        foreach (var god in world.Gods)
        {
            foreach (var miracle in DivineWorlds.Miracles.Miracles)
            {
                foreach (var settlement in world.Settlements)
                {
                    var request = new CastMiracleRequest(god.Id, miracle.Id, settlement.Position);

                    Assert.Equal(caster.Check(world, request, god.Id), caster.Check(world, request, god.Id, cache));
                }
            }
        }
    }

    [Fact]
    public void AiGods_TakeTurns_EachOnceAnInterval_TheFirstWhenItIsDue()
    {
        const int interval = 30;
        const int thinkers = 3;
        var turns = Enumerable.Range(0, thinkers)
            .Select(turn => Enumerable.Range(0, 3 * interval).Where(tick => GodAiSystem.IsTurn(tick, turn, thinkers, interval)).ToList())
            .ToList();

        Assert.Equal([29, 59, 89], turns[0]);
        Assert.All(turns, ticks => Assert.Equal(3, ticks.Count));
        Assert.Equal(9, turns.SelectMany(t => t).Distinct().Count());
        Assert.True(GodAiSystem.IsTurn(29, 0, 1, interval));
    }
}
