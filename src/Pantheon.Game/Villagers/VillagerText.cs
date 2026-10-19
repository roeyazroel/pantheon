using System;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Society;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Disasters;
using Pantheon.Game.Society;
using Pantheon.Simulation.Villagers;

namespace Pantheon.Game.Villagers;

/// <summary>Words for the villagers the gods follow (ADR-043). Pure and culture-invariant; they never assume a villager's pronouns.</summary>
public static class VillagerText
{
    /// <summary>A villager's work as a noun.</summary>
    /// <param name="work">The work.</param>
    /// <returns>For example "farmer".</returns>
    public static string Worker(VillagerWork work) => work switch
    {
        VillagerWork.Farming => "farmer",
        VillagerWork.Building => "builder",
        VillagerWork.Chores => "villager",
    };

    /// <summary>A villager's title.</summary>
    /// <param name="rules">Villager rules, for their age.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "Mara, farmer of Elar, 34".</returns>
    public static string Title(VillagerRules rules, VillagerState villager, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(world);
        return Format("{0}, {1} of {2}, {3:0}", villager.Name, Worker(villager.Work), Village(world, villager.SettlementId), Math.Floor(VillagerModel.Age(rules, villager, world.Tick)));
    }

    /// <summary>What a villager is doing.</summary>
    /// <param name="villager">The villager.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "walking to the fields" or "praying to Ilyra at the shrine".</returns>
    public static string Doing(VillagerState villager, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(world);
        var walking = villager.Path.Count > 0;
        var refuge = villager.RefugeId is { } id ? Village(world, id) : null;
        return villager.Activity switch
        {
            VillagerActivity.Working when walking => "walking to " + villager.Work switch
            {
                VillagerWork.Farming => "the fields",
                VillagerWork.Building => "the building site",
                VillagerWork.Chores => "their chores",
            },
            VillagerActivity.Working => villager.Work switch
            {
                VillagerWork.Farming => "working in the fields",
                VillagerWork.Building => "building",
                VillagerWork.Chores => "doing chores",
            },
            VillagerActivity.Eating => walking ? "walking home to eat" : "eating at home",
            VillagerActivity.Resting => walking ? "walking home to rest" : "resting at home",
            VillagerActivity.Worshipping when walking => "walking to the shrine",
            VillagerActivity.Worshipping => villager.GodId is { } god ? Format("praying to {0} at the shrine", God(world, god)) : "praying at the shrine",
            VillagerActivity.Fleeing => refuge is null ? "fleeing the village" : "fleeing to " + refuge,
            VillagerActivity.Sheltering => refuge is null ? "waiting out the danger" : "sheltering in " + refuge,
            VillagerActivity.Seeking => refuge is null ? "seeking a new home" : "seeking a new home in " + refuge,
        };
    }

    /// <summary>A villager's faith.</summary>
    /// <param name="villager">The villager.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "follows Ilyra (faith 62%)" or "follows no god".</returns>
    public static string Faith(VillagerState villager, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(world);
        return villager.GodId is { } god ? Format("follows {0} (faith {1:0%})", God(world, god), villager.Faith) : "follows no god";
    }

    /// <summary>The status line when a god begins to follow a villager.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "You follow Mara, a farmer of Elar, aged 34, who follows Ilyra. …".</returns>
    public static string Followed(VillagerRules rules, VillagerState villager, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(villager);
        return Format("You follow {0}, a {1} of {2}, aged {3:0}, who {4}. Tab shows their life; / with nobody at the cursor looks at them, Shift+/ lets them go",
            villager.Name, Worker(villager.Work), Village(world, villager.SettlementId), Math.Floor(VillagerModel.Age(rules, villager, world.Tick)), Faith(villager, world));
    }

