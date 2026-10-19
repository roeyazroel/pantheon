using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Deterministic visual layout of a settlement. Houses stand on a sunflower spiral around the
/// shrine, one per <see cref="SettlementRules.HousingPerHouse"/> of housing; house <c>k</c> always
/// takes the <c>k</c>-th dry slot, so growth only adds houses. The house under construction takes
/// the next slot. Fields lie on a second spiral outside the houses, so as the village grows over
/// its inner fields, new ones appear further out.
/// </summary>
public static class SettlementLayout
{
    // Spiral slots tried per house or field; slots on low or steep ground are skipped.
    private const int AttemptsPerHouse = 3;
    private const int AttemptsPerField = 4;

    // Houses keep this far, in metres, from a river or lake (ADR-022).
    private const float RiverClearance = 4f;

    private const float MinimumHouseScale = 0.85f;
    private const float HouseScaleRange = 0.3f;
    private const float HouseYawJitter = 0.35f;
    private const float FieldYawJitter = 0.3f;

    // A square field turned to any yaw reaches this far from its centre, as a share of its side.
    private const float HalfDiagonal = 0.7072f;

    // Hash salts, so houses and fields draw unrelated values from the same slot numbers.
    private const ulong HouseSalt = 1;
    private const ulong FieldVariantSalt = 2;
    private const ulong FieldYawSalt = 3;

