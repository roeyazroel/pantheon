using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;

namespace Pantheon.Tests.Support;

/// <summary>Divine worlds with a player god and an AI rival, for rival tests.</summary>
internal static class RivalWorlds
{
    /// <summary>Id of the rival god.</summary>
    public const int RivalId = 2;

    /// <summary>The shipped god AI rules.</summary>
    public static GodAiRules AiRules => TestContent.Shipped.GodAi;

    /// <summary>
    /// A <see cref="DivineWorlds"/> world with settlements at the given x positions and an AI rival of
    /// the given personality with <paramref name="rivalPower"/> Divine Power. Nobody believes in the rival yet.
    /// </summary>
    /// <param name="personality">The rival's personality.</param>
    /// <param name="rivalPower">The rival's Divine Power.</param>
    /// <param name="positionsX">X positions of the settlements.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(GodPersonality personality, float rivalPower, params float[] positionsX)
    {
        var world = DivineWorlds.Create(100f, positionsX);
        world.Gods.Add(DivineWorlds.God(RivalId, "Rivalis", GodController.Ai, personality, rivalPower));
        return world;
    }

    /// <summary>A god AI over the pinned divine rules and the shipped miracles, without Creatures.</summary>
    /// <returns>The AI.</returns>
    public static GodAi CreateAi()
    {
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);
        return new GodAi(AiRules, DivineWorlds.Rules, DivineWorlds.Miracles, caster, null, null);
    }
}
