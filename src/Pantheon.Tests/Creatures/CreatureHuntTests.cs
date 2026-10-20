using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>Fights a god commands (ADR-062): its Creature hunts the rival nearest where it points and falls on it.</summary>
public sealed class CreatureHuntTests
{
    private static readonly CreatureRules Rules = CreatureBondWorlds.Rules;

    [Fact]
    public void AGod_SetsItsCreature_OnTheRivalNearestWhereItPoints()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 200f);
        var authority = new CreatureAuthority(Rules, Brain());

        var result = authority.Execute(world, Fight(mine, theirs.Position.X + 30f, theirs.Position.Z), mine.GodId, []);

        Assert.True(result.IsAccepted);
        Assert.Equal((CreatureAction.Fight, CreatureSituation.Commanded, CreaturePhase.Travelling, (int?)theirs.Id), (mine.Activity.Action, mine.Activity.Situation, mine.Activity.Phase, mine.Activity.TargetCreatureId));
        Assert.True(CreatureBrain.IsHunting(mine, theirs.Id));
        Assert.Equal(CreatureCommandRejection.NoRivalThere, authority.Execute(world, Fight(mine, 20f, 20f), mine.GodId, []).Rejection);
    }

    [Fact]
    public void AHunter_FallsOnItsRival_EvenRightAfterTheyMet_AndItsGodIsRememberedForIt()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 150f);
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsCreature, CreatureAction.Ignore);
        CreatureBonds.Ensure(Rules.Bonds, mine, theirs.Id).LastMetTick = 0;
        CreatureBonds.Ensure(Rules.Bonds, theirs, mine.Id).LastMetTick = 0;
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        var system = CreatureBondWorlds.System(ledger: ledger);
        new CreatureAuthority(Rules, Brain()).Execute(world, Fight(mine, theirs.Position.X, theirs.Position.Z), mine.GodId, []);

        var events = CreatureBondWorlds.Run(world, system, 300);

        var met = events.OfType<CreaturesMet>().First();
        Assert.Equal((MeetingOutcome.Fought, mine.Id), (met.Outcome, met.CreatureId));
        Assert.False(met.GodsAtWar);
        var grudge = DiplomacyModel.FindRelation(world, theirs.GodId, mine.GodId)!;
        Assert.True(grudge.Rivalry > 0f, "the attacked god remembers who set the Creature on its own");
    }

    [Fact]
    public void AHunter_ChasesARivalThatWalksAway()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 160f);
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsCreature, CreatureAction.Ignore);
        var brain = Brain();
        new CreatureAuthority(Rules, brain).Execute(world, Fight(mine, theirs.Position.X, theirs.Position.Z), mine.GodId, []);
        brain.Command(world, theirs, new GroundPosition(theirs.Position.X + 60f, theirs.Position.Z + 40f));

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 400);

        Assert.Contains(events.OfType<CreaturesMet>(), m => m.Outcome == MeetingOutcome.Fought);
    }

    [Fact]
    public void ATreaty_ForbidsTheFight_AndABanishedRival_EndsTheHunt()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 300f);
        var authority = new CreatureAuthority(Rules, Brain());
        world.Diplomacy.Treaties.Add(new Treaty { Id = world.Diplomacy.NextTreatyId++, Kind = TreatyKind.CreatureNonAggression, FirstGodId = 1, SecondGodId = 2, TargetGodId = null, PayerGodId = null, Amount = 0f, Secret = false, SignedTick = 0, EndsTick = null });

        Assert.Equal(CreatureCommandRejection.Sworn, authority.Execute(world, Fight(mine, theirs.Position.X, theirs.Position.Z), mine.GodId, []).Rejection);

        world.Diplomacy.Treaties.Clear();
        Assert.True(authority.Execute(world, Fight(mine, theirs.Position.X, theirs.Position.Z), mine.GodId, []).IsAccepted);
        theirs.Activity.Phase = CreaturePhase.Banished;
        theirs.Activity.TicksLeft = 5000;
        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 5);

        Assert.Contains(events.OfType<CreatureGaveUp>(), g => g.CreatureId == mine.Id && g.Reason == GiveUpReason.OtherGone);
        Assert.False(CreatureBrain.IsHunting(mine, theirs.Id));
    }

    private static CreatureBrain Brain() => new(Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths, DiplomacyWorlds.Rules);

    private static CreatureCommand Fight(CreatureState creature, float x, float z) => new(creature.GodId, creature.Id, CreatureCommandKind.Fight, new GroundPosition(x, z));
}
