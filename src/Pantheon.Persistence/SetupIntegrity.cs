using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the choices a world was made with in a loaded save (ADR-082): its setup, its victory's hold and its gods' colours.</summary>
internal static class SetupIntegrity
{
    /// <summary>Records every inconsistency of the world's choices.</summary>
    /// <param name="world">The world.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, List<string> errors)
    {
        if (world.Setup is not { } setup || !Enum.IsDefined(setup.Calamities) || !Enum.IsDefined(setup.Rivals) || !Enum.IsDefined(setup.Temper) || !Enum.IsDefined(setup.Villages))
        {
            errors.Add("The world's setup is missing or names an unknown calamity level, rival strength, temper or village count.");
        }

        if (world.Victory is null || world.Victory.HoldSeconds is { } hold && !(float.IsFinite(hold) && hold > 0f))
        {
            errors.Add($"The world's victory settings are missing, or hold a victory for {world.Victory?.HoldSeconds} seconds; it must be a positive number.");
        }

        var colors = new HashSet<int>();
        foreach (var god in world.Gods)
        {
            if (god.Color is { } color && (color < 0 || !colors.Add(color)))
            {
                errors.Add($"God {god.Id} holds colour {color}, below 0 or another god's.");
            }
        }
    }
}
