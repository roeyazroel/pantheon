using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Simulation.Creatures;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>How Creatures regard each other, as pure rules (spec §27, ADR-017).</summary>
public sealed class CreatureBondsTests
{
    private static readonly CreatureBondRules Bonds = CreatureBondWorlds.Bonds;

    [Fact]
    public void WarMakesThemWantToFight_FriendshipToPlay_AndFearAndWeaknessToRun()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        var rules = CreatureBondWorlds.Rules;
        var calm = CreatureBonds.Drives(rules, world, mine, theirs);

        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        var war = CreatureBonds.Drives(rules, world, mine, theirs);
        Assert.True(war.Fight > calm.Fight + 0.3f);
        Assert.True(war.Befriend < calm.Befriend);
        Assert.Equal(CreatureSituation.MeetsEnemyCreature, CreatureBonds.SituationFor(world, mine, theirs));

        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        view.Friendship = 0.9f;
        var friends = CreatureBonds.Drives(rules, world, mine, theirs);
        Assert.Equal(0f, friends.Fight);
        Assert.True(friends.Befriend > war.Befriend);

        view.Friendship = 0f;
        view.Fear = 0.8f;
        mine.Health = 0.3f;
        var afraid = CreatureBonds.Drives(rules, world, mine, theirs);
        Assert.True(afraid.Flee > afraid.Fight);
        Assert.Equal(0f, calm.Flee);
    }

    [Fact]
    public void Confidence_WeighsPowerAndHealth_AndSkillAddsPower()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();
        var rules = CreatureBondWorlds.Rules;

        Assert.Equal(1f, CreatureBonds.Confidence(rules, mine, theirs));
        theirs.CombatSkill = 1f;
        Assert.Equal(CreatureModel.Strength(rules, theirs) * (1f + rules.Combat.SkillPower), CreatureBonds.Power(rules, theirs), 4);
        Assert.Equal(1f / (1f + rules.Combat.SkillPower), CreatureBonds.Confidence(rules, mine, theirs), 4);
        mine.Health = 0.01f;
        Assert.Equal(0.25f, CreatureBonds.Confidence(rules, mine, theirs));
    }

    [Fact]
    public void PlayingTogether_BuildsFriendshipAndTrust_AndAGreetingALittleFriendship()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();

        CreatureBonds.Played(Bonds, mine, theirs.Id, 10);
        CreatureBonds.Greeted(Bonds, mine, theirs, 20);

        var view = CreatureBonds.Find(mine, theirs.Id)!;
        var played = Bonds.FriendshipPerPlay;
        Assert.Equal(played + (Bonds.FriendshipPerGreeting * (1f - played)), view.Friendship, 4);
        Assert.Equal(Bonds.StartingTrust + (Bonds.TrustPerPlay * (1f - Bonds.StartingTrust)), view.Trust, 4);
        Assert.Equal([EncounterKind.Played, EncounterKind.Greeted], view.Encounters.ConvertAll(e => e.Kind));
        Assert.Equal(Bonds.FriendshipPerGreeting, CreatureBonds.Find(theirs, mine.Id)!.Friendship, 4);
    }

    [Fact]
    public void AFriendWhoAttacks_BreaksTheFriendship_AndIsRememberedAsABetrayal()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();
        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        view.Friendship = 0.8f;
        view.Trust = 0.9f;

        CreatureBonds.FightStarted(Bonds, theirs, mine, betrayal: true, 5);

        Assert.Equal(0.8f * (1f - Bonds.FriendshipLossPerFight), view.Friendship, 4);
        Assert.Equal(0.9f * (1f - Bonds.BetrayalTrustLoss), view.Trust, 4);
        Assert.Equal(Bonds.RivalryPerFight, view.Rivalry, 4);
        Assert.Equal(EncounterKind.Betrayed, view.Encounters[^1].Kind);
        Assert.Equal(Bonds.RivalryPerFight, CreatureBonds.Find(theirs, mine.Id)!.Rivalry, 4);
    }

    [Fact]
    public void TheBeaten_FearAndRespectTheWinner_ButTheSparedTrustIt()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();

        CreatureBonds.Beaten(Bonds, mine, theirs, spared: false, 1);
        var beaten = CreatureBonds.Find(theirs, mine.Id)!;
        Assert.Equal(Bonds.FearPerDefeat, beaten.Fear, 4);
        Assert.Equal(Bonds.RespectPerDefeat, beaten.Respect, 4);
        Assert.Equal(EncounterKind.Lost, beaten.Encounters[^1].Kind);
        Assert.Equal(EncounterKind.Won, CreatureBonds.Find(mine, theirs.Id)!.Encounters[^1].Kind);

        var (_, a, b) = CreatureBondWorlds.Create();
        CreatureBonds.Beaten(Bonds, a, b, spared: true, 1);
        var spared = CreatureBonds.Find(b, a.Id)!;
        Assert.Equal(0f, spared.Fear);
        Assert.True(spared.Trust > Bonds.StartingTrust);
        Assert.Equal(Bonds.FriendshipPerSpare, spared.Friendship, 4);
        Assert.Equal(EncounterKind.WasSpared, spared.Encounters[^1].Kind);
        Assert.Equal(EncounterKind.Spared, CreatureBonds.Find(a, b.Id)!.Encounters[^1].Kind);
    }

    [Fact]
    public void Flight_AddsFear_AndADrawRespect()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();

        CreatureBonds.Fled(Bonds, theirs, mine, 1);
        CreatureBonds.Drew(Bonds, mine, theirs, 2);

        Assert.Equal(Bonds.FearPerFlight, CreatureBonds.Find(theirs, mine.Id)!.Fear, 4);
        Assert.Equal([EncounterKind.ChasedOff, EncounterKind.Drew], CreatureBonds.Find(mine, theirs.Id)!.Encounters.ConvertAll(e => e.Kind));
        Assert.Equal(Bonds.RespectPerFight, CreatureBonds.Find(mine, theirs.Id)!.Respect, 4);
    }

    [Fact]
    public void Views_Fade_AndTrustReturnsToWhereItStarted()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();
        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        (view.Fear, view.Rivalry, view.Friendship, view.Respect, view.Trust) = (0.5f, 0.5f, 0.5f, 0.5f, 0.9f);

        CreatureBonds.Fade(Bonds, mine, 100f);

        Assert.Equal(0.5f - (100f * Bonds.FearFadePerSecond), view.Fear, 4);
        Assert.Equal(0.5f - (100f * Bonds.RivalryFadePerSecond), view.Rivalry, 4);
        Assert.Equal(0.5f - (100f * Bonds.FriendshipFadePerSecond), view.Friendship, 4);
        Assert.Equal(0.5f, view.Respect);
        Assert.Equal(0.9f - (100f * Bonds.TrustDriftPerSecond), view.Trust, 4);
        CreatureBonds.Fade(Bonds, mine, 1e6f);
        Assert.Equal(Bonds.StartingTrust, view.Trust);
    }

    [Fact]
    public void TheyMeetAgain_OnlyAfterTheCooldown_AndRememberOnlyTheLatestEncounters()
    {
        var (_, mine, theirs) = CreatureBondWorlds.Create();
        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        var cooldown = (long)(Bonds.MeetCooldownSeconds * 10f);

        Assert.True(CreatureBonds.CanMeet(Bonds, null, 0));
        view.LastMetTick = 100;
        Assert.False(CreatureBonds.CanMeet(Bonds, view, 100 + cooldown - 1));
        Assert.True(CreatureBonds.CanMeet(Bonds, view, 100 + cooldown));

        for (var i = 0; i < Bonds.EncounterMemory + 3; i++)
        {
            CreatureBonds.Greeted(Bonds, mine, theirs, i);
        }

        Assert.Equal(Bonds.EncounterMemory, view.Encounters.Count);
        Assert.Equal(3, view.Encounters[0].Tick);
    }
}
