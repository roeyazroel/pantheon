using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>
/// One Tier 2 figure standing for several inhabitants: its role and the spots its routine visits.
/// Ground positions are world (X, Z) coordinates.
/// </summary>
/// <param name="Index">Stable identity within the settlement's roster, 0 .. count − 1.</param>
/// <param name="Role">Role from the labour split.</param>
/// <param name="House">Index of its house in the site's house list, or −1 when the settlement has no houses.</param>
/// <param name="Home">Spot in front of its door where it rests.</param>
/// <param name="Work">Spot where it works.</param>
/// <param name="WorkYaw">Facing while working: along the field, towards the building site, or any way for chores.</param>
/// <param name="Gathering">Spot around the shrine where it gathers.</param>
/// <param name="GatherYaw">Facing while gathering: towards the shrine.</param>
/// <param name="Phase">Offset into its routine as a share of the cycle, in [0, 1), so neighbours do not move in step.</param>
public readonly record struct RepresentativeCitizen(
    int Index,
    CitizenRole Role,
    int House,
    Vector2 Home,
    Vector2 Work,
    float WorkYaw,
    Vector2 Gathering,
    float GatherYaw,
    float Phase);
