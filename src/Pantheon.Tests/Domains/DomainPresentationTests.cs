using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Divine;
using Pantheon.Game.Domains;
using Pantheon.Game.Hud;
using Pantheon.Simulation.Miracles;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Domains;

/// <summary>What the player sees of domains, holy sites and schisms (ADR-021): the Domains panel, the HUD, the cast hints and the debug view.</summary>
public sealed class DomainPresentationTests
{
    [Fact]
    public void TheDomainsPanel_ListsTheGodsDomainsFirst_WhatEachGives_AndTheHolySites()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var god = session.LocalGod!;
        god.Domains.Add(GodDomain.Storm);
        session.World.HolySites[0].KeeperGodId = god.Id;

        var panel = DomainReadout.Build(session);

        Assert.Equal("Ilyra holds 1 of 1 domain. A Lesser God may hold 2.", panel.Slots);
        Assert.Equal(GodDomain.Storm, panel.Domains[0].Domain);
        Assert.True(panel.Domains[0].Held);
        Assert.All(panel.Domains.Skip(1), d => Assert.False(d.CanChoose));
        Assert.Equal(TestContent.Shipped.Domains.Domains.Count, panel.Domains.Count);
        Assert.StartsWith("Signature: Redirect Storm (Minor Spirit, 72 power)", panel.Domains[0].Signature, StringComparison.Ordinal);
        Assert.Equal(session.World.HolySites.Count, panel.Sites.Count);
        Assert.True(panel.Sites[0].Yours);
        Assert.Equal("kept by Ilyra", panel.Sites[0].State);
        Assert.Contains(" - keeps the ", HudReadout.Build(session, null, null).Domains, StringComparison.Ordinal);
    }

    [Fact]
    public void CastHints_SayWhichDomainASignatureNeeds_AndWhatItDidNotFind()
    {
        var miracle = TestContent.Shipped.Miracles.Find("redirect_storm")!;
        var spear = TestContent.Shipped.Miracles.Find("spear_of_heaven")!;

        var needs = DivineText.DescribeCheck(new CastCheck(CastRejection.DomainRequired, miracle, 0.5f, 90f, 0), miracle.Id, 100f, 10);
        var cloud = DivineText.DescribeCheck(new CastCheck(CastRejection.NothingThere, miracle, 0.5f, 90f, 0), miracle.Id, 100f, 10);
        var creature = DivineText.DescribeCheck(new CastCheck(CastRejection.NothingThere, spear, 0.5f, 90f, 0), spear.Id, 100f, 10);

        Assert.Equal("Redirect Storm belongs to the Storm: take up the Storm first (G)", needs);
        Assert.Equal("Redirect Storm: no rain or storm cloud within 900 m", cloud);
        Assert.Equal("Spear of Heaven: no rival Creature there", creature);
    }

    [Fact]
    public void TheDebugView_ShowsDomainsSitesAndASettlementsOldFaith()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var world = session.World;
        world.Gods[1].Domains.Add(GodDomain.Death);
        var village = world.Settlements[0];
        village.OldFaith = new Pantheon.Domain.Belief.OldFaith { GodId = 1, Share = 0.2f, Returned = 0.05f, Faded = 0f, SinceTick = 0 };
        (village.SanctuaryGodId, village.SanctuaryUntilTick) = (1, 600);

        var lines = DebugReadout.Build(new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = session.Content.SettlementRules,
            DivineRules = session.Content.DivineRules,
            HolySiteRules = session.Content.HolySites,
            SelectedSettlementId = village.Id,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "Terrain3D",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 300f,
            RegionBoundariesVisible = false,
        }).Lines;

        Assert.Contains(lines, l => l.StartsWith("Domains  Ilyra: no domain yet  Vael: Death", StringComparison.Ordinal));
        Assert.Equal(world.HolySites.Count, lines.Count(l => l.StartsWith("Site ", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.StartsWith($"Old Faith of Ilyra: 20% of the people ({0.05f * village.Population:0} returned, 0 lost); Sanctuary of Ilyra for 60 s", StringComparison.Ordinal));
    }
}
