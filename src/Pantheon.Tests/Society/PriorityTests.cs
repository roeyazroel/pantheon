using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Society;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>Setting a settlement's priority (spec §29, §73, ADR-023): who may, how often, and what AI gods ask.</summary>
public sealed class PriorityTests
{
    private static readonly SocietyRules Rules = SocietyWorlds.Rules;

    [Fact]
    public void AGodFollowedByEnough_SetsAPriority_ButNotTooOftenOrForAnother()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.5f, faith: 0.6f);
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f, godId: 2);
        var authority = new SocietyAuthority(Rules);
        var events = new List<WorldEvent>();
        world.Tick = 50;

        var set = authority.Apply(world, new PriorityCommand(1, village.Id, SettlementPriority.Temple), 1, events);
        var again = authority.Check(world, new PriorityCommand(1, village.Id, SettlementPriority.Temple), 1);
        world.Tick += 100;
        var soon = authority.Check(world, new PriorityCommand(1, village.Id, SettlementPriority.Food), 1);
        var few = authority.Check(world, new PriorityCommand(2, village.Id, SettlementPriority.Food), 2);
        var forged = authority.Check(world, new PriorityCommand(1, village.Id, SettlementPriority.Food), 2);
        world.Tick += 600;
        var later = authority.Check(world, new PriorityCommand(1, village.Id, SettlementPriority.Food), 1);

        Assert.Equal(PriorityRejection.None, set);
        Assert.Equal((SettlementPriority.Temple, (int?)1), (village.Society.Priority, village.Society.PriorityGodId));
        Assert.Equal(SettlementPriority.Temple, events.OfType<PriorityChanged>().Single().Priority);
        Assert.Equal(PriorityRejection.Unchanged, again);
        Assert.Equal(PriorityRejection.TooSoon, soon);
        Assert.Equal(PriorityRejection.TooFewFollowers, few);
        Assert.Equal(PriorityRejection.NotAuthorized, forged);
        Assert.Equal(PriorityRejection.None, later);
    }

    [Fact]
    public void AnAiGod_AnswersHungerFirst_ThenWantsATemple_AndLeavesVillagesItDoesNotLead()
    {
        var world = DivineWorlds.Create(0f, 300f, 800f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Guardian, 0f));
        var led = world.Settlements[0];
        var other = world.Settlements[1];
        DivineWorlds.Believe(led, adherence: 0.6f, faith: 0.6f, godId: 2);
        DivineWorlds.Believe(other, adherence: 0.3f, faith: 0.6f, godId: 2);
        DivineWorlds.Believe(other, adherence: 0.5f, faith: 0.6f);
        led.Food = 0f;
        var advisor = new PriorityAdvisor(Rules, SocietyWorlds.Civic, DivineWorlds.Rules, new SocietyAuthority(Rules));
        var rival = world.Gods.Single(g => g.Id == 2);
        var profile = TestContent.Shipped.GodAi.Profiles!.Single(p => p.Personality == GodPersonality.Guardian);

        var hungry = advisor.Choose(world, rival, profile, led);
        led.Food = 400f;
        var fed = advisor.Choose(world, rival, profile, led);
        advisor.Act(world, rival, profile, []);

        Assert.Equal(SettlementPriority.Food, hungry.Priority);
        Assert.Equal(SettlementPriority.Temple, fed.Priority);
        Assert.Equal(SettlementPriority.Temple, led.Society.Priority);
        Assert.Equal(SettlementPriority.None, other.Society.Priority);
    }
}
