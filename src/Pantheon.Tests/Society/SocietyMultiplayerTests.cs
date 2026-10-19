using System.Linq;
using Pantheon.Domain.Society;
using Pantheon.Game.Society;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>A joined player sets the course of its god's villages through the host (spec §73, ADR-023).</summary>
public sealed class SocietyMultiplayerTests
{
    [Fact]
    public void APlayersPriorityIntent_SetsItsGodsVillagesCourse_OnTheHost_AndItsReplica()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var home = shared.Host.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == 2 && b.Adherence >= 0.5f));
        var stranger = shared.Host.World.Settlements.First(s => s.Beliefs.All(b => b.GodId != 2 || b.Adherence < 0.4f));

        var asking = SocietyCommands.SetPriority(ana.Session!, home.Id, SettlementPriority.Worship);
        var refused = SocietyCommands.SetPriority(ana.Session!, stranger.Id, SettlementPriority.Worship);
        var statuses = shared.Exchange(ana);
        shared.Pass(3.0, ana);

        Assert.Equal("Asking the host...", asking);
        Assert.Equal("Asking the host...", refused);
        Assert.Equal((SettlementPriority.Worship, (int?)2), (home.Society.Priority, home.Society.PriorityGodId));
        Assert.Contains(home.Name + " will focus on worship", statuses);
        Assert.Contains(statuses, status => status.StartsWith("Too few in " + stranger.Name, System.StringComparison.Ordinal));
        Assert.Equal(SettlementPriority.Worship, ana.Session!.World.Settlements.Single(s => s.Id == home.Id).Society.Priority);
    }
}
