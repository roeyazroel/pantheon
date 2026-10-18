using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;

namespace Pantheon.Tests.Support;

/// <summary>Divine worlds with a player and three AI gods, for pantheon and politics tests.</summary>
internal static class PantheonWorlds
{
    /// <summary>
    /// A <see cref="DivineWorlds"/> world of four settlements of 100 people and four gods: the player
    /// (1, Testa), a Tyrant (2, Rivalis), a Guardian (3, Tertia) and a Trickster (4, Quarta), each with
    /// 500 Divine Power. God <c>i + 1</c> is followed in settlement <c>i</c> by the given share.
    /// </summary>
    /// <param name="adherence">Each god's share of its own settlement, in god order.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(params float[] adherence)
    {
        var world = DivineWorlds.Create(500f, 200f, 450f, 700f, 950f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        world.Gods.Add(DivineWorlds.God(3, "Tertia", GodController.Ai, GodPersonality.Guardian, 500f));
        world.Gods.Add(DivineWorlds.God(4, "Quarta", GodController.Ai, GodPersonality.Trickster, 500f));
        for (var i = 0; i < adherence.Length; i++)
        {
            if (adherence[i] > 0f)
            {
                DivineWorlds.Believe(world.Settlements[i], adherence[i], faith: 0.8f, godId: i + 1);
            }
        }

        return world;
    }

    /// <summary>Sets the status of a pair, as if it had begun at tick 0.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <param name="status">Status.</param>
    public static void SetStatus(WorldState world, int a, int b, DiplomaticStatus status)
    {
        var stance = DiplomacyModel.EnsureStance(world, a, b);
        stance.Status = status;
        stance.SinceTick = 0;
    }

    /// <summary>The shipped profile of a personality.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>Its profile.</returns>
    public static GodProfile Profile(GodPersonality personality) => RivalWorlds.AiRules.Profiles.Single(p => p.Personality == personality);

    /// <summary>A diplomat over the pinned diplomacy and divine rules.</summary>
    /// <param name="divine">Divine rules; the pinned ones when <c>null</c>.</param>
    /// <returns>The diplomat.</returns>
    public static GodDiplomat Diplomat(Pantheon.Domain.Belief.DivineRules? divine = null) =>
        new(DiplomacyWorlds.Rules, divine ?? DivineWorlds.Rules, DiplomacyWorlds.Authority());

    /// <summary>The utility of one move of an AI god towards another god.</summary>
    /// <param name="diplomat">The diplomat.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The AI god.</param>
    /// <param name="personality">The personality it weighs the move with.</param>
    /// <param name="kind">The move.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <returns>The candidate.</returns>
    public static DiplomacyCandidate Move(GodDiplomat diplomat, WorldState world, int godId, GodPersonality personality, DiplomaticCommandKind kind, int otherGodId) =>
        diplomat.Evaluate(world, world.Gods.Single(g => g.Id == godId), Profile(personality)).Single(c => c.Kind == kind && c.OtherGodId == otherGodId);
}
