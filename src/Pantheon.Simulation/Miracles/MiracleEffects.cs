using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Miracles;

/// <summary>
/// What miracles do to the world once the authority has accepted them (ADR-009, ADR-013, ADR-014,
/// ADR-021): to the settlements they reach, to the land, and to the weather, the ground, the
/// Creatures and the gods' views of each other.
/// </summary>
internal sealed class MiracleEffects
{
    private readonly SettlementRules _settlementRules;
    private readonly IActRecorder? _ledger;
    private readonly WeatherRules? _weather;
    private readonly DisasterRules? _disasters;
    private readonly CreatureRules? _creatures;
    private readonly DomainRules? _domains;

    /// <summary>Creates the effects over validated rules; any rules may be missing, and the effects that need them then do nothing.</summary>
    public MiracleEffects(SettlementRules settlementRules, IActRecorder? ledger, WeatherRules? weather, DisasterRules? disasters, CreatureRules? creatures, DomainRules? domains)
    {
        _settlementRules = settlementRules;
        _ledger = ledger;
        _weather = weather;
        _disasters = disasters;
        _creatures = creatures;
        _domains = domains;
    }

    /// <summary>
    /// Whether a miracle that acts on something in particular finds it at the target: a cloud for
    /// Redirect Storm, a rival Creature for a wound, the god's own Creature for a lesson.
    /// </summary>
    public bool HasTarget(WorldState world, int godId, MiracleDefinition miracle, GroundPosition target)
    {
        foreach (var effect in miracle.Effects)
        {
            var found = effect.Kind switch
            {
                MiracleEffectKind.Redirect => FindCloud(world, target, effect.Amount) is not null,
                MiracleEffectKind.Wound => FindRivalCreatures(world, godId, target, miracle.Radius).Count > 0,
                MiracleEffectKind.Teach => FindOwnCreature(world, godId, target, miracle.Radius) is not null,
                MiracleEffectKind.Flood => Water.WaterModel.Reach(world.Regions, target, miracle.Radius, effect.Amount).Count > 0,
                MiracleEffectKind.Judge => world.Settlements.Exists(s => Distance(s.Position, target) <= miracle.Radius && HasGuilty(world, godId, s)),
                _ => true,
            };
            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Applies the settlement effects to one settlement the miracle reaches.</summary>
    /// <returns>People killed.</returns>
    public int ApplyToSettlement(WorldState world, MiracleDefinition miracle, SettlementState settlement, int godId)
    {
        var deaths = 0;
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.AddFood:
                    var storage = SettlementEconomy.StorageOf(_settlementRules, settlement);
                    settlement.Food = Math.Max(settlement.Food, Math.Min(storage, settlement.Food + effect.Amount));
                    break;
                case MiracleEffectKind.Heal:
                    settlement.Health = Math.Min(1f, settlement.Health + effect.Amount);
                    break;
                case MiracleEffectKind.Smite:
                    var killed = Math.Min(settlement.Population, (int)MathF.Round(effect.Amount));
                    settlement.Population -= killed;
                    deaths += killed;
                    break;
                case MiracleEffectKind.Shield:
                    settlement.ShieldedUntilTick = Math.Max(settlement.ShieldedUntilTick, world.Tick + Ticks(effect.Amount));
                    break;
                case MiracleEffectKind.Populate:
                    Populate(settlement, (int)MathF.Round(effect.Amount));
                    break;
                case MiracleEffectKind.Sanctuary:
                    settlement.SanctuaryGodId = godId;
                    settlement.SanctuaryUntilTick = world.Tick + Ticks(effect.Amount);
                    break;
                case MiracleEffectKind.Terrify:
                    Terrify(world, settlement, godId, effect.Amount);
                    break;
                case MiracleEffectKind.Water:
                    var cisterns = SettlementEconomy.WaterStorageOf(_settlementRules, settlement);
                    settlement.Water = Math.Max(settlement.Water, Math.Min(cisterns, settlement.Water + effect.Amount));
                    break;
                case MiracleEffectKind.Judge:
                    deaths += Judge(world, settlement, godId, effect.Amount);
                    break;
                case MiracleEffectKind.Soak:
                case MiracleEffectKind.Flood:
                case MiracleEffectKind.Bless:
                case MiracleEffectKind.Rain:
                case MiracleEffectKind.Ignite:
                case MiracleEffectKind.Quake:
                case MiracleEffectKind.Regrow:
                case MiracleEffectKind.Redirect:
                case MiracleEffectKind.Wound:
                case MiracleEffectKind.Teach:
                case MiracleEffectKind.Discord:
                    break;
            }
        }

        return deaths;
    }

    /// <summary>Applies the land's effects to every region whose centre the miracle reaches: blessing, soaking, and regrowth after fire.</summary>
    public static void ApplyToRegions(MiracleDefinition miracle, RegionGrid grid, GroundPosition target)
    {
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind is not (MiracleEffectKind.Bless or MiracleEffectKind.Regrow or MiracleEffectKind.Soak))
            {
                continue;
            }

