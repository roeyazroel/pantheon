using System;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Politics;

/// <summary>Why the council authority refuses a command, or <see cref="None"/> when it is carried out.</summary>
public enum CouncilRejection
{
    /// <summary>The command was carried out.</summary>
    None,

    /// <summary>The god does not exist.</summary>
    UnknownGod,

    /// <summary>The issuer does not control the god.</summary>
    NotAuthorized,

    /// <summary>The council is not in session.</summary>
    NoSession,

    /// <summary>The motion names no valid god, site, settlement or practice.</summary>
    InvalidMotion,

    /// <summary>The god already has a motion waiting.</summary>
    AlreadyTabled,
}

/// <summary>A god's intent in the council: to table a motion, or to vote in the session open now (spec §42).</summary>
/// <param name="GodId">The god.</param>
/// <param name="Motion">The motion to table; <c>null</c> to vote.</param>
/// <param name="Choice">The vote; <c>null</c> to table.</param>
public sealed record CouncilCommand(int GodId, CouncilMotion? Motion, VoteChoice? Choice);

/// <summary>The authority for the Divine Council (spec §42, §73, ADR-025): it checks tabling and voting and carries them out.</summary>
public sealed class CouncilAuthority
{
    /// <summary>Whether a command would be accepted, without changing anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <returns>Why it would be refused, or <see cref="CouncilRejection.None"/>.</returns>
    public CouncilRejection Check(WorldState world, CouncilCommand command, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (GodStanding.Find(world, command.GodId) is null)
        {
            return CouncilRejection.UnknownGod;
        }

        if (issuerGodId is { } issuer && issuer != command.GodId)
        {
            return CouncilRejection.NotAuthorized;
        }

        var council = world.Diplomacy.Council;
        if (command.Motion is { } motion)
        {
            return !IsValid(world, motion with { ProposerGodId = command.GodId }) ? CouncilRejection.InvalidMotion
                : council.Tabled.Exists(m => m.ProposerGodId == command.GodId) ? CouncilRejection.AlreadyTabled
                : CouncilRejection.None;
        }

        return command.Choice is null ? CouncilRejection.InvalidMotion
            : council.Session is null ? CouncilRejection.NoSession
            : CouncilRejection.None;
    }

    /// <summary>Checks a command and carries it out when it is allowed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <returns>Why it was refused, or <see cref="CouncilRejection.None"/>.</returns>
    public CouncilRejection Execute(WorldState world, CouncilCommand command, int? issuerGodId)
    {
        var rejection = Check(world, command, issuerGodId);
        if (rejection != CouncilRejection.None)
        {
            return rejection;
        }

        var council = world.Diplomacy.Council;
        if (command.Motion is { } motion)
        {
            council.Tabled.Add(motion with { ProposerGodId = command.GodId });
            return CouncilRejection.None;
        }

        var session = council.Session!;
        var vote = session.Votes.Find(v => v.GodId == command.GodId);
        if (vote is null)
        {
            session.Votes.Add(new CouncilVote { GodId = command.GodId, Choice = command.Choice!.Value });
        }
        else
        {
            vote.Choice = command.Choice!.Value;
        }

        return CouncilRejection.None;
    }

    /// <summary>
    /// Whether a motion names what it needs (a god other than its proposer, a site, a living
    /// settlement or a practice) and does not repeat a decree in force.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="motion">The motion.</param>
    /// <returns><c>true</c> when valid.</returns>
    public static bool IsValid(WorldState world, CouncilMotion motion)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(motion);
        var plain = motion with { ProposerGodId = null, Secret = false };
        if (world.Diplomacy.Council.Decrees.Exists(d => d.Motion with { ProposerGodId = null, Secret = false } == plain))
        {
            return false;
        }

        return motion.Kind switch
        {
            CouncilMotionKind.Condemn or CouncilMotionKind.PantheonWar => motion.TargetGodId is { } target && target != motion.ProposerGodId && GodStanding.Find(world, target) is not null,
            CouncilMotionKind.ProtectSacredLand => motion.SiteId is { } site && world.HolySites.Exists(s => s.Id == site),
            CouncilMotionKind.ProhibitPractice => motion.Practice is { } practice && Enum.IsDefined(practice),
            CouncilMotionKind.CoordinateDisasterResponse => true,
            CouncilMotionKind.NeutralTerritory => motion.SettlementId is { } id && world.Settlements.Exists(s => s.Id == id && s.Population > 0),
        };
    }
}
