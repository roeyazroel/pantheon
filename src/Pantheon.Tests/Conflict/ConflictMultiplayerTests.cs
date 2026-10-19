using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Game.Conflict;
using Pantheon.Game.Politics;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Conflict;

/// <summary>Veiled miracles, proxy acts and holy wars in a shared world (spec §73, ADR-026).</summary>
public sealed class ConflictMultiplayerTests
{
    [Fact]
    public void APlayersVeiledCast_ProxyAct_AndWordToAHolyWar_ReachTheHost()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var host = shared.Host;
        var me = ana.Session!.LocalGodId!.Value;
        var home = host.World.Settlements.First(s => Pantheon.Simulation.Politics.TreatyModel.Leader(s) == me);
        var foreign = host.World.Settlements.First(s => Pantheon.Simulation.Politics.TreatyModel.Leader(s) is { } leader && leader != me);
        ConflictPresentationTests.Believe(foreign, me);
        var leader = Pantheon.Simulation.Politics.TreatyModel.Leader(foreign)!.Value;
        ConflictCommands.StartHolyWar(host, home.Id);
        host.World.Gods.Single(g => g.Id == me).DivinePower = 1000f;
        shared.Exchange(ana);

        PoliticsCommands.Run(ana.Session!, new PoliticsAction("Propaganda", Proxy: new ProxyCommand(me, foreign.Id, leader, ProxyKind.Propaganda)));
        PoliticsCommands.Run(ana.Session!, new PoliticsAction("Urge", HolyWar: new Pantheon.Simulation.Conflict.HolyWarCommand(me, home.Id, HolyWarStance.Encourage)));
        ana.Session!.Veiled = true;
        Pantheon.Game.SessionCommands.CastMiracle(ana.Session!, "food", home.Position);
        var statuses = shared.Exchange(ana);

        Assert.Equal(HolyWarStance.Encourage, home.Society.HolyWar!.Stance);
        Assert.Contains(statuses, s => s.StartsWith("Your agents spread propaganda against ", System.StringComparison.Ordinal));
        Assert.Contains(host.World.Gods.Single(g => g.Id == me).Cooldowns, c => c.MiracleId == "proxy");
        Assert.Contains(host.World.Gods.Single(g => g.Id == me).Cooldowns, c => c.MiracleId == "food");
    }
}
