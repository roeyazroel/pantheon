using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Simulation.Culture;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Culture;

/// <summary>What the world remembers of a god's acts, and the traits that emerge from it (spec §3.2, §11, ADR-024).</summary>
public sealed class ConductTests
{
    private static readonly CultureRules Rules = CultureWorlds.Shipped;

    private static float T => Rules.TraitThreshold;

    [Fact]
    public void Help_WellAboveHarm_IsCompassion_AndWithGifts_Generosity()
    {
        var conduct = Conduct(c => (c.Help, c.Gifts, c.Harm) = (2f * T, T, 0.5f * T));

        Assert.Equal([GodTrait.Compassionate, GodTrait.Generous], ConductModel.Traits(Rules, conduct));
    }

    [Fact]
    public void Harm_WellAboveHelp_OrCrueltyToItsOwn_IsCruelty()
    {
        Assert.Equal([GodTrait.Cruel], ConductModel.Traits(Rules, Conduct(c => (c.Harm, c.Help) = (2f * T, T))));
        Assert.Equal([GodTrait.Cruel], ConductModel.Traits(Rules, Conduct(c => c.Cruelty = 0.5f * T)));
        Assert.Empty(ConductModel.Traits(Rules, Conduct(c => (c.Harm, c.Help) = (1.5f * T, T))));
    }

    [Fact]
    public void Faithfulness_IsMercyAndHonesty_UntilDeceitOrWarSpoilIt()
    {
        Assert.Equal([GodTrait.Merciful, GodTrait.Honest], ConductModel.Traits(Rules, Conduct(c => c.Faithfulness = T)));
        Assert.Equal([GodTrait.Deceptive, GodTrait.Ambitious], ConductModel.Traits(Rules, Conduct(c => (c.Faithfulness, c.Deceit, c.Aggression) = (T, T, 2f * T))));
    }

    [Fact]
    public void Vengeance_Upheaval_AndWars_ShowTheirOwnTraits()
    {
        var conduct = Conduct(c => (c.Vengeance, c.Upheaval, c.Aggression) = (T, T, T));

        Assert.Equal([GodTrait.Vengeful, GodTrait.Chaotic, GodTrait.Ambitious], ConductModel.Traits(Rules, conduct));
    }

    [Fact]
    public void Acts_AreNoted_AndFade()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var conduct = world.Gods[0].Conduct;

        ConductModel.Note(world, DivineWorlds.GodId, ConductModel.Act.Help, 2f);
        ConductModel.Note(world, DivineWorlds.GodId, ConductModel.Act.Upheaval);
        ConductModel.Note(world, 99, ConductModel.Act.Harm);
        ConductModel.Fade(conduct, 0.25f);

        Assert.Equal((1.5f, 0.75f, 0f), (conduct.Help, conduct.Upheaval, conduct.Harm));
    }

    [Fact]
    public void DeclaringWar_IsNotedAsAggression_AndMakingPeace_AsFaithfulnessOnBothSides()
    {
        var world = DiplomacyWorlds.Create(200f);
        foreach (var god in world.Gods)
        {
            god.Conduct.Aggression = 0f;
        }

        var authority = DiplomacyWorlds.Authority();
        var events = new List<WorldEvent>();
        const int me = 1, rival = RivalWorlds.RivalId;

        authority.Execute(world, new DiplomaticCommand(rival, me, DiplomaticCommandKind.DeclareWar), rival, events);
        world.Tick += (long)(DiplomacyWorlds.Rules.StatusCooldownSeconds * Domain.World.SimulationTime.TicksPerSecond);
        authority.Execute(world, new DiplomaticCommand(me, rival, DiplomaticCommandKind.RequestPeace), me, events);
        authority.Execute(world, new DiplomaticCommand(rival, me, DiplomaticCommandKind.Accept), rival, events);

        var gods = world.Gods.ToDictionary(g => g.Id);
        Assert.Equal(2f, gods[rival].Conduct.Aggression);
        Assert.Equal(0f, gods[me].Conduct.Aggression);
        Assert.True(gods[me].Conduct.Faithfulness > 0f);
        Assert.True(gods[rival].Conduct.Faithfulness > 0f);
    }

    private static GodConduct Conduct(System.Action<GodConduct> set)
    {
        var conduct = GodConduct.None();
        set(conduct);
        return conduct;
    }
}
