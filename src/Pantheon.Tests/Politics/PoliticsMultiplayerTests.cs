using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Politics;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Politics;

/// <summary>Treaties and the council in a shared world (spec §39, §43, §73, ADR-025).</summary>
public sealed class PoliticsMultiplayerTests
{
    [Fact]
    public void APlayersTreatyAndVote_ReachTheHost()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var host = shared.Host;
        var me = ana.Session!.LocalGodId!.Value;
        Meetings.All(host.World);
        host.World.Tick = 20_000;
        PoliticsCommands.Summon(host);
        shared.Exchange(ana);

        PoliticsCommands.Run(ana.Session!, new PoliticsAction("NAP", new DiplomaticCommand(me, host.LocalGodId!.Value, DiplomaticCommandKind.ProposeTreaty, Terms: PoliticsWorlds.Terms(TreatyKind.NonAggression))));
        PoliticsCommands.Run(ana.Session!, new PoliticsAction("For", Council: new Pantheon.Simulation.Politics.CouncilCommand(me, null, VoteChoice.Yes)));
        var statuses = shared.Exchange(ana);

        Assert.Contains(host.World.Diplomacy.Proposals, p => p.FromGodId == me && p.Terms?.Kind == TreatyKind.NonAggression);
        Assert.Contains(host.World.Diplomacy.Council.Session!.Votes, v => v.GodId == me && v.Choice == VoteChoice.Yes);
        Assert.Contains("You vote for", statuses);
    }

    [Fact]
    public void SecretTreaties_ReachOnlyTheirParties()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var host = shared.Host;
        var world = Meetings.All(host.World);
        world.Tick = 20_000;
        var ais = world.Gods.Where(g => g.Id != host.LocalGodId && g.Id != ana.Session!.LocalGodId).Select(g => g.Id).ToList();
        var terms = PoliticsWorlds.Terms(TreatyKind.JointWar) with { TargetGodId = ana.Session!.LocalGodId, Secret = true };
        host.Command((a, events) => a.Diplomacy.Execute(world, new DiplomaticCommand(ais[0], ais[1], DiplomaticCommandKind.ProposeTreaty, Terms: terms), null, events));
        host.Command((a, events) => a.Diplomacy.Execute(world, new DiplomaticCommand(ais[1], ais[0], DiplomaticCommandKind.Accept), null, events));

        shared.Pass(25.0, ana);
        var signed = new TreatySigned { Tick = 1, TreatyId = 1, Kind = TreatyKind.JointWar, FirstGodId = ais[0], SecondGodId = ais[1], TargetGodId = null, Secret = true };

        Assert.Single(world.Diplomacy.Treaties);
        Assert.Empty(ana.Session!.World.Diplomacy.Treaties);
        Assert.Empty(WorldSaveSerializer.Deserialize(SecretFilter.World(WorldSaveSerializer.SerializeCompact(world), ana.Session.LocalGodId!.Value)).Diplomacy.Treaties);
        Assert.Single(WorldSaveSerializer.Deserialize(SecretFilter.World(WorldSaveSerializer.SerializeCompact(world), ais[0])).Diplomacy.Treaties);
        Assert.Empty(SecretFilter.Events([signed], ana.Session.LocalGodId!.Value));
        Assert.Single(SecretFilter.Events([signed], ais[1]));
    }
}
