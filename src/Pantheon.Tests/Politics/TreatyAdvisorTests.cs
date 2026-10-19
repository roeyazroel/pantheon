using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Politics;

/// <summary>How AI gods weigh treaties and requests (spec §47–49, ADR-025).</summary>
public sealed class TreatyAdvisorTests
{
    private static GodProfile Guardian => TestContent.Shipped.GodAi.Profiles!.First(p => p.Personality == GodPersonality.Guardian);

    [Fact]
    public void AGodsWord_CountsInWhatOthersAccept()
    {
        var world = Create(300f);
        var guardian = world.Gods.Single(g => g.Id == Third);
        var proposal = new DiplomaticProposal { FromGodId = Rival, ToGodId = Third, Kind = ProposalKind.Treaty, ExpiresAtTick = world.Tick + 600, Terms = Terms(TreatyKind.SharedInfluence) };
        var view = DiplomacyModel.NewRelation(DiplomacyWorlds.Rules, Third, Rival);
        var plain = TreatyAdvisor.AnswerUtility(DiplomacyWorlds.Rules, world, guardian, proposal, Guardian, view);

        world.Gods.Single(g => g.Id == Rival).Traits.Add(GodTrait.Deceptive);

        Assert.True(TreatyAdvisor.AnswerUtility(DiplomacyWorlds.Rules, world, guardian, proposal, Guardian, view) < plain - 0.3f);
    }

    [Fact]
    public void AGod_PaysNoTributeItCannotAfford_AndHonoursAFavourItOwes()
    {
        var world = Create(300f);
        var guardian = world.Gods.Single(g => g.Id == Third);
        guardian.DivinePower = 10f;
        var view = DiplomacyModel.NewRelation(DiplomacyWorlds.Rules, Third, Rival);
        var tribute = new DiplomaticProposal { FromGodId = Rival, ToGodId = Third, Kind = ProposalKind.Treaty, ExpiresAtTick = 0, Terms = Terms(TreatyKind.Tribute) with { PayerGodId = Third, Amount = 20f } };
        var ask = new DiplomaticProposal { FromGodId = Rival, ToGodId = Third, Kind = ProposalKind.Request, ExpiresAtTick = 0, Ask = new RequestAsk(AskKind.Backing) };
        var unowed = TreatyAdvisor.AnswerUtility(DiplomacyWorlds.Rules, world, guardian, ask, Guardian, view);

        FavourBook.Owe(DiplomacyWorlds.PoliticsRules, world, Rival, Third, null);

        Assert.True(TreatyAdvisor.AnswerUtility(DiplomacyWorlds.Rules, world, guardian, tribute, Guardian, view) < -1f);
        Assert.True(TreatyAdvisor.AnswerUtility(DiplomacyWorlds.Rules, world, guardian, ask, Guardian, view) > unowed + 0.5f);
    }

    [Fact]
    public void AWearyGodAtWar_ProposesANonAggressionPact()
    {
        var world = Create(300f);
        SetStatus(world, Third, Rival, DiplomaticStatus.War);
        world.Tick = 100_000;
        var advisor = new TreatyAdvisor(DiplomacyWorlds.Rules, Authority());

        var moves = advisor.Moves(world, world.Gods.Single(g => g.Id == Third), Guardian);

        Assert.Contains(moves, m => m.Command.Kind == DiplomaticCommandKind.ProposeTreaty && m.Command.Terms!.Kind == TreatyKind.NonAggression && m.Command.OtherGodId == Rival && m.Utility > 0.2f);
    }
}
