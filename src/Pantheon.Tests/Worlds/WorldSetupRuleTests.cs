using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>The rules a world's setup goes on changing (ADR-082): how often calamities begin, how its AI gods weigh waiting and chance, and how long its victory is held.</summary>
public sealed class WorldSetupRuleTests
{
    private static readonly DisasterRules Quakes = DisasterWorlds.Rules with
    {
        EarthquakesPerHour = 20f,
        HearthFirePerSecond = 0f,
        Calamities = new CalamityScales { Gentle = 0f, Normal = 1f, Harsh = 3f },
    };

    [Fact]
    public void TheShippedNormalAndEvenChoices_KeepTheContentsRates()
    {
        var content = TestContent.Shipped;

        Assert.Equal(1f, content.Disasters.Calamities.Normal);
        Assert.True(content.Disasters.Calamities.Gentle < 1f && content.Disasters.Calamities.Harsh > 1f);
        Assert.Equal(RivalStrengthRules.Same, content.GodAi.Strengths.Even);
        Assert.True(content.GodAi.Strengths.Weak.Power < 1f && content.GodAi.Strengths.Strong.Power > 1f);
    }

    [Fact]
    public void Calamities_BeginAsOftenAsTheWorldsLevelScalesThem()
    {
        var counts = new[] { CalamityLevel.Gentle, CalamityLevel.Normal, CalamityLevel.Harsh }.Select(QuakesIn).ToList();

        Assert.Equal(0, counts[0]);
        Assert.InRange(counts[1], 10, 32);
        Assert.True(counts[2] > 2 * counts[1], $"harsh {counts[2]}, normal {counts[1]}");
    }

    [Fact]
    public void AWorldMadeBeforeTheChoices_StrikesAsItAlwaysDid()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var made = DivineWorlds.Create(0f, 300f);
        made.Setup = WorldSetup.Default with { Calamities = CalamityLevel.Normal };

        Assert.Equal(1f, DisasterModel.CalamityScale(Quakes.Calamities, world));
        Assert.Equal(QuakesIn(CalamityLevel.Normal), Count(made, DisasterWorlds.Rules with { EarthquakesPerHour = 20f, HearthFirePerSecond = 0f }));
    }

    [Theory]
    [InlineData(RivalStrength.Weak)]
    [InlineData(RivalStrength.Even)]
    [InlineData(RivalStrength.Strong)]
    public void ARivalsStrength_ScalesHowMuchItValuesWaiting_AndNeverThePlayers(RivalStrength strength)
    {
        var world = RivalWorlds.Create(GodPersonality.Guardian, 50f, 0f, 300f);
        world.Setup = world.Setup with { Rivals = strength };
        var ai = RivalWorlds.CreateAi();
        var rules = RivalWorlds.AiRules;

        var rival = ai.Evaluate(world, world.Gods[1]).Single(c => c.Kind == AiActionKind.Wait);
        var player = ai.Evaluate(world, world.Gods[0], PantheonWorlds.Profile(GodPersonality.Guardian)).Single(c => c.Kind == AiActionKind.Wait);

        Assert.Equal(rules.Patience * rules.Strengths.Of(strength).Patience, rival.Utility);
        Assert.Equal(rules.Patience, player.Utility);
    }

    [Fact]
    public void AVictory_IsWonOnlyOnceTheWorldsHoldHasPassed()
    {
        var world = PantheonWorlds.Create(0.9f, 0.02f, 0.02f, 0.02f);
        world.Victory = VictorySettings.Default with { HoldSeconds = 2f * DivineWorlds.Rules.VictorySeconds };
        var events = new List<WorldEvent>();

        Run(world, events);
        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);
        Assert.Empty(world.WinnerGodIds);

        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules, world);
        Run(world, events);
        Assert.Equal([1], world.WinnerGodIds);
        Assert.Equal(2 * VictorySystem.TicksToWin(DivineWorlds.Rules), VictorySystem.TicksToWin(DivineWorlds.Rules, world));
        Assert.Equal(DivineWorlds.Rules.VictorySeconds, VictorySystem.HoldSeconds(DivineWorlds.Rules, VictorySettings.Default));
    }

    /// <summary>The natural earthquakes of an hour in a world of a calamity level.</summary>
    private static int QuakesIn(CalamityLevel level)
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Setup = world.Setup with { Calamities = level };
        return Count(world, Quakes);
    }

    /// <summary>The natural earthquakes of an hour of disaster steps.</summary>
    private static int Count(WorldState world, DisasterRules rules)
    {
        var system = new DisasterSystem(rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules);
        var events = new List<WorldEvent>();
        for (var i = 0; i < 3600; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += rules.StepIntervalTicks;
        }

        return events.OfType<EarthquakeStruck>().Count();
    }

    private static void Run(WorldState world, List<WorldEvent> events) =>
        new VictorySystem(DivineWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), events));
}