            foreach (var region in grid.Cells)
            {
                if (Distance(grid.GetRegionCenter(region.Id), target) > miracle.Radius)
                {
                    continue;
                }

                if (effect.Kind == MiracleEffectKind.Bless)
                {
                    region.Blessing = Math.Max(region.Blessing, SimMath.Clamp01(effect.Amount));
                }
                else if (effect.Kind == MiracleEffectKind.Soak)
                {
                    region.SoilMoisture = Math.Max(region.SoilMoisture, SimMath.Clamp01(effect.Amount));
                }
                else
                {
                    region.Fire = 0f;
                    region.Scorch *= 1f - SimMath.Clamp01(effect.Amount);
                    region.SoilMoisture = Math.Max(region.SoilMoisture, SimMath.Clamp01(effect.Amount));
                }
            }
        }
    }

    /// <summary>
    /// The effects on the world itself: Rain calls its cloud and Redirect Storm moves one (ADR-013,
    /// ADR-021), Fire sets the land alight and Earthquake shakes it (ADR-014), and the miracles of
    /// War, Knowledge and Trickery reach Creatures and gods.
    /// </summary>
    public void ApplyToWorld(WorldState world, MiracleDefinition miracle, int godId, GroundPosition target, IReadOnlyList<int> affected, ICollection<WorldEvent> events)
    {
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.Rain when _weather is not null:
                    WeatherModel.Summon(_weather, world, target, miracle.Radius, effect.Amount, godId);
                    break;
                case MiracleEffectKind.Redirect when FindCloud(world, target, effect.Amount) is { } cloud:
                    (cloud.X, cloud.Z) = (target.X, target.Z);
                    break;
                case MiracleEffectKind.Ignite when _disasters is not null:
                    Ignite(world, miracle.Radius, effect.Amount, target, events);
                    break;
                case MiracleEffectKind.Quake when _disasters is not null:
                    var magnitude = effect.Amount + (DomainModel.Holds(world, godId, GodDomain.Chaos) && _domains is not null ? _domains.Boons.ChaosMagnitude : 0f);
                    var quake = DisasterModel.StartQuake(_disasters, world.Disasters, target, magnitude, world.Tick, godId);
                    events.Add(new EarthquakeStruck { Tick = world.Tick, QuakeId = quake.Id, Epicentre = target, Magnitude = quake.Magnitude, GodId = godId });
                    break;
                case MiracleEffectKind.Wound:
                    Wound(world, godId, target, miracle.Radius, effect.Amount);
                    break;
                case MiracleEffectKind.Teach when _creatures is not null && FindOwnCreature(world, godId, target, miracle.Radius) is { } pupil:
                    if (CreatureModel.AddExperience(_creatures, pupil, effect.Amount))
                    {
                        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = pupil.Id, Change = CreatureChange.LevelledUp });
                    }

                    break;
                case MiracleEffectKind.Discord:
                    SowDiscord(world, godId, affected, (int)MathF.Round(effect.Amount));
                    break;
                case MiracleEffectKind.Flood when Water.WaterModel.Inundate(world, target, miracle.Radius, effect.Amount) is { } deepest:
                    events.Add(new FloodStarted { Tick = world.Tick, RiverId = null, GodId = godId, RegionId = deepest });
                    break;
            }
        }
    }

    /// <summary>Whether a god wronged the caster: they are at war, or the caster bears it a grudge of at least the Justice domain's measure (ADR-022).</summary>
    public bool IsGuilty(WorldState world, int casterId, int godId)
    {
        if (godId == casterId || DiplomacyModel.AreAllied(world, casterId, godId))
        {
            return false;
        }

        if (DiplomacyModel.AreAtWar(world, casterId, godId))
        {
            return true;
        }

        return _domains is not null && DiplomacyModel.FindRelation(world, casterId, godId) is { } view && DiplomacyModel.Grudge(view) >= _domains.Boons.JusticeGuiltGrudge;
    }

    /// <summary>Whether a settlement's people follow, in any number, a god who wronged the caster.</summary>
    public bool HasGuilty(WorldState world, int casterId, SettlementState settlement)
    {
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > 0.05f && IsGuilty(world, casterId, belief.GodId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The followers of the gods who wronged the caster are punished: some die, and the rest doubt their god.</summary>
    /// <returns>People killed.</returns>
    private int Judge(WorldState world, SettlementState settlement, int casterId, float amount)
    {
        var deaths = 0;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence <= 0.05f || !IsGuilty(world, casterId, belief.GodId))
            {
                continue;
            }

            var killed = Math.Min(settlement.Population, (int)MathF.Round(amount * settlement.Population * SimMath.Clamp01(belief.Adherence)));
            settlement.Population -= killed;
            deaths += killed;
            belief.Faith = BeliefModel.ApplyChange(belief.Faith, -amount);
            belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, amount);
        }

        return deaths;
    }

    /// <summary>The strongest rain or storm cloud within a distance of a point, if any.</summary>
    public WeatherCell? FindCloud(WorldState world, GroundPosition target, float range)
    {
        if (_weather is null)
        {
            return null;
        }

        WeatherCell? best = null;
        var bestStrength = 0f;
        foreach (var cell in world.Weather.Cells)
        {
            var strength = cell.Precipitation + cell.ElectricalActivity + cell.Wind;
            if (cell.Precipitation > 0.05f && strength > bestStrength && Distance(new GroundPosition(cell.X, cell.Z), target) <= range)
            {
                bestStrength = strength;
                best = cell;
            }
        }

        return best;
    }

    /// <summary>The Creatures of every other god except allies within a radius of a point, if not banished.</summary>
    public static List<CreatureState> FindRivalCreatures(WorldState world, int godId, GroundPosition target, float radius) =>
        world.Creatures.FindAll(c => c.GodId != godId && c.Activity.Phase != CreaturePhase.Banished && !DiplomacyModel.AreAllied(world, godId, c.GodId) && Distance(c.Position, target) <= radius);

    /// <summary>The god's own Creature, if it stands within a radius of a point.</summary>
    public static CreatureState? FindOwnCreature(WorldState world, int godId, GroundPosition target, float radius) =>
        world.Creatures.Find(c => c.GodId == godId && c.Activity.Phase != CreaturePhase.Banished && Distance(c.Position, target) <= radius);

    /// <summary>
    /// Brings newcomers where there are homes for them. They are born into the village and share its
    /// beliefs, so the gift grows a people rather than converting it; its witnesses' gratitude is the
    /// god's gain.
    /// </summary>
    private static void Populate(SettlementState settlement, int people)
    {
        settlement.Population += Math.Min(people, Math.Max(0, settlement.Housing - settlement.Population));
    }

    /// <summary>Faith in every other god the people follow, except the caster's allies, gives way to doubt.</summary>
    private static void Terrify(WorldState world, SettlementState settlement, int godId, float amount)
    {
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId != godId && !DiplomacyModel.AreAllied(world, godId, belief.GodId))
            {
                belief.Faith = BeliefModel.ApplyChange(belief.Faith, -amount);
                belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, amount);
            }
        }
    }

    /// <summary>Wounds every rival Creature in reach; the harm is divided by its species' toughness, and the god whose Creature it is notes it.</summary>
    private void Wound(WorldState world, int godId, GroundPosition target, float radius, float amount)
    {
        foreach (var creature in FindRivalCreatures(world, godId, target, radius))
        {
            var toughness = _creatures is null ? 1f : CreatureModel.SpeciesOf(_creatures, creature).Toughness;
            creature.Health = SimMath.Clamp01(creature.Health - (amount / toughness));
            _ledger?.Record(world, creature.GodId, godId, DiplomaticAct.Harmed, null);
        }
    }

    /// <summary>Each pair of gods followed in the reached settlements, other than the caster and its allies, remembers wrongs neither did.</summary>
    private void SowDiscord(WorldState world, int godId, IReadOnlyList<int> affected, int wrongs)
    {
        if (_ledger is null || wrongs <= 0)
        {
            return;
        }

        var gods = new SortedSet<int>();
        foreach (var settlement in world.Settlements)
        {
            if (!Contains(affected, settlement.Id))
            {
                continue;
            }

            foreach (var belief in settlement.Beliefs)
            {
                if (belief.GodId != godId && belief.Adherence > 0.05f && !DiplomacyModel.AreAllied(world, godId, belief.GodId))
                {
                    gods.Add(belief.GodId);
                }
            }
        }

        foreach (var a in gods)
        {
            foreach (var b in gods)
            {
                for (var i = 0; i < wrongs && a != b; i++)
                {
                    _ledger.Record(world, a, b, DiplomaticAct.Harmed, null);
                }
            }
        }
    }

    /// <summary>Sets every region whose centre lies within the radius alight, where there is fuel; the target's region always counts.</summary>
    private void Ignite(WorldState world, float radius, float intensity, GroundPosition target, ICollection<WorldEvent> events)
    {
        var grid = world.Regions;
        grid.TryGetRegionIdAt(target.X, target.Z, out var targetRegion);
        foreach (var region in grid.Cells)
        {
            if ((region.Id == targetRegion || Distance(grid.GetRegionCenter(region.Id), target) <= radius)
                && DisasterModel.Ignite(_disasters!, region, intensity))
            {
                events.Add(new WildfireStarted { Tick = world.Tick, RegionId = region.Id, Cause = FireCause.Miracle });
            }
        }
    }

    /// <summary>Whether a list holds an id.</summary>
    private static bool Contains(IReadOnlyList<int> ids, int id)
    {
        foreach (var each in ids)
        {
            if (each == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Simulated seconds as ticks.</summary>
    private static long Ticks(float seconds) => (long)MathF.Round(seconds * SimulationTime.TicksPerSecond);

    /// <summary>Distance between two ground positions in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
