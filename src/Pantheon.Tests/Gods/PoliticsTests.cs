using System;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>
/// Basic politics among four gods (spec §124, ADR-015): coalitions against a leader, the balance of
/// power, fresh peace, betrayal near a joint victory, and the new personalities.
/// </summary>
public sealed class PoliticsTests
{
    private const int Player = 1;
    private const int Tyrant = 2;
    private const int Guardian = 3;

    [Fact]
    public void AThirdGodsLead_DrawsTwoGodsTogether_AndAwayFromWar()
    {
        var diplomat = PantheonWorlds.Diplomat();
        var led = PantheonWorlds.Create(0.25f, 0.25f, 0.5f);
        var even = PantheonWorlds.Create(0.25f, 0.25f, 0.1f);

        var allyLed = PantheonWorlds.Move(diplomat, led, Tyrant, GodPersonality.Trickster, DiplomaticCommandKind.OfferAlliance, Player);
        var allyEven = PantheonWorlds.Move(diplomat, even, Tyrant, GodPersonality.Trickster, DiplomaticCommandKind.OfferAlliance, Player);
        var warLed = PantheonWorlds.Move(diplomat, led, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Player);
        var warEven = PantheonWorlds.Move(diplomat, even, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Player);

        Assert.True(allyLed.Utility > allyEven.Utility + 0.3f, $"{allyLed.Utility} vs {allyEven.Utility}");
        Assert.True(warLed.Utility < warEven.Utility - 0.4f, $"{warLed.Utility} vs {warEven.Utility}");
        Assert.Contains("against Tertia", allyLed.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("against", allyEven.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLeader_DrawsWar()
    {
        var diplomat = PantheonWorlds.Diplomat();
        var world = PantheonWorlds.Create(0.25f, 0.25f, 0.5f);

        var onLeader = PantheonWorlds.Move(diplomat, world, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Guardian);
        var onEqual = PantheonWorlds.Move(diplomat, world, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Player);

        Assert.Contains("they lead", onLeader.Reason, StringComparison.Ordinal);
        Assert.True(onLeader.Utility > onEqual.Utility);
    }

    [Fact]
    public void AnAllyOfEither_IsNoCommonThreat()
    {
        var diplomat = PantheonWorlds.Diplomat();
        var world = PantheonWorlds.Create(0.25f, 0.25f, 0.5f);
        PantheonWorlds.SetStatus(world, Player, Guardian, DiplomaticStatus.Alliance);

        var offer = PantheonWorlds.Move(diplomat, world, Tyrant, GodPersonality.Trickster, DiplomaticCommandKind.OfferAlliance, Player);

        Assert.DoesNotContain("against", offer.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AFreshPeace_HoldsWarBack_UntilItsMemoryFades()
    {
        var diplomat = PantheonWorlds.Diplomat();
        var fresh = PantheonWorlds.Create(0.3f, 0.3f);
        var old = PantheonWorlds.Create(0.3f, 0.3f);
        foreach (var world in new[] { fresh, old })
        {
            PantheonWorlds.SetStatus(world, Tyrant, Player, DiplomaticStatus.Hostile);
        }

        DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, fresh, Tyrant, Player).Memories.Add(new DiplomaticMemory { Tick = 0, Act = DiplomaticAct.MadePeace, SettlementId = null, Weight = 0.5f });

        var afterPeace = PantheonWorlds.Move(diplomat, fresh, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Player);
        var longAgo = PantheonWorlds.Move(diplomat, old, Tyrant, GodPersonality.Tyrant, DiplomaticCommandKind.DeclareWar, Player);

        Assert.Equal(longAgo.Utility - 0.5f, afterPeace.Utility, 3);
    }

    [Fact]
    public void NearAJointVictory_TheTreacherousBreakAway_AndTheLoyalStay()
    {
        var diplomat = PantheonWorlds.Diplomat();
        var near = PantheonWorlds.Create(0.1f, 0.4f, 0.5f);
        var far = PantheonWorlds.Create(0.05f, 0.2f, 0.75f);
        foreach (var world in new[] { near, far })
        {
            PantheonWorlds.SetStatus(world, Tyrant, Player, DiplomaticStatus.Alliance);
        }

        float Betray(GodPersonality personality, Pantheon.Domain.World.WorldState world) =>
            PantheonWorlds.Move(diplomat, world, Tyrant, personality, DiplomaticCommandKind.BreakAlliance, Player).Utility;

        Assert.True(Betray(GodPersonality.Trickster, near) > Betray(GodPersonality.Trickster, far) + 0.5f);
        Assert.True(Betray(GodPersonality.Guardian, near) < Betray(GodPersonality.Guardian, far));
    }

    [Fact]
    public void AnAllyDominatingWithThisGod_IsNoReasonToBetrayIt()
    {
        var world = PantheonWorlds.Create(0.5f, 0.2f, 0.3f);
        PantheonWorlds.SetStatus(world, Tyrant, Player, DiplomaticStatus.Alliance);
        world.Gods[0].DominantSinceTick = 0;
        var together = PantheonWorlds.Diplomat();
        var alone = PantheonWorlds.Diplomat(DivineWorlds.Rules with { PantheonVictory = false });

        var stay = PantheonWorlds.Move(together, world, Tyrant, GodPersonality.Guardian, DiplomaticCommandKind.BreakAlliance, Player);
        var leave = PantheonWorlds.Move(alone, world, Tyrant, GodPersonality.Guardian, DiplomaticCommandKind.BreakAlliance, Player);

        Assert.True(leave.Utility > stay.Utility + 0.8f, $"{leave.Utility} vs {stay.Utility}");
    }

    [Fact]
    public void EveryPersonality_HasAProfile_AndANameOnScreen()
    {
        var personalities = Enum.GetValues<GodPersonality>().Where(p => p != GodPersonality.None);

        Assert.All(personalities, p => Assert.NotNull(PantheonWorlds.Profile(p)));
        Assert.Equal(
            ["Trickster", "Isolationist", "Warlord"],
            new[] { GodPersonality.Trickster, GodPersonality.Isolationist, GodPersonality.Warlord }.Select(Pantheon.Game.Divine.DivineText.Personality));
        Assert.Equal(4, TestContent.Shipped.Pantheon.Gods.Count);
        Assert.Equal(3, TestContent.Shipped.Pantheon.Gods.Select(g => g.Personality).Distinct().Count(p => p != GodPersonality.None));
    }

    [Fact]
    public void Isolationist_TendsItsOwn_AndKeepsOutOfWars()
    {
        var world = RivalWorlds.Create(GodPersonality.Isolationist, 500f, 200f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        var home = world.Settlements[1];
        DivineWorlds.Believe(home, adherence: 0.7f, faith: 0.6f, godId: RivalWorlds.RivalId);
        home.Food = 0f;
        home.Health = 0.4f;
        DiplomacyWorlds.SetStatus(world, DiplomaticStatus.Hostile);
        var diplomat = PantheonWorlds.Diplomat();

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1])[0];
        var isolationistWar = PantheonWorlds.Move(diplomat, world, RivalWorlds.RivalId, GodPersonality.Isolationist, DiplomaticCommandKind.DeclareWar, Player);
        var warlordWar = PantheonWorlds.Move(diplomat, world, RivalWorlds.RivalId, GodPersonality.Warlord, DiplomaticCommandKind.DeclareWar, Player);

        Assert.Equal(home.Id, best.SettlementId);
        Assert.StartsWith("protect", best.Reason, StringComparison.Ordinal);
        Assert.True(isolationistWar.Utility < 0f);
        Assert.True(warlordWar.Utility > isolationistWar.Utility + 0.4f);
    }

    [Fact]
    public void Warlord_StrikesItsRivalsFollowers()
    {
        var world = RivalWorlds.Create(GodPersonality.Warlord, 500f, 200f, 450f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.8f, godId: RivalWorlds.RivalId);

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1])[0];

        Assert.Equal("lightning", best.MiracleId);
        Assert.Equal(world.Settlements[0].Id, best.SettlementId);
    }

    [Fact]
    public void Trickster_CourtsARivalsPeopleInHardship()
    {
        var world = RivalWorlds.Create(GodPersonality.Trickster, 500f, 200f, 450f);
        var theirs = world.Settlements[0];
        DivineWorlds.Believe(theirs, adherence: 0.6f, faith: 0.5f, doubt: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.8f, godId: RivalWorlds.RivalId);
        theirs.Food = 0f;

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1])[0];

        Assert.Equal(theirs.Id, best.SettlementId);
        Assert.StartsWith("convert", best.Reason, StringComparison.Ordinal);
    }
}
