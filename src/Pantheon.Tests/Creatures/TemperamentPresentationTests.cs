using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Book;
using Pantheon.Game.Creatures;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>How a Creature's character is shown and driven (spec §26, ADR-037).</summary>
public sealed class TemperamentPresentationTests
{
    private readonly GameSession _session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

    [Fact]
    public void TheCharacter_NamesEveryStrongTrait_InSection26sWords()
    {
        var creature = _session.LocalCreature!;
        var p = creature.Personality;
        (p.Kindness, p.Aggression, p.Laziness, p.Curiosity, p.Courage, p.Protectiveness, p.Jealousy, p.Playfulness) = (0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
        creature.Loyalty = 0.6f;
        Assert.Equal("even-tempered", TemperamentText.Describe(creature));

        (p.Courage, p.Protectiveness, p.Playfulness) = (0.8f, 0.7f, 0.9f);
        creature.Loyalty = 0.9f;
        Assert.Equal("brave, protective, playful and loyal", TemperamentText.Describe(creature));

        (p.Courage, p.Kindness, p.Aggression, p.Jealousy) = (0.2f, 0.1f, 0.8f, 0.7f);
        creature.Loyalty = 0.3f;
        Assert.Equal(["cowardly", "sadistic", "protective", "playful", "aggressive", "jealous", "independent"], TemperamentText.Traits(creature));
        Assert.Equal(12, TemperamentText.Words.Count);
    }

    [Fact]
    public void TheCard_F1AndTheBook_ShowTheCharacter()
    {
        var creature = _session.LocalCreature!;
        (creature.Personality.Courage, creature.Personality.Playfulness) = (0.9f, 0.9f);

        var card = HudReadout.Build(_session, null, null).Creature!;
        var book = BookReadout.Build(_session, BookTab.Creature);

        Assert.StartsWith("brave", card.Character, StringComparison.Ordinal);
        Assert.Contains("playful", card.Character, StringComparison.Ordinal);
        Assert.Contains(book.Lines, l => l.Text.StartsWith("It is brave", StringComparison.Ordinal) && l.Text.Contains("brave 0.90", StringComparison.Ordinal));
        Assert.Contains("brave 0.90 protective", TemperamentText.Values(creature.Personality), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTemperamentOption_GivesTheCreatureItsTraits_AndWarnsOfUnknownWords()
    {
        var options = LaunchOptions.Parse(["--temperament=brave, Jealous", "--temperament=grumpy"]);

        var said = CreatureCommands.SetTemperament(_session, options.Temperament!);

        Assert.Equal(["brave", "Jealous"], options.Temperament);
        Assert.Single(options.Warnings);
        var creature = _session.LocalCreature!;
        Assert.Equal((0.9f, 0.9f), (creature.Personality.Courage, creature.Personality.Jealousy));
        Assert.StartsWith(creature.Name + " is brave", said, StringComparison.Ordinal);
    }

    [Fact]
    public void ACreatureDefendingByItself_IsAnnounced_ToItsPlayerOnly()
    {
        var world = _session.World;
        var mine = _session.LocalCreature!;
        var theirs = world.Creatures.First(c => c.Id != mine.Id);
        var village = world.Settlements[0];

        var alerts = new AlertFeed().Push(
            [
                new CreatureDefending { Tick = 1, CreatureId = mine.Id, SettlementId = village.Id },
                new CreatureDefending { Tick = 1, CreatureId = theirs.Id, SettlementId = village.Id },
            ],
            world,
            _session.LocalGodId,
            1.0);

        var alert = Assert.Single(alerts);
        Assert.Equal($"{mine.Name} goes by itself to defend {village.Name} from the beast.", alert.Text);
        Assert.Equal(AlertSeverity.Good, alert.Severity);
    }
}
