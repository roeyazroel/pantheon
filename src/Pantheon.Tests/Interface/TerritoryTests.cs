using System;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>The gods' territories painted on the land (ADR-069): one god's land at a time, a bright border on its side, and the player's land washed in its colour.</summary>
public sealed class TerritoryTests
{
    private static readonly Lazy<(GameSession Session, Territories Territories)> Island = new(() =>
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var extent = session.World.Regions.Columns * session.World.Regions.RegionSize;
        return (session, Territories.Build(Territories.Fields(session.Content.DivineRules, session.World), extent));
    });

    [Fact]
    public void LandIsOneGodsAtMost_AndEachTerritoryIsWhereItsGodLeads()
    {
        var (session, territories) = Island.Value;

        Assert.True(territories.Shares.Values.Sum() <= 1.0001f, "no land is two gods' territory");
        Assert.True(territories.Shares[session.LocalGodId!.Value] > 0f, "the player's god holds land at the start");
        foreach (var (x, z) in Points(territories))
        {
            Assert.True(Enumerable.Range(0, territories.GodIds.Count).Count(g => territories.Lead(g, x, z) > 0f) <= 1);
        }
    }

    [Fact]
    public void TheBorderIsBrightOnItsGodsSide_NothingIsPaintedOutside_AndOnlyThePlayersLandIsWashed()
    {
        var (session, territories) = Island.Value;
        var me = session.LocalGodId;
        var look = TerritoryLook.For(territories.Extent);
        var pixel = territories.Extent / TerritoryImage.SideFor(territories.Extent);
        var lines = 0;
        foreach (var (x, z) in Points(territories))
        {
            var (god, alpha, edge) = TerritoryImage.Paint(territories, x, z, pixel, me, look);
            var (lead, inside) = territories.LeadAndSlope(god, x, z);
            var mine = territories.GodIds[god] == me;
            if (inside < -(look.Outline + pixel))
            {
                Assert.Equal(0f, alpha);
            }
            else if (inside < 0f)
            {
                Assert.True(mine ? edge : !edge && alpha <= 0.5f, "only the player's border has a dark edge outside; a rival's only softens");
            }
            else if (inside < look.Line * 0.5f)
            {
                Assert.True(alpha >= 0.95f * (mine ? 1f : look.RivalOpacity) - 1e-3f, "the border line is strong");
                Assert.False(edge);
                lines++;
            }
            else if (inside > look.Glow && lead > 0f)
            {
                Assert.Equal(mine ? look.Wash : 0f, alpha, 3);
            }
        }

        Assert.True(lines > 0, "the scan crossed a border");
    }

    [Fact]
    public void TheImage_IsTheSameForTheSameWorld_AndNoInfluenceMeansNothingPainted()
    {
        var (session, territories) = Island.Value;
        var fields = Territories.Fields(session.Content.DivineRules, session.World);
        var none = Territories.Build(fields, territories.Extent, threshold: 1.01f);

        var first = TerritoryImage.Render(territories, 256, _ => new RgbColor(1f, 0.5f, 0.2f), session.LocalGodId, TerritoryLook.For(territories.Extent));
        var second = TerritoryImage.Render(territories, 256, _ => new RgbColor(1f, 0.5f, 0.2f), session.LocalGodId, TerritoryLook.For(territories.Extent));
        var empty = TerritoryImage.Render(none, 256, _ => new RgbColor(1f, 0.5f, 0.2f), session.LocalGodId, TerritoryLook.For(territories.Extent));

        Assert.Equal(first, second);
        Assert.Contains(first, b => b > 0);
        Assert.All(empty, b => Assert.Equal(0, b));
        Assert.All(none.Shares.Values, share => Assert.Equal(0f, share));
        Assert.Equal(512, TerritoryImage.SideFor(400f));
        Assert.Equal(1365, TerritoryImage.SideFor(2048f));
        Assert.Equal(1536, TerritoryImage.SideFor(12288f));
        Assert.True(TerritoryLook.For(2048f, 1520f).Line > TerritoryLook.For(2048f, 380f).Line, "the borders widen as the camera rises");
        Assert.Equal(0, TerritoryLook.Band(380f));
        Assert.Equal(2, TerritoryLook.Band(1520f));
        Assert.Equal(1520f, TerritoryLook.DistanceOf(2));
        var land = TerritoryImage.Render(territories, 256, _ => new RgbColor(1f, 0.5f, 0.2f), session.LocalGodId, TerritoryLook.For(territories.Extent), (x, _) => x < territories.Extent / 2f);
        Assert.All(Enumerable.Range(0, 256).Select(row => land[(((row * 256) + 200) * 4) + 3]), alpha => Assert.Equal(0, alpha));
    }

    /// <summary>A scan of points over the island, five metres apart along a few lines.</summary>
    private static System.Collections.Generic.IEnumerable<(float X, float Z)> Points(Territories territories)
    {
        for (var line = 1; line < 8; line++)
        {
            for (var x = 0f; x < territories.Extent; x += 5f)
            {
                yield return (x, territories.Extent * line / 8f);
                yield return (territories.Extent * line / 8f, x);
            }
        }
    }
}