    private static readonly float GoldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));

    /// <summary>Number of houses shown for the settlement's housing.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="rules">Settlement rules, for the housing per house.</param>
    /// <returns><c>ceil(Housing / HousingPerHouse)</c>.</returns>
    public static int ComputeHouseCount(SettlementState settlement, SettlementRules rules)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return SettlementEconomy.ComputeHouseCount(rules, settlement.Housing);
    }

    /// <summary>Number of fields for a number of houses: one per <see cref="SettlementLayoutSettings.HousesPerField"/>, up to the maximum.</summary>
    /// <param name="houses">Number of houses.</param>
    /// <param name="settings">Layout settings.</param>
    /// <returns>Field count.</returns>
    public static int ComputeFieldCount(int houses, SettlementLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return houses <= 0 ? 0 : Math.Min(settings.MaxFields, ((houses - 1) / settings.HousesPerField) + 1);
    }

    /// <summary>Lays out the settlement: houses, the house under construction and fields.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="rules">Settlement rules, for the house count and the work per house.</param>
    /// <param name="terrain">Terrain used for heights and slopes.</param>
    /// <param name="settings">Layout settings.</param>
    /// <returns>The site; the same for the same inputs. Houses or fields may be fewer than asked where the land runs out.</returns>
    public static SettlementSite ComputeSite(SettlementState settlement, SettlementRules rules, TerrainField terrain, SettlementLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        var center = new Vector2(settlement.Position.X, settlement.Position.Z);
        var houseCount = ComputeHouseCount(settlement, rules);
        var underWay = settlement.ConstructionProgress > 0f;
        var ruinCount = Math.Max(0, settlement.Ruins);
        var slots = ComputeHouseSlots(settlement.Id, center, terrain, settings, houseCount + (underWay ? 1 : 0) + ruinCount);

        var houses = slots.GetRange(0, Math.Min(houseCount, slots.Count));
        ConstructionPlacement? construction = underWay && slots.Count > houseCount
            ? new ConstructionPlacement(slots[houseCount], Math.Clamp(settlement.ConstructionProgress / SettlementEconomy.ComputeHouseWork(rules, settlement), 0f, 1f))
            : null;
        var ruinsStart = Math.Min(slots.Count, houseCount + (underWay ? 1 : 0));
        var ruins = slots.GetRange(ruinsStart, Math.Min(ruinCount, slots.Count - ruinsStart));
        var discRadius = ComputeDiscRadius(center, slots, settings.ShrineClearance);
        var fields = ComputeFields(settlement.Id, center, terrain, settings, discRadius, ComputeFieldCount(houseCount, settings));

        return new SettlementSite(new Vector3(center.X, terrain.SampleHeight(center.X, center.Y), center.Y), houses, construction, fields, discRadius, ruins);
    }

    /// <summary>The first <paramref name="count"/> dry slots of the house spiral, each facing the shrine with a little jitter.</summary>
    private static List<HutPlacement> ComputeHouseSlots(int settlementId, Vector2 center, TerrainField terrain, SettlementLayoutSettings settings, int count)
    {
        var slots = new List<HutPlacement>(count);
        for (var slot = 0; slots.Count < count && slot < count * AttemptsPerHouse; slot++)
        {
            var radius = settings.ShrineClearance + (settings.HouseSpacing * MathF.Sqrt(slot + 0.5f));
            var ground = SpiralPoint(center, slot, radius);
            var height = terrain.SampleHeight(ground.X, ground.Y);
            if (height < settings.MinimumGroundHeight || terrain.IsNearFreshWater(ground.X, ground.Y, RiverClearance))
            {
                continue;
            }

            var jitter = PresentationHash.Unit(HouseSalt, (ulong)settlementId, (ulong)slot);
            var yaw = FacingYaw(ground, center) + ((jitter - 0.5f) * HouseYawJitter);
            slots.Add(new HutPlacement(new Vector3(ground.X, height, ground.Y), yaw, MinimumHouseScale + (HouseScaleRange * jitter)));
        }

        return slots;
    }

    /// <summary>
    /// The first <paramref name="count"/> usable slots of the field spiral beyond the houses: the
    /// field must clear the house disc and stand on dry ground no steeper than the limit.
    /// </summary>
    private static List<FieldPlacement> ComputeFields(int settlementId, Vector2 center, TerrainField terrain, SettlementLayoutSettings settings, float discRadius, int count)
    {
        var fields = new List<FieldPlacement>(count);
        var innerRadius = discRadius + settings.FieldClearance + (settings.FieldSize * HalfDiagonal);

        // Slot k of the spiral lies at FieldSpacing * sqrt(k + 0.5), so earlier slots are inside the houses.
        var firstSlot = Math.Max(0, (int)MathF.Floor(MathF.Pow(innerRadius / settings.FieldSpacing, 2f) - 0.5f));
        for (var slot = firstSlot; fields.Count < count && slot < firstSlot + (count * AttemptsPerField); slot++)
        {
            var radius = settings.FieldSpacing * MathF.Sqrt(slot + 0.5f);
            var ground = SpiralPoint(center, slot, radius);
            if (radius >= innerRadius && TryPlaceField(terrain, settings, ground, out var position, out var normal))
            {
                var yaw = FacingYaw(ground, center) + ((PresentationHash.Unit(FieldYawSalt, (ulong)settlementId, (ulong)slot) - 0.5f) * FieldYawJitter);
                fields.Add(new FieldPlacement(slot, position, yaw, normal, PresentationHash.Unit(FieldVariantSalt, (ulong)settlementId, (ulong)slot)));
            }
        }

        return fields;
    }

    /// <summary>
    /// Checks a field site: the centre and the middle of each side must be dry and the mean slope
    /// gentle. Returns the centre on the ground and the terrain normal across the field.
    /// </summary>
    private static bool TryPlaceField(TerrainField terrain, SettlementLayoutSettings settings, Vector2 ground, out Vector3 position, out Vector3 normal)
    {
        var half = settings.FieldSize * 0.5f;
        var height = terrain.SampleHeight(ground.X, ground.Y);
        var east = terrain.SampleHeight(ground.X + half, ground.Y);
        var west = terrain.SampleHeight(ground.X - half, ground.Y);
        var south = terrain.SampleHeight(ground.X, ground.Y + half);
        var north = terrain.SampleHeight(ground.X, ground.Y - half);
        var gradientX = (east - west) / settings.FieldSize;
        var gradientZ = (south - north) / settings.FieldSize;

        position = new Vector3(ground.X, height, ground.Y);
        normal = Vector3.Normalize(new Vector3(-gradientX, 1f, -gradientZ));
        var lowest = MathF.Min(height, MathF.Min(MathF.Min(east, west), MathF.Min(south, north)));
        return lowest >= settings.MinimumGroundHeight &&
            MathF.Sqrt((gradientX * gradientX) + (gradientZ * gradientZ)) <= settings.FieldMaxSlope &&
            !terrain.IsNearFreshWater(ground.X, ground.Y, half);
    }

    /// <summary>Distance from the centre to the farthest house slot, or the shrine clearance when there are none.</summary>
    private static float ComputeDiscRadius(Vector2 center, List<HutPlacement> slots, float shrineClearance)
    {
        var radius = shrineClearance;
        foreach (var slot in slots)
        {
            radius = MathF.Max(radius, Vector2.Distance(center, new Vector2(slot.Position.X, slot.Position.Z)));
        }

        return radius;
    }

    /// <summary>Ground point of a sunflower spiral slot at a given radius.</summary>
    private static Vector2 SpiralPoint(Vector2 center, int slot, float radius)
    {
        var angle = slot * GoldenAngle;
        return center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
    }

    /// <summary>Yaw that turns a placement's local +Z axis from <paramref name="from"/> towards <paramref name="to"/>.</summary>
    private static float FacingYaw(Vector2 from, Vector2 to) => MathF.Atan2(to.X - from.X, to.Y - from.Y);
}
