using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Disasters;
using Pantheon.Game.Hand;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>Debris (spec §94, §55, ADR-048): what flies when houses fall or a thrown thing strikes.</summary>
public sealed class DebrisTests
{
    private static readonly DebrisSettings Settings = TestContent.SettlementView.Debris;

    [Fact]
    public void AFallenHouse_ThrowsUpPlanksStonesAndThatch_AStrikeOnlyStones_TheSameEachTime()
    {
        var key = DebrisLayout.KeyOf(812.4f, -301.7f);

        var house = DebrisLayout.Burst(Settings, DebrisCause.House, key, 1.2f);
        var strike = DebrisLayout.Burst(Settings, DebrisCause.Strike, key, 1f);

        Assert.Equal(
            (Settings.HousePlanks, Settings.HouseStones, Settings.HouseShards),
            (house.Count(p => p.Shape == DebrisShape.Plank), house.Count(p => p.Shape == DebrisShape.Stone), house.Count(p => p.Shape == DebrisShape.Shard)));
        Assert.True(strike.Count == Settings.StrikeStones && strike.All(p => p.Shape == DebrisShape.Stone));
        Assert.Equal(house, DebrisLayout.Burst(Settings, DebrisCause.House, key, 1.2f));
        Assert.NotEqual(house, DebrisLayout.Burst(Settings, DebrisCause.House, DebrisLayout.KeyOf(812.4f, -301.6f), 1.2f));
        Assert.All(house, p =>
        {
            Assert.True(p.Velocity.Y > 0f && p.Velocity.Y <= Settings.Lift, "every piece flies up");
            Assert.True(Vector2.Dot(new Vector2(p.Offset.X, p.Offset.Z), new Vector2(p.Velocity.X, p.Velocity.Z)) > 0f, "and outward, away from the house");
            Assert.True(p.Size.X > 0f && p.Size.Y > 0f && p.Size.Z > 0f);
        });
    }

    [Fact]
    public void ABiggerHouse_ThrowsBiggerPieces_AndNoSettingsMeansNoPieces()
    {
        var key = DebrisLayout.KeyOf(10f, 20f);
        var small = DebrisLayout.Burst(Settings, DebrisCause.House, key, 0.8f);
        var big = DebrisLayout.Burst(Settings, DebrisCause.House, key, 1.6f);
        var none = Settings with { HousePlanks = 0, HouseStones = 0, HouseShards = 0 };

        Assert.All(small.Zip(big), pair => Assert.Equal(pair.First.Size.X * 2f, pair.Second.Size.X, 3));
        Assert.Empty(DebrisLayout.Burst(none, DebrisCause.House, key, 1f));
    }

    [Fact]
    public void AThrownRockOrTree_LandsOnTheLineOfTheThrow_AndRollsOn_ButThrownPeopleAreNotLeftLying()
    {
        var away = new Vector2(3f, 4f);

        var rock = DebrisLayout.Thrown(Settings, HeldKind.Rock, away);
        var tree = DebrisLayout.Thrown(Settings, HeldKind.Tree, away);

        Assert.True(rock is { Shape: DebrisShape.Stone } && tree is { Shape: DebrisShape.Plank } && tree.Value.Size.Z > rock.Value.Size.Z);
        foreach (var piece in new[] { rock!.Value, tree!.Value })
        {
            var ahead = Vector2.Normalize(away);
            Assert.True(Vector2.Dot(new Vector2(piece.Offset.X, piece.Offset.Z), ahead) < 0f && piece.Offset.Y > 0f, "it comes in from behind and above");
            Assert.True(Vector2.Dot(new Vector2(piece.Velocity.X, piece.Velocity.Z), ahead) > 0f && piece.Velocity.Y < 0f, "on along the throw and down");
            Assert.Equal(0f, Vector3.Dot(piece.Spin, new Vector3(ahead.X, 0f, ahead.Y)), 4);
        }

        Assert.All([HeldKind.Villagers, HeldKind.Creature, HeldKind.Offering], kind => Assert.Null(DebrisLayout.Thrown(Settings, kind, away)));
    }

    [Fact]
    public void TheHousesThatFell_AreThoseThatStoodBefore_AndNowLieInRuins_NotOnesRebuiltOrNeverBuilt()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var rules = session.Content.SettlementRules;
        var village = session.World.Settlements.OrderByDescending(s => s.Population).First();
        (village.ConstructionProgress, village.Ruins) = (0f, 0);
        SettlementSite Layout() => SettlementLayout.ComputeSite(village, rules, session.Terrain, TestContent.SettlementView.Layout);
        var before = Layout();

        village.Housing -= 2 * rules.HousingPerHouse;
        village.Ruins += 2;
        var after = Layout();
        var fell = DebrisLayout.Fallen(before, after);

        Assert.True(before.Houses.Count >= 4);
        Assert.Equal(2, fell.Count);
        Assert.Equal(after.Ruins.Select(r => r.Position).OrderBy(p => p.X), fell.Select(h => h.Position).OrderBy(p => p.X));
        Assert.All(fell, h => Assert.DoesNotContain(after.Houses, s => s.Position == h.Position));

        village.Housing += rules.HousingPerHouse;
        village.Ruins -= 1;
        Assert.Empty(DebrisLayout.Fallen(after, Layout()));
        village.Housing += rules.HousingPerHouse;
        Assert.Empty(DebrisLayout.Fallen(before, Layout()));
    }

    [Fact]
    public void TheDebugFallingStar_StrikesLikeOneThatFalls_SoItsNewsAndRuinsReachTheDebris()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements.OrderByDescending(s => s.Population).First();
        var (ruins, people) = (village.Ruins, village.Population);
        session.DrainEvents();

        HazardCommands.Start(session, HazardKind.Meteor, village.Position);

        var events = session.DrainEvents();
        Assert.Equal(village.Position, Assert.Single(events.OfType<MeteorStruck>()).Position);
        Assert.Single(events.OfType<EarthquakeStruck>());
        Assert.True(village.Ruins > ruins && village.Population < people, $"ruins {ruins} -> {village.Ruins}, people {people} -> {village.Population}");
    }

    [Fact]
    public void ThrowingAtTheStart_LiftsSomethingBesideTheVillage_AndStrikesIt()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalGodId!.Value;
        var village = session.World.Settlements.OrderByDescending(s => s.Beliefs.Find(b => b.GodId == mine)?.Adherence ?? 0f).First();
        session.DrainEvents();

        var text = HandCommands.ThrowAt(session, village.Position);

        var struck = Assert.Single(session.DrainEvents().OfType<HandStruck>());
        Assert.StartsWith("You hurl", text, StringComparison.Ordinal);
        Assert.True(struck.Kind is HeldKind.Rock or HeldKind.Tree && struck.Position == village.Position && struck.SettlementId == village.Id);
        Assert.Null(session.LocalGod!.Held);
        Assert.Equal(HandAtStart.Throw, LaunchOptions.Parse(["--hand=throw"]).Hand);
    }

    [Fact]
    public void DebrisSettings_OutOfRange_AreRejected()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName)
            .Replace("\"Reach\": 700.0", "\"Reach\": 0.0", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));

        Assert.Contains("Debris: Debris needs a positive lifetime longer than its fade", exception.Message, StringComparison.Ordinal);
    }
}
