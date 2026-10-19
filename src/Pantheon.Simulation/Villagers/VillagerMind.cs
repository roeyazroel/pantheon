using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Villagers;

/// <summary>Something a villager could do next, how much they want it and why.</summary>
/// <param name="Activity">What.</param>
/// <param name="Score">How much they want it.</param>
/// <param name="Reason">Why, in a few words.</param>
public readonly record struct VillagerOption(VillagerActivity Activity, float Score, string Reason);

/// <summary>
/// How a followed villager chooses what to do (spec §80's AI, ADR-043): by their needs, their work,
/// their faith and the hardships they remember. Pure, so the debug view shows the same choice.
/// </summary>
public static class VillagerMind
{
    // How much more a hungry villager wants to eat than a tired one to rest.
    private const float HungerWeight = 1.2f;

    /// <summary>What a villager could do next, the most wanted first.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="villager">The villager.</param>
    /// <returns>Eating, resting, working and praying, scored.</returns>
    public static IReadOnlyList<VillagerOption> Options(VillagerRules rules, WorldState world, VillagerState villager)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(villager);
        var home = world.Settlements.Find(s => s.Id == villager.SettlementId);
        var fed = home is { Food: >= 1f };
        var hardship = VillagerModel.Remembers(villager, world.Tick - (long)(rules.HardshipSeconds * SimulationTime.TicksPerSecond),
            VillagerDeed.Survived, VillagerDeed.Hungered, VillagerDeed.Fled, VillagerDeed.FellIll);
        var sincePrayer = (world.Tick - villager.LastWorshipTick) * SimulationTime.SecondsPerTick;
        var longing = Math.Clamp(sincePrayer / Math.Max(1f, rules.WorshipGapSeconds), 0f, 1f);
        var options = new List<VillagerOption>(4)
        {
            new(VillagerActivity.Eating, fed ? HungerWeight * villager.Hunger : 0f, fed ? Format("hunger {0:0.00}", villager.Hunger) : Format("hunger {0:0.00}, no food in store", villager.Hunger)),
            new(VillagerActivity.Resting, villager.Fatigue, Format("fatigue {0:0.00}", villager.Fatigue)),
            new(VillagerActivity.Working, rules.WorkDrive * (1f - villager.Fatigue) * (1f - (0.5f * villager.Hunger)), Format("strength {0:0.00}", 1f - villager.Fatigue)),
            villager.GodId is null
                ? new(VillagerActivity.Worshipping, 0f, "no god")
                : new(VillagerActivity.Worshipping, (rules.WorshipDrive * villager.Faith * longing) + (hardship ? rules.HardshipWorship : 0f),
                    Format("faith {0:0.00}{1}{2}", villager.Faith, longing < 1f ? Format(", prayed {0:0}s ago", sincePrayer) : string.Empty, hardship ? ", hardship remembered" : string.Empty)),
        };
        options.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Activity.CompareTo(b.Activity));
        return options;
    }

    /// <summary>Where an activity is done.</summary>
    /// <param name="villager">The villager.</param>
    /// <param name="activity">One of the daily activities.</param>
    /// <returns>Their home, work or shrine.</returns>
    public static GroundPosition Place(VillagerState villager, VillagerActivity activity)
    {
        ArgumentNullException.ThrowIfNull(villager);
        return activity switch
        {
            VillagerActivity.Working => villager.WorkPlace,
            VillagerActivity.Worshipping => villager.Shrine,
            VillagerActivity.Resting or VillagerActivity.Eating or VillagerActivity.Fleeing or VillagerActivity.Sheltering or VillagerActivity.Seeking => villager.Home,
        };
    }

    /// <summary>How long an activity lasts once there.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="activity">The activity.</param>
    /// <returns>Seconds.</returns>
    public static float Seconds(VillagerRules rules, VillagerActivity activity)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return activity switch
        {
            VillagerActivity.Working => rules.WorkSeconds,
            VillagerActivity.Eating => rules.EatSeconds,
            VillagerActivity.Worshipping => rules.WorshipSeconds,
            VillagerActivity.Resting or VillagerActivity.Fleeing or VillagerActivity.Sheltering or VillagerActivity.Seeking => rules.RestSeconds,
        };
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
