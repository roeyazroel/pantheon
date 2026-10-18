using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Creatures;

/// <summary>Creates a god's Creature at its home settlement (spec §22: every god receives exactly one).</summary>
public static class CreatureFounder
{
    // A Creature appears this far east of its home's centre, clear of the shrine.
    private const float SpawnOffset = 40f;

    /// <summary>Creates a young Creature with a personality drawn from the world seed and the god's id.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="god">Its god; the Creature takes the god's id as its own.</param>
    /// <param name="name">Its name.</param>
    /// <param name="home">Its home settlement.</param>
    /// <param name="grid">Region grid, to place it on land.</param>
    /// <returns>The Creature.</returns>
    public static CreatureState Found(CreatureRules rules, WorldSeed seed, GodState god, string name, SettlementState home, RegionGrid grid)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(home);
        var random = new SimulationRandom(SeedMixer.Derive(seed, "creature.personality." + god.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        float Trait() => SimMath.Clamp01(0.5f + (rules.PersonalitySpread * ((2f * random.NextFloat()) - 1f)));
        var preferences = new List<LearnedPreference>(rules.StartingPreferences.Count);
        foreach (var seedPreference in rules.StartingPreferences)
        {
            preferences.Add(new LearnedPreference { Situation = seedPreference.Situation, Action = seedPreference.Action, Weight = seedPreference.Weight });
        }

        var position = SpawnPoint(grid, home.Position);
        return new CreatureState
        {
            Id = god.Id,
            GodId = god.Id,
            Name = name,
            Species = rules.Species,
            Position = position,
            Heading = 0f,
            Level = 1,
            Experience = 0f,
            Health = 1f,
            Loyalty = rules.StartingLoyalty,
            Morality = 0f,
            Needs = new CreatureNeeds { Hunger = 0.2f, Fatigue = 0.1f, Boredom = 0.3f },
            Personality = new CreaturePersonality { Kindness = Trait(), Aggression = Trait(), Laziness = Trait(), Curiosity = Trait() },
            Activity = new CreatureActivity
            {
                Action = CreatureAction.Rest,
                Situation = CreatureSituation.Idle,
                Phase = CreaturePhase.Deciding,
                TargetSettlementId = null,
                Target = position,
                Path = [],
                TicksLeft = 0,
            },
            Preferences = preferences,
            Memories = [],
            HomeSettlementId = home.Id,
        };
    }

    /// <summary>A land point beside a settlement: east of it when that is land, else its centre.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="center">The settlement's centre.</param>
    /// <returns>The point.</returns>
    public static GroundPosition SpawnPoint(RegionGrid grid, GroundPosition center)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var east = new GroundPosition(center.X + SpawnOffset, center.Z);
        return grid.TryGetRegionIdAt(east.X, east.Z, out var region) && RegionPathfinder.IsWalkable(grid.Cells[region]) ? east : center;
    }
}
