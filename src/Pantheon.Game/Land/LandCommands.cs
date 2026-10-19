using System;
using System.Globalization;
using Pantheon.Domain.Land;
using Pantheon.Domain.World;
using Pantheon.Simulation.Land;

namespace Pantheon.Game.Land;

/// <summary>
/// The debug driver of the Divine Acts that reshape the land (ADR-045): <c>--reshape</c> finds
/// where an island, a gorge or a river's new course can be made near a point and makes it there at
/// once, as the act would, so captures and tests need no rank or power.
/// </summary>
public static class LandCommands
{
    // How far, in metres, from the point a place is sought, and in what steps.
    private const float SearchReach = 2000f;
    private const float SearchStep = 50f;
    private const int SearchDirections = 16;

    /// <summary>The nearest place to a point where an act can be done.</summary>
    /// <param name="session">The session.</param>
    /// <param name="kind">The reshaping.</param>
    /// <param name="near">The point.</param>
    /// <returns>Where to point, or <c>null</c> when nowhere near will do.</returns>
    public static GroundPosition? Suggest(GameSession session, ReshapeKind kind, GroundPosition near)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rules = session.Content.DivineRules.Land;
        for (var radius = SearchStep; radius <= SearchReach; radius += SearchStep)
        {
            for (var i = 0; i < SearchDirections; i++)
            {
                var angle = MathF.Tau * i / SearchDirections;
                var at = new GroundPosition(near.X + (MathF.Cos(angle) * radius), near.Z + (MathF.Sin(angle) * radius));
                if (LandShaper.Plan(rules, session.World, session.Terrain, kind, at, session.LocalGodId ?? 0) is not null)
                {
                    return at;
                }
            }
        }

        return null;
    }

    /// <summary>Makes a reshaping near a point at once, as the local god's act, for captures and tests.</summary>
    /// <param name="session">The session.</param>
    /// <param name="kind">The reshaping.</param>
    /// <param name="near">The point.</param>
    /// <returns>Where it was made and the status line.</returns>
    public static (GroundPosition? Where, string Text) Reshape(GameSession session, ReshapeKind kind, GroundPosition near)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return (null, refusal);
        }

        if (Suggest(session, kind, near) is not { } at)
        {
            return (null, string.Format(CultureInfo.InvariantCulture, "Nowhere within {0:0} m to make {1}", SearchReach, Name(kind)));
        }

        var god = session.LocalGodId ?? 0;
        session.Command((authorities, events) =>
        {
            LandActs.Act(session.Content.DivineRules.Land, authorities.Miracles.Land, session.World, god, kind, at, events);
            return true;
        });
        return (at, string.Format(CultureInfo.InvariantCulture, "{0} made at ({1:0}, {2:0})", Capitalised(Name(kind)), at.X, at.Z));
    }

    /// <summary>A reshaping's name.</summary>
    /// <param name="kind">The reshaping.</param>
    /// <returns>For example "an island".</returns>
    public static string Name(ReshapeKind kind) => kind switch
    {
        ReshapeKind.Island => "an island",
        ReshapeKind.Gorge => "a gorge through a mountain",
        ReshapeKind.Channel => "a river's new course",
    };

    /// <summary>A phrase with its first letter in capitals.</summary>
    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
