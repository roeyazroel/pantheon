using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Culture;

namespace Pantheon.Simulation.Hand;

/// <summary>
/// Checks and carries out what gods do with their Divine Hands (spec §55, §73, ADR-030): lifting
/// villagers, the god's Creature, a tree or a rock where the god's influence reaches, and setting it
/// down or throwing it; and taking and giving offerings (ADR-039). Villagers lifted out of danger and set down safe love the god for it;
/// villagers taken from a safe home, and the villages things are thrown at, fear it.
/// </summary>
public sealed class HandAuthority
{
    private readonly HandRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlements;
    private readonly HandCreatures _creatures;
    private readonly HandOfferings _offerings;

    /// <summary>Creates the authority.</summary>
    /// <param name="rules">Hand rules.</param>
    /// <param name="divine">Divine rules, for influence.</param>
    /// <param name="settlements">Settlement rules, for houses and danger.</param>
    public HandAuthority(HandRules rules, DivineRules divine, SettlementRules settlements)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(settlements);
        _rules = rules;
        _divine = divine;
        _settlements = settlements;
        _creatures = new HandCreatures(rules);
        _offerings = new HandOfferings(rules, divine, settlements);
    }

    /// <summary>Checks a use of the hand and, if allowed, carries it out.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The god, what it does and where.</param>
    /// <param name="events">Where the events go.</param>
    /// <returns><see cref="HandRejection.None"/> when it was done, or why not.</returns>
    public HandRejection Execute(WorldState world, HandCommand command, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);
        var god = world.Gods.Find(g => g.Id == command.GodId);
        var rejection = Check(world, god, command);
        if (rejection != HandRejection.None)
        {
            return rejection;
        }

        return command.Action switch
        {
            HandAction.PickUp => PickUp(world, god!, command.Target, events),
            HandAction.SetDown => SetDown(world, god!, god!.Held!, command.Target, thrown: false, events),
            HandAction.Throw => SetDown(world, god!, god!.Held!, command.Target, thrown: true, events),
            HandAction.TakeOffering => _offerings.Take(world, god!, command.Target),
        };
    }

    /// <summary>Whether the god may use its hand at the point at all.</summary>
    private HandRejection Check(WorldState world, GodState? god, HandCommand command)
    {
        if (god is null)
        {
            return HandRejection.UnknownGod;
        }

        if (!_rules.Enabled)
        {
            return HandRejection.NoHand;
        }

        if (!world.Regions.TryGetRegionIdAt(command.Target.X, command.Target.Z, out _))
        {
            return HandRejection.OffTheWorld;
        }

        var lifting = command.Action is HandAction.PickUp or HandAction.TakeOffering;
        if (lifting ? god.Held is not null : god.Held is null)
        {
            return lifting ? HandRejection.HandFull : HandRejection.HandEmpty;
        }

        // A god may set its own Creature down anywhere on land, as the Creature may walk anywhere (ADR-070).
        if (command.Action is HandAction.SetDown or HandAction.Throw && god.Held!.Kind == HeldKind.Creature)
        {
            return HandRejection.None;
        }

        return InfluenceField.Build(_divine, world, god.Id).At(command.Target) < _rules.MinInfluence ? HandRejection.NoInfluence : HandRejection.None;
    }

    /// <summary>Lifts the god's Creature or villagers, whichever is nearer the hand within reach, else a tree or a rock.</summary>
    private HandRejection PickUp(WorldState world, GodState god, GroundPosition target, ICollection<WorldEvent> events)
    {
        var village = Nearest(world, target, _rules.Reach);
        var reach = village is null ? _rules.Reach : MathF.Sqrt(((village.Position.X - target.X) * (village.Position.X - target.X)) + ((village.Position.Z - target.Z) * (village.Position.Z - target.Z)));
        var lifting = _creatures.Lift(world, god, target, reach);
        if (lifting != HandCreatures.Lifting.NotNear)
        {
            god.Held = lifting == HandCreatures.Lifting.Lifted ? new HeldObject { Kind = HeldKind.Creature, Amount = 1, Tick = world.Tick } : null;
            return lifting == HandCreatures.Lifting.Lifted ? HandRejection.None : HandRejection.CreatureFighting;
        }

        if (village is { Population: > 1 } settlement)
        {
            var people = Math.Min(_rules.VillagersPerGrab, Math.Max(1, (int)(settlement.Population * _rules.MaxShare)));
            var fromDanger = Disasters.DisasterModel.DangerTo(_settlements, world, settlement) >= HandModel.Danger;
            var beliefs = HandModel.Copy(settlement.Beliefs);
            settlement.Population -= people;
            if (!fromDanger)
            {
                HandModel.Impress(settlement.Beliefs, god.Id, _rules.Abduction, 1f);
                HandModel.Impress(beliefs, god.Id, _rules.Abduction, 1f);
                ConductModel.Note(world, god.Id, ConductModel.Act.Harm, 0.5f);
            }

            god.Held = new HeldObject { Kind = HeldKind.Villagers, Amount = people, SettlementId = settlement.Id, FromDanger = fromDanger, Health = settlement.Health, Beliefs = beliefs, Tick = world.Tick };
            events.Add(new VillagersLifted { Tick = world.Tick, GodId = god.Id, SettlementId = settlement.Id, People = people, FromDanger = fromDanger });
            return HandRejection.None;
        }

        world.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId);
        var region = world.Regions.Cells[regionId];
        if (region.Biome == Biome.Sea)
        {
            return HandRejection.NothingThere;
        }

        var tree = region.Vegetation >= _rules.TreeVegetation;
        if (tree)
        {
            region.Vegetation = SimMath.Clamp01(region.Vegetation - _rules.TreeCover);
            region.Timber = Math.Max(0f, region.Timber - _rules.TreeCover);
        }

        god.Held = new HeldObject { Kind = tree ? HeldKind.Tree : HeldKind.Rock, Amount = 1, Tick = world.Tick };
        return HandRejection.None;
    }

    /// <summary>Sets down or throws what the hand holds; the hand is empty after.</summary>
    private HandRejection SetDown(WorldState world, GodState god, HeldObject held, GroundPosition target, bool thrown, ICollection<WorldEvent> events)
    {
        world.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId);
        var sea = world.Regions.Cells[regionId].Biome == Biome.Sea;
        if (held.Kind == HeldKind.Offering)
        {
            // An offering is given, not hurled: thrown, it lands as gently (ADR-039).
            return _offerings.Give(world, god, held, target, events);
        }

        if (held.Kind == HeldKind.Creature)
        {
            if (sea)
            {
                return HandRejection.NotOnLand;
            }

            _creatures.Place(world, god, target, thrown);
            god.Held = null;
            return HandRejection.None;
        }

        god.Held = null;
        if (thrown)
        {
            new HandThrow(_rules, _settlements).Strike(world, god.Id, held, target, events);
        }
        else if (held.Kind == HeldKind.Villagers)
        {
            new HandCarry(_rules, _settlements).Land(world, god.Id, held, target, sea, events);
        }
        else if (held.Kind == HeldKind.Tree && !sea)
        {
            var region = world.Regions.Cells[regionId];
            region.Vegetation = SimMath.Clamp01(region.Vegetation + _rules.TreeCover);

            // A tree planted is forest that grows (ADR-033).
            region.Timber = SimMath.Clamp01(region.Timber + _rules.TreeCover);
            region.Forest = Math.Max(region.Forest, region.Timber);
            HandModel.Witness(world, god.Id, target, _rules.WitnessRadius * 0.5f, _rules.Planting);
        }

        return HandRejection.None;
    }

    /// <summary>The settlement nearest a point within a distance, if any.</summary>
    internal static SettlementState? Nearest(WorldState world, GroundPosition point, float reach)
    {
        SettlementState? nearest = null;
        var best = reach * reach;
        foreach (var settlement in world.Settlements)
        {
            var (dx, dz) = (settlement.Position.X - point.X, settlement.Position.Z - point.Z);
            var squared = (dx * dx) + (dz * dz);
            if (settlement.Population > 0 && squared <= best)
            {
                (nearest, best) = (settlement, squared);
            }
        }

        return nearest;
    }
}
