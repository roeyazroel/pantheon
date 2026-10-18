using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Where everything of one settlement stands, from <see cref="SettlementLayout.ComputeSite"/>:
/// the shrine at the centre, the houses, the house under construction and the fields.
/// </summary>
/// <param name="Center">Ground position of the shrine.</param>
/// <param name="Houses">Completed houses, in the order they were built.</param>
/// <param name="Construction">The house under construction, or <c>null</c> when none is under way.</param>
/// <param name="Fields">Field plots.</param>
/// <param name="HouseDiscRadius">Distance from the centre to the farthest house or building site centre, in metres.</param>
public sealed record SettlementSite(
    Vector3 Center,
    IReadOnlyList<HutPlacement> Houses,
    ConstructionPlacement? Construction,
    IReadOnlyList<FieldPlacement> Fields,
    float HouseDiscRadius)
{
    /// <summary>The spot just outside a house's door, where its residents come and go.</summary>
    /// <param name="house">House index.</param>
    /// <param name="doorDistance">Distance from the house centre in metres.</param>
    /// <returns>Ground (X, Z) point in front of the door, towards the shrine.</returns>
    public Vector2 DoorOf(int house, float doorDistance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(house);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(house, Houses.Count);
        var placement = Houses[house];
        return new Vector2(placement.Position.X, placement.Position.Z) + (Facing(placement.Yaw) * doorDistance);
    }

    /// <summary>Ground direction of a placement's local +Z axis (a house's door side) after a rotation of <paramref name="yaw"/> about the vertical axis.</summary>
    /// <param name="yaw">Rotation around the vertical axis in radians.</param>
    /// <returns>Unit (X, Z) direction: (sin yaw, cos yaw).</returns>
    public static Vector2 Facing(float yaw) => new(MathF.Sin(yaw), MathF.Cos(yaw));
}
