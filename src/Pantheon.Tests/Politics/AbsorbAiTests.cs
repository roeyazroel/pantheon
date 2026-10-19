using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Politics;

/// <summary>AI gods absorbing their allies' believers near the endgame (spec §54, ADR-040).</summary>
public sealed class AbsorbAiTests
{
    [Fact]
    public void ATreacherousAlly_WhoLeadsWhereItsAllyIsFollowed_WouldRatherAbsorbThanMerelyBreak()
    {
        var (world, diplomat) = Allied();
        DivineWorlds.Believe(world.Settlements[3], adherence: 0.3f, faith: 0.5f, godId: 2);

        var moves = diplomat.Evaluate(world, world.Gods[3], PantheonWorlds.Profile(GodPersonality.Trickster)).Where(c => c.OtherGodId == 2).ToList();

        var absorb = moves.Single(c => c.Kind == DiplomaticCommandKind.Absorb);
        var breaking = moves.Single(c => c.Kind == DiplomaticCommandKind.BreakAlliance);
        Assert.True(absorb.Utility > breaking.Utility);
        Assert.Contains("would take", absorb.Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NoAbsorbing_WithoutAnAllysBelieversWhereTheGodLeads()
    {
        var (world, diplomat) = Allied();

        var moves = diplomat.Evaluate(world, world.Gods[3], PantheonWorlds.Profile(GodPersonality.Trickster));

        Assert.DoesNotContain(moves, c => c.Kind == DiplomaticCommandKind.Absorb);
        Assert.Contains(moves, c => c.Kind == DiplomaticCommandKind.BreakAlliance && c.OtherGodId == 2);
    }

    [Fact]
    public void ATrickster_SickOfItsAlly_AbsorbsIt_TakingItsBelievers()
    {
        var (world, diplomat) = Allied();
        var shared = world.Settlements[3];
        var theirs = DivineWorlds.Believe(shared, adherence: 0.15f, faith: 0.5f, godId: 2);
        var view = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, 4, 2);
        (view.Trust, view.Rivalry) = (0.1f, 0.8f);
        var events = new List<WorldEvent>();

        diplomat.Act(world, world.Gods[3], PantheonWorlds.Profile(GodPersonality.Trickster), events);

        var absorbed = Assert.Single(events.OfType<AllyAbsorbed>());
        Assert.Equal((4, 2), (absorbed.GodId, absorbed.AllyGodId));
        Assert.True(theirs.Adherence < 0.15f);
        Assert.NotEqual(DiplomaticStatus.Alliance, DiplomacyModel.StatusOf(world, 4, 2));
    }

    /// <summary>The pantheon with the Tyrant (2) and the Trickster (4) long allied, and a diplomat over the pinned rules.</summary>
    private static (Pantheon.Domain.World.WorldState World, GodDiplomat Diplomat) Allied()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.8f);
        PantheonWorlds.SetStatus(world, 2, 4, DiplomaticStatus.Alliance);
        world.Tick = 20_000;
        return (world, PantheonWorlds.Diplomat());
    }
}
