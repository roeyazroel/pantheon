using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>A leg of a representative's round.</summary>
public enum CitizenLeg
{
    /// <summary>From home to work.</summary>
    ToWork,

    /// <summary>From work to the shrine.</summary>
    ToGathering,

    /// <summary>From the shrine home.</summary>
    ToHome,
}

/// <summary>The ways a crowd's figures walk round what stands in their way (spec §85, ADR-046).</summary>
public interface ICitizenWays
{
    /// <summary>The points a figure passes on a leg, after its start and ending at its end, or <c>null</c> to walk straight.</summary>
    /// <param name="index">The figure's index in its roster.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="from">Where the leg starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The waypoints, or <c>null</c>.</returns>
    IReadOnlyList<Vector2>? Way(int index, CitizenLeg leg, Vector2 from, Vector2 to);
}
