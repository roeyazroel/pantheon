using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>Religion's look on villages (spec §61, ADR-042).</summary>
public sealed class ReligiousStyleTests
{
    private static readonly StyleSettings Settings = TestContent.SettlementView.Style;

    [Fact]
    public void ALovedNatureGod_GrowsGardens_AFearedWarGod_RaisesMonuments_AndALearnedFaith_Columns()
    {
        var (session, village) = Village();
        var god = session.World.Gods.Single(g => g.Id == Leader(village));
        var belief = village.Beliefs.Single(b => b.GodId == god.Id);

        Set(god, [GodDomain.Nature, GodDomain.Life], []);
        (belief.Love, belief.Fear) = (0.8f, 0f);
        var garden = ReligiousStyles.Of(village, session.World, Settings);

        Set(god, [GodDomain.War, GodDomain.Death], [GodTrait.Cruel]);
        (belief.Love, belief.Fear) = (0f, 0.8f);
        village.Culture.Militarism = 0.7f;
        var monument = ReligiousStyles.Of(village, session.World, Settings);

        Set(god, [GodDomain.Knowledge], [GodTrait.Honest]);
        (belief.Love, belief.Fear, village.Culture.Militarism, village.Culture.KnowledgeSeeking) = (0f, 0f, 0f, 0.8f);
        var lore = ReligiousStyles.Of(village, session.World, Settings);

        Assert.Equal((StyleMotif.Garden, god.Id), (garden!.Motif, garden.GodId));
        Assert.Equal(StyleMotif.Monument, monument!.Motif);
        Assert.Equal(StyleMotif.Lore, lore!.Motif);
        Assert.Contains("doctrine 0.80", lore.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AVillageNoGodHoldsMostOf_ShowsNoStyle_AndAWeakerHoldShowsLess()
    {
        var (session, village) = Village();
        var god = session.World.Gods.Single(g => g.Id == Leader(village));
        Set(god, [GodDomain.Nature], []);
        var belief = village.Beliefs.Single(b => b.GodId == god.Id);
        belief.Love = 1f;
        belief.Adherence = 0.9f;
        var strong = ReligiousStyles.Of(village, session.World, Settings)!;
        belief.Adherence = 0.6f;
        var weaker = ReligiousStyles.Of(village, session.World, Settings)!;
        belief.Adherence = 0.4f;

        Assert.True(strong.Strength > weaker.Strength);
        Assert.Null(ReligiousStyles.Of(village, session.World, Settings));
    }

    [Fact]
    public void Monuments_StandInARingAboutTheShrine_ByTurns_ClearOfTheTemple_AndTheSameEachTime()
    {
        var (session, village) = Village();
        var site = Site(session, village);
        var style = new ReligiousStyle(StyleMotif.Monument, 1f, Leader(village), string.Empty);

        var pieces = ReligiousStyles.Layout(style, village, site, session.Terrain, Settings);
        var again = ReligiousStyles.Layout(style, village, site, session.Terrain, Settings);
        var (temple, _) = CivicLayout.Temple(site, session.Terrain, village.Id);

        Assert.Equal(pieces, again);
        Assert.Contains(pieces, p => p.Kind == StylePieceKind.Obelisk);
        Assert.Contains(pieces, p => p.Kind == StylePieceKind.Brazier);
        Assert.All(pieces, p => Assert.Equal(Settings.ShrineRingRadius, Vector2.Distance(new(p.Position.X, p.Position.Z), new(site.Center.X, site.Center.Z)), 2));
        Assert.All(pieces, p => Assert.True(Vector2.Distance(new(p.Position.X, p.Position.Z), new(temple.X, temple.Z)) > 2f));
    }

    [Fact]
    public void Gardens_GrowAmongTheHouses_ClearOfThem_MoreWhenStronger_AndColumnsRingTheShrine()
    {
        var (session, village) = Village();
        var site = Site(session, village);
        var god = Leader(village);

        var full = ReligiousStyles.Layout(new ReligiousStyle(StyleMotif.Garden, 1f, god, string.Empty), village, site, session.Terrain, Settings);
        var faint = ReligiousStyles.Layout(new ReligiousStyle(StyleMotif.Garden, 0.2f, god, string.Empty), village, site, session.Terrain, Settings);
        var columns = ReligiousStyles.Layout(new ReligiousStyle(StyleMotif.Lore, 0.2f, god, string.Empty), village, site, session.Terrain, Settings);

        Assert.True(full.Count > faint.Count);
        Assert.All(full, p => Assert.True(p.Kind is StylePieceKind.FlowerBed or StylePieceKind.Bush));
        Assert.All(full, p => Assert.All(site.Houses, h => Assert.True(Vector2.Distance(new(p.Position.X, p.Position.Z), new(h.Position.X, h.Position.Z)) >= Settings.HouseClearance)));
        Assert.True(columns.Count(p => p.Kind == StylePieceKind.Column) >= 4);
    }

    /// <summary>A small world's first village most of whose people follow one god, with its god's domains set.</summary>
    private static (GameSession Session, SettlementState Village) Village()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements.First(s => s.Beliefs.Any(b => b.Adherence >= 0.5f) && s.Population > 20);
        return (session, village);
    }

    /// <summary>The god most of a village follows.</summary>
    private static int Leader(SettlementState village) => village.Beliefs.OrderByDescending(b => b.Adherence).First().GodId;

    /// <summary>Gives a god domains and traits.</summary>
    private static void Set(GodState god, GodDomain[] domains, GodTrait[] traits)
    {
        god.Domains.Clear();
        god.Domains.AddRange(domains);
        god.Traits.Clear();
        god.Traits.AddRange(traits);
    }

    /// <summary>A village's layout.</summary>
    private static SettlementSite Site(GameSession session, SettlementState village) =>
        SettlementLayout.ComputeSite(village, session.Content.SettlementRules, session.Terrain, TestContent.SettlementView.Layout);
}