    /// <summary>One thing a villager remembers.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="villager">The villager, for their work.</param>
    /// <param name="world">World state.</param>
    /// <param name="miracle">A miracle's name by its id; the id when <c>null</c>.</param>
    /// <returns>For example "fled the fire in Elar to Brightwater".</returns>
    public static string Memory(VillagerMemory memory, VillagerState villager, WorldState world, Func<string, string>? miracle = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(world);
        var place = memory.SettlementId is { } id ? Village(world, id) : "the wilds";
        var god = memory.GodId is { } godId ? God(world, godId) : "a god";
        var deaths = memory.Count > 0 ? Format(" ({0} died)", memory.Count) : string.Empty;
        return memory.Deed switch
        {
            VillagerDeed.Followed => Format("first noticed by {0} in {1}", god, place),
            VillagerDeed.SawMiracle => Format("saw {0}'s {1}{2}", god, (miracle ?? (m => m))(memory.Detail), memory.Count > 0 ? Format(", which killed {0}", memory.Count) : string.Empty),
            VillagerDeed.Survived => Format("lived through {0} in {1}{2}", Danger(memory.Detail), place, deaths),
            VillagerDeed.Hungered => string.Equals(memory.Detail, "thirst", StringComparison.Ordinal) ? "went thirsty in " + place : "went hungry in " + place,
            VillagerDeed.Fled => Format("fled {0} in {1}{2}", Danger(memory.Detail), place, memory.OtherId is { } refuge ? " to " + Village(world, refuge) : string.Empty),
            VillagerDeed.Returned => "came home to " + place,
            VillagerDeed.Moved => "found a new home in " + place,
            VillagerDeed.Turned => memory.OtherId is { } from ? Format("turned from {0} to {1}", God(world, from), god) : "came to believe in " + god,
            VillagerDeed.SawCreature => Format("saw {0} {1}{2}", world.Creatures.Find(c => c.Id == memory.OtherId)?.Name ?? "a Creature",
                Enum.TryParse<CreatureAction>(memory.Detail, out var action) ? CreatureText.Doing(action, place) : "in " + place, deaths),
            VillagerDeed.Built => Format(villager.Work == VillagerWork.Building ? "helped raise the {0} of {1}" : "saw the {0} of {1} raised",
                Enum.TryParse<BuildingKind>(memory.Detail, out var building) ? SocietyText.Name(building) : memory.Detail, place),
            VillagerDeed.HeardPreaching => Format("heard {0} preach for {1} in {2}", world.Notables.Find(n => n.Id == memory.OtherId)?.Name ?? "a prophet", god, place),
            VillagerDeed.Offering => string.Equals(memory.Detail, "gave", StringComparison.Ordinal) ? Format("gave to an offering to {0}", god) : Format("shared in an offering from {0}", god),
            VillagerDeed.FellIll => "fell ill with the plague in " + place,
        };
    }

    /// <summary>How a villager died.</summary>
    /// <param name="fate">What took them.</param>
    /// <returns>For example "of old age" or "in the fire".</returns>
    public static string Fate(VillagerFate fate) => fate switch
    {
        VillagerFate.OldAge => "of old age",
        VillagerFate.Hunger => "of hunger",
        VillagerFate.Illness => "of illness",
        VillagerFate.Fire => "in the fire",
        VillagerFate.Flood => "in the flood",
        VillagerFate.Earthquake => "in the earthquake",
    };

    /// <summary>A danger a villager fled or lived through.</summary>
    /// <param name="cause">The danger.</param>
    /// <returns>For example "the fire".</returns>
    public static string Danger(VillagerFate cause) => cause switch
    {
        VillagerFate.Fire => "the fire",
        VillagerFate.Flood => "the flood",
        VillagerFate.Earthquake => "the earthquake",
        VillagerFate.OldAge or VillagerFate.Hunger or VillagerFate.Illness => "danger",
    };

    /// <summary>An activity as a verb, for the options.</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>For example "eat".</returns>
    public static string Verb(VillagerActivity activity) => activity switch
    {
        VillagerActivity.Resting => "rest",
        VillagerActivity.Eating => "eat",
        VillagerActivity.Working => "work",
        VillagerActivity.Worshipping => "pray",
        VillagerActivity.Fleeing => "flee",
        VillagerActivity.Sheltering => "shelter",
        VillagerActivity.Seeking => "seek a home",
    };

    /// <summary>Why a villager could not be followed or let go.</summary>
    /// <param name="rejection">The rejection.</param>
    /// <returns>The reason, or empty when none.</returns>
    public static string Describe(VillagerRejection rejection) => rejection switch
    {
        VillagerRejection.None => string.Empty,
        VillagerRejection.NoVillagers => "Villagers cannot be followed in this world",
        VillagerRejection.UnknownGod => "You have no god to follow them",
        VillagerRejection.UnknownSettlement => "Nobody lives there",
        VillagerRejection.TooFar => "Point at someone in a village to follow them",
        VillagerRejection.NotFollowing => "You follow no villager",
    };

    /// <summary>A village's name.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlementId">The village.</param>
    /// <returns>Its name, or "a lost village".</returns>
    public static string Village(WorldState world, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Settlements.Find(s => s.Id == settlementId)?.Name ?? "a lost village";
    }

    /// <summary>A god's name.</summary>
    internal static string God(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>A danger remembered by its stored name: a villager's danger, a disaster, or a storm.</summary>
    private static string Danger(string detail) =>
        Enum.TryParse<VillagerFate>(detail, out var fate) && Enum.IsDefined(fate) ? Danger(fate)
        : Enum.TryParse<DisasterKind>(detail, out var kind) && Enum.IsDefined(kind) ? DisasterText.Kind(kind)
        : "the " + detail.ToLowerInvariant();

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
