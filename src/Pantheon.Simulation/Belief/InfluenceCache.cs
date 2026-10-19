using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// The gods' influence fields over one unchanging world, each built once (ADR-027): for weighing
/// many casts in a row without casting, as the AI does. Never keep one across a change of belief.
/// </summary>
public sealed class InfluenceCache
{
    private readonly DivineRules _rules;
    private readonly WorldState _world;
    private readonly Dictionary<int, InfluenceField> _fields = [];

    /// <summary>Creates an empty cache over a world.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="world">The world, which must not change while the cache is used.</param>
    public InfluenceCache(DivineRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        _rules = rules;
        _world = world;
    }

    /// <summary>The world the cache is over.</summary>
    public WorldState World => _world;

    /// <summary>A god's influence field, built the first time it is asked for.</summary>
    /// <param name="godId">The god.</param>
    /// <returns>The field.</returns>
    public InfluenceField For(int godId)
    {
        if (!_fields.TryGetValue(godId, out var field))
        {
            field = _fields[godId] = InfluenceField.Build(_rules, _world, godId);
        }

        return field;
    }
}
